﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Data;
using System.Text;
using System.Windows.Forms;

using MPTagThat.Core;
using Un4seen.Bass;
using Un4seen.Bass.Misc;

namespace MPTagThat.Player
{
  public partial class PlayerControl : UserControl
  {
    #region Variables
    private delegate void ThreadSafeSetText(MPTagThat.Core.WinControls.MPTLabel label, string text);
    private SYNCPROC PlaybackEndProcDelegate = null;          // SyncProc called to indicate the song has ended
    private ILogger log = ServiceScope.Get<ILogger>();
    private ILocalisation localisation = ServiceScope.Get<ILocalisation>();
    private SortableBindingList<PlayListData> _playList;
    private int _currentStartIndex = -1;
    private int _currentIndexPlaying = -1;
    private int _stream = 0;
    private int _syncHandleEnd = 0;
    private Visuals _vis = new Visuals();                     // visuals class instance
    private int _updateInterval = 50;                         // 50ms
    private int _tickCounter = 0;
    private BASSTimer _updateTimer = null;
    private int _specIdx = 15;
    private int _voicePrintIdx = 0;
    private string _currentTheme = "";
    private Image _imgPlay = null;
    private Image _imgPause = null;
    private double _songLength = 0.0;
    private PlayList _playListForm = null;
    private bool _playListOpen = false;
    private int _mainFormWidth = 0;
    #endregion

    #region ctor
    public PlayerControl()
    {
      InitializeComponent();

      IMessageQueue queueMessage = ServiceScope.Get<IMessageBroker>().GetOrCreate("message");
      queueMessage.OnMessageReceive += new MessageReceivedHandler(OnMessageReceive);
    }
    #endregion

    #region Properties
     /// <summary>
    /// Returns the Playlist
    /// </summary>
    public SortableBindingList<PlayListData> PlayList
    {
      get { return _playList; }
    }

    /// <summary>
    /// Returns the PlayList Form
    /// </summary>
    public PlayList PlayListForm
    {
      get { return _playListForm; }
    }
    #endregion

    #region Form Load
    private void OnLoad(object sender, EventArgs e)
    {
      _playListForm = new PlayList(this);

      _specIdx = Options.MainSettings.PlayerSpectrumIndex;
      _playList = new SortableBindingList<PlayListData>();

      lbTitleText.Text = "";
      lbArtistText.Text = "";
      lbAlbumText.Text = "";

      playBackSlider.Enabled = false;

      // create a secure timer
      _updateTimer = new BASSTimer(_updateInterval);
      _updateTimer.Tick += new EventHandler(timerUpdate_Tick);
    }
    #endregion

    #region Public Methods
    /// <summary>
    /// Starts Playback of the given PlayList Index
    /// </summary>
    /// <param name="index"></param>
    public void Play(int index)
    {
      _currentStartIndex = index;
      buttonPlay_Click(null, new EventArgs());
    }

    /// <summary>
    /// Stop Playback of Stream
    /// </summary>
    public void Stop()
    {
      if (Bass.BASS_ChannelIsActive(_stream) == BASSActive.BASS_ACTIVE_PLAYING ||
          Bass.BASS_ChannelIsActive(_stream) == BASSActive.BASS_ACTIVE_PAUSED)
      {
        Bass.BASS_ChannelStop(_stream);
        Bass.BASS_ChannelRemoveSync(_stream, _syncHandleEnd);
      }
      playBackSlider.Enabled = false;
    }

    /// <summary>
    /// Moves the Playlist panel, when it is docked and the mainform is resized
    /// </summary>
    public void MovePlayList()
    {
      if (_playListOpen)
      {
        _playListForm.Height = this.Parent.Parent.Height;
        _playListForm.Location = new Point(this.Parent.Parent.Location.X + this.Parent.Parent.Width, this.Parent.Parent.Location.Y);
      }
    }
    #endregion

    #region Private Methods
    private void timerUpdate_Tick(object sender, System.EventArgs e)
    {
      // here we gather info about the stream, when it is playing...
      if (Bass.BASS_ChannelIsActive(_stream) != BASSActive.BASS_ACTIVE_PLAYING &&
          Bass.BASS_ChannelIsActive(_stream) != BASSActive.BASS_ACTIVE_PAUSED)
      {
        // the stream is NOT playing anymore...
        _updateTimer.Stop();
        playBackSlider.Value = 0;
        this.pictureBoxSpectrum.Image = null;
        return;
      }

      // from here on, the stream is for sure playing...
      _tickCounter++;

      if (_tickCounter == 5)
      {
        // display the position every 250ms (since timer is 50ms)
        _tickCounter = 0;
        double elapsedtime = Bass.BASS_ChannelBytes2Seconds(_stream, Bass.BASS_ChannelGetPosition(_stream)); // the elapsed time length
        string timeFormatted = String.Format("{0:#0:00}", Utils.FixTimespan(elapsedtime, "MMSS"));

        // Move the PlaybackSlider
        playBackSlider.Value = (int)(Math.Round((elapsedtime / _songLength), 2) * 100.0);

        // Now Paint the time to the Picturebox
        Graphics g = Graphics.FromHwnd(this.pictureBoxTime.Handle);
        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        g.Clear(Color.Black);
        g.DrawString(timeFormatted, new Font("Verdana", 18), new SolidBrush(Color.CornflowerBlue), 0, 0);
        g.Dispose();

      }

      // update spectrum
      DrawSpectrum();
    }
    #endregion

    #region Events
    private void buttonPlay_Click(object sender, EventArgs e)
    {
      Util.EnterMethod(Util.GetCallingMethod());
      
      _updateTimer.Stop();

      // Are we still on the same file, then it is a Play / Pause situation
      if (_currentIndexPlaying == _currentStartIndex)
      {
        if (Bass.BASS_ChannelIsActive(_stream) == BASSActive.BASS_ACTIVE_PLAYING)
        {
          pictureBoxPlayPause.Image = _imgPlay;
          Bass.BASS_ChannelPause(_stream);
          return;
        }

        if (Bass.BASS_ChannelIsActive(_stream) == BASSActive.BASS_ACTIVE_PAUSED)
        {
          pictureBoxPlayPause.Image = _imgPause;
          _updateTimer.Start();
          Bass.BASS_ChannelPlay(_stream, false);
          return;
        }
      }

      if (Bass.BASS_GetDevice() == -1)
      {
        log.Info("Player: Bass not Initialised. Doing Initialisation");
        if (!Bass.BASS_Init(-1, 44100, BASSInit.BASS_DEVICE_DEFAULT, IntPtr.Zero, null))
        {
          int error = (int)Bass.BASS_ErrorGetCode();
          log.Error("Player: Error Init Bass: {0}", Enum.GetName(typeof(BASSError), error));
          return;
        }
      }

      if (_playList.Count == 0)
      {
        log.Info("Player: No Items in Playlist.");
        return;
      }

      // Did we reach the end of the list
      if (_currentStartIndex > _playList.Count - 1)
      {
        log.Info("Player: Reached end of Playlist");
        Stop();
        return;
      }

      DataGridViewSelectedRowCollection selectedRows = _playListForm.PlayListGrid.SelectedRows;
      if (_currentStartIndex == -1)
      {
        if (selectedRows.Count == 0)
          _currentStartIndex = 0;
        else
          _currentStartIndex = selectedRows[0].Index;
      }

      // Stop the Current Stream
      Stop();

      if ((_stream = Bass.BASS_StreamCreateFile(_playList[_currentStartIndex].FileName, 0, 0, BASSFlag.BASS_DEFAULT | BASSFlag.BASS_SAMPLE_FLOAT | BASSFlag.BASS_STREAM_AUTOFREE | BASSFlag.BASS_STREAM_PRESCAN)) == 0)
      {
        int error = (int)Bass.BASS_ErrorGetCode();
        log.Error("Player: Error Creating stream for {0}: {1}", _playList[_currentStartIndex].FileName, Enum.GetName(typeof(BASSError), error));
        return;
      }

      RegisterPlaybackEvents();

      _songLength = Bass.BASS_ChannelBytes2Seconds(_stream, Bass.BASS_ChannelGetLength(_stream, BASSMode.BASS_POS_BYTES));
      if (!Bass.BASS_ChannelPlay(_stream, true))
      {
        int error = (int)Bass.BASS_ErrorGetCode();
        log.Error("Player: Error Playing File {0}: {1}", _playList[_currentStartIndex].FileName, Enum.GetName(typeof(BASSError), error));
        return;
      }

      _currentIndexPlaying = _currentStartIndex;
      playBackSlider.Enabled = true;
      _playListForm.PlayListGrid.ClearSelection();
      _playListForm.PlayListGrid.Rows[_currentStartIndex].Selected = true;

      pictureBoxPlayPause.Image = _imgPause;
      _updateTimer.Start();
      SetText(this.lbTitleText,string.Format("{0} ({1})",_playList[_currentStartIndex].Title, _playList[_currentStartIndex].Duration));
      SetText(this.lbArtistText, _playList[_currentStartIndex].Artist);
      SetText(this.lbAlbumText, _playList[_currentStartIndex].Album);

      Util.LeaveMethod(Util.GetCallingMethod());
    }

    /// <summary>
    /// Play Previous file
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    private void buttonPrev_Click(object sender, EventArgs e)
    {
      Util.EnterMethod(Util.GetCallingMethod());
      _currentStartIndex--;
      buttonPlay_Click(null, new EventArgs());
      Util.LeaveMethod(Util.GetCallingMethod());
    }

    /// <summary>
    /// Play Next file
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    private void buttonNext_Click(object sender, EventArgs e)
    {
      Util.EnterMethod(Util.GetCallingMethod());
      _currentStartIndex++;
      buttonPlay_Click(null, new EventArgs());
      Util.LeaveMethod(Util.GetCallingMethod());
    }

    /// <summary>
    /// The slider was scrolled. Position within the file
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    private void playBackSlider_Scroll(object sender, ScrollEventArgs e)
    {
      double newPos = _songLength * (double)playBackSlider.Value / 100.00;
      Bass.BASS_ChannelSetPosition(_stream, newPos);
    }

    /// <summary>
    /// Sets the Text of a label
    /// </summary>
    /// <param name="label"></param>
    private void SetText(MPTagThat.Core.WinControls.MPTLabel label, string text)
    {
      if (this.InvokeRequired)
      {
        ThreadSafeSetText d = new ThreadSafeSetText(SetText);
        this.Invoke(d, new object[] { label, text});
        return;
      }
      label.Text = text;
    }

    /// <summary>
    /// Show / Hide PlayList Panel
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    private void btnPlayList_Click(object sender, EventArgs e)
    {
      if (_playListOpen)
      {
        // Enlarge the Main Form to it's original size
        this.Parent.Parent.Width = _mainFormWidth;
        _playListOpen = false;
        _playListForm.Hide();
      }
      else
      {
        // Set the height of the Playlist equals to the height of the main Window
        // Decrease the width of the main window and set the playlist to the right
        _mainFormWidth = this.Parent.Parent.Width;
        _playListForm.Height = this.Parent.Parent.Height;

        if ((_mainFormWidth + _playListForm.Width + this.Parent.Parent.Location.X) > Screen.PrimaryScreen.Bounds.Width)
        {
          this.Parent.Parent.Width -= _playListForm.Width;
        }
        _playListForm.Location = new Point(this.Parent.Parent.Location.X + this.Parent.Parent.Width, this.Parent.Parent.Location.Y);

        _playListOpen = true;
        _playListForm.Show();
      }
    }
    #endregion

    #region SyncProcs
    /// <summary>
    /// Register the Playback Events
    /// </summary>
    private void RegisterPlaybackEvents()
    {
      log.Debug("Player: Register Stream Playback events");

      PlaybackEndProcDelegate = new SYNCPROC(PlaybackEndProc);
      _syncHandleEnd = RegisterPlaybackEndEvent();

      log.Debug("Player: Finished Registering Stream Playback events");
    }

    /// <summary>
    /// Register the Playback end Event
    /// </summary>
    private int RegisterPlaybackEndEvent()
    {
      int syncHandle = 0;

      syncHandle = Bass.BASS_ChannelSetSync(_stream,
            BASSSync.BASS_SYNC_END,
            0, PlaybackEndProcDelegate,
            IntPtr.Zero);

      if (syncHandle == 0)
      {
        int error = (int)Bass.BASS_ErrorGetCode();
        log.Error("Player: RegisterPlaybackEndEvent of stream {0} failed with error {1}", _stream, error);
      }

      return syncHandle;
    }

    /// <summary>
    /// Playback end Procedure
    /// </summary>
    private void PlaybackEndProc(int syncHandle, int channel, int data, IntPtr user)
    {
      log.Debug("BASS: End of stream {0}", channel);
      _stream = 0;

      _currentStartIndex++;
      buttonPlay_Click(null, new EventArgs());
    }
    #endregion

    #region Spectrum
    private void pictureBoxSpectrum_MouseDown(object sender, MouseEventArgs e)
    {
      if (e.Button == MouseButtons.Left)
        _specIdx++;
      else
        _specIdx--;

      if (_specIdx > 15)
        _specIdx = 0;
      if (_specIdx < 0)
        _specIdx = 15;

      Options.MainSettings.PlayerSpectrumIndex = _specIdx;
      this.pictureBoxSpectrum.Image = null;
      _vis.ClearPeaks();
    }

    private void DrawSpectrum()
    {
      switch (_specIdx)
      {
        // normal spectrum (width = resolution)
        case 0:
          this.pictureBoxSpectrum.Image = _vis.CreateSpectrum(_stream, this.pictureBoxSpectrum.Width, this.pictureBoxSpectrum.Height, Color.Lime, Color.Red, Color.Black, false, false, false);
          break;
        // normal spectrum (full resolution)
        case 1:
          this.pictureBoxSpectrum.Image = _vis.CreateSpectrum(_stream, this.pictureBoxSpectrum.Width, this.pictureBoxSpectrum.Height, Color.SteelBlue, Color.Pink, Color.Black, false, true, true);
          break;
        // line spectrum (width = resolution)
        case 2:
          this.pictureBoxSpectrum.Image = _vis.CreateSpectrumLine(_stream, this.pictureBoxSpectrum.Width, this.pictureBoxSpectrum.Height, Color.Lime, Color.Red, Color.Black, 2, 2, false, false, false);
          break;
        // line spectrum (full resolution)
        case 3:
          this.pictureBoxSpectrum.Image = _vis.CreateSpectrumLine(_stream, this.pictureBoxSpectrum.Width, this.pictureBoxSpectrum.Height, Color.SteelBlue, Color.Pink, Color.Black, 16, 4, false, true, true);
          break;
        // ellipse spectrum (width = resolution)
        case 4:
          this.pictureBoxSpectrum.Image = _vis.CreateSpectrumEllipse(_stream, this.pictureBoxSpectrum.Width, this.pictureBoxSpectrum.Height, Color.Lime, Color.Red, Color.Black, 1, 2, false, false, false);
          break;
        // ellipse spectrum (full resolution)
        case 5:
          this.pictureBoxSpectrum.Image = _vis.CreateSpectrumEllipse(_stream, this.pictureBoxSpectrum.Width, this.pictureBoxSpectrum.Height, Color.SteelBlue, Color.Pink, Color.Black, 2, 4, false, true, true);
          break;
        // dot spectrum (width = resolution)
        case 6:
          this.pictureBoxSpectrum.Image = _vis.CreateSpectrumDot(_stream, this.pictureBoxSpectrum.Width, this.pictureBoxSpectrum.Height, Color.Lime, Color.Red, Color.Black, 1, 0, false, false, false);
          break;
        // dot spectrum (full resolution)
        case 7:
          this.pictureBoxSpectrum.Image = _vis.CreateSpectrumDot(_stream, this.pictureBoxSpectrum.Width, this.pictureBoxSpectrum.Height, Color.SteelBlue, Color.Pink, Color.Black, 2, 1, false, false, true);
          break;
        // peak spectrum (width = resolution)
        case 8:
          this.pictureBoxSpectrum.Image = _vis.CreateSpectrumLinePeak(_stream, this.pictureBoxSpectrum.Width, this.pictureBoxSpectrum.Height, Color.SeaGreen, Color.LightGreen, Color.Orange, Color.Black, 2, 1, 2, 10, false, false, false);
          break;
        // peak spectrum (full resolution)
        case 9:
          this.pictureBoxSpectrum.Image = _vis.CreateSpectrumLinePeak(_stream, this.pictureBoxSpectrum.Width, this.pictureBoxSpectrum.Height, Color.GreenYellow, Color.RoyalBlue, Color.DarkOrange, Color.Black, 23, 5, 3, 5, false, true, true);
          break;
        // wave spectrum (width = resolution)
        case 10:
          this.pictureBoxSpectrum.Image = _vis.CreateSpectrumWave(_stream, this.pictureBoxSpectrum.Width, this.pictureBoxSpectrum.Height, Color.Yellow, Color.Orange, Color.Black, 1, false, false, false);
          break;
        // dancing beans spectrum (width = resolution)
        case 11:
          this.pictureBoxSpectrum.Image = _vis.CreateSpectrumBean(_stream, this.pictureBoxSpectrum.Width, this.pictureBoxSpectrum.Height, Color.Chocolate, Color.DarkGoldenrod, Color.Black, 4, false, false, true);
          break;
        // dancing text spectrum (width = resolution)
        case 12:
          this.pictureBoxSpectrum.Image = _vis.CreateSpectrumText(_stream, this.pictureBoxSpectrum.Width, this.pictureBoxSpectrum.Height, Color.White, Color.Tomato, Color.Black, "MPTagThat is Great", false, false, true);
          break;
        // frequency detection
        case 13:
          float amp = _vis.DetectFrequency(_stream, 10, 500, true);
          if (amp > 0.3)
            this.pictureBoxSpectrum.BackColor = Color.Red;
          else
            this.pictureBoxSpectrum.BackColor = Color.Black;
          break;
        // 3D voice print
        case 14:
          // we need to draw directly directly on the picture box...
          // normally you would encapsulate this in your own custom control
          Graphics g = Graphics.FromHwnd(this.pictureBoxSpectrum.Handle);
          g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
          _vis.CreateSpectrum3DVoicePrint(_stream, g, new Rectangle(0, 0, this.pictureBoxSpectrum.Width, this.pictureBoxSpectrum.Height), Color.Black, Color.White, _voicePrintIdx, false, false);
          g.Dispose();
          // next call will be at the next pos
          _voicePrintIdx++;
          if (_voicePrintIdx > this.pictureBoxSpectrum.Width - 1)
            _voicePrintIdx = 0;
          break;
        // WaveForm
        case 15:
          this.pictureBoxSpectrum.Image = _vis.CreateWaveForm(_stream, this.pictureBoxSpectrum.Width, this.pictureBoxSpectrum.Height, Color.Green, Color.Red, Color.Gray, Color.Black, 1, true, false, true);
          break;
      }
    }
    #endregion

    #region Message Handling
    /// <summary>
    /// Handle Messages
    /// </summary>
    /// <param name="message"></param>
    private void OnMessageReceive(QueueMessage message)
    {
      string action = message.MessageData["action"] as string;

      switch (action.ToLower())
      {
        // Message sent, when a Theme is changing
        case "themechanged":
          {
            if (ServiceScope.Get<IThemeManager>().CurrentTheme.ThemeName == _currentTheme)
            {
              return;
            }

            _currentTheme = ServiceScope.Get<IThemeManager>().CurrentTheme.ThemeName;
            string imageDir = System.IO.Path.Combine(Application.StartupPath, "Themes\\" + _currentTheme); 
            _imgPlay = Image.FromFile(System.IO.Path.Combine(imageDir, "Play_btn.png"));
            _imgPause = Image.FromFile(System.IO.Path.Combine(imageDir, "Pause_btn.png"));
            break;
          }
      }
    }
    #endregion
  }
}
