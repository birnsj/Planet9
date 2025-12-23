using System;
using Microsoft.Xna.Framework.Graphics;

namespace Planet9.Core
{
    /// <summary>
    /// Manages shared textures to prevent memory leaks and ensure proper disposal
    /// </summary>
    public static class SharedTextureManager
    {
        private static Texture2D? _pixelTexture;
        private static GraphicsDevice? _graphicsDevice;
        private static int _referenceCount = 0;
        private static readonly object _lock = new object();

        /// <summary>
        /// Initialize the texture manager with a graphics device
        /// </summary>
        public static void Initialize(GraphicsDevice graphicsDevice)
        {
            lock (_lock)
            {
                _graphicsDevice = graphicsDevice;
            }
        }

        /// <summary>
        /// Get or create the shared 1x1 pixel texture
        /// </summary>
        public static Texture2D GetPixelTexture(GraphicsDevice graphicsDevice)
        {
            lock (_lock)
            {
                if (_pixelTexture == null || _pixelTexture.IsDisposed)
                {
                    _graphicsDevice = graphicsDevice;
                    _pixelTexture = new Texture2D(graphicsDevice, 1, 1);
                    _pixelTexture.SetData(new[] { Microsoft.Xna.Framework.Color.White });
                    _referenceCount = 0;
                }
                _referenceCount++;
                return _pixelTexture;
            }
        }

        /// <summary>
        /// Release a reference to the pixel texture
        /// </summary>
        public static void ReleasePixelTexture()
        {
            lock (_lock)
            {
                _referenceCount--;
                if (_referenceCount <= 0 && _pixelTexture != null)
                {
                    _pixelTexture.Dispose();
                    _pixelTexture = null;
                    _referenceCount = 0;
                }
            }
        }

        /// <summary>
        /// Dispose all managed textures (call on game exit)
        /// </summary>
        public static void DisposeAll()
        {
            lock (_lock)
            {
                if (_pixelTexture != null && !_pixelTexture.IsDisposed)
                {
                    _pixelTexture.Dispose();
                    _pixelTexture = null;
                }
                _referenceCount = 0;
            }
        }
    }
}

