using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Planet9.Entities
{
    public abstract class Entity
    {
        public Vector2 Position { get; set; }
        public Vector2 Velocity { get; set; }
        public float Rotation { get; set; }
        public bool IsActive { get; set; } = true;

        public abstract void Update(GameTime gameTime);
        public abstract void Draw(SpriteBatch spriteBatch);
    }
}






