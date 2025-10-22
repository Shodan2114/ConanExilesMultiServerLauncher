# Conan Multi-Server Launcher

A lightweight Windows WPF utility to manage multiple Conan Exiles server profiles, handle mod lists, and launch the game with or without BattlEye. Includes quality-of-life features like automatic modlist writing, last server auto-connect, and a quick “Kill Conan Tasks” action when the game hangs.

## Features
- Profiles
  - Create, save, delete, and quickly switch profiles
  - Per-profile `Server Address (ip:port)`, optional password
  - Per-profile `Enable BattleEye` toggle
- Mods management
  - Paste mod IDs, paste Steam collection URL, load from `.txt`, or clear
  - Locate `ConanSandbox\servermodlist.txt` and Steam Workshop `440900` folder
  - Write both `ConanSandbox\servermodlist.txt` and `%LocalAppData%\modlist.txt`
- Game launching
  - Launch Conan with BattlEye or the standard executable
  - Optional `Close launcher after clicking Launch`
  - Optional `Write modlist automatically when switching profile`
  - Optional `Texture Streaming` setting; when disabled, passes `-notexturestreaming` to the game
- Convenience
  - "Kill Conan Tasks" button to terminate stuck `ConanSandbox` processes


## Requirements
- Windows 10/11
- .NET 6 Desktop Runtime (or SDK to build): `net6.0-windows`
- Conan Exiles installed (Steam default layout expected)

## Installation
- Option A: Build from source (see below) and run `ConanMultiServerLauncher.exe` from the build output.
- Option B: Download a release build from the GitHub Releases page (if available), unzip, and run the executable.

The app stores settings and profiles in `%AppData%\ConanMultiServerLauncher`.

## Build from source
```powershell
# Restore & build
 dotnet build .\ConanMultiServerLauncher\ConanMultiServerLauncher.csproj -c Release

# Run (optional)
 dotnet run --project .\ConanMultiServerLauncher\ConanMultiServerLauncher.csproj
