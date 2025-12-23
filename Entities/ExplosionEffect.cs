using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Planet9.Core;

namespace Planet9.Entities
{
    public class ExplosionEffect
    {
        private List<Particle> _particles;
        private GraphicsDevice _graphicsDevice;
        private Texture2D? _particleTexture;
        private System.Random? _random; // Shared Random instance
        private bool _hasExploded = false;

        public ExplosionEffect(GraphicsDevice graphicsDevice, System.Random? random = null)
        {
            _graphicsDevice = graphicsDevice;
            _particles = new List<Particle>();
            
            // Get shared particle texture
            _particleTexture = SharedTextureManager.GetPixelTexture(graphicsDevice);
            
            // Use provided Random or create fallback
            _random = random ?? new System.Random();
        }

        public bool IsActive => _particles.Count > 0;

        public void Explode(Vector2 position)
        {
            if (_hasExploded || _random == null) return; // Only explode once, safety check
            _hasExploded = true;
            
            // Create a massive burst of particles for the explosion
            int particleCount = 300; // Lots of particles for impressive explosion (doubled for bigger effect)
            
            for (int i = 0; i < particleCount; i++)
            {
                // Random angle in all directions
                float angle = (float)(_random.NextDouble() * MathHelper.TwoPi);
                
                // Random distance from center (explosion spreads outward)
                float distance = (float)(_random.NextDouble() * 80f + 20f); // 20-100 pixels (larger spread)
                
                // Random velocity (fast outward burst)
                float speed = (float)(_random.NextDouble() * 300f + 150f); // 150-450 pixels per second (faster)
                
                Vector2 velocity = new Vector2(
                    (float)Math.Cos(angle) * speed,
                    (float)Math.Sin(angle) * speed
                );
                
                // Random position around explosion center
                Vector2 particlePos = position + new Vector2(
                    (float)(Math.Cos(angle) * distance),
                    (float)(Math.Sin(angle) * distance)
                );
                
                // Color based on distance from center (core is white/yellow, outer is orange/red)
                Color particleColor;
                float distFromCenter = (float)_random.NextDouble();
                
                if (distFromCenter < 0.3f) // Core - white/yellow
                {
                    particleColor = new Color(
                        (byte)(255),
                        (byte)(240 + _random.Next(15)), // 240-255
                        (byte)(180 + _random.Next(50)), // 180-230
                        (byte)255
                    );
                }
                else if (distFromCenter < 0.6f) // Middle - bright orange
                {
                    particleColor = new Color(
                        (byte)(255),
                        (byte)(150 + _random.Next(50)), // 150-200
                        (byte)(_random.Next(30)), // 0-30
                        (byte)255
                    );
                }
                else // Outer - dark orange/red
                {
                    particleColor = new Color(
                        (byte)(200 + _random.Next(55)), // 200-255
                        (byte)(80 + _random.Next(50)), // 80-130
                        (byte)(_random.Next(20)), // 0-20
                        (byte)255
                    );
                }
                
                // Random size (larger in center, smaller at edges) - made bigger overall
                float size;
                if (distFromCenter < 0.3f)
                {
                    size = (float)(_random.NextDouble() * 20f + 15f); // 15-35 pixels (much larger core)
                }
                else if (distFromCenter < 0.6f)
                {
                    size = (float)(_random.NextDouble() * 12f + 8f); // 8-20 pixels (larger medium)
                }
                else
                {
                    size = (float)(_random.NextDouble() * 8f + 5f); // 5-13 pixels (larger outer)
                }
                
                // Random lifetime (longer for bigger explosion)
                float lifetime;
                if (distFromCenter < 0.3f)
                {
                    lifetime = 2.0f + (float)(_random.NextDouble() * 1.0f); // 2.0-3.0 seconds (longer)
                }
                else if (distFromCenter < 0.6f)
                {
                    lifetime = 1.5f + (float)(_random.NextDouble() * 0.8f); // 1.5-2.3 seconds (longer)
                }
                else
                {
                    lifetime = 0.8f + (float)(_random.NextDouble() * 0.7f); // 0.8-1.5 seconds (longer)
                }
                
                var particle = ParticlePool.Get(particlePos, velocity, particleColor, size, lifetime);
                _particles.Add(particle);
            }
        }

        public void Update(float deltaTime)
        {
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
            _hasExploded = false;
        }
    }
}

