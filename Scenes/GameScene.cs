using System;
using System.IO;
using System.Text.Json;
using Microsoft.Xna.Framework;
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
        private bool _gridVisible = true; // Grid visibility toggle
        
        // Camera position and zoom
        private Vector2 _cameraPosition;
        private float _cameraZoom = 1.0f; // Start zoomed in
        public float CameraSpeed { get; set; } = 200f; // pixels per second
        private const float MinZoom = 0.40f; // Furthest (most zoomed out)
        private const float MaxZoom = 1.10f; // Closest (most zoomed in)
        private const float ZoomSpeed = 0.1f;
        
        // Player ship
        private PlayerShip? _playerShip;
        
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
        private bool _cameraFollowingPlayer = false; // Track if camera should follow player
        private float _cameraPanSpeed = 800f; // pixels per second for smooth panning (faster)

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
            
            // Calculate map center position first
            const float mapSize = 8192f;
            var mapCenter = new Vector2(mapSize / 2f, mapSize / 2f);
            
            // Create player ship at map center
            _playerShip = new PlayerShip(GraphicsDevice, Content);
            _playerShip.Position = mapCenter;
            
            // Initialize camera to center on player
            _cameraPosition = mapCenter;
            
            // Create pixel texture for drawing grid lines (strong, visible color)
            _gridPixelTexture = new Texture2D(GraphicsDevice, 1, 1);
            _gridPixelTexture.SetData(new[] { new Color(150, 150, 150, 220) }); // Strong gray with high opacity
            
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
            grid.RowsProportions.Add(new Proportion(ProportionType.Auto)); // Inertia label
            grid.RowsProportions.Add(new Proportion(ProportionType.Auto)); // Inertia slider
            
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
            
            // Grid size label - bright magenta for visibility
            _gridSizeLabel = new Label
            {
                Text = $"Grid Size: {_gridSize}",
                TextColor = Color.Magenta,
                GridColumn = 0,
                GridRow = 7,
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top,
                Padding = new Myra.Graphics2D.Thickness(10, 10, 0, 0)
            };
            grid.Widgets.Add(_gridSizeLabel);
            
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
            
            // Inertia label - bright purple for visibility
            _inertiaLabel = new Label
            {
                Text = $"Inertia: {(_playerShip?.Inertia ?? 0.9f):F2}",
                TextColor = new Color(255, 100, 255), // Purple/magenta
                GridColumn = 0,
                GridRow = 11,
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
                GridRow = 12,
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
                }
            };
            grid.Widgets.Add(_inertiaSlider);
            
            _desktop.Root = grid;
            
            // Update zoom label with initial zoom
            _zoomLabel.Text = $"Zoom: {_cameraZoom:F2}x";
            
            // Create save button in lower right
            _saveButtonDesktop = new Desktop();
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
            var saveButtonPanel = new Panel();
            saveButtonPanel.Widgets.Add(_saveButton);
            
            // Save confirmation label (initially hidden)
            _saveConfirmationLabel = new Label
            {
                Text = "Settings Saved!",
                TextColor = Color.Lime,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Visible = false
            };
            saveButtonPanel.Widgets.Add(_saveConfirmationLabel);
            
            _saveButtonDesktop.Root = saveButtonPanel;
            
            // Load saved settings after UI is initialized
            LoadSettings();
            
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
                    
                    // Load ship speed
                    if (settings.TryGetProperty("ShipSpeed", out var shipSpeedElement))
                    {
                        var shipSpeed = shipSpeedElement.GetSingle();
                        System.Console.WriteLine($"Loading ShipSpeed: {shipSpeed}");
                        if (_playerShip != null)
                        {
                            _playerShip.MoveSpeed = shipSpeed;
                        }
                        if (_speedSlider != null)
                        {
                            _speedSlider.Value = shipSpeed;
                        }
                        if (_speedLabel != null)
                        {
                            _speedLabel.Text = $"Speed: {shipSpeed:F0}";
                        }
                    }
                    
                    // Load turn rate
                    if (settings.TryGetProperty("TurnRate", out var turnRateElement))
                    {
                        var turnRate = turnRateElement.GetSingle();
                        System.Console.WriteLine($"Loading TurnRate: {turnRate}");
                        if (_playerShip != null)
                        {
                            _playerShip.RotationSpeed = turnRate;
                        }
                        if (_turnRateSlider != null)
                        {
                            _turnRateSlider.Value = turnRate;
                        }
                        if (_turnRateLabel != null)
                        {
                            _turnRateLabel.Text = $"Turn Rate: {turnRate:F1}";
                        }
                    }
                    
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
                    
                    // Load inertia
                    if (settings.TryGetProperty("Inertia", out var inertiaElement))
                    {
                        var inertia = inertiaElement.GetSingle();
                        System.Console.WriteLine($"Loading Inertia: {inertia}");
                        inertia = MathHelper.Clamp(inertia, 0f, 0.995f);
                        if (_playerShip != null)
                        {
                            _playerShip.Inertia = inertia;
                        }
                        if (_inertiaSlider != null)
                        {
                            _inertiaSlider.Value = inertia;
                        }
                        if (_inertiaLabel != null)
                        {
                            _inertiaLabel.Text = $"Inertia: {inertia:F2}";
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
                var settings = new
                {
                    ShipSpeed = _playerShip?.MoveSpeed ?? 300f,
                    TurnRate = _playerShip?.RotationSpeed ?? 5f,
                    CameraSpeed = CameraSpeed,
                    Zoom = _cameraZoom,
                    GridSize = _gridSize,
                    PanSpeed = _cameraPanSpeed,
                    Inertia = _playerShip?.Inertia ?? 0.9f
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
            var deltaTime = (float)gameTime.ElapsedGameTime.TotalSeconds;
            
            // Check if mouse is over UI before processing player movement
            // UI area is roughly 0-250 width, 0-480 height in top-left corner (extended for inertia controls)
            bool isMouseOverUI = mouseState.X >= 0 && mouseState.X <= 250 && 
                                 mouseState.Y >= 0 && mouseState.Y <= 480;
            
            // Check if mouse is over save button area (lower right)
            // Save button is roughly 1280-150 width (1130-1280), 720-40 height (680-720)
            bool isMouseOverSaveButton = mouseState.X >= GraphicsDevice.Viewport.Width - 160 && 
                                         mouseState.X <= GraphicsDevice.Viewport.Width &&
                                         mouseState.Y >= GraphicsDevice.Viewport.Height - 50 && 
                                         mouseState.Y <= GraphicsDevice.Viewport.Height;
            
            // Combine all UI areas (grid size is now in the top-left panel, so no separate check needed)
            bool isMouseOverAnyUI = isMouseOverUI || isMouseOverSaveButton;
            
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
                // Fire laser from player ship position in the direction the ship is facing
                if (_playerShip != null)
                {
                    var laser = new Laser(_playerShip.Position, _playerShip.Rotation, GraphicsDevice);
                    _lasers.Add(laser);
                }
            }
            _wasRightButtonPressed = mouseState.RightButton == ButtonState.Pressed;
            
            // Toggle grid with G key
            if (keyboardState.IsKeyDown(Keys.G) && !_previousKeyboardState.IsKeyDown(Keys.G))
            {
                _gridVisible = !_gridVisible;
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
                    _cameraPosition += movement * CameraSpeed * deltaTime;
                }
            }
            else if (_isPanningToPlayer && _playerShip != null)
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
                }
                else
                {
                    // Reached player position
                    _cameraPosition = targetPosition;
                    _isPanningToPlayer = false;
                    _cameraFollowingPlayer = true; // Start following player after pan completes
                }
            }
            else if (_cameraFollowingPlayer && _playerShip != null)
            {
                // Keep camera on player after panning completes
                _cameraPosition = _playerShip.Position;
            }
            // If neither WASD nor spacebar and not following, camera stays where it is
            
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
            
            // Draw semi-transparent background behind UI for better readability
            spriteBatch.Begin();
            var uiBackground = new Texture2D(GraphicsDevice, 1, 1);
            uiBackground.SetData(new[] { new Color(0, 0, 0, 200) }); // Semi-transparent black
            spriteBatch.Draw(uiBackground, new Rectangle(0, 0, 250, 480), Color.White); // Top-left UI background (extended for inertia)
            spriteBatch.End();
            
            // Draw UI overlay (zoom level) on top
            _desktop?.Render();
            _saveButtonDesktop?.Render();
        }
    }
}

