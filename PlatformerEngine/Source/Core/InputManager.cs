using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace PlatformerEngine.Source.Core
{
    public static class InputManager
    {
        private static KeyboardState _currentKeyboardState;
        private static KeyboardState _previousKeyboardState;
        private static GamePadState _currentGamePadState;
        private static GamePadState _previousGamePadState;
        private static MouseState _currentMouseState;
        private static MouseState _previousMouseState;

        private static Vector2 _virtualCursorPosition;
        private const float CursorSpeed = 15f; // Pixels per frame

        public static Vector2 Movement { get; private set; }
        public static bool JumpPressed { get; private set; }
        public static bool JumpHeld { get; private set; }
        public static bool InteractPressed { get; private set; }
        public static bool DashPressed { get; private set; }
        public static Vector2 CursorPosition { get; private set; }

        public static void Initialize()
        {
            _virtualCursorPosition = new Vector2(640, 360); // Start center (assuming 1280x720 for now)
        }

        public static void Update()
        {
            _previousKeyboardState = _currentKeyboardState;
            _currentKeyboardState = Keyboard.GetState();

            _previousGamePadState = _currentGamePadState;
            _currentGamePadState = GamePad.GetState(PlayerIndex.One);

            _previousMouseState = _currentMouseState;
            _currentMouseState = Mouse.GetState();

            UpdateMovement();
            UpdateJump();
            UpdateInteract();
            UpdateDash();
            UpdateCursor();
        }

        public static bool IsKeyPressed(Keys key)
        {
            return _currentKeyboardState.IsKeyDown(key) && !_previousKeyboardState.IsKeyDown(key);
        }

        private static void UpdateMovement()
        {
            Vector2 move = Vector2.Zero;

            // Keyboard: Q/D (AZERTY) or A/D (QWERTY) + Arrows
            if (_currentKeyboardState.IsKeyDown(Keys.Left) || _currentKeyboardState.IsKeyDown(Keys.Q) || _currentKeyboardState.IsKeyDown(Keys.A))
            {
                move.X = -1;
            }
            else if (_currentKeyboardState.IsKeyDown(Keys.Right) || _currentKeyboardState.IsKeyDown(Keys.D))
            {
                move.X = 1;
            }

            // Gamepad: D-Pad or Left Stick
            if (_currentGamePadState.IsConnected)
            {
                if (_currentGamePadState.DPad.Left == ButtonState.Pressed || _currentGamePadState.ThumbSticks.Left.X < -0.5f)
                {
                    move.X = -1;
                }
                else if (_currentGamePadState.DPad.Right == ButtonState.Pressed || _currentGamePadState.ThumbSticks.Left.X > 0.5f)
                {
                    move.X = 1;
                }
            }

            Movement = move;
        }

        private static void UpdateJump()
        {
            // Jump Pressed (Initial frame)
            bool kbdJump = (_currentKeyboardState.IsKeyDown(Keys.Space) && !_previousKeyboardState.IsKeyDown(Keys.Space)) ||
                           (_currentKeyboardState.IsKeyDown(Keys.Z) && !_previousKeyboardState.IsKeyDown(Keys.Z));
            bool gpJump = _currentGamePadState.IsConnected && (_currentGamePadState.Buttons.A == ButtonState.Pressed && _previousGamePadState.Buttons.A == ButtonState.Released);

            JumpPressed = kbdJump || gpJump;

            // Jump Held (Continuous)
            kbdJump = _currentKeyboardState.IsKeyDown(Keys.Space) || _currentKeyboardState.IsKeyDown(Keys.Z);
            gpJump = _currentGamePadState.IsConnected && _currentGamePadState.Buttons.A == ButtonState.Pressed;

            JumpHeld = kbdJump || gpJump;
        }

        private static void UpdateInteract()
        {
            // Mouse Click
            bool mouseClick = _currentMouseState.LeftButton == ButtonState.Pressed;

            // Gamepad X Button or Right Trigger
            bool gpInteract = _currentGamePadState.IsConnected && (_currentGamePadState.Buttons.X == ButtonState.Pressed || _currentGamePadState.Triggers.Right > 0.5f);

            InteractPressed = mouseClick || gpInteract;
        }

        private static void UpdateDash()
        {
            // Dash Input
            bool kbdDash = _currentKeyboardState.IsKeyDown(Keys.LeftShift) && !_previousKeyboardState.IsKeyDown(Keys.LeftShift);
            bool gpDash = _currentGamePadState.IsConnected && (_currentGamePadState.Buttons.B == ButtonState.Pressed && _previousGamePadState.Buttons.B == ButtonState.Released);

            DashPressed = kbdDash || gpDash;
        }

        private static void UpdateCursor()
        {
            // If mouse moved, use mouse position
            if (_currentMouseState.Position != _previousMouseState.Position)
            {
                CursorPosition = new Vector2(_currentMouseState.X, _currentMouseState.Y);
                // Sync virtual cursor to mouse so switching back to gamepad feels natural
                _virtualCursorPosition = CursorPosition;
            }
            // If gamepad right stick moved, use virtual cursor
            else if (_currentGamePadState.IsConnected)
            {
                Vector2 stickInput = _currentGamePadState.ThumbSticks.Right;
                // Invert Y because GamePad Y is Up (+), Screen Y is Down (+) usually... wait, MonoGame stick Y is Up(+), Screen Y is Down(+)
                // Actually MonoGame ThumbSticks.Right.Y is +1 Up, -1 Down.
                // Screen coordinates: Y increases Down. So we need to subtract Y input to move Up.

                if (stickInput.LengthSquared() > 0.1f)
                {
                    stickInput.Y *= -1; // Invert Y for screen coordinates
                    _virtualCursorPosition += stickInput * CursorSpeed;

                    // Clamp to screen? Optional, but good practice. Assuming static resolution for now, can be improved.
                    // For now, let it be free.

                    CursorPosition = _virtualCursorPosition;
                }
            }
        }
    }
}
