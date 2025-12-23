using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Planet9.Core;
using Planet9.Entities;
using Core = Planet9.Core;

namespace Planet9.Managers
{
    /// <summary>
    /// Manages rendering of debug/overlay elements like minimap, paths, health bars, etc.
    /// </summary>
    public class RenderingManager
    {
        private GraphicsDevice _graphicsDevice;
        private Texture2D? _gridPixelTexture;
        private Texture2D? _pixelTexture;
        
        // Minimap textures
        private Texture2D? _minimapBackgroundTexture;
        private Texture2D? _minimapPlayerDotTexture;
        private Texture2D? _minimapFriendlyDotTexture;
        private Texture2D? _minimapEnemyDotTexture;
        private Texture2D? _minimapViewportOutlineTexture;
        
        // Galaxy textures for minimap
        private Texture2D? _galaxyTexture;
        private Texture2D? _galaxyOverlayTexture;
        
        private const float MapSize = 8192f;
        private const int MinimapSize = 200;
        
        public RenderingManager(GraphicsDevice graphicsDevice)
        {
            _graphicsDevice = graphicsDevice;
        }
        
        /// <summary>
        /// Initialize textures required for rendering
        /// </summary>
        public void Initialize(
            Texture2D gridPixelTexture,
            Texture2D? minimapBackgroundTexture,
            Texture2D? minimapPlayerDotTexture,
            Texture2D? minimapFriendlyDotTexture,
            Texture2D? minimapEnemyDotTexture,
            Texture2D? minimapViewportOutlineTexture,
            Texture2D? galaxyTexture,
            Texture2D? galaxyOverlayTexture)
        {
            _gridPixelTexture = gridPixelTexture;
            _minimapBackgroundTexture = minimapBackgroundTexture;
            _minimapPlayerDotTexture = minimapPlayerDotTexture;
            _minimapFriendlyDotTexture = minimapFriendlyDotTexture;
            _minimapEnemyDotTexture = minimapEnemyDotTexture;
            _minimapViewportOutlineTexture = minimapViewportOutlineTexture;
            _galaxyTexture = galaxyTexture;
            _galaxyOverlayTexture = galaxyOverlayTexture;
        }
        
        /// <summary>
        /// Draw a circle using line segments
        /// </summary>
        public void DrawCircle(SpriteBatch spriteBatch, Vector2 center, float radius, Color color)
        {
            if (_gridPixelTexture == null) return;
            
            const int segments = 64; // Number of line segments to approximate circle
            float angleStep = MathHelper.TwoPi / segments;
            
            for (int i = 0; i < segments; i++)
            {
                float angle1 = i * angleStep;
                float angle2 = (i + 1) * angleStep;
                
                Vector2 point1 = center + new Vector2(
                    (float)Math.Cos(angle1) * radius,
                    (float)Math.Sin(angle1) * radius
                );
                Vector2 point2 = center + new Vector2(
                    (float)Math.Cos(angle2) * radius,
                    (float)Math.Sin(angle2) * radius
                );
                
                Vector2 direction = point2 - point1;
                float length = direction.Length();
                if (length > 0.1f)
                {
                    float rotation = (float)Math.Atan2(direction.Y, direction.X);
                    spriteBatch.Draw(
                        _gridPixelTexture,
                        point1,
                        null,
                        color,
                        rotation,
                        new Vector2(0, 0.5f),
                        new Vector2(length, 2f),
                        SpriteEffects.None,
                        0f
                    );
                }
            }
        }
        
        /// <summary>
        /// Create or get pixel texture for health bars
        /// </summary>
        private Texture2D GetPixelTexture()
        {
            if (_pixelTexture == null)
            {
                _pixelTexture = new Texture2D(_graphicsDevice, 1, 1);
                _pixelTexture.SetData(new[] { Color.White });
            }
            return _pixelTexture;
        }
        
        /// <summary>
        /// Draw health bar for a ship
        /// </summary>
        public void DrawHealthBarForShip(SpriteBatch spriteBatch, Matrix transform, PlayerShip ship, Color barColor)
        {
            if (ship.MaxHealth <= 0f) return; // Avoid division by zero
            Texture2D pixelTexture = GetPixelTexture();
            
            // Calculate health percentage
            float healthPercent = MathHelper.Clamp(ship.Health / ship.MaxHealth, 0f, 1f);
            
            // Health bar dimensions (in screen space pixels)
            const float barWidth = 60f;
            const float barHeight = 6f;
            const float barOffsetY = -180f; // Position above ship (negative Y = up, increased to move bars higher)
            
            // Calculate bar position in world space (above ship)
            Vector2 barWorldPosition = ship.Position + new Vector2(0, barOffsetY);
            
            // Transform world position to screen position
            Vector2 barScreenPosition = Vector2.Transform(barWorldPosition, transform);
            
            // Draw background (dark gray) - draw in screen space
            Color backgroundColor = new Color(50, 50, 50, 200); // Dark gray with transparency
            spriteBatch.Draw(
                pixelTexture,
                new Rectangle((int)barScreenPosition.X - (int)(barWidth / 2), (int)barScreenPosition.Y - (int)(barHeight / 2),
                    (int)barWidth, (int)barHeight),
                backgroundColor
            );
            
            // Draw health bar (colored based on health percentage)
            Color healthColor = barColor;
            if (healthPercent < 0.3f) // Low health - red tint
            {
                healthColor = Color.Red;
            }
            else if (healthPercent < 0.6f) // Medium health - yellow tint
            {
                healthColor = Color.Orange;
            }
            
            float healthBarWidth = barWidth * healthPercent;
            if (healthBarWidth > 0f)
            {
                spriteBatch.Draw(
                    pixelTexture,
                    new Rectangle((int)barScreenPosition.X - (int)(barWidth / 2), (int)barScreenPosition.Y - (int)(barHeight / 2),
                        (int)healthBarWidth, (int)barHeight),
                    healthColor
                );
            }
            
            // Draw border (1 pixel thick lines in screen space)
            Color borderColor = new Color(200, 200, 200, 255); // Light gray border
            const int borderThickness = 1;
            int barX = (int)barScreenPosition.X - (int)(barWidth / 2);
            int barY = (int)barScreenPosition.Y - (int)(barHeight / 2);
            
            // Top border
            spriteBatch.Draw(
                pixelTexture,
                new Rectangle(barX, barY - borderThickness, (int)barWidth, borderThickness),
                borderColor
            );
            
            // Bottom border
            spriteBatch.Draw(
                pixelTexture,
                new Rectangle(barX, barY + (int)barHeight, (int)barWidth, borderThickness),
                borderColor
            );
            
            // Left border
            spriteBatch.Draw(
                pixelTexture,
                new Rectangle(barX - borderThickness, barY, borderThickness, (int)barHeight),
                borderColor
            );
            
            // Right border
            spriteBatch.Draw(
                pixelTexture,
                new Rectangle(barX + (int)barWidth, barY, borderThickness, (int)barHeight),
                borderColor
            );
        }
        
        /// <summary>
        /// Draw health bars for all ships
        /// </summary>
        public void DrawHealthBars(
            SpriteBatch spriteBatch, 
            Matrix transform,
            PlayerShip? playerShip,
            List<FriendlyShip> friendlyShips,
            List<EnemyShip> enemyShips)
        {
            // Start a new sprite batch for screen-space drawing
            spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend);
            
            // Draw health bar for player ship
            if (playerShip != null && playerShip.IsActive)
            {
                DrawHealthBarForShip(spriteBatch, transform, playerShip, Color.Green);
            }
            
            // Draw health bars for friendly ships
            foreach (var friendlyShip in friendlyShips)
            {
                if (friendlyShip.IsActive)
                {
                    DrawHealthBarForShip(spriteBatch, transform, friendlyShip, Color.Cyan);
                }
            }
            
            // Draw health bars for enemy ships
            foreach (var enemyShip in enemyShips)
            {
                if (enemyShip.IsActive)
                {
                    DrawHealthBarForShip(spriteBatch, transform, enemyShip, Color.Red);
                }
            }
            
            spriteBatch.End();
        }
        
        /// <summary>
        /// Draw avoidance range circles for ships
        /// </summary>
        public void DrawAvoidanceRange(
            SpriteBatch spriteBatch,
            PlayerShip? playerShip,
            List<FriendlyShip> friendlyShips)
        {
            if (_gridPixelTexture == null) return;
            
            // Draw avoidance range circle for player ship (use player's own range)
            if (playerShip != null)
            {
                float playerRadius = playerShip.AvoidanceDetectionRange * 1.33f; // Player avoidance radius (33% larger)
                DrawCircle(spriteBatch, playerShip.Position, playerRadius, new Color(255, 100, 100, 100)); // Red tint for player
            }
            
            // Draw avoidance range circles for friendly ships (use each ship's own range)
            foreach (var friendlyShip in friendlyShips)
            {
                float radius = friendlyShip.AvoidanceDetectionRange; // Use ship's own avoidance range
                DrawCircle(spriteBatch, friendlyShip.Position, radius, new Color(100, 255, 100, 100)); // Green tint for friendly
            }
        }
        
        /// <summary>
        /// Draw enemy paths (ship movement history)
        /// </summary>
        public void DrawEnemyPaths(
            SpriteBatch spriteBatch,
            List<FriendlyShip> friendlyShips,
            Dictionary<FriendlyShip, Core.ShipState> friendlyShipStates,
            Func<FriendlyShip, Core.ShipState> getOrCreateShipState)
        {
            if (_gridPixelTexture == null) return;
            
            foreach (var friendlyShip in friendlyShips)
            {
                var shipState = getOrCreateShipState(friendlyShip);
                if (shipState.Path.Count < 2)
                    continue;
                
                var path = shipState.Path;
                Color pathColor = new Color(255, 100, 100, 150); // Semi-transparent red for ship paths
                
                // Draw path as connected lines
                for (int i = 0; i < path.Count - 1; i++)
                {
                    Vector2 start = path[i];
                    Vector2 end = path[i + 1];
                    
                    Vector2 direction = end - start;
                    float length = direction.Length();
                    if (length > 0.1f)
                    {
                        float rotation = (float)System.Math.Atan2(direction.Y, direction.X);
                        spriteBatch.Draw(
                            _gridPixelTexture,
                            start,
                            null,
                            pathColor,
                            rotation,
                            new Vector2(0, 0.5f),
                            new Vector2(length, 1f), // 1 pixel thick line
                            SpriteEffects.None,
                            0f
                        );
                    }
                }
            }
        }
        
        /// <summary>
        /// Draw enemy target paths (lines from ship to target position)
        /// </summary>
        public void DrawEnemyTargetPaths(
            SpriteBatch spriteBatch,
            List<FriendlyShip> friendlyShips)
        {
            if (_gridPixelTexture == null) return;
            
            foreach (var friendlyShip in friendlyShips)
            {
                Vector2 currentPos = friendlyShip.Position;
                Vector2 targetPos = friendlyShip.TargetPosition;
                
                // Only draw if ship is moving and target is different from current position
                if (Vector2.Distance(currentPos, targetPos) > 1f)
                {
                    Vector2 direction = targetPos - currentPos;
                    float length = direction.Length();
                    
                    if (length > 0.1f)
                    {
                        float rotation = (float)System.Math.Atan2(direction.Y, direction.X);
                        Color pathColor = new Color(255, 200, 0, 200); // Semi-transparent yellow/orange for target paths
                        
                        spriteBatch.Draw(
                            _gridPixelTexture,
                            currentPos,
                            null,
                            pathColor,
                            rotation,
                            new Vector2(0, 0.5f),
                            new Vector2(length, 2f), // 2 pixel thick line
                            SpriteEffects.None,
                            0f
                        );
                        
                        // Draw a small circle at the target position
                        DrawCircle(spriteBatch, targetPos, 5f, new Color(255, 200, 0, 255)); // Yellow circle at target
                    }
                }
            }
        }
        
        /// <summary>
        /// Draw look-ahead lines for ships (shows where ships are looking/aiming)
        /// </summary>
        public void DrawLookAheadLines(
            SpriteBatch spriteBatch,
            PlayerShip? playerShip,
            List<FriendlyShip> friendlyShips,
            List<EnemyShip> enemyShips)
        {
            if (_gridPixelTexture == null) return;
            
            // Draw look-ahead lines for player ship
            if (playerShip != null && playerShip.LookAheadVisible)
            {
                Vector2 shipPos = playerShip.Position;
                
                // Calculate look-ahead target in the direction the ship is facing
                float shipRotation = playerShip.Rotation;
                
                // Convert rotation to direction vector (ship points up at rotation 0)
                Vector2 direction = new Vector2(
                    (float)System.Math.Sin(shipRotation),  // X component
                    -(float)System.Math.Cos(shipRotation) // Y component (negative because up is negative Y in screen space)
                );
                
                float lookAheadDist = playerShip.MoveSpeed * playerShip.LookAheadDistance;
                Vector2 lookAheadTarget = shipPos + direction * lookAheadDist;
                
                // Draw line from ship to look-ahead target
                Vector2 lineDir = lookAheadTarget - shipPos;
                float lineLength = lineDir.Length();
                if (lineLength > 0.1f)
                {
                    float rotation = (float)System.Math.Atan2(lineDir.Y, lineDir.X);
                    Color lineColor = new Color(0, 255, 255, 255); // Cyan for look-ahead line (fully opaque for visibility)
                    
                    spriteBatch.Draw(
                        _gridPixelTexture,
                        shipPos,
                        null,
                        lineColor,
                        rotation,
                        new Vector2(0, 0.5f),
                        new Vector2(lineLength, 3f), // 3 pixel thick line for better visibility
                        SpriteEffects.None,
                        0f
                    );
                    
                    // Draw a small circle at the look-ahead target
                    DrawCircle(spriteBatch, lookAheadTarget, 6f, new Color(0, 255, 255, 255)); // Cyan circle, larger for visibility
                }
            }
            
            // Draw look-ahead lines for friendly ships
            foreach (var friendlyShip in friendlyShips)
            {
                if (friendlyShip.LookAheadVisible)
                {
                    Vector2 shipPos = friendlyShip.Position;
                    
                    // Calculate look-ahead target in the direction the ship is facing
                    float shipRotation = friendlyShip.Rotation;
                    
                    // Convert rotation to direction vector (ship points up at rotation 0)
                    Vector2 direction = new Vector2(
                        (float)System.Math.Sin(shipRotation),  // X component
                        -(float)System.Math.Cos(shipRotation) // Y component (negative because up is negative Y in screen space)
                    );
                    
                    float lookAheadDist = friendlyShip.MoveSpeed * friendlyShip.LookAheadDistance;
                    Vector2 lookAheadTarget = shipPos + direction * lookAheadDist;
                    
                    // Draw line from ship to look-ahead target
                    Vector2 lineDir = lookAheadTarget - shipPos;
                    float lineLength = lineDir.Length();
                    if (lineLength > 0.1f)
                    {
                        float rotation = (float)System.Math.Atan2(lineDir.Y, lineDir.X);
                        Color lineColor = new Color(0, 255, 255, 255); // Cyan for look-ahead line (fully opaque)
                        
                        spriteBatch.Draw(
                            _gridPixelTexture,
                            shipPos,
                            null,
                            lineColor,
                            rotation,
                            new Vector2(0, 0.5f),
                            new Vector2(lineLength, 3f), // 3 pixel thick line for better visibility
                            SpriteEffects.None,
                            0f
                        );
                        
                        // Draw a small circle at the look-ahead target
                        DrawCircle(spriteBatch, lookAheadTarget, 6f, new Color(0, 255, 255, 255)); // Cyan circle, larger for visibility
                    }
                }
            }
            
            // Draw look-ahead lines for enemy ships (if enabled)
            foreach (var enemyShip in enemyShips)
            {
                if (enemyShip.LookAheadVisible)
                {
                    Vector2 shipPos = enemyShip.Position;
                    
                    // Calculate look-ahead target in the direction the ship is facing
                    float shipRotation = enemyShip.Rotation;
                    
                    // Convert rotation to direction vector (ship points up at rotation 0)
                    Vector2 direction = new Vector2(
                        (float)System.Math.Sin(shipRotation),  // X component
                        -(float)System.Math.Cos(shipRotation) // Y component (negative because up is negative Y in screen space)
                    );
                    
                    float lookAheadDist = enemyShip.MoveSpeed * enemyShip.LookAheadDistance;
                    Vector2 lookAheadTarget = shipPos + direction * lookAheadDist;
                    
                    // Draw line from ship to look-ahead target
                    Vector2 lineDir = lookAheadTarget - shipPos;
                    float lineLength = lineDir.Length();
                    if (lineLength > 0.1f)
                    {
                        float rotation = (float)System.Math.Atan2(lineDir.Y, lineDir.X);
                        Color lineColor = new Color(255, 0, 0, 255); // Red for enemy look-ahead line
                        
                        spriteBatch.Draw(
                            _gridPixelTexture,
                            shipPos,
                            null,
                            lineColor,
                            rotation,
                            new Vector2(0, 0.5f),
                            new Vector2(lineLength, 3f), // 3 pixel thick line for better visibility
                            SpriteEffects.None,
                            0f
                        );
                        
                        // Draw a small circle at the look-ahead target
                        DrawCircle(spriteBatch, lookAheadTarget, 6f, new Color(255, 0, 0, 255)); // Red circle for enemy ships
                    }
                }
            }
        }
        
        /// <summary>
        /// Draw minimap in upper right corner
        /// </summary>
        public void DrawMinimap(
            SpriteBatch spriteBatch,
            Viewport viewport,
            PlayerShip? playerShip,
            List<FriendlyShip> friendlyShips,
            List<EnemyShip> enemyShips,
            Vector2 cameraPosition,
            float cameraZoom)
        {
            if (_minimapBackgroundTexture == null || _minimapPlayerDotTexture == null || 
                _minimapFriendlyDotTexture == null || _minimapEnemyDotTexture == null || 
                _minimapViewportOutlineTexture == null)
                return;
                
            int minimapX = viewport.Width - MinimapSize - 10;
            int minimapY = 10;
            
            // Calculate minimap scale (minimap size / map size)
            float minimapScale = MinimapSize / MapSize;
            
            // Draw minimap background
            spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend);
            spriteBatch.Draw(_minimapBackgroundTexture, new Rectangle(minimapX, minimapY, MinimapSize, MinimapSize), Color.White);
            
            // Draw galaxy background in minimap (in screen space)
            if (_galaxyTexture != null)
            {
                var minimapRect = new Rectangle(minimapX, minimapY, MinimapSize, MinimapSize);
                spriteBatch.Draw(
                    _galaxyTexture,
                    minimapRect,
                    null,
                    Color.White * 0.6f, // Slightly dimmed for minimap
                    0f,
                    Vector2.Zero,
                    SpriteEffects.None,
                    0f
                );
            }
            
            // Draw galaxy overlay on minimap at 30% opacity
            if (_galaxyOverlayTexture != null)
            {
                var minimapRect = new Rectangle(minimapX, minimapY, MinimapSize, MinimapSize);
                spriteBatch.Draw(
                    _galaxyOverlayTexture,
                    minimapRect,
                    null,
                    Color.White * 0.3f, // 30% opacity
                    0f,
                    Vector2.Zero,
                    SpriteEffects.None,
                    0f
                );
            }
            
            // Convert world positions to minimap screen positions
            Vector2 WorldToMinimap(Vector2 worldPos)
            {
                return new Vector2(
                    minimapX + worldPos.X * minimapScale,
                    minimapY + worldPos.Y * minimapScale
                );
            }
            
            // Draw friendly ships on minimap
            foreach (var friendlyShip in friendlyShips)
            {
                var friendlyScreenPos = WorldToMinimap(friendlyShip.Position);
                var friendlyDotSize = 3f; // Slightly smaller than player dot
                
                spriteBatch.Draw(
                    _minimapFriendlyDotTexture,
                    friendlyScreenPos,
                    null,
                    Color.Lime, // Green for friendly ships
                    0f,
                    Vector2.Zero,
                    friendlyDotSize,
                    SpriteEffects.None,
                    0f
                );
            }
            
            // Draw enemy ships on minimap
            foreach (var enemyShip in enemyShips)
            {
                var enemyScreenPos = WorldToMinimap(enemyShip.Position);
                var enemyDotSize = 4f; // Same size as player dot for better visibility
                
                // Clamp enemy position to minimap bounds
                enemyScreenPos.X = MathHelper.Clamp(enemyScreenPos.X, minimapX, minimapX + MinimapSize);
                enemyScreenPos.Y = MathHelper.Clamp(enemyScreenPos.Y, minimapY, minimapY + MinimapSize);
                
                spriteBatch.Draw(
                    _minimapEnemyDotTexture,
                    enemyScreenPos,
                    null,
                    Color.White, // Use white so the red texture color shows through
                    0f,
                    Vector2.Zero,
                    enemyDotSize,
                    SpriteEffects.None,
                    0f
                );
            }
            
            // Draw player ship position on minimap
            if (playerShip != null)
            {
                var playerScreenPos = WorldToMinimap(playerShip.Position);
                var playerDotSize = 4f; // 4 pixels on screen
                
                spriteBatch.Draw(
                    _minimapPlayerDotTexture,
                    playerScreenPos,
                    null,
                    Color.Cyan,
                    0f,
                    Vector2.Zero,
                    playerDotSize,
                    SpriteEffects.None,
                    0f
                );
            }
            
            // Draw camera viewport rectangle on minimap
            var viewWidth = viewport.Width / cameraZoom;
            var viewHeight = viewport.Height / cameraZoom;
            
            var cameraTopLeft = WorldToMinimap(new Vector2(
                cameraPosition.X - viewWidth / 2f,
                cameraPosition.Y - viewHeight / 2f
            ));
            
            var cameraBottomRight = WorldToMinimap(new Vector2(
                cameraPosition.X + viewWidth / 2f,
                cameraPosition.Y + viewHeight / 2f
            ));
            
            var cameraRect = new Rectangle(
                (int)cameraTopLeft.X,
                (int)cameraTopLeft.Y,
                (int)(cameraBottomRight.X - cameraTopLeft.X),
                (int)(cameraBottomRight.Y - cameraTopLeft.Y)
            );
            
            // Clamp camera rect to minimap bounds
            cameraRect.X = Math.Max(minimapX, Math.Min(cameraRect.X, minimapX + MinimapSize));
            cameraRect.Y = Math.Max(minimapY, Math.Min(cameraRect.Y, minimapY + MinimapSize));
            cameraRect.Width = Math.Min(cameraRect.Width, minimapX + MinimapSize - cameraRect.X);
            cameraRect.Height = Math.Min(cameraRect.Height, minimapY + MinimapSize - cameraRect.Y);
            
            // Draw camera viewport rectangle outline (2 pixels thick)
            const float lineWidth = 2f;
            Color cameraColor = new Color(255, 255, 255, 128); // White, 50% alpha (128/255)
            
            // Top line
            if (cameraRect.Width > 0 && cameraRect.Y >= minimapY)
            {
                spriteBatch.Draw(
                    _minimapViewportOutlineTexture,
                    new Rectangle(cameraRect.X, cameraRect.Y, cameraRect.Width, (int)lineWidth),
                    cameraColor
                );
            }
            
            // Bottom line
            if (cameraRect.Width > 0 && cameraRect.Y + cameraRect.Height <= minimapY + MinimapSize)
            {
                spriteBatch.Draw(
                    _minimapViewportOutlineTexture,
                    new Rectangle(cameraRect.X, cameraRect.Y + cameraRect.Height - (int)lineWidth, cameraRect.Width, (int)lineWidth),
                    cameraColor
                );
            }
            
            // Left line
            if (cameraRect.Height > 0 && cameraRect.X >= minimapX)
            {
                spriteBatch.Draw(
                    _minimapViewportOutlineTexture,
                    new Rectangle(cameraRect.X, cameraRect.Y, (int)lineWidth, cameraRect.Height),
                    cameraColor
                );
            }
            
            // Right line
            if (cameraRect.Height > 0 && cameraRect.X + cameraRect.Width <= minimapX + MinimapSize)
            {
                spriteBatch.Draw(
                    _minimapViewportOutlineTexture,
                    new Rectangle(cameraRect.X + cameraRect.Width - (int)lineWidth, cameraRect.Y, (int)lineWidth, cameraRect.Height),
                    cameraColor
                );
            }
            
            // Draw minimap border
            const int borderWidth = 2;
            Color borderColor = Color.White; // White border for visibility
            
            // Top border
            spriteBatch.Draw(
                _minimapViewportOutlineTexture,
                new Rectangle(minimapX, minimapY, MinimapSize, borderWidth),
                borderColor
            );
            
            // Bottom border
            spriteBatch.Draw(
                _minimapViewportOutlineTexture,
                new Rectangle(minimapX, minimapY + MinimapSize - borderWidth, MinimapSize, borderWidth),
                borderColor
            );
            
            // Left border
            spriteBatch.Draw(
                _minimapViewportOutlineTexture,
                new Rectangle(minimapX, minimapY, borderWidth, MinimapSize),
                borderColor
            );
            
            // Right border
            spriteBatch.Draw(
                _minimapViewportOutlineTexture,
                new Rectangle(minimapX + MinimapSize - borderWidth, minimapY, borderWidth, MinimapSize),
                borderColor
            );
            
            spriteBatch.End();
        }
        
        /// <summary>
        /// Draw UI grid overlay (for F12 grid mode)
        /// </summary>
        public void DrawUIGrid(SpriteBatch spriteBatch, Viewport viewport)
        {
            if (_gridPixelTexture == null || spriteBatch == null) return;
            
            spriteBatch.Begin(
                SpriteSortMode.Deferred,
                BlendState.AlphaBlend,
                SamplerState.PointClamp,
                DepthStencilState.None,
                RasterizerState.CullNone
            );
            
            const int UIGridSize = 10; // UI grid cell size in pixels
            var mouseState = Microsoft.Xna.Framework.Input.Mouse.GetState();
            var gridColor = new Color(255, 255, 255, 50); // 50% less opaque (was 100, now 50)
            
            // Draw vertical lines
            for (int x = 0; x <= viewport.Width; x += UIGridSize)
            {
                spriteBatch.Draw(
                    _gridPixelTexture,
                    new Rectangle(x, 0, 1, viewport.Height),
                    gridColor
                );
            }
            
            // Draw horizontal lines
            for (int y = 0; y <= viewport.Height; y += UIGridSize)
            {
                spriteBatch.Draw(
                    _gridPixelTexture,
                    new Rectangle(0, y, viewport.Width, 1),
                    gridColor
                );
            }
            
            // Highlight the grid point under the mouse cursor with a 3x3 red pixel
            if (mouseState.X >= 0 && mouseState.X < viewport.Width && 
                mouseState.Y >= 0 && mouseState.Y < viewport.Height)
            {
                int snappedX = (mouseState.X / UIGridSize) * UIGridSize;
                int snappedY = (mouseState.Y / UIGridSize) * UIGridSize;
                
                var highlightColor = Color.Red; // Red pixel
                
                // Draw a 3x3 pixel square at the grid point (centered on grid intersection)
                int offset = 1; // Offset to center the 3x3 square on the grid point
                spriteBatch.Draw(
                    _gridPixelTexture,
                    new Rectangle(snappedX - offset, snappedY - offset, 3, 3),
                    highlightColor
                );
            }
            
            spriteBatch.End();
        }
        
        /// <summary>
        /// Draw pathfinding grid visualization
        /// </summary>
        public void DrawPathfindingGrid(
            SpriteBatch spriteBatch,
            Viewport viewport,
            PathfindingGrid? pathfindingGrid,
            Vector2 cameraPosition,
            float cameraZoom)
        {
            if (pathfindingGrid == null || _gridPixelTexture == null)
            {
                System.Console.WriteLine($"DrawPathfindingGrid: _pathfindingGrid={pathfindingGrid != null}, _gridPixelTexture={_gridPixelTexture != null}");
                return;
            }
            
            // Calculate visible grid range based on camera view
            var viewWidth = viewport.Width / cameraZoom;
            var viewHeight = viewport.Height / cameraZoom;
            var padding = pathfindingGrid.CellSize; // Add one cell padding on each side
            
            var minX = (int)Math.Floor((cameraPosition.X - viewWidth / 2f - padding) / pathfindingGrid.CellSize);
            var maxX = (int)Math.Ceiling((cameraPosition.X + viewWidth / 2f + padding) / pathfindingGrid.CellSize);
            var minY = (int)Math.Floor((cameraPosition.Y - viewHeight / 2f - padding) / pathfindingGrid.CellSize);
            var maxY = (int)Math.Ceiling((cameraPosition.Y + viewHeight / 2f + padding) / pathfindingGrid.CellSize);
            
            // Clamp to grid bounds
            minX = Math.Max(0, minX);
            maxX = Math.Min(pathfindingGrid.GridWidth - 1, maxX);
            minY = Math.Max(0, minY);
            maxY = Math.Min(pathfindingGrid.GridHeight - 1, maxY);
            
            float cellSize = pathfindingGrid.CellSize;
            
            int cellsDrawn = 0;
            int walkableCells = 0;
            int obstacleCells = 0;
            
            // Draw grid cells
            for (int x = minX; x <= maxX; x++)
            {
                for (int y = minY; y <= maxY; y++)
                {
                    var node = pathfindingGrid.GetNodeAt(x, y);
                    if (node == null) continue;
                    
                    Vector2 cellPos = new Vector2(x * cellSize, y * cellSize);
                    Rectangle cellRect = new Rectangle((int)cellPos.X, (int)cellPos.Y, (int)cellSize, (int)cellSize);
                    
                    // Draw cell based on walkability
                    Color cellColor;
                    if (!node.Walkable)
                    {
                        // Obstacle - red with full opacity for visibility
                        cellColor = Color.Red;
                        obstacleCells++;
                    }
                    else
                    {
                        // Walkable - bright green for visibility
                        cellColor = new Color(0, 255, 0, 180);
                        walkableCells++;
                    }
                    
                    spriteBatch.Draw(_gridPixelTexture, cellRect, cellColor);
                    cellsDrawn++;
                    
                    // Draw cell border with higher opacity
                    Color borderColor = new Color(200, 200, 200, 255);
                    int borderWidth = 2; // Thicker border for visibility
                    
                    // Top edge
                    spriteBatch.Draw(_gridPixelTexture, 
                        new Rectangle(cellRect.X, cellRect.Y, cellRect.Width, borderWidth), 
                        borderColor);
                    // Bottom edge
                    spriteBatch.Draw(_gridPixelTexture, 
                        new Rectangle(cellRect.X, cellRect.Y + cellRect.Height - borderWidth, cellRect.Width, borderWidth), 
                        borderColor);
                    // Left edge
                    spriteBatch.Draw(_gridPixelTexture, 
                        new Rectangle(cellRect.X, cellRect.Y, borderWidth, cellRect.Height), 
                        borderColor);
                    // Right edge
                    spriteBatch.Draw(_gridPixelTexture, 
                        new Rectangle(cellRect.X + cellRect.Width - borderWidth, cellRect.Y, borderWidth, cellRect.Height), 
                        borderColor);
                }
            }
            
            // Debug output (only print occasionally to avoid spam)
            if (cellsDrawn > 0 && System.Environment.TickCount % 2000 < 16) // Print roughly every 2 seconds
            {
                System.Console.WriteLine($"A* Grid: Drawing {cellsDrawn} cells (Walkable: {walkableCells}, Obstacles: {obstacleCells}), Range: X[{minX}-{maxX}] Y[{minY}-{maxY}]");
            }
        }
    }
}

