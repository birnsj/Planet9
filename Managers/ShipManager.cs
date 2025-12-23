using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using Planet9.Core;
using Planet9.Entities;
using Planet9.Scenes;

namespace Planet9.Managers
{
    /// <summary>
    /// Manages all ships (player, friendly, enemy) in the game scene
    /// </summary>
    public class ShipManager
    {
        private readonly GraphicsDevice _graphicsDevice;
        private readonly ContentManager _content;
        private readonly Random _random;
        
        // Player ship
        public PlayerShip? PlayerShip { get; private set; }
        
        // Friendly ships
        public List<FriendlyShip> FriendlyShips { get; private set; } = new List<FriendlyShip>();
        public Dictionary<FriendlyShip, ShipState> FriendlyShipStates { get; private set; } = new Dictionary<FriendlyShip, ShipState>();
        
        // Enemy ships
        public List<EnemyShip> EnemyShips { get; private set; } = new List<EnemyShip>();
        public Dictionary<EnemyShip, EnemyShipState> EnemyShipStates { get; private set; } = new Dictionary<EnemyShip, EnemyShipState>();
        
        public ShipManager(GraphicsDevice graphicsDevice, ContentManager content, Random random)
        {
            _graphicsDevice = graphicsDevice;
            _content = content;
            _random = random;
        }
        
        /// <summary>
        /// Get or create ship state for a friendly ship
        /// </summary>
        public ShipState GetOrCreateShipState(FriendlyShip ship)
        {
            if (!FriendlyShipStates.TryGetValue(ship, out var state))
            {
                state = new ShipState();
                FriendlyShipStates[ship] = state;
            }
            return state;
        }
        
        /// <summary>
        /// Get or create ship state for an enemy ship
        /// </summary>
        public EnemyShipState GetOrCreateEnemyShipState(EnemyShip ship)
        {
            if (!EnemyShipStates.TryGetValue(ship, out var state))
            {
                state = new EnemyShipState();
                EnemyShipStates[ship] = state;
            }
            return state;
        }
        
        /// <summary>
        /// Initialize player ship
        /// </summary>
        public void InitializePlayerShip(Vector2 position, float health = 50f, float maxHealth = 50f, float damage = 10f)
        {
            PlayerShip = new PlayerShip(_graphicsDevice, _content);
            PlayerShip.Health = health;
            PlayerShip.MaxHealth = maxHealth;
            PlayerShip.Damage = damage;
            PlayerShip.Position = position;
        }
        
        /// <summary>
        /// Create a friendly ship at the specified position
        /// </summary>
        public FriendlyShip CreateFriendlyShip(Vector2 position, float moveSpeed, float rotationSpeed, 
            float inertia, float aimRotationSpeed, float drift, float avoidanceRange, 
            float lookAheadDistance, bool lookAheadVisible)
        {
            var friendlyShip = new FriendlyShip(_graphicsDevice, _content);
            friendlyShip.Position = position;
            friendlyShip.MoveSpeed = moveSpeed;
            friendlyShip.RotationSpeed = rotationSpeed;
            friendlyShip.Inertia = inertia;
            friendlyShip.AimRotationSpeed = aimRotationSpeed;
            friendlyShip.Drift = drift;
            friendlyShip.AvoidanceDetectionRange = avoidanceRange;
            friendlyShip.LookAheadDistance = lookAheadDistance;
            friendlyShip.LookAheadVisible = lookAheadVisible;
            
            FriendlyShips.Add(friendlyShip);
            
            // Initialize ship state
            var shipState = GetOrCreateShipState(friendlyShip);
            float initialAngle = (float)(_random.NextDouble() * MathHelper.TwoPi);
            shipState.LastDirection = new Vector2((float)Math.Cos(initialAngle), (float)Math.Sin(initialAngle));
            
            return friendlyShip;
        }
        
        /// <summary>
        /// Create an enemy ship at the specified position
        /// </summary>
        public EnemyShip CreateEnemyShip(Vector2 position, float health = 100f, float maxHealth = 100f, float damage = 5f)
        {
            var enemyShip = new EnemyShip(_graphicsDevice, _content);
            enemyShip.Position = position;
            enemyShip.MoveSpeed = 250f;
            enemyShip.RotationSpeed = 3f;
            enemyShip.Inertia = 0.9f;
            enemyShip.AimRotationSpeed = 3f;
            enemyShip.Drift = 0f;
            enemyShip.AvoidanceDetectionRange = 300f;
            enemyShip.LookAheadDistance = 1.5f;
            enemyShip.LookAheadVisible = false;
            enemyShip.Health = health;
            enemyShip.MaxHealth = maxHealth;
            enemyShip.Damage = damage;
            
            EnemyShips.Add(enemyShip);
            
            // Initialize enemy ship state
            var enemyState = GetOrCreateEnemyShipState(enemyShip);
            float initialAngle = (float)(_random.NextDouble() * MathHelper.TwoPi);
            enemyState.PatrolPoints.Clear();
            
            return enemyShip;
        }
        
        /// <summary>
        /// Remove a friendly ship
        /// </summary>
        public void RemoveFriendlyShip(FriendlyShip ship)
        {
            FriendlyShips.Remove(ship);
            FriendlyShipStates.Remove(ship);
        }
        
        /// <summary>
        /// Remove an enemy ship
        /// </summary>
        public void RemoveEnemyShip(EnemyShip ship)
        {
            EnemyShips.Remove(ship);
            EnemyShipStates.Remove(ship);
        }
        
        /// <summary>
        /// Update all ships
        /// </summary>
        public void Update(GameTime gameTime)
        {
            PlayerShip?.Update(gameTime);
            
            foreach (var friendlyShip in FriendlyShips)
            {
                friendlyShip.Update(gameTime);
            }
            
            foreach (var enemyShip in EnemyShips)
            {
                enemyShip.Update(gameTime);
            }
        }
        
        /// <summary>
        /// Draw all ships
        /// </summary>
        public void Draw(SpriteBatch spriteBatch)
        {
            PlayerShip?.Draw(spriteBatch);
            
            foreach (var friendlyShip in FriendlyShips)
            {
                friendlyShip.Draw(spriteBatch);
            }
            
            foreach (var enemyShip in EnemyShips)
            {
                enemyShip.Draw(spriteBatch);
            }
        }
    }
}

