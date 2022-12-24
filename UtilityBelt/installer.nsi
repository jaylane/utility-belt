; Define your application name
!define APPNAME "UtilityBelt"
!define SOFTWARECOMPANY "HackThePlanet"
!define APPGUID "{c51788b5-3c43-471a-8034-79d5865fd7bd}"
!define SERVICEGUID "{8adc5729-db1a-4e28-9475-c4eafae1e6e7}"

!define ASSEMBLY "UBLoader.dll"
!define CLASSNAME "UBLoader.FilterCore"

!define BUILDPATH ".\..\bin\Release\net48"

; Main Install settings
; compressor goes first
SetCompressor LZMA

!getdllversion "${BUILDPATH}\${ASSEMBLY}" Expv_
!define VERSION ${Expv_1}.${Expv_2}.${Expv_3}

Name "${APPNAME} ${VERSION}"
InstallDir "C:\Games\DecalPlugins\${APPNAME}"
InstallDirRegKey HKLM "Software\${SOFTWARECOMPANY}\${APPNAME}" ""
;SetFont "Verdana" 8
;Icon "Installer\Res\Decal.ico"
OutFile "${BUILDPATH}\..\${APPNAME}Installer-${VERSION}.exe"

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


; https://nsis.sourceforge.io/Download_and_Install_dotNET_45
Function CheckAndDownloadDotNet48
    # Set up our Variables
    Var /GLOBAL dotNET48IsThere
    Var /GLOBAL dotNET_CMD_LINE
    Var /GLOBAL EXIT_CODE

    # We are reading a version release DWORD that Microsoft says is the documented
    # way to determine if .NET Framework 4.8 is installed
    ReadRegDWORD $dotNET48IsThere HKLM "SOFTWARE\Microsoft\NET Framework Setup\NDP\v4\Full" "Release"
    IntCmp $dotNET48IsThere 528049 is_equal is_less is_greater

    is_equal:
        Goto done_compare_not_needed
    is_greater:
        Goto done_compare_not_needed
    is_less:
        Goto done_compare_needed

    done_compare_needed:
        #.NET Framework 4.8 install is *NEEDED*
 
        # Microsoft Download Center EXE:
        # Web Bootstrapper: https://go.microsoft.com/fwlink/?LinkId=2085155
        # Full Download: https://go.microsoft.com/fwlink/?linkid=2088631
 
        # Setup looks for components\dotNET48Full.exe relative to the install EXE location
        # This allows the installer to be placed on a USB stick (for computers without internet connections)
        # If the .NET Framework 4.8 installer is *NOT* found, Setup will connect to Microsoft's website
        # and download it for you
 
        # Reboot Required with these Exit Codes:
        # 1641 or 3010
 
        # Command Line Switches:
        # /showrmui /passive /norestart
 
        # Silent Command Line Switches:
        # /q /norestart
 
 
        # Let's see if the user is doing a Silent install or not
        IfSilent is_quiet is_not_quiet
 
        is_quiet:
            StrCpy $dotNET_CMD_LINE "/q /norestart"
            Goto LookForLocalFile
        is_not_quiet:
            StrCpy $dotNET_CMD_LINE "/showrmui /passive /norestart"
            Goto LookForLocalFile
 
        LookForLocalFile:
            # Let's see if the user stored the Full Installer
            IfFileExists "$EXEPATH\components\dotNET48Full.exe" do_local_install do_network_install
 
            do_local_install:
                # .NET Framework found on the local disk.  Use this copy
 
                ExecWait '"$EXEPATH\components\dotNET48Full.exe" $dotNET_CMD_LINE' $EXIT_CODE
                Goto is_reboot_requested
 
            # Now, let's Download the .NET
            do_network_install:
 
                Var /GLOBAL dotNetDidDownload
                NSISdl::download "https://go.microsoft.com/fwlink/?linkid=2088631" "$TEMP\dotNET48Web.exe" $dotNetDidDownload
 
                StrCmp $dotNetDidDownload success fail
                success:
                    ExecWait '"$TEMP\dotNET48Web.exe" $dotNET_CMD_LINE' $EXIT_CODE
                    Goto is_reboot_requested
 
                fail:
                    MessageBox MB_OK|MB_ICONEXCLAMATION "Unable to download .NET Framework.  ${PRODUCT_NAME} will be installed, but will not function without the Framework!"
                    Goto done_dotNET_function
 
                # $EXIT_CODE contains the return codes.  1641 and 3010 means a Reboot has been requested
                is_reboot_requested:
                    ${If} $EXIT_CODE = 1641
                    ${OrIf} $EXIT_CODE = 3010
                        SetRebootFlag true
                    ${EndIf}
 
    done_compare_not_needed:
        # Done dotNET Install
        Goto done_dotNET_function
 
    #exit the function
    done_dotNET_function:
 
FunctionEnd


Section "" CoreSection
; Set Section properties
	SetOverwrite on

	; Set Section Files and Shortcuts
	SetOutPath "$INSTDIR\"
	
	File "${BUILDPATH}\${ASSEMBLY}"
	File "${BUILDPATH}\${APPNAME}.pdb"
	File "${BUILDPATH}\UtilityBelt.dll"
	File "${BUILDPATH}\UtilityBelt.Networking.dll"
	File "${BUILDPATH}\UtilityBelt.Server.exe"
	File "${BUILDPATH}\UtilityBelt.Common.dll"
	File "${BUILDPATH}\UtilityBelt.Common.pdb"
	File "${BUILDPATH}\UtilityBelt.Common.ScriptTypes.json"
	File "${BUILDPATH}\UtilityBelt.Scripting.dll"
	File "${BUILDPATH}\UtilityBelt.Scripting.pdb"
	File "${BUILDPATH}\UtilityBelt.Scripting.ScriptTypes.json"
	File "${BUILDPATH}\UtilityBelt.Helper.dll"
	File "${BUILDPATH}\0Harmony.dll"
	File "${BUILDPATH}\MoonSharp.Interpreter.dll"
	File "${BUILDPATH}\Exceptionless.dll"
	File "${BUILDPATH}\Exceptionless.Models.dll"
	File "${BUILDPATH}\Websocket.Client.dll"
	File "${BUILDPATH}\websocket-sharp.dll"
	File "${BUILDPATH}\System.Drawing.Common.dll"
	File "${BUILDPATH}\System.Numerics.Vectors.dll"
	File "${BUILDPATH}\System.Reactive.dll"
	File "${BUILDPATH}\System.Runtime.CompilerServices.Unsafe.dll"
	File "${BUILDPATH}\System.Threading.Channels.dll"
	File "${BUILDPATH}\System.Threading.Tasks.Extensions.dll"
	File "${BUILDPATH}\System.ValueTuple.dll"
	File "${BUILDPATH}\0Harmony.dll"
	File "${BUILDPATH}\UtilityBelt.Service.Installer.exe"

SectionEnd

Section -FinishSection

	WriteRegStr HKLM "Software\${SOFTWARECOMPANY}\${APPNAME}" "" "$INSTDIR"
	WriteRegStr HKLM "Software\${SOFTWARECOMPANY}\${APPNAME}" "Version" "${VERSION}"

	;Unregister old plugin
	ClearErrors
	ReadRegStr $0 HKLM "Software\Decal\NetworkFilters\${APPGUID}" ""
	${IfNot} ${Errors}
		DeleteRegKey HKLM "Software\Decal\NetworkFilters\${APPGUID}"
	${EndIf}

	;Register in decal
	WriteRegStr HKLM "Software\Decal\NetworkFilters\${APPGUID}" "" "${APPNAME}"
	WriteRegDWORD HKLM "Software\Decal\NetworkFilters\${APPGUID}" "Enabled" "1"
	WriteRegStr HKLM "Software\Decal\NetworkFilters\${APPGUID}" "Object" "${CLASSNAME}"
	WriteRegStr HKLM "Software\Decal\NetworkFilters\${APPGUID}" "Assembly" "${ASSEMBLY}"
	WriteRegStr HKLM "Software\Decal\NetworkFilters\${APPGUID}" "Path" "$INSTDIR"
	WriteRegStr HKLM "Software\Decal\NetworkFilters\${APPGUID}" "Surrogate" "{71A69713-6593-47EC-0002-0000000DECA1}"
	WriteRegStr HKLM "Software\Decal\NetworkFilters\${APPGUID}" "Uninstaller" "${APPNAME}"

	WriteRegStr HKLM "Software\Microsoft\Windows\CurrentVersion\Uninstall\${APPNAME}" "DisplayName" "${APPNAME}"
	WriteRegStr HKLM "Software\Microsoft\Windows\CurrentVersion\Uninstall\${APPNAME}" "UninstallString" "$INSTDIR\uninstall.exe"
	WriteUninstaller "$INSTDIR\uninstall.exe"
	
	;Clean up old plugin resources
	Delete "$INSTDIR\SharedMemory.dll"
	Delete "$INSTDIR\\Resources\tiles\*.bmp"
	RMDir "$INSTDIR\Resources\tiles\"
	Delete "$INSTDIR\Resources\quests.xml"
	RMDir "$INSTDIR\Resources\"
	Delete "$INSTDIR\UBHelper.dll"
	Delete "$INSTDIR\UtilityBelt.Service.dll"
	Delete "$INSTDIR\Newtonsoft.Json.dll"
	Delete "$INSTDIR\cimgui.dll"
	Delete "$INSTDIR\SharedMemory.dll"

    ; make sure dotnet 4.8 is installed
    Call CheckAndDownloadDotNet48
    
    ; make sure UtilityBelt.Service is installed
    ; TODO: try and pull UtilityBelt.Service version from the registry and check it against the version required for this plugin
    ExecWait '"$instdir\UtilityBelt.Service.Installer.exe"'
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
	DeleteRegKey HKLM "Software\Microsoft\Windows\CurrentVersion\Uninstall\${APPNAME}"

	; Delete self
	Delete "$INSTDIR\uninstall.exe"

	;Clean up
	Delete "$INSTDIR\${ASSEMBLY}"
	Delete "$INSTDIR\UBLoader.pdb"
	Delete "$INSTDIR\UtilityBelt.dll"
	Delete "$INSTDIR\UtilityBelt.pdb"
	Delete "$INSTDIR\UtilityBelt.Common.dll"
	Delete "$INSTDIR\UtilityBelt.Common.pdb"
	Delete "$INSTDIR\UtilityBelt.Scripting.dll"
	Delete "$INSTDIR\UtilityBelt.Scripting.pdb"
	Delete "$INSTDIR\UtilityBelt.Networking.dll"
	Delete "$INSTDIR\UtilityBelt.Networking.pdb"
	Delete "$INSTDIR\UtilityBelt.Server.exe"
	Delete "$INSTDIR\0Harmony.dll"
	Delete "$INSTDIR\Exceptionless.dll"
	Delete "$INSTDIR\Exceptionless.Models.dll"
	Delete "$INSTDIR\MoonSharp.Interpreter.dll"
	Delete "$INSTDIR\Websocket.Client.dll"
	Delete "$INSTDIR\websocket-sharp.dll"

SectionEnd

; eof