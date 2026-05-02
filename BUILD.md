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

The project includes IExpress packaging scripts under `packaging/`.

High-level packaging flow:

1. Publish the self-contained app.
2. Stage the app payload plus install/uninstall PowerShell scripts.
3. Generate an IExpress `.sed` file.
4. Run `iexpress.exe` to create `HNXS-Font-Manager-v0.1.0-Setup.exe`.

Generated installer outputs are intentionally ignored by git.
