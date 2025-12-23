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
        /// Update camera position and zoom
        /// </summary>
        public void Update(GameTime gameTime, Vector2? playerPosition, Viewport viewport)
        {
            var deltaTime = (float)gameTime.ElapsedGameTime.TotalSeconds;
            var keyboardState = Keyboard.GetState();
            var mouseState = Mouse.GetState();
            
            // Handle zoom with mouse wheel
            int scrollDelta = mouseState.ScrollWheelValue - (Mouse.GetState().ScrollWheelValue - mouseState.ScrollWheelValue);
            if (scrollDelta != 0)
            {
                float zoomDelta = scrollDelta > 0 ? ZoomSpeed : -ZoomSpeed;
                _zoom = MathHelper.Clamp(_zoom + zoomDelta, MinZoom, MaxZoom);
            }
            
            // Handle keyboard zoom
            if (keyboardState.IsKeyDown(Keys.PageUp))
            {
                _zoom = MathHelper.Clamp(_zoom + ZoomSpeed * deltaTime * 2f, MinZoom, MaxZoom);
            }
            if (keyboardState.IsKeyDown(Keys.PageDown))
            {
                _zoom = MathHelper.Clamp(_zoom - ZoomSpeed * deltaTime * 2f, MinZoom, MaxZoom);
            }
            
            // Handle camera movement
            Vector2 desiredVelocity = Vector2.Zero;
            
            // Follow player if enabled
            if (_followingPlayer && playerPosition.HasValue && !_isPanningToPlayer)
            {
                Vector2 toPlayer = playerPosition.Value - _position;
                float distance = toPlayer.Length();
                
                if (distance > 10f)
                {
                    desiredVelocity = toPlayer;
                    desiredVelocity.Normalize();
                    desiredVelocity *= PanSpeed;
                }
            }
            else
            {
                // Manual camera movement with arrow keys
                if (keyboardState.IsKeyDown(Keys.Left) || keyboardState.IsKeyDown(Keys.A))
                {
                    desiredVelocity.X -= CameraSpeed;
                }
                if (keyboardState.IsKeyDown(Keys.Right) || keyboardState.IsKeyDown(Keys.D))
                {
                    desiredVelocity.X += CameraSpeed;
                }
                if (keyboardState.IsKeyDown(Keys.Up) || keyboardState.IsKeyDown(Keys.W))
                {
                    desiredVelocity.Y -= CameraSpeed;
                }
                if (keyboardState.IsKeyDown(Keys.Down) || keyboardState.IsKeyDown(Keys.S))
                {
                    desiredVelocity.Y += CameraSpeed;
                }
            }
            
            // Apply inertia to camera movement
            _velocity = Vector2.Lerp(_velocity, desiredVelocity, 1f - Inertia);
            _position += _velocity * deltaTime;
            
            // Apply damping to velocity
            _velocity *= 0.9f;
            if (_velocity.LengthSquared() < 1f)
            {
                _velocity = Vector2.Zero;
            }
        }
        
        /// <summary>
        /// Pan camera to player position
        /// </summary>
        public void PanToPlayer(Vector2 playerPosition, float panSpeed)
        {
            _isPanningToPlayer = true;
            Vector2 toPlayer = playerPosition - _position;
            float distance = toPlayer.Length();
            
            if (distance < 10f)
            {
                _isPanningToPlayer = false;
                _followingPlayer = true;
            }
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

