# Installation

## Installer EXE

Use the generated installer:

```text
HNXS-Font-Manager-v0.1.0-Setup.exe
```

The installer copies the app files, creates Start Menu shortcuts, creates an uninstall shortcut, and registers the app in Windows uninstall entries.

## Current User Install

Run the installer normally. The app is installed under:

```text
%LOCALAPPDATA%\HNXS Font Manager
```

## All Users Install

Run the installer as administrator, or run the script manually:

```powershell
.\Install-HNXS-Font-Manager.ps1 -AllUsers
```

The app is installed under:

```text
%ProgramFiles%\HNXS Font Manager
```

## Uninstall

Use the Start Menu uninstall shortcut or Windows Apps uninstall entry.

Manual uninstall:

```powershell
.\Uninstall-HNXS-Font-Manager.ps1
```
