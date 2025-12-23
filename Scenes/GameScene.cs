using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Audio;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Myra.Graphics2D.UI;
using Planet9.Core;
using Planet9.Entities;
using Planet9.Managers;

namespace Planet9.Scenes
{
    public enum FriendlyShipBehavior
    {
        Idle,           // Ship stays in place, may drift slightly
        Patrol,         // Ship moves in a small area, patrolling
        LongDistance,    // Ship flies long distance across the map
        Wander,         // Ship moves randomly within map bounds
        Aggressive,     // Ship targets and attacks the player (enemy ships only)
        Flee            // Ship flees from player/enemy when being shot at
    }
    
    public class GameScene : Scene
    {
        private Texture2D? _galaxyTexture;
        private Texture2D? _galaxyOverlayTexture;
        private Vector2 _tileSize;
        private const int TilesX = 4;
        private const int TilesY = 4;
        private Texture2D? _gridPixelTexture;
        private int _gridSize = 128; // 128x128 grid cells (can be changed via slider)
        private bool _gridVisible = false; // Grid visibility toggle (default off)
        private bool _uiGridVisible = false; // Myra UI grid overlay visibility toggle (default off)
        private bool _minimapVisible = true; // Minimap visibility toggle (default on)
        private bool _pathfindingGridVisible = false; // A* pathfinding grid visibility toggle (default off)
        private bool _gamePaused = false; // Game pause state (toggled with F12)
        private const int UIGridSize = 10; // UI grid cell size in pixels
        private const float MapSize = 8192f; // Total map size
        private const int MinimapSize = 200; // Minimap size in pixels (square)
        private Texture2D? _minimapBackgroundTexture;
        private Texture2D? _minimapPlayerDotTexture;
        private Texture2D? _minimapFriendlyDotTexture;
        private Texture2D? _minimapEnemyDotTexture;
        // _pixelTexture moved to RenderingManager
        private Texture2D? _minimapViewportOutlineTexture;
        
        // Camera controller
        private CameraController? _cameraController;
        
        private RenderingManager? _renderingManager;
        
        // Camera position and zoom (delegated to CameraController, keeping for compatibility during migration)
        private Vector2 _cameraPosition;
        private float _cameraZoom = 0.40f; // Start zoomed out
        public float CameraSpeed { get; set; } = 200f; // pixels per second
        private const float MinZoom = 0.40f; // Furthest (most zoomed out)
        private const float MaxZoom = 1.10f; // Closest (most zoomed in)
        private const float ZoomSpeed = 0.1f;
        
        // Player ship
        private PlayerShip? _playerShip;
        private int _currentShipClassIndex = 0; // 0 = PlayerShip, 1 = FriendlyShip, 2 = EnemyShip
        
        // Friendly ships
        private List<FriendlyShip> _friendlyShips = new List<FriendlyShip>();
        private Dictionary<FriendlyShip, ShipState> _friendlyShipStates = new Dictionary<FriendlyShip, ShipState>();
        
        // Enemy ships
        private List<EnemyShip> _enemyShips = new List<EnemyShip>();
        private Dictionary<EnemyShip, EnemyShipState> _enemyShipStates = new Dictionary<EnemyShip, EnemyShipState>();
        
        private const float EnemyPlayerDetectionRange = 1500f; // Range at which enemy detects and switches to aggressive behavior
        private const int MaxPathPoints = 100; // Maximum number of path points to store per ship
        private System.Random? _random; // Will be initialized from services
        
        // A* Pathfinding system
        private PathfindingGrid? _pathfindingGrid;
        
        // Combat manager - handles lasers, collisions, explosions
        private CombatManager? _combatManager;
        
        // Collision manager - handles ship-to-ship collisions
        private CollisionManager? _collisionManager;
        
        // Behavior manager - handles ship behavior logic
        private BehaviorManager? _behaviorManager;
        
        // Behavior duration ranges (in seconds) - increased for longer behavior changes
        private const float IdleMinDuration = 8f;
        private const float IdleMaxDuration = 20f;
        private const float PatrolMinDuration = 20f;
        private const float PatrolMaxDuration = 50f;
        private const float LongDistanceMinDuration = 40f;
        private const float LongDistanceMaxDuration = 120f;
        private const float WanderMinDuration = 10f;
        private const float WanderMaxDuration = 30f;
        
        // UI for zoom display and ship controls
        private Desktop? _desktop;
        private Desktop? _saveButtonDesktop;
        private Desktop? _coordinateDesktop;
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
        private HorizontalSlider? _driftSlider;
        private Label? _driftLabel;
        private HorizontalSlider? _avoidanceRangeSlider;
        private Label? _avoidanceRangeLabel;
        private CheckBox? _avoidanceRangeVisibleCheckBox;
        private bool _avoidanceRangeVisible = false; // Toggle for showing avoidance range
        private CheckBox? _enemyPathVisibleCheckBox;
        private bool _enemyPathVisible = false; // Toggle for showing ship paths
        private CheckBox? _enemyTargetPathVisibleCheckBox;
        private bool _enemyTargetPathVisible = false; // Toggle for showing enemy target paths
        private float _avoidanceDetectionRange = 300f; // Default avoidance detection range
        private HorizontalSlider? _shipIdleRateSlider;
        private Label? _shipIdleRateLabel;
        private float _shipIdleRate = 0.3f; // Default: 30% chance to idle (0 = always moving, 1 = always idle)
        private HorizontalSlider? _lookAheadSlider;
        private Label? _lookAheadLabel;
        private CheckBox? _lookAheadVisibleCheckBox;
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
        private Panel? _cameraSettingsPanel;
        private Label? _mouseCoordinateLabel;
        private HorizontalSlider? _aimRotationSpeedSlider;
        private Label? _aimRotationSpeedLabel;
        private SoundEffectInstance? _backgroundMusicInstance;
        private SoundEffectInstance? _shipFlySound;
        private SoundEffectInstance? _shipIdleSound;
        private SpriteFont? _font; // Font for drawing behavior labels
        private float _musicVolume = 0.5f; // Default music volume (0-1)
        private float _sfxVolume = 1.0f; // Default SFX volume (0-1)
        private HorizontalSlider? _musicVolumeSlider;
        private Label? _musicVolumeLabel;
        private CheckBox? _musicEnabledCheckBox;
        private bool _musicEnabled = true; // Music enabled/disabled state
        private HorizontalSlider? _sfxVolumeSlider;
        private Label? _sfxVolumeLabel;
        private CheckBox? _sfxEnabledCheckBox;
        private bool _sfxEnabled = true; // SFX enabled/disabled state
        
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
        private bool _behaviorTextVisible = true; // Track behavior text visibility (toggled with U key)

        public GameScene(Game game) : base(game)
        {
        }

        /// <summary>
        /// Get or create ship state for a friendly ship
        /// </summary>
        private ShipState GetOrCreateShipState(FriendlyShip ship)
        {
            if (!_friendlyShipStates.TryGetValue(ship, out var state))
            {
                state = new ShipState();
                _friendlyShipStates[ship] = state;
            }
            return state;
        }

        /// <summary>
        /// Get or create ship state for an enemy ship
        /// </summary>
        private EnemyShipState GetOrCreateEnemyShipState(EnemyShip ship)
        {
            if (!_enemyShipStates.TryGetValue(ship, out var state))
            {
                state = new EnemyShipState();
                _enemyShipStates[ship] = state;
            }
            return state;
        }

        public override void LoadContent()
        {
            // Get shared Random instance from services
            _random = (System.Random?)Game.Services.GetService(typeof(System.Random));
            if (_random == null)
            {
                _random = new System.Random(); // Fallback if service not available
            }
            
            // Initialize camera controller
            _cameraController = new CameraController();
            _cameraController.Zoom = _cameraZoom;
            _cameraController.CameraSpeed = CameraSpeed;
            _cameraController.PanSpeed = _cameraPanSpeed;
            _cameraController.Inertia = _cameraInertia;
            
            // Initialize rendering manager
            _renderingManager = new RenderingManager(GraphicsDevice);
            
            // Initialize combat manager early (needed for sound loading)
            _combatManager = new CombatManager(GraphicsDevice);
            _combatManager.Initialize(MapSize);
            
            // Load galaxy tile texture
            try
            {
                _galaxyTexture = Content.Load<Texture2D>("galaxytile");
                
                // Use original texture size for tiles (no scaling)
                _tileSize = new Vector2(_galaxyTexture.Width, _galaxyTexture.Height);
            }
            catch (System.Exception ex)
            {
                System.Console.WriteLine($"Failed to load galaxy tile texture: {ex.Message}");
            }
            
            // Load galaxy overlay texture
            try
            {
                _galaxyOverlayTexture = Content.Load<Texture2D>("galaxy");
            }
            catch (System.Exception ex)
            {
                System.Console.WriteLine($"Failed to load galaxy overlay texture: {ex.Message}");
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
            
            // Load combat manager content (lasers, explosions, sounds)
            _combatManager?.LoadContent(Content, _sfxVolume, _sfxEnabled);
            
            // Load ship idle and fly sound effects
            try
            {
                var idleSound = Content.Load<SoundEffect>("shipidle1");
                System.Console.WriteLine($"[SHIP SOUND] Idle sound loaded: {idleSound != null}");
                if (idleSound != null)
                {
                    _shipIdleSound = idleSound.CreateInstance();
                    _shipIdleSound.IsLooped = true;
                    _shipIdleSound.Volume = _sfxEnabled ? _sfxVolume : 0f; // Use saved SFX volume or mute if disabled
                    if (_sfxEnabled)
                    {
                        _shipIdleSound.Play(); // Start playing idle sound immediately if enabled
                    }
                    System.Console.WriteLine($"[SHIP SOUND] Idle sound playing. State: {_shipIdleSound.State}, Volume: {_shipIdleSound.Volume}");
                }
                
                var flySound = Content.Load<SoundEffect>("shipfly1");
                System.Console.WriteLine($"[SHIP SOUND] Fly sound loaded: {flySound != null}");
                if (flySound != null)
                {
                    _shipFlySound = flySound.CreateInstance();
                    _shipFlySound.IsLooped = true;
                    _shipFlySound.Volume = _sfxEnabled ? _sfxVolume * 0.8f : 0f; // 20% lower than SFX volume or mute if disabled
                    System.Console.WriteLine($"[SHIP SOUND] Fly sound instance created. Volume: {_shipFlySound.Volume}");
                }
            }
            catch (System.Exception ex)
            {
                System.Console.WriteLine($"[SHIP SOUND ERROR] Failed to load ship sound effects: {ex.Message}");
                System.Console.WriteLine($"[SHIP SOUND ERROR] Stack trace: {ex.StackTrace}");
            }
            
            // Load font for behavior labels
            try
            {
                _font = Content.Load<SpriteFont>("DefaultFont");
                System.Console.WriteLine($"Font loaded successfully: {_font != null}");
            }
            catch (System.Exception ex)
            {
                System.Console.WriteLine($"Failed to load font: {ex.Message}");
            }
            
            // Calculate map center position first
            const float mapSize = 8192f;
            var mapCenter = new Vector2(mapSize / 2f, mapSize / 2f);
            
            // Create player ship at map center (will be switched based on saved class)
            _playerShip = new PlayerShip(GraphicsDevice, Content, _random);
            _playerShip.Health = 50f; // Player has 50 health
            _playerShip.MaxHealth = 50f;
            _playerShip.Damage = 10f; // Player does 10 damage
            _playerShip.Position = mapCenter;
            _currentShipClassIndex = 0; // Default to PlayerShip
            
            // Load FriendlyShip settings from saved file
            float friendlyMoveSpeed = 300f; // Default values
            float friendlyRotationSpeed = 3f; // Reduced for smoother turning
            float friendlyInertia = 0.9f;
            float friendlyAimRotationSpeed = 3f; // Reduced for smoother turning
            float friendlyDrift = 0f; // Default no drift
            float friendlyAvoidanceRange = 300f; // Default avoidance range
            float friendlyLookAheadDistance = 1.5f; // Default look-ahead multiplier
            bool friendlyLookAheadVisible = false; // Default look-ahead visibility
            
            try
            {
                var friendlySettingsPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "settings_FriendlyShip.json");
                if (File.Exists(friendlySettingsPath))
                {
                    var json = File.ReadAllText(friendlySettingsPath);
                    var settings = JsonSerializer.Deserialize<JsonElement>(json);
                    
                    if (settings.TryGetProperty("ShipSpeed", out var shipSpeedElement))
                    {
                        friendlyMoveSpeed = shipSpeedElement.GetSingle();
                    }
                    if (settings.TryGetProperty("TurnRate", out var turnRateElement))
                    {
                        friendlyRotationSpeed = turnRateElement.GetSingle();
                    }
                    if (settings.TryGetProperty("Inertia", out var inertiaElement))
                    {
                        friendlyInertia = inertiaElement.GetSingle();
                    }
                    if (settings.TryGetProperty("AimRotationSpeed", out var aimRotationSpeedElement))
                    {
                        friendlyAimRotationSpeed = aimRotationSpeedElement.GetSingle();
                    }
                    if (settings.TryGetProperty("Drift", out var driftElement))
                    {
                        friendlyDrift = driftElement.GetSingle();
                    }
                    if (settings.TryGetProperty("AvoidanceDetectionRange", out var avoidanceRangeElement))
                    {
                        friendlyAvoidanceRange = avoidanceRangeElement.GetSingle();
                    }
                    if (settings.TryGetProperty("LookAheadDistance", out var lookAheadDistanceElement))
                    {
                        friendlyLookAheadDistance = lookAheadDistanceElement.GetSingle();
                    }
                    if (settings.TryGetProperty("LookAheadVisible", out var lookAheadVisibleElement))
                    {
                        friendlyLookAheadVisible = lookAheadVisibleElement.GetBoolean();
                    }
                }
            }
            catch (System.Exception ex)
            {
                System.Console.WriteLine($"Failed to load FriendlyShip settings: {ex.Message}");
            }
            
            // Create 8 friendly ships at random positions with saved settings
            for (int i = 0; i < 8; i++)
            {
                var friendlyShip = new FriendlyShip(GraphicsDevice, Content, _random);
                // Initialize behavior system
                var shipState = GetOrCreateShipState(friendlyShip);
                shipState.Behavior = _behaviorManager?.GetRandomBehavior() ?? FriendlyShipBehavior.Idle;
                shipState.BehaviorTimer = _behaviorManager?.GetBehaviorDuration(shipState.Behavior) ?? 5f;
                // Random position within map bounds (with some margin from edges)
                float margin = 500f;
                float x = (float)(_random.NextDouble() * (mapSize - margin * 2) + margin);
                float y = (float)(_random.NextDouble() * (mapSize - margin * 2) + margin);
                friendlyShip.Position = new Vector2(x, y);
                
                // Use saved settings instead of random values
                friendlyShip.MoveSpeed = friendlyMoveSpeed;
                friendlyShip.RotationSpeed = friendlyRotationSpeed;
                friendlyShip.Inertia = friendlyInertia;
                friendlyShip.AimRotationSpeed = friendlyAimRotationSpeed;
                friendlyShip.Drift = friendlyDrift; // Apply drift setting
                friendlyShip.AvoidanceDetectionRange = friendlyAvoidanceRange; // Apply avoidance range setting
                friendlyShip.LookAheadDistance = friendlyLookAheadDistance; // Apply look-ahead distance setting
                friendlyShip.LookAheadVisible = friendlyLookAheadVisible; // Apply look-ahead visibility setting
                
                _friendlyShips.Add(friendlyShip);
                // Initialize ship state
                var shipStateInit = GetOrCreateShipState(friendlyShip);
                shipStateInit.Behavior = _behaviorManager?.GetRandomBehavior() ?? FriendlyShipBehavior.Idle;
                shipStateInit.BehaviorTimer = _behaviorManager?.GetBehaviorDuration(shipStateInit.Behavior) ?? 5f;
                // Initialize direction tracking with random direction
                float initialAngle = (float)(_random.NextDouble() * MathHelper.TwoPi);
                shipStateInit.LastDirection = new Vector2((float)Math.Cos(initialAngle), (float)Math.Sin(initialAngle));
            }
            
            // Create 3 enemy ships at random positions
            for (int i = 0; i < 3; i++)
            {
                var enemyShip = new EnemyShip(GraphicsDevice, Content, _random);
                // Initialize behavior system - start with random behavior like friendly ships
                var enemyState = GetOrCreateEnemyShipState(enemyShip);
                enemyState.Behavior = _behaviorManager?.GetRandomBehavior() ?? FriendlyShipBehavior.Idle;
                enemyState.BehaviorTimer = _behaviorManager?.GetBehaviorDuration(enemyState.Behavior) ?? 5f;
                enemyState.AttackCooldown = 0f;
                
                // Random position within map bounds (with some margin from edges)
                float margin = 500f;
                float x = (float)(_random.NextDouble() * (mapSize - margin * 2) + margin);
                float y = (float)(_random.NextDouble() * (mapSize - margin * 2) + margin);
                enemyShip.Position = new Vector2(x, y);
                
                // Use default settings for enemy ships (can be customized later)
                enemyShip.MoveSpeed = 250f;
                enemyShip.RotationSpeed = 3f;
                enemyShip.Inertia = 0.9f;
                enemyShip.AimRotationSpeed = 3f;
                enemyShip.Drift = 0f;
                enemyShip.AvoidanceDetectionRange = 300f;
                enemyShip.LookAheadDistance = 1.5f;
                enemyShip.LookAheadVisible = false; // Enemies don't show look-ahead by default
                enemyShip.Health = 20f; // Enemy ships have 20 health
                enemyShip.MaxHealth = 20f;
                enemyShip.Damage = 5f; // Enemy ships do 5 damage
                enemyShip.HealthRegenRate = 20f; // Health regeneration per second (same as player)
                
                // Initialize direction tracking with random direction (for behaviors)
                float initialAngle = (float)(_random.NextDouble() * MathHelper.TwoPi);
                enemyState.PatrolPoints.Clear(); // Initialize empty patrol points
                
                _enemyShips.Add(enemyShip);
            }
            
            System.Console.WriteLine($"[ENEMY SHIPS] Created {_enemyShips.Count} enemy ships at positions:");
            foreach (var enemyShip in _enemyShips)
            {
                System.Console.WriteLine($"  Enemy ship at ({enemyShip.Position.X:F0}, {enemyShip.Position.Y:F0})");
            }
            
            // Initialize camera to center on player
            _cameraPosition = mapCenter;
            if (_cameraController != null)
            {
                _cameraController.Position = mapCenter;
            }
            
            // Initialize A* pathfinding grid
            _pathfindingGrid = new PathfindingGrid(MapSize, 128f); // 128 pixel cells
            
            // Note: CombatManager is already initialized in LoadContent()
            
            // Initialize collision manager (handles ship-to-ship collisions)
            _collisionManager = new CollisionManager();
            
            // Initialize behavior manager (handles ship behaviors)
            _behaviorManager = new BehaviorManager(_random!);
            
            // Initialize particle pool (static, shared across all effects)
            ParticlePool.Initialize();
            
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
            
            _minimapEnemyDotTexture = new Texture2D(GraphicsDevice, 1, 1);
            _minimapEnemyDotTexture.SetData(new[] { Color.Red }); // Red for enemy ships
            
            _minimapViewportOutlineTexture = new Texture2D(GraphicsDevice, 1, 1);
            _minimapViewportOutlineTexture.SetData(new[] { Color.White }); // White so color can be controlled via Draw parameter
            
            // Initialize rendering manager after textures are created
            _renderingManager = new RenderingManager(GraphicsDevice);
            _renderingManager.Initialize(
                _gridPixelTexture,
                _minimapBackgroundTexture,
                _minimapPlayerDotTexture,
                _minimapFriendlyDotTexture,
                _minimapEnemyDotTexture,
                _minimapViewportOutlineTexture,
                _galaxyTexture,
                _galaxyOverlayTexture
            );
            
            // Initialize UI for zoom display and ship controls
            _desktop = new Desktop();
            
            // Use a Grid layout to organize UI elements properly
            var grid = new Grid
            {
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top,
                ColumnSpacing = 0,
                RowSpacing = 8 // Increased spacing for better readability
            };
            
            // Define columns (one column for all elements)
            grid.ColumnsProportions.Add(new Proportion(ProportionType.Auto));
            
            // Define rows for each UI element (camera controls moved to separate panel)
            grid.RowsProportions.Add(new Proportion(ProportionType.Auto)); // Speed label
            grid.RowsProportions.Add(new Proportion(ProportionType.Auto)); // Speed slider
            grid.RowsProportions.Add(new Proportion(ProportionType.Auto)); // Turn rate label
            grid.RowsProportions.Add(new Proportion(ProportionType.Auto)); // Turn rate slider
            grid.RowsProportions.Add(new Proportion(ProportionType.Auto)); // Grid size label
            grid.RowsProportions.Add(new Proportion(ProportionType.Auto)); // Grid size buttons
            grid.RowsProportions.Add(new Proportion(ProportionType.Auto)); // Aim rotation speed label
            grid.RowsProportions.Add(new Proportion(ProportionType.Auto)); // Aim rotation speed slider
            grid.RowsProportions.Add(new Proportion(ProportionType.Auto)); // Inertia label
            grid.RowsProportions.Add(new Proportion(ProportionType.Auto)); // Inertia slider
            grid.RowsProportions.Add(new Proportion(ProportionType.Auto)); // Drift label
            grid.RowsProportions.Add(new Proportion(ProportionType.Auto)); // Drift slider
            grid.RowsProportions.Add(new Proportion(ProportionType.Auto)); // Avoidance range label
            grid.RowsProportions.Add(new Proportion(ProportionType.Auto)); // Avoidance range slider
            grid.RowsProportions.Add(new Proportion(ProportionType.Auto)); // Ship Idle Rate label
            grid.RowsProportions.Add(new Proportion(ProportionType.Auto)); // Ship Idle Rate slider
            grid.RowsProportions.Add(new Proportion(ProportionType.Auto)); // Look-ahead label
            grid.RowsProportions.Add(new Proportion(ProportionType.Auto)); // Look-ahead slider and checkbox
            grid.RowsProportions.Add(new Proportion(ProportionType.Auto)); // Enemy Path checkbox
            grid.RowsProportions.Add(new Proportion(ProportionType.Auto)); // Enemy Target Path checkbox
            grid.RowsProportions.Add(new Proportion(ProportionType.Auto)); // Music volume label
            grid.RowsProportions.Add(new Proportion(ProportionType.Auto)); // Music volume slider
            grid.RowsProportions.Add(new Proportion(ProportionType.Auto)); // SFX volume label
            grid.RowsProportions.Add(new Proportion(ProportionType.Auto)); // SFX volume slider
            
            // Speed label - bright green for visibility
            _speedLabel = new Label
            {
                Text = $"Ship Speed: {(_playerShip?.MoveSpeed ?? 300f):F0}",
                TextColor = Color.Lime,
                GridColumn = 0,
                GridRow = 0,
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top,
                Padding = new Myra.Graphics2D.Thickness(0, 0, 0, 0) // Padding handled by panel
            };
            grid.Widgets.Add(_speedLabel);
            
            // Speed slider
            _speedSlider = new HorizontalSlider
            {
                Minimum = 50f,
                Maximum = 1000f,
                Value = _playerShip?.MoveSpeed ?? 300f,
                Width = 200,
                Height = 10, // Half the default height
                GridColumn = 0,
                GridRow = 1,
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top,
                Padding = new Myra.Graphics2D.Thickness(0, 0, 0, 0) // Padding handled by panel
            };
            _speedSlider.ValueChanged += (s, a) =>
            {
                if (_playerShip != null)
                {
                    _playerShip.MoveSpeed = _speedSlider.Value;
                    _speedLabel.Text = $"Ship Speed: {_speedSlider.Value:F0}";
                    
                    // Also update all ships of the current class
                    if (_currentShipClassIndex == 1)
                    {
                        foreach (var friendlyShip in _friendlyShips)
                        {
                            friendlyShip.MoveSpeed = _speedSlider.Value;
                        }
                    }
                    else if (_currentShipClassIndex == 2)
                    {
                        foreach (var enemyShip in _enemyShips)
                        {
                            enemyShip.MoveSpeed = _speedSlider.Value;
                        }
                    }
                    
                    // Auto-save when slider changes
                    SaveCurrentShipSettings();
                }
            };
            grid.Widgets.Add(_speedSlider);
            
            // Turn rate label - bright cyan for visibility
            _turnRateLabel = new Label
            {
                Text = $"Ship Turn Rate: {(_playerShip?.RotationSpeed ?? 5f):F1}",
                TextColor = Color.Cyan,
                GridColumn = 0,
                GridRow = 2,
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top,
                Padding = new Myra.Graphics2D.Thickness(0, 0, 0, 0) // Padding handled by panel
            };
            grid.Widgets.Add(_turnRateLabel);
            
            // Turn rate slider
            _turnRateSlider = new HorizontalSlider
            {
                Minimum = 0.5f,
                Maximum = 10f, // Reduced max for less aggressive turning
                Value = _playerShip?.RotationSpeed ?? 3f,
                Width = 200,
                Height = 10, // Half the default height
                GridColumn = 0,
                GridRow = 3,
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top,
                Padding = new Myra.Graphics2D.Thickness(0, 0, 0, 0) // Padding handled by panel
            };
            _turnRateSlider.ValueChanged += (s, a) =>
            {
                if (_playerShip != null)
                {
                    _playerShip.RotationSpeed = _turnRateSlider.Value;
                    _turnRateLabel.Text = $"Ship Turn Rate: {_turnRateSlider.Value:F1}";
                    
                    // Also update all ships of the current class
                    if (_currentShipClassIndex == 1)
                    {
                        foreach (var friendlyShip in _friendlyShips)
                        {
                            friendlyShip.RotationSpeed = _turnRateSlider.Value;
                        }
                    }
                    else if (_currentShipClassIndex == 2)
                    {
                        foreach (var enemyShip in _enemyShips)
                        {
                            enemyShip.RotationSpeed = _turnRateSlider.Value;
                        }
                    }
                    
                    // Auto-save when slider changes
                    SaveCurrentShipSettings();
                }
            };
            grid.Widgets.Add(_turnRateSlider);
            
            // Grid size controls container (label and checkbox in a horizontal layout)
            var gridSizeControlsContainer = new HorizontalStackPanel
            {
                GridColumn = 0,
                GridRow = 4,
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top,
                Spacing = 10,
                Padding = new Myra.Graphics2D.Thickness(0, 0, 0, 0) // Padding handled by panel
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
                SavePanelSettings(); // Auto-save panel settings
            };
            gridSizeControlsContainer.Widgets.Add(_gridVisibleCheckBox);
            
            grid.Widgets.Add(gridSizeControlsContainer);
            
            // Grid size arrow buttons container
            var gridSizeButtonContainer = new HorizontalStackPanel
            {
                GridColumn = 0,
                GridRow = 5,
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top,
                Spacing = 5,
                Padding = new Myra.Graphics2D.Thickness(0, 0, 0, 0) // Padding handled by panel
            };
            
            // Grid size values array
            int[] gridSizeValues = { 64, 128, 256, 512, 1024 };
            
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
            
            // Aim rotation speed label - bright lime green for visibility
            _aimRotationSpeedLabel = new Label
            {
                Text = $"Ship Idle Rotation Speed: {(_playerShip?.AimRotationSpeed ?? 5f):F1}",
                TextColor = Color.Lime,
                GridColumn = 0,
                GridRow = 6,
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top,
                Padding = new Myra.Graphics2D.Thickness(0, 0, 0, 0) // Padding handled by panel
            };
            grid.Widgets.Add(_aimRotationSpeedLabel);
            
            // Aim rotation speed slider (how fast ship rotates toward cursor when stationary)
            _aimRotationSpeedSlider = new HorizontalSlider
            {
                Minimum = 0.5f,
                Maximum = 10f, // Reduced max for less aggressive turning
                Value = _playerShip?.AimRotationSpeed ?? 3f,
                Width = 200,
                Height = 10, // Half the default height
                GridColumn = 0,
                GridRow = 7,
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top,
                Padding = new Myra.Graphics2D.Thickness(0, 0, 0, 0) // Padding handled by panel
            };
            _aimRotationSpeedSlider.ValueChanged += (s, a) =>
            {
                if (_playerShip != null)
                {
                    _playerShip.AimRotationSpeed = _aimRotationSpeedSlider.Value;
                    _aimRotationSpeedLabel.Text = $"Ship Idle Rotation Speed: {_aimRotationSpeedSlider.Value:F1}";
                    
                    // Also update all ships of the current class
                    if (_currentShipClassIndex == 1)
                    {
                        foreach (var friendlyShip in _friendlyShips)
                        {
                            friendlyShip.AimRotationSpeed = _aimRotationSpeedSlider.Value;
                        }
                    }
                    else if (_currentShipClassIndex == 2)
                    {
                        foreach (var enemyShip in _enemyShips)
                        {
                            enemyShip.AimRotationSpeed = _aimRotationSpeedSlider.Value;
                        }
                    }
                    
                    // Auto-save when slider changes
                    SaveCurrentShipSettings();
                }
            };
            grid.Widgets.Add(_aimRotationSpeedSlider);
            
            // Inertia label - bright purple for visibility
            _inertiaLabel = new Label
            {
                Text = $"Ship Inertia: {(_playerShip?.Inertia ?? 0.9f):F2}",
                TextColor = new Color(255, 100, 255), // Purple/magenta
                GridColumn = 0,
                GridRow = 8,
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top,
                Padding = new Myra.Graphics2D.Thickness(0, 0, 0, 0) // Padding handled by panel
            };
            grid.Widgets.Add(_inertiaLabel);
            
            // Inertia slider (0.0 = no inertia/instant stop, 0.995 = maximum inertia)
            _inertiaSlider = new HorizontalSlider
            {
                Minimum = 0f,
                Maximum = 0.995f,
                Value = _playerShip?.Inertia ?? 0.9f,
                Width = 200,
                Height = 10, // Half the default height
                GridColumn = 0,
                GridRow = 9,
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top,
                Padding = new Myra.Graphics2D.Thickness(0, 0, 0, 0) // Padding handled by panel
            };
            _inertiaSlider.ValueChanged += (s, a) =>
            {
                if (_playerShip != null)
                {
                    _playerShip.Inertia = _inertiaSlider.Value;
                    _inertiaLabel.Text = $"Ship Inertia: {_inertiaSlider.Value:F2}";
                    
                    // Also update all ships of the current class
                    if (_currentShipClassIndex == 1)
                    {
                        foreach (var friendlyShip in _friendlyShips)
                        {
                            friendlyShip.Inertia = _inertiaSlider.Value;
                        }
                    }
                    else if (_currentShipClassIndex == 2)
                    {
                        foreach (var enemyShip in _enemyShips)
                        {
                            enemyShip.Inertia = _inertiaSlider.Value;
                        }
                    }
                    
                    // Auto-save when slider changes
                    SaveCurrentShipSettings();
                }
            };
            grid.Widgets.Add(_inertiaSlider);
            
            // Drift label
            _driftLabel = new Label
            {
                Text = $"Ship Drift: {(_playerShip?.Drift ?? 0f):F2}",
                TextColor = new Color(150, 255, 150), // Light green
                GridColumn = 0,
                GridRow = 10,
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top,
                Padding = new Myra.Graphics2D.Thickness(0, 0, 0, 0) // Padding handled by panel
            };
            grid.Widgets.Add(_driftLabel);
            
            // Drift slider (0 = no drift, higher = more random direction drift when idle)
            _driftSlider = new HorizontalSlider
            {
                Minimum = 0f,
                Maximum = 10f,
                Value = _playerShip?.Drift ?? 0f,
                Width = 200,
                Height = 10, // Half the default height
                GridColumn = 0,
                GridRow = 11,
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top,
                Padding = new Myra.Graphics2D.Thickness(0, 0, 0, 0) // Padding handled by panel
            };
            _driftSlider.ValueChanged += (s, a) =>
            {
                if (_playerShip != null)
                {
                    _playerShip.Drift = _driftSlider.Value;
                    _driftLabel.Text = $"Ship Drift: {_driftSlider.Value:F2}";
                    
                    // Also update all ships of the current class
                    if (_currentShipClassIndex == 1)
                    {
                        foreach (var friendlyShip in _friendlyShips)
                        {
                            friendlyShip.Drift = _driftSlider.Value; // Update from FriendlyShip settings
                        }
                    }
                    else if (_currentShipClassIndex == 2)
                    {
                        foreach (var enemyShip in _enemyShips)
                        {
                            enemyShip.Drift = _driftSlider.Value; // Update from EnemyShip settings
                        }
                    }
                    
                    // Auto-save when slider changes
                    SaveCurrentShipSettings();
                }
            };
            grid.Widgets.Add(_driftSlider);
            
            // Avoidance Detection Range label
            _avoidanceRangeLabel = new Label
            {
                Text = $"Avoidance Detection Range: {_avoidanceDetectionRange:F0}",
                TextColor = new Color(100, 255, 100), // Light green
                GridColumn = 0,
                GridRow = 12,
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top,
                Padding = new Myra.Graphics2D.Thickness(0, 0, 0, 0) // Padding handled by panel
            };
            grid.Widgets.Add(_avoidanceRangeLabel);
            
            // Avoidance Detection Range slider and checkbox in a horizontal stack
            var avoidanceRangeContainer = new HorizontalStackPanel
            {
                GridColumn = 0,
                GridRow = 13,
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top,
                Spacing = 10
            };
            
            _avoidanceRangeSlider = new HorizontalSlider
            {
                Minimum = 100f,
                Maximum = 1000f,
                Value = _avoidanceDetectionRange,
                Width = 200,
                Height = 10, // Half the default height
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top,
                Padding = new Myra.Graphics2D.Thickness(0, 0, 0, 0) // Padding handled by panel
            };
            _avoidanceRangeSlider.ValueChanged += (s, a) =>
            {
                _avoidanceDetectionRange = _avoidanceRangeSlider.Value;
                _avoidanceRangeLabel.Text = $"Avoidance Detection Range: {_avoidanceDetectionRange:F0}";
                
                // Update ships based on current ship class
                if (_currentShipClassIndex == 1)
                {
                    // FriendlyShip class - update all friendly ships
                    foreach (var friendlyShip in _friendlyShips)
                    {
                        friendlyShip.AvoidanceDetectionRange = _avoidanceRangeSlider.Value;
                    }
                }
                else if (_currentShipClassIndex == 2)
                {
                    // EnemyShip class - update all enemy ships
                    foreach (var enemyShip in _enemyShips)
                    {
                        enemyShip.AvoidanceDetectionRange = _avoidanceRangeSlider.Value;
                    }
                }
                else if (_playerShip != null)
                {
                    // PlayerShip class - update player ship
                    _playerShip.AvoidanceDetectionRange = _avoidanceRangeSlider.Value;
                }
                
                // Auto-save when slider changes
                SaveCurrentShipSettings();
            };
            avoidanceRangeContainer.Widgets.Add(_avoidanceRangeSlider);
            
            // Checkbox to toggle avoidance range visibility
            _avoidanceRangeVisibleCheckBox = new CheckBox
            {
                Text = "Show Range",
                IsChecked = _avoidanceRangeVisible,
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Center
            };
            _avoidanceRangeVisibleCheckBox.Click += (s, a) =>
            {
                _avoidanceRangeVisible = _avoidanceRangeVisibleCheckBox.IsChecked;
                SavePanelSettings(); // Auto-save panel settings
            };
            avoidanceRangeContainer.Widgets.Add(_avoidanceRangeVisibleCheckBox);
            
            grid.Widgets.Add(avoidanceRangeContainer);
            
            // Ship Idle Rate label
            _shipIdleRateLabel = new Label
            {
                Text = $"Ship Idle Rate: {(_shipIdleRate * 100f):F0}%",
                TextColor = new Color(255, 200, 100), // Orange-yellow
                GridColumn = 0,
                GridRow = 14,
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top,
                Padding = new Myra.Graphics2D.Thickness(0, 0, 0, 0) // Padding handled by panel
            };
            grid.Widgets.Add(_shipIdleRateLabel);
            
            // Ship Idle Rate slider (0 = always moving, 1 = always idle)
            _shipIdleRateSlider = new HorizontalSlider
            {
                Minimum = 0f,
                Maximum = 1f,
                Value = _shipIdleRate,
                Width = 200,
                Height = 10, // Half the default height
                GridColumn = 0,
                GridRow = 15,
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top,
                Padding = new Myra.Graphics2D.Thickness(0, 0, 0, 0) // Padding handled by panel
            };
            _shipIdleRateSlider.ValueChanged += (s, a) =>
            {
                _shipIdleRate = _shipIdleRateSlider.Value;
                _shipIdleRateLabel.Text = $"Ship Idle Rate: {(_shipIdleRate * 100f):F0}%";
                // Auto-save when slider changes
                SaveCurrentShipSettings();
            };
            grid.Widgets.Add(_shipIdleRateSlider);
            
            // Look-ahead label
            _lookAheadLabel = new Label
            {
                Text = $"Look-Ahead Distance: {(_playerShip?.LookAheadDistance ?? 1.5f):F2}x",
                TextColor = new Color(255, 200, 100), // Orange-yellow
                GridColumn = 0,
                GridRow = 16,
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top,
                Padding = new Myra.Graphics2D.Thickness(0, 0, 0, 0)
            };
            grid.Widgets.Add(_lookAheadLabel);
            
            // Look-ahead slider and checkbox container
            var lookAheadContainer = new HorizontalStackPanel
            {
                GridColumn = 0,
                GridRow = 17,
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top,
                Spacing = 10
            };
            
            // Look-ahead slider (0.5x to 5.0x multiplier)
            _lookAheadSlider = new HorizontalSlider
            {
                Minimum = 0.5f,
                Maximum = 5.0f,
                Value = _playerShip?.LookAheadDistance ?? 1.5f,
                Width = 200,
                Height = 10,
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top
            };
            _lookAheadSlider.ValueChanged += (s, a) =>
            {
                if (_playerShip != null)
                {
                    _playerShip.LookAheadDistance = _lookAheadSlider.Value;
                    _lookAheadLabel.Text = $"Look-Ahead Distance: {_lookAheadSlider.Value:F2}x";
                    
                    // Also update all ships of the current class
                    if (_currentShipClassIndex == 1)
                    {
                        foreach (var friendlyShip in _friendlyShips)
                        {
                            friendlyShip.LookAheadDistance = _lookAheadSlider.Value;
                        }
                    }
                    else if (_currentShipClassIndex == 2)
                    {
                        foreach (var enemyShip in _enemyShips)
                        {
                            enemyShip.LookAheadDistance = _lookAheadSlider.Value;
                        }
                    }
                    
                    // Auto-save when slider changes
                    SaveCurrentShipSettings();
                }
            };
            lookAheadContainer.Widgets.Add(_lookAheadSlider);
            
            // Look-ahead visible checkbox
            _lookAheadVisibleCheckBox = new CheckBox
            {
                Text = "Show Line",
                IsChecked = _playerShip?.LookAheadVisible ?? false
            };
            _lookAheadVisibleCheckBox.Click += (s, a) =>
            {
                if (_playerShip != null)
                {
                    _playerShip.LookAheadVisible = _lookAheadVisibleCheckBox.IsChecked;
                    
                    // Always update all friendly ships when checkbox is toggled
                    foreach (var friendlyShip in _friendlyShips)
                    {
                        friendlyShip.LookAheadVisible = _lookAheadVisibleCheckBox.IsChecked;
                    }
                    
                    // Auto-save when checkbox changes
                    SaveCurrentShipSettings();
                }
            };
            lookAheadContainer.Widgets.Add(_lookAheadVisibleCheckBox);
            grid.Widgets.Add(lookAheadContainer);
            
            // Enemy Path checkbox
            _enemyPathVisibleCheckBox = new CheckBox
            {
                Text = "Show Ship Path",
                IsChecked = _enemyPathVisible,
                GridColumn = 0,
                GridRow = 18,
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top,
                Padding = new Myra.Graphics2D.Thickness(0, 0, 0, 0)
            };
            _enemyPathVisibleCheckBox.Click += (s, a) =>
            {
                _enemyPathVisible = _enemyPathVisibleCheckBox.IsChecked;
                SavePanelSettings(); // Auto-save panel settings
            };
            grid.Widgets.Add(_enemyPathVisibleCheckBox);
            
            // Enemy Target Path checkbox
            _enemyTargetPathVisibleCheckBox = new CheckBox
            {
                Text = "Show Ship Target Paths",
                IsChecked = _enemyTargetPathVisible,
                GridColumn = 0,
                GridRow = 19,
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top,
                Padding = new Myra.Graphics2D.Thickness(0, 0, 0, 0)
            };
            _enemyTargetPathVisibleCheckBox.Click += (s, a) =>
            {
                _enemyTargetPathVisible = _enemyTargetPathVisibleCheckBox.IsChecked;
                SavePanelSettings(); // Auto-save panel settings
            };
            grid.Widgets.Add(_enemyTargetPathVisibleCheckBox);
            
            // Music volume label
            _musicVolumeLabel = new Label
            {
                Text = $"Music Volume: {(_musicVolume * 100f):F0}%",
                TextColor = new Color(100, 200, 255), // Light blue
                GridColumn = 0,
                GridRow = 20,
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top,
                Padding = new Myra.Graphics2D.Thickness(0, 0, 0, 0) // Padding handled by panel
            };
            grid.Widgets.Add(_musicVolumeLabel);
            
            // Music volume slider and checkbox container
            var musicVolumeContainer = new HorizontalStackPanel
            {
                GridColumn = 0,
                GridRow = 21,
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top,
                Spacing = 10,
                Padding = new Myra.Graphics2D.Thickness(0, 0, 0, 0) // Padding handled by panel
            };
            
            // Music volume slider (0-100%)
            _musicVolumeSlider = new HorizontalSlider
            {
                Minimum = 0f,
                Maximum = 1f,
                Value = _musicVolume,
                Width = 200,
                Height = 10, // Half the default height
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top
            };
            _musicVolumeSlider.ValueChanged += (s, a) =>
            {
                _musicVolume = _musicVolumeSlider.Value;
                _musicVolumeLabel.Text = $"Music Volume: {(_musicVolume * 100f):F0}%";
                // Apply volume to background music (only if enabled)
                if (_backgroundMusicInstance != null && _musicEnabled)
                {
                    _backgroundMusicInstance.Volume = _musicVolume;
                }
            };
            musicVolumeContainer.Widgets.Add(_musicVolumeSlider);
            
            // Music enabled checkbox
            _musicEnabledCheckBox = new CheckBox
            {
                Text = "Music",
                IsChecked = _musicEnabled
            };
            _musicEnabledCheckBox.Click += (s, a) =>
            {
                _musicEnabled = _musicEnabledCheckBox.IsChecked;
                // Apply or mute music based on checkbox state
                if (_backgroundMusicInstance != null)
                {
                    if (_musicEnabled)
                    {
                        _backgroundMusicInstance.Volume = _musicVolume;
                        if (_backgroundMusicInstance.State == SoundState.Stopped)
                        {
                            _backgroundMusicInstance.Play();
                        }
                    }
                    else
                    {
                        _backgroundMusicInstance.Volume = 0f;
                    }
                }
                SavePanelSettings(); // Auto-save panel settings
            };
            musicVolumeContainer.Widgets.Add(_musicEnabledCheckBox);
            grid.Widgets.Add(musicVolumeContainer);
            
            // SFX volume label
            _sfxVolumeLabel = new Label
            {
                Text = $"SFX Volume: {(_sfxVolume * 100f):F0}%",
                TextColor = new Color(255, 150, 100), // Orange
                GridColumn = 0,
                GridRow = 22,
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top,
                Padding = new Myra.Graphics2D.Thickness(0, 0, 0, 0) // Padding handled by panel
            };
            grid.Widgets.Add(_sfxVolumeLabel);
            
            // SFX volume slider and checkbox container
            var sfxVolumeContainer = new HorizontalStackPanel
            {
                GridColumn = 0,
                GridRow = 23,
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top,
                Spacing = 10,
                Padding = new Myra.Graphics2D.Thickness(0, 0, 0, 0) // Padding handled by panel
            };
            
            // SFX volume slider (0-100%)
            _sfxVolumeSlider = new HorizontalSlider
            {
                Minimum = 0f,
                Maximum = 1f,
                Value = _sfxVolume,
                Width = 200,
                Height = 10, // Half the default height
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top
            };
            _sfxVolumeSlider.ValueChanged += (s, a) =>
            {
                _sfxVolume = _sfxVolumeSlider.Value;
                _sfxVolumeLabel.Text = $"SFX Volume: {(_sfxVolume * 100f):F0}%";
                // Update combat manager SFX settings
                _combatManager?.SetSFXSettings(_sfxVolume, _sfxEnabled);
                // Apply volume to SFX instances (only if enabled)
                if (_shipFlySound != null && _sfxEnabled)
                {
                    _shipFlySound.Volume = _sfxVolume * 0.8f; // 20% lower than SFX volume
                }
                if (_shipIdleSound != null && _sfxEnabled)
                {
                    _shipIdleSound.Volume = _sfxVolume;
                }
            };
            sfxVolumeContainer.Widgets.Add(_sfxVolumeSlider);
            
            // SFX enabled checkbox
            _sfxEnabledCheckBox = new CheckBox
            {
                Text = "SFX",
                IsChecked = _sfxEnabled
            };
            _sfxEnabledCheckBox.Click += (s, a) =>
            {
                _sfxEnabled = _sfxEnabledCheckBox.IsChecked;
                // Update combat manager SFX settings
                _combatManager?.SetSFXSettings(_sfxVolume, _sfxEnabled);
                // Apply or mute SFX based on checkbox state
                if (_shipFlySound != null)
                {
                    if (_sfxEnabled)
                    {
                        _shipFlySound.Volume = _sfxVolume * 0.8f;
                    }
                    else
                    {
                        _shipFlySound.Volume = 0f;
                    }
                }
                if (_shipIdleSound != null)
                {
                    if (_sfxEnabled)
                    {
                        _shipIdleSound.Volume = _sfxVolume;
                    }
                    else
                    {
                        _shipIdleSound.Volume = 0f;
                    }
                }
                SavePanelSettings(); // Auto-save panel settings
            };
            sfxVolumeContainer.Widgets.Add(_sfxEnabledCheckBox);
            grid.Widgets.Add(sfxVolumeContainer);
            
            // Create Camera Settings panel
            var cameraSettingsGrid = new Grid
            {
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top,
                ColumnSpacing = 0,
                RowSpacing = 5
            };
            cameraSettingsGrid.ColumnsProportions.Add(new Proportion(ProportionType.Auto));
            
            // Define rows for camera settings
            cameraSettingsGrid.RowsProportions.Add(new Proportion(ProportionType.Auto)); // Zoom label
            cameraSettingsGrid.RowsProportions.Add(new Proportion(ProportionType.Auto)); // Camera speed label
            cameraSettingsGrid.RowsProportions.Add(new Proportion(ProportionType.Auto)); // Camera speed slider
            cameraSettingsGrid.RowsProportions.Add(new Proportion(ProportionType.Auto)); // Cam to Player Speed label
            cameraSettingsGrid.RowsProportions.Add(new Proportion(ProportionType.Auto)); // Cam to Player Speed slider
            cameraSettingsGrid.RowsProportions.Add(new Proportion(ProportionType.Auto)); // Camera inertia label
            cameraSettingsGrid.RowsProportions.Add(new Proportion(ProportionType.Auto)); // Camera inertia slider
            
            // Zoom label - bright yellow for visibility
            _zoomLabel = new Label
            {
                Text = $"Zoom: {_cameraZoom:F2}x",
                TextColor = Color.Yellow,
                GridColumn = 0,
                GridRow = 0,
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top,
                Padding = new Myra.Graphics2D.Thickness(0, 0, 0, 0) // Padding handled by panel
            };
            cameraSettingsGrid.Widgets.Add(_zoomLabel);
            
            // Camera speed label - bright orange for visibility
            _cameraSpeedLabel = new Label
            {
                Text = $"Camera Speed: {CameraSpeed:F0}",
                TextColor = Color.Orange,
                GridColumn = 0,
                GridRow = 1,
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top,
                Padding = new Myra.Graphics2D.Thickness(0, 0, 0, 0) // Padding handled by panel
            };
            cameraSettingsGrid.Widgets.Add(_cameraSpeedLabel);
            
            // Camera speed slider
            _cameraSpeedSlider = new HorizontalSlider
            {
                Minimum = 50f,
                Maximum = 1000f,
                Value = CameraSpeed,
                Width = 200,
                Height = 10, // Half the default height
                GridColumn = 0,
                GridRow = 2,
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top,
                Padding = new Myra.Graphics2D.Thickness(0, 0, 0, 0) // Padding handled by panel
            };
            _cameraSpeedSlider.ValueChanged += (s, a) =>
            {
                CameraSpeed = _cameraSpeedSlider.Value;
                _cameraSpeedLabel.Text = $"Camera Speed: {_cameraSpeedSlider.Value:F0}";
            };
            cameraSettingsGrid.Widgets.Add(_cameraSpeedSlider);
            
            // Cam to Player Speed label - bright yellow for visibility
            _panSpeedLabel = new Label
            {
                Text = $"Cam to Player Speed: {_cameraPanSpeed:F0}",
                TextColor = Color.Yellow,
                GridColumn = 0,
                GridRow = 3,
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top,
                Padding = new Myra.Graphics2D.Thickness(0, 0, 0, 0) // Padding handled by panel
            };
            cameraSettingsGrid.Widgets.Add(_panSpeedLabel);
            
            // Cam to Player Speed slider
            _panSpeedSlider = new HorizontalSlider
            {
                Minimum = 200f,
                Maximum = 2000f,
                Value = _cameraPanSpeed,
                Width = 200,
                Height = 10, // Half the default height
                GridColumn = 0,
                GridRow = 4,
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top,
                Padding = new Myra.Graphics2D.Thickness(0, 0, 0, 0) // Padding handled by panel
            };
            _panSpeedSlider.ValueChanged += (s, a) =>
            {
                _cameraPanSpeed = _panSpeedSlider.Value;
                _panSpeedLabel.Text = $"Cam to Player Speed: {_panSpeedSlider.Value:F0}";
            };
            cameraSettingsGrid.Widgets.Add(_panSpeedSlider);
            
            // Camera inertia label - bright cyan for visibility
            _cameraInertiaLabel = new Label
            {
                Text = $"Camera Inertia: {_cameraInertia:F2}",
                TextColor = Color.Cyan,
                GridColumn = 0,
                GridRow = 5,
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top,
                Padding = new Myra.Graphics2D.Thickness(0, 0, 0, 0) // Padding handled by panel
            };
            cameraSettingsGrid.Widgets.Add(_cameraInertiaLabel);
            
            // Camera inertia slider (0.0 = no inertia/instant stop, 0.995 = maximum inertia)
            _cameraInertiaSlider = new HorizontalSlider
            {
                Minimum = 0f,
                Maximum = 0.995f,
                Value = _cameraInertia,
                Width = 200,
                Height = 10, // Half the default height
                GridColumn = 0,
                GridRow = 6,
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top,
                Padding = new Myra.Graphics2D.Thickness(0, 0, 0, 0) // Padding handled by panel
            };
            _cameraInertiaSlider.ValueChanged += (s, a) =>
            {
                _cameraInertia = _cameraInertiaSlider.Value;
                _cameraInertiaLabel.Text = $"Camera Inertia: {_cameraInertiaSlider.Value:F2}";
            };
            cameraSettingsGrid.Widgets.Add(_cameraInertiaSlider);
            
            // Wrap camera settings grid in a panel
            _cameraSettingsPanel = new Panel
            {
                Background = new Myra.Graphics2D.Brushes.SolidBrush(new Microsoft.Xna.Framework.Color(20, 20, 20, 220)), // Semi-transparent dark background
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top,
                Padding = new Myra.Graphics2D.Thickness(15, 15, 15, 15), // Add padding around the panel content
                Margin = new Myra.Graphics2D.Thickness(20, 0, 0, 0) // Padding to the right of main panel
            };
            _cameraSettingsPanel.Widgets.Add(cameraSettingsGrid);
            
            // Wrap grid in a panel with background that covers all sliders
            var uiPanel = new Panel
            {
                Background = new Myra.Graphics2D.Brushes.SolidBrush(new Microsoft.Xna.Framework.Color(20, 20, 20, 220)), // Semi-transparent dark background
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top,
                Padding = new Myra.Graphics2D.Thickness(15, 15, 15, 15) // Add padding around the panel content
            };
            uiPanel.Widgets.Add(grid);
            
            // Create a container panel to hold both UI panel and camera settings panel (horizontal layout, top-aligned)
            var containerPanel = new HorizontalStackPanel
            {
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top,
                Spacing = 0, // No spacing, using margin for padding instead
                Padding = new Myra.Graphics2D.Thickness(0, 0, 0, 0)
            };
            containerPanel.Widgets.Add(uiPanel);
            containerPanel.Widgets.Add(_cameraSettingsPanel);
            
            // Create mouse coordinate label in a separate desktop for absolute positioning
            _coordinateDesktop = new Desktop();
            var coordinatePanel = new Panel
            {
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top,
                Padding = new Myra.Graphics2D.Thickness(0, 0, 0, 0)
            };
            _mouseCoordinateLabel = new Label
            {
                Text = "(0, 0)",
                TextColor = Color.Yellow,
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top,
                Padding = new Myra.Graphics2D.Thickness(0, 0, 0, 0),
                Margin = new Myra.Graphics2D.Thickness(0, 0, 0, 0),
                Visible = false
            };
            coordinatePanel.Widgets.Add(_mouseCoordinateLabel);
            _coordinateDesktop.Root = coordinatePanel;
            
            _desktop.Root = containerPanel;
            
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
                Width = 500,
                Height = 500
            };
            
            _previewCoordinateLabel = new Label
            {
                Text = "Coordinates: (0, 0)",
                TextColor = Color.White,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Top,
                Margin = new Myra.Graphics2D.Thickness(0, 10, 0, 0)
            };
            _previewPanel.Widgets.Add(_previewCoordinateLabel);
            
            // Ship name label
            _previewShipLabel = new Label
            {
                Text = "PlayerShip (1/3)",
                TextColor = Color.Yellow,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Top,
                Margin = new Myra.Graphics2D.Thickness(0, 35, 0, 0)
            };
            _previewPanel.Widgets.Add(_previewShipLabel);
            
            // Left arrow button - direct child of preview panel
            _previewLeftButton = new TextButton
            {
                Text = "←",
                Width = 50,
                Height = 50,
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Myra.Graphics2D.Thickness(15, 0, 0, 0)
            };
            _previewLeftButton.Click += (s, a) => 
            {
                _previewShipIndex--;
                if (_previewShipIndex < 0)
                    _previewShipIndex = 2; // Wrap to last (EnemyShip)
                UpdatePreviewShipLabel();
                // Switch ship class to match preview
                SwitchShipClass(_previewShipIndex);
            };
            _previewPanel.Widgets.Add(_previewLeftButton);
            
            // Right arrow button - direct child of preview panel
            _previewRightButton = new TextButton
            {
                Text = "→",
                Width = 50,
                Height = 50,
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Myra.Graphics2D.Thickness(0, 0, 15, 0)
            };
            _previewRightButton.Click += (s, a) => 
            {
                _previewShipIndex++;
                if (_previewShipIndex > 2)
                    _previewShipIndex = 0; // Wrap to PlayerShip
                UpdatePreviewShipLabel();
                // Switch ship class to match preview
                SwitchShipClass(_previewShipIndex);
            };
            _previewPanel.Widgets.Add(_previewRightButton);
            
            // Close button - direct child of preview panel
            var closeButton = new TextButton
            {
                Text = "Close (P)",
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Bottom,
                Margin = new Myra.Graphics2D.Thickness(0, 0, 0, 15),
                Width = 120,
                Height = 35
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
                // EnemyShip uses ship1-256 texture (same as PlayerShip)
            }
            catch (System.Exception ex)
            {
                System.Console.WriteLine($"Failed to load preview ship textures: {ex.Message}");
            }
            
            // Load saved settings after UI is initialized
            LoadSettings();
            
            // Load panel/UI settings
            LoadPanelSettings();
            
            // Load current ship class settings
            LoadCurrentShipSettings();
            
            // Sync look-ahead visibility to all friendly ships after settings are loaded
            if (_playerShip != null && _lookAheadVisibleCheckBox != null)
            {
                bool lookAheadVisible = _lookAheadVisibleCheckBox.IsChecked;
                foreach (var friendlyShip in _friendlyShips)
                {
                    friendlyShip.LookAheadVisible = lookAheadVisible;
                }
            }
            
            _previousMouseState = Mouse.GetState();
            _previousKeyboardState = Keyboard.GetState();
            
            // Set up ship collections in combat manager (after all ships are initialized)
            _combatManager?.SetShipCollections(
                _friendlyShips,
                _enemyShips,
                _friendlyShipStates,
                _enemyShipStates,
                GetOrCreateShipState,
                GetOrCreateEnemyShipState
            );
            
            // Set up behavior manager dependencies (after all ships are initialized)
            _behaviorManager?.SetDependencies(
                _playerShip,
                _friendlyShips,
                _enemyShips,
                _friendlyShipStates,
                _enemyShipStates,
                _pathfindingGrid,
                _combatManager,
                GetOrCreateShipState,
                GetOrCreateEnemyShipState,
                _shipIdleRate
            );
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
                        if (_cameraController != null)
                        {
                            _cameraController.Zoom = _cameraZoom;
                        }
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
                        int[] validGridSizes = { 64, 128, 256, 512, 1024 };
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
                            _panSpeedLabel.Text = $"Cam to Player Speed: {_cameraPanSpeed:F0}";
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
                        _combatManager?.SetSFXSettings(_sfxVolume, _sfxEnabled);
                        if (_sfxVolumeSlider != null)
                        {
                            _sfxVolumeSlider.Value = _sfxVolume;
                        }
                        if (_sfxVolumeLabel != null)
                        {
                            _sfxVolumeLabel.Text = $"SFX Volume: {(_sfxVolume * 100f):F0}%";
                        }
                        // Apply to SFX instances (only if enabled)
                        if (_shipIdleSound != null && _sfxEnabled)
                        {
                            _shipIdleSound.Volume = _sfxVolume;
                        }
                        if (_shipFlySound != null && _sfxEnabled)
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
                
                // Save panel/UI settings to separate file
                SavePanelSettings();
                
                // Save general settings (camera, etc.)
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
        
        private void SavePanelSettings()
        {
            try
            {
                var panelSettings = new
                {
                    UIVisible = _uiVisible,
                    BehaviorTextVisible = _behaviorTextVisible,
                    EnemyPathVisible = _enemyPathVisible,
                    EnemyTargetPathVisible = _enemyTargetPathVisible,
                    AvoidanceRangeVisible = _avoidanceRangeVisible,
                    PathfindingGridVisible = _pathfindingGridVisible,
                    GridVisible = _gridVisible,
                    MinimapVisible = _minimapVisible,
                    MusicEnabled = _musicEnabled,
                    SFXEnabled = _sfxEnabled
                };
                
                var json = JsonSerializer.Serialize(panelSettings, new JsonSerializerOptions { WriteIndented = true });
                var filePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "settings_Panel.json");
                System.Console.WriteLine($"Saving panel settings to: {filePath}");
                File.WriteAllText(filePath, json);
                System.Console.WriteLine($"Panel settings file written successfully.");
            }
            catch (Exception ex)
            {
                System.Console.WriteLine($"Failed to save panel settings: {ex.Message}");
            }
        }
        
        private void LoadPanelSettings()
        {
            try
            {
                var filePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "settings_Panel.json");
                if (File.Exists(filePath))
                {
                    var json = File.ReadAllText(filePath);
                    var settings = JsonSerializer.Deserialize<JsonElement>(json);
                    
                    // Load UI visibility
                    if (settings.TryGetProperty("UIVisible", out var uiVisibleElement))
                    {
                        _uiVisible = uiVisibleElement.GetBoolean();
                        if (_desktop?.Root != null)
                        {
                            _desktop.Root.Visible = _uiVisible;
                        }
                        if (_saveButtonDesktop?.Root != null)
                        {
                            _saveButtonDesktop.Root.Visible = _uiVisible;
                        }
                    }
                    
                    // Load behavior text visibility
                    if (settings.TryGetProperty("BehaviorTextVisible", out var behaviorTextVisibleElement))
                    {
                        _behaviorTextVisible = behaviorTextVisibleElement.GetBoolean();
                    }
                    
                    // Load enemy path visibility
                    if (settings.TryGetProperty("EnemyPathVisible", out var enemyPathVisibleElement))
                    {
                        _enemyPathVisible = enemyPathVisibleElement.GetBoolean();
                        if (_enemyPathVisibleCheckBox != null)
                        {
                            _enemyPathVisibleCheckBox.IsChecked = _enemyPathVisible;
                        }
                    }
                    
                    // Load enemy target path visibility
                    if (settings.TryGetProperty("EnemyTargetPathVisible", out var enemyTargetPathVisibleElement))
                    {
                        _enemyTargetPathVisible = enemyTargetPathVisibleElement.GetBoolean();
                        if (_enemyTargetPathVisibleCheckBox != null)
                        {
                            _enemyTargetPathVisibleCheckBox.IsChecked = _enemyTargetPathVisible;
                        }
                    }
                    
                    // Load avoidance range visibility
                    if (settings.TryGetProperty("AvoidanceRangeVisible", out var avoidanceRangeVisibleElement))
                    {
                        _avoidanceRangeVisible = avoidanceRangeVisibleElement.GetBoolean();
                        if (_avoidanceRangeVisibleCheckBox != null)
                        {
                            _avoidanceRangeVisibleCheckBox.IsChecked = _avoidanceRangeVisible;
                        }
                    }
                    
                    // Load pathfinding grid visibility
                    if (settings.TryGetProperty("PathfindingGridVisible", out var pathfindingGridVisibleElement))
                    {
                        _pathfindingGridVisible = pathfindingGridVisibleElement.GetBoolean();
                    }
                    
                    // Load grid visibility
                    if (settings.TryGetProperty("GridVisible", out var gridVisibleElement))
                    {
                        _gridVisible = gridVisibleElement.GetBoolean();
                        if (_gridVisibleCheckBox != null)
                        {
                            _gridVisibleCheckBox.IsChecked = _gridVisible;
                        }
                    }
                    
                    // Load minimap visibility
                    if (settings.TryGetProperty("MinimapVisible", out var minimapVisibleElement))
                    {
                        _minimapVisible = minimapVisibleElement.GetBoolean();
                    }
                    
                    // Load music enabled
                    if (settings.TryGetProperty("MusicEnabled", out var musicEnabledElement))
                    {
                        _musicEnabled = musicEnabledElement.GetBoolean();
                        if (_musicEnabledCheckBox != null)
                        {
                            _musicEnabledCheckBox.IsChecked = _musicEnabled;
                        }
                        // Apply music state
                        if (_backgroundMusicInstance != null)
                        {
                            if (_musicEnabled)
                            {
                                _backgroundMusicInstance.Volume = _musicVolume;
                                if (_backgroundMusicInstance.State == SoundState.Stopped)
                                {
                                    _backgroundMusicInstance.Play();
                                }
                            }
                            else
                            {
                                _backgroundMusicInstance.Volume = 0f;
                            }
                        }
                    }
                    
                    // Load SFX enabled
                    if (settings.TryGetProperty("SFXEnabled", out var sfxEnabledElement))
                    {
                        _sfxEnabled = sfxEnabledElement.GetBoolean();
                        _combatManager?.SetSFXSettings(_sfxVolume, _sfxEnabled);
                        if (_sfxEnabledCheckBox != null)
                        {
                            _sfxEnabledCheckBox.IsChecked = _sfxEnabled;
                        }
                        // Apply SFX state
                        if (_shipIdleSound != null)
                        {
                            if (_sfxEnabled)
                            {
                                _shipIdleSound.Volume = _sfxVolume;
                            }
                            else
                            {
                                _shipIdleSound.Volume = 0f;
                            }
                        }
                        if (_shipFlySound != null)
                        {
                            if (_sfxEnabled)
                            {
                                _shipFlySound.Volume = _sfxVolume * 0.8f;
                            }
                            else
                            {
                                _shipFlySound.Volume = 0f;
                            }
                        }
                    }
                    
                    System.Console.WriteLine("Panel settings loaded successfully!");
                }
                else
                {
                    System.Console.WriteLine("Panel settings file not found. Using default values.");
                }
            }
            catch (Exception ex)
            {
                System.Console.WriteLine($"Failed to load panel settings: {ex.Message}");
            }
        }

        public override void Update(GameTime gameTime)
        {
            var keyboardState = Keyboard.GetState();
            var mouseState = Mouse.GetState();
            
            // If game is paused (F12 grid mode), only update UI and input, skip game logic
            if (_gamePaused)
            {
                // Update Myra input for UI interaction
                _desktop?.UpdateInput();
                _saveButtonDesktop?.UpdateInput();
                
                // Update mouse coordinates when grid is visible
                if (_uiGridVisible && _mouseCoordinateLabel != null)
                {
                    var mouseX = mouseState.X;
                    var mouseY = mouseState.Y;
                    
                    // Calculate snapped grid point (for display, not actual cursor position)
                    int snappedX = (mouseX / UIGridSize) * UIGridSize;
                    int snappedY = (mouseY / UIGridSize) * UIGridSize;
                    
                    // Update coordinate label position to follow mouse (above cursor)
                    _mouseCoordinateLabel.Text = $"({snappedX}, {snappedY})";
                    _mouseCoordinateLabel.Margin = new Myra.Graphics2D.Thickness(mouseX, mouseY - 25, 0, 0); // 25px above cursor
                }
                
                // Toggle UI grid overlay and pause game with F12
                if (keyboardState.IsKeyDown(Keys.F12) && !_previousKeyboardState.IsKeyDown(Keys.F12))
                {
                    _uiGridVisible = !_uiGridVisible;
                    _gamePaused = _uiGridVisible; // Pause when grid is shown, unpause when hidden
                    // Show/hide coordinate label
                    if (_mouseCoordinateLabel != null)
                    {
                        _mouseCoordinateLabel.Visible = _uiGridVisible;
                    }
                }
                
                _previousKeyboardState = keyboardState;
                _previousMouseState = mouseState;
                return; // Skip all game logic when paused
            }
            
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
                        _previewShipIndex = 2; // Wrap to EnemyShip
                    UpdatePreviewShipLabel();
                    // Switch ship class to match preview
                    SwitchShipClass(_previewShipIndex);
                }
                if (keyboardState.IsKeyDown(Keys.Right) && !_previousKeyboardState.IsKeyDown(Keys.Right))
                {
                    _previewShipIndex++;
                    if (_previewShipIndex > 2)
                        _previewShipIndex = 0; // Wrap to PlayerShip
                    UpdatePreviewShipLabel();
                    // Switch ship class to match preview
                    SwitchShipClass(_previewShipIndex);
                }
            }
            
            // Toggle behavior text and UI panels with U key - check before preview mode
            if (keyboardState.IsKeyDown(Keys.U) && !_previousKeyboardState.IsKeyDown(Keys.U))
            {
                _behaviorTextVisible = !_behaviorTextVisible;
                _uiVisible = !_uiVisible;
                
                // Toggle main UI panel visibility
                if (_desktop?.Root != null)
                {
                    _desktop.Root.Visible = _uiVisible;
                }
                
                // Toggle save button panel visibility
                if (_saveButtonDesktop?.Root != null)
                {
                    _saveButtonDesktop.Root.Visible = _uiVisible;
                }
                
                SavePanelSettings(); // Auto-save panel settings
                System.Console.WriteLine($"[U KEY] UI Panels: {(_uiVisible ? "ON" : "OFF")}, Behavior Text: {(_behaviorTextVisible ? "ON" : "OFF")}");
            }
            
            // If preview is active, only update preview UI, don't update game
            if (_isPreviewActive)
            {
                _previewDesktop?.UpdateInput();
                
                // Get the currently previewed ship texture
                Texture2D? shipTexture = _previewShipIndex == 0 ? _previewShip1Texture : (_previewShipIndex == 1 ? _previewShip2Texture : _previewShip1Texture);
                    
                if (_previewPanel != null && _previewCoordinateLabel != null && shipTexture != null)
                {
                    
                    // Calculate preview panel position (centered)
                    int panelX = (GraphicsDevice.Viewport.Width - 500) / 2;
                    int panelY = (GraphicsDevice.Viewport.Height - 500) / 2;
                    
                    // Sprite position in preview (centered in panel)
                    int spriteX = panelX + 250 - (shipTexture?.Width ?? 0) / 2;
                    int spriteY = panelY + 250 - (shipTexture?.Height ?? 0) / 2;
                    
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
                    // Ensure fly sound is playing (restart if it stopped) - only if SFX is enabled
                    if (_shipFlySound != null && _sfxEnabled && _shipFlySound.State != SoundState.Playing)
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
                    // Ensure idle sound is playing (restart if it stopped) - only if SFX is enabled
                    if (_shipIdleSound != null && _sfxEnabled && _shipIdleSound.State != SoundState.Playing)
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
                Vector2 worldPosition;
                if (_cameraController != null)
                {
                    worldPosition = _cameraController.ScreenToWorld(screenPos, GraphicsDevice.Viewport);
                }
                else
                {
                    var worldX = (screenPos.X - GraphicsDevice.Viewport.Width / 2f) / _cameraZoom + _cameraPosition.X;
                    var worldY = (screenPos.Y - GraphicsDevice.Viewport.Height / 2f) / _cameraZoom + _cameraPosition.Y;
                    worldPosition = new Vector2(worldX, worldY);
                }
                
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
                // Fire lasers from player ship positions in the direction of the cursor
                if (_playerShip != null)
                {
                    // Convert mouse position to world coordinates
                    var mouseScreenPos = new Vector2(mouseState.X, mouseState.Y);
                    Vector2 mouseWorldPos;
                    if (_cameraController != null)
                    {
                        mouseWorldPos = _cameraController.ScreenToWorld(mouseScreenPos, GraphicsDevice.Viewport);
                    }
                    else
                    {
                        var mouseWorldX = (mouseScreenPos.X - GraphicsDevice.Viewport.Width / 2f) / _cameraZoom + _cameraPosition.X;
                        var mouseWorldY = (mouseScreenPos.Y - GraphicsDevice.Viewport.Height / 2f) / _cameraZoom + _cameraPosition.Y;
                        mouseWorldPos = new Vector2(mouseWorldX, mouseWorldY);
                    }
                    
                    // Calculate direction to cursor for laser firing
                    var directionToCursor = mouseWorldPos - _playerShip.Position;
                    if (directionToCursor.LengthSquared() > 0.1f)
                    {
                        directionToCursor.Normalize();
                        // Calculate laser direction (angle to cursor)
                        float laserDirection = (float)Math.Atan2(directionToCursor.Y, directionToCursor.X) + MathHelper.PiOver2;
                        
                        var shipTexture = _playerShip.GetTexture();
                        if (shipTexture != null)
                        {
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
                                
                                // Fire laser in direction of cursor, not ship rotation
                                _combatManager?.FireLaser(laserSpawnPosition, laserDirection, _playerShip.Damage, _playerShip);
                            };
                            
                            // Fire first laser from sprite coordinates (210, 50)
                            fireLaserFromSpriteCoords(210f, 50f);
                            
                            // Fire second laser from sprite coordinates (40, 50)
                            fireLaserFromSpriteCoords(40f, 50f);
                        }
                    }
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
            
            // Toggle A* pathfinding grid with F11
            if (keyboardState.IsKeyDown(Keys.F11) && !_previousKeyboardState.IsKeyDown(Keys.F11))
            {
                _pathfindingGridVisible = !_pathfindingGridVisible;
                SavePanelSettings(); // Auto-save panel settings
                System.Console.WriteLine($"A* Pathfinding Grid: {(_pathfindingGridVisible ? "ON" : "OFF")}");
            }
            
            // Toggle world grid with F10
            if (keyboardState.IsKeyDown(Keys.F10) && !_previousKeyboardState.IsKeyDown(Keys.F10))
            {
                _gridVisible = !_gridVisible;
                if (_gridVisibleCheckBox != null)
                {
                    _gridVisibleCheckBox.IsChecked = _gridVisible;
                }
                SavePanelSettings(); // Auto-save panel settings
            }
            
            // Toggle UI grid overlay and pause game with F12
            if (keyboardState.IsKeyDown(Keys.F12) && !_previousKeyboardState.IsKeyDown(Keys.F12))
            {
                _uiGridVisible = !_uiGridVisible;
                _gamePaused = _uiGridVisible; // Pause when grid is shown, unpause when hidden
                // Show/hide coordinate label
                if (_mouseCoordinateLabel != null)
                {
                    _mouseCoordinateLabel.Visible = _uiGridVisible;
                }
            }
            
            
            // Camera zoom with mouse wheel
            int scrollDelta = mouseState.ScrollWheelValue - _previousMouseState.ScrollWheelValue;
            if (scrollDelta != 0)
            {
                float zoomChange = scrollDelta > 0 ? ZoomSpeed : -ZoomSpeed;
                _cameraZoom = MathHelper.Clamp(_cameraZoom + zoomChange, MinZoom, MaxZoom);
                
                // Sync with CameraController
                if (_cameraController != null)
                {
                    _cameraController.Zoom = _cameraZoom;
                }
                
                // Update zoom label
                if (_zoomLabel != null)
                {
                    _zoomLabel.Text = $"Zoom: {_cameraZoom:F2}x";
                }
            }
            
            // Check for spacebar to smoothly pan camera back to player
            if (keyboardState.IsKeyDown(Keys.Space) && !_previousKeyboardState.IsKeyDown(Keys.Space) && _playerShip != null)
            {
                // Start panning to player when Space is first pressed
                _isPanningToPlayer = true;
                _cameraFollowingPlayer = true; // Enable following when Space is pressed
            }
            
            // Camera movement with WASD - if any WASD key is pressed, use manual camera control and cancel panning
            bool isWASDPressed = keyboardState.IsKeyDown(Keys.W) || keyboardState.IsKeyDown(Keys.A) || 
                                 keyboardState.IsKeyDown(Keys.S) || keyboardState.IsKeyDown(Keys.D);
            
            // Check if camera is panning (WASD pressed, panning to player, or camera has velocity from manual control)
            // Note: This is only used for camera movement, not for ship behavior
            bool isCameraPanning = _isPanningToPlayer || isWASDPressed || _cameraVelocity.LengthSquared() > 1f || !_cameraFollowingPlayer;
            
            // Update player ship aim target (mouse cursor position - always allow aiming, regardless of camera state)
            if (_playerShip != null)
            {
                // Convert mouse position to world coordinates for aiming
                var mouseScreenPos = new Vector2(mouseState.X, mouseState.Y);
                var mouseWorldX = (mouseScreenPos.X - GraphicsDevice.Viewport.Width / 2f) / _cameraZoom + _cameraPosition.X;
                var mouseWorldY = (mouseScreenPos.Y - GraphicsDevice.Viewport.Height / 2f) / _cameraZoom + _cameraPosition.Y;
                var mouseWorldPos = new Vector2(mouseWorldX, mouseWorldY);
                _playerShip.SetAimTarget(mouseWorldPos);
            }
            
            // Update player ship first so we have current position
            _playerShip?.Update(gameTime);
            
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
                    // Don't automatically reattach camera to player - let user control it manually
                    // Camera will only follow player when Space bar is pressed (panning to player)
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
            
            // Sync with CameraController
            if (_cameraController != null)
            {
                _cameraController.Position = _cameraPosition;
            }
            
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
                        _isPanningToPlayer = false; // Panning complete, camera now attached to player
                        // _cameraFollowingPlayer stays true - camera will follow player until screen is scrolled
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
                    
                    // If we hit a boundary, stop panning but keep following
                    if ((_cameraPosition.X <= panMinX || _cameraPosition.X >= panMaxX || 
                         _cameraPosition.Y <= panMinY || _cameraPosition.Y >= panMaxY) && 
                        _cameraPosition != targetPosition)
                    {
                        _isPanningToPlayer = false; // Panning stopped at boundary, camera now attached to player
                        // _cameraFollowingPlayer stays true - camera will follow player until screen is scrolled
                    }
                }
                else
                {
                    // Reached player position - panning complete, camera now attached to player
                    _cameraPosition = targetPosition;
                    _isPanningToPlayer = false;
                    // _cameraFollowingPlayer stays true - camera will follow player until screen is scrolled
                    
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
            else if (_cameraFollowingPlayer && _playerShip != null && !_isPanningToPlayer)
            {
                // Keep camera locked on player after panning completes - stays attached until screen is scrolled (WASD)
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
            
            // Update friendly ships with collision avoidance (including player) and behavior system
            foreach (var friendlyShip in _friendlyShips)
            {
                // Collision avoidance: steer away from player (ship-to-ship collision disabled)
                // Use each ship's own avoidance detection range
                float playerAvoidanceRadius = friendlyShip.AvoidanceDetectionRange * 1.33f; // 33% larger radius for player avoidance
                float playerAvoidanceForce = 300f; // Stronger avoidance for player
                Vector2 avoidanceVector = Vector2.Zero;
                
                // Orbit around player ship - create slow circular motion
                if (_playerShip != null)
                {
                    Vector2 toPlayer = _playerShip.Position - friendlyShip.Position;
                    float distance = toPlayer.Length();
                    
                    // Calculate look-ahead target for player avoidance check
                    Vector2 lookAheadTargetForPlayer = friendlyShip.Position;
                    if (friendlyShip.IsActivelyMoving())
                    {
                        float shipRotation = friendlyShip.Rotation;
                        Vector2 lookAheadDirection = new Vector2(
                            (float)System.Math.Sin(shipRotation),
                            -(float)System.Math.Cos(shipRotation)
                        );
                        float lookAheadDist = friendlyShip.MoveSpeed * friendlyShip.LookAheadDistance;
                        lookAheadTargetForPlayer = friendlyShip.Position + lookAheadDirection * lookAheadDist;
                    }
                    
                    // Check if look-ahead target is within player's avoidance radius
                    Vector2 toLookAheadFromPlayer = lookAheadTargetForPlayer - _playerShip.Position;
                    float lookAheadDistanceFromPlayer = toLookAheadFromPlayer.Length();
                    float playerAvoidanceRadiusForLookAhead = _playerShip.AvoidanceDetectionRange * 1.33f; // 33% larger radius for player avoidance
                    bool lookAheadInPlayerRadius = lookAheadDistanceFromPlayer < playerAvoidanceRadiusForLookAhead;
                    
                    // Use a larger range for orbital behavior
                    float orbitRadius = friendlyShip.AvoidanceDetectionRange * 1.5f; // Orbit at 1.5x avoidance range
                    
                    if ((distance < orbitRadius || lookAheadInPlayerRadius) && distance > 0.1f)
                    {
                        // Calculate desired orbit distance (maintain distance from player)
                        float desiredDistance = friendlyShip.AvoidanceDetectionRange * 1.2f; // Orbit at 1.2x avoidance range
                        
                        // Calculate tangential direction (perpendicular to direction to player) for orbital motion
                        // Rotate the direction vector by 90 degrees to get tangential direction
                        Vector2 tangential = new Vector2(-toPlayer.Y, toPlayer.X);
                        tangential.Normalize();
                        
                        // Calculate radial direction (toward/away from player to maintain orbit distance)
                        Vector2 radialDirection = toPlayer;
                        radialDirection.Normalize();
                        
                        // Calculate distance error (how far from desired orbit distance)
                        float distanceError = distance - desiredDistance;
                        
                        // Blend tangential (orbital) motion with radial (distance correction) motion
                        // Strong tangential for orbital motion, small radial for distance maintenance
                        float tangentialWeight = 0.85f; // 85% tangential (orbital), 15% radial (distance correction)
                        Vector2 orbitalDirection = tangential * tangentialWeight + radialDirection * (1f - tangentialWeight) * Math.Sign(-distanceError);
                        orbitalDirection.Normalize();
                        
                        // Calculate orbital force (gentle, slow turning)
                        float orbitalStrength = MathHelper.Clamp(Math.Abs(distanceError) / desiredDistance, 0.3f, 1f);
                        float slowOrbitalForce = playerAvoidanceForce * 0.4f; // 40% of normal force for slow turning
                        
                        // Increase force if look-ahead is in player's radius (start turning immediately)
                        if (lookAheadInPlayerRadius)
                        {
                            float lookAheadPenetration = (playerAvoidanceRadiusForLookAhead - lookAheadDistanceFromPlayer) / playerAvoidanceRadiusForLookAhead;
                            orbitalStrength = MathHelper.Clamp(orbitalStrength + lookAheadPenetration * 0.5f, 0.5f, 1.5f);
                            slowOrbitalForce = playerAvoidanceForce * (0.4f + lookAheadPenetration * 0.3f); // Increase force up to 70% when look-ahead is in radius
                        }
                        
                        avoidanceVector += orbitalDirection * orbitalStrength * slowOrbitalForce;
                    }
                }
                
                // Avoid other friendly ships with orbital motion (orbit around each other when navigating past)
                float avoidanceRadius = friendlyShip.AvoidanceDetectionRange; // Use ship's own setting
                float avoidanceForce = 300f; // Increased avoidance force for better steering
                
                // Calculate look-ahead target position (where the ship is looking ahead)
                Vector2 lookAheadTarget = friendlyShip.Position;
                if (friendlyShip.IsActivelyMoving())
                {
                    // Calculate look-ahead target in the direction the ship is facing
                    float shipRotation = friendlyShip.Rotation;
                    Vector2 lookAheadDirection = new Vector2(
                        (float)System.Math.Sin(shipRotation),
                        -(float)System.Math.Cos(shipRotation)
                    );
                    float lookAheadDist = friendlyShip.MoveSpeed * friendlyShip.LookAheadDistance;
                    lookAheadTarget = friendlyShip.Position + lookAheadDirection * lookAheadDist;
                }
                
                foreach (var otherShip in _friendlyShips)
                {
                    if (otherShip == friendlyShip) continue;
                    
                    Vector2 toOtherShip = otherShip.Position - friendlyShip.Position;
                    float distance = toOtherShip.Length();
                    
                    // Use the larger of the two ships' avoidance ranges to ensure ships never enter each other's radius
                    float effectiveAvoidanceRadius = Math.Max(avoidanceRadius, otherShip.AvoidanceDetectionRange);
                    
                    // Check if look-ahead target is within other ship's avoidance radius
                    Vector2 toLookAheadFromOther = lookAheadTarget - otherShip.Position;
                    float lookAheadDistanceFromOther = toLookAheadFromOther.Length();
                    bool lookAheadInRadius = lookAheadDistanceFromOther < effectiveAvoidanceRadius;
                    
                    // Proactive avoidance: start veering away when approaching the avoidance radius (1.5x radius for early detection)
                    // OR when look-ahead target hits the avoidance radius
                    float avoidanceDetectionRange = effectiveAvoidanceRadius * 1.5f;
                    
                    if ((distance < avoidanceDetectionRange || lookAheadInRadius) && distance > 0.1f)
                    {
                        toOtherShip.Normalize();
                        
                        // Calculate radial direction (away from other ship)
                        Vector2 radialDirection = -toOtherShip;
                        
                        // Calculate tangential direction (perpendicular for orbital motion)
                        // Choose direction based on ship's current movement to create smooth orbit
                        Vector2 tangentialDirection;
                        if (friendlyShip.IsActivelyMoving())
                        {
                            // Use ship's velocity to determine orbit direction
                            Vector2 shipVelocity = friendlyShip.Velocity;
                            if (shipVelocity.LengthSquared() < 1f)
                            {
                                // If not moving much, try to use last position
                            var shipStateForVel = GetOrCreateShipState(friendlyShip);
                            if (shipStateForVel.LastPosition != Vector2.Zero)
                            {
                                shipVelocity = friendlyShip.Position - shipStateForVel.LastPosition;
                            }
                                
                                if (shipVelocity.LengthSquared() < 1f)
                                {
                                    // If still not moving, use a default orbit direction
                                    tangentialDirection = new Vector2(-toOtherShip.Y, toOtherShip.X);
                                }
                                else
                                {
                                    shipVelocity.Normalize();
                                    // Choose tangential direction that's closer to ship's movement direction
                                    Vector2 tan1 = new Vector2(-toOtherShip.Y, toOtherShip.X);
                                    Vector2 tan2 = new Vector2(toOtherShip.Y, -toOtherShip.X);
                                    
                                    float dot1 = Vector2.Dot(shipVelocity, tan1);
                                    float dot2 = Vector2.Dot(shipVelocity, tan2);
                                    
                                    tangentialDirection = dot1 > dot2 ? tan1 : tan2;
                                }
                            }
                            else
                            {
                                shipVelocity.Normalize();
                                // Choose tangential direction that's closer to ship's movement direction
                                Vector2 tan1 = new Vector2(-toOtherShip.Y, toOtherShip.X);
                                Vector2 tan2 = new Vector2(toOtherShip.Y, -toOtherShip.X);
                                
                                float dot1 = Vector2.Dot(shipVelocity, tan1);
                                float dot2 = Vector2.Dot(shipVelocity, tan2);
                                
                                tangentialDirection = dot1 > dot2 ? tan1 : tan2;
                            }
                        }
                        else
                        {
                            // Default to clockwise orbit
                            tangentialDirection = new Vector2(-toOtherShip.Y, toOtherShip.X);
                        }
                        tangentialDirection.Normalize();
                        
                        // Calculate base avoidance force for this collision
                        float currentAvoidanceForce = avoidanceForce;
                        
                        // Calculate avoidance strength based on distance from avoidance radius
                        // Stronger when closer to or inside the avoidance radius, or when look-ahead hits the radius
                        float distanceFromRadius = distance - effectiveAvoidanceRadius;
                        float avoidanceStrength;
                        
                        if (lookAheadInRadius)
                        {
                            // Look-ahead target is in avoidance radius - start turning immediately
                            // Strength based on how far into the radius the look-ahead is
                            float lookAheadPenetration = (effectiveAvoidanceRadius - lookAheadDistanceFromOther) / effectiveAvoidanceRadius;
                            avoidanceStrength = 1.5f + lookAheadPenetration * 2f; // Scale from 1.5 to 3.5
                            currentAvoidanceForce = avoidanceForce * 1.5f; // Strong force to turn away
                        }
                        else if (distance < effectiveAvoidanceRadius)
                        {
                            // Inside avoidance radius - very strong avoidance
                            avoidanceStrength = 1f + ((effectiveAvoidanceRadius - distance) / effectiveAvoidanceRadius) * 3f;
                            currentAvoidanceForce = 500f; // Strong force to push away
                        }
                        else
                        {
                            // Approaching avoidance radius - moderate avoidance that increases as we get closer
                            float approachFactor = (avoidanceDetectionRange - distance) / (avoidanceDetectionRange - effectiveAvoidanceRadius);
                            avoidanceStrength = approachFactor * 1.5f; // Scale from 0 to 1.5
                            currentAvoidanceForce = avoidanceForce * (1f + approachFactor); // Increase force as approaching
                        }
                        
                        // Blend radial (push away) and tangential (orbit) forces
                        // More tangential when at safe distance, more radial when too close
                        float radialWeight;
                        float tangentialWeight;
                        
                        if (distance < effectiveAvoidanceRadius)
                        {
                            // Inside avoidance radius - prioritize radial (push away strongly)
                            radialWeight = 0.9f; // 90% radial, 10% tangential
                            tangentialWeight = 0.1f;
                        }
                        else if (distance < effectiveAvoidanceRadius * 1.1f)
                        {
                            // Very close to avoidance radius - strong radial
                            radialWeight = 0.7f; // 70% radial, 30% tangential
                            tangentialWeight = 0.3f;
                        }
                        else if (distance < effectiveAvoidanceRadius * 1.3f)
                        {
                            // Approaching avoidance radius - balanced
                            radialWeight = 0.5f; // 50% radial, 50% tangential
                            tangentialWeight = 0.5f;
                        }
                        else
                        {
                            // At safe distance, more orbital motion (gentle steering)
                            radialWeight = 0.2f; // 20% radial, 80% tangential
                            tangentialWeight = 0.8f;
                        }
                        
                        // Combine radial and tangential forces for orbital motion
                        Vector2 orbitalDirection = radialDirection * radialWeight + tangentialDirection * tangentialWeight;
                        orbitalDirection.Normalize();
                        
                        avoidanceVector += orbitalDirection * avoidanceStrength * currentAvoidanceForce;
                    }
                }
                
                // Use A* pathfinding for obstacle avoidance
                if (_pathfindingGrid != null && friendlyShip.IsActivelyMoving())
                {
                    Vector2 currentTarget = friendlyShip.TargetPosition;
                    Vector2 currentPos = friendlyShip.Position;
                    float distanceToTarget = Vector2.Distance(currentPos, currentTarget);
                    
                    // Get ship state for this friendly ship
                    var shipStateForPath = GetOrCreateShipState(friendlyShip);
                    
                    // Check if target has changed - if so, reset progress tracking
                    if (shipStateForPath.LastTarget != Vector2.Zero)
                    {
                        Vector2 lastTarget = shipStateForPath.LastTarget;
                        if (Vector2.Distance(lastTarget, currentTarget) > 50f) // Target changed significantly
                        {
                            // Target changed - reset progress tracking
                            shipStateForPath.ClosestDistanceToTarget = float.MaxValue;
                            shipStateForPath.NoProgressTimer = 0f;
                        }
                    }
                    shipStateForPath.LastTarget = currentTarget;
                    
                    // Track progress toward destination to detect if ship is trapped
                    if (shipStateForPath.ClosestDistanceToTarget == float.MaxValue)
                    {
                        shipStateForPath.ClosestDistanceToTarget = distanceToTarget;
                        shipStateForPath.NoProgressTimer = 0f;
                    }
                    
                    // Check if ship is making progress (getting closer to target)
                    float closestDistance = shipStateForPath.ClosestDistanceToTarget;
                    bool isMakingProgress = distanceToTarget < closestDistance - 10f; // Must get at least 10 pixels closer
                    
                    if (isMakingProgress)
                    {
                        // Ship is making progress - update closest distance and reset timer
                        shipStateForPath.ClosestDistanceToTarget = distanceToTarget;
                        shipStateForPath.NoProgressTimer = 0f;
                    }
                    else
                    {
                        // Ship is not making progress - increment timer
                        shipStateForPath.NoProgressTimer += deltaTime;
                    }
                    
                    // If ship hasn't made progress for 3 seconds, it's likely trapped - force new path
                    bool isTrapped = shipStateForPath.NoProgressTimer > 3.0f;
                    
                    // Check if we need to recalculate path (no path exists, reached waypoint, path is invalid, or ship is trapped)
                    bool needsNewPath = false;
                    if (isTrapped)
                    {
                        needsNewPath = true;
                        // Reset progress tracking for new path
                        shipStateForPath.ClosestDistanceToTarget = float.MaxValue;
                        shipStateForPath.NoProgressTimer = 0f;
                    }
                    if (shipStateForPath.AStarPath.Count == 0)
                    {
                        needsNewPath = true;
                    }
                    else
                    {
                        // Check if we've reached the current waypoint
                        int currentWaypointIndex = shipStateForPath.CurrentWaypointIndex;
                        
                        if (currentWaypointIndex < shipStateForPath.AStarPath.Count)
                        {
                            Vector2 currentWaypoint = shipStateForPath.AStarPath[currentWaypointIndex];
                            float distToWaypoint = Vector2.Distance(currentPos, currentWaypoint);
                            
                            if (distToWaypoint < 100f) // Reached waypoint
                            {
                                currentWaypointIndex++;
                                shipStateForPath.CurrentWaypointIndex = currentWaypointIndex;
                                
                                // If we've reached all waypoints, check if we need a new path to final destination
                                if (currentWaypointIndex >= shipStateForPath.AStarPath.Count)
                                {
                                    float distToFinal = Vector2.Distance(currentPos, currentTarget);
                                    if (distToFinal > 100f)
                                    {
                                        needsNewPath = true;
                                    }
                                }
                            }
                        }
                        else
                        {
                            needsNewPath = true;
                        }
                    }
                    
                    // Update pathfinding grid with current obstacles
                    _pathfindingGrid.ClearObstacles();
                    float obstacleRadius = friendlyShip.AvoidanceDetectionRange * 0.5f;
                    
                    // Mark other ships as obstacles
                    foreach (var otherShip in _friendlyShips)
                    {
                        if (otherShip != friendlyShip)
                        {
                            _pathfindingGrid.SetObstacle(otherShip.Position, obstacleRadius, true);
                        }
                    }
                    
                    // Mark player ship as obstacle
                    if (_playerShip != null)
                    {
                        _pathfindingGrid.SetObstacle(_playerShip.Position, obstacleRadius, true);
                    }
                    
                    // Calculate A* path if needed
                    if (needsNewPath)
                    {
                        var path = _pathfindingGrid.FindPath(currentPos, currentTarget);
                        shipStateForPath.AStarPath.Clear();
                        shipStateForPath.AStarPath.AddRange(path);
                        shipStateForPath.CurrentWaypointIndex = 0;
                        
                        // Reset progress tracking when calculating new path
                        shipStateForPath.ClosestDistanceToTarget = float.MaxValue;
                        shipStateForPath.NoProgressTimer = 0f;
                    }
                    
                    // Follow A* path waypoints with look-ahead for smoother turning
                    if (shipStateForPath.AStarPath.Count > 0)
                    {
                        int waypointIndex = shipStateForPath.CurrentWaypointIndex;
                        var path = shipStateForPath.AStarPath;
                        
                        if (waypointIndex < path.Count)
                        {
                            Vector2 currentWaypoint = path[waypointIndex];
                            float distToWaypoint = Vector2.Distance(currentPos, currentWaypoint);
                            
                            // Look ahead in the path to find a future waypoint to turn toward
                            // This makes the ship turn toward where it's going, not just the immediate next point
                            Vector2 pathLookAheadTarget = currentWaypoint;
                            float lookAheadDistance = friendlyShip.MoveSpeed * friendlyShip.LookAheadDistance; // Use ship's look-ahead multiplier
                            
                            // Find the furthest waypoint we can see ahead
                            float accumulatedDist = distToWaypoint;
                            for (int i = waypointIndex + 1; i < path.Count; i++)
                            {
                                float segmentDist = Vector2.Distance(path[i - 1], path[i]);
                                if (accumulatedDist + segmentDist <= lookAheadDistance)
                                {
                                    accumulatedDist += segmentDist;
                                    pathLookAheadTarget = path[i];
                                }
                                else
                                {
                                    // Interpolate to a point along this segment
                                    float remainingDist = lookAheadDistance - accumulatedDist;
                                    if (remainingDist > 0 && segmentDist > 0)
                                    {
                                        Vector2 segmentDir = path[i] - path[i - 1];
                                        segmentDir.Normalize();
                                        pathLookAheadTarget = path[i - 1] + segmentDir * remainingDist;
                                    }
                                    break;
                                }
                            }
                            
                            // Ensure look-ahead target avoids player's radius
                            if (IsTooCloseToPlayer(pathLookAheadTarget, friendlyShip))
                            {
                                pathLookAheadTarget = AvoidPlayerPosition(pathLookAheadTarget, friendlyShip);
                            }
                            
                            // Clamp look-ahead target to map bounds (keep ships within galaxy map)
                            const float lookAheadMargin = 200f;
                            pathLookAheadTarget = new Vector2(
                                MathHelper.Clamp(pathLookAheadTarget.X, lookAheadMargin, MapSize - lookAheadMargin),
                                MathHelper.Clamp(pathLookAheadTarget.Y, lookAheadMargin, MapSize - lookAheadMargin)
                            );
                            
                            // Store look-ahead target for debug line drawing
                            shipStateForPath.LookAheadTarget = pathLookAheadTarget;
                            
                            // Set target to look-ahead position for smoother turning
                            friendlyShip.SetTargetPosition(pathLookAheadTarget);
                        }
                        else
                        {
                            // Reached end of path, go to final destination (clamped to map bounds)
                            const float finalTargetMargin = 200f;
                            Vector2 clampedTarget = new Vector2(
                                MathHelper.Clamp(currentTarget.X, finalTargetMargin, MapSize - finalTargetMargin),
                                MathHelper.Clamp(currentTarget.Y, finalTargetMargin, MapSize - finalTargetMargin)
                            );
                            
                            // Calculate and store look-ahead target for debug line drawing
                            Vector2 direction = clampedTarget - currentPos;
                            if (direction.LengthSquared() > 0.1f)
                            {
                                direction.Normalize();
                                float lookAheadDist = friendlyShip.MoveSpeed * friendlyShip.LookAheadDistance;
                                Vector2 endPathLookAheadTarget = currentPos + direction * lookAheadDist;
                                shipStateForPath.LookAheadTarget = endPathLookAheadTarget;
                            }
                            
                            friendlyShip.SetTargetPosition(clampedTarget);
                        }
                    }
                }
                else if (avoidanceVector.LengthSquared() > 0.1f)
                {
                    // Fallback: use simple avoidance if pathfinding grid not available
                    avoidanceVector.Normalize();
                    float lookAheadDistance = friendlyShip.MoveSpeed * friendlyShip.LookAheadDistance * 0.8f; // Use ship's look-ahead multiplier
                    Vector2 newAvoidanceTarget = friendlyShip.Position + avoidanceVector * lookAheadDistance;
                    
                    const float safeTargetMargin = 150f;
                    newAvoidanceTarget = new Vector2(
                        MathHelper.Clamp(newAvoidanceTarget.X, safeTargetMargin, MapSize - safeTargetMargin),
                        MathHelper.Clamp(newAvoidanceTarget.Y, safeTargetMargin, MapSize - safeTargetMargin)
                    );
                    
                    // Store look-ahead target for debug line drawing
                    var shipStateForAvoidance = GetOrCreateShipState(friendlyShip);
                    shipStateForAvoidance.LookAheadTarget = newAvoidanceTarget;
                    
                    friendlyShip.SetTargetPosition(newAvoidanceTarget);
                }
                
                friendlyShip.Update(gameTime);
                
                // Ship-to-ship collision detection and resolution (handled by CollisionManager)
                _collisionManager?.HandleFriendlyShipCollisions(friendlyShip, _friendlyShips);
                _collisionManager?.HandleFriendlyPlayerCollision(friendlyShip, _playerShip);
                
                // Update last direction for smooth pathing based on velocity
                if (friendlyShip.IsActivelyMoving())
                {
                    // Use velocity direction if available, otherwise calculate from position change
                    Vector2 velDir = Vector2.Zero;
                    var shipStateForDir = GetOrCreateShipState(friendlyShip);
                    if (shipStateForDir.LastPosition != Vector2.Zero)
                    {
                        Vector2 posChange = friendlyShip.Position - shipStateForDir.LastPosition;
                        if (posChange.LengthSquared() > 1f)
                        {
                            posChange.Normalize();
                            velDir = posChange;
                        }
                    }
                    
                    // Only update if we have a valid direction
                    if (velDir.LengthSquared() > 0.1f)
                    {
                        var shipStateForDirFinal = GetOrCreateShipState(friendlyShip);
                        shipStateForDirFinal.LastDirection = velDir;
                    }
                }
                
                // Track enemy path if enabled
                if (_enemyPathVisible)
                {
                    var shipStateForPathTrack2 = GetOrCreateShipState(friendlyShip);
                    var path = shipStateForPathTrack2.Path;
                    // Add current position to path (only if ship moved significantly)
                    if (path.Count == 0 || Vector2.Distance(path[path.Count - 1], friendlyShip.Position) > 10f)
                    {
                        path.Add(friendlyShip.Position);
                        // Limit path length
                        if (path.Count > MaxPathPoints)
                        {
                            path.RemoveAt(0);
                        }
                    }
                }
                
                // Keep ship within map bounds - clamp position to prevent leaving map
                const float shipMargin = 30f; // Keep ships at least 30 pixels from edges (reduced further to prevent getting stuck)
                float clampedX = MathHelper.Clamp(friendlyShip.Position.X, shipMargin, MapSize - shipMargin);
                float clampedY = MathHelper.Clamp(friendlyShip.Position.Y, shipMargin, MapSize - shipMargin);
                
                // Track ship position to detect if it's stuck
                Vector2 clampedPosition = new Vector2(clampedX, clampedY);
                bool isStuck = false;
                var shipState = GetOrCreateShipState(friendlyShip);
                
                if (shipState.LastPosition != Vector2.Zero)
                {
                    Vector2 lastPos = shipState.LastPosition;
                    float distanceMoved = Vector2.Distance(clampedPosition, lastPos);
                    
                    // If ship hasn't moved much (less than 5 pixels) in the last frame, it might be stuck
                    if (distanceMoved < 5f)
                    {
                        shipState.StuckTimer += deltaTime;
                        
                        // If stuck for more than 0.5 seconds, give it a new target
                        if (shipState.StuckTimer > 0.5f)
                        {
                            isStuck = true;
                        }
                    }
                    else
                    {
                        // Ship is moving, reset stuck timer
                        shipState.StuckTimer = 0f;
                    }
                }
                
                shipState.LastPosition = clampedPosition;
                
                // If ship was clamped or is stuck, give it a new target away from edge
                bool wasClamped = (friendlyShip.Position.X != clampedX || friendlyShip.Position.Y != clampedY);
                if ((wasClamped || isStuck) && !friendlyShip.IsActivelyMoving())
                {
                    // Ship is stuck near edge - give it a new target well away from edge
                    float safeMargin = 300f;
                    Vector2 awayFromEdge = Vector2.Zero;
                    
                    // Determine direction away from nearest edge
                    float distToLeft = clampedX;
                    float distToRight = MapSize - clampedX;
                    float distToTop = clampedY;
                    float distToBottom = MapSize - clampedY;
                    
                    float minDist = Math.Min(Math.Min(distToLeft, distToRight), Math.Min(distToTop, distToBottom));
                    
                    if (minDist == distToLeft)
                        awayFromEdge = new Vector2(1f, 0f); // Move right
                    else if (minDist == distToRight)
                        awayFromEdge = new Vector2(-1f, 0f); // Move left
                    else if (minDist == distToTop)
                        awayFromEdge = new Vector2(0f, 1f); // Move down
                    else
                        awayFromEdge = new Vector2(0f, -1f); // Move up
                    
                    // Add some randomness to the direction
                    float randomAngle = (float)(_random.NextDouble() * MathHelper.PiOver2 - MathHelper.PiOver4);
                    float cos = (float)Math.Cos(randomAngle);
                    float sin = (float)Math.Sin(randomAngle);
                    Vector2 rotatedDir = new Vector2(
                        awayFromEdge.X * cos - awayFromEdge.Y * sin,
                        awayFromEdge.X * sin + awayFromEdge.Y * cos
                    );
                    
                    // Set target 1000-1500 pixels away from current position (ensures ships can turn smoothly)
                    float targetDistance = (float)(_random.NextDouble() * 500f + 1000f);
                    Vector2 newTarget = clampedPosition + rotatedDir * targetDistance;
                    
                    float newTargetX = MathHelper.Clamp(newTarget.X, safeMargin, MapSize - safeMargin);
                    float newTargetY = MathHelper.Clamp(newTarget.Y, safeMargin, MapSize - safeMargin);
                    newTarget = new Vector2(newTargetX, newTargetY);
                    
                    // Avoid other ships' radius when setting unstuck target
                    if (IsTooCloseToOtherShips(newTarget, friendlyShip))
                    {
                        newTarget = AvoidOtherShipsPosition(newTarget, friendlyShip);
                        newTargetX = MathHelper.Clamp(newTarget.X, safeMargin, MapSize - safeMargin);
                        newTargetY = MathHelper.Clamp(newTarget.Y, safeMargin, MapSize - safeMargin);
                        newTarget = new Vector2(newTargetX, newTargetY);
                    }
                    
                    friendlyShip.SetTargetPosition(newTarget);
                    
                    // Reset stuck timer
                    var shipStateForStuck = GetOrCreateShipState(friendlyShip);
                    shipStateForStuck.StuckTimer = 0f;
                }
                
                // Behavior system: Update and execute current behavior
                _behaviorManager?.UpdateFriendlyShipBehavior(friendlyShip, deltaTime);
            }
            
            // Update enemy ships with aggressive behavior and collision avoidance (same system as friendly ships)
            foreach (var enemyShip in _enemyShips)
            {
                // Update enemy ship movement (inherits from FriendlyShip, so uses same Update logic)
                enemyShip.Update(gameTime);
                
                // Collision avoidance: use same orbital motion system as friendly ships
                float avoidanceRadius = enemyShip.AvoidanceDetectionRange;
                float avoidanceForce = 300f;
                Vector2 avoidanceVector = Vector2.Zero;
                
                // Calculate look-ahead target position (where the ship is looking ahead)
                Vector2 lookAheadTarget = enemyShip.Position;
                if (enemyShip.IsActivelyMoving())
                {
                    float shipRotation = enemyShip.Rotation;
                    Vector2 lookAheadDirection = new Vector2(
                        (float)System.Math.Sin(shipRotation),
                        -(float)System.Math.Cos(shipRotation)
                    );
                    float lookAheadDist = enemyShip.MoveSpeed * enemyShip.LookAheadDistance;
                    lookAheadTarget = enemyShip.Position + lookAheadDirection * lookAheadDist;
                }
                
                // Avoid friendly ships with orbital motion (same as friendly ships use)
                foreach (var friendlyShip in _friendlyShips)
                {
                    Vector2 toFriendly = friendlyShip.Position - enemyShip.Position;
                    float distance = toFriendly.Length();
                    float otherAvoidanceRadius = friendlyShip.AvoidanceDetectionRange;
                    float effectiveAvoidanceRadius = Math.Max(avoidanceRadius, otherAvoidanceRadius);
                    float avoidanceDetectionRange = effectiveAvoidanceRadius * 1.5f;
                    
                    // Check if look-ahead target is within other ship's avoidance radius
                    Vector2 toLookAheadFromFriendly = lookAheadTarget - friendlyShip.Position;
                    float lookAheadDistanceFromFriendly = toLookAheadFromFriendly.Length();
                    bool lookAheadInRadius = lookAheadDistanceFromFriendly < effectiveAvoidanceRadius;
                    
                    if ((distance < avoidanceDetectionRange || lookAheadInRadius) && distance > 0.1f)
                    {
                        toFriendly.Normalize();
                        Vector2 radialDirection = -toFriendly;
                        
                        // Calculate tangential direction for orbital motion
                        Vector2 tangentialDirection;
                        if (enemyShip.IsActivelyMoving() && enemyShip.Velocity.LengthSquared() > 100f)
                        {
                            Vector2 shipVelocity = enemyShip.Velocity;
                            shipVelocity.Normalize();
                            Vector2 tan1 = new Vector2(-toFriendly.Y, toFriendly.X);
                            Vector2 tan2 = new Vector2(toFriendly.Y, -toFriendly.X);
                            float dot1 = Vector2.Dot(shipVelocity, tan1);
                            float dot2 = Vector2.Dot(shipVelocity, tan2);
                            tangentialDirection = dot1 > dot2 ? tan1 : tan2;
                        }
                        else
                        {
                            tangentialDirection = new Vector2(-toFriendly.Y, toFriendly.X);
                        }
                        tangentialDirection.Normalize();
                        
                        // Calculate avoidance strength
                        float avoidanceStrength;
                        if (lookAheadInRadius)
                        {
                            float lookAheadPenetration = (effectiveAvoidanceRadius - lookAheadDistanceFromFriendly) / effectiveAvoidanceRadius;
                            avoidanceStrength = 1.5f + lookAheadPenetration * 2f;
                        }
                        else if (distance < effectiveAvoidanceRadius)
                        {
                            float penetration = (effectiveAvoidanceRadius - distance) / effectiveAvoidanceRadius;
                            avoidanceStrength = 1.0f + penetration * 1.5f;
                        }
                        else
                        {
                            avoidanceStrength = (avoidanceDetectionRange - distance) / (avoidanceDetectionRange - effectiveAvoidanceRadius);
                        }
                        
                        // Blend radial and tangential forces for orbital motion
                        float radialWeight, tangentialWeight;
                        if (distance < effectiveAvoidanceRadius)
                        {
                            radialWeight = 0.9f;
                            tangentialWeight = 0.1f;
                        }
                        else if (distance < effectiveAvoidanceRadius * 1.1f)
                        {
                            radialWeight = 0.7f;
                            tangentialWeight = 0.3f;
                        }
                        else if (distance < effectiveAvoidanceRadius * 1.3f)
                        {
                            radialWeight = 0.5f;
                            tangentialWeight = 0.5f;
                        }
                        else
                        {
                            radialWeight = 0.2f;
                            tangentialWeight = 0.8f;
                        }
                        
                        Vector2 orbitalDirection = radialDirection * radialWeight + tangentialDirection * tangentialWeight;
                        orbitalDirection.Normalize();
                        avoidanceVector += orbitalDirection * avoidanceStrength * avoidanceForce;
                    }
                }
                
                // Avoid player ship with orbital motion (but still pursue for attack)
                if (_playerShip != null)
                {
                    Vector2 toPlayer = _playerShip.Position - enemyShip.Position;
                    float distance = toPlayer.Length();
                    float playerAvoidanceRadius = _playerShip.AvoidanceDetectionRange;
                    float effectiveAvoidanceRadius = Math.Max(avoidanceRadius, playerAvoidanceRadius);
                    float avoidanceDetectionRange = effectiveAvoidanceRadius * 1.5f; // Increased range to prevent getting stuck
                    
                    // Check if look-ahead target is within player's avoidance radius
                    Vector2 toLookAheadFromPlayer = lookAheadTarget - _playerShip.Position;
                    float lookAheadDistanceFromPlayer = toLookAheadFromPlayer.Length();
                    bool lookAheadInPlayerRadius = lookAheadDistanceFromPlayer < effectiveAvoidanceRadius;
                    
                    // Avoid when close or when look-ahead hits player's radius
                    if ((distance < avoidanceDetectionRange || lookAheadInPlayerRadius) && distance > 0.1f)
                    {
                        toPlayer.Normalize();
                        Vector2 radialDirection = -toPlayer;
                        
                        // Calculate tangential direction
                        Vector2 tangentialDirection;
                        if (enemyShip.IsActivelyMoving() && enemyShip.Velocity.LengthSquared() > 100f)
                        {
                            Vector2 shipVelocity = enemyShip.Velocity;
                            shipVelocity.Normalize();
                            Vector2 tan1 = new Vector2(-toPlayer.Y, toPlayer.X);
                            Vector2 tan2 = new Vector2(toPlayer.Y, -toPlayer.X);
                            float dot1 = Vector2.Dot(shipVelocity, tan1);
                            float dot2 = Vector2.Dot(shipVelocity, tan2);
                            tangentialDirection = dot1 > dot2 ? tan1 : tan2;
                        }
                        else
                        {
                            tangentialDirection = new Vector2(-toPlayer.Y, toPlayer.X);
                        }
                        tangentialDirection.Normalize();
                        
                        // Calculate avoidance strength - stronger when very close to prevent getting stuck
                        float avoidanceStrength;
                        if (distance < effectiveAvoidanceRadius)
                        {
                            // Inside avoidance radius - strong radial push away
                            float penetration = (effectiveAvoidanceRadius - distance) / effectiveAvoidanceRadius;
                            avoidanceStrength = 1.5f + penetration * 2f; // Much stronger when inside radius
                        }
                        else if (lookAheadInPlayerRadius)
                        {
                            // Look-ahead is in radius - start turning
                            float lookAheadPenetration = (effectiveAvoidanceRadius - lookAheadDistanceFromPlayer) / effectiveAvoidanceRadius;
                            avoidanceStrength = 1.2f + lookAheadPenetration * 1.5f;
                        }
                        else
                        {
                            // Approaching avoidance radius - moderate avoidance
                            float approachFactor = (avoidanceDetectionRange - distance) / (avoidanceDetectionRange - effectiveAvoidanceRadius);
                            avoidanceStrength = 0.8f + approachFactor * 0.7f;
                        }
                        
                        // Blend radial and tangential - more radial when very close to prevent getting stuck
                        float radialWeight, tangentialWeight;
                        if (distance < effectiveAvoidanceRadius)
                        {
                            // Very close - prioritize radial push away
                            radialWeight = 0.8f;
                            tangentialWeight = 0.2f;
                        }
                        else if (distance < effectiveAvoidanceRadius * 1.1f)
                        {
                            // Close - balanced
                            radialWeight = 0.6f;
                            tangentialWeight = 0.4f;
                        }
                        else
                        {
                            // At safe distance - more orbital motion
                            radialWeight = 0.3f;
                            tangentialWeight = 0.7f;
                        }
                        
                        Vector2 orbitalDirection = radialDirection * radialWeight + tangentialDirection * tangentialWeight;
                        orbitalDirection.Normalize();
                        avoidanceVector += orbitalDirection * avoidanceStrength * avoidanceForce; // Full force to prevent getting stuck
                    }
                }
                
                // Avoid other enemy ships with orbital motion
                foreach (var otherEnemyShip in _enemyShips)
                {
                    if (otherEnemyShip == enemyShip) continue;
                    
                    Vector2 toOtherEnemy = otherEnemyShip.Position - enemyShip.Position;
                    float distance = toOtherEnemy.Length();
                    float otherAvoidanceRadius = otherEnemyShip.AvoidanceDetectionRange;
                    float effectiveAvoidanceRadius = Math.Max(avoidanceRadius, otherAvoidanceRadius);
                    float avoidanceDetectionRange = effectiveAvoidanceRadius * 1.5f;
                    
                    // Check if look-ahead target is within other enemy's avoidance radius
                    Vector2 toLookAheadFromOtherEnemy = lookAheadTarget - otherEnemyShip.Position;
                    float lookAheadDistanceFromOtherEnemy = toLookAheadFromOtherEnemy.Length();
                    bool lookAheadInOtherEnemyRadius = lookAheadDistanceFromOtherEnemy < effectiveAvoidanceRadius;
                    
                    if ((distance < avoidanceDetectionRange || lookAheadInOtherEnemyRadius) && distance > 0.1f)
                    {
                        toOtherEnemy.Normalize();
                        Vector2 radialDirection = -toOtherEnemy;
                        
                        // Calculate tangential direction
                        Vector2 tangentialDirection;
                        if (enemyShip.IsActivelyMoving() && enemyShip.Velocity.LengthSquared() > 100f)
                        {
                            Vector2 shipVelocity = enemyShip.Velocity;
                            shipVelocity.Normalize();
                            Vector2 tan1 = new Vector2(-toOtherEnemy.Y, toOtherEnemy.X);
                            Vector2 tan2 = new Vector2(toOtherEnemy.Y, -toOtherEnemy.X);
                            float dot1 = Vector2.Dot(shipVelocity, tan1);
                            float dot2 = Vector2.Dot(shipVelocity, tan2);
                            tangentialDirection = dot1 > dot2 ? tan1 : tan2;
                        }
                        else
                        {
                            tangentialDirection = new Vector2(-toOtherEnemy.Y, toOtherEnemy.X);
                        }
                        tangentialDirection.Normalize();
                        
                        // Calculate avoidance strength
                        float avoidanceStrength;
                        if (lookAheadInOtherEnemyRadius)
                        {
                            float lookAheadPenetration = (effectiveAvoidanceRadius - lookAheadDistanceFromOtherEnemy) / effectiveAvoidanceRadius;
                            avoidanceStrength = 1.5f + lookAheadPenetration * 2f;
                        }
                        else if (distance < effectiveAvoidanceRadius)
                        {
                            float penetration = (effectiveAvoidanceRadius - distance) / effectiveAvoidanceRadius;
                            avoidanceStrength = 1.0f + penetration * 1.5f;
                        }
                        else
                        {
                            avoidanceStrength = (avoidanceDetectionRange - distance) / (avoidanceDetectionRange - effectiveAvoidanceRadius);
                        }
                        
                        // Blend radial and tangential forces
                        float radialWeight, tangentialWeight;
                        if (distance < effectiveAvoidanceRadius)
                        {
                            radialWeight = 0.9f;
                            tangentialWeight = 0.1f;
                        }
                        else if (distance < effectiveAvoidanceRadius * 1.1f)
                        {
                            radialWeight = 0.7f;
                            tangentialWeight = 0.3f;
                        }
                        else if (distance < effectiveAvoidanceRadius * 1.3f)
                        {
                            radialWeight = 0.5f;
                            tangentialWeight = 0.5f;
                        }
                        else
                        {
                            radialWeight = 0.2f;
                            tangentialWeight = 0.8f;
                        }
                        
                        Vector2 orbitalDirection = radialDirection * radialWeight + tangentialDirection * tangentialWeight;
                        orbitalDirection.Normalize();
                        avoidanceVector += orbitalDirection * avoidanceStrength * avoidanceForce;
                    }
                }
                
                // Apply avoidance vector if significant
                if (avoidanceVector.LengthSquared() > 100f)
                {
                    Vector2 currentTarget = enemyShip.TargetPosition;
                    Vector2 avoidanceTarget = enemyShip.Position + avoidanceVector * deltaTime;
                    // Blend avoidance with current target (don't completely override pursuit)
                    Vector2 blendedTarget = Vector2.Lerp(currentTarget, avoidanceTarget, 0.3f);
                    enemyShip.SetTargetPosition(blendedTarget);
                }
                
                // Update ship position and rotation
                enemyShip.Update(gameTime);
                
                // Ship-to-ship collision detection and resolution (handled by CollisionManager)
                _collisionManager?.HandleEnemyFriendlyCollision(enemyShip, _friendlyShips);
                _collisionManager?.HandleEnemyPlayerCollision(enemyShip, _playerShip);
                
                // Ship-to-ship collision detection and resolution (handled by CollisionManager)
                _collisionManager?.HandleEnemyFriendlyCollision(enemyShip, _friendlyShips);
                _collisionManager?.HandleEnemyPlayerCollision(enemyShip, _playerShip);
                _collisionManager?.HandleEnemyShipCollisions(enemyShip, _enemyShips);
                
                // Behavior system: Update and execute aggressive behavior
                _behaviorManager?.UpdateEnemyShipBehavior(enemyShip, deltaTime);
                
                // Clamp position AFTER behavior system (in case behavior teleported ship off-screen)
                _collisionManager?.ClampEnemyShipToMapBounds(enemyShip);
            }
            
            // Populate spatial grid and update combat systems (lasers, collisions, explosions)
            _combatManager?.PopulateSpatialGrid(_playerShip);
            _combatManager?.Update(gameTime, _playerShip);
            
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

        // ========== Behavior System Methods ==========
        // (All behavior methods have been moved to BehaviorManager)
        // ========== End Behavior System Methods ==========
        
        // Helper methods for pathfinding/unstuck logic (used outside behavior system)
        private bool IsTooCloseToPlayer(Vector2 position, FriendlyShip friendlyShip)
        {
            if (_playerShip == null) return false;
            float distToPlayer = Vector2.Distance(position, _playerShip.Position);
            float minSafeDistance = _playerShip.AvoidanceDetectionRange * 1.5f;
            return distToPlayer < minSafeDistance;
        }
        
        private Vector2 AvoidPlayerPosition(Vector2 position, FriendlyShip friendlyShip)
        {
            const float mapBoundaryMargin = 200f;
            if (_playerShip == null) 
            {
                return new Vector2(
                    MathHelper.Clamp(position.X, mapBoundaryMargin, MapSize - mapBoundaryMargin),
                    MathHelper.Clamp(position.Y, mapBoundaryMargin, MapSize - mapBoundaryMargin)
                );
            }
            Vector2 toPosition = position - _playerShip.Position;
            float distToPlayer = toPosition.Length();
            float minSafeDistance = _playerShip.AvoidanceDetectionRange * 1.5f;
            if (distToPlayer < minSafeDistance && distToPlayer > 0.1f)
            {
                toPosition.Normalize();
                Vector2 adjustedPosition = _playerShip.Position + toPosition * minSafeDistance;
                adjustedPosition = new Vector2(
                    MathHelper.Clamp(adjustedPosition.X, mapBoundaryMargin, MapSize - mapBoundaryMargin),
                    MathHelper.Clamp(adjustedPosition.Y, mapBoundaryMargin, MapSize - mapBoundaryMargin)
                );
                return adjustedPosition;
            }
            return new Vector2(
                MathHelper.Clamp(position.X, mapBoundaryMargin, MapSize - mapBoundaryMargin),
                MathHelper.Clamp(position.Y, mapBoundaryMargin, MapSize - mapBoundaryMargin)
            );
        }
        
        private bool IsTooCloseToOtherShips(Vector2 position, FriendlyShip friendlyShip)
        {
            foreach (var otherShip in _friendlyShips)
            {
                if (otherShip == friendlyShip) continue;
                float distToOtherShip = Vector2.Distance(position, otherShip.Position);
                float minSafeDistance = MathHelper.Max(friendlyShip.AvoidanceDetectionRange, otherShip.AvoidanceDetectionRange);
                if (distToOtherShip < minSafeDistance) return true;
            }
            return false;
        }
        
        private Vector2 AvoidOtherShipsPosition(Vector2 position, FriendlyShip friendlyShip)
        {
            const float mapBoundaryMargin = 200f;
            Vector2 adjustedPosition = position;
            foreach (var otherShip in _friendlyShips)
            {
                if (otherShip == friendlyShip) continue;
                Vector2 toPosition = adjustedPosition - otherShip.Position;
                float distToOtherShip = toPosition.Length();
                float minSafeDistance = MathHelper.Max(friendlyShip.AvoidanceDetectionRange, otherShip.AvoidanceDetectionRange);
                if (distToOtherShip < minSafeDistance && distToOtherShip > 0.1f)
                {
                    toPosition.Normalize();
                    adjustedPosition = otherShip.Position + toPosition * minSafeDistance;
                }
            }
            return new Vector2(
                MathHelper.Clamp(adjustedPosition.X, mapBoundaryMargin, MapSize - mapBoundaryMargin),
                MathHelper.Clamp(adjustedPosition.Y, mapBoundaryMargin, MapSize - mapBoundaryMargin)
            );
        }
        
        public override void Draw(GameTime gameTime, SpriteBatch spriteBatch)
        {
            GraphicsDevice.Clear(Color.Black);

            // Get camera transform from CameraController
            Matrix transform;
            if (_cameraController != null)
            {
                transform = _cameraController.GetTransform(GraphicsDevice.Viewport);
            }
            else
            {
                // Fallback to manual calculation if CameraController not available
                transform = Matrix.CreateScale(_cameraZoom) * 
                           Matrix.CreateTranslation(
                               GraphicsDevice.Viewport.Width / 2f - _cameraPosition.X * _cameraZoom,
                               GraphicsDevice.Viewport.Height / 2f - _cameraPosition.Y * _cameraZoom,
                               0f
                           );
            }
            
            spriteBatch.Begin(
                SpriteSortMode.Deferred, 
                BlendState.AlphaBlend, 
                SamplerState.LinearWrap, // Use wrap mode for seamless tiling
                DepthStencilState.None, 
                RasterizerState.CullNone,
                null,
                transform
            );

            // Draw galaxy texture in 8x8 tile pattern
            if (_galaxyTexture != null)
            {
                // Calculate scale for each tile to cover the map
                const float mapSize = 8192f;
                const int tilesX = 8;
                const int tilesY = 8;
                float tileSize = mapSize / tilesX; // Each tile is 1024x1024
                
                float scaleX = tileSize / _tileSize.X;
                float scaleY = tileSize / _tileSize.Y;
                float scale = Math.Max(scaleX, scaleY);
                
                // Draw 8x8 grid of galaxy tiles
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
            
            // Draw galaxy overlay over the whole map at 30% opacity
            if (_galaxyOverlayTexture != null)
            {
                const float mapSize = 8192f;
                // Scale the overlay to cover the entire map
                float scaleX = mapSize / _galaxyOverlayTexture.Width;
                float scaleY = mapSize / _galaxyOverlayTexture.Height;
                float scale = Math.Max(scaleX, scaleY);
                
                spriteBatch.Draw(
                    _galaxyOverlayTexture,
                    Vector2.Zero,
                    null,
                    Color.White * 0.3f, // 30% opacity
                    0f,
                    Vector2.Zero,
                    scale,
                    SpriteEffects.None,
                    0f
                );
            }
            
            // Draw A* pathfinding grid (in world space, before ships)
            if (_pathfindingGridVisible && _pathfindingGrid != null && _gridPixelTexture != null)
            {
                // Update grid with current obstacles for visualization
                UpdatePathfindingGridForVisualization();
                _renderingManager?.DrawPathfindingGrid(spriteBatch, GraphicsDevice.Viewport, _pathfindingGrid, _cameraPosition, _cameraZoom);
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
            
            // Draw avoidance range circles if enabled
            if (_avoidanceRangeVisible)
            {
                _renderingManager?.DrawAvoidanceRange(spriteBatch, _playerShip, _friendlyShips);
            }
            
            // Draw player ship
            _playerShip?.Draw(spriteBatch);
            
            // Draw ship paths if enabled
            if (_enemyPathVisible)
            {
                _renderingManager?.DrawEnemyPaths(spriteBatch, _friendlyShips, _friendlyShipStates, GetOrCreateShipState);
            }
            
            // Draw enemy target paths if enabled
            if (_enemyTargetPathVisible)
            {
                _renderingManager?.DrawEnemyTargetPaths(spriteBatch, _friendlyShips);
            }
            
            // Draw look-ahead debug lines if enabled
            _renderingManager?.DrawLookAheadLines(spriteBatch, _playerShip, _friendlyShips, _enemyShips);
            
            // Draw friendly ships
            foreach (var friendlyShip in _friendlyShips)
            {
                // Draw ship (includes engine trail)
                friendlyShip.Draw(spriteBatch);
            }
            
            // Draw enemy ships
            foreach (var enemyShip in _enemyShips)
            {
                // Draw ship (includes engine trail)
                enemyShip.Draw(spriteBatch);
            }
            
            // Draw active explosions
            _combatManager?.DrawExplosions(spriteBatch);
            
            spriteBatch.End();
            
            // Draw health bars in screen space (after world-space batch ends)
            // Draw health bars using RenderingManager
            _renderingManager?.DrawHealthBars(spriteBatch, transform, _playerShip, _friendlyShips, _enemyShips);
            
            // Draw behavior labels in screen space (after world-space batch ends)
            if (_font != null && _behaviorTextVisible)
            {
                spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend);
                
                int behaviorLabelsDrawn = 0;
                foreach (var friendlyShip in _friendlyShips)
                {
                    if (_friendlyShipStates.TryGetValue(friendlyShip, out var shipState))
                    {
                        FriendlyShipBehavior behavior = shipState.Behavior;
                        string behaviorText = behavior.ToString();
                        behaviorLabelsDrawn++;
                        
                        // Calculate text position (below the ship in world space)
                        Vector2 shipPosition = friendlyShip.Position;
                        Texture2D? friendlyShipTexture = friendlyShip.GetTexture();
                        float offsetY = (friendlyShipTexture?.Height ?? 128) / 2f + 20f; // Half ship height + padding
                        Vector2 textWorldPosition = shipPosition + new Vector2(0, offsetY);
                        
                        // Transform world position to screen position
                        Vector2 screenPosition = Vector2.Transform(textWorldPosition, transform);
                        
                        // Measure text to center it (account for scale)
                        float fontScale = 0.7f; // Make font smaller
                        Vector2 textSize = _font.MeasureString(behaviorText) * fontScale;
                        Vector2 centeredPosition = screenPosition - new Vector2(textSize.X / 2, 0);
                        
                        // Draw text with a shadow for better visibility
                        spriteBatch.DrawString(_font, behaviorText, centeredPosition + new Vector2(1, 1), Color.Black, 0f, Vector2.Zero, fontScale, SpriteEffects.None, 0f);
                        spriteBatch.DrawString(_font, behaviorText, centeredPosition, Color.Yellow, 0f, Vector2.Zero, fontScale, SpriteEffects.None, 0f);
                    }
                }
                
                // Draw enemy ship behavior labels
                int enemyBehaviorLabelsDrawn = 0;
                foreach (var enemyShip in _enemyShips)
                {
                    if (_enemyShipStates.TryGetValue(enemyShip, out var enemyState))
                    {
                        FriendlyShipBehavior behavior = enemyState.Behavior;
                        string behaviorText = behavior.ToString();
                        enemyBehaviorLabelsDrawn++;
                        
                        // Calculate text position (below the ship in world space)
                        Vector2 shipPosition = enemyShip.Position;
                        Texture2D? enemyShipTexture = enemyShip.GetTexture();
                        float offsetY = (enemyShipTexture?.Height ?? 128) / 2f + 20f; // Half ship height + padding
                        Vector2 textWorldPosition = shipPosition + new Vector2(0, offsetY);
                        
                        // Transform world position to screen position
                        Vector2 screenPosition = Vector2.Transform(textWorldPosition, transform);
                        
                        // Measure text to center it (account for scale)
                        float fontScale = 0.7f; // Make font smaller
                        Vector2 textSize = _font.MeasureString(behaviorText) * fontScale;
                        Vector2 centeredPosition = screenPosition - new Vector2(textSize.X / 2, 0);
                        
                        // Draw text with a shadow for better visibility (red for enemy ships)
                        spriteBatch.DrawString(_font, behaviorText, centeredPosition + new Vector2(1, 1), Color.Black, 0f, Vector2.Zero, fontScale, SpriteEffects.None, 0f);
                        spriteBatch.DrawString(_font, behaviorText, centeredPosition, Color.Red, 0f, Vector2.Zero, fontScale, SpriteEffects.None, 0f);
                    }
                }
                
                // Debug output (only print occasionally to avoid spam)
                if ((behaviorLabelsDrawn > 0 || enemyBehaviorLabelsDrawn > 0) && System.Environment.TickCount % 3000 < 16) // Print roughly every 3 seconds
                {
                    System.Console.WriteLine($"[BEHAVIOR TEXT] Drawing {behaviorLabelsDrawn} friendly + {enemyBehaviorLabelsDrawn} enemy behavior labels, Visible: {_behaviorTextVisible}, Font: {_font != null}");
                }
                
                spriteBatch.End();
            }
            else if (System.Environment.TickCount % 3000 < 16) // Debug when not drawing
            {
                System.Console.WriteLine($"[BEHAVIOR TEXT] Not drawing - Font: {_font != null}, Visible: {_behaviorTextVisible}");
            }
            
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
            
            // Draw lasers with additive blending
            _combatManager?.DrawLasers(spriteBatch);

            spriteBatch.End();
            
            // Draw minimap in upper right corner (if visible)
            if (_minimapVisible)
            {
                _renderingManager?.DrawMinimap(spriteBatch, GraphicsDevice.Viewport, _playerShip, _friendlyShips, _enemyShips, _cameraPosition, _cameraZoom);
            }
            
            // Draw UI grid overlay if enabled (draw before UI so grid appears under UI)
            if (_uiGridVisible)
            {
                _renderingManager?.DrawUIGrid(spriteBatch, GraphicsDevice.Viewport);
            }
            
            // Draw UI overlay (zoom level) on top - only if visible
            if (_uiVisible)
            {
                _desktop?.Render();
                _saveButtonDesktop?.Render();
            }
            
            // Update coordinate label position in Draw to ensure it's always updated
            if (_uiGridVisible && _mouseCoordinateLabel != null)
            {
                var mouseState = Mouse.GetState();
                var mouseX = mouseState.X;
                var mouseY = mouseState.Y;
                
                // Calculate snapped grid point
                int snappedX = (mouseX / UIGridSize) * UIGridSize;
                int snappedY = (mouseY / UIGridSize) * UIGridSize;
                
                // Update coordinate label position to follow mouse (above cursor)
                _mouseCoordinateLabel.Text = $"({snappedX}, {snappedY})";
                _mouseCoordinateLabel.Margin = new Myra.Graphics2D.Thickness(mouseX, mouseY - 25, 0, 0); // 25px above cursor
                _mouseCoordinateLabel.Visible = true;
                
                // Render coordinate desktop after main UI so it appears on top
                if (_coordinateDesktop != null)
                {
                    _coordinateDesktop.Render();
                }
            }
            
            // Draw preview screen if active
            Texture2D? shipTexture = _previewShipIndex == 0 ? _previewShip1Texture : _previewShip2Texture;
                
            if (_isPreviewActive && shipTexture != null && _previewDesktop != null)
            {
                
                spriteBatch.Begin();
                
                // Draw semi-transparent background
                var bgTexture = new Texture2D(GraphicsDevice, 1, 1);
                bgTexture.SetData(new[] { new Color(0, 0, 0, 200) });
                int panelX = (GraphicsDevice.Viewport.Width - 500) / 2;
                int panelY = (GraphicsDevice.Viewport.Height - 500) / 2;
                spriteBatch.Draw(bgTexture, new Rectangle(panelX, panelY, 500, 500), Color.White);
                
                // Draw ship sprite centered in preview panel
                int spriteX = panelX + 250 - (shipTexture?.Width ?? 0) / 2;
                int spriteY = panelY + 250 - (shipTexture?.Height ?? 0) / 2;
                
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
            
            string className = _previewShipIndex == 0 ? "PlayerShip" : (_previewShipIndex == 1 ? "FriendlyShip" : "EnemyShip");
            _previewShipLabel.Text = $"{className} ({_previewShipIndex + 1}/3)";
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
                _playerShip = new PlayerShip(GraphicsDevice, Content, _random);
            _playerShip.Health = 50f; // Player has 50 health
            _playerShip.MaxHealth = 50f;
            _playerShip.Damage = 10f; // Player does 10 damage
            }
            else if (classIndex == 1)
            {
                _playerShip = new FriendlyShip(GraphicsDevice, Content, _random);
            }
            else // classIndex == 2
            {
                _playerShip = new EnemyShip(GraphicsDevice, Content, _random);
            }
            _playerShip.Position = mapCenter;
            
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
            
            string className = _currentShipClassIndex == 0 ? "PlayerShip" : (_currentShipClassIndex == 1 ? "FriendlyShip" : "EnemyShip");
            var filePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, $"settings_{className}.json");
            
            try
            {
                // Only include ShipIdleRate for FriendlyShip, not PlayerShip or EnemyShip
                if (_currentShipClassIndex == 0 || _currentShipClassIndex == 2)
                {
                    // PlayerShip or EnemyShip settings (no ShipIdleRate)
                    var settings = new
                    {
                        ShipSpeed = _playerShip.MoveSpeed,
                        TurnRate = _playerShip.RotationSpeed,
                        Inertia = _playerShip.Inertia,
                        AimRotationSpeed = _playerShip.AimRotationSpeed,
                        Drift = _playerShip.Drift,
                        AvoidanceDetectionRange = _avoidanceDetectionRange,
                        LookAheadDistance = _playerShip.LookAheadDistance,
                        LookAheadVisible = _playerShip.LookAheadVisible
                    };
                    var json = JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true });
                    File.WriteAllText(filePath, json);
                    System.Console.WriteLine($"Saved {className} settings to: {filePath}");
                }
                else // _currentShipClassIndex == 1
                {
                    // FriendlyShip settings (includes ShipIdleRate)
                    var settings = new
                    {
                        ShipSpeed = _playerShip.MoveSpeed,
                        TurnRate = _playerShip.RotationSpeed,
                        Inertia = _playerShip.Inertia,
                        AimRotationSpeed = _playerShip.AimRotationSpeed,
                        Drift = _playerShip.Drift,
                        AvoidanceDetectionRange = _avoidanceDetectionRange,
                        ShipIdleRate = _shipIdleRate,
                        LookAheadDistance = _playerShip.LookAheadDistance,
                        LookAheadVisible = _playerShip.LookAheadVisible
                    };
                    var json = JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true });
                    File.WriteAllText(filePath, json);
                    System.Console.WriteLine($"Saved {className} settings to: {filePath}");
                }
            }
            catch (Exception ex)
            {
                System.Console.WriteLine($"Failed to save {className} settings: {ex.Message}");
            }
        }
        
        private void LoadCurrentShipSettings()
        {
            if (_playerShip == null) return;
            
            string className = _currentShipClassIndex == 0 ? "PlayerShip" : (_currentShipClassIndex == 1 ? "FriendlyShip" : "EnemyShip");
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
                        if (_speedLabel != null) _speedLabel.Text = $"Ship Speed: {shipSpeed:F0}";
                    }
                    
                    // Load turn rate
                    if (settings.TryGetProperty("TurnRate", out var turnRateElement))
                    {
                        var turnRate = turnRateElement.GetSingle();
                        _playerShip.RotationSpeed = turnRate;
                        if (_turnRateSlider != null) _turnRateSlider.Value = turnRate;
                        if (_turnRateLabel != null) _turnRateLabel.Text = $"Ship Turn Rate: {turnRate:F1}";
                    }
                    
                    // Load inertia
                    if (settings.TryGetProperty("Inertia", out var inertiaElement))
                    {
                        var inertia = inertiaElement.GetSingle();
                        _playerShip.Inertia = inertia;
                        if (_inertiaSlider != null) _inertiaSlider.Value = inertia;
                        if (_inertiaLabel != null) _inertiaLabel.Text = $"Ship Inertia: {inertia:F2}";
                    }
                    
                    // Load aim rotation speed
                    if (settings.TryGetProperty("AimRotationSpeed", out var aimRotationSpeedElement))
                    {
                        var aimRotationSpeed = aimRotationSpeedElement.GetSingle();
                        _playerShip.AimRotationSpeed = aimRotationSpeed;
                        if (_aimRotationSpeedSlider != null) _aimRotationSpeedSlider.Value = aimRotationSpeed;
                        if (_aimRotationSpeedLabel != null) _aimRotationSpeedLabel.Text = $"Ship Idle Rotation Speed: {aimRotationSpeed:F1}";
                    }
                    
                    // Load drift
                    if (settings.TryGetProperty("Drift", out var driftElement))
                    {
                        var drift = driftElement.GetSingle();
                        _playerShip.Drift = drift;
                        if (_driftSlider != null) _driftSlider.Value = drift;
                        if (_driftLabel != null) _driftLabel.Text = $"Ship Drift: {drift:F2}";
                        
                        // If loading FriendlyShip settings, also update all friendly ships with their own class drift value
                        if (_currentShipClassIndex == 1)
                        {
                            foreach (var friendlyShip in _friendlyShips)
                            {
                                friendlyShip.Drift = drift; // Use drift from FriendlyShip settings
                            }
                        }
                    }
                    
                    // Load avoidance detection range
                    if (settings.TryGetProperty("AvoidanceDetectionRange", out var avoidanceRangeElement))
                    {
                        var avoidanceRange = avoidanceRangeElement.GetSingle();
                        _avoidanceDetectionRange = MathHelper.Clamp(avoidanceRange, 100f, 1000f);
                        if (_avoidanceRangeSlider != null) _avoidanceRangeSlider.Value = _avoidanceDetectionRange;
                        if (_avoidanceRangeLabel != null) _avoidanceRangeLabel.Text = $"Avoidance Detection Range: {_avoidanceDetectionRange:F0}";
                        
                        // Apply to ships based on current ship class
                        if (_currentShipClassIndex == 1)
                        {
                            // FriendlyShip class - update all friendly ships
                            foreach (var friendlyShip in _friendlyShips)
                            {
                                friendlyShip.AvoidanceDetectionRange = _avoidanceDetectionRange;
                            }
                        }
                        else if (_currentShipClassIndex == 2)
                        {
                            // EnemyShip class - update all enemy ships
                            foreach (var enemyShip in _enemyShips)
                            {
                                enemyShip.AvoidanceDetectionRange = _avoidanceDetectionRange;
                            }
                        }
                        else if (_playerShip != null)
                        {
                            // PlayerShip class - update player ship
                            _playerShip.AvoidanceDetectionRange = _avoidanceDetectionRange;
                        }
                    }
                    
                    // Load ship idle rate
                    if (settings.TryGetProperty("ShipIdleRate", out var shipIdleRateElement))
                    {
                        var shipIdleRate = shipIdleRateElement.GetSingle();
                        _shipIdleRate = MathHelper.Clamp(shipIdleRate, 0f, 1f);
                        if (_shipIdleRateSlider != null) _shipIdleRateSlider.Value = _shipIdleRate;
                        if (_shipIdleRateLabel != null) _shipIdleRateLabel.Text = $"Ship Idle Rate: {(_shipIdleRate * 100f):F0}%";
                    }
                    
                    // Load look-ahead distance
                    if (settings.TryGetProperty("LookAheadDistance", out var lookAheadDistanceElement))
                    {
                        var lookAheadDistance = lookAheadDistanceElement.GetSingle();
                        if (_playerShip != null)
                        {
                            _playerShip.LookAheadDistance = MathHelper.Clamp(lookAheadDistance, 0.5f, 5.0f);
                            if (_lookAheadSlider != null) _lookAheadSlider.Value = _playerShip.LookAheadDistance;
                            if (_lookAheadLabel != null) _lookAheadLabel.Text = $"Look-Ahead Distance: {_playerShip.LookAheadDistance:F2}x";
                            
                            // If loading FriendlyShip or EnemyShip settings, also update all ships of that class
                            if (_currentShipClassIndex == 1)
                            {
                                foreach (var friendlyShip in _friendlyShips)
                                {
                                    friendlyShip.LookAheadDistance = _playerShip.LookAheadDistance;
                                }
                            }
                            else if (_currentShipClassIndex == 2)
                            {
                                foreach (var enemyShip in _enemyShips)
                                {
                                    enemyShip.LookAheadDistance = _playerShip.LookAheadDistance;
                                }
                            }
                        }
                    }
                    
                    // Load look-ahead visible
                    if (settings.TryGetProperty("LookAheadVisible", out var lookAheadVisibleElement))
                    {
                        var lookAheadVisible = lookAheadVisibleElement.GetBoolean();
                        if (_playerShip != null)
                        {
                            _playerShip.LookAheadVisible = lookAheadVisible;
                        }
                        if (_lookAheadVisibleCheckBox != null) _lookAheadVisibleCheckBox.IsChecked = lookAheadVisible;
                        
                        // Update all ships of the current class with the checkbox state
                        if (_currentShipClassIndex == 1)
                        {
                            foreach (var friendlyShip in _friendlyShips)
                            {
                                friendlyShip.LookAheadVisible = lookAheadVisible;
                            }
                        }
                        else if (_currentShipClassIndex == 2)
                        {
                            foreach (var enemyShip in _enemyShips)
                            {
                                enemyShip.LookAheadVisible = lookAheadVisible;
                            }
                        }
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
        
        private void UpdatePathfindingGridForVisualization()
        {
            if (_pathfindingGrid == null) return;
            
            // Clear and update obstacles for visualization
            _pathfindingGrid.ClearObstacles();
            
            // Use a standard obstacle radius for visualization
            float obstacleRadius = 150f; // Standard radius for visualization
            
            // Mark all friendly ships as obstacles
            foreach (var ship in _friendlyShips)
            {
                _pathfindingGrid.SetObstacle(ship.Position, obstacleRadius, true);
            }
            
            // Mark player ship as obstacle
            if (_playerShip != null)
            {
                _pathfindingGrid.SetObstacle(_playerShip.Position, obstacleRadius, true);
            }
        }
        
        // DrawUIGrid, DrawPathfindingGrid, and DrawMinimap methods moved to RenderingManager
        
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
        
        // DrawAvoidanceRange, DrawEnemyPaths, and DrawEnemyTargetPaths methods moved to RenderingManager
        
        // DrawLookAheadLines, DrawCircle, DrawHealthBars, and DrawHealthBarForShip methods moved to RenderingManager
    }
    
    // A* Pathfinding System
    public class PathNode
    {
        public int X { get; set; }
        public int Y { get; set; }
        public bool Walkable { get; set; } = true;
        public float GCost { get; set; } = 0f; // Distance from start
        public float HCost { get; set; } = 0f; // Heuristic distance to goal
        public float FCost => GCost + HCost; // Total cost
        public PathNode? Parent { get; set; }
        
        public PathNode(int x, int y)
        {
            X = x;
            Y = y;
        }
        
        public Vector2 ToWorldPosition(float cellSize)
        {
            return new Vector2(X * cellSize + cellSize / 2f, Y * cellSize + cellSize / 2f);
        }
    }
    
    public class PathfindingGrid
    {
        private PathNode[,] _grid;
        private int _gridWidth;
        private int _gridHeight;
        private float _cellSize;
        private float _mapSize;
        
        public int GridWidth => _gridWidth;
        public int GridHeight => _gridHeight;
        public float CellSize => _cellSize;
        
        public PathfindingGrid(float mapSize, float cellSize = 128f)
        {
            _mapSize = mapSize;
            _cellSize = cellSize;
            _gridWidth = (int)Math.Ceiling(mapSize / cellSize);
            _gridHeight = (int)Math.Ceiling(mapSize / cellSize);
            _grid = new PathNode[_gridWidth, _gridHeight];
            
            // Initialize all nodes as walkable, except edge cells
            for (int x = 0; x < _gridWidth; x++)
            {
                for (int y = 0; y < _gridHeight; y++)
                {
                    // Mark edge cells as unwalkable to keep ships within map bounds
                    Vector2 worldPos = new Vector2(x * cellSize + cellSize / 2f, y * cellSize + cellSize / 2f);
                    bool isEdge = worldPos.X < 100f || worldPos.X > mapSize - 100f || 
                                  worldPos.Y < 100f || worldPos.Y > mapSize - 100f;
                    _grid[x, y] = new PathNode(x, y) { Walkable = !isEdge };
                }
            }
        }
        
        public PathNode? GetNode(int x, int y)
        {
            if (x < 0 || x >= _gridWidth || y < 0 || y >= _gridHeight)
                return null;
            return _grid[x, y];
        }
        
        public PathNode? GetNodeAt(int x, int y)
        {
            return GetNode(x, y);
        }
        
        public PathNode? GetNodeFromWorld(Vector2 worldPos)
        {
            int x = (int)(worldPos.X / _cellSize);
            int y = (int)(worldPos.Y / _cellSize);
            return GetNode(x, y);
        }
        
        public void SetObstacle(Vector2 worldPos, float radius, bool isObstacle)
        {
            int centerX = (int)(worldPos.X / _cellSize);
            int centerY = (int)(worldPos.Y / _cellSize);
            int radiusInCells = (int)Math.Ceiling(radius / _cellSize) + 1;
            
            for (int x = centerX - radiusInCells; x <= centerX + radiusInCells; x++)
            {
                for (int y = centerY - radiusInCells; y <= centerY + radiusInCells; y++)
                {
                    var node = GetNode(x, y);
                    if (node != null)
                    {
                        Vector2 nodeWorldPos = node.ToWorldPosition(_cellSize);
                        float dist = Vector2.Distance(worldPos, nodeWorldPos);
                        if (dist <= radius)
                        {
                            node.Walkable = !isObstacle;
                        }
                    }
                }
            }
        }
        
        public void ClearObstacles()
        {
            for (int x = 0; x < _gridWidth; x++)
            {
                for (int y = 0; y < _gridHeight; y++)
                {
                    // Preserve edge obstacles - only clear non-edge cells
                    Vector2 worldPos = new Vector2(x * _cellSize + _cellSize / 2f, y * _cellSize + _cellSize / 2f);
                    bool isEdge = worldPos.X < 100f || worldPos.X > _mapSize - 100f || 
                                  worldPos.Y < 100f || worldPos.Y > _mapSize - 100f;
                    if (!isEdge)
                    {
                        _grid[x, y].Walkable = true;
                    }
                }
            }
        }
        
        public System.Collections.Generic.List<Vector2> FindPath(Vector2 start, Vector2 end)
        {
            var path = new System.Collections.Generic.List<Vector2>();
            
            // Clamp start and end positions to map bounds
            start = new Vector2(MathHelper.Clamp(start.X, 100f, _mapSize - 100f), MathHelper.Clamp(start.Y, 100f, _mapSize - 100f));
            end = new Vector2(MathHelper.Clamp(end.X, 100f, _mapSize - 100f), MathHelper.Clamp(end.Y, 100f, _mapSize - 100f));
            
            PathNode? startNode = GetNodeFromWorld(start);
            PathNode? endNode = GetNodeFromWorld(end);
            
            if (startNode == null || endNode == null || !startNode.Walkable || !endNode.Walkable)
            {
                // If start or end is invalid, return clamped direct path
                path.Add(end);
                return path;
            }
            
            var openSet = new System.Collections.Generic.HashSet<PathNode>();
            var closedSet = new System.Collections.Generic.HashSet<PathNode>();
            
            // Reset all nodes
            for (int x = 0; x < _gridWidth; x++)
            {
                for (int y = 0; y < _gridHeight; y++)
                {
                    _grid[x, y].GCost = 0f;
                    _grid[x, y].HCost = 0f;
                    _grid[x, y].Parent = null;
                }
            }
            
            openSet.Add(startNode);
            
            while (openSet.Count > 0)
            {
                // Find node with lowest F cost
                PathNode? currentNode = null;
                float lowestFCost = float.MaxValue;
                foreach (var node in openSet)
                {
                    if (currentNode == null || node.FCost < lowestFCost || (node.FCost == lowestFCost && node.HCost < currentNode.HCost))
                    {
                        lowestFCost = node.FCost;
                        currentNode = node;
                    }
                }
                
                if (currentNode == null) break;
                
                openSet.Remove(currentNode);
                closedSet.Add(currentNode);
                
                // Check if we reached the goal
                if (currentNode == endNode)
                {
                    // Reconstruct path
                    var nodePath = new System.Collections.Generic.List<PathNode>();
                    PathNode? current = currentNode;
                    while (current != null)
                    {
                        nodePath.Add(current);
                        current = current.Parent;
                    }
                    nodePath.Reverse();
                    
                    // Convert to world positions and simplify path (less granular)
                    if (nodePath.Count > 0)
                    {
                        path.Add(start); // Always start with actual start position
                        
                        // Simplified path: only add waypoints when direction changes significantly
                        // Use a larger angle threshold to reduce granularity
                        const float minAngleChange = 0.3f; // ~17 degrees minimum change to add waypoint
                        const float minDistance = 256f; // Minimum distance between waypoints
                        
                        for (int i = 1; i < nodePath.Count - 1; i++)
                        {
                            if (i > 0 && i < nodePath.Count - 1)
                            {
                                Vector2 prev = nodePath[i - 1].ToWorldPosition(_cellSize);
                                Vector2 curr = nodePath[i].ToWorldPosition(_cellSize);
                                Vector2 next = nodePath[i + 1].ToWorldPosition(_cellSize);
                                
                                Vector2 dir1 = curr - prev;
                                Vector2 dir2 = next - curr;
                                
                                // Check distance from last waypoint
                                float distFromLast = path.Count > 0 ? Vector2.Distance(path[path.Count - 1], curr) : float.MaxValue;
                                
                                if (dir1.LengthSquared() > 0.1f && dir2.LengthSquared() > 0.1f)
                                {
                                    dir1.Normalize();
                                    dir2.Normalize();
                                    
                                    float dot = Vector2.Dot(dir1, dir2);
                                    float angleChange = (float)Math.Acos(MathHelper.Clamp(dot, -1f, 1f));
                                    
                                    // Add waypoint if angle changes significantly AND it's far enough from last waypoint
                                    if (angleChange > minAngleChange && distFromLast > minDistance)
                                    {
                                        path.Add(curr);
                                    }
                                }
                                else if (distFromLast > minDistance * 2f)
                                {
                                    // Add waypoint if it's very far from last one (even if direction doesn't change much)
                                    path.Add(curr);
                                }
                            }
                        }
                        
                        path.Add(end); // Always end with actual end position
                    }
                    else
                    {
                        path.Add(end);
                    }
                    
                    return path;
                }
                
                // Check neighbors (8-directional)
                for (int dx = -1; dx <= 1; dx++)
                {
                    for (int dy = -1; dy <= 1; dy++)
                    {
                        if (dx == 0 && dy == 0) continue;
                        
                        int neighborX = currentNode.X + dx;
                        int neighborY = currentNode.Y + dy;
                        var neighbor = GetNode(neighborX, neighborY);
                        
                        if (neighbor == null || !neighbor.Walkable || closedSet.Contains(neighbor))
                            continue;
                        
                        // Calculate movement cost (diagonal costs more)
                        float movementCost = (dx != 0 && dy != 0) ? 1.414f : 1f;
                        float newGCost = currentNode.GCost + movementCost;
                        
                        if (!openSet.Contains(neighbor))
                        {
                            openSet.Add(neighbor);
                        }
                        else if (newGCost >= neighbor.GCost)
                        {
                            continue; // This path is not better
                        }
                        
                        // This path is better
                        neighbor.Parent = currentNode;
                        neighbor.GCost = newGCost;
                        neighbor.HCost = Heuristic(neighbor, endNode);
                    }
                }
            }
            
            // No path found, return direct path
            path.Add(end);
            return path;
        }
        
        private float Heuristic(PathNode a, PathNode b)
        {
            // Manhattan distance (can use Euclidean for more accurate)
            int dx = Math.Abs(a.X - b.X);
            int dy = Math.Abs(a.Y - b.Y);
            return dx + dy; // Manhattan
            // return (float)Math.Sqrt(dx * dx + dy * dy); // Euclidean (more accurate but slower)
        }
    }
}

