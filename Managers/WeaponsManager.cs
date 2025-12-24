using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Planet9.Entities;

namespace Planet9.Managers
{
    /// <summary>
    /// Manages weapon firing logic, spawn positions, and weapon configurations
    /// </summary>
    public class WeaponsManager
    {
        private CombatManager? _combatManager;

        // Player weapon configuration
        // Player ship fires from two hardpoints on the sprite
        private static readonly Vector2[] PlayerWeaponHardpoints = new Vector2[]
        {
            new Vector2(210f, 50f),  // Right weapon hardpoint
            new Vector2(40f, 50f)    // Left weapon hardpoint
        };

        // Enemy weapon configuration
        private const float EnemyWeaponOffsetY = 20f; // Offset from center toward front of ship

        public WeaponsManager()
        {
        }

        /// <summary>
        /// Initialize the weapons manager with combat manager reference
        /// </summary>
        public void Initialize(CombatManager combatManager)
        {
            _combatManager = combatManager;
        }

        /// <summary>
        /// Fire player ship weapons at a target position (e.g., cursor position)
        /// </summary>
        public void FirePlayerWeapon(PlayerShip playerShip, Vector2 targetPosition)
        {
            if (playerShip == null || _combatManager == null) return;

            var shipTexture = playerShip.GetTexture();
            if (shipTexture == null) return;

            // Calculate direction to target
            var directionToTarget = targetPosition - playerShip.Position;
            if (directionToTarget.LengthSquared() <= 0.1f) return;

            directionToTarget.Normalize();
            float laserDirection = (float)Math.Atan2(directionToTarget.Y, directionToTarget.X) + MathHelper.PiOver2;

            // Fire from each hardpoint
            foreach (var hardpoint in PlayerWeaponHardpoints)
            {
                Vector2 spawnPosition = CalculateWeaponSpawnPosition(
                    playerShip.Position,
                    playerShip.Rotation,
                    shipTexture.Width,
                    shipTexture.Height,
                    hardpoint.X,
                    hardpoint.Y
                );

                _combatManager.FireLaser(spawnPosition, laserDirection, playerShip.Damage, playerShip);
            }
        }

        /// <summary>
        /// Fire enemy ship weapon in the direction the ship is facing
        /// </summary>
        public void FireEnemyWeapon(EnemyShip enemyShip)
        {
            if (enemyShip == null || _combatManager == null) return;

            var shipTexture = enemyShip.GetTexture();
            if (shipTexture == null) return;

            float textureCenterX = shipTexture.Width / 2f;
            float shipRotation = enemyShip.Rotation;

            // Fire from front center of ship
            Vector2 spawnPosition = CalculateWeaponSpawnPosition(
                enemyShip.Position,
                shipRotation,
                shipTexture.Width,
                shipTexture.Height,
                textureCenterX,
                EnemyWeaponOffsetY
            );

            _combatManager.FireLaser(spawnPosition, shipRotation, enemyShip.Damage, enemyShip);
        }

        /// <summary>
        /// Calculate weapon spawn position from sprite coordinates
        /// Converts sprite-space coordinates to world-space position accounting for ship rotation
        /// </summary>
        /// <param name="shipPosition">World position of the ship center</param>
        /// <param name="shipRotation">Rotation of the ship in radians</param>
        /// <param name="textureWidth">Width of the ship texture</param>
        /// <param name="textureHeight">Height of the ship texture</param>
        /// <param name="spriteX">X coordinate on the sprite (sprite space)</param>
        /// <param name="spriteY">Y coordinate on the sprite (sprite space)</param>
        /// <returns>World-space position where the weapon should spawn</returns>
        private Vector2 CalculateWeaponSpawnPosition(
            Vector2 shipPosition,
            float shipRotation,
            float textureWidth,
            float textureHeight,
            float spriteX,
            float spriteY)
        {
            // Calculate sprite center
            float textureCenterX = textureWidth / 2f;
            float textureCenterY = textureHeight / 2f;

            // Convert sprite coordinates to offset from ship center
            float offsetX = spriteX - textureCenterX;
            float offsetY = spriteY - textureCenterY;

            // Rotate the offset by ship's rotation to get world-space offset
            float cos = (float)Math.Cos(shipRotation);
            float sin = (float)Math.Sin(shipRotation);
            float rotatedX = offsetX * cos - offsetY * sin;
            float rotatedY = offsetX * sin + offsetY * cos;

            // Calculate final spawn position
            return shipPosition + new Vector2(rotatedX, rotatedY);
        }
    }
}

