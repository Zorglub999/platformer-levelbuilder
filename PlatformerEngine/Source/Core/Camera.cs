using Microsoft.Xna.Framework;
using System;

namespace PlatformerEngine.Source.Core
{
    /// <summary>
    /// Camera with room-based clamping logic (Celeste style) and screen shake
    /// </summary>
    public class Camera
    {
        public Vector2 Position { get; set; }
        public float Zoom { get; set; }
        public float SmoothSpeed { get; set; }

        private Vector2 targetPosition;

        // Screen shake
        private float shakeTimer;
        private float shakeIntensity;
        private Vector2 shakeOffset;
        private static Random random = new Random();

        public Camera()
        {
            Position = Vector2.Zero;
            Zoom = 1.0f;
            SmoothSpeed = 0.1f; // Smooth follow speed (0-1, lower = more lag)
        }

        /// <summary>
        /// Trigger a screen shake effect
        /// </summary>
        public void Shake(float intensity, float duration = 0.2f)
        {
            shakeIntensity = intensity;
            shakeTimer = duration;
        }

        /// <summary>
        /// Update camera position to center on the current room based on focus position
        /// </summary>
        public void Update(Vector2 focusPosition, int roomWidth, int roomHeight)
        {
            // Calculate which room the focus (player) is in
            float roomX = (float)Math.Floor(focusPosition.X / roomWidth) * roomWidth;
            float roomY = (float)Math.Floor(focusPosition.Y / roomHeight) * roomHeight;

            // Target position should be the center of that room
            targetPosition = new Vector2(roomX + roomWidth * 0.5f, roomY + roomHeight * 0.5f);

            // Lerp towards the target room center
            Position = Vector2.Lerp(Position, targetPosition, SmoothSpeed);

            // Handle Shake
            if (shakeTimer > 0)
            {
                shakeTimer -= 0.016f; // approx 60fps
                if (shakeTimer <= 0)
                {
                    shakeTimer = 0;
                    shakeOffset = Vector2.Zero;
                }
                else
                {
                    // Random offset based on intensity
                    float x = (float)(random.NextDouble() * 2 - 1) * shakeIntensity;
                    float y = (float)(random.NextDouble() * 2 - 1) * shakeIntensity;
                    shakeOffset = new Vector2(x, y);
                }
            }
        }

        /// <summary>
        /// Get the view transformation matrix for SpriteBatch
        /// </summary>
        public Matrix GetViewMatrix(int screenWidth, int screenHeight)
        {
            // Add shake offset to position
            Vector2 shakenPosition = Position + shakeOffset;

            return Matrix.CreateTranslation(new Vector3(-shakenPosition.X, -shakenPosition.Y, 0)) *
                   Matrix.CreateScale(Zoom) *
                   Matrix.CreateTranslation(new Vector3(screenWidth * 0.5f, screenHeight * 0.5f, 0));
        }

        /// <summary>
        /// Convert screen coordinates to world coordinates
        /// </summary>
        public Vector2 ScreenToWorld(Vector2 screenPos, int screenWidth, int screenHeight)
        {
            // Reverse the camera transformation
            Vector2 centered = screenPos - new Vector2(screenWidth * 0.5f, screenHeight * 0.5f);
            Vector2 unzoomed = centered / Zoom;
            Vector2 worldPos = unzoomed + Position;
            return worldPos;
        }
    }
}
