using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.IO;
using System.Text.Json;
using System.Collections.Generic;
using PlatformerEngine.Source.Entities;
using PlatformerEngine.Source.Core;

namespace PlatformerEngine.Source.Levels
{
    /// <summary>
    /// Grid-based tilemap for level layout and collision detection
    /// </summary>
    public class TileMap
    {
        public const int TileSize = 40;

        private int[,] grid;
        private int width;
        private int height;
        private Texture2D pixelTexture;

        // Decoration layer: Map (GridX, GridY) -> TextureName
        private Dictionary<Point, string> decorationGrid;

        // Tile types
        public const int EMPTY = 0;
        public const int WALL = 1;
        public const int PLAYER_SPAWN = 2;
        public const int CHECKPOINT = 3;
        public const int KILL_BLOCK = 4;
        public const int LEVEL_END = 5;

        // Public properties for editor
        public int Width => width;
        public int Height => height;

        public TileMap(int width, int height, Texture2D pixelTexture)
        {
            this.width = width;
            this.height = height;
            this.pixelTexture = pixelTexture;
            grid = new int[width, height];
            decorationGrid = new Dictionary<Point, string>();
        }

        /// <summary>
        /// Set a tile at grid coordinates
        /// </summary>
        public void SetTile(int x, int y, int tileType)
        {
            if (x >= 0 && x < width && y >= 0 && y < height)
            {
                grid[x, y] = tileType;
            }
        }

        /// <summary>
        /// Get tile type at world position
        /// </summary>
        public int GetTileAt(Vector2 position)
        {
            int gridX = (int)(position.X / TileSize);
            int gridY = (int)(position.Y / TileSize);

            if (gridX < 0 || gridX >= width || gridY < 0 || gridY >= height)
                return EMPTY;

            return grid[gridX, gridY];
        }

        /// <summary>
        /// Get tile type at grid coordinates
        /// </summary>
        public int GetTile(int x, int y)
        {
            if (x < 0 || x >= width || y < 0 || y >= height)
                return EMPTY;

            return grid[x, y];
        }

        /// <summary>
        /// Set a decoration at grid coordinates
        /// </summary>
        public void SetDecoration(int x, int y, string textureName)
        {
            if (x >= 0 && x < width && y >= 0 && y < height)
            {
                if (string.IsNullOrEmpty(textureName))
                {
                    decorationGrid.Remove(new Point(x, y));
                }
                else
                {
                    decorationGrid[new Point(x, y)] = textureName;
                }
            }
        }

        /// <summary>
        /// Get decoration texture name at grid coordinates (or null if none)
        /// </summary>
        public string GetDecoration(int x, int y)
        {
            if (decorationGrid.TryGetValue(new Point(x, y), out string textureName))
            {
                return textureName;
            }
            return null;
        }

        /// <summary>
        /// Check if there's a solid tile at world position
        /// </summary>
        public bool IsSolid(Vector2 position)
        {
            return GetTileAt(position) == WALL;
        }

        /// <summary>
        /// Draw the tilemap
        /// </summary>
        /// <summary>
        /// Draw the tilemap
        /// </summary>
        /// <summary>
        /// Draw the tilemap
        /// </summary>
        public void Draw(SpriteBatch spriteBatch, Camera camera, int screenWidth, int screenHeight, AssetLoader assetLoader = null, bool showColliders = true)
        {
            Color wallColor = new Color(100, 100, 100) * (showColliders ? 1f : 0f); // Make invisible if not showing colliders
            if (showColliders) wallColor = new Color(100, 100, 100, 128); // Semi-transparent if showing colliders over deco

            Color spawnColor = new Color(100, 200, 100) * (showColliders ? 1f : 0f);
            if (showColliders) spawnColor = new Color(100, 200, 100, 128);

            Color checkpointColor = new Color(100, 200, 255) * (showColliders ? 1f : 0f);
            if (showColliders) checkpointColor = new Color(100, 200, 255, 128);

            Color killBlockColor = new Color(255, 50, 50) * (showColliders ? 1f : 0f);
            if (showColliders) killBlockColor = new Color(255, 50, 50, 128);

            Color levelEndColor = new Color(255, 215, 0) * (showColliders ? 1f : 0f); // Gold
            if (showColliders) levelEndColor = new Color(255, 215, 0, 128);

            // Calculate visible range
            Vector2 cameraTopLeft = camera.ScreenToWorld(Vector2.Zero, screenWidth, screenHeight);
            Vector2 cameraBottomRight = camera.ScreenToWorld(new Vector2(screenWidth, screenHeight), screenWidth, screenHeight);

            int minX = (int)(cameraTopLeft.X / TileSize) - 1;
            int maxX = (int)(cameraBottomRight.X / TileSize) + 1;
            int minY = (int)(cameraTopLeft.Y / TileSize) - 1;
            int maxY = (int)(cameraBottomRight.Y / TileSize) + 1;

            // Clamp to grid bounds
            minX = Math.Max(0, minX);
            maxX = Math.Min(width, maxX);
            minY = Math.Max(0, minY);
            maxY = Math.Min(height, maxY);

            for (int y = minY; y < maxY; y++)
            {
                for (int x = minX; x < maxX; x++)
                {
                    Rectangle destRect = new Rectangle(
                        x * TileSize,
                        y * TileSize,
                        TileSize,
                        TileSize
                    );

                    // 1. Draw Decoration (Layer 1)
                    if (assetLoader != null)
                    {
                        string decorationName = GetDecoration(x, y);
                        if (!string.IsNullOrEmpty(decorationName) && assetLoader.Decorations.ContainsKey(decorationName))
                        {
                            spriteBatch.Draw(assetLoader.Decorations[decorationName], destRect, Color.White);
                        }
                    }

                    // 2. Draw Collision (Layer 0) - Only if ShowColliders is true
                    if (showColliders)
                    {
                        int tile = grid[x, y];
                        Color tileColor = Color.Transparent;

                        if (tile == WALL)
                            tileColor = wallColor;
                        else if (tile == PLAYER_SPAWN)
                            tileColor = spawnColor;
                        else if (tile == CHECKPOINT)
                            tileColor = checkpointColor;
                        else if (tile == KILL_BLOCK)
                            tileColor = killBlockColor;
                        else if (tile == LEVEL_END)
                            tileColor = levelEndColor;

                        if (tile != EMPTY)
                        {
                            spriteBatch.Draw(pixelTexture, destRect, tileColor);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Save the tilemap to a JSON file
        /// </summary>
        public void Save(string filename, List<DraggableBlock> blocks)
        {
            try
            {
                var blockDataList = new List<DraggableBlockData>();
                if (blocks != null)
                {
                    foreach (var block in blocks)
                    {
                        blockDataList.Add(new DraggableBlockData
                        {
                            PositionX = block.Position.X,
                            PositionY = block.Position.Y,
                            Width = block.Size.X,
                            Height = block.Size.Y
                        });
                    }
                }

                // Convert decoration dictionary to list for serialization
                var decorationDataList = new List<DecorationData>();
                foreach (var kvp in decorationGrid)
                {
                    decorationDataList.Add(new DecorationData
                    {
                        X = kvp.Key.X,
                        Y = kvp.Key.Y,
                        TextureName = kvp.Value
                    });
                }

                var data = new TileMapData
                {
                    Width = width,
                    Height = height,
                    Tiles = ConvertGridToArray(),
                    Blocks = blockDataList,
                    Decorations = decorationDataList
                };

                string json = JsonSerializer.Serialize(data, new JsonSerializerOptions
                {
                    WriteIndented = true
                });

                File.WriteAllText(filename, json);
                Console.WriteLine($"Level saved to {filename}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error saving level: {ex.Message}");
            }
        }

        /// <summary>
        /// Load the tilemap from a JSON file
        /// </summary>
        public void Load(string filename, List<DraggableBlock> blocks, Texture2D blockTexture)
        {
            try
            {
                if (!File.Exists(filename))
                {
                    Console.WriteLine($"File not found: {filename}");
                    return;
                }

                string json = File.ReadAllText(filename);
                var data = JsonSerializer.Deserialize<TileMapData>(json);

                if (data != null && data.Width == width && data.Height == height)
                {
                    ConvertArrayToGrid(data.Tiles);

                    // Load blocks
                    if (blocks != null)
                    {
                        blocks.Clear();
                        if (data.Blocks != null)
                        {
                            foreach (var blockData in data.Blocks)
                            {
                                blocks.Add(new DraggableBlock(
                                    new Vector2(blockData.PositionX, blockData.PositionY),
                                    new Vector2(blockData.Width, blockData.Height),
                                    blockTexture
                                ));
                            }
                        }
                    }

                    // Load decorations
                    decorationGrid.Clear();
                    if (data.Decorations != null)
                    {
                        foreach (var deco in data.Decorations)
                        {
                            decorationGrid[new Point(deco.X, deco.Y)] = deco.TextureName;
                        }
                    }

                    Console.WriteLine($"Level loaded from {filename}");
                }
                else
                {
                    Console.WriteLine("Level dimensions don't match!");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error loading level: {ex.Message}");
            }
        }

        /// <summary>
        /// Convert 2D grid to 1D array for serialization
        /// </summary>
        private int[] ConvertGridToArray()
        {
            int[] array = new int[width * height];
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    array[y * width + x] = grid[x, y];
                }
            }
            return array;
        }

        /// <summary>
        /// Convert 1D array back to 2D grid
        /// </summary>
        private void ConvertArrayToGrid(int[] array)
        {
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    grid[x, y] = array[y * width + x];
                }
            }
        }
    }

    public class TileMapData
    {
        public int Width { get; set; }
        public int Height { get; set; }
        public int[] Tiles { get; set; }
        public List<DraggableBlockData> Blocks { get; set; }
        public List<DecorationData> Decorations { get; set; }
    }

    public class DraggableBlockData
    {
        public float PositionX { get; set; }
        public float PositionY { get; set; }
        public float Width { get; set; }
        public float Height { get; set; }
    }

    public class DecorationData
    {
        public int X { get; set; }
        public int Y { get; set; }
        public string TextureName { get; set; }
    }
}
