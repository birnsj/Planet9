using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;

namespace Planet9.Entities
{
    public class PlayerShip : Entity
    {
        private Texture2D? _texture;
        private GraphicsDevice _graphicsDevice;
        private ContentManager _content;
        private const int ShipSize = 128;
        private Vector2 _targetPosition;
        public float MoveSpeed { get; set; } = 300f; // pixels per second
        public float RotationSpeed { get; set; } = 5f; // radians per second
        public float Inertia { get; set; } = 0.9f; // Inertia/damping factor (0-1, higher = more inertia)
        private bool _isMoving = false;
        private Vector2 _velocity = Vector2.Zero; // Current velocity for inertia

        public PlayerShip(GraphicsDevice graphicsDevice, ContentManager content)
        {
            _graphicsDevice = graphicsDevice;
            _content = content;
            Rotation = 0f; // Ship points up (north) by default
            _targetPosition = Position;
            LoadTexture();
        }
        
        public void SetTargetPosition(Vector2 target)
        {
            _targetPosition = target;
            _isMoving = true;
        }
        
        public void StopMoving()
        {
            _isMoving = false;
            _targetPosition = Position; // Set target to current position
            // Don't reset velocity - let inertia handle it
        }
        
        public Texture2D? GetTexture()
        {
            return _texture;
        }

        private void LoadTexture()
        {
            try
            {
                // Load the ship1-256.png texture
                _texture = _content.Load<Texture2D>("ship1-256");
            }
            catch (System.Exception ex)
            {
                System.Console.WriteLine($"Failed to load ship texture: {ex.Message}");
                // Fallback to creating a simple triangle if texture fails to load
                CreateTempTexture();
            }
        }

        private void CreateTempTexture()
        {
            // Create a simple 128x128 ship graphic - triangle pointing up (north)
            _texture = new Texture2D(_graphicsDevice, ShipSize, ShipSize);
            var colorData = new Color[ShipSize * ShipSize];

            // Fill with transparent background
            for (int i = 0; i < colorData.Length; i++)
            {
                colorData[i] = Color.Transparent;
            }

            // Draw a simple triangle ship pointing up (north)
            int centerX = ShipSize / 2;
            int triangleHeight = ShipSize - 20; // Leave some margin
            int triangleBase = ShipSize / 2; // Base width

            // Create triangle shape: point at top (north), base at bottom (south)
            for (int y = 0; y < ShipSize; y++)
            {
                for (int x = 0; x < ShipSize; x++)
                {
                    int dx = Math.Abs(x - centerX);
                    
                    // Calculate the width of the triangle at this Y position
                    // At top (y=0): width = 0 (point)
                    // At bottom (y=triangleHeight): width = triangleBase
                    int triangleY = y - 10; // Offset from top margin
                    
                    if (triangleY >= 0 && triangleY < triangleHeight)
                    {
                        // Calculate width at this Y position
                        int maxWidth = (int)((float)triangleY / triangleHeight * triangleBase);
                        
                        if (dx <= maxWidth)
                        {
                            colorData[y * ShipSize + x] = Color.Cyan;
                        }
                    }
                }
            }

            _texture.SetData(colorData);
        }

        public override void Update(GameTime gameTime)
        {
            var deltaTime = (float)gameTime.ElapsedGameTime.TotalSeconds;
            
            if (_isMoving)
            {
                var direction = _targetPosition - Position;
                var distance = direction.Length();
                
                if (distance > 1f) // Move if more than 1 pixel away
                {
                    // Calculate rotation to face movement direction
                    // Atan2 gives angle in radians, where 0 is right, Pi/2 is down, -Pi/2 is up
                    // Ship points up (north) at rotation 0, so we need to adjust
                    // For ship pointing up: Atan2(Y, X) - Pi/2 makes 0 point up
                    // But we need to add Pi to flip it if it's backwards
                    float targetRotation = (float)Math.Atan2(direction.Y, direction.X) + MathHelper.PiOver2;
                    
                    // Smoothly rotate towards target direction
                    float rotationDelta = RotationSpeed * deltaTime;
                    
                    // Calculate shortest rotation path
                    float angleDiff = targetRotation - Rotation;
                    
                    // Normalize angle difference to [-Pi, Pi]
                    while (angleDiff > MathHelper.Pi)
                        angleDiff -= MathHelper.TwoPi;
                    while (angleDiff < -MathHelper.Pi)
                        angleDiff += MathHelper.TwoPi;
                    
                    // Rotate towards target
                    if (Math.Abs(angleDiff) < rotationDelta)
                    {
                        Rotation = targetRotation;
                    }
                    else
                    {
                        Rotation += Math.Sign(angleDiff) * rotationDelta;
                    }
                    
                    // Calculate desired velocity towards target
                    direction.Normalize();
                    var desiredVelocity = direction * MoveSpeed;
                    
                    // Apply inertia: gradually change velocity towards desired velocity
                    _velocity = Vector2.Lerp(_velocity, desiredVelocity, 1f - Inertia);
                    
                    // Move based on current velocity
                    var moveDistance = _velocity.Length() * deltaTime;
                    
                    // Don't overshoot the target
                    if (moveDistance > distance)
                    {
                        Position = _targetPosition;
                        _isMoving = false;
                        _velocity = Vector2.Zero; // Stop at target
                    }
                    else
                    {
                        Position += _velocity * deltaTime;
                    }
                }
                else
                {
                    // Reached target
                    Position = _targetPosition;
                    _isMoving = false;
                    _velocity = Vector2.Zero; // Stop at target
                }
            }
            else
            {
                // Apply inertia when not moving - gradually slow down
                _velocity *= Inertia;
                
                // Apply velocity
                Position += _velocity * deltaTime;
                
                // Stop if velocity is very small
                if (_velocity.LengthSquared() < 1f)
                {
                    _velocity = Vector2.Zero;
                }
            }
        }

        public override void Draw(SpriteBatch spriteBatch)
        {
            if (_texture != null && IsActive)
            {
                var origin = new Vector2(_texture.Width / 2f, _texture.Height / 2f);
                
                // Draw at native size (no scaling)
                spriteBatch.Draw(
                    _texture,
                    Position,
                    null,
                    Color.White,
                    Rotation,
                    origin,
                    1f, // No scaling - use native texture size
                    SpriteEffects.None,
                    0f
                );
            }
        }
    }
}

