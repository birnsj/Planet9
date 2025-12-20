using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Planet9.Core
{
    /// <summary>
    /// Simple 2D camera implementation
    /// </summary>
    public class Camera2D
    {
        private Vector2 _position;
        private float _zoom = 1.0f;
        private float _rotation = 0.0f;
        private readonly Viewport _viewport;

        public Vector2 Position
        {
            get => _position;
            set => _position = value;
        }

        public float Zoom
        {
            get => _zoom;
            set => _zoom = MathHelper.Clamp(value, 0.1f, 10.0f);
        }

        public float Rotation
        {
            get => _rotation;
            set => _rotation = value;
        }

        public Matrix Transform
        {
            get
            {
                return Matrix.CreateTranslation(new Vector3(-_position.X, -_position.Y, 0)) *
                       Matrix.CreateRotationZ(_rotation) *
                       Matrix.CreateScale(new Vector3(_zoom, _zoom, 1)) *
                       Matrix.CreateTranslation(new Vector3(_viewport.Width * 0.5f, _viewport.Height * 0.5f, 0));
            }
        }

        public Camera2D(GraphicsDevice graphicsDevice)
        {
            _viewport = graphicsDevice.Viewport;
            _position = Vector2.Zero;
        }

        public void Move(Vector2 amount)
        {
            _position += amount;
        }

        public Vector2 ScreenToWorld(Vector2 screenPosition)
        {
            return Vector2.Transform(screenPosition, Matrix.Invert(Transform));
        }

        public Vector2 WorldToScreen(Vector2 worldPosition)
        {
            return Vector2.Transform(worldPosition, Transform);
        }
    }
}

