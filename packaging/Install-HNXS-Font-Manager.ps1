param(
    [switch]$AllUsers,
    [switch]$CreateDesktopShortcut = $true
)

$ErrorActionPreference = "Stop"

$AppName = "HNXS Font Manager"
$Publisher = "HNXS"
$Version = "0.1.0"
$ExeName = "Hnxs.FontManager.exe"
$PackageRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$AppSource = Join-Path $PackageRoot "app"
if (-not (Test-Path -LiteralPath (Join-Path $AppSource $ExeName))) {
    $AppSource = $PackageRoot
}

function Test-Administrator {
    $identity = [Security.Principal.WindowsIdentity]::GetCurrent()
    $principal = [Security.Principal.WindowsPrincipal]::new($identity)
    return $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
}

if ($AllUsers -and -not (Test-Administrator)) {
    throw "All-users install requires administrator privileges. Re-run PowerShell as Administrator, or install without -AllUsers."
}

if (-not (Test-Path -LiteralPath (Join-Path $AppSource $ExeName))) {
    throw "App payload not found: $AppSource"
}

$InstallRoot = if ($AllUsers) {
    Join-Path $env:ProgramFiles $AppName
} else {
    Join-Path $env:LOCALAPPDATA $AppName
}

$StartMenuRoot = if ($AllUsers) {
    Join-Path $env:ProgramData "Microsoft\Windows\Start Menu\Programs\$AppName"
} else {
    Join-Path $env:APPDATA "Microsoft\Windows\Start Menu\Programs\$AppName"
}

$DesktopPath = if ($AllUsers) {
    Join-Path ([Environment]::GetFolderPath("CommonDesktopDirectory")) "$AppName.lnk"
} else {
    Join-Path ([Environment]::GetFolderPath("DesktopDirectory")) "$AppName.lnk"
}

New-Item -ItemType Directory -Force -Path $InstallRoot | Out-Null
New-Item -ItemType Directory -Force -Path $StartMenuRoot | Out-Null

Copy-Item -Path (Join-Path $AppSource "*") -Destination $InstallRoot -Recurse -Force
Copy-Item -LiteralPath (Join-Path $PackageRoot "Uninstall-HNXS-Font-Manager.ps1") -Destination $InstallRoot -Force

$ExePath = Join-Path $InstallRoot $ExeName
$UninstallScript = Join-Path $InstallRoot "Uninstall-HNXS-Font-Manager.ps1"

$Shell = New-Object -ComObject WScript.Shell

$AppShortcut = $Shell.CreateShortcut((Join-Path $StartMenuRoot "$AppName.lnk"))
$AppShortcut.TargetPath = $ExePath
$AppShortcut.WorkingDirectory = $InstallRoot
$AppShortcut.IconLocation = "$ExePath,0"
$AppShortcut.Save()

$UninstallShortcut = $Shell.CreateShortcut((Join-Path $StartMenuRoot "Uninstall $AppName.lnk"))
$UninstallShortcut.TargetPath = "powershell.exe"
$UninstallShortcut.Arguments = "-ExecutionPolicy Bypass -File `"$UninstallScript`""
$UninstallShortcut.WorkingDirectory = $InstallRoot
$UninstallShortcut.IconLocation = "$ExePath,0"
$UninstallShortcut.Save()

if ($CreateDesktopShortcut) {
    $DesktopShortcut = $Shell.CreateShortcut($DesktopPath)
    $DesktopShortcut.TargetPath = $ExePath
    $DesktopShortcut.WorkingDirectory = $InstallRoot
    $DesktopShortcut.IconLocation = "$ExePath,0"
    $DesktopShortcut.Save()
}

$RegistryRoot = if ($AllUsers) {
    "HKLM:\Software\Microsoft\Windows\CurrentVersion\Uninstall\HNXS Font Manager"
} else {
    "HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\HNXS Font Manager"
}

New-Item -Path $RegistryRoot -Force | Out-Null
Set-ItemProperty -Path $RegistryRoot -Name DisplayName -Value $AppName
Set-ItemProperty -Path $RegistryRoot -Name DisplayVersion -Value $Version
Set-ItemProperty -Path $RegistryRoot -Name Publisher -Value $Publisher
Set-ItemProperty -Path $RegistryRoot -Name InstallLocation -Value $InstallRoot
Set-ItemProperty -Path $RegistryRoot -Name DisplayIcon -Value $ExePath
Set-ItemProperty -Path $RegistryRoot -Name UninstallString -Value "powershell.exe -ExecutionPolicy Bypass -File `"$UninstallScript`""
Set-ItemProperty -Path $RegistryRoot -Name NoModify -Value 1 -Type DWord
Set-ItemProperty -Path $RegistryRoot -Name NoRepair -Value 1 -Type DWord

Write-Host "$AppName $Version installed."
Write-Host "Location: $InstallRoot"
Write-Host "Run from Start Menu or: $ExePath"
