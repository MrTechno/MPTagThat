#region Copyright (C) 2005-2009 Team MediaPortal

/* 
 *  Copyright (C) 2005-2009 Team MediaPortal
 *  http://www.team-mediaportal.com
 *
 *  This Program is free software; you can redistribute it and/or modify
 *  it under the terms of the GNU General Public License as published by
 *  the Free Software Foundation; either version 2, or (at your option)
 *  any later version.
 *   
 *  This Program is distributed in the hope that it will be useful,
 *  but WITHOUT ANY WARRANTY; without even the implied warranty of
 *  MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
 *  GNU General Public License for more details.
 *   
 *  You should have received a copy of the GNU General Public License
 *  along with GNU Make; see the file COPYING.  If not, write to
 *  the Free Software Foundation, 675 Mass Ave, Cambridge, MA 02139, USA. 
 *  http://www.gnu.org/copyleft/gpl.html
 *
 */

#endregion

#**********************************************************************************************************#
#
#   For building the installer on your own you need:
#       1. Latest NSIS version from http://nsis.sourceforge.net/Download
#
#**********************************************************************************************************#

Name "MPTagThat"

SetCompressor /SOLID lzma

# Defines
!define REGKEY "SOFTWARE\Team MediaPortal\$(^Name)"
!define VERSION 3.0.0
!define COMPANY "Team MediaPortal"
!define AUTHOR "Helmut Wahrmann"
!define URL www.team-mediaportal.com

# MUI defines
!define MUI_ICON "MPTagThat.ico"
!define MUI_FINISHPAGE_NOAUTOCLOSE
!define MUI_FINISHPAGE_RUN            "$INSTDIR\MPTagThat.exe"
!define MUI_FINISHPAGE_RUN_TEXT       "Run MPTagThat after install"
!define MUI_STARTMENUPAGE_REGISTRY_ROOT HKLM
!define MUI_STARTMENUPAGE_NODISABLE
!define MUI_STARTMENUPAGE_REGISTRY_KEY "${REGKEY}"
!define MUI_STARTMENUPAGE_REGISTRY_VALUENAME StartMenuGroup
!define MUI_STARTMENUPAGE_DEFAULTFOLDER "Team MediaPortal\MPTagThat"
!define MUI_UNICON "MPTagThat.ico"
!define MUI_UNFINISHPAGE_NOAUTOCLOSE

# Included files
!include Sections.nsh
!include MUI2.nsh
!include LogicLib.nsh
!include InstallOptions.nsh
!include "DotNetVer.nsh"

# Variables
Var StartMenuGroup
Var CreateStartMenu
Var CreateDeskTopShortCut
Var CreateExplorerMenu

# Installer pages
!insertmacro MUI_PAGE_WELCOME
!insertmacro MUI_PAGE_DIRECTORY
Page custom Options OptionsValidate
!define MUI_PAGE_CUSTOMFUNCTION_PRE StartMenu_Pre
!insertmacro MUI_PAGE_STARTMENU Application $StartMenuGroup
!insertmacro MUI_PAGE_INSTFILES
!insertmacro MUI_PAGE_FINISH
!insertmacro MUI_UNPAGE_CONFIRM
!insertmacro MUI_UNPAGE_INSTFILES

# Installer languages
!insertmacro MUI_LANGUAGE English

# Installer attributes
OutFile MPTagThat_setup.exe
InstallDir "$PROGRAMFILES\Team MediaPortal\MPTagThat"
CRCCheck on
XPStyle on
ShowInstDetails show
VIProductVersion 3.0.0.0
VIAddVersionKey ProductName "MPTagThat the MediaPortal Tag Editor"
VIAddVersionKey ProductVersion "${VERSION}"
VIAddVersionKey CompanyName "${COMPANY}"
VIAddVersionKey CompanyWebsite "${URL}"
VIAddVersionKey Author "${AUTHOR}"
VIAddVersionKey FileVersion "${VERSION}"
VIAddVersionKey FileDescription ""
VIAddVersionKey LegalCopyright ""
InstallDirRegKey HKLM "${REGKEY}" Path
ShowUninstDetails show

BrandingText  "$(^Name) ${VERSION} by ${AUTHOR}"

# Installer sections
Section -Main SEC0000
	${IfNOT} ${HasDotNet4.0}
		MessageBox MB_OK "Missing installation of Microsoft .NET Framework 4.0. Please install first."
		Abort "Missing Microsoft .NET Framework 4.0"
		Quit
	${EndIf}

	DetailPrint "Microsoft .NET Framework 4.0 installed."
	
    SetOverwrite on
    
    # Bin Dir including external binaries
    SetOutPath $INSTDIR\bin
    File /r /x .svn ..\MPTagThat.Base\bin\*
    File ..\LyricsEngine\bin\Release\LyricsEngine.dll
    File ..\LyricsEngine\bin\Release\LyricsEngine.dll.config
	File ..\taglib-sharp\bin\Release\taglib-sharp.dll
	File ..\gain\bin\Release\gain.dll
	File ..\FreeImage\bin\Release\FreeImageNET.dll
    
	# Docs Dir
	SetOutPath $INSTDIR\Docs
	File /r /x .svn ..\MPTagThat.Base\Docs\*

    # FileIcons
    SetOutPath $INSTDIR\FileIcons
    File /r /x .svn ..\MPTagThat.Base\FileIcons\*

	
    # Language Dir
    SetOutPath $INSTDIR\Language
    File /r /x .svn ..\MPTagThat.Base\Language\*
    
    # Scripts
    SetOutPath $INSTDIR\Scripts
    File /r /x .svn ..\MPTagThat.Base\Scripts\*
    
    # Themes
    SetOutPath $INSTDIR\Themes
    File /r /x .svn ..\MPTagThat.Base\Themes\*
    
    # Base Files
    SetOutPath $INSTDIR
    File ..\MPTagThat.Base\Config.xml
    File ..\MPTagThat.Base\libfftw3-3.dll
    File ..\MPTagThat.Base\libofa.dll
    File ..\MPTagThat\bin\Release\MPTagThat.exe
    File ..\MPTagThat\bin\Release\MPTagThat.exe.config
    File ..\MPTagThat\bin\Release\MPTagThat.Core.dll
    WriteRegStr HKLM "${REGKEY}\Components" Main 1
SectionEnd

Section -post SEC0001
    WriteRegStr HKLM "${REGKEY}" Path $INSTDIR
    SetOutPath $INSTDIR
    WriteUninstaller $INSTDIR\uninstall.exe
    
	${IF} $CreateDeskTopShortCut == 1
		CreateShortCut "$DESKTOP\$(^Name).lnk" "$INSTDIR\MpTagThat.exe" "" "$INSTDIR\MpTagThat.exe"   0 "" "" "MPTagThat"
    ${ENDIF}
	
	${IF} $CreateStartMenu == 1
		!insertmacro MUI_STARTMENU_WRITE_BEGIN Application
		CreateShortCut "$SMPROGRAMS\$StartMenuGroup\$(^Name).lnk" "$INSTDIR\MpTagThat.exe" "" "$INSTDIR\MpTagThat.exe" 0 "" "" "MPTagThat" 
		CreateShortCut "$SMPROGRAMS\$StartMenuGroup\User Files.lnk" "$AppData\$(^Name)" "" "$AppData\$(^Name)" 0 "" "" "Browse your config files, logs, ..."
		CreateShortCut "$SMPROGRAMS\$StartMenuGroup\Uninstall $(^Name).lnk" "$INSTDIR\uninstall.exe"
		!insertmacro MUI_STARTMENU_WRITE_END
	${ENDIF}
	
    WriteRegStr HKLM "SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\$(^Name)" DisplayName "$(^Name)"
    WriteRegStr HKLM "SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\$(^Name)" DisplayVersion "${VERSION}"
    WriteRegStr HKLM "SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\$(^Name)" Publisher "${AUTHOR}"
    WriteRegStr HKLM "SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\$(^Name)" URLInfoAbout "${URL}"
    WriteRegStr HKLM "SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\$(^Name)" DisplayIcon $INSTDIR\uninstall.exe
    WriteRegStr HKLM "SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\$(^Name)" UninstallString $INSTDIR\uninstall.exe
    WriteRegDWORD HKLM "SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\$(^Name)" NoModify 1
    WriteRegDWORD HKLM "SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\$(^Name)" NoRepair 1
	
	# Folder Context Menu
	${IF} $CreateExplorerMenu == 1
		WriteRegStr HKCR "Folder\shell\Open folder in MPTagThat\command" "" '"$INSTDIR\MPTagThat.exe" "/folder=%L"'
	${ENDIF}
	
SectionEnd

# Macro for selecting uninstaller sections
!macro SELECT_UNSECTION SECTION_NAME UNSECTION_ID
    Push $R0
    ReadRegStr $R0 HKLM "${REGKEY}\Components" "${SECTION_NAME}"
    StrCmp $R0 1 0 next${UNSECTION_ID}
    !insertmacro SelectSection "${UNSECTION_ID}"
    GoTo done${UNSECTION_ID}
next${UNSECTION_ID}:
    !insertmacro UnselectSection "${UNSECTION_ID}"
done${UNSECTION_ID}:
    Pop $R0
!macroend

# Uninstaller sections
Section /o -un.Main UNSEC0000
    RmDir /r /REBOOTOK $INSTDIR\bin
    RmDir /r /REBOOTOK $INSTDIR\Docs
    RmDir /r /REBOOTOK $INSTDIR\FileIcons
    RmDir /r /REBOOTOK $INSTDIR\Language
    RmDir /r /REBOOTOK $INSTDIR\Scripts
    RmDir /r /REBOOTOK $INSTDIR\Themes
    Delete /REBOOTOK $INSTDIR\Config.xml
    Delete /REBOOTOK $INSTDIR\libfftw3-3.dll
    Delete /REBOOTOK $INSTDIR\libofa.dll
    Delete /REBOOTOK $INSTDIR\MPTagThat.exe
    Delete /REBOOTOK $INSTDIR\MPTagThat.exe.config
    Delete /REBOOTOK $INSTDIR\MPTagThat.Core.dll
    Delete /REBOOTOK $INSTDIR\MPLanguageTool.exe
    rmDir /REBOOTOK $INSTDIR
    
    DeleteRegValue HKLM "${REGKEY}\Components" Main
	DeleteRegKey HKCR "Folder\shell\Open folder in MPTagThat"
SectionEnd

Section -un.post UNSEC0001
    DeleteRegKey HKLM "SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\$(^Name)"
    Delete /REBOOTOK "$SMPROGRAMS\$StartMenuGroup\$(^Name).lnk"
    Delete /REBOOTOK "$SMPROGRAMS\$StartMenuGroup\Uninstall $(^Name).lnk"
    Delete /REBOOTOK "$SMPROGRAMS\$StartMenuGroup\User Files.lnk"
    Delete "$DESKTOP\$(^Name).lnk"
    Delete /REBOOTOK $INSTDIR\uninstall.exe
    DeleteRegValue HKLM "${REGKEY}" StartMenuGroup
    DeleteRegValue HKLM "${REGKEY}" Path
    DeleteRegKey /IfEmpty HKLM "${REGKEY}\Components"
    DeleteRegKey /IfEmpty HKLM "${REGKEY}"
    RmDir /REBOOTOK $SMPROGRAMS\$StartMenuGroup
    RmDir /REBOOTOK $INSTDIR
SectionEnd

# Installer functions
Function .onInit

    InitPluginsDir
	;Extract InstallOptions files
	File /oname=$PLUGINSDIR\options.ini "options.ini"

	; Check for old verion and do uninstall
	ReadRegStr $R0 HKLM "Software\Microsoft\Windows\CurrentVersion\Uninstall\$(^Name)" "UninstallString"
	StrCmp $R0 "" done
 
	MessageBox MB_OKCANCEL|MB_ICONEXCLAMATION "$(^Name) is already installed. $\n$\nClick `OK` to remove the  previous version or `Cancel` to cancel this upgrade." IDOK uninst
	Abort
 
	;Run the uninstaller
	uninst:
		ClearErrors
		Exec $INSTDIR\uninstall.exe
 
	done:

FunctionEnd

Function StartMenu_Pre

	${IF} $CreateStartMenu == 0
		Abort
	${ENDIF}

FunctionEnd

# Custom Page showing the installer options
Function Options
 
  !insertmacro MUI_HEADER_TEXT "Installation Options" "Select installation options ..." 
  InstallOptions::dialog "$PLUGINSDIR\options.ini"
 
FunctionEnd

Function OptionsValidate

  ReadINIStr $CreateStartMenu "$PLUGINSDIR\options.ini" "Field 1" "State"
  ReadINIStr $CreateDeskTopShortCut "$PLUGINSDIR\options.ini" "Field 2" "State"
  ReadINIStr $CreateExplorerMenu "$PLUGINSDIR\options.ini" "Field 3" "State"
 
FunctionEnd

# Uninstaller functions
Function un.onInit
    ReadRegStr $INSTDIR HKLM "${REGKEY}" Path
    !insertmacro MUI_STARTMENU_GETFOLDER Application $StartMenuGroup
    !insertmacro SELECT_UNSECTION Main ${UNSEC0000}
FunctionEnd

