using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Planet9.Core;

namespace Planet9.Entities
{
    public class EngineTrail
    {
        private List<Particle> _particles;
        private GraphicsDevice _graphicsDevice;
        private Texture2D? _particleTexture;
        private System.Random? _random; // Shared Random instance
        private const float ParticleLifetime = 0.8f; // Seconds (longer lifetime)
        private const float EmissionRate = 120f; // Particles per second (more particles)
        private float _timeSinceLastEmission = 0f;
        private const float EmissionInterval = 1f / EmissionRate;

        public EngineTrail(GraphicsDevice graphicsDevice, System.Random? random = null)
        {
            _graphicsDevice = graphicsDevice;
            _particles = new List<Particle>();
            
            // Get shared particle texture
            _particleTexture = SharedTextureManager.GetPixelTexture(graphicsDevice);
            
            // Use provided Random or create fallback
            _random = random ?? new System.Random();
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

            // Add some randomness to position and velocity (use shared Random instance)
            if (_random == null) return; // Safety check
            
            float angleVariation = (float)(_random.NextDouble() - 0.5) * 0.5f; // ±0.25 radians
            float speedVariation = (float)(_random.NextDouble() * 0.3f + 0.7f); // 70-100% of base speed
            
            var particleDirection = new Vector2(
                backwardDirection.X * (float)Math.Cos(angleVariation) - backwardDirection.Y * (float)Math.Sin(angleVariation),
                backwardDirection.X * (float)Math.Sin(angleVariation) + backwardDirection.Y * (float)Math.Cos(angleVariation)
            );

            Vector2 particlePosition = emitPosition + new Vector2(
                (float)(_random.NextDouble() - 0.5) * 5f,
                (float)(_random.NextDouble() - 0.5) * 5f
            );
            Vector2 particleVelocity = particleDirection * speed * speedVariation;
            Color particleColor = new Color(
                (byte)(220 + _random.Next(35)), // Brighter orange-red range
                (byte)(120 + _random.Next(50)),
                (byte)(_random.Next(20)),
                (byte)255
            );
            float particleSize = (float)(_random.NextDouble() * 5f + 4f); // 4-9 pixels (larger)
            float particleLifetime = ParticleLifetime + (float)(_random.NextDouble() - 0.5) * 0.2f;
            
            var particle = ParticlePool.Get(particlePosition, particleVelocity, particleColor, particleSize, particleLifetime);
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
                    // Return particle to pool before removing
                    ParticlePool.Return(_particles[i]);
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
            // Return all particles to pool before clearing
            foreach (var particle in _particles)
            {
                ParticlePool.Return(particle);
            }
            _particles.Clear();
        }
    }
}

