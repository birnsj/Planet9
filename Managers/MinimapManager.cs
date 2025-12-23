using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Planet9.Entities;

namespace Planet9.Managers
{
    /// <summary>
    /// Manages minimap initialization and state
    /// Note: Rendering is handled by RenderingManager
    /// </summary>
    public class MinimapManager
    {
        private GraphicsDevice _graphicsDevice;
        private Texture2D? _minimapBackgroundTexture;
        private Texture2D? _minimapPlayerDotTexture;
        private Texture2D? _minimapFriendlyDotTexture;
        private Texture2D? _minimapEnemyDotTexture;
        private Texture2D? _minimapViewportOutlineTexture;
        
        private bool _minimapVisible = true;
        private const int MinimapSize = 200;
        
        public bool MinimapVisible
        {
            get => _minimapVisible;
            set => _minimapVisible = value;
        }
        
        public Texture2D? MinimapBackgroundTexture => _minimapBackgroundTexture;
        public Texture2D? MinimapPlayerDotTexture => _minimapPlayerDotTexture;
        public Texture2D? MinimapFriendlyDotTexture => _minimapFriendlyDotTexture;
        public Texture2D? MinimapEnemyDotTexture => _minimapEnemyDotTexture;
        public Texture2D? MinimapViewportOutlineTexture => _minimapViewportOutlineTexture;
        
        public MinimapManager(GraphicsDevice graphicsDevice)
        {
            _graphicsDevice = graphicsDevice;
        }
        
        /// <summary>
        /// Initialize minimap textures
        /// </summary>
        public void Initialize()
        {
            // Create textures for minimap
            _minimapBackgroundTexture = new Texture2D(_graphicsDevice, 1, 1);
            _minimapBackgroundTexture.SetData(new[] { new Color(0, 0, 0, 200) }); // Semi-transparent black
            
            _minimapPlayerDotTexture = new Texture2D(_graphicsDevice, 1, 1);
            _minimapPlayerDotTexture.SetData(new[] { Color.Cyan });
            
            _minimapFriendlyDotTexture = new Texture2D(_graphicsDevice, 1, 1);
            _minimapFriendlyDotTexture.SetData(new[] { Color.Lime }); // Green for friendly ships
            
            _minimapEnemyDotTexture = new Texture2D(_graphicsDevice, 1, 1);
            _minimapEnemyDotTexture.SetData(new[] { Color.Red }); // Red for enemy ships
            
            _minimapViewportOutlineTexture = new Texture2D(_graphicsDevice, 1, 1);
            _minimapViewportOutlineTexture.SetData(new[] { Color.White }); // White so color can be controlled via Draw parameter
        }
        
        /// <summary>
        /// Dispose minimap textures
        /// </summary>
        public void Dispose()
        {
            _minimapBackgroundTexture?.Dispose();
            _minimapPlayerDotTexture?.Dispose();
            _minimapFriendlyDotTexture?.Dispose();
            _minimapEnemyDotTexture?.Dispose();
            _minimapViewportOutlineTexture?.Dispose();
        }
    }
}

