using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System.Collections.Generic;

namespace PlatformerEngine.Source.Core
{
    /// <summary>
    /// Simple particle system for visual effects (jump, landing, etc.)
    /// </summary>
    public class ParticleSystem
    {
        private class Particle
        {
            public Vector2 Position;
            public Vector2 Velocity;
            public Color Color;
            public float Lifetime;
            public float MaxLifetime;
            public float Size;
        }

        private List<Particle> particles;
        private const float Gravity = 0.3f;

        public ParticleSystem()
        {
            particles = new List<Particle>();
        }

        /// <summary>
        /// Spawn particles at a position with random velocities
        /// </summary>
        public void Spawn(Vector2 position, int count, Vector2 velocityDirection, float spread = 1.0f)
        {
            for (int i = 0; i < count; i++)
            {
                float angle = (float)(Game1.Random.NextDouble() * MathHelper.TwoPi);
                float speed = (float)(Game1.Random.NextDouble() * 3 + 1) * spread;
                
                Vector2 velocity = new Vector2(
                    (float)System.Math.Cos(angle) * speed,
                    (float)System.Math.Sin(angle) * speed
                ) + velocityDirection;

                particles.Add(new Particle
                {
                    Position = position,
                    Velocity = velocity,
                    Color = Color.White,
                    Lifetime = 1.0f,
                    MaxLifetime = 1.0f,
                    Size = (float)(Game1.Random.NextDouble() * 3 + 2)
                });
            }
        }

        /// <summary>
        /// Update all particles
        /// </summary>
        public void Update()
        {
            for (int i = particles.Count - 1; i >= 0; i--)
            {
                Particle p = particles[i];

                // Update position
                p.Position += p.Velocity;
                
                // Apply gravity
                p.Velocity.Y += Gravity;
                
                // Decrease lifetime
                p.Lifetime -= 0.016f; // Approximately 1/60th of a second

                // Remove dead particles
                if (p.Lifetime <= 0)
                {
                    particles.RemoveAt(i);
                }
            }
        }

        /// <summary>
        /// Draw all particles
        /// </summary>
        public void Draw(SpriteBatch spriteBatch, Texture2D pixelTexture)
        {
            foreach (Particle p in particles)
            {
                float alpha = p.Lifetime / p.MaxLifetime;
                float scale = p.Size * alpha;
                
                Color color = p.Color * alpha;
                
                Rectangle destRect = new Rectangle(
                    (int)(p.Position.X - scale / 2),
                    (int)(p.Position.Y - scale / 2),
                    (int)scale,
                    (int)scale
                );
                
                spriteBatch.Draw(pixelTexture, destRect, color);
            }
        }
    }
}
