using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using System.Collections.Generic;
using PlatformerEngine.Source.Core;
using PlatformerEngine.Source.Entities;
using PlatformerEngine.Source.Levels;
using PlatformerEngine.Source.Editor;
using ImGuiNET;

namespace PlatformerEngine;

// Define Game States
public enum GameState
{
    MainMenu,
    Playing,
    Settings,
    Credits,
    LevelSelect
}

public class Game1 : Game
{
    // Static random for particle system
    public static System.Random Random = new System.Random();

    private GraphicsDeviceManager _graphics;
    private SpriteBatch _spriteBatch;

    // Core systems
    private Texture2D pixelTexture;
    private Camera camera;
    private TileMap tileMap;
    private Player player;
    private ParticleSystem particleSystem;
    private List<DraggableBlock> draggableBlocks;
    private AssetLoader assetLoader;

    // Editor systems
    private ImGuiRenderer imguiRenderer;
    private LevelEditor levelEditor;
    private bool isEditorMode = false;

    // UI Systems
    private PlatformerEngine.Source.UI.MenuSystem menuSystem;

    // Game State
    private GameState currentState = GameState.MainMenu;

    // Input state tracking
    private KeyboardState previousKeyboardState;

    // UI Animation State
    private float menuTime = 0f;

    // Level Selection
    private List<string> levelFiles = new List<string>();

    public Game1()
    {
        _graphics = new GraphicsDeviceManager(this);
        Content.RootDirectory = "Content";
        IsMouseVisible = true;

        // Load settings
        SettingsManager.Initialize();
        SettingsManager.Apply(_graphics);
    }

    protected override void Initialize()
    {
        base.Initialize();
        InputManager.Initialize();
        Window.Title = "Celeste mais en moins bien";
    }

    protected override void LoadContent()
    {
        _spriteBatch = new SpriteBatch(GraphicsDevice);

        // Create 1x1 white pixel texture for drawing colored rectangles
        pixelTexture = new Texture2D(GraphicsDevice, 1, 1);
        pixelTexture.SetData(new[] { Color.White });

        // Initialize camera
        camera = new Camera();

        // Initialize tilemap (512x512 tiles for expanded world)
        tileMap = new TileMap(512, 512, pixelTexture);

        // Initialize particle system
        particleSystem = new ParticleSystem();

        // Initialize draggable blocks (empty by default)
        draggableBlocks = new List<DraggableBlock>();

        // Initialize player at spawn position (needs draggable blocks for collision)
        player = new Player(new Vector2(100, 100), tileMap, pixelTexture, particleSystem, draggableBlocks);

        // Initialize Asset Loader and load decorations
        assetLoader = new AssetLoader(GraphicsDevice);
        assetLoader.LoadDecorations("Content/Decorations");

        player.OnLevelComplete += () =>
        {
            currentState = GameState.MainMenu;
            IsMouseVisible = true;
        };

        // Initialize editor systems
        imguiRenderer = new ImGuiRenderer(this);
        levelEditor = new LevelEditor(
            tileMap,
            camera,
            imguiRenderer,
            _graphics.PreferredBackBufferWidth,
            _graphics.PreferredBackBufferHeight,
            draggableBlocks,
            pixelTexture,
            assetLoader
        );

        // Initialize Menu System
        menuSystem = new PlatformerEngine.Source.UI.MenuSystem(this);
        menuSystem.LoadContent();

        previousKeyboardState = Keyboard.GetState();
    }

    // Public methods for MenuSystem to call
    public void StartGame()
    {
        RefreshLevelList();
        currentState = GameState.LevelSelect;
    }

    public void OpenSettings()
    {
        currentState = GameState.Settings;
    }

    public void OpenCredits()
    {
        currentState = GameState.Credits;
    }

    private void LoadLevel(string filename)
    {
        // Use Levels folder
        string path = System.IO.Path.Combine("Levels", filename);
        // Pass draggableBlocks and pixelTexture to Load
        tileMap.Load(path, draggableBlocks, pixelTexture);

        player.Position = new Vector2(100, 100);

        // Find spawn point in map
        for (int x = 0; x < tileMap.Width; x++)
        {
            for (int y = 0; y < tileMap.Height; y++)
            {
                if (tileMap.GetTile(x, y) == TileMap.PLAYER_SPAWN)
                {
                    player.Position = new Vector2(x * TileMap.TileSize, y * TileMap.TileSize);
                    break;
                }
            }
        }

        player.SetSpawnPoint(player.Position);

        // If level loaded is empty (all 0s), generate a default one
        // Check if bottom row is empty as a heuristic
        if (!tileMap.IsSolid(new Vector2(100, 511 * TileMap.TileSize)))
        {
            GenerateDefaultLevel();
        }
    }

    private void GenerateDefaultLevel()
    {
        // Floor
        for (int x = 0; x < tileMap.Width; x++)
        {
            tileMap.SetTile(x, tileMap.Height - 1, TileMap.WALL);
            tileMap.SetTile(x, tileMap.Height - 2, TileMap.WALL);
        }

        // Left/Right Walls
        for (int y = 0; y < tileMap.Height; y++)
        {
            tileMap.SetTile(0, y, TileMap.WALL);
            tileMap.SetTile(tileMap.Width - 1, y, TileMap.WALL);
        }

        // Some platforms
        for (int x = 10; x < 20; x++) tileMap.SetTile(x, tileMap.Height - 6, TileMap.WALL);
        for (int x = 25; x < 35; x++) tileMap.SetTile(x, tileMap.Height - 10, TileMap.WALL);

        // Spawn Point
        player.Position = new Vector2(100, (tileMap.Height - 4) * TileMap.TileSize);
    }

    protected override void Update(GameTime gameTime)
    {
        InputManager.Update(); // Update inputs (Keyboard, GamePad, Mouse)

        KeyboardState keyboardState = Keyboard.GetState();

        if (keyboardState.IsKeyDown(Keys.Escape))
        {
            if (currentState == GameState.Playing)
            {
                currentState = GameState.MainMenu;
                IsMouseVisible = true;
            }
            else if (currentState == GameState.LevelSelect)
            {
                currentState = GameState.MainMenu;
            }
        }

        // Update logic based on state
        if (currentState == GameState.MainMenu)
        {
            menuSystem.Update(gameTime);
        }
        else if (currentState != GameState.Playing)
        {
            // Other menus updated via ImGui logic or checking inputs if needed
            menuTime += (float)gameTime.ElapsedGameTime.TotalSeconds;
            // Existing particle system update for other menus if any
            particleSystem.Update();
        }

        // Toggle editor mode with TAB key (Only in Playing state)
        if (currentState == GameState.Playing && keyboardState.IsKeyDown(Keys.Tab) && previousKeyboardState.IsKeyUp(Keys.Tab))
        {
            isEditorMode = !isEditorMode;
            IsMouseVisible = isEditorMode; // Show system mouse only in editor
        }

        if (currentState == GameState.Playing)
        {
            if (isEditorMode)
            {
                // Editor mode - update editor, pause game logic
                levelEditor.Update();

                // Manual Camera Control in Editor
                float camSpeed = 500f * (float)gameTime.ElapsedGameTime.TotalSeconds;
                if (keyboardState.IsKeyDown(Keys.LeftShift)) camSpeed *= 2f;

                if (keyboardState.IsKeyDown(Keys.W) || keyboardState.IsKeyDown(Keys.Up)) camera.Position += new Vector2(0, -camSpeed);
                if (keyboardState.IsKeyDown(Keys.S) || keyboardState.IsKeyDown(Keys.Down)) camera.Position += new Vector2(0, camSpeed);
                if (keyboardState.IsKeyDown(Keys.A) || keyboardState.IsKeyDown(Keys.Left)) camera.Position += new Vector2(-camSpeed, 0);
                if (keyboardState.IsKeyDown(Keys.D) || keyboardState.IsKeyDown(Keys.Right)) camera.Position += new Vector2(camSpeed, 0);
            }
            else
            {
                // Game mode - normal game logic
                player.Update(camera);

                // Update particle system
                particleSystem.Update();

                // Update draggable blocks
                foreach (var block in draggableBlocks)
                {
                    block.Update(camera, _graphics.PreferredBackBufferWidth, _graphics.PreferredBackBufferHeight);
                }

                // Camera room clamping
                Vector2 playerCenter = player.Position + player.Size * 0.5f;
                camera.Update(playerCenter, _graphics.PreferredBackBufferWidth, _graphics.PreferredBackBufferHeight);
            }
        }
        // Menu updates handled by ImGui

        previousKeyboardState = keyboardState;
        base.Update(gameTime);
    }

    protected override void Draw(GameTime gameTime)
    {
        GraphicsDevice.Clear(new Color(20, 20, 30)); // Deep dark blue background for menu

        if (currentState == GameState.MainMenu)
        {
            menuSystem.Draw(gameTime);
        }
        else if (currentState != GameState.Playing)
        {
            // Draw menu background particles for other states (LevelSelect, Settings)
            _spriteBatch.Begin(blendState: BlendState.Additive);
            particleSystem.Draw(_spriteBatch, pixelTexture);
            _spriteBatch.End();
        }

        if (currentState == GameState.Playing)
        {
            GraphicsDevice.Clear(new Color(50, 50, 70)); // Game background

            // Draw Game World with Camera
            Matrix viewMatrix = camera.GetViewMatrix(
                _graphics.PreferredBackBufferWidth,
                _graphics.PreferredBackBufferHeight
            );

            _spriteBatch.Begin(
                sortMode: SpriteSortMode.Deferred,
                blendState: BlendState.AlphaBlend,
                samplerState: SamplerState.PointClamp, // Pixel-perfect rendering
                transformMatrix: viewMatrix
            );

            bool showColliders = levelEditor?.ShowColliders ?? true;

            tileMap.Draw(_spriteBatch, camera, _graphics.PreferredBackBufferWidth, _graphics.PreferredBackBufferHeight, assetLoader, showColliders);

            // Draw draggable blocks
            foreach (var block in draggableBlocks)
            {
                block.Draw(_spriteBatch);
            }

            // Draw player (only in game mode or dimmed in editor mode)
            if (!isEditorMode)
            {
                player.Draw(_spriteBatch);
            }

            // Draw particles on top
            particleSystem.Draw(_spriteBatch, pixelTexture);

            // Draw Virtual Cursor if using GamePad (or always for now to see it)
            if (InputManager.CursorPosition != Vector2.Zero)
            {
                Vector2 cursor = InputManager.CursorPosition;
                int cursorSize = 10;
                Rectangle cursorRectH = new Rectangle((int)cursor.X - cursorSize / 2, (int)cursor.Y - 1, cursorSize, 2);
                Rectangle cursorRectV = new Rectangle((int)cursor.X - 1, (int)cursor.Y - cursorSize / 2, 2, cursorSize);
                _spriteBatch.Draw(pixelTexture, cursorRectH, Color.Yellow);
                _spriteBatch.Draw(pixelTexture, cursorRectV, Color.Yellow);
            }

            if (isEditorMode)
            {
                levelEditor.DrawWorld(_spriteBatch);
            }

            _spriteBatch.End();
        }

        // Draw UI (ImGui)
        imguiRenderer.BeginLayout(gameTime);

        if (currentState == GameState.MainMenu)
        {
            // Draw UI labels for MenuSystem
            menuSystem.DrawUI();
        }
        else if (currentState == GameState.Settings)
        {
            DrawSettingsMenu();
        }
        else if (currentState == GameState.Credits)
        {
            DrawCreditsMenu();
        }
        else if (currentState == GameState.Playing && isEditorMode)
        {
            levelEditor.DrawUI();
        }
        else if (currentState == GameState.LevelSelect)
        {
            DrawLevelSelectMenu();
        }

        imguiRenderer.EndLayout();

        base.Draw(gameTime);
    }

    // ... Keep existing DrawSettingsMenu, DrawCreditsMenu, DrawLevelSelectMenu, RefreshLevelList methods ...
    private void DrawSettingsMenu()
    {
        ImGui.PushStyleVar(ImGuiStyleVar.WindowRounding, 10f);
        ImGui.PushStyleColor(ImGuiCol.WindowBg, new System.Numerics.Vector4(0, 0, 0, 0.8f));

        ImGui.SetNextWindowPos(new System.Numerics.Vector2(_graphics.PreferredBackBufferWidth / 2 - 200, _graphics.PreferredBackBufferHeight / 2 - 150));
        ImGui.SetNextWindowSize(new System.Numerics.Vector2(400, 300));

        if (ImGui.Begin("Settings", ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoMove | ImGuiWindowFlags.NoTitleBar))
        {
            ImGui.SetWindowFontScale(1.5f);
            ImGui.Text("SETTINGS");
            ImGui.SetWindowFontScale(1.0f);
            ImGui.Separator();
            ImGui.Spacing();

            // Resolution
            ImGui.Text("Resolution");
            if (ImGui.Button("1280x720"))
            {
                SettingsManager.SetResolution(1280, 720);
                SettingsManager.Apply(_graphics);
            }
            ImGui.SameLine();
            if (ImGui.Button("1920x1080"))
            {
                SettingsManager.SetResolution(1920, 1080);
                SettingsManager.Apply(_graphics);
            }

            ImGui.Spacing();

            // Fullscreen
            bool isFullscreen = SettingsManager.CurrentSettings.IsFullscreen;
            if (ImGui.Checkbox("Fullscreen", ref isFullscreen))
            {
                SettingsManager.SetFullscreen(isFullscreen);
                SettingsManager.Apply(_graphics);
            }

            ImGui.Spacing();

            // VSync
            bool isVsync = SettingsManager.CurrentSettings.IsVSync;
            if (ImGui.Checkbox("Vertical Sync", ref isVsync))
            {
                SettingsManager.SetVSync(isVsync);
                SettingsManager.Apply(_graphics);
            }

            ImGui.Spacing();

            // Volume
            float volume = SettingsManager.CurrentSettings.MasterVolume;
            if (ImGui.SliderFloat("Master Volume", ref volume, 0f, 1f))
            {
                SettingsManager.SetVolume(volume);
            }

            ImGui.Spacing();
            ImGui.Separator();
            ImGui.Spacing();

            // Save & Back
            if (ImGui.Button("SAVE & BACK", new System.Numerics.Vector2(380, 40)))
            {
                SettingsManager.Save();
                currentState = GameState.MainMenu;
            }
            ImGui.End();
        }
        ImGui.PopStyleColor();
        ImGui.PopStyleVar();
    }

    private void DrawCreditsMenu()
    {
        ImGui.PushStyleVar(ImGuiStyleVar.WindowRounding, 10f);
        ImGui.PushStyleColor(ImGuiCol.WindowBg, new System.Numerics.Vector4(0, 0, 0, 0.8f));

        ImGui.SetNextWindowPos(new System.Numerics.Vector2(_graphics.PreferredBackBufferWidth / 2 - 150, _graphics.PreferredBackBufferHeight / 2 - 100));
        ImGui.SetNextWindowSize(new System.Numerics.Vector2(300, 200));

        if (ImGui.Begin("Credits", ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoMove | ImGuiWindowFlags.NoTitleBar))
        {
            ImGui.SetWindowFontScale(1.5f);
            ImGui.Text("CREDITS");
            ImGui.SetWindowFontScale(1.0f);
            ImGui.Separator();
            ImGui.Text("Programming: Zorglub");
            ImGui.Text("Engine: MonoGame");
            ImGui.Text("UI: ImGui.NET");

            ImGui.Spacing();
            ImGui.Separator();
            ImGui.Spacing();

            if (ImGui.Button("BACK", new System.Numerics.Vector2(280, 40)))
            {
                currentState = GameState.MainMenu;
            }
            ImGui.End();
        }
        ImGui.PopStyleColor();
        ImGui.PopStyleVar();
    }

    private void RefreshLevelList()
    {
        levelFiles.Clear();
        string levelsDir = "Levels";
        if (System.IO.Directory.Exists(levelsDir))
        {
            string[] files = System.IO.Directory.GetFiles(levelsDir, "*.json");
            foreach (string file in files)
            {
                levelFiles.Add(System.IO.Path.GetFileName(file));
            }
        }
    }

    private void DrawLevelSelectMenu()
    {
        ImGui.PushStyleVar(ImGuiStyleVar.WindowRounding, 10f);
        ImGui.PushStyleColor(ImGuiCol.WindowBg, new System.Numerics.Vector4(0, 0, 0, 0.8f));

        ImGui.SetNextWindowPos(new System.Numerics.Vector2(_graphics.PreferredBackBufferWidth / 2 - 200, _graphics.PreferredBackBufferHeight / 2 - 200));
        ImGui.SetNextWindowSize(new System.Numerics.Vector2(400, 400));

        if (ImGui.Begin("Select Level", ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoMove | ImGuiWindowFlags.NoTitleBar))
        {
            ImGui.SetWindowFontScale(1.5f);
            ImGui.Text("SELECT LEVEL");
            ImGui.SetWindowFontScale(1.0f);
            ImGui.Separator();
            ImGui.Spacing();

            if (levelFiles.Count == 0)
            {
                ImGui.Text("No levels found in Levels/ directory.");
            }
            else
            {
                foreach (string levelFile in levelFiles)
                {
                    if (ImGui.Button(levelFile, new System.Numerics.Vector2(380, 30)))
                    {
                        LoadLevel(levelFile);
                        currentState = GameState.Playing;
                        IsMouseVisible = false; // Hide mouse during gameplay
                    }
                }
            }

            ImGui.Spacing();
            ImGui.Separator();
            ImGui.Spacing();

            if (ImGui.Button("BACK", new System.Numerics.Vector2(380, 40)))
            {
                currentState = GameState.MainMenu;
            }
            ImGui.End();
        }
        ImGui.PopStyleColor();
        ImGui.PopStyleVar();
    }
}
