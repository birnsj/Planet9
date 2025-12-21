using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Audio;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Myra.Graphics2D.UI;
using Planet9.Core;

namespace Planet9.Scenes
{
    public class MainMenuScene : Scene
    {
        private Desktop? _desktop;
        private KeyboardState _previousKeyboardState;
        private Label? _titleLabel;
        private Label? _subtitleLabel;
        private Label? _instructionsLabel;
        private SoundEffectInstance? _menuMusicInstance;

        public MainMenuScene(Game game) : base(game)
        {
        }

        public override void LoadContent()
        {
            // Initialize Myra Desktop
            _desktop = new Desktop();

            // Create a Grid for proper layout
            var grid = new Grid
            {
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                ColumnSpacing = 0,
                RowSpacing = 30
            };

            // Add rows for title, subtitle, spacer, and instructions
            grid.RowsProportions.Add(new Proportion(ProportionType.Auto));
            grid.RowsProportions.Add(new Proportion(ProportionType.Auto));
            grid.RowsProportions.Add(new Proportion(ProportionType.Auto));
            grid.RowsProportions.Add(new Proportion(ProportionType.Auto));
            grid.ColumnsProportions.Add(new Proportion(ProportionType.Auto));

            // Title label
            _titleLabel = new Label
            {
                Text = "PLANET 9",
                TextColor = Microsoft.Xna.Framework.Color.White,
                GridColumn = 0,
                GridRow = 0,
                HorizontalAlignment = HorizontalAlignment.Center
            };
            grid.Widgets.Add(_titleLabel);

            // Subtitle label
            _subtitleLabel = new Label
            {
                Text = "Space Adventure",
                TextColor = Microsoft.Xna.Framework.Color.LightGray,
                GridColumn = 0,
                GridRow = 1,
                HorizontalAlignment = HorizontalAlignment.Center
            };
            grid.Widgets.Add(_subtitleLabel);

            // Spacer
            var spacer = new Panel
            {
                GridColumn = 0,
                GridRow = 2,
                Height = 50
            };
            grid.Widgets.Add(spacer);

            // Instructions label
            _instructionsLabel = new Label
            {
                Text = "Press Space to Start",
                TextColor = Microsoft.Xna.Framework.Color.Yellow,
                GridColumn = 0,
                GridRow = 3,
                HorizontalAlignment = HorizontalAlignment.Center
            };
            grid.Widgets.Add(_instructionsLabel);

            _desktop.Root = grid;
            _previousKeyboardState = Keyboard.GetState();
            
            // Load and play main menu music
            try
            {
                var menuMusicEffect = Content.Load<SoundEffect>("mainmenu1");
                if (menuMusicEffect != null)
                {
                    _menuMusicInstance = menuMusicEffect.CreateInstance();
                    _menuMusicInstance.IsLooped = true; // Loop the music
                    _menuMusicInstance.Volume = 0.5f; // 50% volume
                    _menuMusicInstance.Play();
                    System.Console.WriteLine($"[MUSIC] Main menu music loaded and playing. State: {_menuMusicInstance.State}, Volume: {_menuMusicInstance.Volume}");
                }
            }
            catch (System.Exception ex)
            {
                System.Console.WriteLine($"[MUSIC ERROR] Failed to load main menu music: {ex.Message}");
            }
        }

        public override void Update(GameTime gameTime)
        {
            // Restart music if it stops unexpectedly
            if (_menuMusicInstance != null && _menuMusicInstance.State == SoundState.Stopped)
            {
                try
                {
                    _menuMusicInstance.Play();
                    System.Console.WriteLine($"[MUSIC] Restarted main menu music (was stopped)");
                }
                catch (System.Exception ex)
                {
                    System.Console.WriteLine($"[MUSIC ERROR] Failed to restart: {ex.Message}");
                }
            }
            
            var keyboardState = Keyboard.GetState();

            // Update Myra input
            _desktop?.UpdateInput();

            // Check for Space bar press (not held down) to start game immediately
            if (keyboardState.IsKeyDown(Keys.Space) && !_previousKeyboardState.IsKeyDown(Keys.Space))
            {
                // Transition to game scene immediately on Space press
                var sceneManager = (SceneManager)Game.Services.GetService(typeof(SceneManager));
                if (sceneManager != null)
                {
                    // Create and load game scene immediately
                    var gameScene = new GameScene(Game);
                    sceneManager.ChangeScene(gameScene);
                    // Scene is now active and will start immediately
                }
            }

            _previousKeyboardState = keyboardState;
        }

        public override void Draw(GameTime gameTime, SpriteBatch spriteBatch)
        {
            GraphicsDevice.Clear(Color.Navy);

            // Render Myra UI
            _desktop?.Render();
        }
        
        public override void UnloadContent()
        {
            // Stop music when leaving the scene
            try
            {
                if (_menuMusicInstance != null)
                {
                    _menuMusicInstance.Stop();
                    _menuMusicInstance.Dispose();
                    _menuMusicInstance = null;
                }
            }
            catch { }
            base.UnloadContent();
        }
    }
}

