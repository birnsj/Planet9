using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Planet9.Core
{
    public class SceneManager : DrawableGameComponent
    {
        private Scene? _currentScene;

        public Scene? CurrentScene => _currentScene;

        public SceneManager(Game game) : base(game)
        {
        }

        public void ChangeScene(Scene newScene)
        {
            if (_currentScene != null)
            {
                _currentScene.UnloadContent();
            }

            _currentScene = newScene;
            _currentScene.Initialize();
            _currentScene.LoadContent();
        }

        public override void Update(GameTime gameTime)
        {
            _currentScene?.Update(gameTime);
            base.Update(gameTime);
        }

        public override void Draw(GameTime gameTime)
        {
            var spriteBatch = (SpriteBatch)Game.Services.GetService(typeof(SpriteBatch));
            if (spriteBatch != null)
            {
                _currentScene?.Draw(gameTime, spriteBatch);
            }
            base.Draw(gameTime);
        }
    }
}

