# Souls Launcher

An unofficial, native Windows launcher for FromSoftware's Souls games:

- Dark Souls Remastered
- Dark Souls II: Scholar of the First Sin
- Dark Souls III
- Elden Ring
- Elden Ring Nightreign

## Features

- Automatically discovers installations across all Steam library folders
- Supports manually selecting either a Steam game folder or its `Game` subfolder
- Detects the appropriate launcher executable for each supported game
- Opens the installed mod's settings file directly from the launcher
- Stores configuration locally and does not modify game or mod files
- Runs as a native Windows application without a terminal window

## Install

1. Download the ZIP from the project's Releases page.
2. Extract the entire ZIP to a folder.
3. Run **`Souls Launcher.exe`**.

Keep the `assets` folder beside the executable. No installer is required.

Steam libraries are scanned automatically at startup, including libraries on secondary drives. You can also use **Scan Steam** in Settings or select folders manually. When a compatible settings file is found, **Open Seamless Settings** appears and opens it in your default text editor.

Configuration is stored at `%LOCALAPPDATA%\SoulsLauncher\settings.json`.

## Requirements

- Windows 10 or 11 with .NET Framework 4.8

The launcher does not download, install, bundle, or modify games or mods. Users must obtain and install any third-party modifications separately.

## Build from source

The project uses the .NET Framework compiler included with Windows development tooling. From PowerShell:

```powershell
.\build.ps1
```

The compiled application and required artwork are written to `build/`. To produce a release ZIP:

```powershell
.\package.ps1 -Version 1.0.0
```

The package is written to `dist/`.

## Artwork

The bundled panoramic backgrounds are original generated artwork created for this launcher. They contain no game logos or copied box art.

## Disclaimer

This is an independent, unofficial community project. It is not affiliated with, endorsed by, or sponsored by FromSoftware, Bandai Namco Entertainment, Valve, or any mod author or mod project. All product names and trademarks belong to their respective owners.
