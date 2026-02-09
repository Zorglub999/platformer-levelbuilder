# Platformer Engine

A high-performance 2D platformer framework built with **MonoGame** and **ImGui.NET**, designed for precision gameplay and rapid development.

## 🚀 Features

- **Advanced Physics**: Custom AABB physics engine featuring:
  - Precise collision detection and resolution.
  - Wall sliding and wall jumping.
  - Variable jump height and dashing mechanics.
  - Support for moving/draggable platforms.
- **Optimized Rendering**: 
  - 512x512 TileMap system with view culling for high performance.
  - Smooth camera tracking with lerp smoothing.
- **Developer Tools**:
  - Integrated **ImGui** for realtime debugging and level editing.
  - Persistent settings management (Video, Audio, Gameplay) via JSON.
- **Game Elements**:
  - Player controller with distinct states (Idle, Run, Jump, Fall, WallSlide, Dash).
  - Hazard tiles (Kill Blocks) and Checkpoint system.

## 🎮 Controls

| Action | Input |
|--------|-------|
| **Move** | `A` / `D` or `Left` / `Right` |
| **Jump** | `Space` |
| **Dash** | `Shift` |
| **Debug Respawn** | `P` |
| **UI Interaction** | `Mouse` |

## 🛠️ Getting Started

### Prerequisites
- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)

### Running the Project
1. Open a terminal at the project root.
2. Run the engine:
   ```bash
   dotnet run --project PlatformerEngine
   ```
   *Or open `PlatformerEngine/PlatformerEngine.csproj` in Visual Studio / Rider.*

## 📂 Project Structure

- **PlatformerEngine/**: Main game source code.
  - **Source/Core/**: Engine systems (Camera, Input, Settings).
  - **Source/Entities/**: Game objects (Player, Physics Blocks).
  - **Source/Levels/**: Map data and TileMap logic.
- **Content/**: MonoGame assets (if applicable).
