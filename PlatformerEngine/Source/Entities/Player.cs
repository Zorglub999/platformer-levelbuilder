using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using System.Collections.Generic;
using PlatformerEngine.Source.Levels;
using PlatformerEngine.Source.Core;

namespace PlatformerEngine.Source.Entities
{
    public class Player
    {
        public enum PlayerState
        {
            Idle,
            Run,
            Jump,
            Fall,
            WallSlide,
            Dashing
        }

        private struct GhostTrail
        {
            public Vector2 Position;
            public float Alpha;
            public float Timer;
        }

        // Position and size
        public Vector2 Position { get; set; }
        public Vector2 Size { get; set; }
        private Vector2 spawnPosition;

        // Physics
        private Vector2 velocity;
        private const float Gravity = 0.6f;
        private const float MaxFallSpeed = 15f;
        private const float MoveSpeed = 5f;
        private const float JumpPower = 14f;
        private const float GroundFriction = 0.8f;
        private const float AirFriction = 0.95f;
        private const float WallSlideSpeed = 2f;
        private const float WallJumpForceX = 8f;
        private const float WallJumpForceY = 12f;
        private const float DashSpeed = 20f;
        private const float DashDuration = 0.15f; // Short burst

        // State
        public PlayerState CurrentState { get; private set; }
        private bool isGrounded;
        private bool wasGrounded;
        private int facingDirection = 1; // 1 = Right, -1 = Left
        private float wallJumpLockTimer = 0f;

        // Dash State
        private bool canDash = true;
        private float dashTimer = 0f;
        private Vector2 dashDirection;
        private List<GhostTrail> ghostTrails = new List<GhostTrail>();
        private float ghostTrailSpawnTimer = 0f;

        // References
        private TileMap tileMap;
        private Texture2D pixelTexture;
        private List<DraggableBlock> draggableBlocks;
        private ParticleSystem particleSystem;

        // Visual effects
        public Vector2 RenderScale { get; private set; }
        private const float SquishSpeed = 0.2f;

        public Player(Vector2 startPosition, TileMap tileMap, Texture2D pixelTexture, ParticleSystem particleSystem, List<DraggableBlock> draggableBlocks)
        {
            Position = startPosition;
            spawnPosition = startPosition;
            Size = new Vector2(30, 30);
            velocity = Vector2.Zero;
            this.tileMap = tileMap;
            this.pixelTexture = pixelTexture;
            this.particleSystem = particleSystem;
            this.draggableBlocks = draggableBlocks;
            RenderScale = Vector2.One;
            CurrentState = PlayerState.Idle;
        }

        public void Update(Camera camera)
        {
            // Debug Respawn
            if (InputManager.IsKeyPressed(Keys.P))
            {
                Respawn();
            }

            wasGrounded = isGrounded;

            // State Machine
            switch (CurrentState)
            {
                case PlayerState.Idle:
                case PlayerState.Run:
                case PlayerState.Jump:
                case PlayerState.Fall:
                    UpdateStandardMovement(camera);
                    break;
                case PlayerState.WallSlide:
                    UpdateWallSlide(camera);
                    break;
                case PlayerState.Dashing:
                    UpdateDash(camera);
                    break;
            }

            // Physics application
            if (CurrentState != PlayerState.Dashing)
            {
                ApplyPhysics();
            }

            // Movement & Collision
            MoveX();
            MoveY(camera);

            // Interaction with special tiles (Checkpoints, Kill Blocks)
            CheckSpecialTiles();

            // State Transitions for Animation/Logic
            UpdateStateTransitions();

            // Visuals
            UpdateVisuals();
        }

        private void UpdateStandardMovement(Camera camera)
        {
            // Horizontal Movement
            if (wallJumpLockTimer > 0)
            {
                wallJumpLockTimer -= 0.016f;
            }
            else
            {
                float inputX = InputManager.Movement.X;
                if (inputX != 0)
                {
                    velocity.X = inputX * MoveSpeed;
                    facingDirection = (int)inputX;
                }
                else
                {
                    velocity.X *= (isGrounded ? GroundFriction : AirFriction);
                    if (MathHelper.Distance(velocity.X, 0) < 0.1f) velocity.X = 0;
                }
            }

            // Jump
            if (InputManager.JumpPressed && isGrounded)
            {
                velocity.Y = -JumpPower;
                isGrounded = false;
                Squish(0.6f, 1.4f);
                SpawnParticles(new Vector2(0, 2), 0.8f);
            }

            // Variable Jump Height
            if (!InputManager.JumpHeld && velocity.Y < 0)
            {
                velocity.Y *= 0.5f;
            }

            // Start Dash
            if (InputManager.DashPressed && canDash)
            {
                StartDash(camera);
            }

            // Reset Dash on Ground
            if (isGrounded)
            {
                canDash = true;
            }
        }

        private void UpdateWallSlide(Camera camera)
        {
            // Wall Slide friction
            if (velocity.Y > WallSlideSpeed)
            {
                velocity.Y = WallSlideSpeed;
            }

            bool wallLeft = CheckWall(-1);
            bool wallRight = CheckWall(1);

            // Wall Jump
            if (InputManager.JumpPressed)
            {
                if (wallLeft)
                {
                    velocity.X = WallJumpForceX; // Jump Right
                    velocity.Y = -WallJumpForceY;
                    wallJumpLockTimer = 0.2f;
                    // directionAfterWallJump = 1;
                }
                else if (wallRight)
                {
                    velocity.X = -WallJumpForceX; // Jump Left
                    velocity.Y = -WallJumpForceY;
                    wallJumpLockTimer = 0.2f;
                    // directionAfterWallJump = -1;
                }

                CurrentState = PlayerState.Jump;
                Squish(0.6f, 1.4f);
                return;
            }

            // Push away from wall to fall off
            if ((wallLeft && InputManager.Movement.X > 0) || (wallRight && InputManager.Movement.X < 0))
            {
                // Small timer or just let standard movement take over in next frame?
                // Standard movement will apply force away, so we just transition to Fall in UpdateStateTransitions
            }

            // Dash from wall
            if (InputManager.DashPressed && canDash)
            {
                StartDash(camera);
            }
        }

        // Helper to know which way we faced during last jump for animation if needed
        // private int directionAfterWallJump = 0; // Unused for now

        private void StartDash(Camera camera)
        {
            CurrentState = PlayerState.Dashing;
            dashTimer = DashDuration;
            canDash = false; // Consume dash

            // Determine direction
            Vector2 inputDir = InputManager.Movement;

            // Normalize generic input if diagonal
            if (inputDir != Vector2.Zero)
            {
                inputDir.Normalize();
            }
            // If no input, dash forward
            else
            {
                inputDir = new Vector2(facingDirection, 0);
            }

            dashDirection = inputDir;
            velocity = dashDirection * DashSpeed;

            camera.Shake(3.0f, 0.2f); // Screen Shake on Dash
        }

        private void UpdateDash(Camera camera)
        {
            velocity = dashDirection * DashSpeed; // Lock velocity
            dashTimer -= 0.016f;

            // Spawn Ghost Trails
            ghostTrailSpawnTimer += 0.016f;
            if (ghostTrailSpawnTimer > 0.03f)
            {
                ghostTrails.Add(new GhostTrail { Position = Position, Alpha = 0.5f, Timer = 0.2f });
                ghostTrailSpawnTimer = 0;
            }

            if (dashTimer <= 0)
            {
                velocity = Vector2.Zero; // Stop or keep some momentum?
                // Keep percentage of momentum
                velocity = dashDirection * MoveSpeed;
                CurrentState = PlayerState.Fall;
            }
        }

        private void UpdateStateTransitions()
        {
            if (CurrentState == PlayerState.Dashing) return;

            // Grounded states
            if (isGrounded)
            {
                if (InputManager.Movement.X != 0) CurrentState = PlayerState.Run;
                else CurrentState = PlayerState.Idle;
            }
            else
            {
                // Air states
                if (velocity.Y < 0) CurrentState = PlayerState.Jump;
                else
                {
                    CurrentState = PlayerState.Fall;

                    // Check Wall Slide
                    bool wallLeft = CheckWall(-1);
                    bool wallRight = CheckWall(1);

                    if ((wallLeft || wallRight) && velocity.Y > 0)
                    {
                        CurrentState = PlayerState.WallSlide;
                        facingDirection = wallLeft ? -1 : 1;
                    }
                }
            }
        }

        private void ApplyPhysics()
        {
            velocity.Y += Gravity;
            if (velocity.Y > MaxFallSpeed) velocity.Y = MaxFallSpeed;
        }

        private bool CheckWall(int dirX)
        {
            // Check 1 pixel to the side
            float checkX = (dirX > 0) ? Position.X + Size.X + 1 : Position.X - 1;

            // Check top and bottom corners + middle to be sure
            Vector2 topCheck = new Vector2(checkX, Position.Y);
            Vector2 bottomCheck = new Vector2(checkX, Position.Y + Size.Y - 1);
            Vector2 midCheck = new Vector2(checkX, Position.Y + Size.Y * 0.5f);

            bool tileCollision = tileMap.IsSolid(topCheck) || tileMap.IsSolid(bottomCheck) || tileMap.IsSolid(midCheck);

            if (tileCollision) return true;

            // Check draggable blocks
            Rectangle checkBox = new Rectangle((int)checkX, (int)Position.Y, 1, (int)Size.Y);
            foreach (var block in draggableBlocks)
            {
                if (block.IsSolid() && block.GetBounds().Intersects(checkBox))
                {
                    return true;
                }
            }

            return false;
        }

        private Rectangle GetBounds()
        {
            return new Rectangle((int)Position.X, (int)Position.Y, (int)Size.X, (int)Size.Y);
        }

        private DraggableBlock GetCollidingBlock()
        {
            Rectangle playerBounds = GetBounds();
            // Using Float logic from before
            foreach (var block in draggableBlocks)
            {
                if (block.IsSolid())
                {
                    Rectangle blockBounds = block.GetBounds();
                    // Float check
                    if (Position.X < blockBounds.Right &&
                        Position.X + Size.X > blockBounds.Left &&
                        Position.Y < blockBounds.Bottom &&
                        Position.Y + Size.Y > blockBounds.Top)
                    {
                        return block;
                    }
                }
            }
            return null;
        }

        private void MoveX()
        {
            Position = new Vector2(Position.X + velocity.X, Position.Y);

            // Tile Collision
            if (velocity.X > 0) // Right
            {
                Vector2 topRight = new Vector2(Position.X + Size.X, Position.Y);
                Vector2 bottomRight = new Vector2(Position.X + Size.X, Position.Y + Size.Y - 1);
                if (tileMap.IsSolid(topRight) || tileMap.IsSolid(bottomRight))
                {
                    float newX = ((int)(Position.X + Size.X) / TileMap.TileSize) * TileMap.TileSize - Size.X;
                    Position = new Vector2(newX, Position.Y);
                    velocity.X = 0;
                }
            }
            else if (velocity.X < 0) // Left
            {
                Vector2 topLeft = new Vector2(Position.X, Position.Y);
                Vector2 bottomLeft = new Vector2(Position.X, Position.Y + Size.Y - 1);
                if (tileMap.IsSolid(topLeft) || tileMap.IsSolid(bottomLeft))
                {
                    float newX = ((int)Position.X / TileMap.TileSize + 1) * TileMap.TileSize;
                    Position = new Vector2(newX, Position.Y);
                    velocity.X = 0;
                }
            }

            // Block Collision
            DraggableBlock collidingBlock = GetCollidingBlock();
            if (collidingBlock != null)
            {
                Rectangle blockBounds = collidingBlock.GetBounds();
                if (velocity.X > 0)
                {
                    Position = new Vector2(blockBounds.Left - Size.X, Position.Y);
                    velocity.X = 0;
                }
                else if (velocity.X < 0)
                {
                    Position = new Vector2(blockBounds.Right, Position.Y);
                    velocity.X = 0;
                }
            }
        }

        private void MoveY(Camera camera)
        {
            Position = new Vector2(Position.X, Position.Y + velocity.Y);
            isGrounded = false;
            bool justLanded = false;

            // Tile Collision
            if (velocity.Y > 0) // Down
            {
                Vector2 bottomLeft = new Vector2(Position.X, Position.Y + Size.Y);
                Vector2 bottomRight = new Vector2(Position.X + Size.X - 1, Position.Y + Size.Y);
                if (tileMap.IsSolid(bottomLeft) || tileMap.IsSolid(bottomRight))
                {
                    float newY = ((int)(Position.Y + Size.Y) / TileMap.TileSize) * TileMap.TileSize - Size.Y;
                    Position = new Vector2(Position.X, newY);
                    velocity.Y = 0;
                    isGrounded = true;
                    if (!wasGrounded)
                    {
                        justLanded = true;
                        Squish(1.4f, 0.6f);
                        SpawnParticles(Vector2.Zero, 1.2f);
                        if (velocity.Y > 5f) camera.Shake(2.0f, 0.1f); // Hard landing shake
                    }
                }
            }
            else if (velocity.Y < 0) // Up
            {
                Vector2 topLeft = new Vector2(Position.X, Position.Y);
                Vector2 topRight = new Vector2(Position.X + Size.X - 1, Position.Y);
                if (tileMap.IsSolid(topLeft) || tileMap.IsSolid(topRight))
                {
                    float newY = ((int)Position.Y / TileMap.TileSize + 1) * TileMap.TileSize;
                    Position = new Vector2(Position.X, newY);
                    velocity.Y = 0;
                }
            }

            // Block Collision
            DraggableBlock collidingBlock = GetCollidingBlock();
            if (collidingBlock != null)
            {
                Rectangle blockBounds = collidingBlock.GetBounds();
                if (velocity.Y > 0)
                {
                    Position = new Vector2(Position.X, blockBounds.Top - Size.Y);
                    velocity.Y = 0;
                    if (!wasGrounded && !justLanded)
                    {
                        Squish(1.4f, 0.6f);
                        SpawnParticles(Vector2.Zero, 1.2f);
                        camera.Shake(2.0f, 0.1f); // Landing shake on block
                    }
                    isGrounded = true;
                }
                else if (velocity.Y < 0)
                {
                    Position = new Vector2(Position.X, blockBounds.Bottom);
                    velocity.Y = 0;
                }
            }
        }

        private void SpawnParticles(Vector2 velocityBias, float speed)
        {
            Vector2 particlePos = Position + new Vector2(Size.X * 0.5f, Size.Y);
            particleSystem.Spawn(particlePos, 10, velocityBias, speed);
        }

        public void Squish(float scaleX, float scaleY)
        {
            RenderScale = new Vector2(scaleX, scaleY);
        }

        private void UpdateVisuals()
        {
            RenderScale = Vector2.Lerp(RenderScale, Vector2.One, SquishSpeed);

            // Update Ghost Trails
            for (int i = ghostTrails.Count - 1; i >= 0; i--)
            {
                GhostTrail trail = ghostTrails[i];
                trail.Timer -= 0.016f;
                trail.Alpha = trail.Timer / 0.2f;
                ghostTrails[i] = trail; // Struct update

                if (trail.Timer <= 0)
                {
                    ghostTrails.RemoveAt(i);
                }
            }
        }

        public void Draw(SpriteBatch spriteBatch)
        {
            // Draw Ghost Trails
            foreach (var trail in ghostTrails)
            {
                Rectangle trailRect = new Rectangle((int)trail.Position.X, (int)trail.Position.Y, (int)Size.X, (int)Size.Y);
                spriteBatch.Draw(pixelTexture, trailRect, Color.White * trail.Alpha * 0.5f);
            }

            // Draw Player
            Color playerColor = canDash ? Color.White : Color.Cyan;

            Vector2 center = Position + Size * 0.5f;
            Vector2 scaledSize = Size * RenderScale;
            Rectangle destRect = new Rectangle(
                (int)(center.X - scaledSize.X * 0.5f),
                (int)(center.Y - scaledSize.Y * 0.5f),
                (int)scaledSize.X,
                (int)scaledSize.Y
            );

            spriteBatch.Draw(pixelTexture, destRect, playerColor);
        }
        private void CheckSpecialTiles()
        {
            // Check center point for interaction
            Vector2 center = Position + Size * 0.5f;
            int tileType = tileMap.GetTileAt(center);

            if (tileType == TileMap.CHECKPOINT)
            {
                // Update spawn to center of tile
                int gridX = (int)(center.X / TileMap.TileSize);
                int gridY = (int)(center.Y / TileMap.TileSize);
                Vector2 checkoutPos = new Vector2(gridX * TileMap.TileSize, gridY * TileMap.TileSize) + new Vector2(TileMap.TileSize - Size.X, TileMap.TileSize - Size.Y) * 0.5f;
                // Wait, let's just keep position if we are standing there
                // Actually safer to align? Let's just update if it's different to avoid spam
                // But better to verify we are safe.
                // Just update spawnPosition
                spawnPosition = new Vector2(gridX * TileMap.TileSize + (TileMap.TileSize - Size.X) / 2, gridY * TileMap.TileSize + (TileMap.TileSize - Size.Y)); // Bottom align?
                                                                                                                                                                 // Let's settle for simple center of tile - size half
                spawnPosition = new Vector2(
                   gridX * TileMap.TileSize + (TileMap.TileSize - Size.X) / 2,
                   gridY * TileMap.TileSize + (TileMap.TileSize - Size.Y) / 2
               );
            }
            else if (tileType == TileMap.KILL_BLOCK)
            {
                Respawn();
            }
        }

        private void Respawn()
        {
            Position = spawnPosition;
            velocity = Vector2.Zero;
            CurrentState = PlayerState.Idle; // Or fall
            Squish(0.5f, 1.5f); // Stretch effect
            SpawnParticles(Vector2.Zero, 2.0f); // Explosion?
        }
    }
}
