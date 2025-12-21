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
        private const float ParticleLifetime = 0.8f; // Seconds (longer lifetime)
        private const float EmissionRate = 120f; // Particles per second (more particles)
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

        public void Emit(Vector2 position, float rotation, float speed, float textureWidth, float textureHeight, float spriteX, float spriteY)
        {
            // Engine particles emit from specified sprite coordinates
            // Convert sprite coordinates to offset from ship center
            float textureCenterX = textureWidth / 2f;
            float textureCenterY = textureHeight / 2f;
            float offsetX = spriteX - textureCenterX;
            float offsetY = spriteY - textureCenterY;
            
            // Rotate the offset by ship's rotation to get world-space offset
            float cos = (float)Math.Cos(rotation);
            float sin = (float)Math.Sin(rotation);
            float rotatedX = offsetX * cos - offsetY * sin;
            float rotatedY = offsetX * sin + offsetY * cos;
            
            // Calculate emit position in world space
            Vector2 emitPosition = position + new Vector2(rotatedX, rotatedY);
            
            // Calculate backward direction (opposite of ship's facing direction) for particle velocity
            var backwardDirection = new Vector2(
                -(float)Math.Sin(rotation),
                (float)Math.Cos(rotation)
            );

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
                    (byte)(220 + random.Next(35)), // Brighter orange-red range
                    (byte)(120 + random.Next(50)),
                    (byte)(random.Next(20)),
                    (byte)255
                ),
                Life = 1f,
                Size = (float)(random.NextDouble() * 5f + 4f), // 4-9 pixels (larger)
                LifeTime = ParticleLifetime + (float)(random.NextDouble() - 0.5) * 0.2f,
                Age = 0f
            };

            _particles.Add(particle);
        }

        public void Update(float deltaTime, Vector2 position, float rotation, float speed, float textureWidth, float textureHeight)
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
                    // Emit from first engine at (180, 180)
                    Emit(position, rotation, speed, textureWidth, textureHeight, 180f, 180f);
                    
                    // Emit from second engine at (70, 180)
                    Emit(position, rotation, speed, textureWidth, textureHeight, 70f, 180f);
                    
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

