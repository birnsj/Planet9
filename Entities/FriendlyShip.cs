using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;

namespace Planet9.Entities
{
    public class FriendlyShip : PlayerShip
    {
        private float _targetRotation = 0f; // Smooth rotation target
        public bool IsIdle { get; set; } = false; // Track if ship is in idle behavior
        
        public FriendlyShip(GraphicsDevice graphicsDevice, ContentManager content) 
            : base(graphicsDevice, content)
        {
            _targetRotation = Rotation;
        }

        protected override void LoadTexture()
        {
            try
            {
                // Load the ship2-256.png texture
                _texture = _content.Load<Texture2D>("ship2-256");
            }
            catch (System.Exception ex)
            {
                System.Console.WriteLine($"Failed to load friendly ship texture: {ex.Message}");
                // Fallback to parent class behavior
                base.LoadTexture();
            }
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
                    // Calculate desired velocity towards target
                    float desiredSpeed = MoveSpeed;
                    Vector2 desiredVelocity = direction;
                    desiredVelocity.Normalize();
                    desiredVelocity *= desiredSpeed;
                    
                    // Apply inertia: blend current velocity with desired velocity
                    _velocity = Vector2.Lerp(_velocity, desiredVelocity, 1f - Inertia);
                    
                    // Update position
                    Position += _velocity * deltaTime;
                    
                    // Smoothly rotate to face the direction of movement using shortest rotation path (unless in idle behavior)
                    if (!IsIdle && direction.LengthSquared() > 0.1f) // Only rotate if there's a direction
                    {
                        // Calculate rotation to face target direction (not velocity, but desired direction)
                        float targetRotation = (float)System.Math.Atan2(direction.Y, direction.X) + MathHelper.PiOver2;
                        
                        // Update smooth rotation target
                        _targetRotation = targetRotation;
                        
                        // Calculate shortest rotation path
                        float angleDiff = _targetRotation - Rotation;
                        
                        // Normalize angle difference to [-Pi, Pi] to get shortest path
                        while (angleDiff > MathHelper.Pi)
                            angleDiff -= MathHelper.TwoPi;
                        while (angleDiff < -MathHelper.Pi)
                            angleDiff += MathHelper.TwoPi;
                        
                        // Smoothly rotate towards target using rotation speed with interpolation
                        float rotationDelta = RotationSpeed * deltaTime;
                        if (System.Math.Abs(angleDiff) < rotationDelta)
                        {
                            // Close enough, snap to target
                            Rotation = _targetRotation;
                        }
                        else
                        {
                            // Smooth interpolation for smoother turning
                            float lerpFactor = MathHelper.Clamp(rotationDelta / System.Math.Abs(angleDiff), 0f, 1f);
                            Rotation = MathHelper.Lerp(Rotation, _targetRotation, lerpFactor);
                        }
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
                // Not moving - apply drift if enabled
                if (Drift > 0f && _velocity.LengthSquared() < 1f) // Only drift when nearly stopped
                {
                    // Update drift direction change timer
                    _driftDirectionChangeTimer -= deltaTime;
                    if (_driftDirectionChangeTimer <= 0f)
                    {
                        // Change drift direction randomly every 1-3 seconds
                        _driftDirection = (float)(_driftRandom.NextDouble() * Microsoft.Xna.Framework.MathHelper.TwoPi);
                        _driftDirectionChangeTimer = (float)(_driftRandom.NextDouble() * 2f + 1f);
                    }
                    
                    // Apply drift velocity
                    Vector2 driftVelocity = new Vector2(
                        (float)System.Math.Cos(_driftDirection),
                        (float)System.Math.Sin(_driftDirection)
                    ) * Drift * MoveSpeed * 0.3f; // 30% of move speed for drift
                    
                    Position += driftVelocity * deltaTime;
                    
                    // Do not rotate when idle - ships should maintain their current rotation
                    // (Rotation code removed for idle behavior)
                }
                else
                {
                    // Apply inertia to slow down
                    _velocity *= Inertia;
                    Position += _velocity * deltaTime;
                    
                    // Do not rotate when idle - ships should maintain their current rotation
                    // Only rotate if not idle and has significant velocity
                    if (!IsIdle && _velocity.LengthSquared() > 100f) // Only rotate if moving with significant speed
                    {
                        float targetRotation = (float)System.Math.Atan2(_velocity.Y, _velocity.X) + MathHelper.PiOver2;
                        _targetRotation = targetRotation;
                        
                        float angleDiff = _targetRotation - Rotation;
                        while (angleDiff > MathHelper.Pi) angleDiff -= MathHelper.TwoPi;
                        while (angleDiff < -MathHelper.Pi) angleDiff += MathHelper.TwoPi;
                        
                        float rotationDelta = RotationSpeed * deltaTime;
                        if (System.Math.Abs(angleDiff) < rotationDelta)
                            Rotation = _targetRotation;
                        else
                        {
                            // Smooth interpolation for smoother turning
                            float lerpFactor = MathHelper.Clamp(rotationDelta / System.Math.Abs(angleDiff), 0f, 1f);
                            Rotation = MathHelper.Lerp(Rotation, _targetRotation, lerpFactor);
                        }
                    }
                }
            }
        }
    }
}

