using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;

namespace Planet9.Core
{
    public abstract class Scene
    {
        protected Game Game { get; }
        protected ContentManager Content { get; }
        protected GraphicsDevice GraphicsDevice { get; }
        protected SpriteBatch? SpriteBatch { get; }

        public Scene(Game game)
        {
            Game = game;
            Content = game.Content;
            GraphicsDevice = game.GraphicsDevice;
            SpriteBatch = (SpriteBatch?)game.Services.GetService(typeof(SpriteBatch));
        }

        public virtual void Initialize() { }
        public abstract void LoadContent();
        public abstract void Update(GameTime gameTime);
        public abstract void Draw(GameTime gameTime, SpriteBatch spriteBatch);
        public virtual void UnloadContent() { }
    }
}


