using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Myra;
using Planet9.Core;
using Planet9.Scenes;

namespace Planet9
{
    public class Planet9Game : Game
    {
        private GraphicsDeviceManager _graphics;
        private SpriteBatch? _spriteBatch;
        private SceneManager? _sceneManager;
        private Camera2D? _camera;

        public Planet9Game()
        {
            _graphics = new GraphicsDeviceManager(this);
            Content.RootDirectory = "Content";
            IsMouseVisible = true;
            
            // Set up window properties
            _graphics.PreferredBackBufferWidth = 1280;
            _graphics.PreferredBackBufferHeight = 720;
            _graphics.ApplyChanges();
            
            Window.Title = "Planet 9 - Space Adventure";
        }

        protected override void Initialize()
        {
            // Initialize scene manager BEFORE base.Initialize() to ensure it's available
            _sceneManager = new SceneManager(this);
            Components.Add(_sceneManager);
            Services.AddService(typeof(SceneManager), _sceneManager);
            
            base.Initialize();

            // Initialize camera after base initialization
            if (GraphicsDevice != null)
            {
                _camera = new Camera2D(GraphicsDevice);
            }
        }

        protected override void LoadContent()
        {
            // Initialize Myra
            MyraEnvironment.Game = this;
            
            _spriteBatch = new SpriteBatch(GraphicsDevice);
            
            // Set sprite batch in services for dependency injection
            Services.AddService(typeof(SpriteBatch), _spriteBatch);
            if (_camera != null)
            {
                Services.AddService(typeof(Camera2D), _camera);
            }

            // Load initial scene (main menu) after services are set up
            _sceneManager!.ChangeScene(new MainMenuScene(this));
        }

        protected override void Update(GameTime gameTime)
        {
            var keyboardState = Keyboard.GetState();
            
            if (GamePad.GetState(PlayerIndex.One).Buttons.Back == ButtonState.Pressed || 
                keyboardState.IsKeyDown(Keys.Escape))
            {
                Exit();
            }

            base.Update(gameTime);
        }

        protected override void Draw(GameTime gameTime)
        {
            GraphicsDevice.Clear(Color.Black);

            base.Draw(gameTime);
        }
    }
}

