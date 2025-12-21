using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MonoGame.Extended;

namespace Planet9.Entities
{
    public class Laser : Entity
    {
        private const float LaserSpeed = 1800f; // pixels per second (faster)
        private const float LaserLength = 60f; // Longer lasers
        private const float LaserWidth = 4f; // Thinner lasers
        private Color _laserColor = Color.Red;
        private Color _coreColor = Color.White;
        private static Texture2D? _pixelTexture;
        private GraphicsDevice _graphicsDevice;

        public Laser(Vector2 startPosition, float direction, GraphicsDevice graphicsDevice)
        {
            Position = startPosition;
            Rotation = direction;
            _graphicsDevice = graphicsDevice;
            
            // Calculate velocity based on direction
            Velocity = new Vector2(
                (float)Math.Cos(direction - MathHelper.PiOver2),
                (float)Math.Sin(direction - MathHelper.PiOver2)
            ) * LaserSpeed;
            
            // Create pixel texture if needed
            if (_pixelTexture == null)
            {
                _pixelTexture = new Texture2D(_graphicsDevice, 1, 1);
                _pixelTexture.SetData(new[] { Color.White });
            }
        }

        public override void Update(GameTime gameTime)
        {
            if (!IsActive) return;

            var deltaTime = (float)gameTime.ElapsedGameTime.TotalSeconds;
            Position += Velocity * deltaTime;
        }

        public override void Draw(SpriteBatch spriteBatch)
        {
            if (!IsActive || _pixelTexture == null) return;

            // Calculate laser direction
            var forward = new Vector2(
                (float)Math.Cos(Rotation - MathHelper.PiOver2),
                (float)Math.Sin(Rotation - MathHelper.PiOver2)
            );
            
            var start = Position - forward * LaserLength / 2f;
            var end = Position + forward * LaserLength / 2f;
            var direction = end - start;
            var length = direction.Length();
            var angle = (float)Math.Atan2(direction.Y, direction.X);
            
            // Draw outer glow layer 1 (red, widest, most transparent)
            spriteBatch.Draw(
                _pixelTexture,
                start,
                null,
                _laserColor * 0.5f,
                angle,
                new Vector2(0, 0.5f),
                new Vector2(length, LaserWidth * 3f),
                SpriteEffects.None,
                0f
            );
            
            // Draw outer glow layer 2 (red, medium width, more opaque)
            spriteBatch.Draw(
                _pixelTexture,
                start,
                null,
                _laserColor * 0.7f,
                angle,
                new Vector2(0, 0.5f),
                new Vector2(length, LaserWidth * 2f),
                SpriteEffects.None,
                0f
            );
            
            // Draw inner glow (red, brighter)
            spriteBatch.Draw(
                _pixelTexture,
                start,
                null,
                _laserColor * 0.9f,
                angle,
                new Vector2(0, 0.5f),
                new Vector2(length, LaserWidth * 1.2f),
                SpriteEffects.None,
                0f
            );
            
            // Draw core (white, brightest center)
            spriteBatch.Draw(
                _pixelTexture,
                start,
                null,
                _coreColor,
                angle,
                new Vector2(0, 0.5f),
                new Vector2(length, LaserWidth * 0.6f),
                SpriteEffects.None,
                0f
            );
        }
    }
}

