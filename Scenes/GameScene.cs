using System;
using System.IO;
using System.Text.Json;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Audio;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Myra.Graphics2D.UI;
using Planet9.Core;
using Planet9.Entities;

namespace Planet9.Scenes
{
    public class GameScene : Scene
    {
        private Texture2D? _galaxyTexture;
        private Vector2 _tileSize;
        private const int TilesX = 4;
        private const int TilesY = 4;
        private Texture2D? _gridPixelTexture;
        private int _gridSize = 128; // 128x128 grid cells (can be changed via slider)
        private bool _gridVisible = false; // Grid visibility toggle (default off)
        private const float MapSize = 8192f; // Total map size
        private const int MinimapSize = 200; // Minimap size in pixels (square)
        private Texture2D? _minimapBackgroundTexture;
        private Texture2D? _minimapPlayerDotTexture;
        private Texture2D? _minimapFriendlyDotTexture;
        private Texture2D? _minimapViewportOutlineTexture;
        
        // Camera position and zoom
        private Vector2 _cameraPosition;
        private float _cameraZoom = 0.40f; // Start zoomed out
        public float CameraSpeed { get; set; } = 200f; // pixels per second
        private const float MinZoom = 0.40f; // Furthest (most zoomed out)
        private const float MaxZoom = 1.10f; // Closest (most zoomed in)
        private const float ZoomSpeed = 0.1f;
        
        // Player ship
        private PlayerShip? _playerShip;
        private int _currentShipClassIndex = 0; // 0 = PlayerShip, 1 = ShipFriendly
        private Label? _shipClassLabel;
        private TextButton? _shipClassLeftButton;
        private TextButton? _shipClassRightButton;
        
        // Friendly ships
        private System.Collections.Generic.List<ShipFriendly> _friendlyShips = new System.Collections.Generic.List<ShipFriendly>();
        private System.Collections.Generic.Dictionary<ShipFriendly, float> _friendlyShipAlpha = new System.Collections.Generic.Dictionary<ShipFriendly, float>();
        private System.Random _random = new System.Random();
        
        // Lasers
        private System.Collections.Generic.List<Laser> _lasers = new System.Collections.Generic.List<Laser>();
        
        // UI for zoom display and ship controls
        private Desktop? _desktop;
        private Desktop? _saveButtonDesktop;
        private Label? _zoomLabel;
        private HorizontalSlider? _speedSlider;
        private HorizontalSlider? _turnRateSlider;
        private HorizontalSlider? _cameraSpeedSlider;
        private Label? _speedLabel;
        private Label? _turnRateLabel;
        private Label? _cameraSpeedLabel;
        private TextButton? _gridSizeLeftButton;
        private TextButton? _gridSizeRightButton;
        private Label? _gridSizeLabel;
        private CheckBox? _gridVisibleCheckBox;
        private HorizontalSlider? _panSpeedSlider;
        private Label? _panSpeedLabel;
        private HorizontalSlider? _inertiaSlider;
        private Label? _inertiaLabel;
        private TextButton? _saveButton;
        private Label? _saveConfirmationLabel;
        private float _saveConfirmationTimer = 0f;
        private const float SaveConfirmationDuration = 2f; // seconds
        private MouseState _previousMouseState;
        private KeyboardState _previousKeyboardState;
        private bool _wasLeftButtonPressed = false;
        private bool _wasRightButtonPressed = false;
        private bool _isFollowingMouse = false;
        private Vector2 _clickStartPosition;
        private bool _isPanningToPlayer = false;
        private bool _cameraFollowingPlayer = true; // Track if camera should follow player (start as true)
        private float _cameraPanSpeed = 800f; // pixels per second for smooth panning (faster)
        private Vector2 _cameraVelocity = Vector2.Zero; // Camera velocity for inertia
        private float _cameraInertia = 0.85f; // Camera inertia factor (0 = no inertia, 1 = full inertia)
        private HorizontalSlider? _cameraInertiaSlider;
        private Label? _cameraInertiaLabel;
        private HorizontalSlider? _aimRotationSpeedSlider;
        private Label? _aimRotationSpeedLabel;
        private SoundEffectInstance? _backgroundMusicInstance;
        private SoundEffect? _laserFireSound;
        private SoundEffectInstance? _shipFlySound;
        private SoundEffectInstance? _shipIdleSound;
        private float _musicVolume = 0.5f; // Default music volume (0-1)
        private float _sfxVolume = 1.0f; // Default SFX volume (0-1)
        private HorizontalSlider? _musicVolumeSlider;
        private Label? _musicVolumeLabel;
        private HorizontalSlider? _sfxVolumeSlider;
        private Label? _sfxVolumeLabel;
        
        // Ship preview screen
        private bool _isPreviewActive = false;
        private Desktop? _previewDesktop;
        private Panel? _previewPanel;
        private Label? _previewCoordinateLabel;
        private Label? _previewShipLabel;
        private TextButton? _previewLeftButton;
        private TextButton? _previewRightButton;
        private int _previewShipIndex = 0; // 0 = ship1-256, 1 = ship2-256
        private Texture2D? _previewShip1Texture;
        private Texture2D? _previewShip2Texture;
        private bool _uiVisible = true; // Track UI visibility (toggled with U key)

        public GameScene(Game game) : base(game)
        {
        }

        public override void LoadContent()
        {
            // Load galaxy texture
            try
            {
                _galaxyTexture = Content.Load<Texture2D>("galaxy");
                
                // Use original texture size for tiles (no scaling)
                _tileSize = new Vector2(_galaxyTexture.Width, _galaxyTexture.Height);
            }
            catch (System.Exception ex)
            {
                System.Console.WriteLine($"Failed to load galaxy texture: {ex.Message}");
            }
            
            // Load and play background music
            try
            {
                var musicEffect = Content.Load<SoundEffect>("galaxy1");
                if (musicEffect != null)
                {
                    _backgroundMusicInstance = musicEffect.CreateInstance();
                    _backgroundMusicInstance.IsLooped = true; // Loop the music
                    _backgroundMusicInstance.Volume = _musicVolume; // Use saved volume
                    _backgroundMusicInstance.Play();
                    System.Console.WriteLine($"[MUSIC] Galaxy music loaded and playing. State: {_backgroundMusicInstance.State}, Volume: {_backgroundMusicInstance.Volume}");
                }
            }
            catch (System.Exception ex)
            {
                System.Console.WriteLine($"[MUSIC ERROR] Failed to load background music: {ex.Message}");
            }
            
            // Load laser fire sound effect
            try
            {
                _laserFireSound = Content.Load<SoundEffect>("shipfire1");
            }
            catch (System.Exception ex)
            {
                System.Console.WriteLine($"Failed to load laser fire sound: {ex.Message}");
            }
            
            // Load ship idle and fly sound effects
            try
            {
                var idleSound = Content.Load<SoundEffect>("shipidle1");
                System.Console.WriteLine($"[SHIP SOUND] Idle sound loaded: {idleSound != null}");
                if (idleSound != null)
                {
                    _shipIdleSound = idleSound.CreateInstance();
                    _shipIdleSound.IsLooped = true;
                    _shipIdleSound.Volume = _sfxVolume; // Use saved SFX volume
                    _shipIdleSound.Play(); // Start playing idle sound immediately
                    System.Console.WriteLine($"[SHIP SOUND] Idle sound playing. State: {_shipIdleSound.State}, Volume: {_shipIdleSound.Volume}");
                }
                
                var flySound = Content.Load<SoundEffect>("shipfly1");
                System.Console.WriteLine($"[SHIP SOUND] Fly sound loaded: {flySound != null}");
                if (flySound != null)
                {
                    _shipFlySound = flySound.CreateInstance();
                    _shipFlySound.IsLooped = true;
                    _shipFlySound.Volume = _sfxVolume * 0.8f; // 20% lower than SFX volume (80% of SFX volume)
                    System.Console.WriteLine($"[SHIP SOUND] Fly sound instance created. Volume: {_shipFlySound.Volume}");
                }
            }
            catch (System.Exception ex)
            {
                System.Console.WriteLine($"[SHIP SOUND ERROR] Failed to load ship sound effects: {ex.Message}");
                System.Console.WriteLine($"[SHIP SOUND ERROR] Stack trace: {ex.StackTrace}");
            }
            
            // Calculate map center position first
            const float mapSize = 8192f;
            var mapCenter = new Vector2(mapSize / 2f, mapSize / 2f);
            
            // Create player ship at map center (will be switched based on saved class)
            _playerShip = new PlayerShip(GraphicsDevice, Content);
            _playerShip.Position = mapCenter;
            _currentShipClassIndex = 0; // Default to PlayerShip
            
            // Create 8 friendly ships at random positions
            for (int i = 0; i < 8; i++)
            {
                var friendlyShip = new ShipFriendly(GraphicsDevice, Content);
                // Random position within map bounds (with some margin from edges)
                float margin = 500f;
                float x = (float)(_random.NextDouble() * (mapSize - margin * 2) + margin);
                float y = (float)(_random.NextDouble() * (mapSize - margin * 2) + margin);
                friendlyShip.Position = new Vector2(x, y);
                
                // Random movement properties with wider speed range
                friendlyShip.MoveSpeed = (float)(_random.NextDouble() * 400f + 100f); // 100-500 speed (wider range)
                friendlyShip.RotationSpeed = (float)(_random.NextDouble() * 3f + 2f); // 2-5 rotation speed
                friendlyShip.Inertia = (float)(_random.NextDouble() * 0.2f + 0.7f); // 0.7-0.9 inertia
                
                _friendlyShips.Add(friendlyShip);
            }
            
            // Initialize camera to center on player
            _cameraPosition = mapCenter;
            
            // Create pixel texture for drawing grid lines (strong, visible color)
            _gridPixelTexture = new Texture2D(GraphicsDevice, 1, 1);
            _gridPixelTexture.SetData(new[] { new Color(150, 150, 150, 220) }); // Strong gray with high opacity
            
            // Create textures for minimap
            _minimapBackgroundTexture = new Texture2D(GraphicsDevice, 1, 1);
            _minimapBackgroundTexture.SetData(new[] { new Color(0, 0, 0, 200) }); // Semi-transparent black
            
            _minimapPlayerDotTexture = new Texture2D(GraphicsDevice, 1, 1);
            _minimapPlayerDotTexture.SetData(new[] { Color.Cyan });
            
            _minimapFriendlyDotTexture = new Texture2D(GraphicsDevice, 1, 1);
            _minimapFriendlyDotTexture.SetData(new[] { Color.Lime }); // Green for friendly ships
            
            _minimapViewportOutlineTexture = new Texture2D(GraphicsDevice, 1, 1);
            _minimapViewportOutlineTexture.SetData(new[] { Color.White }); // White so color can be controlled via Draw parameter
            
            // Initialize UI for zoom display and ship controls
            _desktop = new Desktop();
            
            // Use a Grid layout to organize UI elements properly
            var grid = new Grid
            {
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top,
                ColumnSpacing = 0,
                RowSpacing = 5
            };
            
            // Define columns (one column for all elements)
            grid.ColumnsProportions.Add(new Proportion(ProportionType.Auto));
            
            // Define rows for each UI element
            grid.RowsProportions.Add(new Proportion(ProportionType.Auto)); // Zoom label
            grid.RowsProportions.Add(new Proportion(ProportionType.Auto)); // Speed label
            grid.RowsProportions.Add(new Proportion(ProportionType.Auto)); // Speed slider
            grid.RowsProportions.Add(new Proportion(ProportionType.Auto)); // Turn rate label
            grid.RowsProportions.Add(new Proportion(ProportionType.Auto)); // Turn rate slider
            grid.RowsProportions.Add(new Proportion(ProportionType.Auto)); // Camera speed label
            grid.RowsProportions.Add(new Proportion(ProportionType.Auto)); // Camera speed slider
            grid.RowsProportions.Add(new Proportion(ProportionType.Auto)); // Grid size label
            grid.RowsProportions.Add(new Proportion(ProportionType.Auto)); // Grid size buttons
            grid.RowsProportions.Add(new Proportion(ProportionType.Auto)); // Pan speed label
            grid.RowsProportions.Add(new Proportion(ProportionType.Auto)); // Pan speed slider
            grid.RowsProportions.Add(new Proportion(ProportionType.Auto)); // Camera inertia label
            grid.RowsProportions.Add(new Proportion(ProportionType.Auto)); // Camera inertia slider
            grid.RowsProportions.Add(new Proportion(ProportionType.Auto)); // Aim rotation speed label
            grid.RowsProportions.Add(new Proportion(ProportionType.Auto)); // Aim rotation speed slider
            grid.RowsProportions.Add(new Proportion(ProportionType.Auto)); // Inertia label
            grid.RowsProportions.Add(new Proportion(ProportionType.Auto)); // Inertia slider
            grid.RowsProportions.Add(new Proportion(ProportionType.Auto)); // Music volume label
            grid.RowsProportions.Add(new Proportion(ProportionType.Auto)); // Music volume slider
            grid.RowsProportions.Add(new Proportion(ProportionType.Auto)); // SFX volume label
            grid.RowsProportions.Add(new Proportion(ProportionType.Auto)); // SFX volume slider
            grid.RowsProportions.Add(new Proportion(ProportionType.Auto)); // Ship class label
            grid.RowsProportions.Add(new Proportion(ProportionType.Auto)); // Ship class buttons
            
            // Zoom label - bright yellow for visibility
            _zoomLabel = new Label
            {
                Text = $"Zoom: {_cameraZoom:F2}x",
                TextColor = Color.Yellow,
                GridColumn = 0,
                GridRow = 0,
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top,
                Padding = new Myra.Graphics2D.Thickness(10, 10, 0, 0)
            };
            grid.Widgets.Add(_zoomLabel);
            
            // Speed label - bright green for visibility
            _speedLabel = new Label
            {
                Text = $"Speed: {(_playerShip?.MoveSpeed ?? 300f):F0}",
                TextColor = Color.Lime,
                GridColumn = 0,
                GridRow = 1,
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top,
                Padding = new Myra.Graphics2D.Thickness(10, 5, 0, 0)
            };
            grid.Widgets.Add(_speedLabel);
            
            // Speed slider
            _speedSlider = new HorizontalSlider
            {
                Minimum = 50f,
                Maximum = 1000f,
                Value = _playerShip?.MoveSpeed ?? 300f,
                Width = 200,
                GridColumn = 0,
                GridRow = 2,
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top,
                Padding = new Myra.Graphics2D.Thickness(10, 5, 0, 0)
            };
            _speedSlider.ValueChanged += (s, a) =>
            {
                if (_playerShip != null)
                {
                    _playerShip.MoveSpeed = _speedSlider.Value;
                    _speedLabel.Text = $"Speed: {_speedSlider.Value:F0}";
                    // Auto-save when slider changes
                    SaveCurrentShipSettings();
                }
            };
            grid.Widgets.Add(_speedSlider);
            
            // Turn rate label - bright cyan for visibility
            _turnRateLabel = new Label
            {
                Text = $"Turn Rate: {(_playerShip?.RotationSpeed ?? 5f):F1}",
                TextColor = Color.Cyan,
                GridColumn = 0,
                GridRow = 3,
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top,
                Padding = new Myra.Graphics2D.Thickness(10, 10, 0, 0)
            };
            grid.Widgets.Add(_turnRateLabel);
            
            // Turn rate slider
            _turnRateSlider = new HorizontalSlider
            {
                Minimum = 1f,
                Maximum = 20f,
                Value = _playerShip?.RotationSpeed ?? 5f,
                Width = 200,
                GridColumn = 0,
                GridRow = 4,
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top,
                Padding = new Myra.Graphics2D.Thickness(10, 5, 0, 0)
            };
            _turnRateSlider.ValueChanged += (s, a) =>
            {
                if (_playerShip != null)
                {
                    _playerShip.RotationSpeed = _turnRateSlider.Value;
                    _turnRateLabel.Text = $"Turn Rate: {_turnRateSlider.Value:F1}";
                    // Auto-save when slider changes
                    SaveCurrentShipSettings();
                }
            };
            grid.Widgets.Add(_turnRateSlider);
            
            // Camera speed label - bright orange for visibility
            _cameraSpeedLabel = new Label
            {
                Text = $"Camera Speed: {CameraSpeed:F0}",
                TextColor = Color.Orange,
                GridColumn = 0,
                GridRow = 5,
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top,
                Padding = new Myra.Graphics2D.Thickness(10, 10, 0, 0)
            };
            grid.Widgets.Add(_cameraSpeedLabel);
            
            // Camera speed slider
            _cameraSpeedSlider = new HorizontalSlider
            {
                Minimum = 50f,
                Maximum = 1000f,
                Value = CameraSpeed,
                Width = 200,
                GridColumn = 0,
                GridRow = 6,
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top,
                Padding = new Myra.Graphics2D.Thickness(10, 5, 0, 0)
            };
            _cameraSpeedSlider.ValueChanged += (s, a) =>
            {
                CameraSpeed = _cameraSpeedSlider.Value;
                _cameraSpeedLabel.Text = $"Camera Speed: {_cameraSpeedSlider.Value:F0}";
            };
            grid.Widgets.Add(_cameraSpeedSlider);
            
            // Grid size controls container (label and checkbox in a horizontal layout)
            var gridSizeControlsContainer = new HorizontalStackPanel
            {
                GridColumn = 0,
                GridRow = 7,
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top,
                Spacing = 10,
                Padding = new Myra.Graphics2D.Thickness(10, 10, 0, 0)
            };
            
            // Grid size label - bright magenta for visibility
            _gridSizeLabel = new Label
            {
                Text = $"Grid Size: {_gridSize}",
                TextColor = Color.Magenta
            };
            gridSizeControlsContainer.Widgets.Add(_gridSizeLabel);
            
            // Grid visibility checkbox
            _gridVisibleCheckBox = new CheckBox
            {
                Text = "Show Grid",
                IsChecked = _gridVisible
            };
            _gridVisibleCheckBox.Click += (s, a) =>
            {
                _gridVisible = _gridVisibleCheckBox.IsChecked;
            };
            gridSizeControlsContainer.Widgets.Add(_gridVisibleCheckBox);
            
            grid.Widgets.Add(gridSizeControlsContainer);
            
            // Grid size arrow buttons container
            var gridSizeButtonContainer = new HorizontalStackPanel
            {
                GridColumn = 0,
                GridRow = 8,
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top,
                Spacing = 5,
                Padding = new Myra.Graphics2D.Thickness(10, 5, 0, 0)
            };
            
            // Grid size values array
            int[] gridSizeValues = { 64, 128, 256, 512 };
            
            // Left arrow button (decrease)
            _gridSizeLeftButton = new TextButton
            {
                Text = "←",
                Width = 40,
                Height = 30
            };
            _gridSizeLeftButton.Click += (s, a) =>
            {
                int currentIndex = Array.IndexOf(gridSizeValues, _gridSize);
                if (currentIndex > 0)
                {
                    _gridSize = gridSizeValues[currentIndex - 1];
                    _gridSizeLabel.Text = $"Grid Size: {_gridSize}";
                }
            };
            gridSizeButtonContainer.Widgets.Add(_gridSizeLeftButton);
            
            // Right arrow button (increase)
            _gridSizeRightButton = new TextButton
            {
                Text = "→",
                Width = 40,
                Height = 30
            };
            _gridSizeRightButton.Click += (s, a) =>
            {
                int currentIndex = Array.IndexOf(gridSizeValues, _gridSize);
                if (currentIndex >= 0 && currentIndex < gridSizeValues.Length - 1)
                {
                    _gridSize = gridSizeValues[currentIndex + 1];
                    _gridSizeLabel.Text = $"Grid Size: {_gridSize}";
                }
            };
            gridSizeButtonContainer.Widgets.Add(_gridSizeRightButton);
            
            grid.Widgets.Add(gridSizeButtonContainer);
            
            // Pan speed label - bright yellow for visibility
            _panSpeedLabel = new Label
            {
                Text = $"Pan Speed: {_cameraPanSpeed:F0}",
                TextColor = Color.Yellow,
                GridColumn = 0,
                GridRow = 9,
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top,
                Padding = new Myra.Graphics2D.Thickness(10, 10, 0, 0)
            };
            grid.Widgets.Add(_panSpeedLabel);
            
            // Pan speed slider
            _panSpeedSlider = new HorizontalSlider
            {
                Minimum = 200f,
                Maximum = 2000f,
                Value = _cameraPanSpeed,
                Width = 200,
                GridColumn = 0,
                GridRow = 10,
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top,
                Padding = new Myra.Graphics2D.Thickness(10, 5, 0, 0)
            };
            _panSpeedSlider.ValueChanged += (s, a) =>
            {
                _cameraPanSpeed = _panSpeedSlider.Value;
                _panSpeedLabel.Text = $"Pan Speed: {_panSpeedSlider.Value:F0}";
            };
            grid.Widgets.Add(_panSpeedSlider);
            
            // Camera inertia label - bright cyan for visibility
            _cameraInertiaLabel = new Label
            {
                Text = $"Camera Inertia: {_cameraInertia:F2}",
                TextColor = Color.Cyan,
                GridColumn = 0,
                GridRow = 11,
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top,
                Padding = new Myra.Graphics2D.Thickness(10, 10, 0, 0)
            };
            grid.Widgets.Add(_cameraInertiaLabel);
            
            // Camera inertia slider (0.0 = no inertia/instant stop, 0.995 = maximum inertia)
            _cameraInertiaSlider = new HorizontalSlider
            {
                Minimum = 0f,
                Maximum = 0.995f,
                Value = _cameraInertia,
                Width = 200,
                GridColumn = 0,
                GridRow = 12,
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top,
                Padding = new Myra.Graphics2D.Thickness(10, 5, 0, 0)
            };
            _cameraInertiaSlider.ValueChanged += (s, a) =>
            {
                _cameraInertia = _cameraInertiaSlider.Value;
                _cameraInertiaLabel.Text = $"Camera Inertia: {_cameraInertiaSlider.Value:F2}";
            };
            grid.Widgets.Add(_cameraInertiaSlider);
            
            // Aim rotation speed label - bright lime green for visibility
            _aimRotationSpeedLabel = new Label
            {
                Text = $"Aim Rotation Speed: {(_playerShip?.AimRotationSpeed ?? 5f):F1}",
                TextColor = Color.Lime,
                GridColumn = 0,
                GridRow = 13,
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top,
                Padding = new Myra.Graphics2D.Thickness(10, 10, 0, 0)
            };
            grid.Widgets.Add(_aimRotationSpeedLabel);
            
            // Aim rotation speed slider (how fast ship rotates toward cursor when stationary)
            _aimRotationSpeedSlider = new HorizontalSlider
            {
                Minimum = 1f,
                Maximum = 20f,
                Value = _playerShip?.AimRotationSpeed ?? 5f,
                Width = 200,
                GridColumn = 0,
                GridRow = 14,
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top,
                Padding = new Myra.Graphics2D.Thickness(10, 5, 0, 0)
            };
            _aimRotationSpeedSlider.ValueChanged += (s, a) =>
            {
                if (_playerShip != null)
                {
                    _playerShip.AimRotationSpeed = _aimRotationSpeedSlider.Value;
                    _aimRotationSpeedLabel.Text = $"Aim Rotation Speed: {_aimRotationSpeedSlider.Value:F1}";
                    // Auto-save when slider changes
                    SaveCurrentShipSettings();
                }
            };
            grid.Widgets.Add(_aimRotationSpeedSlider);
            
            // Inertia label - bright purple for visibility
            _inertiaLabel = new Label
            {
                Text = $"Inertia: {(_playerShip?.Inertia ?? 0.9f):F2}",
                TextColor = new Color(255, 100, 255), // Purple/magenta
                GridColumn = 0,
                GridRow = 15,
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top,
                Padding = new Myra.Graphics2D.Thickness(10, 10, 0, 0)
            };
            grid.Widgets.Add(_inertiaLabel);
            
            // Inertia slider (0.0 = no inertia/instant stop, 0.995 = maximum inertia)
            _inertiaSlider = new HorizontalSlider
            {
                Minimum = 0f,
                Maximum = 0.995f,
                Value = _playerShip?.Inertia ?? 0.9f,
                Width = 200,
                GridColumn = 0,
                GridRow = 16,
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top,
                Padding = new Myra.Graphics2D.Thickness(10, 5, 0, 0)
            };
            _inertiaSlider.ValueChanged += (s, a) =>
            {
                if (_playerShip != null)
                {
                    _playerShip.Inertia = _inertiaSlider.Value;
                    _inertiaLabel.Text = $"Inertia: {_inertiaSlider.Value:F2}";
                    // Auto-save when slider changes
                    SaveCurrentShipSettings();
                }
            };
            grid.Widgets.Add(_inertiaSlider);
            
            // Music volume label
            _musicVolumeLabel = new Label
            {
                Text = $"Music Volume: {(_musicVolume * 100f):F0}%",
                TextColor = new Color(100, 200, 255), // Light blue
                GridColumn = 0,
                GridRow = 17,
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top,
                Padding = new Myra.Graphics2D.Thickness(10, 10, 0, 0)
            };
            grid.Widgets.Add(_musicVolumeLabel);
            
            // Music volume slider (0-100%)
            _musicVolumeSlider = new HorizontalSlider
            {
                Minimum = 0f,
                Maximum = 1f,
                Value = _musicVolume,
                Width = 200,
                GridColumn = 0,
                GridRow = 18,
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top,
                Padding = new Myra.Graphics2D.Thickness(10, 5, 0, 0)
            };
            _musicVolumeSlider.ValueChanged += (s, a) =>
            {
                _musicVolume = _musicVolumeSlider.Value;
                _musicVolumeLabel.Text = $"Music Volume: {(_musicVolume * 100f):F0}%";
                // Apply volume to background music
                if (_backgroundMusicInstance != null)
                {
                    _backgroundMusicInstance.Volume = _musicVolume;
                }
            };
            grid.Widgets.Add(_musicVolumeSlider);
            
            // SFX volume label
            _sfxVolumeLabel = new Label
            {
                Text = $"SFX Volume: {(_sfxVolume * 100f):F0}%",
                TextColor = new Color(255, 150, 100), // Orange
                GridColumn = 0,
                GridRow = 19,
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top,
                Padding = new Myra.Graphics2D.Thickness(10, 10, 0, 0)
            };
            grid.Widgets.Add(_sfxVolumeLabel);
            
            // SFX volume slider (0-100%)
            _sfxVolumeSlider = new HorizontalSlider
            {
                Minimum = 0f,
                Maximum = 1f,
                Value = _sfxVolume,
                Width = 200,
                GridColumn = 0,
                GridRow = 20,
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top,
                Padding = new Myra.Graphics2D.Thickness(10, 5, 0, 0)
            };
            _sfxVolumeSlider.ValueChanged += (s, a) =>
            {
                _sfxVolume = _sfxVolumeSlider.Value;
                _sfxVolumeLabel.Text = $"SFX Volume: {(_sfxVolume * 100f):F0}%";
                // Apply volume to SFX instances
                if (_shipFlySound != null)
                {
                    _shipFlySound.Volume = _sfxVolume * 0.8f; // 20% lower than SFX volume
                }
            };
            grid.Widgets.Add(_sfxVolumeSlider);
            
            // Ship class label
            _shipClassLabel = new Label
            {
                Text = "Ship Class: PlayerShip",
                TextColor = new Color(255, 200, 100), // Orange
                GridColumn = 0,
                GridRow = 21,
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top,
                Padding = new Myra.Graphics2D.Thickness(10, 10, 0, 0)
            };
            grid.Widgets.Add(_shipClassLabel);
            
            // Ship class buttons container
            var shipClassButtonContainer = new HorizontalStackPanel
            {
                GridColumn = 0,
                GridRow = 22,
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top,
                Spacing = 5,
                Padding = new Myra.Graphics2D.Thickness(10, 5, 0, 0)
            };
            
            // Left arrow button (switch to PlayerShip)
            _shipClassLeftButton = new TextButton
            {
                Text = "← PlayerShip",
                Width = 120,
                Height = 30
            };
            _shipClassLeftButton.Click += (s, a) => SwitchShipClass(0);
            shipClassButtonContainer.Widgets.Add(_shipClassLeftButton);
            
            // Right arrow button (switch to ShipFriendly)
            _shipClassRightButton = new TextButton
            {
                Text = "ShipFriendly →",
                Width = 120,
                Height = 30
            };
            _shipClassRightButton.Click += (s, a) => SwitchShipClass(1);
            shipClassButtonContainer.Widgets.Add(_shipClassRightButton);
            
            grid.Widgets.Add(shipClassButtonContainer);
            
            // Wrap grid in a panel with background that covers all sliders
            var uiPanel = new Panel
            {
                Background = new Myra.Graphics2D.Brushes.SolidBrush(new Microsoft.Xna.Framework.Color(20, 20, 20, 220)), // Semi-transparent dark background
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top,
                Padding = new Myra.Graphics2D.Thickness(0, 0, 0, 0)
            };
            uiPanel.Widgets.Add(grid);
            
            _desktop.Root = uiPanel;
            
            // Create save button in bottom right as separate desktop
            _saveButtonDesktop = new Desktop();
            var saveButtonPanel = new Panel
            {
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Bottom,
                Padding = new Myra.Graphics2D.Thickness(0, 0, 0, 0)
            };
            
            _saveButton = new TextButton
            {
                Text = "Save Settings",
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Bottom,
                Padding = new Myra.Graphics2D.Thickness(10, 10, 10, 10),
                Width = 150,
                Height = 40
            };
            _saveButton.Click += (s, a) => SaveSettings();
            saveButtonPanel.Widgets.Add(_saveButton);
            
            // Save confirmation label (initially hidden)
            _saveConfirmationLabel = new Label
            {
                Text = "Settings Saved!",
                TextColor = Color.Lime,
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Bottom,
                Padding = new Myra.Graphics2D.Thickness(0, 0, 10, 60),
                Visible = false
            };
            saveButtonPanel.Widgets.Add(_saveConfirmationLabel);
            
            _saveButtonDesktop.Root = saveButtonPanel;
            
            // Update zoom label with initial zoom
            _zoomLabel.Text = $"Zoom: {_cameraZoom:F2}x";
            
            // Create preview screen UI
            _previewDesktop = new Desktop();
            
            _previewPanel = new Panel
            {
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Width = 700,
                Height = 700
            };
            
            _previewCoordinateLabel = new Label
            {
                Text = "Coordinates: (0, 0)",
                TextColor = Color.White,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Top,
                Padding = new Myra.Graphics2D.Thickness(10, 10, 10, 10)
            };
            _previewPanel.Widgets.Add(_previewCoordinateLabel);
            
            // Ship name label
            _previewShipLabel = new Label
            {
                Text = "PlayerShip (1/2)",
                TextColor = Color.Yellow,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Top,
                Padding = new Myra.Graphics2D.Thickness(10, 40, 10, 10)
            };
            _previewPanel.Widgets.Add(_previewShipLabel);
            
            // Arrow buttons container - positioned inside the preview panel at the bottom
            var arrowContainer = new HorizontalStackPanel
            {
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Bottom,
                Spacing = 20,
                Padding = new Myra.Graphics2D.Thickness(10, 10, 10, 60) // Extra bottom padding to position above close button
            };
            
            // Left arrow button
            _previewLeftButton = new TextButton
            {
                Text = "←",
                Width = 60,
                Height = 60
            };
            _previewLeftButton.Click += (s, a) => 
            {
                _previewShipIndex--;
                if (_previewShipIndex < 0)
                    _previewShipIndex = 1; // Wrap to last (ship2)
                UpdatePreviewShipLabel();
                // Switch player ship class to match preview
                SwitchShipClass(_previewShipIndex);
            };
            arrowContainer.Widgets.Add(_previewLeftButton);
            
            // Right arrow button
            _previewRightButton = new TextButton
            {
                Text = "→",
                Width = 60,
                Height = 60
            };
            _previewRightButton.Click += (s, a) => 
            {
                _previewShipIndex++;
                if (_previewShipIndex > 1)
                    _previewShipIndex = 0; // Wrap to ship1
                UpdatePreviewShipLabel();
                // Switch player ship class to match preview
                SwitchShipClass(_previewShipIndex);
            };
            arrowContainer.Widgets.Add(_previewRightButton);
            
            _previewPanel.Widgets.Add(arrowContainer);
            
            var closeButton = new TextButton
            {
                Text = "Close (P)",
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Bottom,
                Padding = new Myra.Graphics2D.Thickness(10, 10, 10, 10),
                Width = 150,
                Height = 40
            };
            closeButton.Click += (s, a) => _isPreviewActive = false;
            _previewPanel.Widgets.Add(closeButton);
            
            _previewDesktop.Root = _previewPanel;
            _previewDesktop.Root.Visible = false;
            
            // Load ship textures for preview
            try
            {
                _previewShip1Texture = Content.Load<Texture2D>("ship1-256");
                _previewShip2Texture = Content.Load<Texture2D>("ship2-256");
            }
            catch (System.Exception ex)
            {
                System.Console.WriteLine($"Failed to load preview ship textures: {ex.Message}");
            }
            
            // Load saved settings after UI is initialized
            LoadSettings();
            
            // Load current ship class settings
            LoadCurrentShipSettings();
            
            // Update ship class label
            if (_shipClassLabel != null)
            {
                _shipClassLabel.Text = $"Ship Class: {(_currentShipClassIndex == 0 ? "PlayerShip" : "ShipFriendly")}";
            }
            
            _previousMouseState = Mouse.GetState();
            _previousKeyboardState = Keyboard.GetState();
        }
        
        private void LoadSettings()
        {
            try
            {
                var filePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "settings.json");
                System.Console.WriteLine($"Attempting to load settings from: {filePath}");
                
                if (File.Exists(filePath))
                {
                    var json = File.ReadAllText(filePath);
                    System.Console.WriteLine($"Settings file found. Content: {json}");
                    var settings = JsonSerializer.Deserialize<JsonElement>(json);
                    
                    // Load current ship class
                    if (settings.TryGetProperty("CurrentShipClass", out var shipClassElement))
                    {
                        var shipClassIndex = shipClassElement.GetInt32();
                        if (shipClassIndex != _currentShipClassIndex)
                        {
                            SwitchShipClass(shipClassIndex);
                        }
                    }
                    
                    // Ship-specific settings are loaded by LoadCurrentShipSettings()
                    
                    // Load camera speed
                    if (settings.TryGetProperty("CameraSpeed", out var cameraSpeedElement))
                    {
                        var cameraSpeed = cameraSpeedElement.GetSingle();
                        System.Console.WriteLine($"Loading CameraSpeed: {cameraSpeed}");
                        CameraSpeed = cameraSpeed;
                        if (_cameraSpeedSlider != null)
                        {
                            _cameraSpeedSlider.Value = cameraSpeed;
                        }
                        if (_cameraSpeedLabel != null)
                        {
                            _cameraSpeedLabel.Text = $"Camera Speed: {cameraSpeed:F0}";
                        }
                    }
                    
                    // Load zoom
                    if (settings.TryGetProperty("Zoom", out var zoomElement))
                    {
                        var zoom = zoomElement.GetSingle();
                        System.Console.WriteLine($"Loading Zoom: {zoom}");
                        _cameraZoom = MathHelper.Clamp(zoom, MinZoom, MaxZoom);
                        if (_zoomLabel != null)
                        {
                            _zoomLabel.Text = $"Zoom: {_cameraZoom:F2}x";
                        }
                    }
                    
                    // Load grid size
                    if (settings.TryGetProperty("GridSize", out var gridSizeElement))
                    {
                        var gridSize = gridSizeElement.GetInt32();
                        System.Console.WriteLine($"Loading GridSize: {gridSize}");
                        // Validate and snap to valid values
                        int[] validGridSizes = { 64, 128, 256, 512 };
                        int closestSize = validGridSizes[0];
                        int minDiff = Math.Abs(gridSize - closestSize);
                        foreach (int size in validGridSizes)
                        {
                            int diff = Math.Abs(gridSize - size);
                            if (diff < minDiff)
                            {
                                minDiff = diff;
                                closestSize = size;
                            }
                        }
                        _gridSize = closestSize;
                        
                        if (_gridSizeLabel != null)
                        {
                            _gridSizeLabel.Text = $"Grid Size: {_gridSize}";
                        }
                    }
                    
                    // Load pan speed
                    if (settings.TryGetProperty("PanSpeed", out var panSpeedElement))
                    {
                        var panSpeed = panSpeedElement.GetSingle();
                        System.Console.WriteLine($"Loading PanSpeed: {panSpeed}");
                        _cameraPanSpeed = MathHelper.Clamp(panSpeed, 200f, 2000f);
                        if (_panSpeedSlider != null)
                        {
                            _panSpeedSlider.Value = _cameraPanSpeed;
                        }
                        if (_panSpeedLabel != null)
                        {
                            _panSpeedLabel.Text = $"Pan Speed: {_cameraPanSpeed:F0}";
                        }
                    }
                    
                    // Load camera inertia
                    if (settings.TryGetProperty("CameraInertia", out var cameraInertiaElement))
                    {
                        var cameraInertia = cameraInertiaElement.GetSingle();
                        System.Console.WriteLine($"Loading CameraInertia: {cameraInertia}");
                        _cameraInertia = MathHelper.Clamp(cameraInertia, 0f, 0.995f);
                        if (_cameraInertiaSlider != null)
                        {
                            _cameraInertiaSlider.Value = _cameraInertia;
                        }
                        if (_cameraInertiaLabel != null)
                        {
                            _cameraInertiaLabel.Text = $"Camera Inertia: {_cameraInertia:F2}";
                        }
                    }
                    
                    
                    // Load music volume
                    if (settings.TryGetProperty("MusicVolume", out var musicVolumeElement))
                    {
                        var musicVolume = musicVolumeElement.GetSingle();
                        System.Console.WriteLine($"Loading MusicVolume: {musicVolume}");
                        _musicVolume = MathHelper.Clamp(musicVolume, 0f, 1f);
                        if (_musicVolumeSlider != null)
                        {
                            _musicVolumeSlider.Value = _musicVolume;
                        }
                        if (_musicVolumeLabel != null)
                        {
                            _musicVolumeLabel.Text = $"Music Volume: {(_musicVolume * 100f):F0}%";
                        }
                        // Apply to background music instance
                        if (_backgroundMusicInstance != null)
                        {
                            _backgroundMusicInstance.Volume = _musicVolume;
                        }
                    }
                    
                    // Load SFX volume
                    if (settings.TryGetProperty("SFXVolume", out var sfxVolumeElement))
                    {
                        var sfxVolume = sfxVolumeElement.GetSingle();
                        System.Console.WriteLine($"Loading SFXVolume: {sfxVolume}");
                        _sfxVolume = MathHelper.Clamp(sfxVolume, 0f, 1f);
                        if (_sfxVolumeSlider != null)
                        {
                            _sfxVolumeSlider.Value = _sfxVolume;
                        }
                        if (_sfxVolumeLabel != null)
                        {
                            _sfxVolumeLabel.Text = $"SFX Volume: {(_sfxVolume * 100f):F0}%";
                        }
                        // Apply to SFX instances
                        if (_shipIdleSound != null)
                        {
                            _shipIdleSound.Volume = _sfxVolume;
                        }
                        if (_shipFlySound != null)
                        {
                            _shipFlySound.Volume = _sfxVolume * 0.8f; // 20% lower than SFX volume
                        }
                    }
                    
                    System.Console.WriteLine("Settings loaded successfully!");
                }
                else
                {
                    System.Console.WriteLine("Settings file not found. Using default values.");
                }
            }
            catch (Exception ex)
            {
                System.Console.WriteLine($"Failed to load settings: {ex.Message}");
                System.Console.WriteLine($"Stack trace: {ex.StackTrace}");
            }
        }
        
        private void SaveSettings()
        {
            try
            {
                // Save current ship class settings
                SaveCurrentShipSettings();
                
                // Save general settings (camera, UI, etc.)
                var settings = new
                {
                    CurrentShipClass = _currentShipClassIndex,
                    CameraSpeed = CameraSpeed,
                    Zoom = _cameraZoom,
                    GridSize = _gridSize,
                    PanSpeed = _cameraPanSpeed,
                    CameraInertia = _cameraInertia,
                    MusicVolume = _musicVolume,
                    SFXVolume = _sfxVolume
                };
                
                var json = JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true });
                var filePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "settings.json");
                System.Console.WriteLine($"Saving settings to: {filePath}");
                System.Console.WriteLine($"Settings JSON: {json}");
                File.WriteAllText(filePath, json);
                System.Console.WriteLine($"Settings file written successfully. File exists: {File.Exists(filePath)}");
                
                // Show confirmation message
                if (_saveConfirmationLabel != null)
                {
                    _saveConfirmationLabel.Visible = true;
                    _saveConfirmationTimer = SaveConfirmationDuration;
                }
                
                System.Console.WriteLine("Settings saved successfully!");
            }
            catch (Exception ex)
            {
                System.Console.WriteLine($"Failed to save settings: {ex.Message}");
                // Show error message if save fails
                if (_saveConfirmationLabel != null)
                {
                    _saveConfirmationLabel.Text = "Save Failed!";
                    _saveConfirmationLabel.TextColor = Color.Red;
                    _saveConfirmationLabel.Visible = true;
                    _saveConfirmationTimer = SaveConfirmationDuration;
                }
            }
        }

        public override void Update(GameTime gameTime)
        {
            var keyboardState = Keyboard.GetState();
            var mouseState = Mouse.GetState();
            
            // Toggle preview with P key
            if (keyboardState.IsKeyDown(Keys.P) && !_previousKeyboardState.IsKeyDown(Keys.P))
            {
                _isPreviewActive = !_isPreviewActive;
                if (_previewDesktop?.Root != null)
                {
                    _previewDesktop.Root.Visible = _isPreviewActive;
                }
                if (_isPreviewActive)
                {
                    // Sync preview index with current ship class when opening
                    _previewShipIndex = _currentShipClassIndex;
                    UpdatePreviewShipLabel();
                }
            }
            
            // Arrow keys to switch ships in preview
            if (_isPreviewActive)
            {
                if (keyboardState.IsKeyDown(Keys.Left) && !_previousKeyboardState.IsKeyDown(Keys.Left))
                {
                    _previewShipIndex--;
                    if (_previewShipIndex < 0)
                        _previewShipIndex = 1; // Wrap to ship2
                    UpdatePreviewShipLabel();
                    // Switch player ship class to match preview
                    SwitchShipClass(_previewShipIndex);
                }
                if (keyboardState.IsKeyDown(Keys.Right) && !_previousKeyboardState.IsKeyDown(Keys.Right))
                {
                    _previewShipIndex++;
                    if (_previewShipIndex > 1)
                        _previewShipIndex = 0; // Wrap to ship1
                    UpdatePreviewShipLabel();
                    // Switch player ship class to match preview
                    SwitchShipClass(_previewShipIndex);
                }
            }
            
            // Toggle UI with U key
            if (keyboardState.IsKeyDown(Keys.U) && !_previousKeyboardState.IsKeyDown(Keys.U))
            {
                _uiVisible = !_uiVisible;
                if (_desktop?.Root != null)
                {
                    _desktop.Root.Visible = _uiVisible;
                }
                if (_saveButtonDesktop?.Root != null)
                {
                    _saveButtonDesktop.Root.Visible = _uiVisible;
                }
            }
            
            // If preview is active, only update preview UI, don't update game
            if (_isPreviewActive)
            {
                _previewDesktop?.UpdateInput();
                
                // Get the currently previewed ship texture
                Texture2D? shipTexture = _previewShipIndex == 0 ? _previewShip1Texture : _previewShip2Texture;
                    
                if (_previewPanel != null && _previewCoordinateLabel != null && shipTexture != null)
                {
                    
                    // Calculate preview panel position (centered)
                    int panelX = (GraphicsDevice.Viewport.Width - 700) / 2;
                    int panelY = (GraphicsDevice.Viewport.Height - 700) / 2;
                    
                    // Sprite position in preview (centered in panel)
                    int spriteX = panelX + 350 - (shipTexture?.Width ?? 0) / 2;
                    int spriteY = panelY + 350 - (shipTexture?.Height ?? 0) / 2;
                    
                    // Check if mouse is over sprite
                    if (shipTexture != null && mouseState.X >= spriteX && mouseState.X < spriteX + shipTexture.Width &&
                        mouseState.Y >= spriteY && mouseState.Y < spriteY + shipTexture.Height)
                    {
                        // Calculate texture coordinates
                        int texX = mouseState.X - spriteX;
                        int texY = mouseState.Y - spriteY;
                        _previewCoordinateLabel.Text = $"Coordinates: ({texX}, {texY})";
                    }
                    else
                    {
                        _previewCoordinateLabel.Text = "Coordinates: (--, --)";
                    }
                }
                
                _previousKeyboardState = keyboardState;
                return; // Don't update game logic when preview is active
            }
            
            // Restart music if it stops unexpectedly
            if (_backgroundMusicInstance != null && _backgroundMusicInstance.State == SoundState.Stopped)
            {
                try
                {
                    _backgroundMusicInstance.Play();
                    System.Console.WriteLine($"[MUSIC] Restarted galaxy music (was stopped)");
                }
                catch (System.Exception ex)
                {
                    System.Console.WriteLine($"[MUSIC ERROR] Failed to restart: {ex.Message}");
                }
            }
            
            // Update ship sound effects based on forward motion state
            if (_playerShip != null)
            {
                // Check if ship is actively moving forward (not just coasting from inertia)
                bool isMovingForward = _playerShip.IsActivelyMoving();
                
                // Play fly sound when moving forward, idle sound when not moving forward
                if (isMovingForward)
                {
                    // Stop idle sound if playing
                    if (_shipIdleSound != null && _shipIdleSound.State == SoundState.Playing)
                    {
                        _shipIdleSound.Stop();
                        System.Console.WriteLine($"[SHIP SOUND] Stopped idle sound");
                    }
                    // Ensure fly sound is playing (restart if it stopped)
                    if (_shipFlySound != null && _shipFlySound.State != SoundState.Playing)
                    {
                        _shipFlySound.Play();
                        System.Console.WriteLine($"[SHIP SOUND] Started/restarted fly sound. State: {_shipFlySound.State}, Volume: {_shipFlySound.Volume}");
                    }
                }
                else
                {
                    // Stop fly sound when not moving forward
                    if (_shipFlySound != null && _shipFlySound.State == SoundState.Playing)
                    {
                        _shipFlySound.Stop();
                        System.Console.WriteLine($"[SHIP SOUND] Stopped fly sound");
                    }
                    // Ensure idle sound is playing (restart if it stopped)
                    if (_shipIdleSound != null && _shipIdleSound.State != SoundState.Playing)
                    {
                        _shipIdleSound.Play();
                        System.Console.WriteLine($"[SHIP SOUND] Started/restarted idle sound. State: {_shipIdleSound.State}, Volume: {_shipIdleSound.Volume}");
                    }
                }
            }
            
            var deltaTime = (float)gameTime.ElapsedGameTime.TotalSeconds;
            
            // Check if mouse cursor is within the game window bounds
            bool isMouseInWindow = mouseState.X >= 0 && mouseState.X < GraphicsDevice.Viewport.Width &&
                                  mouseState.Y >= 0 && mouseState.Y < GraphicsDevice.Viewport.Height;
            
            // Only process mouse input if cursor is within window
            if (!isMouseInWindow)
            {
                _previousMouseState = mouseState;
                _previousKeyboardState = keyboardState;
                return;
            }
            
            // Check if mouse is over UI before processing player movement
            // Only check if UI is visible
            bool isMouseOverUI = false;
            bool isMouseOverAnyUI = false;
            
            if (_uiVisible)
            {
                // UI area is roughly 0-250 width, 0-800 height in top-left corner (extended to cover all sliders and volume controls)
                isMouseOverUI = mouseState.X >= 0 && mouseState.X <= 250 && 
                                mouseState.Y >= 0 && mouseState.Y <= 800;
                
                // Check if mouse is over save button area (bottom right)
                bool isMouseOverSaveButton = mouseState.X >= GraphicsDevice.Viewport.Width - 160 && 
                                            mouseState.X <= GraphicsDevice.Viewport.Width &&
                                            mouseState.Y >= GraphicsDevice.Viewport.Height - 50 && 
                                            mouseState.Y <= GraphicsDevice.Viewport.Height;
                
                // Combine all UI areas
                isMouseOverAnyUI = isMouseOverUI || isMouseOverSaveButton;
            }
            
            // If mouse is over UI, stop any following movement immediately
            if (isMouseOverAnyUI && _isFollowingMouse)
            {
                if (_playerShip != null)
                {
                    _playerShip.StopMoving();
                }
                _isFollowingMouse = false;
            }
            
            // Left mouse button handling - click to move or hold to follow
            // Only process if mouse is NOT over UI (including save button)
            if (!isMouseOverAnyUI && mouseState.LeftButton == ButtonState.Pressed)
            {
                var screenPos = new Vector2(mouseState.X, mouseState.Y);
                
                if (!_wasLeftButtonPressed)
                {
                    // Just pressed - start tracking for click vs hold
                    _clickStartPosition = screenPos;
                    _isFollowingMouse = false;
                }
                else
                {
                    // Button is held - check if mouse moved enough to start following
                    var moveDistance = (screenPos - _clickStartPosition).Length();
                    if (moveDistance > 5f) // Threshold to distinguish click from drag
                    {
                        _isFollowingMouse = true;
                    }
                }
                
                // Convert screen coordinates to world coordinates
                var worldX = (screenPos.X - GraphicsDevice.Viewport.Width / 2f) / _cameraZoom + _cameraPosition.X;
                var worldY = (screenPos.Y - GraphicsDevice.Viewport.Height / 2f) / _cameraZoom + _cameraPosition.Y;
                var worldPosition = new Vector2(worldX, worldY);
                
                if (_isFollowingMouse)
                {
                    // Following mode - continuously update target
                    if (_playerShip != null)
                    {
                        _playerShip.SetTargetPosition(worldPosition);
                    }
                }
            }
            else if (!isMouseOverAnyUI && _wasLeftButtonPressed && mouseState.LeftButton == ButtonState.Released)
            {
                // Left mouse button was just released (and not over UI)
                if (_isFollowingMouse)
                {
                    // Was following - stop immediately
                    if (_playerShip != null)
                    {
                        _playerShip.StopMoving();
                    }
                }
                else
                {
                    // Was a click - move to clicked position
                    var screenPos = new Vector2(_previousMouseState.X, _previousMouseState.Y);
                    var worldX = (screenPos.X - GraphicsDevice.Viewport.Width / 2f) / _cameraZoom + _cameraPosition.X;
                    var worldY = (screenPos.Y - GraphicsDevice.Viewport.Height / 2f) / _cameraZoom + _cameraPosition.Y;
                    var worldPosition = new Vector2(worldX, worldY);
                    
                    if (_playerShip != null)
                    {
                        _playerShip.SetTargetPosition(worldPosition);
                    }
                }
                _isFollowingMouse = false;
            }
            
            _wasLeftButtonPressed = mouseState.LeftButton == ButtonState.Pressed;
            
            // Right mouse button to fire lasers
            if (mouseState.RightButton == ButtonState.Pressed && !_wasRightButtonPressed && !isMouseOverAnyUI)
            {
                // Fire lasers from player ship positions in the direction the ship is facing
                if (_playerShip != null)
                {
                    var shipTexture = _playerShip.GetTexture();
                    if (shipTexture == null) return;
                    
                    float textureCenterX = shipTexture.Width / 2f;
                    float textureCenterY = shipTexture.Height / 2f;
                    float shipRotation = _playerShip.Rotation;
                    float cos = (float)Math.Cos(shipRotation);
                    float sin = (float)Math.Sin(shipRotation);
                    
                    // Helper function to create laser from sprite coordinates
                    Action<float, float> fireLaserFromSpriteCoords = (float spriteX, float spriteY) =>
                    {
                        // Convert sprite coordinates to offset from ship center
                        float offsetX = spriteX - textureCenterX;
                        float offsetY = spriteY - textureCenterY;
                        
                        // Rotate the offset by ship's rotation to get world-space offset
                        float rotatedX = offsetX * cos - offsetY * sin;
                        float rotatedY = offsetX * sin + offsetY * cos;
                        
                        // Calculate laser spawn position
                        Vector2 laserSpawnPosition = _playerShip.Position + new Vector2(rotatedX, rotatedY);
                        
                        var laser = new Laser(laserSpawnPosition, shipRotation, GraphicsDevice);
                        _lasers.Add(laser);
                    };
                    
                    // Fire first laser from sprite coordinates (210, 50)
                    fireLaserFromSpriteCoords(210f, 50f);
                    
                    // Fire second laser from sprite coordinates (40, 50)
                    fireLaserFromSpriteCoords(40f, 50f);
                    
                    // Play laser fire sound effect with SFX volume
                    _laserFireSound?.Play(_sfxVolume, 0f, 0f);
                }
            }
            _wasRightButtonPressed = mouseState.RightButton == ButtonState.Pressed;
            
            // Toggle grid with G key
            if (keyboardState.IsKeyDown(Keys.G) && !_previousKeyboardState.IsKeyDown(Keys.G))
            {
                _gridVisible = !_gridVisible;
                if (_gridVisibleCheckBox != null)
                {
                    _gridVisibleCheckBox.IsChecked = _gridVisible;
                }
            }
            
            // Camera zoom with mouse wheel
            int scrollDelta = mouseState.ScrollWheelValue - _previousMouseState.ScrollWheelValue;
            if (scrollDelta != 0)
            {
                float zoomChange = scrollDelta > 0 ? ZoomSpeed : -ZoomSpeed;
                _cameraZoom = MathHelper.Clamp(_cameraZoom + zoomChange, MinZoom, MaxZoom);
                
                // Update zoom label
                if (_zoomLabel != null)
                {
                    _zoomLabel.Text = $"Zoom: {_cameraZoom:F2}x";
                }
            }
            
            // Update player ship aim target (mouse cursor position when not actively moving)
            if (_playerShip != null && !_playerShip.IsActivelyMoving())
            {
                // Convert mouse position to world coordinates for aiming
                var mouseScreenPos = new Vector2(mouseState.X, mouseState.Y);
                var mouseWorldX = (mouseScreenPos.X - GraphicsDevice.Viewport.Width / 2f) / _cameraZoom + _cameraPosition.X;
                var mouseWorldY = (mouseScreenPos.Y - GraphicsDevice.Viewport.Height / 2f) / _cameraZoom + _cameraPosition.Y;
                var mouseWorldPos = new Vector2(mouseWorldX, mouseWorldY);
                _playerShip.SetAimTarget(mouseWorldPos);
            }
            else if (_playerShip != null)
            {
                _playerShip.SetAimTarget(null); // Clear aim target when actively moving
            }
            
            // Update player ship first so we have current position
            _playerShip?.Update(gameTime);
            
            // Check for spacebar to smoothly pan camera back to player
            if (keyboardState.IsKeyDown(Keys.Space) && _playerShip != null)
            {
                // Start or continue panning to player
                _isPanningToPlayer = true;
            }
            
            // Camera movement with WASD - if any WASD key is pressed, use manual camera control and cancel panning
            bool isWASDPressed = keyboardState.IsKeyDown(Keys.W) || keyboardState.IsKeyDown(Keys.A) || 
                                 keyboardState.IsKeyDown(Keys.S) || keyboardState.IsKeyDown(Keys.D);
            
            if (isWASDPressed)
            {
                // Cancel panning and following when WASD is pressed
                _isPanningToPlayer = false;
                _cameraFollowingPlayer = false;
                
                // Manual camera control with WASD
                var movement = Vector2.Zero;
                if (keyboardState.IsKeyDown(Keys.A))
                    movement.X -= 1f; // Move left
                if (keyboardState.IsKeyDown(Keys.D))
                    movement.X += 1f; // Move right
                if (keyboardState.IsKeyDown(Keys.W))
                    movement.Y -= 1f; // Move up
                if (keyboardState.IsKeyDown(Keys.S))
                    movement.Y += 1f; // Move down
                
                // Normalize diagonal movement
                if (movement.Length() > 0)
                {
                    movement.Normalize();
                    var targetVelocity = movement * CameraSpeed;
                    // Apply inertia to velocity
                    _cameraVelocity = Vector2.Lerp(_cameraVelocity, targetVelocity, (1f - _cameraInertia));
                }
                else
                {
                    // Apply deceleration when no input
                    _cameraVelocity *= _cameraInertia;
                }
            }
            else
            {
                // Apply deceleration when not using WASD
                if (_cameraVelocity.Length() > 1f)
                {
                    _cameraVelocity *= _cameraInertia;
                }
                else
                {
                    _cameraVelocity = Vector2.Zero;
                    // Reattach camera to player when WASD is released and velocity stops
                    if (!_cameraFollowingPlayer && _playerShip != null)
                    {
                        _cameraFollowingPlayer = true;
                    }
                }
            }
            
            // Apply velocity to camera position, then clamp to map bounds
            _cameraPosition += _cameraVelocity * deltaTime;
            
            // Clamp camera position to map bounds (accounting for viewport size and zoom)
            var viewWidth = GraphicsDevice.Viewport.Width / _cameraZoom;
            var viewHeight = GraphicsDevice.Viewport.Height / _cameraZoom;
            var minX = viewWidth / 2f;
            var maxX = MapSize - viewWidth / 2f;
            var minY = viewHeight / 2f;
            var maxY = MapSize - viewHeight / 2f;
            
            _cameraPosition.X = MathHelper.Clamp(_cameraPosition.X, minX, maxX);
            _cameraPosition.Y = MathHelper.Clamp(_cameraPosition.Y, minY, maxY);
            
            // Stop velocity if we hit a boundary
            if ((_cameraPosition.X <= minX && _cameraVelocity.X < 0) || 
                (_cameraPosition.X >= maxX && _cameraVelocity.X > 0))
            {
                _cameraVelocity.X = 0;
            }
            if ((_cameraPosition.Y <= minY && _cameraVelocity.Y < 0) || 
                (_cameraPosition.Y >= maxY && _cameraVelocity.Y > 0))
            {
                _cameraVelocity.Y = 0;
            }
            
            if (_isPanningToPlayer && _playerShip != null)
            {
                // Smoothly pan camera back to player position
                var targetPosition = _playerShip.Position;
                var direction = targetPosition - _cameraPosition;
                var distance = direction.Length();
                
                if (distance > 1f)
                {
                    // Move camera towards player
                    direction.Normalize();
                    var moveDistance = _cameraPanSpeed * deltaTime;
                    
                    // Don't overshoot
                    if (moveDistance > distance)
                    {
                        _cameraPosition = targetPosition;
                        _isPanningToPlayer = false;
                        _cameraFollowingPlayer = true; // Start following player after pan completes
                    }
                    else
                    {
                        _cameraPosition += direction * moveDistance;
                    }
                    
                    // Clamp to map bounds during panning
                    var panViewWidth = GraphicsDevice.Viewport.Width / _cameraZoom;
                    var panViewHeight = GraphicsDevice.Viewport.Height / _cameraZoom;
                    var panMinX = panViewWidth / 2f;
                    var panMaxX = MapSize - panViewWidth / 2f;
                    var panMinY = panViewHeight / 2f;
                    var panMaxY = MapSize - panViewHeight / 2f;
                    
                    _cameraPosition.X = MathHelper.Clamp(_cameraPosition.X, panMinX, panMaxX);
                    _cameraPosition.Y = MathHelper.Clamp(_cameraPosition.Y, panMinY, panMaxY);
                    
                    // If we hit a boundary, stop panning
                    if ((_cameraPosition.X <= panMinX || _cameraPosition.X >= panMaxX || 
                         _cameraPosition.Y <= panMinY || _cameraPosition.Y >= panMaxY) && 
                        _cameraPosition != targetPosition)
                    {
                        _isPanningToPlayer = false;
                        _cameraFollowingPlayer = true;
                    }
                }
                else
                {
                    // Reached player position
                    _cameraPosition = targetPosition;
                    _isPanningToPlayer = false;
                    _cameraFollowingPlayer = true; // Start following player after pan completes
                    
                    // Clamp final position to map bounds
                    var finalViewWidth = GraphicsDevice.Viewport.Width / _cameraZoom;
                    var finalViewHeight = GraphicsDevice.Viewport.Height / _cameraZoom;
                    var finalMinX = finalViewWidth / 2f;
                    var finalMaxX = MapSize - finalViewWidth / 2f;
                    var finalMinY = finalViewHeight / 2f;
                    var finalMaxY = MapSize - finalViewHeight / 2f;
                    
                    _cameraPosition.X = MathHelper.Clamp(_cameraPosition.X, finalMinX, finalMaxX);
                    _cameraPosition.Y = MathHelper.Clamp(_cameraPosition.Y, finalMinY, finalMaxY);
                }
            }
            else if (_cameraFollowingPlayer && _playerShip != null)
            {
                // Keep camera on player after panning completes, but clamp to map bounds
                _cameraPosition = _playerShip.Position;
                
                // Clamp to map bounds
                var followViewWidth = GraphicsDevice.Viewport.Width / _cameraZoom;
                var followViewHeight = GraphicsDevice.Viewport.Height / _cameraZoom;
                var followMinX = followViewWidth / 2f;
                var followMaxX = MapSize - followViewWidth / 2f;
                var followMinY = followViewHeight / 2f;
                var followMaxY = MapSize - followViewHeight / 2f;
                
                _cameraPosition.X = MathHelper.Clamp(_cameraPosition.X, followMinX, followMaxX);
                _cameraPosition.Y = MathHelper.Clamp(_cameraPosition.Y, followMinY, followMaxY);
            }
            // If neither WASD nor spacebar and not following, camera stays where it is
            
            // Update friendly ships with collision avoidance
            foreach (var friendlyShip in _friendlyShips)
            {
                // Collision avoidance: steer away from nearby ships
                const float avoidanceRadius = 300f; // Distance to start avoiding
                const float avoidanceForce = 200f; // How strongly to avoid (pixels per second)
                Vector2 avoidanceVector = Vector2.Zero;
                
                foreach (var otherShip in _friendlyShips)
                {
                    if (otherShip == friendlyShip) continue;
                    
                    Vector2 direction = friendlyShip.Position - otherShip.Position;
                    float distance = direction.Length();
                    
                    if (distance < avoidanceRadius && distance > 0.1f)
                    {
                        // Calculate avoidance force (stronger when closer)
                        float avoidanceStrength = (avoidanceRadius - distance) / avoidanceRadius;
                        direction.Normalize();
                        avoidanceVector += direction * avoidanceStrength * avoidanceForce;
                    }
                }
                
                // Apply avoidance by pushing position away from nearby ships
                if (avoidanceVector.LengthSquared() > 0.1f)
                {
                    // Push the ship away from nearby ships
                    friendlyShip.Position += avoidanceVector * deltaTime;
                    
                    // If ship is moving, redirect it away from the collision
                    if (friendlyShip.IsActivelyMoving())
                    {
                        // Set a new target in the avoidance direction to steer away
                        Vector2 avoidanceTarget = friendlyShip.Position + avoidanceVector * 150f;
                        // Don't clamp - allow ships to go outside map bounds
                        friendlyShip.SetTargetPosition(avoidanceTarget);
                    }
                }
                
                friendlyShip.Update(gameTime);
                
                // Check if ship is near or outside map edges and fade out
                const float fadeStartDistance = 200f; // Start fading when within 200 pixels of edge
                const float fadeSpeed = 2.0f; // Fade speed per second
                float distanceToEdge = Math.Min(
                    Math.Min(friendlyShip.Position.X, MapSize - friendlyShip.Position.X),
                    Math.Min(friendlyShip.Position.Y, MapSize - friendlyShip.Position.Y)
                );
                
                if (distanceToEdge < fadeStartDistance)
                {
                    // Fade out as ship approaches edge
                    float fadeAmount = (fadeStartDistance - distanceToEdge) / fadeStartDistance;
                    if (_friendlyShipAlpha.ContainsKey(friendlyShip))
                    {
                        _friendlyShipAlpha[friendlyShip] = Math.Max(0f, 1.0f - fadeAmount);
                    }
                    else
                    {
                        _friendlyShipAlpha[friendlyShip] = Math.Max(0f, 1.0f - fadeAmount);
                    }
                }
                else if (friendlyShip.Position.X < -500 || friendlyShip.Position.X > MapSize + 500 ||
                         friendlyShip.Position.Y < -500 || friendlyShip.Position.Y > MapSize + 500)
                {
                    // Ship is far outside map - fully faded, respawn on opposite side
                    if (_friendlyShipAlpha.ContainsKey(friendlyShip) && _friendlyShipAlpha[friendlyShip] <= 0.1f)
                    {
                        // Respawn on opposite side of map
                        float newX, newY;
                        if (friendlyShip.Position.X < 0)
                        {
                            newX = MapSize + 200f; // Spawn on right edge
                        }
                        else if (friendlyShip.Position.X > MapSize)
                        {
                            newX = -200f; // Spawn on left edge
                        }
                        else
                        {
                            newX = friendlyShip.Position.X;
                        }
                        
                        if (friendlyShip.Position.Y < 0)
                        {
                            newY = MapSize + 200f; // Spawn on bottom edge
                        }
                        else if (friendlyShip.Position.Y > MapSize)
                        {
                            newY = -200f; // Spawn on top edge
                        }
                        else
                        {
                            newY = friendlyShip.Position.Y;
                        }
                        
                        friendlyShip.Position = new Vector2(newX, newY);
                        _friendlyShipAlpha[friendlyShip] = 1.0f; // Reset to fully visible
                        
                        // Set a target across the map
                        float targetX = MapSize - newX;
                        float targetY = MapSize - newY;
                        friendlyShip.SetTargetPosition(new Vector2(targetX, targetY));
                    }
                }
                else
                {
                    // Ship is inside map bounds - ensure fully visible
                    if (_friendlyShipAlpha.ContainsKey(friendlyShip))
                    {
                        _friendlyShipAlpha[friendlyShip] = Math.Min(1.0f, _friendlyShipAlpha[friendlyShip] + fadeSpeed * deltaTime);
                    }
                    else
                    {
                        _friendlyShipAlpha[friendlyShip] = 1.0f;
                    }
                }
                
                // Randomly decide to move or idle, or fly across map
                // If not currently moving, periodically pick a new random target
                if (!friendlyShip.IsActivelyMoving() && _random.NextDouble() < 0.002f)
                {
                    // 30% chance to fly across the whole map (edge to edge)
                    if (_random.NextDouble() < 0.3f)
                    {
                        // Pick a random edge to start from and opposite edge to fly to
                        int startEdge = _random.Next(4); // 0=top, 1=right, 2=bottom, 3=left
                        float startX, startY, targetX, targetY;
                        
                        switch (startEdge)
                        {
                            case 0: // Top edge
                                startX = (float)(_random.NextDouble() * MapSize);
                                startY = -200f;
                                targetX = startX;
                                targetY = MapSize + 200f; // Bottom edge
                                break;
                            case 1: // Right edge
                                startX = MapSize + 200f;
                                startY = (float)(_random.NextDouble() * MapSize);
                                targetX = -200f; // Left edge
                                targetY = startY;
                                break;
                            case 2: // Bottom edge
                                startX = (float)(_random.NextDouble() * MapSize);
                                startY = MapSize + 200f;
                                targetX = startX;
                                targetY = -200f; // Top edge
                                break;
                            default: // Left edge
                                startX = -200f;
                                startY = (float)(_random.NextDouble() * MapSize);
                                targetX = MapSize + 200f; // Right edge
                                targetY = startY;
                                break;
                        }
                        
                        friendlyShip.Position = new Vector2(startX, startY);
                        friendlyShip.SetTargetPosition(new Vector2(targetX, targetY));
                        _friendlyShipAlpha[friendlyShip] = 1.0f; // Start fully visible
                    }
                    else
                    {
                        // Normal random movement within map
                        float margin = 500f;
                        float targetX = (float)(_random.NextDouble() * (MapSize - margin * 2) + margin);
                        float targetY = (float)(_random.NextDouble() * (MapSize - margin * 2) + margin);
                        friendlyShip.SetTargetPosition(new Vector2(targetX, targetY));
                    }
                    
                    // Randomly change speed when picking a new target (50% chance)
                    if (_random.NextDouble() < 0.5f)
                    {
                        friendlyShip.MoveSpeed = (float)(_random.NextDouble() * 400f + 100f); // 100-500 speed
                    }
                }
            }
            
            // Update lasers
            foreach (var laser in _lasers)
            {
                laser.Update(gameTime);
            }
            
            // Remove lasers that are off screen or too far away
            _lasers.RemoveAll(laser => 
                !laser.IsActive || 
                laser.Position.X < -1000 || laser.Position.X > 9192 ||
                laser.Position.Y < -1000 || laser.Position.Y > 9192
            );
            
            // Update save confirmation timer
            if (_saveConfirmationTimer > 0f)
            {
                _saveConfirmationTimer -= deltaTime;
                if (_saveConfirmationTimer <= 0f)
                {
                    _saveConfirmationTimer = 0f;
                    if (_saveConfirmationLabel != null)
                    {
                        _saveConfirmationLabel.Visible = false;
                    }
                }
            }
            
            // Update Myra UI input
            _desktop?.UpdateInput();
            
            // Return to menu on Escape
            if (keyboardState.IsKeyDown(Keys.Escape))
            {
                var sceneManager = (SceneManager)Game.Services.GetService(typeof(SceneManager));
                sceneManager?.ChangeScene(new MainMenuScene(Game));
            }
            
            _previousMouseState = mouseState;
            _previousKeyboardState = keyboardState;
        }

        public override void Draw(GameTime gameTime, SpriteBatch spriteBatch)
        {
            GraphicsDevice.Clear(Color.Black);

            // Apply zoom transform
            var transform = Matrix.CreateScale(_cameraZoom) * 
                           Matrix.CreateTranslation(
                               GraphicsDevice.Viewport.Width / 2f - _cameraPosition.X * _cameraZoom,
                               GraphicsDevice.Viewport.Height / 2f - _cameraPosition.Y * _cameraZoom,
                               0f
                           );
            
            spriteBatch.Begin(
                SpriteSortMode.Deferred, 
                BlendState.AlphaBlend, 
                SamplerState.LinearWrap, // Use wrap mode for seamless tiling
                DepthStencilState.None, 
                RasterizerState.CullNone,
                null,
                transform
            );

            // Draw galaxy texture in 2x2 tile pattern
            if (_galaxyTexture != null)
            {
                // Calculate scale for each tile to cover the map
                const float mapSize = 8192f;
                const int tilesX = 2;
                const int tilesY = 2;
                float tileSize = mapSize / tilesX; // Each tile is 4096x4096
                
                float scaleX = tileSize / _tileSize.X;
                float scaleY = tileSize / _tileSize.Y;
                float scale = Math.Max(scaleX, scaleY);
                
                // Draw 2x2 grid of galaxy tiles
                for (int y = 0; y < tilesY; y++)
                {
                    for (int x = 0; x < tilesX; x++)
                    {
                        var position = new Vector2(
                            x * tileSize,
                            y * tileSize
                        );
                        
                        spriteBatch.Draw(
                            _galaxyTexture,
                            position,
                            null,
                            Color.White,
                            0f,
                            Vector2.Zero,
                            scale,
                            SpriteEffects.None,
                            0f
                        );
                    }
                }
            }
            
            // Draw grid lines (in world space, before ship so it draws under)
            if (_gridPixelTexture != null && _gridVisible)
            {
                const float mapSize = 8192f;
                int gridSize = _gridSize;
                
                // Calculate visible grid range based on camera view
                // Add padding to ensure complete grid lines are drawn at edges
                var viewWidth = GraphicsDevice.Viewport.Width / _cameraZoom;
                var viewHeight = GraphicsDevice.Viewport.Height / _cameraZoom;
                var padding = gridSize; // Add one grid cell padding on each side
                
                // Use Math.Floor for smoother grid positioning when camera moves
                var minX = (int)Math.Floor((_cameraPosition.X - viewWidth / 2f - padding) / gridSize) * gridSize;
                var maxX = (int)Math.Ceiling((_cameraPosition.X + viewWidth / 2f + padding) / gridSize) * gridSize;
                var minY = (int)Math.Floor((_cameraPosition.Y - viewHeight / 2f - padding) / gridSize) * gridSize;
                var maxY = (int)Math.Ceiling((_cameraPosition.Y + viewHeight / 2f + padding) / gridSize) * gridSize;
                
                // Clamp to map bounds
                minX = Math.Max(0, minX);
                maxX = Math.Min((int)mapSize, maxX);
                minY = Math.Max(0, minY);
                maxY = Math.Min((int)mapSize, maxY);
                
                // Draw vertical lines
                for (int x = minX; x <= maxX; x += gridSize)
                {
                    var start = new Vector2(x, minY);
                    var end = new Vector2(x, maxY);
                    var length = (end - start).Length();
                    var angle = (float)Math.Atan2(end.Y - start.Y, end.X - start.X);
                    
                    spriteBatch.Draw(
                        _gridPixelTexture,
                        start,
                        null,
                        Color.White,
                        angle,
                        new Vector2(0, 0.5f),
                        new Vector2(length, 2f), // Scale with zoom for visibility
                        SpriteEffects.None,
                        0f
                    );
                }
                
                // Draw horizontal lines
                for (int y = minY; y <= maxY; y += gridSize)
                {
                    var start = new Vector2(minX, y);
                    var end = new Vector2(maxX, y);
                    var length = (end - start).Length();
                    var angle = (float)Math.Atan2(end.Y - start.Y, end.X - start.X);
                    
                    spriteBatch.Draw(
                        _gridPixelTexture,
                        start,
                        null,
                        Color.White,
                        angle,
                        new Vector2(0, 0.5f),
                        new Vector2(length, 2f), // Scale with zoom for visibility
                        SpriteEffects.None,
                        0f
                    );
                }
            }
            
            // Draw player ship
            _playerShip?.Draw(spriteBatch);
            
            // Draw friendly ships with fade effect
            foreach (var friendlyShip in _friendlyShips)
            {
                float alpha = _friendlyShipAlpha.ContainsKey(friendlyShip) ? _friendlyShipAlpha[friendlyShip] : 1.0f;
                if (alpha > 0.01f) // Only draw if not fully faded
                {
                    // Draw ship with alpha (includes engine trail)
                    friendlyShip.Draw(spriteBatch, alpha);
                }
            }

            spriteBatch.End();
            
            // Draw lasers with additive blending for glow effect
            spriteBatch.Begin(
                SpriteSortMode.Deferred,
                BlendState.Additive, // Additive blending for laser glow
                SamplerState.LinearClamp,
                DepthStencilState.None,
                RasterizerState.CullNone,
                null,
                transform
            );
            
            foreach (var laser in _lasers)
            {
                laser.Draw(spriteBatch);
            }

            spriteBatch.End();
            
            // Draw minimap in upper right corner
            DrawMinimap(spriteBatch);
            
            // Draw UI overlay (zoom level) on top
            _desktop?.Render();
            _saveButtonDesktop?.Render();
            
            // Draw preview screen if active
            Texture2D? shipTexture = _previewShipIndex == 0 ? _previewShip1Texture : _previewShip2Texture;
                
            if (_isPreviewActive && shipTexture != null && _previewDesktop != null)
            {
                
                spriteBatch.Begin();
                
                // Draw semi-transparent background
                var bgTexture = new Texture2D(GraphicsDevice, 1, 1);
                bgTexture.SetData(new[] { new Color(0, 0, 0, 200) });
                int panelX = (GraphicsDevice.Viewport.Width - 700) / 2;
                int panelY = (GraphicsDevice.Viewport.Height - 700) / 2;
                spriteBatch.Draw(bgTexture, new Rectangle(panelX, panelY, 700, 700), Color.White);
                
                // Draw ship sprite centered in preview panel
                int spriteX = panelX + 350 - (shipTexture?.Width ?? 0) / 2;
                int spriteY = panelY + 350 - (shipTexture?.Height ?? 0) / 2;
                
                // Draw box around sprite
                if (_gridPixelTexture != null && shipTexture != null)
                {
                    int borderThickness = 2;
                    Color borderColor = Color.White;
                    
                    // Top line
                    spriteBatch.Draw(
                        _gridPixelTexture,
                        new Rectangle(spriteX - borderThickness, spriteY - borderThickness, shipTexture.Width + borderThickness * 2, borderThickness),
                        borderColor
                    );
                    
                    // Bottom line
                    spriteBatch.Draw(
                        _gridPixelTexture,
                        new Rectangle(spriteX - borderThickness, spriteY + shipTexture.Height, shipTexture.Width + borderThickness * 2, borderThickness),
                        borderColor
                    );
                    
                    // Left line
                    spriteBatch.Draw(
                        _gridPixelTexture,
                        new Rectangle(spriteX - borderThickness, spriteY - borderThickness, borderThickness, shipTexture.Height + borderThickness * 2),
                        borderColor
                    );
                    
                    // Right line
                    spriteBatch.Draw(
                        _gridPixelTexture,
                        new Rectangle(spriteX + shipTexture.Width, spriteY - borderThickness, borderThickness, shipTexture.Height + borderThickness * 2),
                        borderColor
                    );
                }
                
                spriteBatch.Draw(
                    shipTexture,
                    new Vector2(spriteX, spriteY),
                    null,
                    Color.White,
                    0f,
                    Vector2.Zero,
                    1f,
                    SpriteEffects.None,
                    0f
                );
                
                spriteBatch.End();
                
                // Render preview UI (labels, buttons)
                _previewDesktop.Render();
            }
        }
        
        private void UpdatePreviewShipLabel()
        {
            if (_previewShipLabel == null) return;
            
            string className = _previewShipIndex == 0 ? "PlayerShip" : "ShipFriendly";
            _previewShipLabel.Text = $"{className} ({_previewShipIndex + 1}/2)";
        }
        
        private void SwitchShipClass(int classIndex)
        {
            if (classIndex == _currentShipClassIndex) return;
            
            // Save current ship settings before switching
            SaveCurrentShipSettings();
            
            // Switch ship class
            _currentShipClassIndex = classIndex;
            var mapCenter = _playerShip?.Position ?? new Vector2(4096f, 4096f);
            
            // Create new ship instance
            if (classIndex == 0)
            {
                _playerShip = new PlayerShip(GraphicsDevice, Content);
            }
            else
            {
                _playerShip = new ShipFriendly(GraphicsDevice, Content);
            }
            _playerShip.Position = mapCenter;
            
            // Update UI label
            if (_shipClassLabel != null)
            {
                _shipClassLabel.Text = $"Ship Class: {(_currentShipClassIndex == 0 ? "PlayerShip" : "ShipFriendly")}";
            }
            
            // Sync preview index if preview is active
            if (_isPreviewActive)
            {
                _previewShipIndex = _currentShipClassIndex;
                UpdatePreviewShipLabel();
            }
            
            // Load settings for the new class
            LoadCurrentShipSettings();
        }
        
        private void SaveCurrentShipSettings()
        {
            if (_playerShip == null) return;
            
            string className = _currentShipClassIndex == 0 ? "PlayerShip" : "ShipFriendly";
            var filePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, $"settings_{className}.json");
            
            try
            {
                var settings = new
                {
                    ShipSpeed = _playerShip.MoveSpeed,
                    TurnRate = _playerShip.RotationSpeed,
                    Inertia = _playerShip.Inertia,
                    AimRotationSpeed = _playerShip.AimRotationSpeed
                };
                
                var json = JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(filePath, json);
                System.Console.WriteLine($"Saved {className} settings to: {filePath}");
            }
            catch (Exception ex)
            {
                System.Console.WriteLine($"Failed to save {className} settings: {ex.Message}");
            }
        }
        
        private void LoadCurrentShipSettings()
        {
            if (_playerShip == null) return;
            
            string className = _currentShipClassIndex == 0 ? "PlayerShip" : "ShipFriendly";
            var filePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, $"settings_{className}.json");
            
            try
            {
                if (File.Exists(filePath))
                {
                    var json = File.ReadAllText(filePath);
                    var settings = JsonSerializer.Deserialize<JsonElement>(json);
                    
                    // Load ship speed
                    if (settings.TryGetProperty("ShipSpeed", out var shipSpeedElement))
                    {
                        var shipSpeed = shipSpeedElement.GetSingle();
                        _playerShip.MoveSpeed = shipSpeed;
                        if (_speedSlider != null) _speedSlider.Value = shipSpeed;
                        if (_speedLabel != null) _speedLabel.Text = $"Speed: {shipSpeed:F0}";
                    }
                    
                    // Load turn rate
                    if (settings.TryGetProperty("TurnRate", out var turnRateElement))
                    {
                        var turnRate = turnRateElement.GetSingle();
                        _playerShip.RotationSpeed = turnRate;
                        if (_turnRateSlider != null) _turnRateSlider.Value = turnRate;
                        if (_turnRateLabel != null) _turnRateLabel.Text = $"Turn Rate: {turnRate:F1}";
                    }
                    
                    // Load inertia
                    if (settings.TryGetProperty("Inertia", out var inertiaElement))
                    {
                        var inertia = inertiaElement.GetSingle();
                        _playerShip.Inertia = inertia;
                        if (_inertiaSlider != null) _inertiaSlider.Value = inertia;
                        if (_inertiaLabel != null) _inertiaLabel.Text = $"Inertia: {inertia:F2}";
                    }
                    
                    // Load aim rotation speed
                    if (settings.TryGetProperty("AimRotationSpeed", out var aimRotationSpeedElement))
                    {
                        var aimRotationSpeed = aimRotationSpeedElement.GetSingle();
                        _playerShip.AimRotationSpeed = aimRotationSpeed;
                        if (_aimRotationSpeedSlider != null) _aimRotationSpeedSlider.Value = aimRotationSpeed;
                        if (_aimRotationSpeedLabel != null) _aimRotationSpeedLabel.Text = $"Aim Rotation Speed: {aimRotationSpeed:F1}";
                    }
                    
                    System.Console.WriteLine($"Loaded {className} settings from: {filePath}");
                }
                else
                {
                    System.Console.WriteLine($"No saved settings found for {className}, using defaults");
                }
            }
            catch (Exception ex)
            {
                System.Console.WriteLine($"Failed to load {className} settings: {ex.Message}");
            }
        }
        
        private void DrawMinimap(SpriteBatch spriteBatch)
        {
            if (_minimapBackgroundTexture == null || _minimapPlayerDotTexture == null || _minimapFriendlyDotTexture == null || _minimapViewportOutlineTexture == null)
                return;
                
            int minimapX = GraphicsDevice.Viewport.Width - MinimapSize - 10;
            int minimapY = 10;
            
            // Calculate minimap scale (minimap size / map size)
            float minimapScale = MinimapSize / MapSize;
            
            // Draw minimap background
            spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend);
            spriteBatch.Draw(_minimapBackgroundTexture, new Rectangle(minimapX, minimapY, MinimapSize, MinimapSize), Color.White);
            
            // Draw galaxy background in minimap (in screen space)
            if (_galaxyTexture != null)
            {
                // Draw a single scaled galaxy texture to fill the minimap
                // We'll use a source rectangle to show a portion of the galaxy centered on the player
                var minimapRect = new Rectangle(minimapX, minimapY, MinimapSize, MinimapSize);
                spriteBatch.Draw(
                    _galaxyTexture,
                    minimapRect,
                    null,
                    Color.White * 0.6f, // Slightly dimmed for minimap
                    0f,
                    Vector2.Zero,
                    SpriteEffects.None,
                    0f
                );
            }
            
            // Convert world positions to minimap screen positions
            Vector2 WorldToMinimap(Vector2 worldPos)
            {
                return new Vector2(
                    minimapX + worldPos.X * minimapScale,
                    minimapY + worldPos.Y * minimapScale
                );
            }
            
            // Draw friendly ships on minimap
            foreach (var friendlyShip in _friendlyShips)
            {
                var friendlyScreenPos = WorldToMinimap(friendlyShip.Position);
                var friendlyDotSize = 3f; // Slightly smaller than player dot
                
                spriteBatch.Draw(
                    _minimapFriendlyDotTexture,
                    friendlyScreenPos,
                    null,
                    Color.Lime, // Green for friendly ships
                    0f,
                    Vector2.Zero,
                    friendlyDotSize,
                    SpriteEffects.None,
                    0f
                );
            }
            
            // Draw player ship position on minimap
            if (_playerShip != null)
            {
                var playerScreenPos = WorldToMinimap(_playerShip.Position);
                var playerDotSize = 4f; // 4 pixels on screen
                
                spriteBatch.Draw(
                    _minimapPlayerDotTexture,
                    playerScreenPos,
                    null,
                    Color.Cyan,
                    0f,
                    Vector2.Zero,
                    playerDotSize,
                    SpriteEffects.None,
                    0f
                );
            }
            
            // Draw camera viewport rectangle on minimap
            var viewWidth = GraphicsDevice.Viewport.Width / _cameraZoom;
            var viewHeight = GraphicsDevice.Viewport.Height / _cameraZoom;
            
            var cameraTopLeft = WorldToMinimap(new Vector2(
                _cameraPosition.X - viewWidth / 2f,
                _cameraPosition.Y - viewHeight / 2f
            ));
            
            var cameraBottomRight = WorldToMinimap(new Vector2(
                _cameraPosition.X + viewWidth / 2f,
                _cameraPosition.Y + viewHeight / 2f
            ));
            
            var cameraRect = new Rectangle(
                (int)cameraTopLeft.X,
                (int)cameraTopLeft.Y,
                (int)(cameraBottomRight.X - cameraTopLeft.X),
                (int)(cameraBottomRight.Y - cameraTopLeft.Y)
            );
            
            // Clamp camera rect to minimap bounds
            cameraRect.X = Math.Max(minimapX, Math.Min(cameraRect.X, minimapX + MinimapSize));
            cameraRect.Y = Math.Max(minimapY, Math.Min(cameraRect.Y, minimapY + MinimapSize));
            cameraRect.Width = Math.Min(cameraRect.Width, minimapX + MinimapSize - cameraRect.X);
            cameraRect.Height = Math.Min(cameraRect.Height, minimapY + MinimapSize - cameraRect.Y);
            
            // Draw camera viewport rectangle outline (2 pixels thick)
            const float lineWidth = 2f;
            Color cameraColor = new Color(255, 255, 255, 128); // White, 50% alpha (128/255)
            
            // Top line
            if (cameraRect.Width > 0 && cameraRect.Y >= minimapY)
            {
                spriteBatch.Draw(
                    _minimapViewportOutlineTexture,
                    new Rectangle(cameraRect.X, cameraRect.Y, cameraRect.Width, (int)lineWidth),
                    cameraColor
                );
            }
            
            // Bottom line
            if (cameraRect.Width > 0 && cameraRect.Y + cameraRect.Height <= minimapY + MinimapSize)
            {
                spriteBatch.Draw(
                    _minimapViewportOutlineTexture,
                    new Rectangle(cameraRect.X, cameraRect.Y + cameraRect.Height - (int)lineWidth, cameraRect.Width, (int)lineWidth),
                    cameraColor
                );
            }
            
            // Left line
            if (cameraRect.Height > 0 && cameraRect.X >= minimapX)
            {
                spriteBatch.Draw(
                    _minimapViewportOutlineTexture,
                    new Rectangle(cameraRect.X, cameraRect.Y, (int)lineWidth, cameraRect.Height),
                    cameraColor
                );
            }
            
            // Right line
            if (cameraRect.Height > 0 && cameraRect.X + cameraRect.Width <= minimapX + MinimapSize)
            {
                spriteBatch.Draw(
                    _minimapViewportOutlineTexture,
                    new Rectangle(cameraRect.X + cameraRect.Width - (int)lineWidth, cameraRect.Y, (int)lineWidth, cameraRect.Height),
                    cameraColor
                );
            }
            
            spriteBatch.End();
        }
        
        public override void UnloadContent()
        {
            // Stop and dispose sound effects
            try
            {
                if (_backgroundMusicInstance != null)
                {
                    _backgroundMusicInstance.Stop();
                    _backgroundMusicInstance.Dispose();
                    _backgroundMusicInstance = null;
                }
                if (_shipIdleSound != null)
                {
                    _shipIdleSound.Stop();
                    _shipIdleSound.Dispose();
                    _shipIdleSound = null;
                }
                if (_shipFlySound != null)
                {
                    _shipFlySound.Stop();
                    _shipFlySound.Dispose();
                    _shipFlySound = null;
                }
            }
            catch { }
            base.UnloadContent();
        }
    }
}

