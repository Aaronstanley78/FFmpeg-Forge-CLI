; FFmpeg Forge CLI 2.0.0 - Windows installer (NSIS 3)
; Build:  makensis -DSRCDIR=<FFmpegForge-CLI portable folder> [-DOUTFILE=<setup.exe>] installer\forge-cli.nsi
; Installs for the current user (no administrator rights) in %LOCALAPPDATA%\Programs\FFmpeg Forge CLI,
; adds forge to the user PATH, creates Start menu shortcuts and an uninstaller (Settings > Apps).

Unicode true
SetCompressor /SOLID lzma
SetCompressorDictSize 64

!ifndef SRCDIR
  !error "Define SRCDIR: the FFmpeg Forge CLI portable folder (forge.exe, bin\, FFmpeg\ ...)"
!endif
!ifndef OUTFILE
  !define OUTFILE "FFmpegForge-CLI-2.0.0-Setup.exe"
!endif

!define APPNAME     "FFmpeg Forge CLI"
!define APPVERSION  "2.0.0"
!define PUBLISHER   "Aaronstanley78"
!define URL         "https://github.com/Aaronstanley78/FFmpeg-Forge-CLI"
!define UNINSTKEY   "Software\Microsoft\Windows\CurrentVersion\Uninstall\FFmpegForgeCLI"

Name "${APPNAME} ${APPVERSION}"
OutFile "${OUTFILE}"
InstallDir "$LOCALAPPDATA\Programs\FFmpeg Forge CLI"
InstallDirRegKey HKCU "Software\FFmpeg Forge CLI" "InstallDir"
RequestExecutionLevel user
ShowInstDetails show
ShowUninstDetails show
BrandingText "${APPNAME} ${APPVERSION}"

VIProductVersion "2.0.0.0"
VIAddVersionKey "ProductName" "${APPNAME}"
VIAddVersionKey "ProductVersion" "${APPVERSION}"
VIAddVersionKey "FileVersion" "${APPVERSION}"
VIAddVersionKey "FileDescription" "${APPNAME} ${APPVERSION} setup"
VIAddVersionKey "LegalCopyright" "GNU GPL v3"
VIAddVersionKey "CompanyName" "${PUBLISHER}"

!include "MUI2.nsh"
!include "StrFunc.nsh"
!include "WinMessages.nsh"
${StrStr}
${UnStrRep}

!define MUI_ICON   "${SRCDIR}\Source\src\app.ico"
!define MUI_UNICON "${SRCDIR}\Source\src\app.ico"
!define MUI_ABORTWARNING
!define MUI_WELCOMEPAGE_TITLE "${APPNAME} ${APPVERSION}"
!define MUI_WELCOMEPAGE_TEXT "This installs the command-line edition of FFmpeg Forge (engine of FFmpeg Forge 3.0.1) for your Windows account. No administrator rights are needed.$\r$\n$\r$\nAfter the installation, open a new terminal anywhere and type:$\r$\n$\r$\n    forge help$\r$\n    forge status$\r$\n$\r$\nThe AI engines (OptiPulsar, RazorNext, WanGP...) are installed later, when you need them, with  forge install ..."
!define MUI_COMPONENTSPAGE_SMALLDESC
!define MUI_FINISHPAGE_RUN "$INSTDIR\Forge console.cmd"
!define MUI_FINISHPAGE_RUN_TEXT "Open the Forge console"
!define MUI_FINISHPAGE_SHOWREADME "$INSTDIR\README.txt"
!define MUI_FINISHPAGE_SHOWREADME_TEXT "Show README.txt"
!define MUI_FINISHPAGE_LINK "FFmpeg Forge CLI on GitHub"
!define MUI_FINISHPAGE_LINK_LOCATION "${URL}"

!insertmacro MUI_PAGE_WELCOME
!insertmacro MUI_PAGE_LICENSE "${SRCDIR}\LICENSE-GPL-3.0.txt"
!insertmacro MUI_PAGE_COMPONENTS
!insertmacro MUI_PAGE_DIRECTORY
!insertmacro MUI_PAGE_INSTFILES
!insertmacro MUI_PAGE_FINISH
!insertmacro MUI_UNPAGE_CONFIRM
!insertmacro MUI_UNPAGE_INSTFILES
!insertmacro MUI_LANGUAGE "English"
!insertmacro MUI_LANGUAGE "French"

; ---------------------------------------------------------------- sections
Section "!FFmpeg Forge CLI (required)" SecCore
  SectionIn RO
  SetOutPath "$INSTDIR"
  File "${SRCDIR}\forge.exe"
  File "${SRCDIR}\Forge console.cmd"
  File "${SRCDIR}\README.txt"
  File "${SRCDIR}\USAGE.md"
  File "${SRCDIR}\LICENSE-GPL-3.0.txt"
  File "${SRCDIR}\LICENSE-dovi_tool.txt"
  File "${SRCDIR}\SHADER_LICENSES.txt"
  File "${SRCDIR}\ForgeRTX.exe"
  File "${SRCDIR}\dovi_tool.exe"
  File "${SRCDIR}\portable.txt"          ; settings and AI engines stay in this (writable) folder
  SetOutPath "$INSTDIR\VE-presets"
  File "${SRCDIR}\VE-presets\README.txt"
  SetOutPath "$INSTDIR\models\custom"
  File /nonfatal /r "${SRCDIR}\models\custom\*.*"
  SetOutPath "$INSTDIR"
  WriteUninstaller "$INSTDIR\Uninstall.exe"
  WriteRegStr HKCU "Software\FFmpeg Forge CLI" "InstallDir" "$INSTDIR"
  WriteRegStr HKCU "${UNINSTKEY}" "DisplayName" "${APPNAME} ${APPVERSION}"
  WriteRegStr HKCU "${UNINSTKEY}" "DisplayVersion" "${APPVERSION}"
  WriteRegStr HKCU "${UNINSTKEY}" "Publisher" "${PUBLISHER}"
  WriteRegStr HKCU "${UNINSTKEY}" "URLInfoAbout" "${URL}"
  WriteRegStr HKCU "${UNINSTKEY}" "DisplayIcon" "$INSTDIR\forge.exe"
  WriteRegStr HKCU "${UNINSTKEY}" "InstallLocation" "$INSTDIR"
  WriteRegStr HKCU "${UNINSTKEY}" "UninstallString" '"$INSTDIR\Uninstall.exe"'
  WriteRegStr HKCU "${UNINSTKEY}" "QuietUninstallString" '"$INSTDIR\Uninstall.exe" /S'
  WriteRegDWORD HKCU "${UNINSTKEY}" "NoModify" 1
  WriteRegDWORD HKCU "${UNINSTKEY}" "NoRepair" 1
SectionEnd

Section "FFmpeg 9.0.2 full build (recommended)" SecFFmpeg
  SetOutPath "$INSTDIR\bin"
  File "${SRCDIR}\bin\ffmpeg.exe"
  File "${SRCDIR}\bin\ffprobe.exe"
  File "${SRCDIR}\bin\ffplay.exe"
  SetOutPath "$INSTDIR\FFmpeg"
  File /r "${SRCDIR}\FFmpeg\*.*"
SectionEnd

Section "Add forge to the PATH" SecPath
  ReadRegStr $0 HKCU "Environment" "Path"
  ${StrStr} $1 ";$0;" ";$INSTDIR;"
  StrCmp $1 "" 0 path_done
    StrCmp $0 "" 0 +3
      WriteRegExpandStr HKCU "Environment" "Path" "$INSTDIR"
      Goto path_sent
    WriteRegExpandStr HKCU "Environment" "Path" "$0;$INSTDIR"
  path_sent:
    WriteRegDWORD HKCU "Software\FFmpeg Forge CLI" "AddedToPath" 1
    SendMessage ${HWND_BROADCAST} ${WM_SETTINGCHANGE} 0 "STR:Environment" /TIMEOUT=5000
  path_done:
SectionEnd

Section "Start menu shortcuts" SecMenu
  CreateDirectory "$SMPROGRAMS\FFmpeg Forge CLI"
  CreateShortcut "$SMPROGRAMS\FFmpeg Forge CLI\Forge console.lnk" "$INSTDIR\Forge console.cmd" "" "$INSTDIR\forge.exe" 0
  CreateShortcut "$SMPROGRAMS\FFmpeg Forge CLI\Command reference.lnk" "$INSTDIR\USAGE.md"
  CreateShortcut "$SMPROGRAMS\FFmpeg Forge CLI\README.lnk" "$INSTDIR\README.txt"
  CreateShortcut "$SMPROGRAMS\FFmpeg Forge CLI\Uninstall FFmpeg Forge CLI.lnk" "$INSTDIR\Uninstall.exe"
SectionEnd

Section /o "Example scripts and source code" SecExtra
  SetOutPath "$INSTDIR\examples"
  File /r "${SRCDIR}\examples\*.*"
  SetOutPath "$INSTDIR\Source"
  File /r "${SRCDIR}\Source\*.*"
SectionEnd

Section "-size"
  SectionGetSize ${SecCore} $0
  SectionGetSize ${SecFFmpeg} $1
  IntOp $0 $0 + $1
  WriteRegDWORD HKCU "${UNINSTKEY}" "EstimatedSize" $0
SectionEnd

LangString DESC_Core   ${LANG_ENGLISH} "forge.exe, the Forge console, dovi_tool (Dolby Vision) and ForgeRTX (NVIDIA RTX Video)."
LangString DESC_FFmpeg ${LANG_ENGLISH} "FFmpeg 9.0.2 full build (gyan.dev). Without it, forge uses C:\FFmpeg\bin or the PATH."
LangString DESC_Path   ${LANG_ENGLISH} "Type  forge  in any terminal (user PATH, no administrator rights)."
LangString DESC_Menu   ${LANG_ENGLISH} "Forge console, command reference and uninstaller in the Start menu."
LangString DESC_Extra  ${LANG_ENGLISH} "Ready-to-use .cmd / .ps1 examples and the C# source code."
LangString DESC_Core   ${LANG_FRENCH} "forge.exe, la console Forge, dovi_tool (Dolby Vision) et ForgeRTX (NVIDIA RTX Video)."
LangString DESC_FFmpeg ${LANG_FRENCH} "FFmpeg 9.0.2 full build (gyan.dev). Sans lui, forge utilise C:\FFmpeg\bin ou le PATH."
LangString DESC_Path   ${LANG_FRENCH} "Taper  forge  dans n'importe quel terminal (PATH utilisateur, sans droits administrateur)."
LangString DESC_Menu   ${LANG_FRENCH} "Console Forge, aide des commandes et désinstallation dans le menu Démarrer."
LangString DESC_Extra  ${LANG_FRENCH} "Exemples .cmd / .ps1 prêts à l'emploi et code source C#."
!insertmacro MUI_FUNCTION_DESCRIPTION_BEGIN
  !insertmacro MUI_DESCRIPTION_TEXT ${SecCore}   $(DESC_Core)
  !insertmacro MUI_DESCRIPTION_TEXT ${SecFFmpeg} $(DESC_FFmpeg)
  !insertmacro MUI_DESCRIPTION_TEXT ${SecPath}   $(DESC_Path)
  !insertmacro MUI_DESCRIPTION_TEXT ${SecMenu}   $(DESC_Menu)
  !insertmacro MUI_DESCRIPTION_TEXT ${SecExtra}  $(DESC_Extra)
!insertmacro MUI_FUNCTION_DESCRIPTION_END

Function .onInit
  !insertmacro MUI_LANGDLL_DISPLAY
FunctionEnd

; ---------------------------------------------------------------- uninstaller
Section "Uninstall"
  ; user PATH
  ReadRegDWORD $2 HKCU "Software\FFmpeg Forge CLI" "AddedToPath"
  StrCmp $2 1 0 path_kept
    ReadRegStr $0 HKCU "Environment" "Path"
    ${UnStrRep} $0 $0 ";$INSTDIR" ""
    ${UnStrRep} $0 $0 "$INSTDIR;" ""
    ${UnStrRep} $0 $0 "$INSTDIR" ""
    WriteRegExpandStr HKCU "Environment" "Path" $0
    SendMessage ${HWND_BROADCAST} ${WM_SETTINGCHANGE} 0 "STR:Environment" /TIMEOUT=5000
  path_kept:

  Delete "$INSTDIR\forge.exe"
  Delete "$INSTDIR\Forge console.cmd"
  Delete "$INSTDIR\README.txt"
  Delete "$INSTDIR\USAGE.md"
  Delete "$INSTDIR\LICENSE-GPL-3.0.txt"
  Delete "$INSTDIR\LICENSE-dovi_tool.txt"
  Delete "$INSTDIR\SHADER_LICENSES.txt"
  Delete "$INSTDIR\ForgeRTX.exe"
  Delete "$INSTDIR\dovi_tool.exe"
  Delete "$INSTDIR\portable.txt"
  Delete "$INSTDIR\VE-presets\README.txt"
  RMDir "$INSTDIR\VE-presets"
  RMDir /r "$INSTDIR\bin"
  RMDir /r "$INSTDIR\FFmpeg"
  RMDir /r "$INSTDIR\examples"
  RMDir /r "$INSTDIR\Source"
  RMDir /r "$SMPROGRAMS\FFmpeg Forge CLI"

  ; AI engines (OptiPulsar, RazorNext, WanGP...), models and settings: several GB, kept unless the user agrees
  MessageBox MB_YESNO|MB_ICONQUESTION "Also delete the AI engines, models and settings installed in$\r$\n$INSTDIR$\r$\n(OptiPulsar, RazorNext, VisionNext, WanGP, SeedVR2, AI denoiser, QTGMC, Settings)?" /SD IDNO IDNO keep_engines
    RMDir /r "$INSTDIR"
  keep_engines:
  Delete "$INSTDIR\Uninstall.exe"
  RMDir "$INSTDIR\models\custom"
  RMDir "$INSTDIR\models"
  RMDir "$INSTDIR"

  DeleteRegKey HKCU "${UNINSTKEY}"
  DeleteRegKey HKCU "Software\FFmpeg Forge CLI"
SectionEnd

Function un.onInit
  !insertmacro MUI_UNGETLANGUAGE
FunctionEnd
