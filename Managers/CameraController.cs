using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace Planet9.Managers
{
    /// <summary>
    /// Manages camera position, zoom, and movement
    /// </summary>
    public class CameraController
    {
        private Vector2 _position;
        private float _zoom = 0.40f;
        private const float MinZoom = 0.40f;
        private const float MaxZoom = 1.10f;
        private const float ZoomSpeed = 0.1f;
        
        public float CameraSpeed { get; set; } = 200f;
        public float PanSpeed { get; set; } = 800f;
        public float Inertia { get; set; } = 0.85f;
        
        private Vector2 _velocity = Vector2.Zero;
        private bool _followingPlayer = true;
        private bool _isPanningToPlayer = false;
        
        // Callbacks for camera update
        public Func<Vector2?>? GetPlayerPosition { get; set; }
        public Func<bool>? IsWASDPressed { get; set; }
        public Func<bool>? IsKeyJustPressed { get; set; }
        public Func<Keys, bool>? IsKeyDown { get; set; }
        public Func<int>? GetScrollDelta { get; set; }
        
        public Vector2 Position
        {
            get => _position;
            set => _position = value;
        }
        
        public float Zoom
        {
            get => _zoom;
            set => _zoom = MathHelper.Clamp(value, MinZoom, MaxZoom);
        }
        
        public bool FollowingPlayer
        {
            get => _followingPlayer;
            set => _followingPlayer = value;
        }
        
        public bool IsPanningToPlayer
        {
            get => _isPanningToPlayer;
            set => _isPanningToPlayer = value;
        }
        
        public Vector2 Velocity => _velocity;
        
        /// <summary>
        /// Get the camera transform matrix for the given viewport
        /// </summary>
        public Matrix GetTransform(Viewport viewport)
        {
            return Matrix.CreateScale(_zoom) * 
                   Matrix.CreateTranslation(
                       viewport.Width / 2f - _position.X * _zoom,
                       viewport.Height / 2f - _position.Y * _zoom,
                       0f
                   );
        }
        
        /// <summary>
        /// Get the camera transform matrix (legacy property for compatibility)
        /// </summary>
        [System.Obsolete("Use GetTransform(Viewport) instead")]
        public Matrix Transform
        {
            get
            {
                var viewport = new Viewport(0, 0, 1280, 720);
                return GetTransform(viewport);
            }
        }
        
        public CameraController()
        {
        }
        
        /// <summary>
        /// Update camera position, zoom, and movement
        /// </summary>
        public void Update(GameTime gameTime, Viewport viewport, float mapSize)
        {
            var deltaTime = (float)gameTime.ElapsedGameTime.TotalSeconds;
            var playerPosition = GetPlayerPosition?.Invoke();
            
            // Handle zoom with mouse wheel
            int scrollDelta = GetScrollDelta?.Invoke() ?? 0;
            if (scrollDelta != 0)
            {
                float zoomChange = scrollDelta > 0 ? ZoomSpeed : -ZoomSpeed;
                _zoom = MathHelper.Clamp(_zoom + zoomChange, MinZoom, MaxZoom);
            }
            
            // Check for spacebar to smoothly pan camera back to player
            if (IsKeyJustPressed?.Invoke() == true && playerPosition.HasValue)
            {
                _isPanningToPlayer = true;
                _followingPlayer = true;
            }
            
            // Check if WASD is pressed
            bool isWASDPressed = IsWASDPressed?.Invoke() ?? false;
            
            if (isWASDPressed)
            {
                // Cancel panning and following when WASD is pressed
                _isPanningToPlayer = false;
                _followingPlayer = false;
                
                // Manual camera control with WASD
                var movement = Vector2.Zero;
                if (IsKeyDown?.Invoke(Keys.A) == true)
                    movement.X -= 1f; // Move left
                if (IsKeyDown?.Invoke(Keys.D) == true)
                    movement.X += 1f; // Move right
                if (IsKeyDown?.Invoke(Keys.W) == true)
                    movement.Y -= 1f; // Move up
                if (IsKeyDown?.Invoke(Keys.S) == true)
                    movement.Y += 1f; // Move down
                
                // Normalize diagonal movement
                if (movement.Length() > 0)
                {
                    movement.Normalize();
                    var targetVelocity = movement * CameraSpeed;
                    // Apply inertia to velocity
                    _velocity = Vector2.Lerp(_velocity, targetVelocity, (1f - Inertia));
                }
                else
                {
                    // Apply deceleration when no input
                    _velocity *= Inertia;
                }
            }
            else
            {
                // Apply deceleration when not using WASD
                if (_velocity.Length() > 1f)
                {
                    _velocity *= Inertia;
                }
                else
                {
                    _velocity = Vector2.Zero;
                }
            }
            
            // Apply velocity to camera position, then clamp to map bounds
            _position += _velocity * deltaTime;
            
            // Clamp camera position to map bounds (accounting for viewport size and zoom)
            var viewWidth = viewport.Width / _zoom;
            var viewHeight = viewport.Height / _zoom;
            var minX = viewWidth / 2f;
            var maxX = mapSize - viewWidth / 2f;
            var minY = viewHeight / 2f;
            var maxY = mapSize - viewHeight / 2f;
            
            _position.X = MathHelper.Clamp(_position.X, minX, maxX);
            _position.Y = MathHelper.Clamp(_position.Y, minY, maxY);
            
            // Stop velocity if we hit a boundary
            if ((_position.X <= minX && _velocity.X < 0) || 
                (_position.X >= maxX && _velocity.X > 0))
            {
                _velocity.X = 0;
            }
            if ((_position.Y <= minY && _velocity.Y < 0) || 
                (_position.Y >= maxY && _velocity.Y > 0))
            {
                _velocity.Y = 0;
            }
            
            if (_isPanningToPlayer && playerPosition.HasValue)
            {
                // Smoothly pan camera back to player position
                var targetPosition = playerPosition.Value;
                var direction = targetPosition - _position;
                var distance = direction.Length();
                
                if (distance > 1f)
                {
                    // Move camera towards player
                    direction.Normalize();
                    var moveDistance = PanSpeed * deltaTime;
                    
                    // Don't overshoot
                    if (moveDistance > distance)
                    {
                        _position = targetPosition;
                        _isPanningToPlayer = false;
                    }
                    else
                    {
                        _position += direction * moveDistance;
                    }
                    
                    // Clamp to map bounds during panning
                    var panViewWidth = viewport.Width / _zoom;
                    var panViewHeight = viewport.Height / _zoom;
                    var panMinX = panViewWidth / 2f;
                    var panMaxX = mapSize - panViewWidth / 2f;
                    var panMinY = panViewHeight / 2f;
                    var panMaxY = mapSize - panViewHeight / 2f;
                    
                    _position.X = MathHelper.Clamp(_position.X, panMinX, panMaxX);
                    _position.Y = MathHelper.Clamp(_position.Y, panMinY, panMaxY);
                    
                    // If we hit a boundary, stop panning but keep following
                    if ((_position.X <= panMinX || _position.X >= panMaxX || 
                         _position.Y <= panMinY || _position.Y >= panMaxY) && 
                        _position != targetPosition)
                    {
                        _isPanningToPlayer = false;
                    }
                }
                else
                {
                    // Reached player position - panning complete
                    _position = targetPosition;
                    _isPanningToPlayer = false;
                    
                    // Clamp final position to map bounds
                    var finalViewWidth = viewport.Width / _zoom;
                    var finalViewHeight = viewport.Height / _zoom;
                    var finalMinX = finalViewWidth / 2f;
                    var finalMaxX = mapSize - finalViewWidth / 2f;
                    var finalMinY = finalViewHeight / 2f;
                    var finalMaxY = mapSize - finalViewHeight / 2f;
                    
                    _position.X = MathHelper.Clamp(_position.X, finalMinX, finalMaxX);
                    _position.Y = MathHelper.Clamp(_position.Y, finalMinY, finalMaxY);
                }
            }
            else if (_followingPlayer && playerPosition.HasValue && !_isPanningToPlayer)
            {
                // Keep camera locked on player after panning completes
                _position = playerPosition.Value;
                
                // Clamp to map bounds
                var followViewWidth = viewport.Width / _zoom;
                var followViewHeight = viewport.Height / _zoom;
                var followMinX = followViewWidth / 2f;
                var followMaxX = mapSize - followViewWidth / 2f;
                var followMinY = followViewHeight / 2f;
                var followMaxY = mapSize - followViewHeight / 2f;
                
                _position.X = MathHelper.Clamp(_position.X, followMinX, followMaxX);
                _position.Y = MathHelper.Clamp(_position.Y, followMinY, followMaxY);
            }
        }
        
        /// <summary>
        /// Pan camera to player position (legacy method for compatibility)
        /// </summary>
        public void PanToPlayer(Vector2 playerPosition, float panSpeed)
        {
            _isPanningToPlayer = true;
            _followingPlayer = true;
        }
        
        /// <summary>
        /// Convert screen coordinates to world coordinates
        /// </summary>
        public Vector2 ScreenToWorld(Vector2 screenPosition, Viewport viewport)
        {
            return Vector2.Transform(screenPosition, Matrix.Invert(GetTransform(viewport)));
        }
        
        /// <summary>
        /// Convert world coordinates to screen coordinates
        /// </summary>
        public Vector2 WorldToScreen(Vector2 worldPosition, Viewport viewport)
        {
            return Vector2.Transform(worldPosition, GetTransform(viewport));
        }
    }
}

