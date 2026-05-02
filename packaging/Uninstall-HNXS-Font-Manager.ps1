param(
    [switch]$AllUsers
)

$ErrorActionPreference = "Stop"

$AppName = "HNXS Font Manager"

function Test-Administrator {
    $identity = [Security.Principal.WindowsIdentity]::GetCurrent()
    $principal = [Security.Principal.WindowsPrincipal]::new($identity)
    return $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
}

$PossibleInstallRoots = @(
    (Join-Path $env:LOCALAPPDATA $AppName),
    (Join-Path $env:ProgramFiles $AppName)
)

$InstallRoot = $PossibleInstallRoots | Where-Object { Test-Path -LiteralPath $_ } | Select-Object -First 1

if ($AllUsers -and -not (Test-Administrator)) {
    throw "All-users uninstall requires administrator privileges. Re-run PowerShell as Administrator."
}

Get-Process Hnxs.FontManager -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue

$StartMenuPaths = @(
    (Join-Path $env:APPDATA "Microsoft\Windows\Start Menu\Programs\$AppName"),
    (Join-Path $env:ProgramData "Microsoft\Windows\Start Menu\Programs\$AppName")
)

foreach ($path in $StartMenuPaths) {
    if (Test-Path -LiteralPath $path) {
        Remove-Item -LiteralPath $path -Recurse -Force
    }
}

$DesktopLinks = @(
    (Join-Path ([Environment]::GetFolderPath("DesktopDirectory")) "$AppName.lnk"),
    (Join-Path ([Environment]::GetFolderPath("CommonDesktopDirectory")) "$AppName.lnk")
)

foreach ($path in $DesktopLinks) {
    if (Test-Path -LiteralPath $path) {
        Remove-Item -LiteralPath $path -Force
    }
}

$RegistryPaths = @(
    "HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\HNXS Font Manager",
    "HKLM:\Software\Microsoft\Windows\CurrentVersion\Uninstall\HNXS Font Manager"
)

foreach ($path in $RegistryPaths) {
    if (Test-Path $path) {
        if ($path.StartsWith("HKLM:") -and -not (Test-Administrator)) {
            Write-Warning "Skipping machine uninstall registry key; administrator privileges are required."
            continue
        }

        Remove-Item -Path $path -Recurse -Force
    }
}

if ($InstallRoot -and (Test-Path -LiteralPath $InstallRoot)) {
    Remove-Item -LiteralPath $InstallRoot -Recurse -Force
}

Write-Host "$AppName uninstalled."
