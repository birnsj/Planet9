using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Planet9.Entities;

namespace Planet9.Core
{
    /// <summary>
    /// Static object pool for Particle objects to reduce allocations
    /// Shared across all particle effects (engine trails, damage, explosions)
    /// </summary>
    public static class ParticlePool
    {
        private static readonly Queue<Particle> _availableParticles = new Queue<Particle>();
        private const int InitialPoolSize = 500; // Pre-allocate 500 particles (high volume)
        private const int MaxPoolSize = 2000; // Maximum pool size
        private static bool _initialized = false;
        private static readonly object _lock = new object();

        /// <summary>
        /// Initialize the particle pool (call once at game startup)
        /// </summary>
        public static void Initialize()
        {
            lock (_lock)
            {
                if (_initialized) return;
                
                // Pre-allocate initial pool
                for (int i = 0; i < InitialPoolSize; i++)
                {
                    _availableParticles.Enqueue(new Particle());
                }
                
                _initialized = true;
            }
        }

        /// <summary>
        /// Get a particle from the pool or create a new one if pool is empty
        /// </summary>
        public static Particle Get(Vector2 position, Vector2 velocity, Color color, float size, float lifetime)
        {
            lock (_lock)
            {
                Particle particle;
                
                if (_availableParticles.Count > 0)
                {
                    // Reuse from pool
                    particle = _availableParticles.Dequeue();
                }
                else
                {
                    // Pool exhausted, create new particle
                    particle = new Particle();
                }
                
                // Reset and configure particle
                particle.Reset(position, velocity, color, size, lifetime);
                return particle;
            }
        }

        /// <summary>
        /// Return a particle to the pool when it's no longer alive
        /// </summary>
        public static void Return(Particle particle)
        {
            if (particle == null) return;
            
            lock (_lock)
            {
                // Only return to pool if pool isn't too large
                if (_availableParticles.Count < MaxPoolSize)
                {
                    _availableParticles.Enqueue(particle);
                }
                // If pool is full, let particle be garbage collected
            }
        }

        /// <summary>
        /// Get count of available particles in pool
        /// </summary>
        public static int AvailableCount
        {
            get
            {
                lock (_lock)
                {
                    return _availableParticles.Count;
                }
            }
        }
    }
}

