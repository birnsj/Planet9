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
        private Texture2D? _backgroundTexture;

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
            
            // Load main menu background image
            try
            {
                _backgroundTexture = Content.Load<Texture2D>("mainmenubackground");
                System.Console.WriteLine($"[MENU] Main menu background loaded: {_backgroundTexture?.Width}x{_backgroundTexture?.Height}");
            }
            catch (System.Exception ex)
            {
                System.Console.WriteLine($"[MENU ERROR] Failed to load main menu background: {ex.Message}");
                System.Console.WriteLine($"[MENU ERROR] Please ensure 'mainmenubackground.png' exists in Content/png/ and Content.mgcb includes it");
                // Try fallback to galaxy texture if available
                try
                {
                    _backgroundTexture = Content.Load<Texture2D>("galaxy");
                    System.Console.WriteLine($"[MENU] Using galaxy texture as fallback background");
                }
                catch
                {
                    System.Console.WriteLine($"[MENU ERROR] Fallback texture also failed, using solid color");
                    _backgroundTexture = null;
                }
            }
            
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

            // Check for Space bar press (not held down) to start game
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
            GraphicsDevice.Clear(Color.Black);

            spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp, DepthStencilState.None, RasterizerState.CullNone);
            
            // Draw background image if loaded, otherwise use solid color
            if (_backgroundTexture != null)
            {
                // Calculate scale to fill screen while maintaining aspect ratio
                float scaleX = (float)GraphicsDevice.Viewport.Width / _backgroundTexture.Width;
                float scaleY = (float)GraphicsDevice.Viewport.Height / _backgroundTexture.Height;
                float scale = Math.Max(scaleX, scaleY); // Use larger scale to fill screen
                
                // Calculate position to center the image
                float scaledWidth = _backgroundTexture.Width * scale;
                float scaledHeight = _backgroundTexture.Height * scale;
                float x = (GraphicsDevice.Viewport.Width - scaledWidth) / 2f;
                float y = (GraphicsDevice.Viewport.Height - scaledHeight) / 2f;
                
                spriteBatch.Draw(
                    _backgroundTexture,
                    new Vector2(x, y),
                    null,
                    Color.White,
                    0f,
                    Vector2.Zero,
                    scale,
                    SpriteEffects.None,
                    0f
                );
            }
            else
            {
                // Fallback: draw a gradient or use galaxy texture if available
                System.Console.WriteLine("[MENU] Background texture is null, using black background");
            }
            
            spriteBatch.End();

            // Render Myra UI on top
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

