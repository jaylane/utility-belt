param([string]$NuGetVersion,
     [string]$SolutionDir,
     [string]$ProjectDir,
     [string]$ProjectPath,
     [string]$ConfigurationName,
     [string]$TargetName,
     [string]$TargetDir,
     [string]$ProjectName,
     [string]$PlatformName,
     [string]$NuGetPackageRoot,
     [string]$TargetPath);

Remove-Item -Path "./../bin/Release/*Installer*.exe"

If ($Env:OS -ne "" -and $Env:OS -ne $null -and $Env:OS.ToLower().Contains("windows")) {
    Write-Host "$($NuGetPackageRoot)nsis-tool\3.0.8\tools\makensis.exe installer.nsi"
    "$($NuGetPackageRoot)nsis-tool\3.0.8\tools\makensis.exe installer.nsi" | Invoke-Expression
}
else {
    Write-Host "makensis installer.nsi"
    "makensis installer.nsi" | Invoke-Expression
}

Move-Item -Path "./../bin/Release/*Installer-*.exe" -Destination "./../bin/Release/UtilityBelt-Installer-${NuGetVersion}.exe"
