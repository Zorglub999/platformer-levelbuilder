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
    Credits
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

    // Editor systems
    private ImGuiRenderer imguiRenderer;
    private LevelEditor levelEditor;
    private bool isEditorMode = false;

    // Game State
    private GameState currentState = GameState.MainMenu;

    // Input state tracking
    private KeyboardState previousKeyboardState;

    // UI Animation State
    private float menuTime = 0f;

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
        Window.Title = "Platformer Engine";
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

        // Initialize editor systems
        imguiRenderer = new ImGuiRenderer(this);
        levelEditor = new LevelEditor(
            tileMap,
            camera,
            imguiRenderer,
            _graphics.PreferredBackBufferWidth,
            _graphics.PreferredBackBufferHeight,
            draggableBlocks,
            pixelTexture
        );

        previousKeyboardState = Keyboard.GetState();
    }

    private void LoadLevel(string filename)
    {
        // Use Levels folder
        string path = System.IO.Path.Combine("Levels", filename);
        // Pass draggableBlocks and pixelTexture to Load
        tileMap.Load(path, draggableBlocks, pixelTexture);
        // No need to clear draggableBlocks here as Load now handles it (or clears it if new list is passed)
        // actually Load clears it internally if we pass the list.
        // Reset player if spawn point not found
        // Reset player if spawn point not found
        player.Position = new Vector2(100, 100);

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
        }

        // Animated Menu Background Update
        if (currentState != GameState.Playing)
        {
            menuTime += (float)gameTime.ElapsedGameTime.TotalSeconds;
            // Spawn some background particles
            if (Random.NextDouble() < 0.1)
            {
                Vector2 pos = new Vector2(Random.Next(_graphics.PreferredBackBufferWidth), _graphics.PreferredBackBufferHeight + 10);
                particleSystem.Spawn(pos, 1, new Vector2(0, -2), 0.5f); // Floating up
            }
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

                // Ensure camera doesn't drift efficiently? iterating clamping might be good but free cam is free cam.
                // We do NOT call camera.Update(player) here.
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

        if (currentState != GameState.Playing)
        {
            // Draw menu background particles
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

            // Draw tilemap with culling
            tileMap.Draw(_spriteBatch, camera, _graphics.PreferredBackBufferWidth, _graphics.PreferredBackBufferHeight);

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

            _spriteBatch.End();
        }

        // Draw UI (ImGui)
        imguiRenderer.BeginLayout(gameTime);

        if (currentState == GameState.MainMenu)
        {
            DrawMainMenu();
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

        imguiRenderer.EndLayout();

        base.Draw(gameTime);
    }

    private void DrawMainMenu()
    {
        // Styling
        ImGui.PushStyleVar(ImGuiStyleVar.WindowRounding, 10f);
        ImGui.PushStyleVar(ImGuiStyleVar.FrameRounding, 5f);
        ImGui.PushStyleColor(ImGuiCol.WindowBg, new System.Numerics.Vector4(0, 0, 0, 0.7f));
        ImGui.PushStyleColor(ImGuiCol.Button, new System.Numerics.Vector4(0.2f, 0.6f, 1.0f, 0.8f));
        ImGui.PushStyleColor(ImGuiCol.ButtonHovered, new System.Numerics.Vector4(0.3f, 0.7f, 1.0f, 1.0f));
        ImGui.PushStyleColor(ImGuiCol.Text, new System.Numerics.Vector4(1, 1, 1, 1));

        // Animate Window Position
        float offsetY = (float)System.Math.Sin(menuTime * 2) * 5f;

        ImGui.SetNextWindowPos(new System.Numerics.Vector2(_graphics.PreferredBackBufferWidth / 2 - 125, _graphics.PreferredBackBufferHeight / 2 - 150 + offsetY));
        ImGui.SetNextWindowSize(new System.Numerics.Vector2(250, 350));

        if (ImGui.Begin("Main Menu", ImGuiWindowFlags.NoTitleBar | ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoMove))
        {
            // Title
            ImGui.SetWindowFontScale(2.0f);
            float textWidth = ImGui.CalcTextSize("PLATFORMER").X;
            ImGui.SetCursorPosX((250 - textWidth) / 2);
            ImGui.Text("PLATFORMER");
            ImGui.SetWindowFontScale(1.0f);

            ImGui.Separator();
            ImGui.Spacing();
            ImGui.Spacing();

            // Play Button
            if (ImGui.Button("PLAY", new System.Numerics.Vector2(230, 50)))
            {
                LoadLevel("level1.json");
                currentState = GameState.Playing;
                IsMouseVisible = false; // Hide mouse during gameplay
            }

            ImGui.Spacing();

            // Settings Button
            if (ImGui.Button("SETTINGS", new System.Numerics.Vector2(230, 50)))
            {
                currentState = GameState.Settings;
            }

            ImGui.Spacing();

            // Credits Button
            if (ImGui.Button("CREDITS", new System.Numerics.Vector2(230, 50)))
            {
                currentState = GameState.Credits;
            }

            ImGui.Spacing();
            ImGui.Separator();
            ImGui.Spacing();

            // Exit Button
            ImGui.PushStyleColor(ImGuiCol.Button, new System.Numerics.Vector4(0.8f, 0.2f, 0.2f, 0.8f));
            ImGui.PushStyleColor(ImGuiCol.ButtonHovered, new System.Numerics.Vector4(1.0f, 0.3f, 0.3f, 1.0f));
            if (ImGui.Button("EXIT", new System.Numerics.Vector2(230, 40)))
            {
                Exit();
            }
            ImGui.PopStyleColor(2);

            ImGui.End();
        }

        ImGui.PopStyleColor(4);
        ImGui.PopStyleVar(2);
    }

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
}
