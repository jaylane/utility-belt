; Define your application name
!define APPNAME "UtilityBelt"
!define SOFTWARECOMPANY "HackThePlanet"
!define APPGUID "{c51788b5-3c43-471a-8034-79d5865fd7bd}"
!define SERVICEGUID "{8adc5729-db1a-4e28-9475-c4eafae1e6e7}"

!define ASSEMBLY "UBLoader.dll"
!define CLASSNAME "UBLoader.FilterCore"

!define BUILDPATH ".\..\bin"

; Main Install settings
; compressor goes first
SetCompressor LZMA

!getdllversion "${BUILDPATH}\${ASSEMBLY}" Expv_
!define VERSION ${Expv_1}.${Expv_2}.${Expv_3}

Name "${APPNAME} ${VERSION}"
InstallDir "C:\Games\Decal Plugins\${APPNAME}"
InstallDirRegKey HKLM "Software\${SOFTWARECOMPANY}\${APPNAME}" ""
;SetFont "Verdana" 8
;Icon "Installer\Res\Decal.ico"
OutFile "${BUILDPATH}\${APPNAME}Installer-${VERSION}.exe"

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
	File "${BUILDPATH}\UtilityBelt.dll"
	File "${BUILDPATH}\UtilityBelt.pdb"
	File "${BUILDPATH}\UBNetworking.dll"
	File "${BUILDPATH}\UBNetworking.pdb"
	File "${BUILDPATH}\UBNetServer.exe"
	File "${BUILDPATH}\0Harmony.dll"
	File "${BUILDPATH}\cimgui.dll"
	File "${BUILDPATH}\Exceptionless.dll"
	File "${BUILDPATH}\Exceptionless.Models.dll"
	File "${BUILDPATH}\themes\*.json"

SectionEnd

Section -FinishSection

	WriteRegStr HKLM "Software\${SOFTWARECOMPANY}\${APPNAME}" "" "$INSTDIR"
	WriteRegStr HKLM "Software\${SOFTWARECOMPANY}\${APPNAME}" "Version" "${VERSION}"

	;Unregister old plugin
	ClearErrors
	ReadRegStr $0 HKLM "Software\Decal\Plugins\${APPGUID}" ""
	${IfNot} ${Errors}
		DeleteRegKey HKLM "Software\Decal\Plugins\${APPGUID}"
	${EndIf}

	;Register in decal
	ClearErrors
	ReadRegStr $0 HKLM "Software\Decal\NetworkFilters\${APPGUID}" ""
	${If} ${Errors}
		WriteRegStr HKLM "Software\Decal\NetworkFilters\${APPGUID}" "" "${APPNAME}"
		WriteRegDWORD HKLM "Software\Decal\NetworkFilters\${APPGUID}" "Enabled" "1"
		WriteRegStr HKLM "Software\Decal\NetworkFilters\${APPGUID}" "Object" "${CLASSNAME}"
		WriteRegStr HKLM "Software\Decal\NetworkFilters\${APPGUID}" "Assembly" "${ASSEMBLY}"
		WriteRegStr HKLM "Software\Decal\NetworkFilters\${APPGUID}" "Path" "$INSTDIR"
		WriteRegStr HKLM "Software\Decal\NetworkFilters\${APPGUID}" "Surrogate" "{71A69713-6593-47EC-0002-0000000DECA1}"
		WriteRegStr HKLM "Software\Decal\NetworkFilters\${APPGUID}" "Uninstaller" "${APPNAME}"
	${Else}
		${IF} $0 != "${APPNAME}"
			MESSAGEBOX MB_OK|MB_ICONSTOP "Skipped decal UBLoader registration. A decal filter with this GUID already exists ($0), and is not ${APPNAME}.  This should not happen, but report it on gitlab or discord if you are seeing this."
		${ENDIF}
	${EndIf}

	ClearErrors
	ReadRegStr $0 HKLM "Software\Decal\Services\${SERVICEGUID}" ""
	${If} ${Errors}
		WriteRegStr HKLM "Software\Decal\Services\${SERVICEGUID}" "" "UBService"
		WriteRegDWORD HKLM "Software\Decal\Services\${SERVICEGUID}" "Enabled" "1"
		WriteRegStr HKLM "Software\Decal\Services\${SERVICEGUID}" "Object" "UBService.UBService"
		WriteRegStr HKLM "Software\Decal\Services\${SERVICEGUID}" "Assembly" "UBService"
		WriteRegStr HKLM "Software\Decal\Services\${SERVICEGUID}" "Path" "$INSTDIR"
		WriteRegStr HKLM "Software\Decal\Services\${SERVICEGUID}" "Surrogate" "{71A69713-6593-47EC-0002-0000000DECA1}"
		WriteRegStr HKLM "Software\Decal\Services\${SERVICEGUID}" "Uninstaller" "${APPNAME}"
	${Else}
		${IF} $0 != "UBService"
			MESSAGEBOX MB_OK|MB_ICONSTOP "Skipped decal UBService registration. A decal filter with this GUID already exists ($0), and is not UBService.  This should not happen, but report it on gitlab or discord if you are seeing this."
		${ENDIF}
	${EndIf}

	WriteRegStr HKLM "Software\Microsoft\Windows\CurrentVersion\Uninstall\${APPNAME}" "DisplayName" "${APPNAME}"
	WriteRegStr HKLM "Software\Microsoft\Windows\CurrentVersion\Uninstall\${APPNAME}" "UninstallString" "$INSTDIR\uninstall.exe"
	WriteUninstaller "$INSTDIR\uninstall.exe"
	
	;Clean up old plugin resources
	Delete "$INSTDIR\SharedMemory.dll"
	Delete "$INSTDIR\Newtonsoft.Json.dll"
	Delete "$INSTDIR\\Resources\tiles\*.bmp"
	RMDir "$INSTDIR\Resources\tiles\"
	Delete "$INSTDIR\Resources\quests.xml"
	RMDir "$INSTDIR\Resources\"
	Delete "$INSTDIR\UBHelper.dll"

SectionEnd

; Modern install component descriptions
!insertmacro MUI_FUNCTION_DESCRIPTION_BEGIN
	!insertmacro MUI_DESCRIPTION_TEXT ${CoreSection} ""
!insertmacro MUI_FUNCTION_DESCRIPTION_END

;Uninstall section
Section Uninstall

	;Remove from registry...
	DeleteRegKey HKLM "Software\${SOFTWARECOMPANY}\${APPNAME}"
	DeleteRegKey HKLM "Software\Decal\NetworkFilters\${APPGUID}"
	DeleteRegKey HKLM "Software\Decal\Services\${SERVICEGUID}"
	DeleteRegKey HKLM "Software\Microsoft\Windows\CurrentVersion\Uninstall\${APPNAME}"

	; Delete self
	Delete "$INSTDIR\uninstall.exe"

	;Clean up
	Delete "$INSTDIR\${ASSEMBLY}"
	Delete "$INSTDIR\UBLoader.pdb"
	Delete "$INSTDIR\UtilityBelt.dll"
	Delete "$INSTDIR\UtilityBelt.pdb"
	Delete "$$INSTDIR\UBNetworking.dll"
	Delete "$$INSTDIR\UBNetworking.pdb"
	Delete "$$INSTDIR\UBNetServer.exe"
	Delete "$INSTDIR\0Harmony.dll"
	Delete "$$INSTDIR\UBService.dll"
	Delete "$INSTDIR\cimgui.dll"
	Delete "$INSTDIR\SharedMemory.dll"
	Delete "$INSTDIR\Newtonsoft.Json.dll"
	Delete "$INSTDIR\Exceptionless.dll"
	Delete "$INSTDIR\Exceptionless.Models.dll"
	Delete "${BUILDPATH}\Resources\tiles\*.bmp"
	Delete "${BUILDPATH}\Resources\quests.xml"
	RMDir "$INSTDIR\"

SectionEnd

; eof