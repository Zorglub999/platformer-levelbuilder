using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using System;
using System.Collections.Generic;
using System.IO;

namespace PlatformerEngine.Source.UI
{
    public class MenuSystem
    {
        private Game1 game;
        private GraphicsDevice graphicsDevice;
        private SpriteBatch spriteBatch;

        // Textures
        private Texture2D backgroundTexture;
        private Texture2D titleTexture;
        private Texture2D pixelTexture;

        // Buttons
        private class MenuButton
        {
            public string Text;
            public Rectangle Bounds;
            public Action OnClick;
            public bool IsHovered;
            public Color HoverColor = Color.CornflowerBlue; // Fallback color
            public Color NormalColor = Color.DarkSlateBlue; // Fallback color
        }
        private List<MenuButton> buttons = new List<MenuButton>();

        // Snow System
        private struct SnowParticle
        {
            public Vector2 Position;
            public float Speed;
            public float Size;
            public float Alpha;
        }
        private List<SnowParticle> snowParticles = new List<SnowParticle>();
        private Random random = new Random();

        public MenuSystem(Game1 game)
        {
            this.game = game;
            this.graphicsDevice = game.GraphicsDevice;
        }

        public void LoadContent()
        {
            spriteBatch = new SpriteBatch(graphicsDevice);

            // Create 1x1 texture for fallbacks/drawing shapes
            pixelTexture = new Texture2D(graphicsDevice, 1, 1);
            pixelTexture.SetData(new[] { Color.White });

            // Try loading assets
            backgroundTexture = LoadTexture("Content/UI/menu_background.png");
            titleTexture = LoadTexture("Content/UI/game_title.png");

            // Initialize Snow
            for (int i = 0; i < 200; i++)
            {
                snowParticles.Add(CreateSnowParticle(true));
            }

            // Setup Buttons
            SetupButtons();
        }

        private Texture2D LoadTexture(string path)
        {
            if (File.Exists(path))
            {
                try
                {
                    using (FileStream stream = File.OpenRead(path))
                    {
                        return Texture2D.FromStream(graphicsDevice, stream);
                    }
                }
                catch (Exception e)
                {
                    Console.WriteLine($"Failed to load texture {path}: {e.Message}");
                }
            }
            return null;
        }

        private void SetupButtons()
        {
            int centerX = graphicsDevice.PresentationParameters.BackBufferWidth / 2;
            int startY = 400; // Below title
            int buttonWidth = 200;
            int buttonHeight = 50;
            int spacing = 20;

            AddButton("PLAY", new Rectangle(centerX - buttonWidth / 2, startY, buttonWidth, buttonHeight), () =>
            {
                game.StartGame();
            });

            AddButton("SETTINGS", new Rectangle(centerX - buttonWidth / 2, startY + (buttonHeight + spacing), buttonWidth, buttonHeight), () =>
            {
                game.OpenSettings();
            });

            AddButton("CREDITS", new Rectangle(centerX - buttonWidth / 2, startY + (buttonHeight + spacing) * 2, buttonWidth, buttonHeight), () =>
            {
                game.OpenCredits();
            });

            AddButton("EXIT", new Rectangle(centerX - buttonWidth / 2, startY + (buttonHeight + spacing) * 3, buttonWidth, buttonHeight), () =>
            {
                game.Exit();
            });
        }

        private void AddButton(string text, Rectangle bounds, Action onClick)
        {
            buttons.Add(new MenuButton { Text = text, Bounds = bounds, OnClick = onClick });
        }

        private SnowParticle CreateSnowParticle(bool randomY = false)
        {
            float x = (float)random.NextDouble() * graphicsDevice.PresentationParameters.BackBufferWidth;
            float y = randomY ? (float)random.NextDouble() * graphicsDevice.PresentationParameters.BackBufferHeight : -10;

            return new SnowParticle
            {
                Position = new Vector2(x, y),
                Speed = (float)random.NextDouble() * 2 + 1,
                Size = (float)random.NextDouble() * 2 + 1,
                Alpha = (float)random.NextDouble() * 0.5f + 0.5f
            };
        }

        public void Update(GameTime gameTime)
        {
            // Update Snow
            for (int i = 0; i < snowParticles.Count; i++)
            {
                var p = snowParticles[i];
                p.Position.Y += p.Speed;
                p.Position.X += (float)Math.Sin(gameTime.TotalGameTime.TotalSeconds + p.Position.Y * 0.01f) * 0.5f; // Slight sway

                if (p.Position.Y > graphicsDevice.PresentationParameters.BackBufferHeight)
                {
                    snowParticles[i] = CreateSnowParticle(); // Respawn at top
                }
                else
                {
                    snowParticles[i] = p;
                }
            }

            // Update Buttons
            MouseState mouse = Mouse.GetState();
            Point mousePoint = mouse.Position;
            bool mouseClicked = mouse.LeftButton == ButtonState.Pressed; // Simple click check, ideally check press/release

            // Debounce click simply by checking if it was just pressed? 
            // InputManager checks for "Pressed" this frame. Let's use InputManager if possible, 
            // but for now simple mouse state is fine for menu.

            // To prevent continuous clicking, we might need a "wasPressed" state or use InputManager.
            // Let's assume InputManager is updated in Game1.Update() before this.

            foreach (var button in buttons)
            {
                button.IsHovered = button.Bounds.Contains(mousePoint);
                if (button.IsHovered && mouse.LeftButton == ButtonState.Pressed)
                {
                    // Ideally we trigger on release or use InputManager specific "Clicked" logic.
                    // For now, let's use a simple approach: if InputManager.InteractPressed is true?
                    // But we don't have access to InputManager here directly unless passed or static.
                    // InputManager is static public.
                }
            }

            // Using InputManager for clicks to be consistent
            if (Source.Core.InputManager.InteractPressed)
            {
                foreach (var button in buttons)
                {
                    if (button.IsHovered)
                    {
                        button.OnClick?.Invoke();
                    }
                }
            }
        }

        public void Draw(GameTime gameTime)
        {
            spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp);

            // Draw Background
            if (backgroundTexture != null)
            {
                spriteBatch.Draw(backgroundTexture, new Rectangle(0, 0, graphicsDevice.PresentationParameters.BackBufferWidth, graphicsDevice.PresentationParameters.BackBufferHeight), Color.White);
            }
            else
            {
                graphicsDevice.Clear(new Color(10, 10, 25)); // Deep blue fallback
            }

            // Draw Snow (Behind title/buttons? Or in front? Celeste snow is usually foreground and back)
            // Let's draw some faint snow behind
            foreach (var p in snowParticles)
            {
                spriteBatch.Draw(pixelTexture, new Rectangle((int)p.Position.X, (int)p.Position.Y, (int)p.Size, (int)p.Size), Color.White * p.Alpha);
            }

            // Draw Title
            if (titleTexture != null)
            {
                // Center title
                Vector2 titlePos = new Vector2(
                    (graphicsDevice.PresentationParameters.BackBufferWidth - titleTexture.Width) / 2,
                    100
                );
                spriteBatch.Draw(titleTexture, titlePos, Color.White);
            }
            else
            {
                // Fallback text rendering if we had a font... we don't.
                // Draw a placeholder rectangle for title
                /*
                Rectangle titleRect = new Rectangle(
                    graphicsDevice.PresentationParameters.BackBufferWidth / 2 - 200,
                    100, 400, 100);
                spriteBatch.Draw(pixelTexture, titleRect, Color.Transparent); // Invisible if no texture
                */
            }

            // Draw Buttons
            foreach (var button in buttons)
            {
                Color color = button.IsHovered ? button.HoverColor : button.NormalColor;

                // Draw button background
                spriteBatch.Draw(pixelTexture, button.Bounds, color);

                // Draw border
                int borderWidth = 2;
                Color borderColor = Color.White;
                // Top
                spriteBatch.Draw(pixelTexture, new Rectangle(button.Bounds.X, button.Bounds.Y, button.Bounds.Width, borderWidth), borderColor);
                // Bottom
                spriteBatch.Draw(pixelTexture, new Rectangle(button.Bounds.X, button.Bounds.Bottom - borderWidth, button.Bounds.Width, borderWidth), borderColor);
                // Left
                spriteBatch.Draw(pixelTexture, new Rectangle(button.Bounds.X, button.Bounds.Y, borderWidth, button.Bounds.Height), borderColor);
                // Right
                spriteBatch.Draw(pixelTexture, new Rectangle(button.Bounds.Right - borderWidth, button.Bounds.Y, borderWidth, button.Bounds.Height), borderColor);

                // Draw Text? We don't have SpriteFont loaded in Game1. 
                // We rely on ImGui for text usually. 
                // We could use ImGui here just for text on top of our buttons? 
                // Or we can rely on generated button images which we don't have yet.
                // Since this is a "vibe" menu, mixing ImGui text on top might look okay-ish for now.
            }

            spriteBatch.End();

            // Draw handling is split: 
            // - Draw() handles SpriteBatch (Background, Snow, Buttons)
            // - DrawUI() handles ImGui text overlays
        }

        public void DrawUI()
        {
            // Draw button labels using ImGui explicitly positioned?
            // Or just rely on the user knowing which button is which? 
            // No, that's bad.
            // Let's use ImGui to draw text centered on our button rects.

            ImGuiNET.ImGui.SetNextWindowPos(new System.Numerics.Vector2(0, 0));
            ImGuiNET.ImGui.SetNextWindowSize(new System.Numerics.Vector2(graphicsDevice.PresentationParameters.BackBufferWidth, graphicsDevice.PresentationParameters.BackBufferHeight));
            ImGuiNET.ImGui.Begin("MenuOverlay", ImGuiNET.ImGuiWindowFlags.NoTitleBar | ImGuiNET.ImGuiWindowFlags.NoBackground | ImGuiNET.ImGuiWindowFlags.NoResize | ImGuiNET.ImGuiWindowFlags.NoInputs); // No inputs because we handle clicks ourselves

            foreach (var button in buttons)
            {
                // Centered text
                string text = button.Text;
                System.Numerics.Vector2 textSize = ImGuiNET.ImGui.CalcTextSize(text);
                System.Numerics.Vector2 pos = new System.Numerics.Vector2(
                    button.Bounds.Center.X - textSize.X / 2,
                    button.Bounds.Center.Y - textSize.Y / 2
                );

                ImGuiNET.ImGui.SetCursorPos(pos);
                ImGuiNET.ImGui.Text(text);
            }

            // Draw Title Text if texture missing
            if (titleTexture == null)
            {
                string title = "CELESTE EN NUL";
                ImGuiNET.ImGui.SetWindowFontScale(3.0f);
                System.Numerics.Vector2 titleSize = ImGuiNET.ImGui.CalcTextSize(title);
                ImGuiNET.ImGui.SetCursorPos(new System.Numerics.Vector2(
                    graphicsDevice.PresentationParameters.BackBufferWidth / 2 - titleSize.X / 2,
                    150
                ));
                ImGuiNET.ImGui.Text(title);
                ImGuiNET.ImGui.SetWindowFontScale(1.0f);
            }

            ImGuiNET.ImGui.End();
        }
    }
}
