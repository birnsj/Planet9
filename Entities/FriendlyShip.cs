using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;

namespace Planet9.Entities
{
    public class FriendlyShip : PlayerShip
    {
        private float _targetRotation = 0f; // Smooth rotation target
        public bool IsIdle { get; set; } = false; // Track if ship is in idle behavior
        
        public FriendlyShip(GraphicsDevice graphicsDevice, ContentManager content, System.Random? random = null) 
            : base(graphicsDevice, content, random)
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
            
            // Health regeneration (only if ship is alive and not at full health)
            if (Health > 0f && Health < MaxHealth)
            {
                Health += HealthRegenRate * deltaTime;
                if (Health > MaxHealth)
                {
                    Health = MaxHealth; // Clamp to max health
                }
            }
            
            // Update damage effect (activate when ship is damaged and remain active while damaged)
            if (_damageEffect != null)
            {
                // Activate damage effect when ship has taken damage (Health < MaxHealth)
                // This will show particles when fleeing starts and keep them active while the ship is damaged
                bool shouldShowDamage = Health < MaxHealth && Health > 0f;
                _damageEffect.SetActive(shouldShowDamage);
                
                if (shouldShowDamage)
                {
                    _damageEffect.Update(deltaTime, Position, Rotation);
                }
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
                    
                    // Check if we have an aim target (for enemy ships attacking player) - prioritize this over movement direction
                    if (_aimTarget.HasValue)
                    {
                        // Prioritize aiming at target (e.g., player for enemy ships)
                        var aimDirection = _aimTarget.Value - Position;
                        if (aimDirection.LengthSquared() > 0.1f)
                        {
                            float targetRotation = (float)System.Math.Atan2(aimDirection.Y, aimDirection.X) + MathHelper.PiOver2;
                            float angleDiff = targetRotation - Rotation;
                            while (angleDiff > MathHelper.Pi) angleDiff -= MathHelper.TwoPi;
                            while (angleDiff < -MathHelper.Pi) angleDiff += MathHelper.TwoPi;
                            float rotationDelta = AimRotationSpeed * deltaTime * 3f; // Fast rotation to face target
                            if (System.Math.Abs(angleDiff) < rotationDelta)
                                Rotation = targetRotation;
                            else
                                Rotation += System.Math.Sign(angleDiff) * rotationDelta;
                        }
                    }
                    // Smoothly rotate to face the direction of movement (velocity direction) using shortest rotation path (unless in idle behavior)
                    // Always face the direction we're actually moving, not the target direction
                    else if (!IsIdle && _velocity.LengthSquared() > 1f) // Only rotate if we have significant velocity
                    {
                        // Calculate rotation to face velocity direction (the direction we're actually moving)
                        float targetRotation = (float)System.Math.Atan2(_velocity.Y, _velocity.X) + MathHelper.PiOver2;
                        
                        // Smooth the target rotation itself to prevent sudden changes
                        float targetAngleDiff = targetRotation - _targetRotation;
                        while (targetAngleDiff > MathHelper.Pi) targetAngleDiff -= MathHelper.TwoPi;
                        while (targetAngleDiff < -MathHelper.Pi) targetAngleDiff += MathHelper.TwoPi;
                        
                        // Gradually update target rotation (smooth out sudden direction changes)
                        float targetRotationSmoothing = 0.3f; // How quickly target rotation updates (0-1, lower = smoother)
                        _targetRotation += targetAngleDiff * targetRotationSmoothing;
                        
                        // Normalize target rotation
                        while (_targetRotation > MathHelper.TwoPi) _targetRotation -= MathHelper.TwoPi;
                        while (_targetRotation < 0) _targetRotation += MathHelper.TwoPi;
                        
                        // Calculate shortest rotation path
                        float angleDiff = _targetRotation - Rotation;
                        
                        // Normalize angle difference to [-Pi, Pi] to get shortest path
                        while (angleDiff > MathHelper.Pi)
                            angleDiff -= MathHelper.TwoPi;
                        while (angleDiff < -MathHelper.Pi)
                            angleDiff += MathHelper.TwoPi;
                        
                        // Smoothly rotate towards target using rotation speed with interpolation
                        // Use adaptive rotation speed based on angle difference for smoother turning
                        float rotationDelta = RotationSpeed * deltaTime;
                        
                        // Scale rotation speed based on angle - much slower for large angles (sharp turns)
                        // For sharp turns (close to 180 degrees), reduce speed significantly
                        float absAngleDiff = System.Math.Abs(angleDiff);
                        float angleScale;
                        if (absAngleDiff > MathHelper.Pi * 0.75f) // Very sharp turn (135+ degrees)
                        {
                            // Very slow for sharp turns in place
                            angleScale = 0.3f; // 30% of normal speed
                        }
                        else if (absAngleDiff > MathHelper.Pi * 0.5f) // Sharp turn (90+ degrees)
                        {
                            // Slow for sharp turns
                            angleScale = 0.5f; // 50% of normal speed
                        }
                        else if (absAngleDiff > MathHelper.Pi * 0.25f) // Moderate turn (45+ degrees)
                        {
                            // Slightly reduced for moderate turns
                            angleScale = 0.75f; // 75% of normal speed
                        }
                        else
                        {
                            // Normal speed for small turns
                            angleScale = 1f;
                        }
                        rotationDelta *= angleScale;
                        
                        if (System.Math.Abs(angleDiff) < rotationDelta)
                        {
                            // Close enough, snap to target
                            Rotation = _targetRotation;
                        }
                        else
                        {
                            // Smooth interpolation for smoother turning
                            // Use slower lerp for sharp turns
                            float baseLerpFactor = MathHelper.Clamp(rotationDelta / absAngleDiff, 0.1f, 0.5f);
                            // Further reduce lerp speed for sharp turns
                            if (absAngleDiff > MathHelper.Pi * 0.5f)
                            {
                                baseLerpFactor *= 0.6f; // Even slower for sharp turns
                            }
                            Rotation = MathHelper.Lerp(Rotation, _targetRotation, baseLerpFactor);
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
                        if (_driftRandom != null)
                        {
                            _driftDirection = (float)(_driftRandom.NextDouble() * Microsoft.Xna.Framework.MathHelper.TwoPi);
                            _driftDirectionChangeTimer = (float)(_driftRandom.NextDouble() * 2f + 1f);
                        }
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
                    
                    // Always rotate to face velocity direction when moving (even when coasting)
                    // Only rotate if not idle and has any velocity
                    if (!IsIdle && _velocity.LengthSquared() > 1f) // Rotate if we have any velocity
                    {
                        float targetRotation = (float)System.Math.Atan2(_velocity.Y, _velocity.X) + MathHelper.PiOver2;
                        
                        // Smooth the target rotation itself to prevent sudden changes
                        float targetAngleDiff = targetRotation - _targetRotation;
                        while (targetAngleDiff > MathHelper.Pi) targetAngleDiff -= MathHelper.TwoPi;
                        while (targetAngleDiff < -MathHelper.Pi) targetAngleDiff += MathHelper.TwoPi;
                        
                        // Gradually update target rotation (smooth out sudden direction changes)
                        float targetRotationSmoothing = 0.3f; // How quickly target rotation updates (0-1, lower = smoother)
                        _targetRotation += targetAngleDiff * targetRotationSmoothing;
                        
                        // Normalize target rotation
                        while (_targetRotation > MathHelper.TwoPi) _targetRotation -= MathHelper.TwoPi;
                        while (_targetRotation < 0) _targetRotation += MathHelper.TwoPi;
                        
                        float angleDiff = _targetRotation - Rotation;
                        while (angleDiff > MathHelper.Pi) angleDiff -= MathHelper.TwoPi;
                        while (angleDiff < -MathHelper.Pi) angleDiff += MathHelper.TwoPi;
                        
                        // Use adaptive rotation speed - much slower for sharp turns
                        float absAngleDiff = System.Math.Abs(angleDiff);
                        float rotationDelta = RotationSpeed * deltaTime;
                        float angleScale;
                        if (absAngleDiff > MathHelper.Pi * 0.75f) // Very sharp turn (135+ degrees)
                        {
                            // Very slow for sharp turns in place
                            angleScale = 0.3f; // 30% of normal speed
                        }
                        else if (absAngleDiff > MathHelper.Pi * 0.5f) // Sharp turn (90+ degrees)
                        {
                            // Slow for sharp turns
                            angleScale = 0.5f; // 50% of normal speed
                        }
                        else if (absAngleDiff > MathHelper.Pi * 0.25f) // Moderate turn (45+ degrees)
                        {
                            // Slightly reduced for moderate turns
                            angleScale = 0.75f; // 75% of normal speed
                        }
                        else
                        {
                            // Normal speed for small turns
                            angleScale = 1f;
                        }
                        rotationDelta *= angleScale;
                        
                        if (absAngleDiff < rotationDelta)
                            Rotation = _targetRotation;
                        else
                        {
                            // Smooth interpolation for smoother turning
                            // Use slower lerp for sharp turns
                            float baseLerpFactor = MathHelper.Clamp(rotationDelta / absAngleDiff, 0.1f, 0.5f);
                            // Further reduce lerp speed for sharp turns
                            if (absAngleDiff > MathHelper.Pi * 0.5f)
                            {
                                baseLerpFactor *= 0.6f; // Even slower for sharp turns
                            }
                            Rotation = MathHelper.Lerp(Rotation, _targetRotation, baseLerpFactor);
                        }
                    }
                }
            }
        }
    }
}

