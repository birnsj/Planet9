using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Planet9.Entities;

namespace Planet9.Core
{
    /// <summary>
    /// Object pool for Laser objects to reduce allocations
    /// </summary>
    public class LaserPool
    {
        private readonly Queue<Laser> _availableLasers;
        private readonly List<Laser> _activeLasers;
        private readonly GraphicsDevice _graphicsDevice;
        private const int InitialPoolSize = 50; // Pre-allocate 50 lasers
        private const int MaxPoolSize = 200; // Maximum pool size

        public LaserPool(GraphicsDevice graphicsDevice)
        {
            _graphicsDevice = graphicsDevice;
            _availableLasers = new Queue<Laser>();
            _activeLasers = new List<Laser>();
            
            // Pre-allocate initial pool
            for (int i = 0; i < InitialPoolSize; i++)
            {
                var laser = new Laser(Vector2.Zero, 0f, _graphicsDevice);
                laser.IsActive = false; // Start inactive
                _availableLasers.Enqueue(laser);
            }
        }

        /// <summary>
        /// Get a laser from the pool or create a new one if pool is empty
        /// </summary>
        public Laser Get(Vector2 startPosition, float direction, float damage, Entity? owner)
        {
            Laser laser;
            
            if (_availableLasers.Count > 0)
            {
                // Reuse from pool
                laser = _availableLasers.Dequeue();
            }
            else
            {
                // Pool exhausted, create new laser
                laser = new Laser(Vector2.Zero, 0f, _graphicsDevice);
            }
            
            // Reset and configure laser
            laser.Reset(startPosition, direction, damage, owner);
            laser.IsActive = true;
            
            _activeLasers.Add(laser);
            return laser;
        }

        /// <summary>
        /// Return a laser to the pool when it's no longer active
        /// </summary>
        public void Return(Laser laser)
        {
            if (laser == null) return;
            
            _activeLasers.Remove(laser);
            
            // Only return to pool if pool isn't too large
            if (_availableLasers.Count < MaxPoolSize)
            {
                laser.IsActive = false;
                _availableLasers.Enqueue(laser);
            }
            // If pool is full, let laser be garbage collected
        }

        /// <summary>
        /// Update all active lasers and return inactive ones to pool
        /// </summary>
        public void UpdateAndCleanup(Microsoft.Xna.Framework.GameTime gameTime)
        {
            // Update active lasers
            for (int i = _activeLasers.Count - 1; i >= 0; i--)
            {
                var laser = _activeLasers[i];
                laser.Update(gameTime);
                
                // Return inactive lasers to pool
                if (!laser.IsActive)
                {
                    Return(laser);
                }
            }
        }

        /// <summary>
        /// Get all active lasers for drawing
        /// </summary>
        public IEnumerable<Laser> GetActiveLasers()
        {
            return _activeLasers;
        }

        /// <summary>
        /// Clear all lasers from the pool
        /// </summary>
        public void Clear()
        {
            _activeLasers.Clear();
            
            // Clear pool but keep pre-allocated lasers
            while (_availableLasers.Count > 0)
            {
                var laser = _availableLasers.Dequeue();
                laser.IsActive = false;
            }
            
            // Re-add pre-allocated lasers
            for (int i = 0; i < InitialPoolSize && _availableLasers.Count < InitialPoolSize; i++)
            {
                var laser = new Laser(Vector2.Zero, 0f, _graphicsDevice);
                laser.IsActive = false;
                _availableLasers.Enqueue(laser);
            }
        }

        /// <summary>
        /// Get count of active lasers
        /// </summary>
        public int ActiveCount => _activeLasers.Count;

        /// <summary>
        /// Get count of available lasers in pool
        /// </summary>
        public int AvailableCount => _availableLasers.Count;
    }
}

