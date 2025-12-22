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
                    // Calculate rotation to face movement direction
                    float targetRotation = (float)System.Math.Atan2(direction.Y, direction.X) + MathHelper.PiOver2;
                    
                    // Update smooth rotation target
                    _targetRotation = targetRotation;
                    
                    // Calculate shortest rotation path
                    float angleDiff = _targetRotation - Rotation;
                    
                    // Normalize angle difference to [-Pi, Pi]
                    while (angleDiff > MathHelper.Pi)
                        angleDiff -= MathHelper.TwoPi;
                    while (angleDiff < -MathHelper.Pi)
                        angleDiff += MathHelper.TwoPi;
                    
                    // Only turn if the angle difference is significant
                    const float minTurnAngle = 0.52f; // ~30 degrees
                    
                    if (System.Math.Abs(angleDiff) > minTurnAngle)
                    {
                        // Smoothly interpolate rotation towards target (much smoother than base class)
                        float rotationDelta = RotationSpeed * deltaTime * 0.6f; // 40% reduction for very smooth turning
                        
                        // Use lerp for even smoother rotation
                        float lerpAmount = System.Math.Min(rotationDelta / System.Math.Abs(angleDiff), 1f);
                        Rotation = MathHelper.Lerp(Rotation, _targetRotation, lerpAmount);
                    }
                    
                    // Calculate desired velocity towards target
                    float desiredSpeed = MoveSpeed;
                    Vector2 desiredVelocity = direction;
                    desiredVelocity.Normalize();
                    desiredVelocity *= desiredSpeed;
                    
                    // Apply inertia: blend current velocity with desired velocity
                    _velocity = Vector2.Lerp(_velocity, desiredVelocity, 1f - Inertia);
                    
                    // Update position
                    Position += _velocity * deltaTime;
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
                }
                else
                {
                    // Apply inertia to slow down
                    _velocity *= Inertia;
                    Position += _velocity * deltaTime;
                }
                
                // Handle aiming at cursor when stationary
                if (_aimTarget.HasValue)
                {
                    Vector2 aimDirection = _aimTarget.Value - Position;
                    if (aimDirection.LengthSquared() > 0.1f) // Only rotate if target is not too close
                    {
                        // Calculate target rotation to face aim direction
                        float targetRotation = (float)System.Math.Atan2(aimDirection.Y, aimDirection.X) + MathHelper.PiOver2;
                        
                        // Smoothly rotate towards target direction using aim rotation speed
                        float rotationDelta = AimRotationSpeed * deltaTime * 0.6f; // 40% reduction for smoother turning
                        
                        // Calculate shortest rotation path
                        float angleDiff = targetRotation - Rotation;
                        
                        // Normalize angle difference to [-Pi, Pi]
                        while (angleDiff > MathHelper.Pi)
                            angleDiff -= MathHelper.TwoPi;
                        while (angleDiff < -MathHelper.Pi)
                            angleDiff += MathHelper.TwoPi;
                        
                        // Rotate towards target
                        if (System.Math.Abs(angleDiff) < rotationDelta)
                        {
                            Rotation = targetRotation;
                        }
                        else
                        {
                            // Use lerp for smoother rotation
                            float lerpAmount = System.Math.Min(rotationDelta / System.Math.Abs(angleDiff), 1f);
                            Rotation = MathHelper.Lerp(Rotation, targetRotation, lerpAmount);
                        }
                    }
                }
            }
        }
    }
}

