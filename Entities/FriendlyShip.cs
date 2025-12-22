using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;

namespace Planet9.Entities
{
    public class FriendlyShip : PlayerShip
    {
        private float _targetRotation = 0f; // Smooth rotation target
        
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
                    
                    // Always face the direction of actual movement (velocity direction)
                    if (_velocity.LengthSquared() > 0.1f) // Only rotate if actually moving
                    {
                        // Calculate rotation to face velocity direction
                        float targetRotation = (float)System.Math.Atan2(_velocity.Y, _velocity.X) + MathHelper.PiOver2;
                        
                        // Update smooth rotation target
                        _targetRotation = targetRotation;
                        
                        // Calculate shortest rotation path
                        float angleDiff = _targetRotation - Rotation;
                        
                        // Normalize angle difference to [-Pi, Pi]
                        while (angleDiff > MathHelper.Pi)
                            angleDiff -= MathHelper.TwoPi;
                        while (angleDiff < -MathHelper.Pi)
                            angleDiff += MathHelper.TwoPi;
                        
                        // Always rotate to face movement direction (no minimum angle threshold)
                        float rotationDelta = RotationSpeed * deltaTime * 0.6f; // 40% reduction for very smooth turning
                        
                        // Use lerp for smooth rotation
                        float lerpAmount = System.Math.Min(rotationDelta / System.Math.Max(System.Math.Abs(angleDiff), 0.01f), 1f);
                        Rotation = MathHelper.Lerp(Rotation, _targetRotation, lerpAmount);
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
                    
                    // Face drift direction
                    if (driftVelocity.LengthSquared() > 0.1f)
                    {
                        float targetRotation = (float)System.Math.Atan2(driftVelocity.Y, driftVelocity.X) + MathHelper.PiOver2;
                        float angleDiff = targetRotation - Rotation;
                        while (angleDiff > MathHelper.Pi) angleDiff -= MathHelper.TwoPi;
                        while (angleDiff < -MathHelper.Pi) angleDiff += MathHelper.TwoPi;
                        float rotationDelta = RotationSpeed * deltaTime * 0.6f;
                        float lerpAmount = System.Math.Min(rotationDelta / System.Math.Max(System.Math.Abs(angleDiff), 0.01f), 1f);
                        Rotation = MathHelper.Lerp(Rotation, targetRotation, lerpAmount);
                    }
                }
                else
                {
                    // Apply inertia to slow down
                    _velocity *= Inertia;
                    Position += _velocity * deltaTime;
                    
                    // Face velocity direction while coasting
                    if (_velocity.LengthSquared() > 0.1f)
                    {
                        float targetRotation = (float)System.Math.Atan2(_velocity.Y, _velocity.X) + MathHelper.PiOver2;
                        float angleDiff = targetRotation - Rotation;
                        while (angleDiff > MathHelper.Pi) angleDiff -= MathHelper.TwoPi;
                        while (angleDiff < -MathHelper.Pi) angleDiff += MathHelper.TwoPi;
                        float rotationDelta = RotationSpeed * deltaTime * 0.6f;
                        float lerpAmount = System.Math.Min(rotationDelta / System.Math.Max(System.Math.Abs(angleDiff), 0.01f), 1f);
                        Rotation = MathHelper.Lerp(Rotation, targetRotation, lerpAmount);
                    }
                }
            }
        }
    }
}

