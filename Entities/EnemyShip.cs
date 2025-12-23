using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;

namespace Planet9.Entities
{
    public class EnemyShip : FriendlyShip
    {
        public EnemyShip(GraphicsDevice graphicsDevice, ContentManager content, System.Random? random = null) 
            : base(graphicsDevice, content, random)
        {
        }

        protected override void LoadTexture()
        {
            try
            {
                // Load the ship1-256.png texture for enemy ships
                // TODO: Replace with dedicated enemy ship texture when available
                _texture = _content.Load<Texture2D>("ship1-256");
            }
            catch (System.Exception ex)
            {
                System.Console.WriteLine($"Failed to load enemy ship texture: {ex.Message}");
                // Fallback to parent class behavior
                base.LoadTexture();
            }
        }
    }
}

