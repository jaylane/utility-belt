; Define your application name
!define APPNAME "UtilityBelt"
!define SOFTWARECOMPANY "SunnujDecalPlugins"
!define VERSION	"0.0.8"
!define APPGUID "{c51788b5-3c43-471a-8034-79d5865fd7bd}"

!define ASSEMBLY "UtilityBelt.dll"
!define CLASSNAME "UtilityBelt.PluginCore"

!define BUILDPATH ".\..\bin\x86\Release"

; Main Install settings
; compressor goes first
SetCompressor LZMA

Name "${APPNAME} ${VERSION}"
InstallDir "C:\Games\Decal Plugins\${APPNAME}"
InstallDirRegKey HKLM "Software\${SOFTWARECOMPANY}\${APPNAME}" ""
;SetFont "Verdana" 8
;Icon "Installer\Res\Decal.ico"
OutFile "${APPNAME}Installer-${VERSION}.exe"

; Use compression

; Modern interface settings
!include "MUI.nsh"

!define MUI_ABORTWARNING

!insertmacro MUI_PAGE_WELCOME
;!insertmacro MUI_PAGE_COMPONENTS
!insertmacro MUI_PAGE_DIRECTORY
!insertmacro MUI_PAGE_INSTFILES
!insertmacro MUI_PAGE_FINISH

!insertmacro MUI_UNPAGE_CONFIRM
!insertmacro MUI_UNPAGE_INSTFILES

; Set languages (first is default language)
!insertmacro MUI_LANGUAGE "English"
!insertmacro MUI_RESERVEFILE_LANGDLL


Section "" CoreSection
; Set Section properties
	SetOverwrite on

	; Set Section Files and Shortcuts
	SetOutPath "$INSTDIR\"

	File "${BUILDPATH}\${ASSEMBLY}"
	File "${BUILDPATH}\${APPNAME}.pdb"

	File "${BUILDPATH}\SharedMemory.dll"
	File "${BUILDPATH}\Newtonsoft.Json.dll"
	
	SetOutPath "$INSTDIR\Resources\"
	File "${BUILDPATH}\Resources\quests.xml"

	SetOutPath "$INSTDIR\Resources\tiles"
	File "${BUILDPATH}\Resources\tiles\*.bmp"

SectionEnd

Section -FinishSection

	WriteRegStr HKLM "Software\${SOFTWARECOMPANY}\${APPNAME}" "" "$INSTDIR"
	WriteRegStr HKLM "Software\${SOFTWARECOMPANY}\${APPNAME}" "Version" "${VERSION}"

	;Register in decal
	WriteRegStr HKLM "Software\Decal\Plugins\${APPGUID}" "" "${APPNAME}"
	WriteRegDWORD HKLM "Software\Decal\Plugins\${APPGUID}" "Enabled" "1"
	WriteRegStr HKLM "Software\Decal\Plugins\${APPGUID}" "Object" "${CLASSNAME}"
	WriteRegStr HKLM "Software\Decal\Plugins\${APPGUID}" "Assembly" "${ASSEMBLY}"
	WriteRegStr HKLM "Software\Decal\Plugins\${APPGUID}" "Path" "$INSTDIR"
	WriteRegStr HKLM "Software\Decal\Plugins\${APPGUID}" "Surrogate" "{71A69713-6593-47EC-0002-0000000DECA1}"
	WriteRegStr HKLM "Software\Decal\Plugins\${APPGUID}" "Uninstaller" "${APPNAME}"

	WriteRegStr HKLM "Software\Microsoft\Windows\CurrentVersion\Uninstall\${APPNAME}" "DisplayName" "${APPNAME}"
	WriteRegStr HKLM "Software\Microsoft\Windows\CurrentVersion\Uninstall\${APPNAME}" "UninstallString" "$INSTDIR\uninstall.exe"
	WriteUninstaller "$INSTDIR\uninstall.exe"
	;MessageBox MB_OK "Done"

SectionEnd

; Modern install component descriptions
!insertmacro MUI_FUNCTION_DESCRIPTION_BEGIN
	!insertmacro MUI_DESCRIPTION_TEXT ${CoreSection} ""
!insertmacro MUI_FUNCTION_DESCRIPTION_END

;Uninstall section
Section Uninstall

	;Remove from registry...
	DeleteRegKey HKLM "Software\${SOFTWARECOMPANY}\${APPNAME}"
	DeleteRegKey HKLM "Software\Decal\Plugins\${APPGUID}"
	DeleteRegKey HKLM "Software\Microsoft\Windows\CurrentVersion\Uninstall\${APPNAME}"

	; Delete self
	Delete "$INSTDIR\uninstall.exe"

	;Clean up
	Delete "$INSTDIR\${ASSEMBLY}"
	Delete "${BUILDPATH}\{APPNAME}.pdb"
	Delete "$INSTDIR\SharedMemory.dll"
	Delete "$INSTDIR\Newtonsoft.Json.dll"
	Delete "${BUILDPATH}\Resources\tiles\*.bmp"
	Delete "${BUILDPATH}\Resources\quests.xml"
	;Delete "$INSTDIR\ADDITIONALFILES"
	RMDir "$INSTDIR\"

SectionEnd

; eof