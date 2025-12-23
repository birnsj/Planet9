using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Planet9.Core;
using Planet9.Entities;

namespace Planet9.Managers
{
    /// <summary>
    /// Manages ship-to-ship collision detection and resolution
    /// </summary>
    public class CollisionManager
    {
        private const float MapSize = 8192f;
        private const float ShipMargin = 30f; // Keep ships at least 30 pixels from edges
        
        /// <summary>
        /// Handle collisions between friendly ships
        /// </summary>
        public void HandleFriendlyShipCollisions(FriendlyShip friendlyShip, List<FriendlyShip> allFriendlyShips)
        {
            float shipAvoidanceRadius = friendlyShip.AvoidanceDetectionRange;
            
            foreach (var otherShip in allFriendlyShips)
            {
                if (otherShip == friendlyShip) continue;
                
                Vector2 direction = friendlyShip.Position - otherShip.Position;
                float distance = direction.Length();
                
                // Use the larger of the two ships' avoidance ranges for minimum safe distance
                float otherAvoidanceRadius = otherShip.AvoidanceDetectionRange;
                float minSafeDistance = MathHelper.Max(shipAvoidanceRadius, otherAvoidanceRadius);
                
                // Check if ships are too close (within each other's avoidance radius)
                if (distance < minSafeDistance && distance > 0.1f)
                {
                    // Calculate how far ships need to be pushed apart
                    float overlap = minSafeDistance - distance;
                    
                    // Normalize direction
                    direction.Normalize();
                    
                    // Push ships apart (both ships move half the overlap distance)
                    float pushDistance = overlap * 0.5f;
                    friendlyShip.Position += direction * pushDistance;
                    otherShip.Position -= direction * pushDistance;
                    
                    // Stop ships if they're too close (prevent them from continuing into each other)
                    if (distance < minSafeDistance * 0.8f)
                    {
                        // Stop both ships to prevent further collision
                        friendlyShip.StopMoving();
                        otherShip.StopMoving();
                    }
                }
            }
        }
        
        /// <summary>
        /// Handle collision between a friendly ship and the player ship
        /// </summary>
        public void HandleFriendlyPlayerCollision(FriendlyShip friendlyShip, PlayerShip? playerShip)
        {
            if (playerShip == null) return;
            
            float shipAvoidanceRadius = friendlyShip.AvoidanceDetectionRange;
            Vector2 direction = friendlyShip.Position - playerShip.Position;
            float distance = direction.Length();
            
            // Use the larger of the two ships' avoidance ranges for minimum safe distance
            float playerAvoidanceRadiusForCollision = playerShip.AvoidanceDetectionRange;
            float minSafeDistance = MathHelper.Max(shipAvoidanceRadius, playerAvoidanceRadiusForCollision);
            
            if (distance < minSafeDistance && distance > 0.1f)
            {
                float overlap = minSafeDistance - distance;
                direction.Normalize();
                
                // Push friendly ship away from player (player doesn't move)
                float pushDistance = overlap;
                friendlyShip.Position += direction * pushDistance;
                
                // Stop friendly ship if too close
                if (distance < minSafeDistance * 0.8f)
                {
                    friendlyShip.StopMoving();
                }
            }
        }
        
        /// <summary>
        /// Handle collisions between enemy ships
        /// </summary>
        public void HandleEnemyShipCollisions(EnemyShip enemyShip, List<EnemyShip> allEnemyShips)
        {
            float shipAvoidanceRadius = enemyShip.AvoidanceDetectionRange;
            
            foreach (var otherEnemyShip in allEnemyShips)
            {
                if (otherEnemyShip == enemyShip) continue;
                
                Vector2 direction = enemyShip.Position - otherEnemyShip.Position;
                float distance = direction.Length();
                
                // Use the larger of the two ships' avoidance ranges for minimum safe distance
                float otherAvoidanceRadius = otherEnemyShip.AvoidanceDetectionRange;
                float minSafeDistance = MathHelper.Max(shipAvoidanceRadius, otherAvoidanceRadius);
                
                if (distance < minSafeDistance && distance > 0.1f)
                {
                    float overlap = minSafeDistance - distance;
                    direction.Normalize();
                    float pushDistance = overlap * 0.5f;
                    enemyShip.Position += direction * pushDistance;
                    otherEnemyShip.Position -= direction * pushDistance;
                    
                    if (distance < minSafeDistance * 0.8f)
                    {
                        enemyShip.StopMoving();
                        otherEnemyShip.StopMoving();
                    }
                }
            }
        }
        
        /// <summary>
        /// Handle collision between an enemy ship and the player ship
        /// </summary>
        public void HandleEnemyPlayerCollision(EnemyShip enemyShip, PlayerShip? playerShip)
        {
            if (playerShip == null) return;
            
            float shipAvoidanceRadius = enemyShip.AvoidanceDetectionRange;
            Vector2 direction = enemyShip.Position - playerShip.Position;
            float distance = direction.Length();
            
            // Use the larger of the two ships' avoidance ranges for minimum safe distance
            float playerAvoidanceRadiusForCollision = playerShip.AvoidanceDetectionRange;
            float minSafeDistance = MathHelper.Max(shipAvoidanceRadius, playerAvoidanceRadiusForCollision);
            
            if (distance < minSafeDistance && distance > 0.1f)
            {
                float overlap = minSafeDistance - distance;
                direction.Normalize();
                float pushDistance = overlap * 1.5f;
                enemyShip.Position += direction * pushDistance;
                
                // Don't stop moving - instead, set a target away from player to back away
                if (distance < minSafeDistance * 0.9f)
                {
                    // Set target position away from player to actively back away
                    float backAwayDistance = minSafeDistance * 1.5f;
                    Vector2 backAwayTarget = enemyShip.Position + direction * backAwayDistance;
                    enemyShip.SetTargetPosition(backAwayTarget);
                }
            }
        }
        
        /// <summary>
        /// Handle collision between an enemy ship and friendly ships
        /// </summary>
        public void HandleEnemyFriendlyCollision(EnemyShip enemyShip, List<FriendlyShip> friendlyShips)
        {
            float shipAvoidanceRadius = enemyShip.AvoidanceDetectionRange;
            
            foreach (var friendlyShip in friendlyShips)
            {
                Vector2 direction = enemyShip.Position - friendlyShip.Position;
                float distance = direction.Length();
                
                // Use the larger of the two ships' avoidance ranges for minimum safe distance
                float otherAvoidanceRadius = friendlyShip.AvoidanceDetectionRange;
                float minSafeDistance = MathHelper.Max(shipAvoidanceRadius, otherAvoidanceRadius);
                
                if (distance < minSafeDistance && distance > 0.1f)
                {
                    float overlap = minSafeDistance - distance;
                    direction.Normalize();
                    float pushDistance = overlap * 0.5f;
                    enemyShip.Position += direction * pushDistance;
                    friendlyShip.Position -= direction * pushDistance;
                    
                    if (distance < minSafeDistance * 0.8f)
                    {
                        enemyShip.StopMoving();
                        friendlyShip.StopMoving();
                    }
                }
            }
        }
        
        /// <summary>
        /// Clamp ship position to map bounds
        /// </summary>
        public void ClampShipToMapBounds(PlayerShip ship)
        {
            float clampedX = MathHelper.Clamp(ship.Position.X, ShipMargin, MapSize - ShipMargin);
            float clampedY = MathHelper.Clamp(ship.Position.Y, ShipMargin, MapSize - ShipMargin);
            ship.Position = new Vector2(clampedX, clampedY);
        }
        
        /// <summary>
        /// Clamp friendly ship position to map bounds
        /// </summary>
        public void ClampFriendlyShipToMapBounds(FriendlyShip ship)
        {
            float clampedX = MathHelper.Clamp(ship.Position.X, ShipMargin, MapSize - ShipMargin);
            float clampedY = MathHelper.Clamp(ship.Position.Y, ShipMargin, MapSize - ShipMargin);
            ship.Position = new Vector2(clampedX, clampedY);
        }
        
        /// <summary>
        /// Clamp enemy ship position to map bounds
        /// </summary>
        public void ClampEnemyShipToMapBounds(EnemyShip ship)
        {
            float clampedX = MathHelper.Clamp(ship.Position.X, ShipMargin, MapSize - ShipMargin);
            float clampedY = MathHelper.Clamp(ship.Position.Y, ShipMargin, MapSize - ShipMargin);
            ship.Position = new Vector2(clampedX, clampedY);
        }
    }
}

