using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Audio;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using Planet9.Core;
using Planet9.Entities;
using Planet9.Scenes;

namespace Planet9.Managers
{
    /// <summary>
    /// Manages combat-related systems: lasers, collisions, damage, explosions
    /// </summary>
    public class CombatManager
    {
        private readonly GraphicsDevice _graphicsDevice;
        private SoundEffect? _laserFireSound;
        private SoundEffect? _explosionSound;
        private float _sfxVolume = 1.0f;
        private bool _sfxEnabled = true;
        
        // Laser pool for efficient laser management
        private LaserPool? _laserPool;
        
        // Spatial grid for efficient collision detection
        private SpatialHashGrid? _spatialGrid;
        
        // Active explosions (explosions that continue after ships are destroyed)
        private List<ExplosionEffect> _activeExplosions = new List<ExplosionEffect>();
        
        // References to ship collections (for collision detection and ship removal)
        private List<FriendlyShip>? _friendlyShips;
        private List<EnemyShip>? _enemyShips;
        private Dictionary<FriendlyShip, ShipState>? _friendlyShipStates;
        private Dictionary<EnemyShip, EnemyShipState>? _enemyShipStates;
        
        // Callbacks for ship state management
        private Func<FriendlyShip, ShipState>? _getOrCreateShipState;
        private Func<EnemyShip, EnemyShipState>? _getOrCreateEnemyShipState;
        
        private const float MapSize = 8192f;
        private const float CollisionRadius = 64f; // Ship collision radius
        
        public LaserPool? LaserPool => _laserPool;
        public List<ExplosionEffect> ActiveExplosions => _activeExplosions;
        
        public CombatManager(GraphicsDevice graphicsDevice)
        {
            _graphicsDevice = graphicsDevice;
        }
        
        /// <summary>
        /// Initialize combat systems
        /// </summary>
        public void Initialize(float mapSize = 8192f)
        {
            _laserPool = new LaserPool(_graphicsDevice);
            _spatialGrid = new SpatialHashGrid(mapSize, 256f);
        }
        
        /// <summary>
        /// Set ship collections for collision detection
        /// </summary>
        public void SetShipCollections(
            List<FriendlyShip> friendlyShips,
            List<EnemyShip> enemyShips,
            Dictionary<FriendlyShip, ShipState> friendlyShipStates,
            Dictionary<EnemyShip, EnemyShipState> enemyShipStates,
            Func<FriendlyShip, ShipState> getOrCreateShipState,
            Func<EnemyShip, EnemyShipState> getOrCreateEnemyShipState)
        {
            _friendlyShips = friendlyShips;
            _enemyShips = enemyShips;
            _friendlyShipStates = friendlyShipStates;
            _enemyShipStates = enemyShipStates;
            _getOrCreateShipState = getOrCreateShipState;
            _getOrCreateEnemyShipState = getOrCreateEnemyShipState;
        }
        
        /// <summary>
        /// Load combat-related content
        /// </summary>
        public void LoadContent(ContentManager content, float sfxVolume, bool sfxEnabled)
        {
            _sfxVolume = sfxVolume;
            _sfxEnabled = sfxEnabled;
            
            try
            {
                _laserFireSound = content.Load<SoundEffect>("shipfire1");
            }
            catch (System.Exception ex)
            {
                System.Console.WriteLine($"[SOUND ERROR] Failed to load laser fire sound: {ex.Message}");
            }
            
            try
            {
                _explosionSound = content.Load<SoundEffect>("explosion1");
            }
            catch (System.Exception ex)
            {
                System.Console.WriteLine($"[SOUND ERROR] Failed to load explosion sound: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Set SFX volume and enabled state
        /// </summary>
        public void SetSFXSettings(float volume, bool enabled)
        {
            _sfxVolume = volume;
            _sfxEnabled = enabled;
        }
        
        /// <summary>
        /// Fire a laser from a ship using the laser pool
        /// </summary>
        public void FireLaser(Vector2 startPosition, float direction, float damage, Entity? owner = null)
        {
            if (_laserPool == null) return;
            
            var laser = _laserPool.Get(startPosition, direction, damage, owner);
            
            // Play laser fire sound effect
            if (_laserFireSound != null && _sfxEnabled && _sfxVolume > 0f)
            {
                try
                {
                    var laserSoundInstance = _laserFireSound.CreateInstance();
                    laserSoundInstance.Volume = _sfxVolume;
                    laserSoundInstance.Play();
                }
                catch (System.Exception ex)
                {
                    System.Console.WriteLine($"[SOUND ERROR] Failed to play laser fire sound: {ex.Message}");
                }
            }
        }
        
        /// <summary>
        /// Create an explosion effect and add it to active explosions
        /// </summary>
        public ExplosionEffect CreateExplosion(Vector2 position, System.Random? random = null)
        {
            var explosion = new ExplosionEffect(_graphicsDevice, random);
            explosion.Explode(position);
            _activeExplosions.Add(explosion);
            
            // Play explosion sound
            if (_explosionSound != null && _sfxEnabled && _sfxVolume > 0f)
            {
                try
                {
                    var explosionSoundInstance = _explosionSound.CreateInstance();
                    explosionSoundInstance.Volume = _sfxVolume;
                    explosionSoundInstance.Play();
                }
                catch (System.Exception ex)
                {
                    System.Console.WriteLine($"[SOUND ERROR] Failed to play explosion sound: {ex.Message}");
                }
            }
            
            return explosion;
        }
        
        /// <summary>
        /// Handle ship destruction - create explosion and remove ship
        /// </summary>
        private void HandleShipDestroyed(PlayerShip ship, System.Random? random = null)
        {
            ship.Health = 0f;
            var explosionEffect = ship.GetExplosionEffect();
            if (explosionEffect != null)
            {
                explosionEffect.Explode(ship.Position);
                _activeExplosions.Add(explosionEffect);
                
                // Play explosion sound
                if (_explosionSound != null && _sfxEnabled && _sfxVolume > 0f)
                {
                    try
                    {
                        var explosionSoundInstance = _explosionSound.CreateInstance();
                        explosionSoundInstance.Volume = _sfxVolume;
                        explosionSoundInstance.Play();
                    }
                    catch (System.Exception ex)
                    {
                        System.Console.WriteLine($"[SOUND ERROR] Failed to play explosion sound: {ex.Message}");
                    }
                }
            }
            ship.IsActive = false;
        }
        
        /// <summary>
        /// Update all combat entities and handle collisions
        /// </summary>
        public void Update(GameTime gameTime, PlayerShip? playerShip)
        {
            var deltaTime = (float)gameTime.ElapsedGameTime.TotalSeconds;
            
            // Update lasers using pool (handles cleanup automatically)
            _laserPool?.UpdateAndCleanup(gameTime);
            
            // Remove lasers that are off screen or too far away
            if (_laserPool != null)
            {
                foreach (var laser in _laserPool.GetActiveLasers())
                {
                    if (laser.Position.X < -1000 || laser.Position.X > 9192 ||
                        laser.Position.Y < -1000 || laser.Position.Y > 9192)
                    {
                        laser.IsActive = false; // Pool will clean this up
                    }
                }
            }
            
            // Update active explosions
            for (int i = _activeExplosions.Count - 1; i >= 0; i--)
            {
                _activeExplosions[i].Update(deltaTime);
                if (!_activeExplosions[i].IsActive)
                {
                    _activeExplosions.RemoveAt(i);
                }
            }
            
            // Handle laser-ship collisions if we have the necessary components
            if (_laserPool != null && _spatialGrid != null && playerShip != null)
            {
                HandleLaserShipCollisions(playerShip);
            }
        }
        
        /// <summary>
        /// Populate spatial grid with ships for collision detection
        /// </summary>
        public void PopulateSpatialGrid(PlayerShip? playerShip)
        {
            if (_spatialGrid == null) return;
            
            _spatialGrid.Clear();
            
            // Add player ship
            if (playerShip != null && playerShip.IsActive)
            {
                _spatialGrid.Add(playerShip);
            }
            
            // Add friendly ships
            if (_friendlyShips != null)
            {
                foreach (var friendlyShip in _friendlyShips)
                {
                    if (friendlyShip.IsActive)
                    {
                        _spatialGrid.Add(friendlyShip);
                    }
                }
            }
            
            // Add enemy ships
            if (_enemyShips != null)
            {
                foreach (var enemyShip in _enemyShips)
                {
                    if (enemyShip.IsActive)
                    {
                        _spatialGrid.Add(enemyShip);
                    }
                }
            }
        }
        
        /// <summary>
        /// Handle laser-ship collisions using spatial grid
        /// </summary>
        private void HandleLaserShipCollisions(PlayerShip playerShip)
        {
            if (_laserPool == null || _spatialGrid == null) return;
            if (_friendlyShips == null || _enemyShips == null) return;
            if (_friendlyShipStates == null || _enemyShipStates == null) return;
            if (_getOrCreateShipState == null || _getOrCreateEnemyShipState == null) return;
            
            foreach (var laser in _laserPool.GetActiveLasers())
            {
                if (!laser.IsActive) continue;
                
                // Use spatial grid to only check nearby ships (much faster than checking all ships)
                var nearbyShips = _spatialGrid.GetNearby(laser.Position, CollisionRadius);
                
                foreach (var ship in nearbyShips)
                {
                    // Skip if ship is the owner of the laser
                    if (laser.Owner == ship) continue;
                    
                    // Only process ships (PlayerShip and subclasses)
                    if (!(ship is PlayerShip playerShipTarget)) continue;
                    
                    // Check actual distance (spatial grid gives approximate, verify exact distance)
                    float distance = Vector2.Distance(laser.Position, ship.Position);
                    if (distance < CollisionRadius)
                    {
                        playerShipTarget.Health -= laser.Damage;
                        laser.IsActive = false; // Remove laser on hit
                        
                        // Handle player ship
                        if (playerShipTarget == playerShip)
                        {
                            if (playerShipTarget.Health <= 0f)
                            {
                                HandleShipDestroyed(playerShipTarget);
                                System.Console.WriteLine("[PLAYER] Player ship destroyed!");
                            }
                            else
                            {
                                System.Console.WriteLine($"[PLAYER] Health: {playerShip.Health:F1}/{playerShip.MaxHealth:F1}");
                            }
                        }
                        // Handle friendly ships
                        else if (playerShipTarget is FriendlyShip friendlyShip && !(playerShipTarget is EnemyShip))
                        {
                            // Only switch to Flee behavior when hit by player's laser (not enemy lasers)
                            if (friendlyShip.Health > 0f && laser.Owner == playerShip)
                            {
                                var shipState = _getOrCreateShipState(friendlyShip);
                                shipState.Behavior = FriendlyShipBehavior.Flee;
                                shipState.BehaviorTimer = 10.0f;
                                friendlyShip.IsIdle = false;
                                friendlyShip.IsFleeing = true;
                                System.Console.WriteLine($"[FRIENDLY] Hit by player! Health: {friendlyShip.Health:F1}/{friendlyShip.MaxHealth:F1} - Switching to Flee behavior");
                            }
                            
                            if (friendlyShip.Health <= 0f)
                            {
                                HandleShipDestroyed(friendlyShip);
                                
                                // Remove from list
                                int index = _friendlyShips.IndexOf(friendlyShip);
                                if (index >= 0)
                                {
                                    _friendlyShips.RemoveAt(index);
                                }
                                _friendlyShipStates.Remove(friendlyShip);
                                System.Console.WriteLine("[FRIENDLY] Friendly ship destroyed!");
                            }
                        }
                        // Handle enemy ships
                        else if (playerShipTarget is EnemyShip enemyShip)
                        {
                            // Switch to Flee behavior when health <= 10 (50% of max health for 20 HP enemies)
                            if (enemyShip.Health <= 10f && enemyShip.Health > 0f)
                            {
                                var enemyState = _getOrCreateEnemyShipState(enemyShip);
                                enemyState.Behavior = FriendlyShipBehavior.Flee;
                                enemyState.BehaviorTimer = 10.0f;
                                enemyShip.IsFleeing = true;
                                System.Console.WriteLine($"[ENEMY] Low health! Health: {enemyShip.Health:F1}/{enemyShip.MaxHealth:F1} - Switching to Flee behavior");
                            }
                            
                            if (enemyShip.Health <= 0f)
                            {
                                HandleShipDestroyed(enemyShip);
                                
                                // Remove from list
                                int index = _enemyShips.IndexOf(enemyShip);
                                if (index >= 0)
                                {
                                    _enemyShips.RemoveAt(index);
                                }
                                _enemyShipStates.Remove(enemyShip);
                                System.Console.WriteLine("[ENEMY] Enemy ship destroyed!");
                            }
                            else
                            {
                                System.Console.WriteLine($"[ENEMY] Health: {enemyShip.Health:F1}/{enemyShip.MaxHealth:F1}");
                            }
                        }
                        
                        // Laser hit a ship, break to next laser
                        break;
                    }
                }
            }
        }
        
        /// <summary>
        /// Draw all lasers (should be called with additive blending)
        /// </summary>
        public void DrawLasers(SpriteBatch spriteBatch)
        {
            if (_laserPool != null)
            {
                foreach (var laser in _laserPool.GetActiveLasers())
                {
                    laser.Draw(spriteBatch);
                }
            }
        }
        
        /// <summary>
        /// Draw all explosions (should be called with normal blending)
        /// </summary>
        public void DrawExplosions(SpriteBatch spriteBatch)
        {
            foreach (var explosion in _activeExplosions)
            {
                explosion.Draw(spriteBatch);
            }
        }
        
        /// <summary>
        /// Clear all combat entities
        /// </summary>
        public void Clear()
        {
            _laserPool?.Clear();
            _activeExplosions.Clear();
            _spatialGrid?.Clear();
        }
    }
}
