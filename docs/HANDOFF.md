# Project Handoff

This document captures the current state of HNXS Font Manager so future work can resume without rediscovering the project history.

## Repository

- GitHub: `https://github.com/honixies/HNXS-Font-Manager`
- Main branch: `main`
- Commit immediately before this handoff document: `bbf492d Add Inno Setup installer build`
- Local workspace: `C:\Users\honix\Documents\Codex\HNXS-Font-Manager`

Generated build outputs are intentionally ignored by git. Rebuild them locally when needed.

## Product Summary

HNXS Font Manager is a Windows WPF application for managing font files from folders or ZIP archives.

Current app version:

```text
v0.1.0
```

Primary goals:

- Fast Windows font browsing
- Folder and ZIP import
- Installed/uninstalled filtering
- Duplicate candidate detection
- Batch install
- Metadata-based batch filename rename
- Multilingual font preview
- Incremental folder reload on startup
- GUI-based Windows installer

## Implemented Features

- WPF `.NET 8` desktop app
- App title and UI version display: `HNXS Font Manager v0.1.0`
- Custom app icon in `Hnxs.FontManager/Assets`
- Folder import
- ZIP import with temporary extraction and save-back workflow
- Last opened source restore on startup
- Incremental folder scan cache
- Font list with checkbox selection
- Combined font name and filename display
- Search by font name and filename
- Installed, uninstalled, all, and duplicate filters
- Duplicate candidate count in the navigation
- Duplicate candidate popup with auto-select delete candidates
- Current-user and system-wide install modes
- Default install mode changes based on admin state
- Admin relaunch button for system-wide installation
- Batch filename rename using font metadata
- Multiline multilingual preview text
- Preview size default: `20`
- Font info panel with version, manufacturer, designer, language support, and license
- Work log panel with limited visible rows and scrolling
- Crash log file under `%LOCALAPPDATA%\HNXS Font Manager\Logs\crash.log`

## Important Runtime Notes

- Current-user font installation copies fonts to:

```text
%LOCALAPPDATA%\Microsoft\Windows\Fonts
```

- System-wide font installation copies fonts to:

```text
%WINDIR%\Fonts
```

- System-wide installation requires administrator privileges.
- Some fonts do not include Korean glyphs. In that case, WPF may fall back to another system font for Korean preview characters.
- ZIP files are extracted to a temporary working folder before edits are applied.

## Cache And State

The app stores state under:

```text
%LOCALAPPDATA%\HNXS Font Manager\state.json
```

Folder reload is incremental. Cached metadata is reused when path, file size, and last write time are unchanged. New or changed files are parsed again.

ZIP reload is supported as a source restore, but ZIP internals are not deeply incremental in the same way as normal folders.

## Build

Development build:

```powershell
dotnet build HNXS-Font-Manager.sln
```

Run locally:

```powershell
dotnet run --project Hnxs.FontManager\Hnxs.FontManager.csproj
```

Publish for installer payload:

```powershell
dotnet publish Hnxs.FontManager\Hnxs.FontManager.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:EnableCompressionInSingleFile=true -p:PublishReadyToRun=true -o publish\win-x64-inno
```

If a sandboxed shell cannot access user temp or SDK cache folders, set temp to a writable path or run the build with normal local permissions:

```powershell
$env:TEMP='C:\tmp'
$env:TMP='C:\tmp'
```

## Installer

The recommended installer is Inno Setup.

Installer script:

```text
packaging\HNXS-Font-Manager.iss
```

Compile command:

```powershell
& "C:\Program Files (x86)\Inno Setup 6\ISCC.exe" packaging\HNXS-Font-Manager.iss
```

Generated installer:

```text
dist\HNXS-Font-Manager-v0.1.0-Setup.exe
```

At the time of handoff, Inno Setup `6.7.1` was installed locally and used successfully.

## Documentation Map

- `README.md`: general project overview
- `BUILD.md`: build and packaging commands
- `CHANGELOG.md`: release history
- `docs/INSTALL.md`: installation notes
- `docs/ARCHITECTURE.md`: implementation architecture
- `docs/PROGRAM_SITE.md`: website/product description copy
- `docs/HANDOFF.md`: current continuation notes
- `seeds/hnxs-font-manager.seed.yaml`: original product seed

## Git Workflow

Check state:

```powershell
git status --short --branch
```

Commit:

```powershell
git add <files>
git commit -m "<message>"
```

Push:

```powershell
git push
```

The repository has local git identity set to:

```text
Codex <codex@local>
```

## Suggested Next Work

Good next improvements:

- Test the Inno installer on a clean Windows user profile
- Add a GitHub Release and attach the installer EXE
- Add installer signing if distributing publicly
- Add automated build packaging script
- Add UI tests for folder import, ZIP import, selection count, duplicate popup, and install mode defaults
- Recheck font preview behavior for fonts with mixed Latin/Korean glyph coverage
- Improve duplicate detection rules if real-world font collections produce too many false positives

## Known Caveats

- Build outputs under `dist/` and `publish/` are ignored and not pushed to git.
- The installer EXE exists locally only after building.
- GitHub does not contain generated binaries unless they are attached to a Release manually.
- Inno Setup licensing should be checked before commercial distribution.
