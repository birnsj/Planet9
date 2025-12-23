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
        // Minimap visibility managed by MinimapManager
        private bool _pathfindingGridVisible = false; // A* pathfinding grid visibility toggle (default off)
        private bool _gamePaused = false; // Game pause state (toggled with F12)
        private const int UIGridSize = 10; // UI grid cell size in pixels
        private const float MapSize = 8192f; // Total map size
        private const int MinimapSize = 200; // Minimap size in pixels (square)
        // Minimap textures moved to MinimapManager
        
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
        
        // Camera state (delegated to CameraController, keeping for compatibility)
        private bool _isPanningToPlayer = false;
        private bool _cameraFollowingPlayer = true; // Track if camera should follow player (start as true)
        private Vector2 _cameraVelocity = Vector2.Zero; // Camera velocity for inertia
        
        // Ship Manager - handles all ship management
        private ShipManager? _shipManager;
        
        // Player ship (delegated to ShipManager, keeping for compatibility)
        private PlayerShip? _playerShip;
        private int _currentShipClassIndex = 0; // 0 = PlayerShip, 1 = FriendlyShip, 2 = EnemyShip
        
        // Friendly ships (delegated to ShipManager, keeping for compatibility)
        private List<FriendlyShip> _friendlyShips = new List<FriendlyShip>();
        private Dictionary<FriendlyShip, ShipState> _friendlyShipStates = new Dictionary<FriendlyShip, ShipState>();
        
        // Enemy ships (delegated to ShipManager, keeping for compatibility)
        private List<EnemyShip> _enemyShips = new List<EnemyShip>();
        private Dictionary<EnemyShip, EnemyShipState> _enemyShipStates = new Dictionary<EnemyShip, EnemyShipState>();
        
        private const float EnemyPlayerDetectionRange = 1500f; // Range at which enemy detects and switches to aggressive behavior
        private const int MaxPathPoints = 100; // Maximum number of path points to store per ship
        private System.Random? _random; // Will be initialized from services
        
        // A* Pathfinding system
        private PathfindingManager? _pathfindingManager;
        
        // Minimap manager - handles minimap initialization and state
        private MinimapManager? _minimapManager;
        
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
        
        // UI Manager - handles all UI elements
        private GameSceneUIManager? _uiManager;
        
        // Settings Manager - handles loading and saving settings
        private SettingsManager? _settingsManager;
        
        // Input Manager - handles keyboard and mouse input
        private InputManager? _inputManager;
        
        // Input state (delegated to InputManager, keeping for compatibility)
        private MouseState _previousMouseState;
        private KeyboardState _previousKeyboardState;
        private bool _wasLeftButtonPressed = false;
        private bool _isFollowingMouse = false;
        private float _cameraPanSpeed = 800f; // pixels per second for smooth panning (faster)
        private float _cameraInertia = 0.85f; // Camera inertia factor (0 = no inertia, 1 = full inertia)
        
        // Audio Manager - handles all audio (music and SFX)
        private AudioManager? _audioManager;
        
        // Audio (delegated to AudioManager, keeping for compatibility)
        private SoundEffectInstance? _backgroundMusicInstance;
        private SoundEffectInstance? _shipFlySound;
        private SoundEffectInstance? _shipIdleSound;
        private SpriteFont? _font; // Font for drawing behavior labels
        
        // UI state (managed by UI manager, but kept for compatibility)
        private bool _uiVisible = true;
        private bool _behaviorTextVisible = true;
        private float _avoidanceDetectionRange = 300f;
        private float _shipIdleRate = 0.3f;

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
            
            // Initialize Audio Manager
            _audioManager = new AudioManager();
            _audioManager.GetMusicVolume = () => _uiManager?.MusicVolume ?? 0.5f;
            _audioManager.GetSFXVolume = () => _uiManager?.SFXVolume ?? 1.0f;
            _audioManager.GetMusicEnabled = () => _uiManager?.MusicEnabled ?? true;
            _audioManager.GetSFXEnabled = () => _uiManager?.SFXEnabled ?? true;
            
            // Load and play background music
            _audioManager.LoadBackgroundMusic(Content, "galaxy1");
            _backgroundMusicInstance = _audioManager.BackgroundMusicInstance;
            
            // Load combat manager content (lasers, explosions, sounds)
            _combatManager?.LoadContent(Content, _uiManager?.SFXVolume ?? 1.0f, _uiManager?.SFXEnabled ?? true);
            
            // Load ship idle and fly sound effects
            _audioManager.LoadShipSounds(Content, "shipidle1", "shipfly1");
            _shipIdleSound = _audioManager.ShipIdleSound;
            _shipFlySound = _audioManager.ShipFlySound;
            
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
            
            // Initialize Ship Manager
            _shipManager = new ShipManager(GraphicsDevice, Content, _random);
            
            // Set up Ship Manager callbacks for SwitchShipClass
            _shipManager.GetCurrentShipClassIndex = () => _currentShipClassIndex;
            _shipManager.SetCurrentShipClassIndex = (index) => { _currentShipClassIndex = index; };
            _shipManager.SaveCurrentShipSettings = () => { _settingsManager?.SaveCurrentShipSettings(); };
            _shipManager.LoadCurrentShipSettings = (float avoidanceRange, float idleRate) => 
            { 
                float avoidanceRangeRef = avoidanceRange;
                float idleRateRef = idleRate;
                _settingsManager?.LoadCurrentShipSettings(ref avoidanceRangeRef, ref idleRateRef);
                _avoidanceDetectionRange = avoidanceRangeRef;
                _shipIdleRate = idleRateRef;
                return (avoidanceRangeRef, idleRateRef);
            };
            _shipManager.GetAvoidanceDetectionRange = () => _avoidanceDetectionRange;
            _shipManager.SetAvoidanceDetectionRange = (range) => { _avoidanceDetectionRange = range; };
            _shipManager.GetShipIdleRate = () => _shipIdleRate;
            _shipManager.SetShipIdleRate = (rate) => { _shipIdleRate = rate; };
            _shipManager.UpdatePreviewShipIndex = (index) => { if (_uiManager != null) _uiManager.PreviewShipIndex = index; };
            _shipManager.UpdatePreviewShipLabel = UpdatePreviewShipLabel;
            _shipManager.IsPreviewActive = () => _uiManager?.IsPreviewActive ?? false;
            
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
            _shipManager.PlayerShip = _playerShip; // Sync with ShipManager
            
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
            
            // Initialize A* pathfinding manager
            _pathfindingManager = new PathfindingManager();
            _pathfindingManager.Initialize(MapSize, 128f); // 128 pixel cells
            
            // Initialize minimap manager
            _minimapManager = new MinimapManager(GraphicsDevice);
            _minimapManager.Initialize();
            
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
            
            // Initialize rendering manager after minimap textures are created
            _renderingManager = new RenderingManager(GraphicsDevice);
            _renderingManager.Initialize(
                _gridPixelTexture,
                _minimapManager.MinimapBackgroundTexture,
                _minimapManager.MinimapPlayerDotTexture,
                _minimapManager.MinimapFriendlyDotTexture,
                _minimapManager.MinimapEnemyDotTexture,
                _minimapManager.MinimapViewportOutlineTexture,
                _galaxyTexture,
                _galaxyOverlayTexture
            );
            
            // Initialize Settings Manager
            _settingsManager = new SettingsManager();
            
            // Set up Settings Manager callbacks
            _settingsManager.GetCurrentShipClassIndex = () => _currentShipClassIndex;
            _settingsManager.SwitchShipClass = SwitchShipClass;
            _settingsManager.GetCameraSpeed = () => CameraSpeed;
            _settingsManager.SetCameraSpeed = (speed) => { CameraSpeed = speed; if (_cameraController != null) _cameraController.CameraSpeed = speed; };
            _settingsManager.GetCameraZoom = () => _cameraZoom;
            _settingsManager.SetCameraZoom = (zoom) => { _cameraZoom = zoom; if (_cameraController != null) _cameraController.Zoom = zoom; };
            _settingsManager.GetCameraPanSpeed = () => _cameraPanSpeed;
            _settingsManager.SetCameraPanSpeed = (speed) => { _cameraPanSpeed = speed; if (_cameraController != null) _cameraController.PanSpeed = speed; };
            _settingsManager.GetCameraInertia = () => _cameraInertia;
            _settingsManager.SetCameraInertia = (inertia) => { _cameraInertia = inertia; if (_cameraController != null) _cameraController.Inertia = inertia; };
            _settingsManager.GetGridSize = () => _gridSize;
            _settingsManager.SetGridSize = (size) => { _gridSize = size; };
            _settingsManager.GetPlayerShip = () => _playerShip;
            _settingsManager.GetFriendlyShips = () => _friendlyShips;
            _settingsManager.GetEnemyShips = () => _enemyShips;
            _settingsManager.GetAvoidanceDetectionRange = () => _avoidanceDetectionRange;
            _settingsManager.SetAvoidanceDetectionRange = (range) => { _avoidanceDetectionRange = range; };
            _settingsManager.GetShipIdleRate = () => _shipIdleRate;
            _settingsManager.SetShipIdleRate = (rate) => { _shipIdleRate = rate; };
            _settingsManager.GetMusicVolume = () => _uiManager?.MusicVolume ?? 0.5f;
            _settingsManager.SetMusicVolume = (volume) => { if (_uiManager != null) _uiManager.MusicVolume = volume; };
            _settingsManager.GetSFXVolume = () => _uiManager?.SFXVolume ?? 1.0f;
            _settingsManager.SetSFXVolume = (volume) => { if (_uiManager != null) _uiManager.SFXVolume = volume; };
            _settingsManager.GetMusicEnabled = () => _uiManager?.MusicEnabled ?? true;
            _settingsManager.SetMusicEnabled = (enabled) => { if (_uiManager != null) _uiManager.MusicEnabled = enabled; };
            _settingsManager.GetSFXEnabled = () => _uiManager?.SFXEnabled ?? true;
            _settingsManager.SetSFXEnabled = (enabled) => { if (_uiManager != null) _uiManager.SFXEnabled = enabled; };
            _settingsManager.GetUIVisible = () => _uiVisible;
            _settingsManager.SetUIVisible = (visible) => { _uiVisible = visible; };
            _settingsManager.GetBehaviorTextVisible = () => _behaviorTextVisible;
            _settingsManager.SetBehaviorTextVisible = (visible) => { _behaviorTextVisible = visible; };
            _settingsManager.GetEnemyPathVisible = () => _uiManager?.EnemyPathVisible ?? false;
            _settingsManager.SetEnemyPathVisible = (visible) => { if (_uiManager != null) _uiManager.EnemyPathVisible = visible; };
            _settingsManager.GetEnemyTargetPathVisible = () => _uiManager?.EnemyTargetPathVisible ?? false;
            _settingsManager.SetEnemyTargetPathVisible = (visible) => { if (_uiManager != null) _uiManager.EnemyTargetPathVisible = visible; };
            _settingsManager.GetAvoidanceRangeVisible = () => _uiManager?.AvoidanceRangeVisible ?? false;
            _settingsManager.SetAvoidanceRangeVisible = (visible) => { if (_uiManager != null) _uiManager.AvoidanceRangeVisible = visible; };
            _settingsManager.GetPathfindingGridVisible = () => _pathfindingGridVisible;
            _settingsManager.SetPathfindingGridVisible = (visible) => { _pathfindingGridVisible = visible; };
            _settingsManager.GetGridVisible = () => _gridVisible;
            _settingsManager.SetGridVisible = (visible) => { _gridVisible = visible; };
            _settingsManager.GetMinimapVisible = () => _minimapManager?.MinimapVisible ?? true;
            _settingsManager.SetMinimapVisible = (visible) => { if (_minimapManager != null) _minimapManager.MinimapVisible = visible; };
            _settingsManager.SetUIGridSize = (size) => { if (_uiManager != null) _uiManager.GridSize = size; };
            _settingsManager.SetUIAvoidanceDetectionRange = (range) => { if (_uiManager != null) _uiManager.AvoidanceDetectionRange = range; };
            _settingsManager.SetUIShipIdleRate = (rate) => { if (_uiManager != null) _uiManager.ShipIdleRate = rate; };
            _settingsManager.GetBackgroundMusicInstance = () => _backgroundMusicInstance;
            _settingsManager.GetShipFlySound = () => _shipFlySound;
            _settingsManager.GetShipIdleSound = () => _shipIdleSound;
            _settingsManager.SetCombatSFXSettings = (volume, enabled) => { _combatManager?.SetSFXSettings(volume, enabled); };
            _settingsManager.GetUIDesktop = () => _uiManager?.Desktop;
            _settingsManager.GetSaveButtonDesktop = () => _uiManager?.SaveButtonDesktop;
            
            // Initialize UI Manager
            _uiManager = new GameSceneUIManager(GraphicsDevice, Content, _font);
            
            // Set up UI manager callbacks
            _uiManager.OnSaveCurrentShipSettings = () => _settingsManager?.SaveCurrentShipSettings();
            _uiManager.OnSaveSettings = () => _settingsManager?.SaveSettings(() => _settingsManager?.SaveCurrentShipSettings(), () => _settingsManager?.SavePanelSettings());
            _uiManager.OnSavePanelSettings = () => _settingsManager?.SavePanelSettings();
            _uiManager.OnSwitchShipClass = SwitchShipClass;
            _uiManager.OnShipIdleRateChanged = (rate) => 
            { 
                _shipIdleRate = rate; 
                _behaviorManager?.SetDependencies(_playerShip, _friendlyShips, _enemyShips, _friendlyShipStates, _enemyShipStates, _pathfindingManager?.Grid, _combatManager, GetOrCreateShipState, GetOrCreateEnemyShipState, rate); 
            };
            _uiManager.OnSFXSettingsChanged = (volume, enabled) => { _combatManager?.SetSFXSettings(volume, enabled); };
            _uiManager.GetPlayerShip = () => _playerShip;
            _uiManager.GetCurrentShipClassIndex = () => _currentShipClassIndex;
            _uiManager.GetFriendlyShips = () => _friendlyShips;
            _uiManager.GetEnemyShips = () => _enemyShips;
            _uiManager.GetCameraZoom = () => _cameraZoom;
            _uiManager.GetCameraSpeed = () => CameraSpeed;
            _uiManager.GetCameraPanSpeed = () => _cameraPanSpeed;
            _uiManager.GetCameraInertia = () => _cameraInertia;
            _uiManager.SetCameraSpeed = (speed) => { CameraSpeed = speed; if (_cameraController != null) _cameraController.CameraSpeed = speed; };
            _uiManager.SetCameraPanSpeed = (speed) => { _cameraPanSpeed = speed; if (_cameraController != null) _cameraController.PanSpeed = speed; };
            _uiManager.SetCameraInertia = (inertia) => { _cameraInertia = inertia; if (_cameraController != null) _cameraController.Inertia = inertia; };
            _uiManager.SetCameraZoom = (zoom) => { _cameraZoom = zoom; if (_cameraController != null) _cameraController.Zoom = zoom; };
            _uiManager.GetBackgroundMusicInstance = () => _backgroundMusicInstance;
            _uiManager.GetShipFlySound = () => _shipFlySound;
            _uiManager.GetShipIdleSound = () => _shipIdleSound;
            _uiManager.GridPixelTexture = _gridPixelTexture;
            
            // Initialize UI
            _uiManager.Initialize(_playerShip, _cameraZoom, CameraSpeed, _cameraPanSpeed, _cameraInertia);
            
            // Load saved settings after UI is initialized
            _settingsManager?.LoadSettings();
            
            // Load panel/UI settings
            bool uiVisible = _uiVisible;
            bool behaviorTextVisible = _behaviorTextVisible;
            bool pathfindingGridVisible = _pathfindingGridVisible;
            bool gridVisible = _gridVisible;
            _settingsManager?.LoadPanelSettings(ref uiVisible, ref behaviorTextVisible, ref pathfindingGridVisible, ref gridVisible);
            _uiVisible = uiVisible;
            _behaviorTextVisible = behaviorTextVisible;
            _pathfindingGridVisible = pathfindingGridVisible;
            _gridVisible = gridVisible;
            
            // Load current ship class settings
            float avoidanceDetectionRange = _avoidanceDetectionRange;
            float shipIdleRate = _shipIdleRate;
            _settingsManager?.LoadCurrentShipSettings(ref avoidanceDetectionRange, ref shipIdleRate);
            _avoidanceDetectionRange = avoidanceDetectionRange;
            _shipIdleRate = shipIdleRate;
            if (_uiManager != null)
            {
                _uiManager.AvoidanceDetectionRange = avoidanceDetectionRange;
                _uiManager.ShipIdleRate = shipIdleRate;
            }
            
            // Sync look-ahead visibility to all friendly ships after settings are loaded
                if (_playerShip != null)
                    {
                        foreach (var friendlyShip in _friendlyShips)
                        {
                    friendlyShip.LookAheadVisible = _playerShip.LookAheadVisible;
                }
            }
            
            // Update UI for current ship class
            _uiManager?.UpdateUIForShipClass(_playerShip);
            
            // Initialize Input Manager
            _inputManager = new InputManager();
            _inputManager.ScreenToWorld = (screenPos) =>
            {
                if (_cameraController != null)
                {
                    return _cameraController.ScreenToWorld(screenPos, GraphicsDevice.Viewport);
                }
                var worldX = (screenPos.X - GraphicsDevice.Viewport.Width / 2f) / _cameraZoom + _cameraPosition.X;
                var worldY = (screenPos.Y - GraphicsDevice.Viewport.Height / 2f) / _cameraZoom + _cameraPosition.Y;
                return new Vector2(worldX, worldY);
            };
            
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
                _pathfindingManager?.Grid,
                _combatManager,
                GetOrCreateShipState,
                GetOrCreateEnemyShipState,
                _shipIdleRate
            );
            
            // Set up CameraController callbacks (after InputManager is initialized)
            if (_cameraController != null && _inputManager != null)
            {
                _cameraController.GetPlayerPosition = () => _playerShip?.Position;
                _cameraController.IsWASDPressed = () => 
                    (_inputManager.IsKeyDown(Keys.W) || _inputManager.IsKeyDown(Keys.A) || 
                     _inputManager.IsKeyDown(Keys.S) || _inputManager.IsKeyDown(Keys.D));
                _cameraController.IsKeyJustPressed = () => _inputManager.IsKeyJustPressed(Keys.Space);
                _cameraController.IsKeyDown = (key) => _inputManager.IsKeyDown(key);
                _cameraController.GetScrollDelta = () => _inputManager.GetScrollDelta();
            }
        }
        

        public override void Update(GameTime gameTime)
        {
            // Update input manager
            _inputManager?.Update();
            var keyboardState = _inputManager?.CurrentKeyboardState ?? Keyboard.GetState();
            var mouseState = _inputManager?.CurrentMouseState ?? Mouse.GetState();
            
            // If game is paused (F12 grid mode), only update UI and input, skip game logic
            if (_gamePaused)
            {
                // Update Myra input for UI interaction
                _uiManager?.Desktop?.UpdateInput();
                _uiManager?.SaveButtonDesktop?.UpdateInput();
                
                // Update mouse coordinates when grid is visible (handled by UI manager)
                var uiGridVisible = _uiManager?.UIGridVisible ?? false;
                
                // Toggle UI grid overlay and pause game with F12
                if (_inputManager?.IsKeyJustPressed(Keys.F12) == true)
                {
                    if (_uiManager != null)
                    {
                        _uiManager.UIGridVisible = !_uiManager.UIGridVisible;
                    }
                    _gamePaused = _uiManager?.UIGridVisible ?? false; // Pause when grid is shown, unpause when hidden
                }
                
                _inputManager?.SaveState();
                _previousKeyboardState = keyboardState;
                _previousMouseState = mouseState;
                return; // Skip all game logic when paused
            }
            
            // Toggle preview with P key
            if (_inputManager?.IsKeyJustPressed(Keys.P) == true)
            {
                if (_uiManager != null)
                {
                    _uiManager.IsPreviewActive = !_uiManager.IsPreviewActive;
                    if (_uiManager.PreviewDesktop?.Root != null)
                    {
                        _uiManager.PreviewDesktop.Root.Visible = _uiManager.IsPreviewActive;
                }
                }
                if (_uiManager?.IsPreviewActive == true)
                {
                    // Sync preview index with current ship class when opening
                    if (_uiManager != null)
                    {
                        _uiManager.PreviewShipIndex = _currentShipClassIndex;
                    }
                    UpdatePreviewShipLabel();
                }
            }
            
            // Arrow keys to switch ships in preview
            if (_uiManager?.IsPreviewActive == true)
            {
                if (_inputManager?.IsKeyJustPressed(Keys.Left) == true)
                {
                    if (_uiManager != null)
                    {
                        var previewIndex = _uiManager.PreviewShipIndex - 1;
                        if (previewIndex < 0)
                            previewIndex = 2; // Wrap to EnemyShip
                        _uiManager.PreviewShipIndex = previewIndex;
                    UpdatePreviewShipLabel();
                    // Switch ship class to match preview
                        SwitchShipClass(previewIndex);
                    }
                }
                if (_inputManager?.IsKeyJustPressed(Keys.Right) == true)
                {
                    if (_uiManager != null)
                    {
                        var previewIndex = _uiManager.PreviewShipIndex + 1;
                        if (previewIndex > 2)
                            previewIndex = 0; // Wrap to PlayerShip
                        _uiManager.PreviewShipIndex = previewIndex;
                    UpdatePreviewShipLabel();
                    // Switch ship class to match preview
                        SwitchShipClass(previewIndex);
                    }
                }
            }
            
            // Toggle behavior text and UI panels with U key - check before preview mode
            if (_inputManager?.IsKeyJustPressed(Keys.U) == true)
            {
                _behaviorTextVisible = !_behaviorTextVisible;
                _uiVisible = !_uiVisible;
                
                // Toggle main UI panel visibility
                if (_uiManager?.Desktop?.Root != null)
                {
                    _uiManager.Desktop.Root.Visible = _uiVisible;
                }
                
                // Toggle save button panel visibility
                if (_uiManager?.SaveButtonDesktop?.Root != null)
                {
                    _uiManager.SaveButtonDesktop.Root.Visible = _uiVisible;
                }
                
                _settingsManager?.SavePanelSettings(); // Auto-save panel settings
                System.Console.WriteLine($"[U KEY] UI Panels: {(_uiVisible ? "ON" : "OFF")}, Behavior Text: {(_behaviorTextVisible ? "ON" : "OFF")}");
            }
            
            // If preview is active, only update preview UI, don't update game
            if (_uiManager?.IsPreviewActive == true)
            {
                _uiManager?.PreviewDesktop?.UpdateInput();
                
                // Get the currently previewed ship texture
                var previewIndex = _uiManager?.PreviewShipIndex ?? 0;
                Texture2D? shipTexture = previewIndex == 0 ? _uiManager?.PreviewShip1Texture : (previewIndex == 1 ? _uiManager?.PreviewShip2Texture : _uiManager?.PreviewShip1Texture);
                    
                if (shipTexture != null)
                {
                    
                    // Calculate preview panel position (centered)
                    int panelX = (GraphicsDevice.Viewport.Width - 500) / 2;
                    int panelY = (GraphicsDevice.Viewport.Height - 500) / 2;
                    
                    // Sprite position in preview (centered in panel)
                    int spriteX = panelX + 250 - (shipTexture?.Width ?? 0) / 2;
                    int spriteY = panelY + 250 - (shipTexture?.Height ?? 0) / 2;
                    
                    // Check if mouse is over sprite
                    var mousePos = _inputManager?.MouseScreenPosition ?? Vector2.Zero;
                    if (shipTexture != null && mousePos.X >= spriteX && mousePos.X < spriteX + shipTexture.Width &&
                        mousePos.Y >= spriteY && mousePos.Y < spriteY + shipTexture.Height)
                    {
                        // Calculate texture coordinates
                        int texX = (int)(mousePos.X - spriteX);
                        int texY = (int)(mousePos.Y - spriteY);
                        // Preview coordinate label is managed by UI manager
                    }
                    else
                    {
                        // Preview coordinate label is managed by UI manager
                    }
                }
                
                _inputManager?.SaveState();
                _previousKeyboardState = keyboardState;
                return; // Don't update game logic when preview is active
            }
            
            // Update audio (music restart and ship sounds)
            _audioManager?.UpdateMusic();
            _audioManager?.UpdateShipSounds(_playerShip);
            
            var deltaTime = (float)gameTime.ElapsedGameTime.TotalSeconds;
            
            // Check if mouse cursor is within the game window bounds
            bool isMouseInWindow = _inputManager?.IsMouseInWindow(GraphicsDevice.Viewport) ?? true;
            
            // Only process mouse input if cursor is within window
            if (!isMouseInWindow)
            {
                _inputManager?.SaveState();
                _previousMouseState = mouseState;
                _previousKeyboardState = keyboardState;
                return;
            }
            
            // Check if mouse is over UI before processing player movement
            bool isMouseOverAnyUI = _inputManager?.IsMouseOverUI(GraphicsDevice.Viewport, _uiVisible) ?? false;
            
            // Update mouse following state
            _inputManager?.UpdateMouseFollowing(isMouseOverAnyUI);
            _isFollowingMouse = _inputManager?.IsFollowingMouse ?? false;
            
            // If mouse is over UI, stop any following movement immediately
            if (isMouseOverAnyUI && _isFollowingMouse)
            {
                if (_playerShip != null)
                {
                    _playerShip.StopMoving();
                }
                _inputManager?.ResetMouseFollowing();
                _isFollowingMouse = false;
            }
            
            // Left mouse button handling - click to move or hold to follow
            // Only process if mouse is NOT over UI (including save button)
            if (!isMouseOverAnyUI && _inputManager?.IsLeftButtonPressed == true)
            {
                var worldPosition = _inputManager.MouseWorldPosition;
                
                if (_isFollowingMouse)
                {
                    // Following mode - continuously update target
                    if (_playerShip != null)
                    {
                        _playerShip.SetTargetPosition(worldPosition);
                    }
                }
            }
            else if (!isMouseOverAnyUI && _inputManager?.WasLeftButtonJustReleased == true)
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
                    var worldPosition = _inputManager?.ConvertScreenToWorld(screenPos, GraphicsDevice.Viewport, _cameraZoom, _cameraPosition) ?? Vector2.Zero;
                    
                    if (_playerShip != null)
                    {
                        _playerShip.SetTargetPosition(worldPosition);
                    }
                }
                _inputManager?.ResetMouseFollowing();
                _isFollowingMouse = false;
            }
            
            _wasLeftButtonPressed = _inputManager?.IsLeftButtonPressed ?? false;
            
            // Right mouse button to fire lasers
            if (_inputManager?.WasRightButtonJustPressed == true && !isMouseOverAnyUI)
            {
                // Fire lasers from player ship positions in the direction of the cursor
                if (_playerShip != null)
                {
                    // Convert mouse position to world coordinates
                    var mouseWorldPos = _inputManager?.MouseWorldPosition ?? Vector2.Zero;
                    
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
            
            // Toggle grid with G key
            if (_inputManager?.IsKeyJustPressed(Keys.G) == true)
            {
                _gridVisible = !_gridVisible;
                // Grid visibility is managed by UI manager
                if (_uiManager != null)
                {
                    _uiManager.GridVisible = _gridVisible;
                }
            }
            
            // Toggle A* pathfinding grid with F11
            if (_inputManager?.IsKeyJustPressed(Keys.F11) == true)
            {
                _pathfindingGridVisible = !_pathfindingGridVisible;
                _settingsManager?.SavePanelSettings(); // Auto-save panel settings
                System.Console.WriteLine($"A* Pathfinding Grid: {(_pathfindingGridVisible ? "ON" : "OFF")}");
            }
            
            // Toggle world grid with F10
            if (_inputManager?.IsKeyJustPressed(Keys.F10) == true)
            {
                _gridVisible = !_gridVisible;
                // Grid visibility is managed by UI manager
                if (_uiManager != null)
                {
                    _uiManager.GridVisible = _gridVisible;
                }
                _settingsManager?.SavePanelSettings(); // Auto-save panel settings
            }
            
            // Toggle UI grid overlay and pause game with F12
            if (_inputManager?.IsKeyJustPressed(Keys.F12) == true)
            {
                _uiGridVisible = !_uiGridVisible;
                _gamePaused = _uiGridVisible; // Pause when grid is shown, unpause when hidden
                // Show/hide coordinate label
                // Mouse coordinate label visibility is managed by UI manager
            }
            
            
            // Update player ship aim target (mouse cursor position - always allow aiming, regardless of camera state)
            if (_playerShip != null)
            {
                // Convert mouse position to world coordinates for aiming
                var mouseWorldPos = _inputManager?.MouseWorldPosition ?? Vector2.Zero;
                _playerShip.SetAimTarget(mouseWorldPos);
            }
            
            // Update player ship first so we have current position
            _playerShip?.Update(gameTime);
            
            // Update camera (handles zoom, WASD movement, panning, following)
            _cameraController?.Update(gameTime, GraphicsDevice.Viewport, MapSize);
            
            // Sync camera state back to GameScene for compatibility
            if (_cameraController != null)
            {
                _cameraPosition = _cameraController.Position;
                _cameraZoom = _cameraController.Zoom;
                _isPanningToPlayer = _cameraController.IsPanningToPlayer;
                _cameraFollowingPlayer = _cameraController.FollowingPlayer;
                _cameraVelocity = _cameraController.Velocity;
            }
            
            // Check if camera is panning (WASD pressed, panning to player, or camera has velocity from manual control)
            // Note: This is only used for camera movement, not for ship behavior
            bool isWASDPressed = _inputManager?.IsKeyDown(Keys.W) == true || _inputManager?.IsKeyDown(Keys.A) == true || 
                                 _inputManager?.IsKeyDown(Keys.S) == true || _inputManager?.IsKeyDown(Keys.D) == true;
            bool isCameraPanning = _isPanningToPlayer || isWASDPressed || _cameraVelocity.LengthSquared() > 1f || !_cameraFollowingPlayer;
            
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
                if (_pathfindingManager != null && friendlyShip.IsActivelyMoving())
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
                    float obstacleRadius = friendlyShip.AvoidanceDetectionRange * 0.5f;
                    _pathfindingManager.UpdateObstacles(_friendlyShips, _playerShip, obstacleRadius);
                    
                    // Calculate A* path if needed
                    if (needsNewPath)
                    {
                        var path = _pathfindingManager.FindPath(currentPos, currentTarget);
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
                if (_uiManager?.EnemyPathVisible == true)
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
                    float randomAngle = (float)((_random?.NextDouble() ?? 0.5) * MathHelper.PiOver2 - MathHelper.PiOver4);
                    float cos = (float)Math.Cos(randomAngle);
                    float sin = (float)Math.Sin(randomAngle);
                    Vector2 rotatedDir = new Vector2(
                        awayFromEdge.X * cos - awayFromEdge.Y * sin,
                        awayFromEdge.X * sin + awayFromEdge.Y * cos
                    );
                    
                    // Set target 1000-1500 pixels away from current position (ensures ships can turn smoothly)
                    float targetDistance = (float)((_random?.NextDouble() ?? 0.5) * 500f + 1000f);
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
            
            // Update save confirmation timer (handled by UI manager)
            
            // Update Myra UI input
            _uiManager?.Desktop?.UpdateInput();
            
            // Return to menu on Escape
            if (keyboardState.IsKeyDown(Keys.Escape))
            {
                var sceneManager = (SceneManager)Game.Services.GetService(typeof(SceneManager));
                sceneManager?.ChangeScene(new MainMenuScene(Game));
            }
            
            // Save input state for next frame
            _inputManager?.SaveState();
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
            if (_pathfindingGridVisible && _pathfindingManager?.Grid != null && _gridPixelTexture != null)
            {
                // Update grid with current obstacles for visualization
                UpdatePathfindingGridForVisualization();
                _renderingManager?.DrawPathfindingGrid(spriteBatch, GraphicsDevice.Viewport, _pathfindingManager.Grid, _cameraPosition, _cameraZoom);
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
            if (_uiManager?.AvoidanceRangeVisible == true)
            {
                _renderingManager?.DrawAvoidanceRange(spriteBatch, _playerShip, _friendlyShips);
            }
            
            // Draw player ship
            _playerShip?.Draw(spriteBatch);
            
            // Draw ship paths if enabled
            if (_uiManager?.EnemyPathVisible == true)
            {
                _renderingManager?.DrawEnemyPaths(spriteBatch, _friendlyShips, _friendlyShipStates, GetOrCreateShipState);
            }
            
            // Draw enemy target paths if enabled
            if (_uiManager?.EnemyTargetPathVisible == true)
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
            if (_minimapManager?.MinimapVisible ?? true)
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
                _uiManager?.Desktop?.Render();
                _uiManager?.SaveButtonDesktop?.Render();
            }
            
            // Update coordinate label position in Draw to ensure it's always updated
            var uiGridVisible = _uiManager?.UIGridVisible ?? false;
            if (uiGridVisible && _uiManager?.MouseCoordinateLabel != null)
            {
                var mouseState = Mouse.GetState();
                var mouseX = mouseState.X;
                var mouseY = mouseState.Y;
                
                // Calculate snapped grid point
                int snappedX = (mouseX / UIGridSize) * UIGridSize;
                int snappedY = (mouseY / UIGridSize) * UIGridSize;
                
                // Update coordinate label position to follow mouse (above cursor)
                // Mouse coordinate label is managed by UI manager
                // Render coordinate desktop after main UI so it appears on top
                if (_uiManager?.CoordinateDesktop != null)
                {
                    _uiManager.CoordinateDesktop.Render();
                }
            }
            
            // Draw preview screen if active
            var previewIndex = _uiManager?.PreviewShipIndex ?? 0;
            Texture2D? shipTexture = previewIndex == 0 ? _uiManager?.PreviewShip1Texture : _uiManager?.PreviewShip2Texture;
                
            if (_uiManager?.IsPreviewActive == true && shipTexture != null && _uiManager?.PreviewDesktop != null)
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
                _uiManager?.PreviewDesktop?.Render();
            }
        }
        
        private void UpdatePreviewShipLabel()
        {
            var previewIndex = _uiManager?.PreviewShipIndex ?? 0;
            string className = previewIndex == 0 ? "PlayerShip" : (previewIndex == 1 ? "FriendlyShip" : "EnemyShip");
            // Preview ship label is managed by UI manager
        }
        
        private void SwitchShipClass(int classIndex)
        {
            _shipManager?.SwitchShipClass(classIndex);
            // Sync player ship back from ShipManager
            if (_shipManager != null)
            {
                _playerShip = _shipManager.PlayerShip;
            }
        }
        
        
        private void UpdatePathfindingGridForVisualization()
        {
            if (_pathfindingManager == null) return;
            
            // Use a standard obstacle radius for visualization
            float obstacleRadius = 150f; // Standard radius for visualization
            
            // Update obstacles for visualization
            _pathfindingManager.UpdateObstacles(_friendlyShips, _playerShip, obstacleRadius);
        }
        
        // DrawUIGrid, DrawPathfindingGrid, and DrawMinimap methods moved to RenderingManager
        
        public override void UnloadContent()
        {
            // Dispose audio resources
            _audioManager?.Dispose();
            _backgroundMusicInstance = null;
            _shipIdleSound = null;
            _shipFlySound = null;
            base.UnloadContent();
        }
        
        // DrawAvoidanceRange, DrawEnemyPaths, and DrawEnemyTargetPaths methods moved to RenderingManager
        
        // DrawLookAheadLines, DrawCircle, DrawHealthBars, and DrawHealthBarForShip methods moved to RenderingManager
    }
    
    // A* Pathfinding System moved to Managers/PathfindingManager.cs
}

