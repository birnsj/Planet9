using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;

namespace Planet9.Entities
{
    public class PlayerShip : Entity
    {
        protected Texture2D? _texture;
        protected GraphicsDevice _graphicsDevice;
        protected ContentManager _content;
        private const int ShipSize = 128;
        protected Vector2 _targetPosition;
        public float MoveSpeed { get; set; } = 300f; // pixels per second
        public float RotationSpeed { get; set; } = 3f; // radians per second (for movement) - reduced for smoother turning
        public float AimRotationSpeed { get; set; } = 3f; // radians per second (for aiming at cursor when stationary) - reduced for smoother turning
        public float Inertia { get; set; } = 0.9f; // Inertia/damping factor (0-1, higher = more inertia)
        public float Drift { get; set; } = 0f; // Drift amount when idle (0 = no drift, higher = more random direction drift)
        public float AvoidanceDetectionRange { get; set; } = 300f; // Avoidance detection range for this ship
        protected bool _isMoving = false;
        protected Vector2 _velocity = Vector2.Zero; // Current velocity for inertia
        protected Vector2? _aimTarget = null; // Target position to aim at when not moving
        protected EngineTrail? _engineTrail;
        protected System.Random _driftRandom = new System.Random(); // Random for drift direction
        protected float _driftDirection = 0f; // Current drift direction in radians
        protected float _driftDirectionChangeTimer = 0f; // Timer for changing drift direction

        public PlayerShip(GraphicsDevice graphicsDevice, ContentManager content)
        {
            _graphicsDevice = graphicsDevice;
            _content = content;
            Rotation = 0f; // Ship points up (north) by default
            _targetPosition = Position;
            LoadTexture();
            _engineTrail = new EngineTrail(_graphicsDevice);
        }
        
        public Vector2 TargetPosition => _targetPosition; // Public property to access target position
        
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
        
        public void SetAimTarget(Vector2? target)
        {
            _aimTarget = target;
        }
        
        public Texture2D? GetTexture()
        {
            return _texture;
        }
        
        public bool IsActivelyMoving()
        {
            // Only return true if actively moving (has a target), not just coasting from inertia
            return _isMoving;
        }

        protected virtual void LoadTexture()
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
            
            // Update engine trail
            float currentSpeed = _velocity.Length();
            if (_texture != null && _engineTrail != null)
            {
                _engineTrail.Update(deltaTime, Position, Rotation, currentSpeed, _texture.Width, _texture.Height);
            }
            
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
                    
                    // Calculate shortest rotation path
                    float angleDiff = targetRotation - Rotation;
                    
                    // Normalize angle difference to [-Pi, Pi]
                    while (angleDiff > MathHelper.Pi)
                        angleDiff -= MathHelper.TwoPi;
                    while (angleDiff < -MathHelper.Pi)
                        angleDiff += MathHelper.TwoPi;
                    
                    // Only turn if the angle difference is significant (ships travel longer before turning)
                    // Minimum angle threshold: ~30 degrees (0.52 radians) before starting to turn
                    const float minTurnAngle = 0.52f; // ~30 degrees
                    
                    if (Math.Abs(angleDiff) > minTurnAngle)
                    {
                        // Smoothly rotate towards target direction (reduced for less aggressive turning)
                        float rotationDelta = RotationSpeed * deltaTime * 0.8f; // 20% reduction for smoother turning
                        
                        // Rotate towards target
                        if (Math.Abs(angleDiff) < rotationDelta)
                        {
                            Rotation = targetRotation;
                        }
                        else
                        {
                            Rotation += Math.Sign(angleDiff) * rotationDelta;
                        }
                    }
                    // If angle difference is small, continue forward without turning
                    
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
                
                // Apply drift when idle (not moving and velocity is near zero)
                if (Drift > 0f && _velocity.LengthSquared() < 1f)
                {
                    // Change drift direction periodically (every 1-3 seconds)
                    _driftDirectionChangeTimer -= deltaTime;
                    if (_driftDirectionChangeTimer <= 0f)
                    {
                        // Pick a new random drift direction
                        _driftDirection = (float)(_driftRandom.NextDouble() * MathHelper.TwoPi);
                        _driftDirectionChangeTimer = (float)(_driftRandom.NextDouble() * 2f + 1f); // 1-3 seconds
                    }
                    
                    // Apply drift velocity in random direction
                    float driftSpeed = Drift * 50f; // Scale drift value to pixels per second
                    Vector2 driftVelocity = new Vector2(
                        (float)Math.Cos(_driftDirection),
                        (float)Math.Sin(_driftDirection)
                    ) * driftSpeed;
                    
                    // Apply drift to position
                    Position += driftVelocity * deltaTime;
                }
                
                // When not moving, rotate toward aim target (mouse cursor/firing direction)
                if (_aimTarget.HasValue)
                {
                    var aimDirection = _aimTarget.Value - Position;
                    if (aimDirection.LengthSquared() > 0.1f) // Only rotate if target is not too close
                    {
                        // Calculate target rotation to face aim direction
                        float targetRotation = (float)Math.Atan2(aimDirection.Y, aimDirection.X) + MathHelper.PiOver2;
                        
                        // Smoothly rotate towards target direction using aim rotation speed (reduced for smoother turning)
                        float rotationDelta = AimRotationSpeed * deltaTime * 0.8f; // 20% reduction for smoother turning
                        
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
                    }
                }
            }
        }

        public override void Draw(SpriteBatch spriteBatch)
        {
            Draw(spriteBatch, 1.0f);
        }
        
        public void Draw(SpriteBatch spriteBatch, float alpha)
        {
            // Draw engine trail first (behind the ship) with alpha applied
            if (_engineTrail != null && alpha > 0.01f)
            {
                // Draw engine trail with alpha (we'll need to modify EngineTrail to accept alpha)
                // For now, just draw it normally - particles fade on their own
                _engineTrail.Draw(spriteBatch);
            }
            
            if (_texture != null && IsActive && alpha > 0.01f)
            {
                var origin = new Vector2(_texture.Width / 2f, _texture.Height / 2f);
                
                // Draw at native size (no scaling) with alpha
                spriteBatch.Draw(
                    _texture,
                    Position,
                    null,
                    Color.White * alpha,
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

