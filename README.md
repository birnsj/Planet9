# Planet 9 - Space Adventure

A space adventure game built with MonoGame, MonoGame.Extended, and Myra UI.

## Prerequisites

- .NET 6.0 SDK or later
- MonoGame 3.8.1 or later
- Visual Studio 2022 or VS Code with C# extension

## Setup Instructions

1. **Restore NuGet Packages**
   ```bash
   dotnet restore
   ```

2. **Build the Content Pipeline**
   - Open `Content/Content.mgcb` in the MonoGame Pipeline Tool
   - Build the content (Build > Build, or Ctrl+Shift+B)
   - Alternatively, if you have the MGCB Editor extension, you can build from command line:
   ```bash
   dotnet mgcb Content/Content.mgcb
   ```

3. **Build and Run**
   ```bash
   dotnet build
   dotnet run
   ```

## Project Structure

```
Planet9/
├── Core/               # Core game systems (Scene management, etc.)
├── Scenes/            # Game scenes (MainMenu, GameScene, etc.)
├── Entities/          # Game entities (Player, Enemies, etc.)
├── UI/                # UI management and Myra integration
├── Content/           # Game content (textures, fonts, sounds)
├── Planet9Game.cs     # Main game class
├── Program.cs         # Entry point
└── Planet9.csproj     # Project file
```

## Features

- **Scene Management System**: Easy scene switching and management
- **Camera System**: Using MonoGame.Extended Camera2D
- **Entity System**: Base structure for game entities
- **UI System**: Integrated with Myra UI for modern, declarative UI
- **Modular Architecture**: Easy to extend and maintain

## Next Steps

1. Add player entity and controls
2. Implement space environment and backgrounds
3. Add enemies and combat system
4. Enhance Myra UI integration for HUD and menus
5. Add particle effects for space ambiance
6. Implement audio system

## Dependencies

- MonoGame.Framework.DesktopGL (3.8.1.303)
- MonoGame.Extended (4.0.0)
- Myra (1.3.0)

## Notes

- The project uses DesktopGL platform (works on Windows, Mac, Linux)
- Content pipeline must be built before running the game
- Default font (Arial) is included for basic text rendering


