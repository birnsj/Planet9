using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;

namespace Planet9.Entities
{
    public class ShipFriendly : PlayerShip
    {
        public ShipFriendly(GraphicsDevice graphicsDevice, ContentManager content) 
            : base(graphicsDevice, content)
        {
        }

        protected override void LoadTexture()
        {
            try
            {
                // Load the ship2-256.png texture
                _texture = _content.Load<Texture2D>("ship2-256");
            }
            catch (System.Exception ex)
            {
                System.Console.WriteLine($"Failed to load friendly ship texture: {ex.Message}");
                // Fallback to parent class behavior
                base.LoadTexture();
            }
        }
    }
}

