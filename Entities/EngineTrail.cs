using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Planet9.Entities
{
    public class EngineTrail
    {
        private List<Particle> _particles;
        private GraphicsDevice _graphicsDevice;
        private static Texture2D? _particleTexture;
        private const float ParticleLifetime = 0.5f; // Seconds
        private const float EmissionRate = 60f; // Particles per second
        private float _timeSinceLastEmission = 0f;
        private const float EmissionInterval = 1f / EmissionRate;

        public EngineTrail(GraphicsDevice graphicsDevice)
        {
            _graphicsDevice = graphicsDevice;
            _particles = new List<Particle>();
            
            // Create particle texture if needed
            if (_particleTexture == null)
            {
                _particleTexture = new Texture2D(_graphicsDevice, 1, 1);
                _particleTexture.SetData(new[] { Color.White });
            }
        }

        public void Emit(Vector2 position, float rotation, float speed)
        {
            // Calculate backward direction (opposite of ship's facing direction)
            // Ship points in the direction of rotation, engine is at the back
            var backwardDirection = new Vector2(
                -(float)Math.Sin(rotation),
                (float)Math.Cos(rotation)
            );

            // Create particle at ship's rear (slightly offset from center)
            float offsetDistance = 40f; // Distance from ship center to engine
            Vector2 emitPosition = position + backwardDirection * offsetDistance;

            // Add some randomness to position and velocity
            var random = new Random();
            float angleVariation = (float)(random.NextDouble() - 0.5) * 0.5f; // ±0.25 radians
            float speedVariation = (float)(random.NextDouble() * 0.3f + 0.7f); // 70-100% of base speed
            
            var particleDirection = new Vector2(
                backwardDirection.X * (float)Math.Cos(angleVariation) - backwardDirection.Y * (float)Math.Sin(angleVariation),
                backwardDirection.X * (float)Math.Sin(angleVariation) + backwardDirection.Y * (float)Math.Cos(angleVariation)
            );

            var particle = new Particle
            {
                Position = emitPosition + new Vector2(
                    (float)(random.NextDouble() - 0.5) * 5f,
                    (float)(random.NextDouble() - 0.5) * 5f
                ),
                Velocity = particleDirection * speed * speedVariation,
                Color = new Color(
                    (byte)(200 + random.Next(55)), // Orange-red range
                    (byte)(100 + random.Next(50)),
                    (byte)(random.Next(30)),
                    (byte)255
                ),
                Life = 1f,
                Size = (float)(random.NextDouble() * 3f + 2f), // 2-5 pixels
                LifeTime = ParticleLifetime + (float)(random.NextDouble() - 0.5) * 0.2f,
                Age = 0f
            };

            _particles.Add(particle);
        }

        public void Update(float deltaTime, Vector2 position, float rotation, float speed)
        {
            // Emit particles based on speed (more particles when moving faster)
            if (speed > 10f) // Only emit when moving
            {
                _timeSinceLastEmission += deltaTime;
                
                // Emit more frequently when moving faster
                float speedFactor = MathHelper.Clamp(speed / 300f, 0.1f, 1f);
                float adjustedInterval = EmissionInterval / speedFactor;
                
                while (_timeSinceLastEmission >= adjustedInterval)
                {
                    Emit(position, rotation, speed);
                    _timeSinceLastEmission -= adjustedInterval;
                }
            }

            // Update existing particles
            for (int i = _particles.Count - 1; i >= 0; i--)
            {
                _particles[i].Update(deltaTime);
                
                if (!_particles[i].IsAlive)
                {
                    _particles.RemoveAt(i);
                }
            }
        }

        public void Draw(SpriteBatch spriteBatch)
        {
            if (_particleTexture == null) return;

            // Draw particles with additive blending for glow effect
            foreach (var particle in _particles)
            {
                if (particle.IsAlive)
                {
                    // Calculate alpha based on life
                    Color drawColor = particle.Color * particle.Life;
                    
                    spriteBatch.Draw(
                        _particleTexture,
                        particle.Position,
                        null,
                        drawColor,
                        0f,
                        Vector2.Zero,
                        particle.Size,
                        SpriteEffects.None,
                        0f
                    );
                }
            }
        }

        public void Clear()
        {
            _particles.Clear();
        }
    }
}

