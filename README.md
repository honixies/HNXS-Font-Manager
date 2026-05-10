# HNXS Font Manager

HNXS Font Manager is a Windows WPF application for quickly browsing, previewing, installing, renaming, and deduplicating font files from folders or ZIP archives.

Current version: `v0.1.0`

## Features

- Import font files from a folder or ZIP archive
- Restore the last opened source on startup
- Incrementally rescan folders by reusing cached font metadata when files are unchanged
- Show installed state, duplicate candidates, format, version, and source file name
- Preview selected fonts with multilingual sample text
- Batch install selected fonts for the current user or system-wide when running as administrator
- Batch rename font files with metadata-based filename templates
- Detect duplicate candidates and remove selected duplicates
- Build a self-contained Windows package and installer executable

## Requirements

- Windows 10/11
- .NET 8 SDK for development
- PowerShell for install/uninstall scripts

The published self-contained build includes the .NET runtime dependencies needed to run the app.

## Quick Start

Build and run from source:

```powershell
dotnet build HNXS-Font-Manager.sln
dotnet run --project Hnxs.FontManager\Hnxs.FontManager.csproj
```

Create a self-contained build:

```powershell
dotnet publish Hnxs.FontManager\Hnxs.FontManager.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:EnableCompressionInSingleFile=true -p:PublishReadyToRun=true -o publish\win-x64-self-contained
```

## Notes

- System-wide font installation requires administrator privileges.
- Some fonts do not contain Korean glyphs. In that case, WPF may fall back to a system font for Korean characters, and the preview header shows that Korean glyphs are missing.
- ZIP files are extracted to a temporary working folder before edits are applied.

## Documentation

- [Build and Packaging](BUILD.md)
- [Installation Notes](docs/INSTALL.md)
- [Architecture](docs/ARCHITECTURE.md)
- [Program Site Copy](docs/PROGRAM_SITE.md)
- [Project Handoff](docs/HANDOFF.md)
