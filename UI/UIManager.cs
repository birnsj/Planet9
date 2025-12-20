using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Myra;
using Myra.Graphics2D.UI;
using Planet9.Core;

namespace Planet9.UI
{
    /// <summary>
    /// UI Manager for handling Myra UI integration
    /// This class will manage GUI elements and integrate with Myra UI library
    /// </summary>
    public class UIManager
    {
        private readonly Game _game;
        private Desktop? _desktop;

        public UIManager(Game game)
        {
            _game = game;
        }

        public void Initialize()
        {
            // Initialize Myra
            MyraEnvironment.Game = _game;
            _desktop = new Desktop();
        }

        public void Update(GameTime gameTime)
        {
            // Update UI elements
            _desktop?.UpdateInput();
        }

        public void Draw(GameTime gameTime)
        {
            // Draw UI elements using Myra
            _desktop?.Render();
        }

        public Desktop? Desktop => _desktop;
    }
}


