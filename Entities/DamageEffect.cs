using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Planet9.Core;

namespace Planet9.Entities
{
    public class DamageEffect
    {
        private List<Particle> _particles;
        private GraphicsDevice _graphicsDevice;
        private Texture2D? _particleTexture;
        private const float ParticleLifetime = 2.5f; // Seconds (longer lifetime for more visibility)
        private const float EmissionRate = 80f; // Particles per second (more particles for better visibility)
        private float _timeSinceLastEmission = 0f;
        private const float EmissionInterval = 1f / EmissionRate;
        private bool _isActive = false;
        private System.Random? _random; // Shared Random instance

        public DamageEffect(GraphicsDevice graphicsDevice, System.Random? random = null)
        {
            _graphicsDevice = graphicsDevice;
            _particles = new List<Particle>();
            
            // Get shared particle texture
            _particleTexture = SharedTextureManager.GetPixelTexture(graphicsDevice);
            
            // Use provided Random or create fallback
            _random = random ?? new System.Random();
        }

        public void SetActive(bool active)
        {
            _isActive = active;
        }

        public bool IsActive => _isActive;

        private void Emit(Vector2 position, float rotation)
        {
            if (_random == null) return; // Safety check
            
            // Emit smoke particles from random positions around the ship
            float angleVariation = (float)(_random.NextDouble() * MathHelper.TwoPi);
            float distanceVariation = (float)(_random.NextDouble() * 60f + 30f); // 30-90 pixels from center (larger area, more spread)
            
            Vector2 emitOffset = new Vector2(
                (float)Math.Cos(angleVariation) * distanceVariation,
                (float)Math.Sin(angleVariation) * distanceVariation
            );
            
            Vector2 emitPosition = position + emitOffset;
            
            // Random velocity direction (upward and outward)
            float velocityAngle = angleVariation + (float)(_random.NextDouble() - 0.5) * 0.5f;
            float velocitySpeed = (float)(_random.NextDouble() * 80f + 40f); // 40-120 pixels per second (faster, more noticeable)
            
            Vector2 velocity = new Vector2(
                (float)Math.Cos(velocityAngle) * velocitySpeed,
                (float)Math.Sin(velocityAngle) * velocitySpeed
            );
            
            // Random size and color (lighter gray to white for smoke - more noticeable)
            byte grayValue = (byte)(_random.NextDouble() * 120f + 100f); // 100-220 (lighter gray/white, much more visible)
            Color particleColor = new Color(
                grayValue,
                grayValue,
                grayValue,
                (byte)255
            );
            
            float particleSize = (float)(_random.NextDouble() * 10f + 8f); // 8-18 pixels (larger, more noticeable)
            float particleLifetime = ParticleLifetime + (float)(_random.NextDouble() - 0.5) * 0.3f;
            
            var particle = ParticlePool.Get(emitPosition, velocity, particleColor, particleSize, particleLifetime);
            _particles.Add(particle);
            
            // More frequently emit a spark (bright orange/yellow) for better visibility
            if (_random.NextDouble() < 0.5f) // 50% chance for spark (more sparks, more noticeable)
            {
                float sparkAngle = angleVariation + (float)(_random.NextDouble() - 0.5) * 1.0f;
                float sparkSpeed = (float)(_random.NextDouble() * 80f + 50f); // 50-130 pixels per second (faster)
                
                Vector2 sparkVelocity = new Vector2(
                    (float)Math.Cos(sparkAngle) * sparkSpeed,
                    (float)Math.Sin(sparkAngle) * sparkSpeed
                );
                
                // Very bright orange/yellow spark (more saturated)
                Color sparkColor = new Color(
                    (byte)(240 + _random.Next(15)), // 240-255 (very bright)
                    (byte)(180 + _random.Next(40)), // 180-220 (bright yellow)
                    (byte)(_random.Next(20)), // 0-20
                    (byte)255
                );
                
                float sparkSize = (float)(_random.NextDouble() * 8f + 6f); // 6-14 pixels (larger, much more visible)
                float sparkLifetime = 1.0f + (float)(_random.NextDouble() - 0.5) * 0.4f; // Longer lifetime
                
                var spark = ParticlePool.Get(emitPosition, sparkVelocity, sparkColor, sparkSize, sparkLifetime);
                _particles.Add(spark);
            }
        }

        public void Update(float deltaTime, Vector2 position, float rotation)
        {
            // Emit particles when active
            if (_isActive)
            {
                _timeSinceLastEmission += deltaTime;
                
                int particlesEmitted = 0;
                while (_timeSinceLastEmission >= EmissionInterval)
                {
                    Emit(position, rotation);
                    _timeSinceLastEmission -= EmissionInterval;
                    particlesEmitted++;
                }
                
                // Debug output (only occasionally to avoid spam)
                if (System.Environment.TickCount % 2000 < 16 && particlesEmitted > 0)
                {
                    System.Console.WriteLine($"[DAMAGE FX] Emitted {particlesEmitted} particles at position ({position.X:F1}, {position.Y:F1})");
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

            // Draw particles
            int particlesDrawn = 0;
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
                    particlesDrawn++;
                }
            }
            
            // Debug output (only occasionally to avoid spam)
            if (System.Environment.TickCount % 2000 < 16 && _isActive && particlesDrawn > 0)
            {
                System.Console.WriteLine($"[DAMAGE FX] Active: {_isActive}, Particles: {_particles.Count}, Drawn: {particlesDrawn}");
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

