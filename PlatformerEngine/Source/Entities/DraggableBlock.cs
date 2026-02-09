using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using PlatformerEngine.Source.Core;

namespace PlatformerEngine.Source.Entities
{
    /// <summary>
    /// Interactive block that can be dragged with the mouse
    /// </summary>
    public class DraggableBlock
    {
        public enum BlockState
        {
            Idle,
            Dragging,
            WaitBeforeReturn,
            Returning
        }

        public Vector2 Position { get; set; }
        public Vector2 OriginPosition { get; private set; }
        public Vector2 Size { get; set; }
        public BlockState CurrentState { get; private set; }

        private float returnTimer;
        private float waitTimer;
        private const float ReturnDuration = 0.5f;
        private const float WaitDuration = 0.5f;
        private Vector2 returnStartPosition;
        private Texture2D pixelTexture;
        private bool wasMousePressed;

        public DraggableBlock(Vector2 position, Vector2 size, Texture2D pixelTexture)
        {
            Position = position;
            OriginPosition = position;
            Size = size;
            this.pixelTexture = pixelTexture;
            CurrentState = BlockState.Idle;
            wasMousePressed = false;
        }

        /// <summary>
        /// Check if the block is solid (only in Idle and WaitBeforeReturn states)
        /// </summary>
        public bool IsSolid()
        {
            return CurrentState == BlockState.Idle || CurrentState == BlockState.WaitBeforeReturn;
        }

        /// <summary>
        /// Get the bounding rectangle for collision detection
        /// </summary>
        public Rectangle GetBounds()
        {
            return new Rectangle(
                (int)Position.X,
                (int)Position.Y,
                (int)Size.X,
                (int)Size.Y
            );
        }

        /// <summary>
        /// Check if a world position is inside the block
        /// </summary>
        public bool IsPointInside(Vector2 worldPos)
        {
            return worldPos.X >= Position.X &&
                   worldPos.X <= Position.X + Size.X &&
                   worldPos.Y >= Position.Y &&
                   worldPos.Y <= Position.Y + Size.Y;
        }

        /// <summary>
        /// Update block state and position
        /// </summary>
        /// <summary>
        /// Update block state and position
        /// </summary>
        public void Update(Camera camera, int screenWidth, int screenHeight)
        {
            // Use generic cursor position from InputManager
            // Note: Camera ScreenToWorld might need the raw screen pos or InputManager.CursorPosition is already world?
            // InputManager.CursorPosition is Screen Coordinates (Mouse/Virtual).
            // We need to convert it to World Coordinates.

            Vector2 cursorWorldPos = camera.ScreenToWorld(
                InputManager.CursorPosition,
                screenWidth,
                screenHeight
            );

            bool isInteractPressed = InputManager.InteractPressed;

            switch (CurrentState)
            {
                case BlockState.Idle:
                    // Check if clicked/interacted with this block
                    if (isInteractPressed && !wasMousePressed && IsPointInside(cursorWorldPos))
                    {
                        CurrentState = BlockState.Dragging;
                    }
                    break;

                case BlockState.Dragging:
                    // Follow cursor
                    Position = cursorWorldPos - Size * 0.5f; // Center on cursor

                    // Check if released
                    if (!isInteractPressed)
                    {
                        CurrentState = BlockState.WaitBeforeReturn;
                        waitTimer = 0f;
                    }
                    break;

                case BlockState.WaitBeforeReturn:
                    // Wait for 0.5 seconds before returning
                    waitTimer += 0.016f; // Approximately 1/60th of a second

                    if (waitTimer >= WaitDuration)
                    {
                        CurrentState = BlockState.Returning;
                        returnTimer = 0f;
                        returnStartPosition = Position;
                    }
                    break;

                case BlockState.Returning:
                    // Lerp back to origin
                    returnTimer += 0.016f; // Approximately 1/60th of a second
                    float t = MathHelper.Clamp(returnTimer / ReturnDuration, 0f, 1f);

                    // Smooth easing
                    t = t * t * (3f - 2f * t); // Smoothstep

                    Position = Vector2.Lerp(returnStartPosition, OriginPosition, t);

                    // Return to Idle when done
                    if (returnTimer >= ReturnDuration)
                    {
                        Position = OriginPosition;
                        CurrentState = BlockState.Idle;
                    }
                    break;
            }

            wasMousePressed = isInteractPressed;
        }

        /// <summary>
        /// Draw the block with appropriate color based on state
        /// </summary>
        public void Draw(SpriteBatch spriteBatch)
        {
            Color blockColor;

            switch (CurrentState)
            {
                case BlockState.Idle:
                    blockColor = new Color(100, 200, 100); // Green
                    break;
                case BlockState.Dragging:
                    blockColor = new Color(255, 255, 100, 180); // Yellow, semi-transparent
                    break;
                case BlockState.WaitBeforeReturn:
                    blockColor = new Color(255, 180, 100, 200); // Orange, waiting
                    break;
                case BlockState.Returning:
                    blockColor = new Color(150, 220, 150); // Light green
                    break;
                default:
                    blockColor = Color.White;
                    break;
            }

            Rectangle destRect = new Rectangle(
                (int)Position.X,
                (int)Position.Y,
                (int)Size.X,
                (int)Size.Y
            );

            spriteBatch.Draw(pixelTexture, destRect, blockColor);
        }
    }
}
