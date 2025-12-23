using System;
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
        private Texture2D? _pixelTexture; // 1x1 pixel texture for drawing health bars
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
        private int _currentShipClassIndex = 0; // 0 = PlayerShip, 1 = FriendlyShip, 2 = EnemyShip
        
        // Friendly ships
        private System.Collections.Generic.List<FriendlyShip> _friendlyShips = new System.Collections.Generic.List<FriendlyShip>();
        private System.Collections.Generic.Dictionary<FriendlyShip, FriendlyShipBehavior> _friendlyShipBehaviors = new System.Collections.Generic.Dictionary<FriendlyShip, FriendlyShipBehavior>();
        private System.Collections.Generic.Dictionary<FriendlyShip, float> _friendlyShipBehaviorTimer = new System.Collections.Generic.Dictionary<FriendlyShip, float>(); // Timer for current behavior
        
        // Enemy ships
        private System.Collections.Generic.List<EnemyShip> _enemyShips = new System.Collections.Generic.List<EnemyShip>();
        
        // Active explosions (explosions that continue after ships are destroyed)
        private System.Collections.Generic.List<ExplosionEffect> _activeExplosions = new System.Collections.Generic.List<ExplosionEffect>();
        private System.Collections.Generic.Dictionary<EnemyShip, FriendlyShipBehavior> _enemyShipBehaviors = new System.Collections.Generic.Dictionary<EnemyShip, FriendlyShipBehavior>();
        private System.Collections.Generic.Dictionary<EnemyShip, float> _enemyShipBehaviorTimer = new System.Collections.Generic.Dictionary<EnemyShip, float>(); // Timer for current behavior
        private System.Collections.Generic.Dictionary<EnemyShip, float> _enemyShipAttackCooldown = new System.Collections.Generic.Dictionary<EnemyShip, float>(); // Cooldown between attacks
        private const float EnemyPlayerDetectionRange = 1500f; // Range at which enemy detects and switches to aggressive behavior
        private System.Collections.Generic.Dictionary<FriendlyShip, Vector2> _friendlyShipLastAvoidanceTarget = new System.Collections.Generic.Dictionary<FriendlyShip, Vector2>();
        private System.Collections.Generic.Dictionary<FriendlyShip, Vector2> _friendlyShipOriginalDestination = new System.Collections.Generic.Dictionary<FriendlyShip, Vector2>(); // Store original destination before avoidance
        private System.Collections.Generic.Dictionary<FriendlyShip, Vector2> _friendlyShipLastPosition = new System.Collections.Generic.Dictionary<FriendlyShip, Vector2>();
        private System.Collections.Generic.Dictionary<FriendlyShip, float> _friendlyShipStuckTimer = new System.Collections.Generic.Dictionary<FriendlyShip, float>();
        private System.Collections.Generic.Dictionary<FriendlyShip, System.Collections.Generic.List<Vector2>> _friendlyShipPaths = new System.Collections.Generic.Dictionary<FriendlyShip, System.Collections.Generic.List<Vector2>>();
        private System.Collections.Generic.Dictionary<FriendlyShip, System.Collections.Generic.List<Vector2>> _friendlyShipPatrolPoints = new System.Collections.Generic.Dictionary<FriendlyShip, System.Collections.Generic.List<Vector2>>(); // Patrol waypoints
        
        // Enemy ship behavior tracking (shared with friendly ship dictionaries where applicable)
        private System.Collections.Generic.Dictionary<EnemyShip, System.Collections.Generic.List<Vector2>> _enemyShipPatrolPoints = new System.Collections.Generic.Dictionary<EnemyShip, System.Collections.Generic.List<Vector2>>(); // Patrol waypoints for enemy ships
        private System.Collections.Generic.Dictionary<FriendlyShip, Vector2> _friendlyShipLastDirection = new System.Collections.Generic.Dictionary<FriendlyShip, Vector2>(); // Track last movement direction for smooth pathing
        private const int MaxPathPoints = 100; // Maximum number of path points to store per ship
        private System.Random _random = new System.Random();
        
        // A* Pathfinding system
        private PathfindingGrid? _pathfindingGrid;
        private System.Collections.Generic.Dictionary<FriendlyShip, System.Collections.Generic.List<Vector2>> _friendlyShipAStarPaths = new System.Collections.Generic.Dictionary<FriendlyShip, System.Collections.Generic.List<Vector2>>(); // A* calculated paths
        private System.Collections.Generic.Dictionary<FriendlyShip, int> _friendlyShipCurrentWaypointIndex = new System.Collections.Generic.Dictionary<FriendlyShip, int>(); // Current waypoint in A* path
        private System.Collections.Generic.Dictionary<FriendlyShip, float> _friendlyShipClosestDistanceToTarget = new System.Collections.Generic.Dictionary<FriendlyShip, float>(); // Track closest distance reached to target
        private System.Collections.Generic.Dictionary<FriendlyShip, float> _friendlyShipNoProgressTimer = new System.Collections.Generic.Dictionary<FriendlyShip, float>(); // Timer for how long ship hasn't made progress
        private System.Collections.Generic.Dictionary<FriendlyShip, Vector2> _friendlyShipLastTarget = new System.Collections.Generic.Dictionary<FriendlyShip, Vector2>(); // Track last target to detect target changes
        private System.Collections.Generic.Dictionary<FriendlyShip, Vector2> _friendlyShipLookAheadTarget = new System.Collections.Generic.Dictionary<FriendlyShip, Vector2>(); // Store look-ahead target for debug line drawing
        
        // Behavior duration ranges (in seconds) - increased for longer behavior changes
        private const float IdleMinDuration = 8f;
        private const float IdleMaxDuration = 20f;
        private const float PatrolMinDuration = 20f;
        private const float PatrolMaxDuration = 50f;
        private const float LongDistanceMinDuration = 40f;
        private const float LongDistanceMaxDuration = 120f;
        private const float WanderMinDuration = 10f;
        private const float WanderMaxDuration = 30f;
        
        // Lasers
        private System.Collections.Generic.List<Laser> _lasers = new System.Collections.Generic.List<Laser>();
        
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
        private SoundEffect? _laserFireSound;
        private SoundEffect? _explosionSound;
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

        public override void LoadContent()
        {
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
            
            // Load explosion sound effect
            try
            {
                _explosionSound = Content.Load<SoundEffect>("explosion1");
                System.Console.WriteLine($"[EXPLOSION SOUND] Loaded successfully: {_explosionSound != null}");
            }
            catch (System.Exception ex)
            {
                System.Console.WriteLine($"[EXPLOSION SOUND ERROR] Failed to load explosion sound: {ex.Message}");
                System.Console.WriteLine($"[EXPLOSION SOUND ERROR] Stack trace: {ex.StackTrace}");
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
            _playerShip = new PlayerShip(GraphicsDevice, Content);
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
                var friendlyShip = new FriendlyShip(GraphicsDevice, Content);
                // Initialize behavior system
                _friendlyShipBehaviors[friendlyShip] = GetRandomBehavior();
                _friendlyShipBehaviorTimer[friendlyShip] = GetBehaviorDuration(_friendlyShipBehaviors[friendlyShip]);
                // Initialize direction tracking with random direction
                float initialAngle = (float)(_random.NextDouble() * MathHelper.TwoPi);
                _friendlyShipLastDirection[friendlyShip] = new Vector2((float)Math.Cos(initialAngle), (float)Math.Sin(initialAngle));
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
            }
            
            // Create 3 enemy ships at random positions
            for (int i = 0; i < 3; i++)
            {
                var enemyShip = new EnemyShip(GraphicsDevice, Content);
                // Initialize behavior system - start with random behavior like friendly ships
                _enemyShipBehaviors[enemyShip] = GetRandomBehavior();
                _enemyShipBehaviorTimer[enemyShip] = GetBehaviorDuration(_enemyShipBehaviors[enemyShip]);
                _enemyShipAttackCooldown[enemyShip] = 0f;
                
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
                enemyShip.Health = 100f; // Enemy ships have 100 health
                enemyShip.MaxHealth = 100f;
                enemyShip.Damage = 5f; // Enemy ships do 5 damage
                
                // Initialize direction tracking with random direction (for behaviors)
                float initialAngle = (float)(_random.NextDouble() * MathHelper.TwoPi);
                // Cast to FriendlyShip to use shared dictionaries
                FriendlyShip enemyAsFriendly = (FriendlyShip)enemyShip;
                if (!_friendlyShipLastDirection.ContainsKey(enemyAsFriendly))
                {
                    _friendlyShipLastDirection[enemyAsFriendly] = new Vector2((float)Math.Cos(initialAngle), (float)Math.Sin(initialAngle));
                }
                
                _enemyShips.Add(enemyShip);
            }
            
            System.Console.WriteLine($"[ENEMY SHIPS] Created {_enemyShips.Count} enemy ships at positions:");
            foreach (var enemyShip in _enemyShips)
            {
                System.Console.WriteLine($"  Enemy ship at ({enemyShip.Position.X:F0}, {enemyShip.Position.Y:F0})");
            }
            
            // Initialize camera to center on player
            _cameraPosition = mapCenter;
            
            // Initialize A* pathfinding grid
            _pathfindingGrid = new PathfindingGrid(MapSize, 128f); // 128 pixel cells
            
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
                Padding = new Myra.Graphics2D.Thickness(0, 0, 0, 0),
                Margin = new Myra.Graphics2D.Thickness(0, 0, 0, 0) // No margin needed, VerticalStackPanel handles spacing
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
            
            // Create a container panel to hold both UI panel and camera settings panel (vertical layout)
            var containerPanel = new VerticalStackPanel
            {
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top,
                Spacing = 10, // Padding between panels
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
                // Fire lasers from player ship positions in the direction of the cursor
                if (_playerShip != null)
                {
                    // Convert mouse position to world coordinates
                    var mouseScreenPos = new Vector2(mouseState.X, mouseState.Y);
                    var mouseWorldX = (mouseScreenPos.X - GraphicsDevice.Viewport.Width / 2f) / _cameraZoom + _cameraPosition.X;
                    var mouseWorldY = (mouseScreenPos.Y - GraphicsDevice.Viewport.Height / 2f) / _cameraZoom + _cameraPosition.Y;
                    var mouseWorldPos = new Vector2(mouseWorldX, mouseWorldY);
                    
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
                                var laser = new Laser(laserSpawnPosition, laserDirection, GraphicsDevice, _playerShip.Damage, _playerShip);
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
                                if (_friendlyShipLastPosition.ContainsKey(friendlyShip))
                                {
                                    shipVelocity = friendlyShip.Position - _friendlyShipLastPosition[friendlyShip];
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
                    
                    // Check if target has changed - if so, reset progress tracking
                    if (_friendlyShipLastTarget.ContainsKey(friendlyShip))
                    {
                        Vector2 lastTarget = _friendlyShipLastTarget[friendlyShip];
                        if (Vector2.Distance(lastTarget, currentTarget) > 50f) // Target changed significantly
                        {
                            // Target changed - reset progress tracking
                            _friendlyShipClosestDistanceToTarget.Remove(friendlyShip);
                            _friendlyShipNoProgressTimer.Remove(friendlyShip);
                        }
                    }
                    _friendlyShipLastTarget[friendlyShip] = currentTarget;
                    
                    // Track progress toward destination to detect if ship is trapped
                    if (!_friendlyShipClosestDistanceToTarget.ContainsKey(friendlyShip))
                    {
                        _friendlyShipClosestDistanceToTarget[friendlyShip] = distanceToTarget;
                        _friendlyShipNoProgressTimer[friendlyShip] = 0f;
                    }
                    
                    // Check if ship is making progress (getting closer to target)
                    float closestDistance = _friendlyShipClosestDistanceToTarget[friendlyShip];
                    bool isMakingProgress = distanceToTarget < closestDistance - 10f; // Must get at least 10 pixels closer
                    
                    if (isMakingProgress)
                    {
                        // Ship is making progress - update closest distance and reset timer
                        _friendlyShipClosestDistanceToTarget[friendlyShip] = distanceToTarget;
                        _friendlyShipNoProgressTimer[friendlyShip] = 0f;
                    }
                    else
                    {
                        // Ship is not making progress - increment timer
                        _friendlyShipNoProgressTimer[friendlyShip] += deltaTime;
                    }
                    
                    // If ship hasn't made progress for 3 seconds, it's likely trapped - force new path
                    bool isTrapped = _friendlyShipNoProgressTimer[friendlyShip] > 3.0f;
                    
                    // Check if we need to recalculate path (no path exists, reached waypoint, path is invalid, or ship is trapped)
                    bool needsNewPath = false;
                    if (isTrapped)
                    {
                        needsNewPath = true;
                        // Reset progress tracking for new path
                        _friendlyShipClosestDistanceToTarget.Remove(friendlyShip);
                        _friendlyShipNoProgressTimer.Remove(friendlyShip);
                    }
                    if (!_friendlyShipAStarPaths.ContainsKey(friendlyShip) || _friendlyShipAStarPaths[friendlyShip].Count == 0)
                    {
                        needsNewPath = true;
                    }
                    else
                    {
                        // Check if we've reached the current waypoint
                        int currentWaypointIndex = _friendlyShipCurrentWaypointIndex.ContainsKey(friendlyShip) 
                            ? _friendlyShipCurrentWaypointIndex[friendlyShip] 
                            : 0;
                        
                        if (currentWaypointIndex < _friendlyShipAStarPaths[friendlyShip].Count)
                        {
                            Vector2 currentWaypoint = _friendlyShipAStarPaths[friendlyShip][currentWaypointIndex];
                            float distToWaypoint = Vector2.Distance(currentPos, currentWaypoint);
                            
                            if (distToWaypoint < 100f) // Reached waypoint
                            {
                                currentWaypointIndex++;
                                _friendlyShipCurrentWaypointIndex[friendlyShip] = currentWaypointIndex;
                                
                                // If we've reached all waypoints, check if we need a new path to final destination
                                if (currentWaypointIndex >= _friendlyShipAStarPaths[friendlyShip].Count)
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
                        _friendlyShipAStarPaths[friendlyShip] = path;
                        _friendlyShipCurrentWaypointIndex[friendlyShip] = 0;
                        
                        // Reset progress tracking when calculating new path
                        _friendlyShipClosestDistanceToTarget.Remove(friendlyShip);
                        _friendlyShipNoProgressTimer.Remove(friendlyShip);
                    }
                    
                    // Follow A* path waypoints with look-ahead for smoother turning
                    if (_friendlyShipAStarPaths.ContainsKey(friendlyShip) && _friendlyShipAStarPaths[friendlyShip].Count > 0)
                    {
                        int waypointIndex = _friendlyShipCurrentWaypointIndex.ContainsKey(friendlyShip) 
                            ? _friendlyShipCurrentWaypointIndex[friendlyShip] 
                            : 0;
                        
                        var path = _friendlyShipAStarPaths[friendlyShip];
                        
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
                            _friendlyShipLookAheadTarget[friendlyShip] = pathLookAheadTarget;
                            
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
                                _friendlyShipLookAheadTarget[friendlyShip] = endPathLookAheadTarget;
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
                    _friendlyShipLookAheadTarget[friendlyShip] = newAvoidanceTarget;
                    
                    friendlyShip.SetTargetPosition(newAvoidanceTarget);
                }
                
                friendlyShip.Update(gameTime);
                
                // Ship-to-ship collision detection and resolution (prevent ships from entering each other's avoidance radius)
                // Use full avoidance range as the minimum distance ships must maintain
                float shipAvoidanceRadius = friendlyShip.AvoidanceDetectionRange;
                foreach (var otherShip in _friendlyShips)
                {
                    if (otherShip == friendlyShip) continue;
                    
                    Vector2 direction = friendlyShip.Position - otherShip.Position;
                    float distance = direction.Length();
                    
                    // Use the larger of the two ships' avoidance ranges for minimum safe distance
                    float otherAvoidanceRadius = otherShip.AvoidanceDetectionRange;
                    float minSafeDistance = Math.Max(shipAvoidanceRadius, otherAvoidanceRadius);
                    
                    // Check if ships are too close (within each other's avoidance radius)
                    if (distance < minSafeDistance && distance > 0.1f)
                    {
                        // Calculate how far ships need to be pushed apart
                        float overlap = minSafeDistance - distance;
                        
                        // Normalize direction
                        direction.Normalize();
                        
                        // Push ships apart (both ships move half the overlap distance)
                        float pushDistance = overlap * 0.5f;
                        friendlyShip.Position += direction * pushDistance;
                        otherShip.Position -= direction * pushDistance;
                        
                        // Stop ships if they're too close (prevent them from continuing into each other)
                        if (distance < minSafeDistance * 0.8f)
                        {
                            // Stop both ships to prevent further collision
                            friendlyShip.StopMoving();
                            otherShip.StopMoving();
                        }
                    }
                }
                
                // Also check collision with player ship (prevent friendly ship from entering player's avoidance radius)
                if (_playerShip != null)
                {
                    Vector2 direction = friendlyShip.Position - _playerShip.Position;
                    float distance = direction.Length();
                    
                    // Use the larger of the two ships' avoidance ranges for minimum safe distance
                    float playerAvoidanceRadiusForCollision = _playerShip.AvoidanceDetectionRange;
                    float minSafeDistance = Math.Max(shipAvoidanceRadius, playerAvoidanceRadiusForCollision);
                    
                    if (distance < minSafeDistance && distance > 0.1f)
                    {
                        float overlap = minSafeDistance - distance;
                        direction.Normalize();
                        
                        // Push friendly ship away from player (player doesn't move)
                        float pushDistance = overlap;
                        friendlyShip.Position += direction * pushDistance;
                        
                        // Stop friendly ship if too close
                        if (distance < minSafeDistance * 0.8f)
                        {
                            friendlyShip.StopMoving();
                        }
                    }
                }
                
                // Update last direction for smooth pathing based on velocity
                if (friendlyShip.IsActivelyMoving())
                {
                    // Use velocity direction if available, otherwise calculate from position change
                    Vector2 velDir = Vector2.Zero;
                    if (_friendlyShipLastPosition.ContainsKey(friendlyShip))
                    {
                        Vector2 posChange = friendlyShip.Position - _friendlyShipLastPosition[friendlyShip];
                        if (posChange.LengthSquared() > 1f)
                        {
                            posChange.Normalize();
                            velDir = posChange;
                        }
                    }
                    
                    // Only update if we have a valid direction
                    if (velDir.LengthSquared() > 0.1f)
                    {
                        _friendlyShipLastDirection[friendlyShip] = velDir;
                    }
                }
                
                // Track enemy path if enabled
                if (_enemyPathVisible)
                {
                    if (!_friendlyShipPaths.ContainsKey(friendlyShip))
                    {
                        _friendlyShipPaths[friendlyShip] = new System.Collections.Generic.List<Vector2>();
                    }
                    
                    var path = _friendlyShipPaths[friendlyShip];
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
                
                if (_friendlyShipLastPosition.ContainsKey(friendlyShip))
                {
                    Vector2 lastPos = _friendlyShipLastPosition[friendlyShip];
                    float distanceMoved = Vector2.Distance(clampedPosition, lastPos);
                    
                    // If ship hasn't moved much (less than 5 pixels) in the last frame, it might be stuck
                    if (distanceMoved < 5f)
                    {
                        if (!_friendlyShipStuckTimer.ContainsKey(friendlyShip))
                        {
                            _friendlyShipStuckTimer[friendlyShip] = 0f;
                        }
                        _friendlyShipStuckTimer[friendlyShip] += deltaTime;
                        
                        // If stuck for more than 0.5 seconds, give it a new target
                        if (_friendlyShipStuckTimer[friendlyShip] > 0.5f)
                        {
                            isStuck = true;
                        }
                    }
                    else
                    {
                        // Ship is moving, reset stuck timer
                        if (_friendlyShipStuckTimer.ContainsKey(friendlyShip))
                        {
                            _friendlyShipStuckTimer.Remove(friendlyShip);
                        }
                    }
                }
                
                _friendlyShipLastPosition[friendlyShip] = clampedPosition;
                
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
                    if (_friendlyShipStuckTimer.ContainsKey(friendlyShip))
                    {
                        _friendlyShipStuckTimer.Remove(friendlyShip);
                    }
                }
                
                // Behavior system: Update and execute current behavior
                UpdateFriendlyShipBehavior(friendlyShip, deltaTime);
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
                
                // Ship-to-ship collision detection and resolution (same as friendly ships)
                float shipAvoidanceRadius = enemyShip.AvoidanceDetectionRange;
                
                // Collision with friendly ships
                foreach (var friendlyShip in _friendlyShips)
                {
                    Vector2 direction = enemyShip.Position - friendlyShip.Position;
                    float distance = direction.Length();
                    float otherAvoidanceRadius = friendlyShip.AvoidanceDetectionRange;
                    float minSafeDistance = Math.Max(shipAvoidanceRadius, otherAvoidanceRadius);
                    
                    if (distance < minSafeDistance && distance > 0.1f)
                    {
                        float overlap = minSafeDistance - distance;
                        direction.Normalize();
                        float pushDistance = overlap * 0.5f;
                        enemyShip.Position += direction * pushDistance;
                        friendlyShip.Position -= direction * pushDistance;
                        
                        if (distance < minSafeDistance * 0.8f)
                        {
                            enemyShip.StopMoving();
                            friendlyShip.StopMoving();
                        }
                    }
                }
                
                // Collision with player - push away more aggressively and don't stop moving
                if (_playerShip != null)
                {
                    Vector2 direction = enemyShip.Position - _playerShip.Position;
                    float distance = direction.Length();
                    float playerAvoidanceRadiusForCollision = _playerShip.AvoidanceDetectionRange;
                    float minSafeDistance = Math.Max(shipAvoidanceRadius, playerAvoidanceRadiusForCollision);
                    
                    if (distance < minSafeDistance && distance > 0.1f)
                    {
                        float overlap = minSafeDistance - distance;
                        direction.Normalize();
                        // Push away more aggressively (1.5x the overlap)
                        float pushDistance = overlap * 1.5f;
                        enemyShip.Position += direction * pushDistance;
                        
                        // Don't stop moving - instead, set a target away from player to back away
                        if (distance < minSafeDistance * 0.9f)
                        {
                            // Set target position away from player to actively back away
                            float backAwayDistance = minSafeDistance * 1.5f;
                            Vector2 backAwayTarget = enemyShip.Position + direction * backAwayDistance;
                            enemyShip.SetTargetPosition(backAwayTarget);
                        }
                    }
                }
                
                // Collision with other enemy ships
                foreach (var otherEnemyShip in _enemyShips)
                {
                    if (otherEnemyShip == enemyShip) continue;
                    
                    Vector2 direction = enemyShip.Position - otherEnemyShip.Position;
                    float distance = direction.Length();
                    float otherAvoidanceRadius = otherEnemyShip.AvoidanceDetectionRange;
                    float minSafeDistance = Math.Max(shipAvoidanceRadius, otherAvoidanceRadius);
                    
                    if (distance < minSafeDistance && distance > 0.1f)
                    {
                        float overlap = minSafeDistance - distance;
                        direction.Normalize();
                        float pushDistance = overlap * 0.5f;
                        enemyShip.Position += direction * pushDistance;
                        otherEnemyShip.Position -= direction * pushDistance;
                        
                        if (distance < minSafeDistance * 0.8f)
                        {
                            enemyShip.StopMoving();
                            otherEnemyShip.StopMoving();
                        }
                    }
                }
                
                // Behavior system: Update and execute aggressive behavior
                UpdateEnemyShipBehavior(enemyShip, deltaTime);
                
                // Clamp position AFTER behavior system (in case behavior teleported ship off-screen)
                const float shipMargin = 30f; // Keep ships at least 30 pixels from edges
                float clampedX = MathHelper.Clamp(enemyShip.Position.X, shipMargin, MapSize - shipMargin);
                float clampedY = MathHelper.Clamp(enemyShip.Position.Y, shipMargin, MapSize - shipMargin);
                enemyShip.Position = new Vector2(clampedX, clampedY);
            }
            
            // Update lasers
            foreach (var laser in _lasers)
            {
                laser.Update(gameTime);
            }
            
            // Update active explosions (continue even after ships are destroyed)
            for (int i = _activeExplosions.Count - 1; i >= 0; i--)
            {
                _activeExplosions[i].Update(deltaTime);
                if (!_activeExplosions[i].IsActive)
                {
                    _activeExplosions.RemoveAt(i); // Remove when explosion is done
                }
            }
            
            // Check for laser-ship collisions and apply damage
            foreach (var laser in _lasers.ToList()) // Use ToList to avoid modification during iteration
            {
                if (!laser.IsActive) continue;
                
                // Check collision with player ship
                if (_playerShip != null && laser.Owner != _playerShip && _playerShip.IsActive)
                {
                    float distance = Vector2.Distance(laser.Position, _playerShip.Position);
                    if (distance < 64f) // Ship collision radius (half of 128px ship size)
                    {
                        _playerShip.Health -= laser.Damage;
                        laser.IsActive = false; // Remove laser on hit
                        
                        if (_playerShip.Health <= 0f)
                        {
                            _playerShip.Health = 0f;
                            // Trigger explosion effect before deactivating
                            var explosionEffect = _playerShip.GetExplosionEffect();
                            if (explosionEffect != null)
                            {
                                explosionEffect.Explode(_playerShip.Position);
                                _activeExplosions.Add(explosionEffect); // Track explosion to continue updating/drawing
                                
                                // Play explosion sound
                                if (_explosionSound != null)
                                {
                                    System.Console.WriteLine($"[EXPLOSION SOUND] Playing explosion sound. SFX Enabled: {_sfxEnabled}, Volume: {_sfxVolume}");
                                    var explosionSoundInstance = _explosionSound.CreateInstance();
                                    explosionSoundInstance.Volume = _sfxEnabled ? _sfxVolume : 0f;
                                    explosionSoundInstance.Play();
                                    System.Console.WriteLine($"[EXPLOSION SOUND] Sound instance created. State: {explosionSoundInstance.State}, Volume: {explosionSoundInstance.Volume}");
                                }
                                else
                                {
                                    System.Console.WriteLine("[EXPLOSION SOUND] Warning: _explosionSound is null!");
                                }
                            }
                            _playerShip.IsActive = false;
                            System.Console.WriteLine("[PLAYER] Player ship destroyed!");
                        }
                        else
                        {
                            System.Console.WriteLine($"[PLAYER] Health: {_playerShip.Health:F1}/{_playerShip.MaxHealth:F1}");
                        }
                    }
                }
                
                // Check collision with friendly ships
                foreach (var friendlyShip in _friendlyShips.ToList())
                {
                    if (laser.Owner == friendlyShip || !friendlyShip.IsActive) continue;
                    
                    float distance = Vector2.Distance(laser.Position, friendlyShip.Position);
                    if (distance < 64f) // Ship collision radius
                    {
                        friendlyShip.Health -= laser.Damage;
                        laser.IsActive = false; // Remove laser on hit
                        
                        // Only switch to Flee behavior when hit by player's laser (not enemy lasers)
                        // Flee like enemies do - same behavior, same duration, but triggered on single hit from player
                        if (friendlyShip.Health > 0f && laser.Owner == _playerShip)
                        {
                            // Ensure behavior dictionary has this ship
                            if (!_friendlyShipBehaviors.ContainsKey(friendlyShip))
                            {
                                _friendlyShipBehaviors[friendlyShip] = FriendlyShipBehavior.Flee;
                            }
                            
                            // Always switch to Flee and reset timer, regardless of current behavior
                            // Use same flee duration as enemies (10 seconds)
                            _friendlyShipBehaviors[friendlyShip] = FriendlyShipBehavior.Flee;
                            _friendlyShipBehaviorTimer[friendlyShip] = 10.0f; // Flee for 10 seconds (same as enemies)
                            friendlyShip.IsIdle = false; // Ensure ship starts moving
                            friendlyShip.IsFleeing = true; // Mark ship as fleeing to activate damage effect
                            System.Console.WriteLine($"[FRIENDLY] Hit by player! Health: {friendlyShip.Health:F1}/{friendlyShip.MaxHealth:F1} - Switching to Flee behavior");
                        }
                        
                        if (friendlyShip.Health <= 0f)
                        {
                            friendlyShip.Health = 0f;
                            // Trigger explosion effect before removing
                            var explosionEffect = friendlyShip.GetExplosionEffect();
                            if (explosionEffect != null)
                            {
                                explosionEffect.Explode(friendlyShip.Position);
                                _activeExplosions.Add(explosionEffect); // Track explosion to continue updating/drawing
                                
                                // Play explosion sound
                                if (_explosionSound != null)
                                {
                                    System.Console.WriteLine($"[EXPLOSION SOUND] Playing explosion sound (friendly). SFX Enabled: {_sfxEnabled}, Volume: {_sfxVolume}");
                                    var explosionSoundInstance = _explosionSound.CreateInstance();
                                    explosionSoundInstance.Volume = _sfxEnabled ? _sfxVolume : 0f;
                                    explosionSoundInstance.Play();
                                    System.Console.WriteLine($"[EXPLOSION SOUND] Sound instance created. State: {explosionSoundInstance.State}, Volume: {explosionSoundInstance.Volume}");
                                }
                            }
                            friendlyShip.IsActive = false;
                            _friendlyShips.Remove(friendlyShip);
                            if (_friendlyShipBehaviors.ContainsKey(friendlyShip))
                                _friendlyShipBehaviors.Remove(friendlyShip);
                            if (_friendlyShipBehaviorTimer.ContainsKey(friendlyShip))
                                _friendlyShipBehaviorTimer.Remove(friendlyShip);
                            System.Console.WriteLine("[FRIENDLY] Friendly ship destroyed!");
                        }
                    }
                }
                
                // Check collision with enemy ships
                foreach (var enemyShip in _enemyShips.ToList())
                {
                    if (laser.Owner == enemyShip || !enemyShip.IsActive) continue;
                    
                    float distance = Vector2.Distance(laser.Position, enemyShip.Position);
                    if (distance < 64f) // Ship collision radius
                    {
                        enemyShip.Health -= laser.Damage;
                        laser.IsActive = false; // Remove laser on hit
                        
                        // Switch to Flee behavior when health <= 10
                        if (enemyShip.Health <= 10f && enemyShip.Health > 0f)
                        {
                            if (_enemyShipBehaviors.ContainsKey(enemyShip))
                            {
                                _enemyShipBehaviors[enemyShip] = FriendlyShipBehavior.Flee;
                                // Set a timer for flee behavior (flee for 10 seconds or until health recovers)
                                _enemyShipBehaviorTimer[enemyShip] = 10.0f;
                                enemyShip.IsFleeing = true; // Mark ship as fleeing to activate damage effect
                                System.Console.WriteLine($"[ENEMY] Low health! Health: {enemyShip.Health:F1}/{enemyShip.MaxHealth:F1} - Switching to Flee behavior");
                            }
                        }
                        
                        if (enemyShip.Health <= 0f)
                        {
                            enemyShip.Health = 0f;
                            // Trigger explosion effect before removing
                            var explosionEffect = enemyShip.GetExplosionEffect();
                            if (explosionEffect != null)
                            {
                                explosionEffect.Explode(enemyShip.Position);
                                _activeExplosions.Add(explosionEffect); // Track explosion to continue updating/drawing
                                
                                // Play explosion sound
                                if (_explosionSound != null)
                                {
                                    System.Console.WriteLine($"[EXPLOSION SOUND] Playing explosion sound (enemy). SFX Enabled: {_sfxEnabled}, Volume: {_sfxVolume}");
                                    var explosionSoundInstance = _explosionSound.CreateInstance();
                                    explosionSoundInstance.Volume = _sfxEnabled ? _sfxVolume : 0f;
                                    explosionSoundInstance.Play();
                                    System.Console.WriteLine($"[EXPLOSION SOUND] Sound instance created. State: {explosionSoundInstance.State}, Volume: {explosionSoundInstance.Volume}");
                                }
                            }
                            enemyShip.IsActive = false;
                            _enemyShips.Remove(enemyShip);
                            if (_enemyShipBehaviors.ContainsKey(enemyShip))
                                _enemyShipBehaviors.Remove(enemyShip);
                            if (_enemyShipBehaviorTimer.ContainsKey(enemyShip))
                                _enemyShipBehaviorTimer.Remove(enemyShip);
                            if (_enemyShipAttackCooldown.ContainsKey(enemyShip))
                                _enemyShipAttackCooldown.Remove(enemyShip);
                            System.Console.WriteLine("[ENEMY] Enemy ship destroyed!");
                        }
                        else
                        {
                            System.Console.WriteLine($"[ENEMY] Health: {enemyShip.Health:F1}/{enemyShip.MaxHealth:F1}");
                        }
                    }
                }
            }
            
            // Remove lasers that are off screen, too far away, or inactive
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

        // ========== Behavior System Methods ==========
        
        // Helper method to check if a position is too close to player
        private bool IsTooCloseToPlayer(Vector2 position, FriendlyShip friendlyShip)
        {
            if (_playerShip == null) return false;
            
            float distToPlayer = Vector2.Distance(position, _playerShip.Position);
            float minSafeDistance = _playerShip.AvoidanceDetectionRange * 1.5f; // 1.5x player's avoidance range
            
            return distToPlayer < minSafeDistance;
        }
        
        // Helper method to adjust position away from player if too close
        private Vector2 AvoidPlayerPosition(Vector2 position, FriendlyShip friendlyShip)
        {
            // Clamp position to map bounds (keep ships within galaxy map)
            const float mapBoundaryMargin = 200f;
            
            if (_playerShip == null) 
            {
                // Clamp position to map bounds
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
                // Push position away from player
                toPosition.Normalize();
                Vector2 adjustedPosition = _playerShip.Position + toPosition * minSafeDistance;
                
                // Clamp adjusted position to map bounds
                adjustedPosition = new Vector2(
                    MathHelper.Clamp(adjustedPosition.X, mapBoundaryMargin, MapSize - mapBoundaryMargin),
                    MathHelper.Clamp(adjustedPosition.Y, mapBoundaryMargin, MapSize - mapBoundaryMargin)
                );
                
                return adjustedPosition;
            }
            
            // Clamp original position to map bounds
            return new Vector2(
                MathHelper.Clamp(position.X, mapBoundaryMargin, MapSize - mapBoundaryMargin),
                MathHelper.Clamp(position.Y, mapBoundaryMargin, MapSize - mapBoundaryMargin)
            );
        }
        
        // Helper method to check if a position is too close to any other friendly ship
        private bool IsTooCloseToOtherShips(Vector2 position, FriendlyShip friendlyShip)
        {
            foreach (var otherShip in _friendlyShips)
            {
                if (otherShip == friendlyShip) continue;
                
                float distToOtherShip = Vector2.Distance(position, otherShip.Position);
                // Use the larger of the two ships' avoidance ranges for minimum safe distance (no multiplier - must stay outside radius)
                float minSafeDistance = Math.Max(friendlyShip.AvoidanceDetectionRange, otherShip.AvoidanceDetectionRange);
                
                if (distToOtherShip < minSafeDistance)
                {
                    return true;
                }
            }
            
            return false;
        }
        
        // Helper method to adjust position away from other friendly ships if too close
        private Vector2 AvoidOtherShipsPosition(Vector2 position, FriendlyShip friendlyShip)
        {
            const float mapBoundaryMargin = 200f;
            Vector2 adjustedPosition = position;
            
            // Check each other ship and adjust position if needed
            foreach (var otherShip in _friendlyShips)
            {
                if (otherShip == friendlyShip) continue;
                
                Vector2 toPosition = adjustedPosition - otherShip.Position;
                float distToOtherShip = toPosition.Length();
                // Use the larger of the two ships' avoidance ranges for minimum safe distance (no multiplier - must stay outside radius)
                float minSafeDistance = Math.Max(friendlyShip.AvoidanceDetectionRange, otherShip.AvoidanceDetectionRange);
                
                if (distToOtherShip < minSafeDistance && distToOtherShip > 0.1f)
                {
                    // Push position away from other ship
                    toPosition.Normalize();
                    adjustedPosition = otherShip.Position + toPosition * minSafeDistance;
                }
            }
            
            // Clamp to map bounds
            adjustedPosition = new Vector2(
                MathHelper.Clamp(adjustedPosition.X, mapBoundaryMargin, MapSize - mapBoundaryMargin),
                MathHelper.Clamp(adjustedPosition.Y, mapBoundaryMargin, MapSize - mapBoundaryMargin)
            );
            
            return adjustedPosition;
        }
        
        private FriendlyShipBehavior GetRandomBehavior()
        {
            // Weight behaviors: 30% Idle, 25% Patrol, 20% LongDistance, 25% Wander
            // Note: Aggressive behavior is not included here - it's only for enemy ships
            double roll = _random.NextDouble();
            if (roll < _shipIdleRate)
                return FriendlyShipBehavior.Idle;
            else if (roll < _shipIdleRate + 0.25f)
                return FriendlyShipBehavior.Patrol;
            else if (roll < _shipIdleRate + 0.45f)
                return FriendlyShipBehavior.LongDistance;
            else
                return FriendlyShipBehavior.Wander;
        }
        
        private FriendlyShipBehavior GetEnemyBehavior()
        {
            // Enemy ships always use Aggressive behavior
            return FriendlyShipBehavior.Aggressive;
        }
        
        private float GetBehaviorDuration(FriendlyShipBehavior behavior)
        {
            switch (behavior)
            {
                case FriendlyShipBehavior.Idle:
                    return (float)(_random.NextDouble() * (IdleMaxDuration - IdleMinDuration) + IdleMinDuration);
                case FriendlyShipBehavior.Patrol:
                    return (float)(_random.NextDouble() * (PatrolMaxDuration - PatrolMinDuration) + PatrolMinDuration);
                case FriendlyShipBehavior.LongDistance:
                    return (float)(_random.NextDouble() * (LongDistanceMaxDuration - LongDistanceMinDuration) + LongDistanceMinDuration);
                case FriendlyShipBehavior.Wander:
                    return (float)(_random.NextDouble() * (WanderMaxDuration - WanderMinDuration) + WanderMinDuration);
                default:
                    return 5f;
            }
        }
        
        private void UpdateFriendlyShipBehavior(FriendlyShip friendlyShip, float deltaTime)
        {
            // Initialize behavior if not set
            if (!_friendlyShipBehaviors.ContainsKey(friendlyShip))
            {
                _friendlyShipBehaviors[friendlyShip] = GetRandomBehavior();
                _friendlyShipBehaviorTimer[friendlyShip] = GetBehaviorDuration(_friendlyShipBehaviors[friendlyShip]);
            }
            
            // Check if currently fleeing - if fully healed, resume normal behavior immediately
            FriendlyShipBehavior friendlyCurrentBehavior = _friendlyShipBehaviors[friendlyShip];
            if (friendlyCurrentBehavior == FriendlyShipBehavior.Flee && friendlyShip.Health >= friendlyShip.MaxHealth)
            {
                // Fully healed, switch back to random behavior immediately
                FriendlyShipBehavior newBehavior = GetRandomBehavior();
                // Don't randomly select Flee
                while (newBehavior == FriendlyShipBehavior.Flee)
                {
                    newBehavior = GetRandomBehavior();
                }
                _friendlyShipBehaviors[friendlyShip] = newBehavior;
                _friendlyShipBehaviorTimer[friendlyShip] = GetBehaviorDuration(newBehavior);
                friendlyShip.IsFleeing = false; // No longer fleeing, stop damage effect
                
                // Face the direction the ship is moving when resuming behavior
                if (friendlyShip.Velocity.LengthSquared() > 1f)
                {
                    float targetRotation = (float)System.Math.Atan2(friendlyShip.Velocity.Y, friendlyShip.Velocity.X) + MathHelper.PiOver2;
                    friendlyShip.Rotation = targetRotation;
                }
                
                if (newBehavior == FriendlyShipBehavior.Idle)
                {
                    friendlyShip.StopMoving();
                    friendlyShip.IsIdle = true;
                }
                else
                {
                    friendlyShip.IsIdle = false;
                }
                
                System.Console.WriteLine($"[FRIENDLY] Fully healed! Health: {friendlyShip.Health:F1}/{friendlyShip.MaxHealth:F1} - Resuming normal behavior: {newBehavior}");
            }
            // Decrement behavior timer (only if not fleeing or not fully healed)
            else if (_friendlyShipBehaviorTimer.ContainsKey(friendlyShip))
            {
                // Only decrement timer if not fleeing (flee timer is managed separately)
                if (friendlyCurrentBehavior != FriendlyShipBehavior.Flee)
                {
                    _friendlyShipBehaviorTimer[friendlyShip] -= deltaTime;
                }
                else
                {
                    // Still fleeing and not fully healed, continue fleeing - reset timer if needed
                    if (_friendlyShipBehaviorTimer[friendlyShip] <= 0f)
                    {
                        _friendlyShipBehaviorTimer[friendlyShip] = 10.0f; // Continue fleeing
                    }
                    else
                    {
                        _friendlyShipBehaviorTimer[friendlyShip] -= deltaTime;
                    }
                }
                
                // Check if behavior should transition (timer-based transitions)
                if (_friendlyShipBehaviorTimer[friendlyShip] <= 0f)
                {
                    // If currently fleeing (but not fully healed yet), continue fleeing
                    if (friendlyCurrentBehavior == FriendlyShipBehavior.Flee)
                    {
                        // Still damaged, continue fleeing - reset timer
                        _friendlyShipBehaviorTimer[friendlyShip] = 10.0f; // Continue fleeing
                    }
                    else
                    {
                        // Transition to new behavior (but not Flee - that's only triggered by damage)
                        FriendlyShipBehavior newBehavior = GetRandomBehavior();
                        while (newBehavior == FriendlyShipBehavior.Flee)
                        {
                            newBehavior = GetRandomBehavior();
                        }
                        _friendlyShipBehaviors[friendlyShip] = newBehavior;
                        _friendlyShipBehaviorTimer[friendlyShip] = GetBehaviorDuration(newBehavior);
                        
                        if (newBehavior == FriendlyShipBehavior.Idle)
                        {
                            friendlyShip.StopMoving();
                            friendlyShip.IsIdle = true;
                        }
                        else
                        {
                            friendlyShip.IsIdle = false;
                        }
                    }
                }
            }
            
            // Execute current behavior
            FriendlyShipBehavior currentBehavior = _friendlyShipBehaviors[friendlyShip];
            
            // Only execute behavior if ship is not actively moving (reached target) or if behavior requires immediate action
            if (!friendlyShip.IsActivelyMoving() || currentBehavior == FriendlyShipBehavior.LongDistance || currentBehavior == FriendlyShipBehavior.Idle || currentBehavior == FriendlyShipBehavior.Flee)
            {
                switch (currentBehavior)
                {
                    case FriendlyShipBehavior.Idle:
                        ExecuteIdleBehavior(friendlyShip);
                        break;
                    case FriendlyShipBehavior.Patrol:
                        ExecutePatrolBehavior(friendlyShip);
                        break;
                    case FriendlyShipBehavior.LongDistance:
                        ExecuteLongDistanceBehavior(friendlyShip);
                        break;
                    case FriendlyShipBehavior.Wander:
                        ExecuteWanderBehavior(friendlyShip);
                        break;
                    case FriendlyShipBehavior.Aggressive:
                        // Aggressive behavior should not be used for friendly ships
                        // This is handled separately for enemy ships via UpdateEnemyShipBehavior
                        break;
                    case FriendlyShipBehavior.Flee:
                        friendlyShip.IsFleeing = true; // Ensure flee flag is set while executing flee behavior
                        ExecuteFleeBehavior(friendlyShip);
                        break;
                }
            }
        }
        
        private void UpdateEnemyShipBehavior(EnemyShip enemyShip, float deltaTime)
        {
            // Initialize behavior if not set
            if (!_enemyShipBehaviors.ContainsKey(enemyShip))
            {
                _enemyShipBehaviors[enemyShip] = GetRandomBehavior();
                _enemyShipBehaviorTimer[enemyShip] = GetBehaviorDuration(_enemyShipBehaviors[enemyShip]);
            }
            
            // Initialize behavior timer if not set
            if (!_enemyShipBehaviorTimer.ContainsKey(enemyShip))
            {
                _enemyShipBehaviorTimer[enemyShip] = GetBehaviorDuration(_enemyShipBehaviors[enemyShip]);
            }
            
            // Initialize attack cooldown if not set
            if (!_enemyShipAttackCooldown.ContainsKey(enemyShip))
            {
                _enemyShipAttackCooldown[enemyShip] = 0f;
            }
            
            // Check if player is within detection range
            bool playerDetected = false;
            if (_playerShip != null)
            {
                Vector2 toPlayer = _playerShip.Position - enemyShip.Position;
                float distanceToPlayer = toPlayer.Length();
                playerDetected = distanceToPlayer < EnemyPlayerDetectionRange;
            }
            
            // If player is detected and not fleeing, switch to Aggressive behavior
            if (playerDetected)
            {
                // Don't override Flee behavior - let it continue
                if (_enemyShipBehaviors[enemyShip] != FriendlyShipBehavior.Aggressive && _enemyShipBehaviors[enemyShip] != FriendlyShipBehavior.Flee)
                {
                    _enemyShipBehaviors[enemyShip] = FriendlyShipBehavior.Aggressive;
                    _enemyShipBehaviorTimer[enemyShip] = float.MaxValue; // Aggressive is permanent until player leaves range
                }
            }
            else
            {
                // Player not detected - use normal behaviors with timers
                // If currently aggressive (and not fleeing), switch back to random behavior
                if (_enemyShipBehaviors[enemyShip] == FriendlyShipBehavior.Aggressive)
                {
                    FriendlyShipBehavior newBehavior = GetRandomBehavior();
                    while (newBehavior == FriendlyShipBehavior.Flee)
                    {
                        newBehavior = GetRandomBehavior();
                    }
                    _enemyShipBehaviors[enemyShip] = newBehavior;
                    _enemyShipBehaviorTimer[enemyShip] = GetBehaviorDuration(newBehavior);
                }
                
                // Decrement behavior timer for non-aggressive behaviors
                if (_enemyShipBehaviorTimer.ContainsKey(enemyShip) && _enemyShipBehaviors[enemyShip] != FriendlyShipBehavior.Aggressive)
                {
                    FriendlyShipBehavior enemyCurrentBehavior = _enemyShipBehaviors[enemyShip];
                    
                    // Only decrement timer if not fleeing (flee timer is managed separately)
                    if (enemyCurrentBehavior != FriendlyShipBehavior.Flee)
                    {
                        _enemyShipBehaviorTimer[enemyShip] -= deltaTime;
                    }
                    else
                    {
                        // For flee behavior, check if fully healed - if so, resume normal behavior immediately
                        if (enemyShip.Health >= enemyShip.MaxHealth)
                        {
                            // Fully healed, switch back to random behavior immediately
                            FriendlyShipBehavior newBehavior = GetRandomBehavior();
                            while (newBehavior == FriendlyShipBehavior.Flee)
                            {
                                newBehavior = GetRandomBehavior();
                            }
                            _enemyShipBehaviors[enemyShip] = newBehavior;
                            _enemyShipBehaviorTimer[enemyShip] = GetBehaviorDuration(newBehavior);
                            enemyShip.IsFleeing = false; // No longer fleeing, stop damage effect
                            
                            // Face the direction the ship is moving when resuming behavior
                            if (enemyShip.Velocity.LengthSquared() > 1f)
                            {
                                float targetRotation = (float)System.Math.Atan2(enemyShip.Velocity.Y, enemyShip.Velocity.X) + MathHelper.PiOver2;
                                enemyShip.Rotation = targetRotation;
                            }
                            
                            if (newBehavior == FriendlyShipBehavior.Idle)
                            {
                                enemyShip.StopMoving();
                                enemyShip.IsIdle = true;
                            }
                            else
                            {
                                enemyShip.IsIdle = false;
                            }
                            
                            System.Console.WriteLine($"[ENEMY] Fully healed! Health: {enemyShip.Health:F1}/{enemyShip.MaxHealth:F1} - Resuming normal behavior: {newBehavior}");
                        }
                        else
                        {
                            // Still damaged, continue fleeing and update timer
                            _enemyShipBehaviorTimer[enemyShip] -= deltaTime;
                        }
                    }
                    
                    // Check if behavior should transition
                    if (_enemyShipBehaviorTimer[enemyShip] <= 0f)
                    {
                        // If currently fleeing, check if fully healed - if so, resume normal behavior immediately
                        if (enemyCurrentBehavior == FriendlyShipBehavior.Flee)
                        {
                            // If fully healed, switch back to random behavior immediately
                            if (enemyShip.Health >= enemyShip.MaxHealth)
                            {
                                FriendlyShipBehavior newBehavior = GetRandomBehavior();
                                while (newBehavior == FriendlyShipBehavior.Flee)
                                {
                                    newBehavior = GetRandomBehavior();
                                }
                                _enemyShipBehaviors[enemyShip] = newBehavior;
                                _enemyShipBehaviorTimer[enemyShip] = GetBehaviorDuration(newBehavior);
                                enemyShip.IsFleeing = false; // No longer fleeing, stop damage effect
                                
                                // Face the direction the ship is moving when resuming behavior
                                if (enemyShip.Velocity.LengthSquared() > 1f)
                                {
                                    float targetRotation = (float)System.Math.Atan2(enemyShip.Velocity.Y, enemyShip.Velocity.X) + MathHelper.PiOver2;
                                    enemyShip.Rotation = targetRotation;
                                }
                                
                                if (newBehavior == FriendlyShipBehavior.Idle)
                                {
                                    enemyShip.StopMoving();
                                    enemyShip.IsIdle = true;
                                }
                                else
                                {
                                    enemyShip.IsIdle = false;
                                }
                                
                                System.Console.WriteLine($"[ENEMY] Fully healed! Health: {enemyShip.Health:F1}/{enemyShip.MaxHealth:F1} - Resuming normal behavior: {newBehavior}");
                            }
                            else
                            {
                                // Still damaged, continue fleeing - reset timer
                                _enemyShipBehaviorTimer[enemyShip] = 10.0f; // Continue fleeing
                                enemyShip.IsFleeing = true; // Keep damage effect active
                            }
                        }
                        else
                        {
                            // Transition to new behavior (but not Flee - that's only triggered by low health)
                            FriendlyShipBehavior newBehavior = GetRandomBehavior();
                            while (newBehavior == FriendlyShipBehavior.Flee)
                            {
                                newBehavior = GetRandomBehavior();
                            }
                            _enemyShipBehaviors[enemyShip] = newBehavior;
                            _enemyShipBehaviorTimer[enemyShip] = GetBehaviorDuration(newBehavior);
                            
                            if (newBehavior == FriendlyShipBehavior.Idle)
                            {
                                enemyShip.StopMoving();
                                enemyShip.IsIdle = true;
                            }
                            else
                            {
                                enemyShip.IsIdle = false;
                            }
                        }
                    }
                }
            }
            
            // Update attack cooldown
            if (_enemyShipAttackCooldown[enemyShip] > 0f)
            {
                _enemyShipAttackCooldown[enemyShip] -= deltaTime;
            }
            
            // Execute current behavior
            FriendlyShipBehavior currentBehavior = _enemyShipBehaviors[enemyShip];
            
            if (currentBehavior == FriendlyShipBehavior.Aggressive)
            {
                // Execute aggressive behavior
                ExecuteAggressiveBehavior(enemyShip, deltaTime);
            }
            else
            {
                // Execute normal behaviors (same as friendly ships)
                // Only execute behavior if ship is not actively moving (reached target) or if behavior requires immediate action
                if (!enemyShip.IsActivelyMoving() || currentBehavior == FriendlyShipBehavior.LongDistance || currentBehavior == FriendlyShipBehavior.Idle || currentBehavior == FriendlyShipBehavior.Flee)
                {
                    switch (currentBehavior)
                    {
                        case FriendlyShipBehavior.Idle:
                            ExecuteIdleBehavior((FriendlyShip)enemyShip);
                            break;
                        case FriendlyShipBehavior.Patrol:
                            ExecutePatrolBehavior((FriendlyShip)enemyShip);
                            break;
                        case FriendlyShipBehavior.LongDistance:
                            ExecuteLongDistanceBehavior((FriendlyShip)enemyShip);
                            break;
                        case FriendlyShipBehavior.Wander:
                            ExecuteWanderBehavior((FriendlyShip)enemyShip);
                            break;
                        case FriendlyShipBehavior.Flee:
                            enemyShip.IsFleeing = true; // Ensure flee flag is set while executing flee behavior
                            ExecuteFleeBehavior((FriendlyShip)enemyShip);
                            break;
                    }
                }
            }
        }
        
        private void ExecuteAggressiveBehavior(EnemyShip enemyShip, float deltaTime)
        {
            if (_playerShip == null) return;
            
            // Calculate distance to player
            Vector2 toPlayer = _playerShip.Position - enemyShip.Position;
            float distanceToPlayer = toPlayer.Length();
            
            // Attack range: if within range, attack the player
            const float attackRange = 800f; // Range at which enemy will attack
            const float attackCooldownTime = 1.5f; // Time between attacks (seconds)
            const float pursuitRange = 2000f; // Range at which enemy will pursue player
            
            if (distanceToPlayer < pursuitRange && distanceToPlayer > 0.1f)
            {
                toPlayer.Normalize();
                Vector2 targetPosition = _playerShip.Position;
                
                // Calculate safe distances
                float playerAvoidanceRadius = _playerShip.AvoidanceDetectionRange;
                float enemyAvoidanceRadius = enemyShip.AvoidanceDetectionRange;
                float effectiveAvoidanceRadius = Math.Max(playerAvoidanceRadius, enemyAvoidanceRadius);
                const float preferredAttackDistance = 600f; // Preferred distance for attacking (outside avoidance radius but close enough to attack)
                float tooCloseDistance = effectiveAvoidanceRadius * 1.5f; // If closer than this, fly away
                float minAttackDistance = effectiveAvoidanceRadius * 1.2f; // Minimum safe distance
                
                if (distanceToPlayer < tooCloseDistance)
                {
                    // Too close - fly away from player
                    float backAwayDistance = (tooCloseDistance - distanceToPlayer) * 2f; // Back away 2x the overlap
                    targetPosition = enemyShip.Position - toPlayer * backAwayDistance;
                    
                    // Ensure we back away to at least the preferred attack distance
                    Vector2 awayDirection = -toPlayer;
                    float distanceToPreferred = preferredAttackDistance - distanceToPlayer;
                    if (distanceToPreferred > 0)
                    {
                        targetPosition = enemyShip.Position + awayDirection * distanceToPreferred;
                    }
                }
                else if (distanceToPlayer > preferredAttackDistance)
                {
                    // Too far - move closer to preferred attack distance
                    Vector2 directionToPlayer = toPlayer;
                    float approachDistance = distanceToPlayer - preferredAttackDistance;
                    targetPosition = enemyShip.Position + directionToPlayer * Math.Min(approachDistance, 500f); // Approach gradually
                }
                else
                {
                    // At good distance - maintain position relative to player (orbit or strafe)
                    // Try to maintain preferred attack distance while staying mobile
                    Vector2 perpendicular = new Vector2(-toPlayer.Y, toPlayer.X); // Perpendicular direction
                    perpendicular.Normalize();
                    // Add some perpendicular movement to make enemies strafe around player
                    targetPosition = _playerShip.Position - toPlayer * preferredAttackDistance + perpendicular * 200f;
                }
                
                enemyShip.SetTargetPosition(targetPosition);
                enemyShip.IsIdle = false; // Ensure ship is not idle
                
                // Always face the player when in aggressive mode
                enemyShip.SetAimTarget(_playerShip.Position);
                
                // Attack if within range and cooldown is ready
                if (distanceToPlayer < attackRange && _enemyShipAttackCooldown[enemyShip] <= 0f)
                {
                    // Fire laser at player
                    FireEnemyLaser(enemyShip);
                    _enemyShipAttackCooldown[enemyShip] = attackCooldownTime;
                }
            }
            else
            {
                // Player out of range - clear aim target so ship can rotate normally
                enemyShip.SetAimTarget(null);
            }
        }
        
        private void FireEnemyLaser(EnemyShip enemyShip)
        {
            if (enemyShip == null) return;
            
            var shipTexture = enemyShip.GetTexture();
            if (shipTexture == null) return;
            
            float textureCenterX = shipTexture.Width / 2f;
            float textureCenterY = shipTexture.Height / 2f;
            float shipRotation = enemyShip.Rotation;
            float cos = (float)Math.Cos(shipRotation);
            float sin = (float)Math.Sin(shipRotation);
            
            // Fire laser from center of ship (front)
            float spriteX = textureCenterX;
            float spriteY = 20f; // Near the front of the ship
            
            // Convert sprite coordinates to offset from ship center
            float offsetX = spriteX - textureCenterX;
            float offsetY = spriteY - textureCenterY;
            
            // Rotate the offset by ship's rotation to get world-space offset
            float rotatedX = offsetX * cos - offsetY * sin;
            float rotatedY = offsetX * sin + offsetY * cos;
            
            // Calculate laser spawn position
            Vector2 laserSpawnPosition = enemyShip.Position + new Vector2(rotatedX, rotatedY);
            
            var laser = new Laser(laserSpawnPosition, shipRotation, GraphicsDevice, enemyShip.Damage, enemyShip);
            _lasers.Add(laser);
            
            // Play laser fire sound effect with SFX volume
            _laserFireSound?.Play(_sfxVolume, 0f, 0f);
        }
        
        private void ExecuteIdleBehavior(FriendlyShip friendlyShip)
        {
            // Idle: Ship stops and uses drift
            // Stop the ship by setting target to current position
            friendlyShip.StopMoving();
            // Ship will now use drift if Drift > 0 (handled in FriendlyShip.Update)
        }
        
        private void ExecutePatrolBehavior(FriendlyShip friendlyShip)
        {
            // Patrol: Ship moves between waypoints in a small area
            if (!_friendlyShipPatrolPoints.ContainsKey(friendlyShip) || _friendlyShipPatrolPoints[friendlyShip].Count == 0)
            {
                // Initialize patrol points around current position
                InitializePatrolPoints(friendlyShip);
            }
            
            var patrolPoints = _friendlyShipPatrolPoints[friendlyShip];
            
            // If ship reached current target, move to next patrol point
            if (!friendlyShip.IsActivelyMoving())
            {
                // Find next patrol point (cycle through them)
                Vector2 currentPos = friendlyShip.Position;
                int closestIndex = 0;
                float closestDist = float.MaxValue;
                
                for (int i = 0; i < patrolPoints.Count; i++)
                {
                    float dist = Vector2.Distance(currentPos, patrolPoints[i]);
                    if (dist < closestDist)
                    {
                        closestDist = dist;
                        closestIndex = i;
                    }
                }
                
                // Move to next point in sequence
                int nextIndex = (closestIndex + 1) % patrolPoints.Count;
                Vector2 nextTarget = patrolPoints[nextIndex];
                
                // Ensure target is far enough for smooth turning (minimum 500 pixels)
                float distanceToTarget = Vector2.Distance(currentPos, nextTarget);
                if (distanceToTarget < 500f)
                {
                    // Target too close - skip to next point or extend direction
                    int skipIndex = (nextIndex + 1) % patrolPoints.Count;
                    Vector2 skipTarget = patrolPoints[skipIndex];
                    float skipDistance = Vector2.Distance(currentPos, skipTarget);
                    
                    if (skipDistance >= 500f)
                    {
                        nextTarget = skipTarget;
                    }
                    else
                    {
                        // Extend direction to minimum distance
                        Vector2 direction = nextTarget - currentPos;
                        if (direction.LengthSquared() > 0.1f)
                        {
                            direction.Normalize();
                            nextTarget = currentPos + direction * 500f;
                        // Clamp to map bounds (keep ships within galaxy map)
                        const float margin = 200f;
                        nextTarget = new Vector2(
                            MathHelper.Clamp(nextTarget.X, margin, MapSize - margin),
                            MathHelper.Clamp(nextTarget.Y, margin, MapSize - margin)
                        );
                        }
                    }
                }
                
                // Avoid player's radius - adjust target if too close
                if (IsTooCloseToPlayer(nextTarget, friendlyShip))
                {
                    nextTarget = AvoidPlayerPosition(nextTarget, friendlyShip);
                    // Clamp to map bounds after adjustment
                    const float margin = 200f;
                    nextTarget = new Vector2(
                        MathHelper.Clamp(nextTarget.X, margin, MapSize - margin),
                        MathHelper.Clamp(nextTarget.Y, margin, MapSize - margin)
                    );
                }
                
                // Avoid other ships' radius - adjust target if too close
                if (IsTooCloseToOtherShips(nextTarget, friendlyShip))
                {
                    nextTarget = AvoidOtherShipsPosition(nextTarget, friendlyShip);
                    // Clamp to map bounds after adjustment
                    const float margin = 200f;
                    nextTarget = new Vector2(
                        MathHelper.Clamp(nextTarget.X, margin, MapSize - margin),
                        MathHelper.Clamp(nextTarget.Y, margin, MapSize - margin)
                    );
                }
                
                // Clear any stored original destination and A* path since we're setting a new behavior target
                if (_friendlyShipOriginalDestination.ContainsKey(friendlyShip))
                {
                    _friendlyShipOriginalDestination.Remove(friendlyShip);
                }
                if (_friendlyShipAStarPaths.ContainsKey(friendlyShip))
                {
                    _friendlyShipAStarPaths.Remove(friendlyShip);
                }
                if (_friendlyShipCurrentWaypointIndex.ContainsKey(friendlyShip))
                {
                    _friendlyShipCurrentWaypointIndex.Remove(friendlyShip);
                }
                friendlyShip.SetTargetPosition(nextTarget);
            }
        }
        
        private void InitializePatrolPoints(FriendlyShip friendlyShip)
        {
            // Create 3-5 patrol waypoints in a circular pattern around current position
            // Minimum distance: ship needs ~400-500 pixels to turn smoothly (180° turn at 300px/s speed and 3 rad/s rotation)
            Vector2 center = friendlyShip.Position;
            int numPoints = _random.Next(3, 6); // 3-5 points
            float patrolRadius = (float)(_random.NextDouble() * 600f + 600f); // 600-1200 pixel radius (ensures ships can turn smoothly)
            
            var points = new System.Collections.Generic.List<Vector2>();
            for (int i = 0; i < numPoints; i++)
            {
                float angle = (float)(i * MathHelper.TwoPi / numPoints + _random.NextDouble() * 0.5f); // Add some randomness
                Vector2 point = center + new Vector2(
                    (float)Math.Cos(angle) * patrolRadius,
                    (float)Math.Sin(angle) * patrolRadius
                );
                
                // Clamp to map bounds
                const float margin = 200f;
                point = new Vector2(
                    MathHelper.Clamp(point.X, margin, MapSize - margin),
                    MathHelper.Clamp(point.Y, margin, MapSize - margin)
                );
                
                // Ensure minimum distance from center (at least 500 pixels for smooth turning)
                float distanceFromCenter = Vector2.Distance(point, center);
                if (distanceFromCenter < 500f)
                {
                    // Extend point to minimum distance
                    Vector2 direction = point - center;
                    if (direction.LengthSquared() > 0.1f)
                    {
                        direction.Normalize();
                        point = center + direction * 500f;
                        // Re-clamp after extension
                        point = new Vector2(
                            MathHelper.Clamp(point.X, margin, MapSize - margin),
                            MathHelper.Clamp(point.Y, margin, MapSize - margin)
                        );
                    }
                }
                
                // Avoid player's radius when creating patrol points
                if (_playerShip != null)
                {
                    if (IsTooCloseToPlayer(point, friendlyShip))
                    {
                        point = AvoidPlayerPosition(point, friendlyShip);
                        // Re-clamp after adjustment
                        point = new Vector2(
                            MathHelper.Clamp(point.X, margin, MapSize - margin),
                            MathHelper.Clamp(point.Y, margin, MapSize - margin)
                        );
                    }
                }
                
                // Avoid other ships' radius when creating patrol points
                if (IsTooCloseToOtherShips(point, friendlyShip))
                {
                    point = AvoidOtherShipsPosition(point, friendlyShip);
                    // Re-clamp after adjustment
                    point = new Vector2(
                        MathHelper.Clamp(point.X, margin, MapSize - margin),
                        MathHelper.Clamp(point.Y, margin, MapSize - margin)
                    );
                }
                
                points.Add(point);
            }
            
            _friendlyShipPatrolPoints[friendlyShip] = points;
        }
        
        private void ExecuteLongDistanceBehavior(FriendlyShip friendlyShip)
        {
            // LongDistance: Ship flies one long path across most of the map
            if (!friendlyShip.IsActivelyMoving())
            {
                Vector2 currentPos = friendlyShip.Position;
                const float minDistance = MapSize * 0.75f; // At least 75% of the map (6144 pixels) - longer paths
                const float maxDistance = MapSize * 1.5f; // Up to 1.5x map size for very long edge-to-edge paths
                Vector2 targetPos;
                int attempts = 0;
                const int maxAttempts = 20;
                
                do
                {
                    // Pick a random direction
                    float randomAngle = (float)(_random.NextDouble() * MathHelper.TwoPi);
                    Vector2 direction = new Vector2(
                        (float)Math.Cos(randomAngle),
                        (float)Math.Sin(randomAngle)
                    );
                    
                    // Pick a random distance between min and max
                    float distance = (float)(_random.NextDouble() * (maxDistance - minDistance) + minDistance);
                    
                    // Calculate target position
                    targetPos = currentPos + direction * distance;
                    
                    // Check if target is within map bounds (keep ships within galaxy map)
                    const float margin = 200f;
                    bool isValid = targetPos.X >= margin && targetPos.X <= MapSize - margin &&
                                   targetPos.Y >= margin && targetPos.Y <= MapSize - margin;
                    
                    if (isValid)
                    {
                        // Verify the distance is at least half the map
                        float actualDistance = Vector2.Distance(currentPos, targetPos);
                        if (actualDistance >= minDistance)
                        {
                            // Check if target is too close to player or other ships
                            if (!IsTooCloseToPlayer(targetPos, friendlyShip) && !IsTooCloseToOtherShips(targetPos, friendlyShip))
                            {
                                break;
                            }
                        }
                    }
                    
                    attempts++;
                } while (attempts < maxAttempts);
                
                // If we couldn't find a valid target after max attempts, use a guaranteed long path
                if (attempts >= maxAttempts)
                {
                    // Pick a random direction and ensure it's at least half the map
                    float randomAngle = (float)(_random.NextDouble() * MathHelper.TwoPi);
                    Vector2 direction = new Vector2(
                        (float)Math.Cos(randomAngle),
                        (float)Math.Sin(randomAngle)
                    );
                    
                    // Calculate target that extends to map edge or beyond
                    float distance = minDistance;
                    targetPos = currentPos + direction * distance;
                    
                    // Ensure path stays within map bounds while being long
                    // Calculate maximum distance we can travel in this direction while staying in bounds
                    float maxDistX = direction.X > 0 ? (MapSize - 200f - currentPos.X) / Math.Max(direction.X, 0.001f) : (currentPos.X - 200f) / Math.Min(direction.X, -0.001f);
                    float maxDistY = direction.Y > 0 ? (MapSize - 200f - currentPos.Y) / Math.Max(direction.Y, 0.001f) : (currentPos.Y - 200f) / Math.Min(direction.Y, -0.001f);
                    float maxDist = Math.Min(maxDistX, maxDistY);
                    
                    // Use the minimum of desired distance and maximum safe distance
                    distance = Math.Min(distance, Math.Max(maxDist, minDistance));
                    targetPos = currentPos + direction * distance;
                }
                
                // Clamp target to map bounds (keep ships within galaxy map)
                const float targetMargin = 200f;
                targetPos = new Vector2(
                    MathHelper.Clamp(targetPos.X, targetMargin, MapSize - targetMargin),
                    MathHelper.Clamp(targetPos.Y, targetMargin, MapSize - targetMargin)
                );
                
                // Avoid player's radius - adjust target if too close
                if (IsTooCloseToPlayer(targetPos, friendlyShip))
                {
                    targetPos = AvoidPlayerPosition(targetPos, friendlyShip);
                    // Re-clamp after adjustment to keep within map bounds
                    targetPos = new Vector2(
                        MathHelper.Clamp(targetPos.X, targetMargin, MapSize - targetMargin),
                        MathHelper.Clamp(targetPos.Y, targetMargin, MapSize - targetMargin)
                    );
                }
                
                // Avoid other ships' radius - adjust target if too close
                if (IsTooCloseToOtherShips(targetPos, friendlyShip))
                {
                    targetPos = AvoidOtherShipsPosition(targetPos, friendlyShip);
                    // Re-clamp after adjustment to keep within map bounds
                    targetPos = new Vector2(
                        MathHelper.Clamp(targetPos.X, targetMargin, MapSize - targetMargin),
                        MathHelper.Clamp(targetPos.Y, targetMargin, MapSize - targetMargin)
                    );
                }
                
                // Clear any stored original destination and A* path since we're setting a new behavior target
                if (_friendlyShipOriginalDestination.ContainsKey(friendlyShip))
                {
                    _friendlyShipOriginalDestination.Remove(friendlyShip);
                }
                if (_friendlyShipAStarPaths.ContainsKey(friendlyShip))
                {
                    _friendlyShipAStarPaths.Remove(friendlyShip);
                }
                if (_friendlyShipCurrentWaypointIndex.ContainsKey(friendlyShip))
                {
                    _friendlyShipCurrentWaypointIndex.Remove(friendlyShip);
                }
                friendlyShip.SetTargetPosition(targetPos);
            }
        }
        
        private void ExecuteFleeBehavior(FriendlyShip friendlyShip)
        {
            // Flee: Ship continuously tries to escape from nearest threat (player or enemy)
            Vector2? nearestThreatPos = null;
            float nearestThreatDistance = float.MaxValue;
            
            // Check player as threat
            if (_playerShip != null && _playerShip.IsActive)
            {
                float distanceToPlayer = Vector2.Distance(friendlyShip.Position, _playerShip.Position);
                if (distanceToPlayer < nearestThreatDistance)
                {
                    nearestThreatDistance = distanceToPlayer;
                    nearestThreatPos = _playerShip.Position;
                }
            }
            
            // Check enemy ships as threats (for friendly ships)
            if (friendlyShip is not EnemyShip)
            {
                foreach (var enemyShip in _enemyShips)
                {
                    if (!enemyShip.IsActive) continue;
                    float distanceToEnemy = Vector2.Distance(friendlyShip.Position, enemyShip.Position);
                    if (distanceToEnemy < nearestThreatDistance)
                    {
                        nearestThreatDistance = distanceToEnemy;
                        nearestThreatPos = enemyShip.Position;
                    }
                }
            }
            
            // If no threat found, stop fleeing
            if (!nearestThreatPos.HasValue) return;
            
            // Calculate direction away from nearest threat
            Vector2 awayFromThreat = friendlyShip.Position - nearestThreatPos.Value;
            float distanceToThreat = awayFromThreat.Length();
            
            if (distanceToThreat > 0.1f)
            {
                awayFromThreat.Normalize();
                
                // Set aim target in the flee direction so ship immediately turns away from threat
                // This makes the ship turn to face the direction it's fleeing (like enemy ships do)
                Vector2 fleeAimTarget = friendlyShip.Position + awayFromThreat * 1000f; // Aim point far in flee direction
                friendlyShip.SetAimTarget(fleeAimTarget);
                
                // Continuously update flee target to keep moving away
                // Use a shorter, more dynamic flee distance that gets updated frequently to prevent getting stuck
                // Start with a closer target and extend it as the ship moves away
                float currentDistanceToThreat = Vector2.Distance(friendlyShip.Position, nearestThreatPos.Value);
                float fleeDistance = Math.Max(1500f, currentDistanceToThreat + 1000f); // At least 1500 away, or current distance + 1000
                Vector2 fleeTarget = friendlyShip.Position + awayFromThreat * fleeDistance;
                
                // Clamp to map bounds
                const float margin = 200f;
                fleeTarget = new Vector2(
                    MathHelper.Clamp(fleeTarget.X, margin, MapSize - margin),
                    MathHelper.Clamp(fleeTarget.Y, margin, MapSize - margin)
                );
                
                // Avoid other ships' radius when setting flee target
                if (IsTooCloseToOtherShips(fleeTarget, friendlyShip))
                {
                    fleeTarget = AvoidOtherShipsPosition(fleeTarget, friendlyShip);
                    fleeTarget = new Vector2(
                        MathHelper.Clamp(fleeTarget.X, margin, MapSize - margin),
                        MathHelper.Clamp(fleeTarget.Y, margin, MapSize - margin)
                    );
                }
                
                // Clear A* pathfinding when fleeing to prevent getting stuck
                // Fleeing ships should use direct movement, not pathfinding
                if (_friendlyShipAStarPaths.ContainsKey(friendlyShip))
                {
                    _friendlyShipAStarPaths.Remove(friendlyShip);
                }
                if (_friendlyShipOriginalDestination.ContainsKey(friendlyShip))
                {
                    _friendlyShipOriginalDestination.Remove(friendlyShip);
                }
                if (_friendlyShipCurrentWaypointIndex.ContainsKey(friendlyShip))
                {
                    _friendlyShipCurrentWaypointIndex.Remove(friendlyShip);
                }
                
                // Always update target position to keep fleeing (even if already moving)
                // This ensures the ship continuously moves away from the threat
                friendlyShip.SetTargetPosition(fleeTarget);
                friendlyShip.IsIdle = false; // Ensure ship is moving
            }
        }
        
        private void ExecuteWanderBehavior(FriendlyShip friendlyShip)
        {
            // Wander: Ship moves randomly within map bounds, avoiding center and player
            if (!friendlyShip.IsActivelyMoving())
            {
                Vector2 currentPos = friendlyShip.Position;
                Vector2 mapCenter = new Vector2(MapSize / 2f, MapSize / 2f);
                Vector2 toCenter = mapCenter - currentPos;
                float distanceToCenter = toCenter.Length();
                toCenter.Normalize();
                
                Vector2 targetPos;
                int attempts = 0;
                const int maxAttempts = 10;
                float minDistanceFromPlayer = friendlyShip.AvoidanceDetectionRange * 3f;
                const float minDistanceFromCenter = 1000f;
                
                do
                {
                    float randomAngle = (float)(_random.NextDouble() * MathHelper.TwoPi);
                    Vector2 randomDirection = new Vector2(
                        (float)Math.Cos(randomAngle),
                        (float)Math.Sin(randomAngle)
                    );
                    
                    float centerBiasStrength = MathHelper.Clamp(1f - (distanceToCenter / (MapSize * 0.3f)), 0.3f, 0.8f);
                    Vector2 awayFromCenter = -toCenter;
                    Vector2 blendedDirection = randomDirection * (1f - centerBiasStrength) + awayFromCenter * centerBiasStrength;
                    
                    if (_playerShip != null)
                    {
                        Vector2 awayFromPlayer = currentPos - _playerShip.Position;
                        float distToPlayer = awayFromPlayer.Length();
                        if (distToPlayer > 0.1f)
                        {
                            awayFromPlayer.Normalize();
                            float playerBias = MathHelper.Clamp((minDistanceFromPlayer - distToPlayer) / minDistanceFromPlayer, 0f, 0.8f);
                            blendedDirection = blendedDirection * (1f - playerBias) + awayFromPlayer * playerBias;
                        }
                    }
                    
                    blendedDirection.Normalize();
                    
                    // Make paths longer and smoother by considering current direction
                    Vector2 currentDirection = Vector2.Zero;
                    if (_friendlyShipLastDirection.ContainsKey(friendlyShip))
                    {
                        currentDirection = _friendlyShipLastDirection[friendlyShip];
                        // Blend new direction with current direction for smoother paths (70% new, 30% current)
                        blendedDirection = blendedDirection * 0.7f + currentDirection * 0.3f;
                        blendedDirection.Normalize();
                    }
                    
                    // Longer paths: 1000-2000 pixels (minimum 1000 to ensure ships can turn smoothly)
                    // Ship needs ~400-500 pixels minimum to complete a 180° turn while moving
                    float targetDistance = (float)(_random.NextDouble() * 1000f + 1000f);
                    Vector2 targetOffset = blendedDirection * targetDistance;
                    targetPos = currentPos + targetOffset;
                    
                    // Store direction for next path calculation
                    _friendlyShipLastDirection[friendlyShip] = blendedDirection;
                    
                    const float targetMargin = 200f;
                    targetPos = new Vector2(
                        MathHelper.Clamp(targetPos.X, targetMargin, MapSize - targetMargin),
                        MathHelper.Clamp(targetPos.Y, targetMargin, MapSize - targetMargin)
                    );
                    
                    attempts++;
                    
                    float distToCenter = Vector2.Distance(targetPos, mapCenter);
                    if (distToCenter < minDistanceFromCenter)
                    {
                        if (attempts < maxAttempts)
                            continue;
                    }
                    
                    // Check if target is safe from player and other ships
                    bool safeFromPlayer = true;
                    if (_playerShip != null)
                    {
                        float distToPlayer = Vector2.Distance(targetPos, _playerShip.Position);
                        float minSafeDistance = _playerShip.AvoidanceDetectionRange * 1.5f; // 1.5x player's avoidance range
                        safeFromPlayer = distToPlayer >= minSafeDistance;
                    }
                    
                    bool safeFromOtherShips = !IsTooCloseToOtherShips(targetPos, friendlyShip);
                    
                    if (safeFromPlayer && safeFromOtherShips && distToCenter >= minDistanceFromCenter)
                    {
                        break;
                    }
                    else if (attempts >= maxAttempts)
                    {
                        break;
                    }
                } while (attempts < maxAttempts);
                
                // Ensure target avoids player's radius
                if (IsTooCloseToPlayer(targetPos, friendlyShip))
                {
                    targetPos = AvoidPlayerPosition(targetPos, friendlyShip);
                    // Re-clamp to map bounds
                    const float margin = 200f;
                    targetPos = new Vector2(
                        MathHelper.Clamp(targetPos.X, margin, MapSize - margin),
                        MathHelper.Clamp(targetPos.Y, margin, MapSize - margin)
                    );
                }
                
                // Ensure target avoids other ships' radius
                if (IsTooCloseToOtherShips(targetPos, friendlyShip))
                {
                    targetPos = AvoidOtherShipsPosition(targetPos, friendlyShip);
                    // Re-clamp to map bounds
                    const float margin = 200f;
                    targetPos = new Vector2(
                        MathHelper.Clamp(targetPos.X, margin, MapSize - margin),
                        MathHelper.Clamp(targetPos.Y, margin, MapSize - margin)
                    );
                }
                
                // Clear any stored original destination and A* path since we're setting a new behavior target
                if (_friendlyShipOriginalDestination.ContainsKey(friendlyShip))
                {
                    _friendlyShipOriginalDestination.Remove(friendlyShip);
                }
                if (_friendlyShipAStarPaths.ContainsKey(friendlyShip))
                {
                    _friendlyShipAStarPaths.Remove(friendlyShip);
                }
                if (_friendlyShipCurrentWaypointIndex.ContainsKey(friendlyShip))
                {
                    _friendlyShipCurrentWaypointIndex.Remove(friendlyShip);
                }
                friendlyShip.SetTargetPosition(targetPos);
            }
        }
        
        // ========== End Behavior System Methods ==========
        
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
            
            // Draw A* pathfinding grid (in world space, before ships)
            if (_pathfindingGridVisible && _pathfindingGrid != null && _gridPixelTexture != null)
            {
                // Update grid with current obstacles for visualization
                UpdatePathfindingGridForVisualization();
                DrawPathfindingGrid(spriteBatch);
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
                DrawAvoidanceRange(spriteBatch);
            }
            
            // Draw player ship
            _playerShip?.Draw(spriteBatch);
            
            // Draw ship paths if enabled
            if (_enemyPathVisible)
            {
                DrawEnemyPaths(spriteBatch);
            }
            
            // Draw enemy target paths if enabled
            if (_enemyTargetPathVisible)
            {
                DrawEnemyTargetPaths(spriteBatch);
            }
            
            // Draw look-ahead debug lines if enabled
            DrawLookAheadLines(spriteBatch);
            
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
            
            // Draw active explosions (explosions that continue after ships are destroyed)
            foreach (var explosion in _activeExplosions)
            {
                explosion.Draw(spriteBatch);
            }
            
            spriteBatch.End();
            
            // Draw health bars in screen space (after world-space batch ends)
            DrawHealthBars(spriteBatch, transform);
            
            // Draw behavior labels in screen space (after world-space batch ends)
            if (_font != null && _behaviorTextVisible)
            {
                spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend);
                
                int behaviorLabelsDrawn = 0;
                foreach (var friendlyShip in _friendlyShips)
                {
                    if (_friendlyShipBehaviors.ContainsKey(friendlyShip))
                    {
                        FriendlyShipBehavior behavior = _friendlyShipBehaviors[friendlyShip];
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
                    if (_enemyShipBehaviors.ContainsKey(enemyShip))
                    {
                        FriendlyShipBehavior behavior = _enemyShipBehaviors[enemyShip];
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
            
            foreach (var laser in _lasers)
            {
                laser.Draw(spriteBatch);
            }

            spriteBatch.End();
            
            // Draw minimap in upper right corner (if visible)
            if (_minimapVisible)
            {
                DrawMinimap(spriteBatch);
            }
            
            // Draw UI grid overlay if enabled (draw before UI so grid appears under UI)
            if (_uiGridVisible)
            {
                DrawUIGrid(spriteBatch);
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
                _playerShip = new PlayerShip(GraphicsDevice, Content);
            _playerShip.Health = 50f; // Player has 50 health
            _playerShip.MaxHealth = 50f;
            _playerShip.Damage = 10f; // Player does 10 damage
            }
            else if (classIndex == 1)
            {
                _playerShip = new FriendlyShip(GraphicsDevice, Content);
            }
            else // classIndex == 2
            {
                _playerShip = new EnemyShip(GraphicsDevice, Content);
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
        
        private void DrawUIGrid(SpriteBatch spriteBatch)
        {
            if (_gridPixelTexture == null || spriteBatch == null) return;
            
            spriteBatch.Begin(
                SpriteSortMode.Deferred,
                BlendState.AlphaBlend,
                SamplerState.PointClamp,
                DepthStencilState.None,
                RasterizerState.CullNone
            );
            
            var viewport = GraphicsDevice.Viewport;
            var mouseState = Mouse.GetState();
            var gridColor = new Color(255, 255, 255, 50); // 50% less opaque (was 100, now 50)
            
            // Draw vertical lines
            for (int x = 0; x <= viewport.Width; x += UIGridSize)
            {
                spriteBatch.Draw(
                    _gridPixelTexture,
                    new Rectangle(x, 0, 1, viewport.Height),
                    gridColor
                );
            }
            
            // Draw horizontal lines
            for (int y = 0; y <= viewport.Height; y += UIGridSize)
            {
                spriteBatch.Draw(
                    _gridPixelTexture,
                    new Rectangle(0, y, viewport.Width, 1),
                    gridColor
                );
            }
            
            // Highlight the grid point under the mouse cursor with a 3x3 red pixel
            if (mouseState.X >= 0 && mouseState.X < viewport.Width && 
                mouseState.Y >= 0 && mouseState.Y < viewport.Height)
            {
                int snappedX = (mouseState.X / UIGridSize) * UIGridSize;
                int snappedY = (mouseState.Y / UIGridSize) * UIGridSize;
                
                var highlightColor = Color.Red; // Red pixel
                
                // Draw a 3x3 pixel square at the grid point (centered on grid intersection)
                int offset = 1; // Offset to center the 3x3 square on the grid point
                spriteBatch.Draw(
                    _gridPixelTexture,
                    new Rectangle(snappedX - offset, snappedY - offset, 3, 3),
                    highlightColor
                );
            }
            
            spriteBatch.End();
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
        
        private void DrawPathfindingGrid(SpriteBatch spriteBatch)
        {
            if (_pathfindingGrid == null || _gridPixelTexture == null)
            {
                System.Console.WriteLine($"DrawPathfindingGrid: _pathfindingGrid={_pathfindingGrid != null}, _gridPixelTexture={_gridPixelTexture != null}");
                return;
            }
            
            // Calculate visible grid range based on camera view
            var viewWidth = GraphicsDevice.Viewport.Width / _cameraZoom;
            var viewHeight = GraphicsDevice.Viewport.Height / _cameraZoom;
            var padding = _pathfindingGrid.CellSize; // Add one cell padding on each side
            
            var minX = (int)Math.Floor((_cameraPosition.X - viewWidth / 2f - padding) / _pathfindingGrid.CellSize);
            var maxX = (int)Math.Ceiling((_cameraPosition.X + viewWidth / 2f + padding) / _pathfindingGrid.CellSize);
            var minY = (int)Math.Floor((_cameraPosition.Y - viewHeight / 2f - padding) / _pathfindingGrid.CellSize);
            var maxY = (int)Math.Ceiling((_cameraPosition.Y + viewHeight / 2f + padding) / _pathfindingGrid.CellSize);
            
            // Clamp to grid bounds
            minX = Math.Max(0, minX);
            maxX = Math.Min(_pathfindingGrid.GridWidth - 1, maxX);
            minY = Math.Max(0, minY);
            maxY = Math.Min(_pathfindingGrid.GridHeight - 1, maxY);
            
            float cellSize = _pathfindingGrid.CellSize;
            
            int cellsDrawn = 0;
            int walkableCells = 0;
            int obstacleCells = 0;
            
            // Draw grid cells
            for (int x = minX; x <= maxX; x++)
            {
                for (int y = minY; y <= maxY; y++)
                {
                    var node = _pathfindingGrid.GetNodeAt(x, y);
                    if (node == null) continue;
                    
                    Vector2 cellPos = new Vector2(x * cellSize, y * cellSize);
                    Rectangle cellRect = new Rectangle((int)cellPos.X, (int)cellPos.Y, (int)cellSize, (int)cellSize);
                    
                    // Draw cell based on walkability
                    Color cellColor;
                    if (!node.Walkable)
                    {
                        // Obstacle - red with full opacity for visibility
                        cellColor = Color.Red;
                        obstacleCells++;
                    }
                    else
                    {
                        // Walkable - bright green for visibility
                        cellColor = new Color(0, 255, 0, 180);
                        walkableCells++;
                    }
                    
                    spriteBatch.Draw(_gridPixelTexture, cellRect, cellColor);
                    cellsDrawn++;
                    
                    // Draw cell border with higher opacity
                    Color borderColor = new Color(200, 200, 200, 255);
                    int borderWidth = 2; // Thicker border for visibility
                    
                    // Top edge
                    spriteBatch.Draw(_gridPixelTexture, 
                        new Rectangle(cellRect.X, cellRect.Y, cellRect.Width, borderWidth), 
                        borderColor);
                    // Bottom edge
                    spriteBatch.Draw(_gridPixelTexture, 
                        new Rectangle(cellRect.X, cellRect.Y + cellRect.Height - borderWidth, cellRect.Width, borderWidth), 
                        borderColor);
                    // Left edge
                    spriteBatch.Draw(_gridPixelTexture, 
                        new Rectangle(cellRect.X, cellRect.Y, borderWidth, cellRect.Height), 
                        borderColor);
                    // Right edge
                    spriteBatch.Draw(_gridPixelTexture, 
                        new Rectangle(cellRect.X + cellRect.Width - borderWidth, cellRect.Y, borderWidth, cellRect.Height), 
                        borderColor);
                }
            }
            
            // Debug output (only print occasionally to avoid spam)
            if (cellsDrawn > 0 && System.Environment.TickCount % 2000 < 16) // Print roughly every 2 seconds
            {
                System.Console.WriteLine($"A* Grid: Drawing {cellsDrawn} cells (Walkable: {walkableCells}, Obstacles: {obstacleCells}), Range: X[{minX}-{maxX}] Y[{minY}-{maxY}]");
            }
        }
        
        private void DrawMinimap(SpriteBatch spriteBatch)
        {
            if (_minimapBackgroundTexture == null || _minimapPlayerDotTexture == null || _minimapFriendlyDotTexture == null || _minimapEnemyDotTexture == null || _minimapViewportOutlineTexture == null)
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
            
            // Draw enemy ships on minimap
            int enemyShipsDrawn = 0;
            foreach (var enemyShip in _enemyShips)
            {
                var enemyScreenPos = WorldToMinimap(enemyShip.Position);
                var enemyDotSize = 4f; // Same size as player dot for better visibility
                
                // Clamp enemy position to minimap bounds
                enemyScreenPos.X = MathHelper.Clamp(enemyScreenPos.X, minimapX, minimapX + MinimapSize);
                enemyScreenPos.Y = MathHelper.Clamp(enemyScreenPos.Y, minimapY, minimapY + MinimapSize);
                
                // Use bright red for enemy ships
                Color enemyColor = new Color(255, 0, 0, 255); // Bright red, fully opaque
                
                spriteBatch.Draw(
                    _minimapEnemyDotTexture, // Use dedicated red texture for enemy ships
                    enemyScreenPos,
                    null,
                    Color.White, // Use white so the red texture color shows through
                    0f,
                    Vector2.Zero,
                    enemyDotSize,
                    SpriteEffects.None,
                    0f
                );
                enemyShipsDrawn++;
            }
            
            // Debug output (only print occasionally to avoid spam)
            if (System.Environment.TickCount % 3000 < 16) // Print roughly every 3 seconds
            {
                System.Console.WriteLine($"[MINIMAP] Total enemy ships: {_enemyShips.Count}, Drawing: {enemyShipsDrawn} enemy ships (red dots)");
                if (_enemyShips.Count > 0)
                {
                    foreach (var enemyShip in _enemyShips)
                    {
                        var worldPos = enemyShip.Position;
                        var minimapPos = WorldToMinimap(worldPos);
                        System.Console.WriteLine($"  Enemy at world ({worldPos.X:F0}, {worldPos.Y:F0}) -> minimap ({minimapPos.X:F0}, {minimapPos.Y:F0})");
                    }
                }
                
                // Also write to a log file for easier debugging
                try
                {
                    var logPath = System.IO.Path.Combine(System.AppDomain.CurrentDomain.BaseDirectory, "minimap_debug.log");
                    using (var writer = new System.IO.StreamWriter(logPath, append: true))
                    {
                        writer.WriteLine($"[{System.DateTime.Now:HH:mm:ss}] MINIMAP: Total enemy ships: {_enemyShips.Count}, Drawing: {enemyShipsDrawn}");
                        if (_enemyShips.Count > 0)
                        {
                            foreach (var enemyShip in _enemyShips)
                            {
                                var worldPos = enemyShip.Position;
                                var minimapPos = WorldToMinimap(worldPos);
                                writer.WriteLine($"  Enemy at world ({worldPos.X:F0}, {worldPos.Y:F0}) -> minimap ({minimapPos.X:F0}, {minimapPos.Y:F0})");
                            }
                        }
                    }
                }
                catch { /* Ignore log file errors */ }
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
        
        private void DrawAvoidanceRange(SpriteBatch spriteBatch)
        {
            if (_gridPixelTexture == null) return;
            
            // Draw avoidance range circle for player ship (use player's own range)
            if (_playerShip != null)
            {
                float playerRadius = _playerShip.AvoidanceDetectionRange * 1.33f; // Player avoidance radius (33% larger)
                DrawCircle(spriteBatch, _playerShip.Position, playerRadius, new Color(255, 100, 100, 100)); // Red tint for player
            }
            
            // Draw avoidance range circles for friendly ships (use each ship's own range)
            foreach (var friendlyShip in _friendlyShips)
            {
                float radius = friendlyShip.AvoidanceDetectionRange; // Use ship's own avoidance range
                DrawCircle(spriteBatch, friendlyShip.Position, radius, new Color(100, 255, 100, 100)); // Green tint for friendly
            }
        }
        
        private void DrawEnemyPaths(SpriteBatch spriteBatch)
        {
            if (_gridPixelTexture == null) return;
            
            foreach (var friendlyShip in _friendlyShips)
            {
                if (!_friendlyShipPaths.ContainsKey(friendlyShip) || _friendlyShipPaths[friendlyShip].Count < 2)
                    continue;
                
                var path = _friendlyShipPaths[friendlyShip];
                Color pathColor = new Color(255, 100, 100, 150); // Semi-transparent red for ship paths
                
                // Draw path as connected lines
                for (int i = 0; i < path.Count - 1; i++)
                {
                    Vector2 start = path[i];
                    Vector2 end = path[i + 1];
                    
                    Vector2 direction = end - start;
                    float length = direction.Length();
                    if (length > 0.1f)
                    {
                        float rotation = (float)System.Math.Atan2(direction.Y, direction.X);
                        spriteBatch.Draw(
                            _gridPixelTexture,
                            start,
                            null,
                            pathColor,
                            rotation,
                            new Vector2(0, 0.5f),
                            new Vector2(length, 1f), // 1 pixel thick line
                            SpriteEffects.None,
                            0f
                        );
                    }
                }
            }
        }
        
        private void DrawEnemyTargetPaths(SpriteBatch spriteBatch)
        {
            if (_gridPixelTexture == null) return;
            
            foreach (var friendlyShip in _friendlyShips)
            {
                Vector2 currentPos = friendlyShip.Position;
                Vector2 targetPos = friendlyShip.TargetPosition;
                
                // Only draw if ship is moving and target is different from current position
                if (Vector2.Distance(currentPos, targetPos) > 1f)
                {
                    Vector2 direction = targetPos - currentPos;
                    float length = direction.Length();
                    
                    if (length > 0.1f)
                    {
                        float rotation = (float)System.Math.Atan2(direction.Y, direction.X);
                        Color pathColor = new Color(255, 200, 0, 200); // Semi-transparent yellow/orange for target paths
                        
                        spriteBatch.Draw(
                            _gridPixelTexture,
                            currentPos,
                            null,
                            pathColor,
                            rotation,
                            new Vector2(0, 0.5f),
                            new Vector2(length, 2f), // 2 pixel thick line
                            SpriteEffects.None,
                            0f
                        );
                        
                        // Draw a small circle at the target position
                        DrawCircle(spriteBatch, targetPos, 5f, new Color(255, 200, 0, 255)); // Yellow circle at target
                    }
                }
            }
        }
        
        private void DrawLookAheadLines(SpriteBatch spriteBatch)
        {
            if (_gridPixelTexture == null)
            {
                System.Console.WriteLine("[LOOK-AHEAD] _gridPixelTexture is null!");
                return;
            }
            
            // Draw look-ahead lines for player ship
            if (_playerShip != null && _playerShip.LookAheadVisible)
            {
                Vector2 shipPos = _playerShip.Position;
                
                // Calculate look-ahead target in the direction the ship is facing
                // Ship rotation: 0 = pointing up (north), so we need to adjust for sprite rotation
                // Rotation is in radians, where 0 = up, Pi/2 = right, Pi = down, -Pi/2 = left
                float shipRotation = _playerShip.Rotation;
                
                // Convert rotation to direction vector (ship points up at rotation 0)
                // Rotation 0 = (0, -1) in screen space, but we need world space
                Vector2 direction = new Vector2(
                    (float)System.Math.Sin(shipRotation),  // X component
                    -(float)System.Math.Cos(shipRotation) // Y component (negative because up is negative Y in screen space)
                );
                
                float lookAheadDist = _playerShip.MoveSpeed * _playerShip.LookAheadDistance;
                Vector2 lookAheadTarget = shipPos + direction * lookAheadDist;
                
                // Draw line from ship to look-ahead target
                Vector2 lineDir = lookAheadTarget - shipPos;
                float lineLength = lineDir.Length();
                if (lineLength > 0.1f)
                {
                    float rotation = (float)System.Math.Atan2(lineDir.Y, lineDir.X);
                    Color lineColor = new Color(0, 255, 255, 255); // Cyan for look-ahead line (fully opaque for visibility)
                    
                    spriteBatch.Draw(
                        _gridPixelTexture,
                        shipPos,
                        null,
                        lineColor,
                        rotation,
                        new Vector2(0, 0.5f),
                        new Vector2(lineLength, 3f), // 3 pixel thick line for better visibility
                        SpriteEffects.None,
                        0f
                    );
                    
                    // Draw a small circle at the look-ahead target
                    DrawCircle(spriteBatch, lookAheadTarget, 6f, new Color(0, 255, 255, 255)); // Cyan circle, larger for visibility
                }
            }
            else
            {
                if (_playerShip == null)
                {
                    System.Console.WriteLine("[LOOK-AHEAD] _playerShip is null!");
                }
                else if (!_playerShip.LookAheadVisible)
                {
                    System.Console.WriteLine("[LOOK-AHEAD] LookAheadVisible is false!");
                }
            }
            
            // Draw look-ahead lines for friendly ships
            foreach (var friendlyShip in _friendlyShips)
            {
                if (friendlyShip.LookAheadVisible)
                {
                    Vector2 shipPos = friendlyShip.Position;
                    
                    // Calculate look-ahead target in the direction the ship is facing
                    float shipRotation = friendlyShip.Rotation;
                    
                    // Convert rotation to direction vector (ship points up at rotation 0)
                    Vector2 direction = new Vector2(
                        (float)System.Math.Sin(shipRotation),  // X component
                        -(float)System.Math.Cos(shipRotation) // Y component (negative because up is negative Y in screen space)
                    );
                    
                    float lookAheadDist = friendlyShip.MoveSpeed * friendlyShip.LookAheadDistance;
                    Vector2 lookAheadTarget = shipPos + direction * lookAheadDist;
                    
                    // Draw line from ship to look-ahead target
                    Vector2 lineDir = lookAheadTarget - shipPos;
                    float lineLength = lineDir.Length();
                    if (lineLength > 0.1f)
                    {
                        float rotation = (float)System.Math.Atan2(lineDir.Y, lineDir.X);
                        Color lineColor = new Color(0, 255, 255, 255); // Cyan for look-ahead line (fully opaque)
                        
                        spriteBatch.Draw(
                            _gridPixelTexture,
                            shipPos,
                            null,
                            lineColor,
                            rotation,
                            new Vector2(0, 0.5f),
                            new Vector2(lineLength, 3f), // 3 pixel thick line for better visibility
                            SpriteEffects.None,
                            0f
                        );
                        
                        // Draw a small circle at the look-ahead target
                        DrawCircle(spriteBatch, lookAheadTarget, 6f, new Color(0, 255, 255, 255)); // Cyan circle, larger for visibility
                    }
                }
            }
            
            // Draw look-ahead lines for enemy ships (if enabled)
            foreach (var enemyShip in _enemyShips)
            {
                if (enemyShip.LookAheadVisible)
                {
                    Vector2 shipPos = enemyShip.Position;
                    
                    // Calculate look-ahead target in the direction the ship is facing
                    float shipRotation = enemyShip.Rotation;
                    
                    // Convert rotation to direction vector (ship points up at rotation 0)
                    Vector2 direction = new Vector2(
                        (float)System.Math.Sin(shipRotation),  // X component
                        -(float)System.Math.Cos(shipRotation) // Y component (negative because up is negative Y in screen space)
                    );
                    
                    float lookAheadDist = enemyShip.MoveSpeed * enemyShip.LookAheadDistance;
                    Vector2 lookAheadTarget = shipPos + direction * lookAheadDist;
                    
                    // Draw line from ship to look-ahead target
                    Vector2 lineDir = lookAheadTarget - shipPos;
                    float lineLength = lineDir.Length();
                    if (lineLength > 0.1f)
                    {
                        float rotation = (float)System.Math.Atan2(lineDir.Y, lineDir.X);
                        Color lineColor = new Color(255, 0, 0, 255); // Red for enemy look-ahead line
                        
                        spriteBatch.Draw(
                            _gridPixelTexture,
                            shipPos,
                            null,
                            lineColor,
                            rotation,
                            new Vector2(0, 0.5f),
                            new Vector2(lineLength, 3f), // 3 pixel thick line for better visibility
                            SpriteEffects.None,
                            0f
                        );
                        
                        // Draw a small circle at the look-ahead target
                        DrawCircle(spriteBatch, lookAheadTarget, 6f, new Color(255, 0, 0, 255)); // Red circle for enemy ships
                    }
                }
            }
        }
        
        private void DrawCircle(SpriteBatch spriteBatch, Vector2 center, float radius, Color color)
        {
            if (_gridPixelTexture == null) return;
            
            const int segments = 64; // Number of line segments to approximate circle
            float angleStep = MathHelper.TwoPi / segments;
            
            for (int i = 0; i < segments; i++)
            {
                float angle1 = i * angleStep;
                float angle2 = (i + 1) * angleStep;
                
                Vector2 point1 = center + new Vector2(
                    (float)Math.Cos(angle1) * radius,
                    (float)Math.Sin(angle1) * radius
                );
                Vector2 point2 = center + new Vector2(
                    (float)Math.Cos(angle2) * radius,
                    (float)Math.Sin(angle2) * radius
                );
                
                Vector2 direction = point2 - point1;
                float length = direction.Length();
                if (length > 0.1f)
                {
                    float rotation = (float)Math.Atan2(direction.Y, direction.X);
                    spriteBatch.Draw(
                        _gridPixelTexture,
                        point1,
                        null,
                        color,
                        rotation,
                        new Vector2(0, 0.5f),
                        new Vector2(length, 2f),
                        SpriteEffects.None,
                        0f
                    );
                }
            }
        }
        
        private void DrawHealthBars(SpriteBatch spriteBatch, Matrix transform)
        {
            // Create pixel texture if it doesn't exist
            if (_pixelTexture == null)
            {
                _pixelTexture = new Texture2D(GraphicsDevice, 1, 1);
                _pixelTexture.SetData(new[] { Color.White });
            }
            
            // Start a new sprite batch for screen-space drawing
            spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend);
            
            // Draw health bar for player ship
            if (_playerShip != null && _playerShip.IsActive)
            {
                DrawHealthBarForShip(spriteBatch, transform, _playerShip, Color.Green);
            }
            
            // Draw health bars for friendly ships
            foreach (var friendlyShip in _friendlyShips)
            {
                if (friendlyShip.IsActive)
                {
                    DrawHealthBarForShip(spriteBatch, transform, friendlyShip, Color.Cyan);
                }
            }
            
            // Draw health bars for enemy ships
            foreach (var enemyShip in _enemyShips)
            {
                if (enemyShip.IsActive)
                {
                    DrawHealthBarForShip(spriteBatch, transform, enemyShip, Color.Red);
                }
            }
            
            spriteBatch.End();
        }
        
        private void DrawHealthBarForShip(SpriteBatch spriteBatch, Matrix transform, PlayerShip ship, Color barColor)
        {
            if (ship.MaxHealth <= 0f) return; // Avoid division by zero
            if (_pixelTexture == null) return;
            
            // Calculate health percentage
            float healthPercent = MathHelper.Clamp(ship.Health / ship.MaxHealth, 0f, 1f);
            
            // Health bar dimensions (in screen space pixels)
            const float barWidth = 60f;
            const float barHeight = 6f;
            const float barOffsetY = -180f; // Position above ship (negative Y = up, increased to move bars higher)
            
            // Calculate bar position in world space (above ship)
            Vector2 barWorldPosition = ship.Position + new Vector2(0, barOffsetY);
            
            // Transform world position to screen position
            Vector2 barScreenPosition = Vector2.Transform(barWorldPosition, transform);
            
            // Draw background (dark gray) - draw in screen space
            Color backgroundColor = new Color(50, 50, 50, 200); // Dark gray with transparency
            spriteBatch.Draw(
                _pixelTexture,
                new Rectangle((int)barScreenPosition.X - (int)(barWidth / 2), (int)barScreenPosition.Y - (int)(barHeight / 2),
                    (int)barWidth, (int)barHeight),
                backgroundColor
            );
            
            // Draw health bar (colored based on health percentage)
            Color healthColor = barColor;
            if (healthPercent < 0.3f) // Low health - red tint
            {
                healthColor = Color.Red;
            }
            else if (healthPercent < 0.6f) // Medium health - yellow tint
            {
                healthColor = Color.Orange;
            }
            
            float healthBarWidth = barWidth * healthPercent;
            if (healthBarWidth > 0f)
            {
                spriteBatch.Draw(
                    _pixelTexture,
                    new Rectangle((int)barScreenPosition.X - (int)(barWidth / 2), (int)barScreenPosition.Y - (int)(barHeight / 2),
                        (int)healthBarWidth, (int)barHeight),
                    healthColor
                );
            }
            
            // Draw border (1 pixel thick lines in screen space)
            Color borderColor = new Color(200, 200, 200, 255); // Light gray border
            const int borderThickness = 1;
            
            // Top border
            spriteBatch.Draw(
                _pixelTexture,
                new Rectangle((int)barScreenPosition.X - (int)(barWidth / 2), (int)barScreenPosition.Y - (int)(barHeight / 2) - borderThickness,
                    (int)barWidth, borderThickness),
                borderColor
            );
            // Bottom border
            spriteBatch.Draw(
                _pixelTexture,
                new Rectangle((int)barScreenPosition.X - (int)(barWidth / 2), (int)barScreenPosition.Y - (int)(barHeight / 2) + (int)barHeight,
                    (int)barWidth, borderThickness),
                borderColor
            );
            // Left border
            spriteBatch.Draw(
                _pixelTexture,
                new Rectangle((int)barScreenPosition.X - (int)(barWidth / 2) - borderThickness, (int)barScreenPosition.Y - (int)(barHeight / 2),
                    borderThickness, (int)barHeight),
                borderColor
            );
            // Right border
            spriteBatch.Draw(
                _pixelTexture,
                new Rectangle((int)barScreenPosition.X - (int)(barWidth / 2) + (int)barWidth, (int)barScreenPosition.Y - (int)(barHeight / 2),
                    borderThickness, (int)barHeight),
                borderColor
            );
        }
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

