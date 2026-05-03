# Build and Packaging

## Development Build

```powershell
dotnet build HNXS-Font-Manager.sln
```

## Run Locally

```powershell
dotnet run --project Hnxs.FontManager\Hnxs.FontManager.csproj
```

## Self-Contained Publish

This creates a Windows x64 build that includes .NET runtime dependencies:

```powershell
dotnet publish Hnxs.FontManager\Hnxs.FontManager.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:EnableCompressionInSingleFile=true -p:PublishReadyToRun=true -o publish\win-x64-self-contained
```

WPF may still emit native companion DLLs next to the main executable.

## Installer EXE

The recommended installer is built with Inno Setup.

Install Inno Setup 6, then publish the app payload:

```powershell
dotnet publish Hnxs.FontManager\Hnxs.FontManager.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:EnableCompressionInSingleFile=true -p:PublishReadyToRun=true -o publish\win-x64-inno
```

Compile the installer script:

```powershell
& "C:\Program Files (x86)\Inno Setup 6\ISCC.exe" packaging\HNXS-Font-Manager.iss
```

The generated installer is:

```text
dist\HNXS-Font-Manager-v0.1.0-Setup.exe
```

Generated installer outputs are intentionally ignored by git.
