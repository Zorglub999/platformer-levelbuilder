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
    private AssetLoader assetLoader;

    // Editor state
    private int selectedTileType = TileMap.WALL;
    private string selectedDecoration = null;
    private bool placingDraggableBlock = false;
    private string saveFilename = "level1.json";
    private string levelsDirectory = "Levels";
    private bool showPalette = true;
    private bool showFileMenu = true;
    public bool ShowColliders { get; private set; } = true;

    // Editor Tabs
    private int currentTab = 0; // 0 = Layout, 1 = Deco

    // ImGui Texture IDs
    private Dictionary<string, IntPtr> decorationTextureIds = new Dictionary<string, IntPtr>();

    // Mouse state tracking
    private MouseState previousMouseState;
    private KeyboardState previousKeyboardState;

    // Tools
    private enum EditorTool
    {
        Pencil,
        Select
    }
    private EditorTool currentTool = EditorTool.Pencil;

    // Selection State
    private Rectangle? selectionRect;
    private bool isDraggingSelection = false;
    private Vector2 dragStartPos;
    private Rectangle dragStartRect;
    // Clipboard
    private int[,] clipboardTiles;
    private string[,] clipboardDecorations;
    private List<DraggableBlock> clipboardBlocks;

    // Selection Moving temporary storage
    private int[,] movingTiles;
    private string[,] movingDecorations;
    private List<DraggableBlock> movingBlocks;
    private bool isMovingSelection = false; // distinct from dragging the selection rectangle creation


    // Screen dimensions
    private int screenWidth;
    private int screenHeight;

    public LevelEditor(TileMap tileMap, Camera camera, ImGuiRenderer imguiRenderer, int screenWidth, int screenHeight, List<DraggableBlock> draggableBlocks, Texture2D pixelTexture, AssetLoader assetLoader)
    {
        this.tileMap = tileMap;
        this.camera = camera;
        this.imguiRenderer = imguiRenderer;
        this.screenWidth = screenWidth;
        this.screenHeight = screenHeight;
        this.draggableBlocks = draggableBlocks;
        this.pixelTexture = pixelTexture;
        this.assetLoader = assetLoader;

        previousMouseState = Mouse.GetState();

        // Bind decoration textures for ImGui
        if (assetLoader != null)
        {
            foreach (var kvp in assetLoader.Decorations)
            {
                IntPtr id = imguiRenderer.BindTexture(kvp.Value);
                decorationTextureIds[kvp.Key] = id;
            }
        }
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

            // Convert world position to grid coordinates
            int gridX = (int)(mouseWorldPos.X / TileMap.TileSize);
            int gridY = (int)(mouseWorldPos.Y / TileMap.TileSize);

            if (currentTool == EditorTool.Pencil)
            {
                UpdatePencilTool(mouseState, gridX, gridY, mouseWorldPos);
            }
            else if (currentTool == EditorTool.Select)
            {
                UpdateSelectTool(mouseState, mouseWorldPos);
            }
        }

        previousMouseState = mouseState;
        previousKeyboardState = Keyboard.GetState();
    }

    private void UpdatePencilTool(MouseState mouseState, int gridX, int gridY, Vector2 mouseWorldPos)
    {
        if (currentTab == 0) // Layout Mode
        {
            if (placingDraggableBlock)
            {
                // Place draggable block on left click
                if (mouseState.LeftButton == ButtonState.Pressed && previousMouseState.LeftButton == ButtonState.Released)
                {
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
        else if (currentTab == 1) // Deco Mode
        {
            // Left click - place decoration
            if (mouseState.LeftButton == ButtonState.Pressed)
            {
                tileMap.SetDecoration(gridX, gridY, selectedDecoration);
            }

            // Right click - erase decoration
            if (mouseState.RightButton == ButtonState.Pressed)
            {
                tileMap.SetDecoration(gridX, gridY, null);
            }
        }
    }

    private void UpdateSelectTool(MouseState mouseState, Vector2 mouseWorldPos)
    {
        KeyboardState keyState = Keyboard.GetState();

        // --- Shortcuts ---
        // Delete
        if (keyState.IsKeyDown(Keys.Delete) && previousKeyboardState.IsKeyUp(Keys.Delete))
        {
            DeleteSelection();
        }
        // Copy (Ctrl+C)
        if (keyState.IsKeyDown(Keys.LeftControl) && keyState.IsKeyDown(Keys.C) && previousKeyboardState.IsKeyUp(Keys.C))
        {
            CopySelection();
        }
        // Paste (Ctrl+V)
        if (keyState.IsKeyDown(Keys.LeftControl) && keyState.IsKeyDown(Keys.V) && previousKeyboardState.IsKeyUp(Keys.V))
        {
            PasteClipboard(mouseWorldPos);
        }

        // --- Mouse Interaction ---
        if (mouseState.LeftButton == ButtonState.Pressed && previousMouseState.LeftButton == ButtonState.Released)
        {
            // Just clicked
            if (isMovingSelection)
            {
                // Place it
                PlaceMovingSelection();
            }
            else if (selectionRect.HasValue && selectionRect.Value.Contains(mouseWorldPos))
            {
                // Clicked inside existing selection -> Start Dragging/Moving it
                StartMovingSelection(mouseWorldPos);
            }
            else
            {
                // Clicked outside -> Start New Selection
                isDraggingSelection = true;
                dragStartPos = mouseWorldPos;
                selectionRect = new Rectangle((int)mouseWorldPos.X, (int)mouseWorldPos.Y, 0, 0);
            }
        }
        else if (mouseState.LeftButton == ButtonState.Pressed)
        {
            // Holding click
            Vector2 currentPos = mouseWorldPos;

            if (isMovingSelection)
            {
                // Move the selection relative to start
                Vector2 delta = currentPos - dragStartPos;
                Rectangle currentRect = dragStartRect;
                currentRect.X += (int)delta.X;
                currentRect.Y += (int)delta.Y;

                // Snap to grid? Maybe not for smooth drag, but for placement yes.
                // Let's keep it smooth for now, snap on release.
                selectionRect = currentRect;
            }
            else if (isDraggingSelection)
            {
                // Update selection rect
                int x = (int)Math.Min(dragStartPos.X, currentPos.X);
                int y = (int)Math.Min(dragStartPos.Y, currentPos.Y);
                int w = (int)Math.Abs(dragStartPos.X - currentPos.X);
                int h = (int)Math.Abs(dragStartPos.Y - currentPos.Y);
                selectionRect = new Rectangle(x, y, w, h);
            }
        }
        else if (mouseState.LeftButton == ButtonState.Released && previousMouseState.LeftButton == ButtonState.Pressed)
        {
            // Released click
            if (isDraggingSelection)
            {
                isDraggingSelection = false;
                // Snap selection rect to grid? 
                // It helps if we select full tiles.
            }
            else if (isMovingSelection)
            {
                PlaceMovingSelection();
            }
        }
    }

    private void DeleteSelection()
    {
        if (selectionRect.HasValue)
        {
            Rectangle rect = selectionRect.Value;
            var (minX, minY, maxX, maxY) = GetGridBounds(rect);

            for (int x = minX; x <= maxX; x++)
            {
                for (int y = minY; y <= maxY; y++)
                {
                    tileMap.SetTile(x, y, TileMap.EMPTY);
                    tileMap.SetDecoration(x, y, null);
                }
            }

            // Also delete blocks inside
            for (int i = draggableBlocks.Count - 1; i >= 0; i--)
            {
                if (rect.Contains(draggableBlocks[i].GetBounds()))
                {
                    draggableBlocks.RemoveAt(i);
                }
            }
        }
    }

    private void CopySelection()
    {
        if (selectionRect.HasValue)
        {
            Rectangle rect = selectionRect.Value;
            var (minX, minY, maxX, maxY) = GetGridBounds(rect);
            int w = maxX - minX + 1;
            int h = maxY - minY + 1;

            clipboardTiles = new int[w, h];
            clipboardDecorations = new string[w, h];
            clipboardBlocks = new List<DraggableBlock>();

            // Copy Tiles and Decorations
            for (int x = 0; x < w; x++)
            {
                for (int y = 0; y < h; y++)
                {
                    clipboardTiles[x, y] = tileMap.GetTile(minX + x, minY + y);
                    clipboardDecorations[x, y] = tileMap.GetDecoration(minX + x, minY + y);
                }
            }

            // Copy Blocks (relative position)
            foreach (var block in draggableBlocks)
            {
                if (rect.Intersects(block.GetBounds()))
                {
                    // Create copy
                    var newBlock = new DraggableBlock(
                        block.Position - new Vector2(rect.X, rect.Y), // Relative pos
                        block.Size,
                        pixelTexture
                    );
                    clipboardBlocks.Add(newBlock);
                }
            }
        }
    }

    private void PasteClipboard(Vector2 pos)
    {
        if (clipboardTiles != null)
        {
            movingTiles = (int[,])clipboardTiles.Clone();
            movingDecorations = (string[,])clipboardDecorations.Clone();
            movingBlocks = new List<DraggableBlock>();
            foreach (var b in clipboardBlocks)
            {
                movingBlocks.Add(new DraggableBlock(b.Position, b.Size, pixelTexture));
            }

            isMovingSelection = true;
            dragStartPos = pos; // Anchor for relative movement

            // Initial rect setup
            int w = movingTiles.GetLength(0) * TileMap.TileSize;
            int h = movingTiles.GetLength(1) * TileMap.TileSize;
            selectionRect = new Rectangle((int)pos.X, (int)pos.Y, w, h);
            dragStartRect = selectionRect.Value;
        }
    }

    private void StartMovingSelection(Vector2 mousePos)
    {
        CopySelection(); // Copy to clipboard/temp first
        DeleteSelection(); // Remove from world

        // Move to 'Moving' state using the copied data
        movingTiles = clipboardTiles; // Reference is fine since we just created it
        movingDecorations = clipboardDecorations;
        movingBlocks = clipboardBlocks;

        isMovingSelection = true;
        dragStartPos = mousePos;
        dragStartRect = selectionRect.Value;
    }

    private void PlaceMovingSelection()
    {
        if (isMovingSelection && selectionRect.HasValue)
        {
            // Snap rect to Grid
            Rectangle rect = selectionRect.Value;
            int snapX = (int)Math.Round(rect.X / (float)TileMap.TileSize) * TileMap.TileSize;
            int snapY = (int)Math.Round(rect.Y / (float)TileMap.TileSize) * TileMap.TileSize;

            // Re-calculate grid bounds based on snapped position
            int gridX = snapX / TileMap.TileSize;
            int gridY = snapY / TileMap.TileSize;

            int w = movingTiles.GetLength(0);
            int h = movingTiles.GetLength(1);

            // Place Tiles and Decorations
            for (int x = 0; x < w; x++)
            {
                for (int y = 0; y < h; y++)
                {
                    tileMap.SetTile(gridX + x, gridY + y, movingTiles[x, y]);
                    // Place decoration (even if null, to overwrite/erase)
                    tileMap.SetDecoration(gridX + x, gridY + y, movingDecorations[x, y]);
                }
            }

            // Place Blocks
            foreach (var b in movingBlocks)
            {
                Vector2 finalPos = new Vector2(snapX, snapY) + b.Position;
                draggableBlocks.Add(new DraggableBlock(finalPos, b.Size, pixelTexture));
            }

            // Update selection rect to final snapped position
            selectionRect = new Rectangle(snapX, snapY, w * TileMap.TileSize, h * TileMap.TileSize);

            isMovingSelection = false;
            movingTiles = null;
            movingBlocks = null;
        }
    }

    private (int, int, int, int) GetGridBounds(Rectangle rect)
    {
        int minX = (int)Math.Floor(rect.Left / (float)TileMap.TileSize);
        int minY = (int)Math.Floor(rect.Top / (float)TileMap.TileSize);
        int maxX = (int)Math.Floor((rect.Right - 1) / (float)TileMap.TileSize); // -1 to handle exact edge cases
        int maxY = (int)Math.Floor((rect.Bottom - 1) / (float)TileMap.TileSize);

        return (minX, minY, maxX, maxY);
    }

    public void DrawWorld(SpriteBatch spriteBatch)
    {
        // Draw Selection Rect
        if (selectionRect.HasValue)
        {
            DrawRectangle(spriteBatch, selectionRect.Value, Color.Yellow, 2);
        }

        // Draw Moving Preview
        if (isMovingSelection && movingTiles != null && selectionRect.HasValue)
        {
            Rectangle rect = selectionRect.Value;
            // Draw semi-transparent preview of tiles/decos
            int w = movingTiles.GetLength(0);
            int h = movingTiles.GetLength(1);

            for (int x = 0; x < w; x++)
            {
                for (int y = 0; y < h; y++)
                {
                    Vector2 pos = new Vector2(rect.X + x * TileMap.TileSize, rect.Y + y * TileMap.TileSize);
                    Rectangle dest = new Rectangle((int)pos.X, (int)pos.Y, TileMap.TileSize, TileMap.TileSize);

                    // Draw Tile Preview
                    if (movingTiles[x, y] != TileMap.EMPTY)
                    {
                        // Just a colored box for tile preview
                        // We could use GetTileColor but that returns Vector4, need Color
                        spriteBatch.Draw(pixelTexture, dest, Color.White * 0.5f);
                    }

                    // Draw Deco Preview
                    if (assetLoader != null && movingDecorations != null && movingDecorations[x, y] != null)
                    {
                        string decoName = movingDecorations[x, y];
                        if (assetLoader.Decorations.ContainsKey(decoName))
                        {
                            spriteBatch.Draw(assetLoader.Decorations[decoName], dest, Color.White * 0.5f);
                        }
                    }
                }
            }

            // Draw border
            DrawRectangle(spriteBatch, rect, Color.Yellow, 2);
        }
    }

    private void DrawRectangle(SpriteBatch spriteBatch, Rectangle rect, Color color, int thickness)
    {
        spriteBatch.Draw(pixelTexture, new Rectangle(rect.X, rect.Y, rect.Width, thickness), color);
        spriteBatch.Draw(pixelTexture, new Rectangle(rect.X, rect.Bottom - thickness, rect.Width, thickness), color);
        spriteBatch.Draw(pixelTexture, new Rectangle(rect.X, rect.Y, thickness, rect.Height), color);
        spriteBatch.Draw(pixelTexture, new Rectangle(rect.Right - thickness, rect.Y, thickness, rect.Height), color);
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
            ImGui.SetNextWindowSize(new System.Numerics.Vector2(250, 400), ImGuiCond.FirstUseEver);

            if (ImGui.Begin("Tools", ref showPalette))
            {
                if (ImGui.BeginTabBar("EditorTabs"))
                {
                    // Tab: Layout (Physics)
                    if (ImGui.BeginTabItem("Layout"))
                    {
                        currentTab = 0;
                        ImGui.Text("Physics & Collision");
                        ImGui.Separator();

                        // Tool selection
                        ImGui.Text("Tool:");
                        if (ImGui.RadioButton("Pencil", currentTool == EditorTool.Pencil)) currentTool = EditorTool.Pencil;
                        ImGui.SameLine();
                        if (ImGui.RadioButton("Select", currentTool == EditorTool.Select)) currentTool = EditorTool.Select;
                        ImGui.Separator();

                        // Mode selection
                        bool isTileMode = !placingDraggableBlock;
                        if (ImGui.RadioButton("Tile Mode", isTileMode))
                        {
                            placingDraggableBlock = false;
                        }

                        bool isBlockMode = placingDraggableBlock;
                        if (ImGui.RadioButton("Draggable Block", isBlockMode))
                        {
                            placingDraggableBlock = true;
                        }

                        ImGui.Separator();

                        if (!placingDraggableBlock)
                        {
                            ImGui.Text("Tile Type:");

                            // Radio buttons for tile selection
                            // Simple array or loop would be cleaner but let's stick to explicit for now
                            if (ImGui.RadioButton("Empty (0)", selectedTileType == TileMap.EMPTY)) selectedTileType = TileMap.EMPTY;
                            if (ImGui.RadioButton("Wall (1)", selectedTileType == TileMap.WALL)) selectedTileType = TileMap.WALL;
                            if (ImGui.RadioButton("Player Spawn (2)", selectedTileType == TileMap.PLAYER_SPAWN)) selectedTileType = TileMap.PLAYER_SPAWN;
                            if (ImGui.RadioButton("Checkpoint (3)", selectedTileType == TileMap.CHECKPOINT)) selectedTileType = TileMap.CHECKPOINT;
                            if (ImGui.RadioButton("Kill Block (4)", selectedTileType == TileMap.KILL_BLOCK)) selectedTileType = TileMap.KILL_BLOCK;
                            if (ImGui.RadioButton("Level End (5)", selectedTileType == TileMap.LEVEL_END)) selectedTileType = TileMap.LEVEL_END;

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
                        }
                        ImGui.EndTabItem();
                    }

                    // Tab: Deco (Visuals)
                    if (ImGui.BeginTabItem("Deco"))
                    {
                        currentTab = 1;
                        ImGui.Text("Visual Decorations");
                        ImGui.Separator();

                        ImGui.TextWrapped("Select a decoration to paint:");

                        // "None" option (eraser)
                        bool isEraser = selectedDecoration == null;
                        if (isEraser)
                        {
                            ImGui.PushStyleColor(ImGuiCol.Button, new System.Numerics.Vector4(0.5f, 0.2f, 0.2f, 1f));
                        }
                        if (ImGui.Button("Eraser (None)", new System.Numerics.Vector2(100, 30)))
                        {
                            selectedDecoration = null;
                        }
                        if (isEraser) // Do not pop if not pushed!
                        {
                            ImGui.PopStyleColor();
                        }

                        ImGui.Separator();

                        // Grid of Image Buttons
                        if (assetLoader != null)
                        {
                            ImGuiStylePtr style = ImGui.GetStyle();
                            float windowVisibleX = ImGui.GetWindowPos().X + ImGui.GetWindowWidth() - style.WindowPadding.X;
                            float buttonSize = 64f;

                            foreach (var decoName in assetLoader.Decorations.Keys)
                            {
                                if (decorationTextureIds.ContainsKey(decoName))
                                {
                                    IntPtr texParams = decorationTextureIds[decoName];

                                    // Highlight selected
                                    bool isSelected = selectedDecoration == decoName;
                                    if (isSelected)
                                    {
                                        ImGui.PushStyleColor(ImGuiCol.Button, new System.Numerics.Vector4(1f, 1f, 0f, 0.5f)); // Yellow tint
                                    }

                                    // ImageButton uses the texture ID
                                    ImGui.PushID(decoName);
                                    if (ImGui.ImageButton(decoName, texParams, new System.Numerics.Vector2(buttonSize, buttonSize)))
                                    {
                                        selectedDecoration = decoName;
                                    }
                                    ImGui.PopID();

                                    if (isSelected)
                                    {
                                        ImGui.PopStyleColor();
                                    }

                                    // Simple tooltip for name
                                    if (ImGui.IsItemHovered())
                                    {
                                        ImGui.SetTooltip(decoName);
                                    }

                                    // Auto-layout grid logic from ImGui demo
                                    float lastButtonX = ImGui.GetItemRectMax().X;
                                    float nextButtonX = lastButtonX + style.ItemSpacing.X + buttonSize;

                                    if (nextButtonX < windowVisibleX)
                                    {
                                        ImGui.SameLine();
                                    }
                                }
                            }
                        }
                        else
                        {
                            ImGui.TextColored(new System.Numerics.Vector4(1, 0, 0, 1), "AssetLoader not initialized!");
                        }

                        ImGui.Separator();
                        ImGui.Text($"Selected: {selectedDecoration ?? "Eraser"}");
                        ImGui.EndTabItem();
                    }

                    ImGui.EndTabBar();
                }

                ImGui.Separator();
                ImGui.Spacing();
                bool showCol = ShowColliders;
                if (ImGui.Checkbox("Show Colliders", ref showCol))
                {
                    ShowColliders = showCol;
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
            TileMap.LEVEL_END => "Level End",
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
            TileMap.LEVEL_END => new System.Numerics.Vector4(1.0f, 0.8f, 0.2f, 1.0f), // Gold
            _ => new System.Numerics.Vector4(1.0f, 0.0f, 1.0f, 1.0f) // Magenta for unknown
        };
    }
}
