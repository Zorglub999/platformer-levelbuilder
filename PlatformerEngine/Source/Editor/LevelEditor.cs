using System;
using System.Collections.Generic;
using ImGuiNET;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Microsoft.Xna.Framework.Graphics;
using PlatformerEngine.Source.Core;
using PlatformerEngine.Source.Levels;
using PlatformerEngine.Source.Entities;

namespace PlatformerEngine.Source.Editor;

/// <summary>
/// Level editor UI and logic for editing tilemaps in real-time
/// </summary>
public class LevelEditor
{
    private TileMap tileMap;
    private Camera camera;
    private ImGuiRenderer imguiRenderer;
    private List<DraggableBlock> draggableBlocks;
    private Texture2D pixelTexture;

    // Editor state
    private int selectedTileType = TileMap.WALL;
    private bool placingDraggableBlock = false;
    private string saveFilename = "level1.json"; // Editor should just use filename, Game1 or TileMap handles path? 
    // Actually, TileMap.Save just takes a string. So we should probably prepend Levels/ here or in TileMap.
    // Let's prepend it in LevelEditor for now to be safe.
    private string levelsDirectory = "Levels";
    private bool showPalette = true;
    private bool showFileMenu = true;

    // Mouse state tracking
    private MouseState previousMouseState;

    // Screen dimensions
    private int screenWidth;
    private int screenHeight;

    public LevelEditor(TileMap tileMap, Camera camera, ImGuiRenderer imguiRenderer, int screenWidth, int screenHeight, List<DraggableBlock> draggableBlocks, Texture2D pixelTexture)
    {
        this.tileMap = tileMap;
        this.camera = camera;
        this.imguiRenderer = imguiRenderer;
        this.screenWidth = screenWidth;
        this.screenHeight = screenHeight;
        this.draggableBlocks = draggableBlocks;
        this.pixelTexture = pixelTexture;

        previousMouseState = Mouse.GetState();
    }

    /// <summary>
    /// Update editor logic - handle mouse input for tile placement
    /// </summary>
    public void Update()
    {
        MouseState mouseState = Mouse.GetState();

        // Only process mouse clicks if not over ImGui windows
        if (!imguiRenderer.WantCaptureMouse())
        {
            // Convert screen position to world position
            Vector2 mouseScreenPos = new Vector2(mouseState.X, mouseState.Y);
            Vector2 mouseWorldPos = ScreenToWorld(mouseScreenPos);

            if (placingDraggableBlock)
            {
                // Place draggable block on left click
                if (mouseState.LeftButton == ButtonState.Pressed && previousMouseState.LeftButton == ButtonState.Released)
                {
                    // Snap to grid
                    int gridX = (int)(mouseWorldPos.X / TileMap.TileSize);
                    int gridY = (int)(mouseWorldPos.Y / TileMap.TileSize);
                    Vector2 snappedPos = new Vector2(gridX * TileMap.TileSize, gridY * TileMap.TileSize);

                    draggableBlocks.Add(new DraggableBlock(snappedPos, new Vector2(TileMap.TileSize, TileMap.TileSize), pixelTexture));
                }

                // Remove draggable block on right click
                if (mouseState.RightButton == ButtonState.Pressed && previousMouseState.RightButton == ButtonState.Released)
                {
                    for (int i = draggableBlocks.Count - 1; i >= 0; i--)
                    {
                        if (draggableBlocks[i].IsPointInside(mouseWorldPos))
                        {
                            draggableBlocks.RemoveAt(i);
                            break;
                        }
                    }
                }
            }
            else
            {
                // Convert world position to grid coordinates
                int gridX = (int)(mouseWorldPos.X / TileMap.TileSize);
                int gridY = (int)(mouseWorldPos.Y / TileMap.TileSize);

                // Left click - place tile
                if (mouseState.LeftButton == ButtonState.Pressed)
                {
                    tileMap.SetTile(gridX, gridY, selectedTileType);
                }

                // Right click - erase tile
                if (mouseState.RightButton == ButtonState.Pressed)
                {
                    tileMap.SetTile(gridX, gridY, TileMap.EMPTY);
                }
            }
        }

        previousMouseState = mouseState;
    }

    /// <summary>
    /// Draw editor UI using ImGui
    /// </summary>
    public void DrawUI()
    {
        // Tile Palette Window
        if (showPalette)
        {
            ImGui.SetNextWindowPos(new System.Numerics.Vector2(10, 10), ImGuiCond.FirstUseEver);
            ImGui.SetNextWindowSize(new System.Numerics.Vector2(200, 180), ImGuiCond.FirstUseEver);

            if (ImGui.Begin("Tile Palette", ref showPalette))
            {
                ImGui.Text("Select Tool:");
                ImGui.Separator();

                // Mode selection
                bool isTileMode = !placingDraggableBlock;
                if (ImGui.RadioButton("Tile Mode", isTileMode))
                {
                    placingDraggableBlock = false;
                }

                bool isBlockMode = placingDraggableBlock;
                if (ImGui.RadioButton("Draggable Block Mode", isBlockMode))
                {
                    placingDraggableBlock = true;
                }

                ImGui.Separator();

                if (!placingDraggableBlock)
                {
                    ImGui.Text("Tile Type:");

                    // Radio buttons for tile selection
                    bool isEmpty = selectedTileType == TileMap.EMPTY;
                    if (ImGui.RadioButton("Empty (0)", isEmpty))
                    {
                        selectedTileType = TileMap.EMPTY;
                    }

                    bool isWall = selectedTileType == TileMap.WALL;
                    if (ImGui.RadioButton("Wall (1)", isWall))
                    {
                        selectedTileType = TileMap.WALL;
                    }

                    bool isSpawn = selectedTileType == TileMap.PLAYER_SPAWN;
                    if (ImGui.RadioButton("Player Spawn (2)", isSpawn))
                    {
                        selectedTileType = TileMap.PLAYER_SPAWN;
                    }

                    bool isCheckpoint = selectedTileType == TileMap.CHECKPOINT;
                    if (ImGui.RadioButton("Checkpoint (3)", isCheckpoint))
                    {
                        selectedTileType = TileMap.CHECKPOINT;
                    }

                    bool isKillBlock = selectedTileType == TileMap.KILL_BLOCK;
                    if (ImGui.RadioButton("Kill Block (4)", isKillBlock))
                    {
                        selectedTileType = TileMap.KILL_BLOCK;
                    }

                    ImGui.Separator();
                    ImGui.Text($"Current: {GetTileTypeName(selectedTileType)}");

                    // Color preview
                    var color = GetTileColor(selectedTileType);
                    ImGui.ColorButton("Preview", color, ImGuiColorEditFlags.NoAlpha, new System.Numerics.Vector2(40, 40));
                }
                else
                {
                    ImGui.Text("Draggable Block:");
                    ImGui.TextWrapped("Left Click: Place Block");
                    ImGui.TextWrapped("Right Click: Remove Block");
                    ImGui.Separator();
                    ImGui.Text($"Total Blocks: {draggableBlocks.Count}");

                    // Color preview
                    var blockColor = new System.Numerics.Vector4(0.4f, 0.8f, 0.4f, 1.0f); // Green
                    ImGui.ColorButton("Block Preview", blockColor, ImGuiColorEditFlags.NoAlpha, new System.Numerics.Vector2(40, 40));
                }
            }
            ImGui.End();
        }

        // File Operations Window
        if (showFileMenu)
        {
            ImGui.SetNextWindowPos(new System.Numerics.Vector2(10, 200), ImGuiCond.FirstUseEver);
            ImGui.SetNextWindowSize(new System.Numerics.Vector2(300, 150), ImGuiCond.FirstUseEver);

            if (ImGui.Begin("File Operations", ref showFileMenu))
            {
                ImGui.Text("Level File Management:");
                ImGui.Separator();

                // Filename input
                ImGui.InputText("Filename", ref saveFilename, 256);

                ImGui.Spacing();

                // Save button
                if (ImGui.Button("Save Level", new System.Numerics.Vector2(140, 30)))
                {
                    string path = System.IO.Path.Combine(levelsDirectory, saveFilename);
                    tileMap.Save(path, draggableBlocks);
                }

                ImGui.SameLine();

                // Load button
                if (ImGui.Button("Load Level", new System.Numerics.Vector2(140, 30)))
                {
                    string path = System.IO.Path.Combine(levelsDirectory, saveFilename);
                    tileMap.Load(path, draggableBlocks, pixelTexture);
                }

                ImGui.Spacing();
                ImGui.TextWrapped("Tip: Files are saved in the game's root directory.");
            }
            ImGui.End();
        }

        // Editor Info Window (always visible)
        ImGui.SetNextWindowPos(new System.Numerics.Vector2(screenWidth - 310, 10), ImGuiCond.FirstUseEver);
        ImGui.SetNextWindowSize(new System.Numerics.Vector2(300, 120), ImGuiCond.FirstUseEver);

        if (ImGui.Begin("Editor Info"))
        {
            ImGui.Text("EDITOR MODE ACTIVE");
            ImGui.Separator();
            ImGui.Text("Left Click: Place Tile");
            ImGui.Text("Right Click: Erase Tile");
            ImGui.Text("TAB: Return to Game Mode");
            ImGui.Separator();

            var mouseState = Mouse.GetState();
            Vector2 mouseWorldPos = ScreenToWorld(new Vector2(mouseState.X, mouseState.Y));
            int gridX = (int)(mouseWorldPos.X / TileMap.TileSize);
            int gridY = (int)(mouseWorldPos.Y / TileMap.TileSize);

            ImGui.Text($"Grid: ({gridX}, {gridY})");
        }
        ImGui.End();
    }

    /// <summary>
    /// Convert screen coordinates to world coordinates (accounting for camera)
    /// </summary>
    private Vector2 ScreenToWorld(Vector2 screenPos)
    {
        // Calculate camera offset
        Vector2 cameraOffset = new Vector2(
            camera.Position.X - screenWidth / 2f,
            camera.Position.Y - screenHeight / 2f
        );

        return screenPos + cameraOffset;
    }

    /// <summary>
    /// Get human-readable name for tile type
    /// </summary>
    private string GetTileTypeName(int tileType)
    {
        return tileType switch
        {
            TileMap.EMPTY => "Empty",
            TileMap.WALL => "Wall",
            TileMap.PLAYER_SPAWN => "Player Spawn",
            TileMap.CHECKPOINT => "Checkpoint",
            TileMap.KILL_BLOCK => "Kill Block",
            _ => "Unknown"
        };
    }

    /// <summary>
    /// Get color for tile type preview
    /// </summary>
    private System.Numerics.Vector4 GetTileColor(int tileType)
    {
        return tileType switch
        {
            TileMap.EMPTY => new System.Numerics.Vector4(0.2f, 0.2f, 0.3f, 1.0f), // Dark blue-gray
            TileMap.WALL => new System.Numerics.Vector4(0.4f, 0.4f, 0.4f, 1.0f), // Gray
            TileMap.PLAYER_SPAWN => new System.Numerics.Vector4(0.4f, 0.8f, 0.4f, 1.0f), // Green
            TileMap.CHECKPOINT => new System.Numerics.Vector4(0.4f, 0.8f, 1.0f, 1.0f), // Cyan
            TileMap.KILL_BLOCK => new System.Numerics.Vector4(1.0f, 0.2f, 0.2f, 1.0f), // Red
            _ => new System.Numerics.Vector4(1.0f, 0.0f, 1.0f, 1.0f) // Magenta for unknown
        };
    }
}
