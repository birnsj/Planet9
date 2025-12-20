using System;
using Microsoft.Xna.Framework;
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
                Text = "Press ENTER to Start",
                TextColor = Microsoft.Xna.Framework.Color.Yellow,
                GridColumn = 0,
                GridRow = 3,
                HorizontalAlignment = HorizontalAlignment.Center
            };
            grid.Widgets.Add(_instructionsLabel);

            _desktop.Root = grid;
            _previousKeyboardState = Keyboard.GetState();
        }

        public override void Update(GameTime gameTime)
        {
            var keyboardState = Keyboard.GetState();

            // Update Myra input
            _desktop?.UpdateInput();

            if (keyboardState.IsKeyDown(Keys.Enter) && _previousKeyboardState.IsKeyUp(Keys.Enter))
            {
                // Transition to game scene
                var sceneManager = (SceneManager)Game.Services.GetService(typeof(SceneManager));
                sceneManager?.ChangeScene(new GameScene(Game));
            }

            _previousKeyboardState = keyboardState;
        }

        public override void Draw(GameTime gameTime, SpriteBatch spriteBatch)
        {
            GraphicsDevice.Clear(Color.Navy);

            // Render Myra UI
            _desktop?.Render();
        }
    }
}

