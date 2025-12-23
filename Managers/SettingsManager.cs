using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Audio;
using Myra.Graphics2D.UI;
using Planet9.Entities;
using Planet9.Scenes;

namespace Planet9.Managers
{
    /// <summary>
    /// Manages loading and saving of game settings
    /// </summary>
    public class SettingsManager
    {
        // Callbacks for interacting with GameScene
        public Func<int>? GetCurrentShipClassIndex { get; set; }
        public Action<int>? SwitchShipClass { get; set; }
        public Func<float>? GetCameraSpeed { get; set; }
        public Action<float>? SetCameraSpeed { get; set; }
        public Func<float>? GetCameraZoom { get; set; }
        public Action<float>? SetCameraZoom { get; set; }
        public Func<float>? GetCameraPanSpeed { get; set; }
        public Action<float>? SetCameraPanSpeed { get; set; }
        public Func<float>? GetCameraInertia { get; set; }
        public Action<float>? SetCameraInertia { get; set; }
        public Func<int>? GetGridSize { get; set; }
        public Action<int>? SetGridSize { get; set; }
        public Func<PlayerShip?>? GetPlayerShip { get; set; }
        public Func<List<FriendlyShip>>? GetFriendlyShips { get; set; }
        public Func<List<EnemyShip>>? GetEnemyShips { get; set; }
        public Func<float>? GetAvoidanceDetectionRange { get; set; }
        public Action<float>? SetAvoidanceDetectionRange { get; set; }
        public Func<float>? GetShipIdleRate { get; set; }
        public Action<float>? SetShipIdleRate { get; set; }
        
        // UI Manager callbacks
        public Func<float>? GetMusicVolume { get; set; }
        public Action<float>? SetMusicVolume { get; set; }
        public Func<float>? GetSFXVolume { get; set; }
        public Action<float>? SetSFXVolume { get; set; }
        public Func<bool>? GetMusicEnabled { get; set; }
        public Action<bool>? SetMusicEnabled { get; set; }
        public Func<bool>? GetSFXEnabled { get; set; }
        public Action<bool>? SetSFXEnabled { get; set; }
        public Func<bool>? GetUIVisible { get; set; }
        public Action<bool>? SetUIVisible { get; set; }
        public Func<bool>? GetBehaviorTextVisible { get; set; }
        public Action<bool>? SetBehaviorTextVisible { get; set; }
        public Func<bool>? GetEnemyPathVisible { get; set; }
        public Action<bool>? SetEnemyPathVisible { get; set; }
        public Func<bool>? GetEnemyTargetPathVisible { get; set; }
        public Action<bool>? SetEnemyTargetPathVisible { get; set; }
        public Func<bool>? GetAvoidanceRangeVisible { get; set; }
        public Action<bool>? SetAvoidanceRangeVisible { get; set; }
        public Func<bool>? GetPathfindingGridVisible { get; set; }
        public Action<bool>? SetPathfindingGridVisible { get; set; }
        public Func<bool>? GetGridVisible { get; set; }
        public Action<bool>? SetGridVisible { get; set; }
        public Func<bool>? GetMinimapVisible { get; set; }
        public Action<bool>? SetMinimapVisible { get; set; }
        public Action<int>? SetUIGridSize { get; set; }
        public Action<float>? SetUIAvoidanceDetectionRange { get; set; }
        public Action<float>? SetUIShipIdleRate { get; set; }
        
        // Audio callbacks
        public Func<SoundEffectInstance?>? GetBackgroundMusicInstance { get; set; }
        public Func<SoundEffectInstance?>? GetShipFlySound { get; set; }
        public Func<SoundEffectInstance?>? GetShipIdleSound { get; set; }
        public Action<float, bool>? SetCombatSFXSettings { get; set; }
        
        // UI Desktop callbacks
        public Func<Desktop?>? GetUIDesktop { get; set; }
        public Func<Desktop?>? GetSaveButtonDesktop { get; set; }
        
        private const float MinZoom = 0.40f;
        private const float MaxZoom = 1.10f;
        
        /// <summary>
        /// Load general settings from settings.json
        /// </summary>
        public void LoadSettings()
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
                        var currentIndex = GetCurrentShipClassIndex?.Invoke() ?? 0;
                        if (shipClassIndex != currentIndex)
                        {
                            SwitchShipClass?.Invoke(shipClassIndex);
                        }
                    }
                    
                    // Load camera speed
                    if (settings.TryGetProperty("CameraSpeed", out var cameraSpeedElement))
                    {
                        var cameraSpeed = cameraSpeedElement.GetSingle();
                        System.Console.WriteLine($"Loading CameraSpeed: {cameraSpeed}");
                        SetCameraSpeed?.Invoke(cameraSpeed);
                    }
                    
                    // Load zoom
                    if (settings.TryGetProperty("Zoom", out var zoomElement))
                    {
                        var zoom = zoomElement.GetSingle();
                        System.Console.WriteLine($"Loading Zoom: {zoom}");
                        var clampedZoom = MathHelper.Clamp(zoom, MinZoom, MaxZoom);
                        SetCameraZoom?.Invoke(clampedZoom);
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
                        SetGridSize?.Invoke(closestSize);
                        SetUIGridSize?.Invoke(closestSize);
                    }
                    
                    // Load pan speed
                    if (settings.TryGetProperty("PanSpeed", out var panSpeedElement))
                    {
                        var panSpeed = panSpeedElement.GetSingle();
                        System.Console.WriteLine($"Loading PanSpeed: {panSpeed}");
                        var clampedPanSpeed = MathHelper.Clamp(panSpeed, 200f, 2000f);
                        SetCameraPanSpeed?.Invoke(clampedPanSpeed);
                    }
                    
                    // Load camera inertia
                    if (settings.TryGetProperty("CameraInertia", out var cameraInertiaElement))
                    {
                        var cameraInertia = cameraInertiaElement.GetSingle();
                        System.Console.WriteLine($"Loading CameraInertia: {cameraInertia}");
                        var clampedInertia = MathHelper.Clamp(cameraInertia, 0f, 0.995f);
                        SetCameraInertia?.Invoke(clampedInertia);
                    }
                    
                    // Load music volume
                    if (settings.TryGetProperty("MusicVolume", out var musicVolumeElement))
                    {
                        var musicVolume = musicVolumeElement.GetSingle();
                        System.Console.WriteLine($"Loading MusicVolume: {musicVolume}");
                        var clampedVolume = MathHelper.Clamp(musicVolume, 0f, 1f);
                        SetMusicVolume?.Invoke(clampedVolume);
                        // Apply to background music instance
                        var musicInstance = GetBackgroundMusicInstance?.Invoke();
                        if (musicInstance != null)
                        {
                            musicInstance.Volume = clampedVolume;
                        }
                    }
                    
                    // Load SFX volume
                    if (settings.TryGetProperty("SFXVolume", out var sfxVolumeElement))
                    {
                        var sfxVolume = sfxVolumeElement.GetSingle();
                        System.Console.WriteLine($"Loading SFXVolume: {sfxVolume}");
                        var clampedVolume = MathHelper.Clamp(sfxVolume, 0f, 1f);
                        var sfxEnabled = GetSFXEnabled?.Invoke() ?? true;
                        SetSFXVolume?.Invoke(clampedVolume);
                        SetCombatSFXSettings?.Invoke(clampedVolume, sfxEnabled);
                        // Apply to SFX instances (only if enabled)
                        if (sfxEnabled)
                        {
                            var shipIdleSound = GetShipIdleSound?.Invoke();
                            if (shipIdleSound != null)
                            {
                                shipIdleSound.Volume = clampedVolume;
                            }
                            var shipFlySound = GetShipFlySound?.Invoke();
                            if (shipFlySound != null)
                            {
                                shipFlySound.Volume = clampedVolume * 0.8f; // 20% lower than SFX volume
                            }
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
        
        /// <summary>
        /// Save general settings to settings.json
        /// </summary>
        public void SaveSettings(Action? saveCurrentShipSettings, Action? savePanelSettings)
        {
            try
            {
                // Save current ship class settings
                saveCurrentShipSettings?.Invoke();
                
                // Save panel/UI settings to separate file
                savePanelSettings?.Invoke();
                
                // Save general settings (camera, etc.)
                var settings = new
                {
                    CurrentShipClass = GetCurrentShipClassIndex?.Invoke() ?? 0,
                    CameraSpeed = GetCameraSpeed?.Invoke() ?? 200f,
                    Zoom = GetCameraZoom?.Invoke() ?? 0.40f,
                    GridSize = GetGridSize?.Invoke() ?? 128,
                    PanSpeed = GetCameraPanSpeed?.Invoke() ?? 800f,
                    CameraInertia = GetCameraInertia?.Invoke() ?? 0.85f,
                    MusicVolume = GetMusicVolume?.Invoke() ?? 0.5f,
                    SFXVolume = GetSFXVolume?.Invoke() ?? 1.0f
                };
                
                var json = JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true });
                var filePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "settings.json");
                System.Console.WriteLine($"Saving settings to: {filePath}");
                System.Console.WriteLine($"Settings JSON: {json}");
                File.WriteAllText(filePath, json);
                System.Console.WriteLine($"Settings file written successfully. File exists: {File.Exists(filePath)}");
                
                System.Console.WriteLine("Settings saved successfully!");
            }
            catch (Exception ex)
            {
                System.Console.WriteLine($"Failed to save settings: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Save panel/UI settings to settings_Panel.json
        /// </summary>
        public void SavePanelSettings()
        {
            try
            {
                var panelSettings = new
                {
                    UIVisible = GetUIVisible?.Invoke() ?? true,
                    BehaviorTextVisible = GetBehaviorTextVisible?.Invoke() ?? true,
                    EnemyPathVisible = GetEnemyPathVisible?.Invoke() ?? false,
                    EnemyTargetPathVisible = GetEnemyTargetPathVisible?.Invoke() ?? false,
                    AvoidanceRangeVisible = GetAvoidanceRangeVisible?.Invoke() ?? false,
                    PathfindingGridVisible = GetPathfindingGridVisible?.Invoke() ?? false,
                    GridVisible = GetGridVisible?.Invoke() ?? false,
                    MinimapVisible = GetMinimapVisible?.Invoke() ?? true,
                    MusicEnabled = GetMusicEnabled?.Invoke() ?? true,
                    SFXEnabled = GetSFXEnabled?.Invoke() ?? true
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
        
        /// <summary>
        /// Load panel/UI settings from settings_Panel.json
        /// </summary>
        public void LoadPanelSettings(ref bool uiVisible, ref bool behaviorTextVisible, ref bool pathfindingGridVisible, ref bool gridVisible)
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
                        uiVisible = uiVisibleElement.GetBoolean();
                        SetUIVisible?.Invoke(uiVisible);
                        var desktop = GetUIDesktop?.Invoke();
                        if (desktop?.Root != null)
                        {
                            desktop.Root.Visible = uiVisible;
                        }
                        var saveButtonDesktop = GetSaveButtonDesktop?.Invoke();
                        if (saveButtonDesktop?.Root != null)
                        {
                            saveButtonDesktop.Root.Visible = uiVisible;
                        }
                    }
                    
                    // Load behavior text visibility
                    if (settings.TryGetProperty("BehaviorTextVisible", out var behaviorTextVisibleElement))
                    {
                        behaviorTextVisible = behaviorTextVisibleElement.GetBoolean();
                        SetBehaviorTextVisible?.Invoke(behaviorTextVisible);
                    }
                    
                    // Load enemy path visibility
                    if (settings.TryGetProperty("EnemyPathVisible", out var enemyPathVisibleElement))
                    {
                        var enemyPathVisible = enemyPathVisibleElement.GetBoolean();
                        SetEnemyPathVisible?.Invoke(enemyPathVisible);
                    }
                    
                    // Load enemy target path visibility
                    if (settings.TryGetProperty("EnemyTargetPathVisible", out var enemyTargetPathVisibleElement))
                    {
                        var enemyTargetPathVisible = enemyTargetPathVisibleElement.GetBoolean();
                        SetEnemyTargetPathVisible?.Invoke(enemyTargetPathVisible);
                    }
                    
                    // Load avoidance range visibility
                    if (settings.TryGetProperty("AvoidanceRangeVisible", out var avoidanceRangeVisibleElement))
                    {
                        var avoidanceRangeVisible = avoidanceRangeVisibleElement.GetBoolean();
                        SetAvoidanceRangeVisible?.Invoke(avoidanceRangeVisible);
                    }
                    
                    // Load pathfinding grid visibility
                    if (settings.TryGetProperty("PathfindingGridVisible", out var pathfindingGridVisibleElement))
                    {
                        pathfindingGridVisible = pathfindingGridVisibleElement.GetBoolean();
                        SetPathfindingGridVisible?.Invoke(pathfindingGridVisible);
                    }
                    
                    // Load grid visibility
                    if (settings.TryGetProperty("GridVisible", out var gridVisibleElement))
                    {
                        gridVisible = gridVisibleElement.GetBoolean();
                        SetGridVisible?.Invoke(gridVisible);
                    }
                    
                    // Load minimap visibility
                    if (settings.TryGetProperty("MinimapVisible", out var minimapVisibleElement))
                    {
                        var minimapVisible = minimapVisibleElement.GetBoolean();
                        SetMinimapVisible?.Invoke(minimapVisible);
                    }
                    
                    // Load music enabled
                    if (settings.TryGetProperty("MusicEnabled", out var musicEnabledElement))
                    {
                        var musicEnabled = musicEnabledElement.GetBoolean();
                        SetMusicEnabled?.Invoke(musicEnabled);
                        // Apply music state
                        var musicInstance = GetBackgroundMusicInstance?.Invoke();
                        if (musicInstance != null)
                        {
                            if (musicEnabled)
                            {
                                var musicVolume = GetMusicVolume?.Invoke() ?? 0.5f;
                                musicInstance.Volume = musicVolume;
                                if (musicInstance.State == SoundState.Stopped)
                                {
                                    musicInstance.Play();
                                }
                            }
                            else
                            {
                                musicInstance.Volume = 0f;
                            }
                        }
                    }
                    
                    // Load SFX enabled
                    if (settings.TryGetProperty("SFXEnabled", out var sfxEnabledElement))
                    {
                        var sfxEnabledValue = sfxEnabledElement.GetBoolean();
                        var sfxVolumeValue = GetSFXVolume?.Invoke() ?? 1.0f;
                        SetSFXEnabled?.Invoke(sfxEnabledValue);
                        SetCombatSFXSettings?.Invoke(sfxVolumeValue, sfxEnabledValue);
                        // Apply SFX state
                        var shipIdleSound = GetShipIdleSound?.Invoke();
                        if (shipIdleSound != null)
                        {
                            shipIdleSound.Volume = sfxEnabledValue ? sfxVolumeValue : 0f;
                        }
                        var shipFlySound = GetShipFlySound?.Invoke();
                        if (shipFlySound != null)
                        {
                            shipFlySound.Volume = sfxEnabledValue ? sfxVolumeValue * 0.8f : 0f;
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
        
        /// <summary>
        /// Save current ship class settings to settings_{className}.json
        /// </summary>
        public void SaveCurrentShipSettings()
        {
            var playerShip = GetPlayerShip?.Invoke();
            if (playerShip == null) return;
            
            var currentShipClassIndex = GetCurrentShipClassIndex?.Invoke() ?? 0;
            string className = currentShipClassIndex == 0 ? "PlayerShip" : (currentShipClassIndex == 1 ? "FriendlyShip" : "EnemyShip");
            var filePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, $"settings_{className}.json");
            
            try
            {
                var avoidanceDetectionRange = GetAvoidanceDetectionRange?.Invoke() ?? 300f;
                var shipIdleRate = GetShipIdleRate?.Invoke() ?? 0.3f;
                
                // Only include ShipIdleRate for FriendlyShip, not PlayerShip or EnemyShip
                if (currentShipClassIndex == 0 || currentShipClassIndex == 2)
                {
                    // PlayerShip or EnemyShip settings (no ShipIdleRate)
                    var settings = new
                    {
                        ShipSpeed = playerShip.MoveSpeed,
                        TurnRate = playerShip.RotationSpeed,
                        Inertia = playerShip.Inertia,
                        AimRotationSpeed = playerShip.AimRotationSpeed,
                        Drift = playerShip.Drift,
                        AvoidanceDetectionRange = avoidanceDetectionRange,
                        LookAheadDistance = playerShip.LookAheadDistance,
                        LookAheadVisible = playerShip.LookAheadVisible
                    };
                    var json = JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true });
                    File.WriteAllText(filePath, json);
                    System.Console.WriteLine($"Saved {className} settings to: {filePath}");
                }
                else // currentShipClassIndex == 1
                {
                    // FriendlyShip settings (includes ShipIdleRate)
                    var settings = new
                    {
                        ShipSpeed = playerShip.MoveSpeed,
                        TurnRate = playerShip.RotationSpeed,
                        Inertia = playerShip.Inertia,
                        AimRotationSpeed = playerShip.AimRotationSpeed,
                        Drift = playerShip.Drift,
                        AvoidanceDetectionRange = avoidanceDetectionRange,
                        ShipIdleRate = shipIdleRate,
                        LookAheadDistance = playerShip.LookAheadDistance,
                        LookAheadVisible = playerShip.LookAheadVisible
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
        
        /// <summary>
        /// Load current ship class settings from settings_{className}.json
        /// </summary>
        public void LoadCurrentShipSettings(ref float avoidanceDetectionRange, ref float shipIdleRate)
        {
            var playerShip = GetPlayerShip?.Invoke();
            if (playerShip == null) return;
            
            var currentShipClassIndex = GetCurrentShipClassIndex?.Invoke() ?? 0;
            string className = currentShipClassIndex == 0 ? "PlayerShip" : (currentShipClassIndex == 1 ? "FriendlyShip" : "EnemyShip");
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
                        playerShip.MoveSpeed = shipSpeed;
                    }
                    
                    // Load turn rate
                    if (settings.TryGetProperty("TurnRate", out var turnRateElement))
                    {
                        var turnRate = turnRateElement.GetSingle();
                        playerShip.RotationSpeed = turnRate;
                    }
                    
                    // Load inertia
                    if (settings.TryGetProperty("Inertia", out var inertiaElement))
                    {
                        var inertia = inertiaElement.GetSingle();
                        playerShip.Inertia = inertia;
                    }
                    
                    // Load aim rotation speed
                    if (settings.TryGetProperty("AimRotationSpeed", out var aimRotationSpeedElement))
                    {
                        var aimRotationSpeed = aimRotationSpeedElement.GetSingle();
                        playerShip.AimRotationSpeed = aimRotationSpeed;
                    }
                    
                    // Load drift
                    if (settings.TryGetProperty("Drift", out var driftElement))
                    {
                        var drift = driftElement.GetSingle();
                        playerShip.Drift = drift;
                        
                        // If loading FriendlyShip settings, also update all friendly ships with their own class drift value
                        if (currentShipClassIndex == 1)
                        {
                            var friendlyShips = GetFriendlyShips?.Invoke();
                            if (friendlyShips != null)
                            {
                                foreach (var friendlyShip in friendlyShips)
                                {
                                    friendlyShip.Drift = drift;
                                }
                            }
                        }
                    }
                    
                    // Load avoidance detection range
                    if (settings.TryGetProperty("AvoidanceDetectionRange", out var avoidanceRangeElement))
                    {
                        var avoidanceRange = avoidanceRangeElement.GetSingle();
                        avoidanceDetectionRange = MathHelper.Clamp(avoidanceRange, 100f, 1000f);
                        SetAvoidanceDetectionRange?.Invoke(avoidanceDetectionRange);
                        SetUIAvoidanceDetectionRange?.Invoke(avoidanceDetectionRange);
                        
                        // Apply to ships based on current ship class
                        if (currentShipClassIndex == 1)
                        {
                            // FriendlyShip class - update all friendly ships
                            var friendlyShips = GetFriendlyShips?.Invoke();
                            if (friendlyShips != null)
                            {
                                foreach (var friendlyShip in friendlyShips)
                                {
                                    friendlyShip.AvoidanceDetectionRange = avoidanceDetectionRange;
                                }
                            }
                        }
                        else if (currentShipClassIndex == 2)
                        {
                            // EnemyShip class - update all enemy ships
                            var enemyShips = GetEnemyShips?.Invoke();
                            if (enemyShips != null)
                            {
                                foreach (var enemyShip in enemyShips)
                                {
                                    enemyShip.AvoidanceDetectionRange = avoidanceDetectionRange;
                                }
                            }
                        }
                        else
                        {
                            // PlayerShip class - update player ship
                            playerShip.AvoidanceDetectionRange = avoidanceDetectionRange;
                        }
                    }
                    
                    // Load ship idle rate
                    if (settings.TryGetProperty("ShipIdleRate", out var shipIdleRateElement))
                    {
                        var idleRate = shipIdleRateElement.GetSingle();
                        shipIdleRate = MathHelper.Clamp(idleRate, 0f, 1f);
                        SetShipIdleRate?.Invoke(shipIdleRate);
                        SetUIShipIdleRate?.Invoke(shipIdleRate);
                    }
                    
                    // Load look-ahead distance
                    if (settings.TryGetProperty("LookAheadDistance", out var lookAheadDistanceElement))
                    {
                        var lookAheadDistance = lookAheadDistanceElement.GetSingle();
                        playerShip.LookAheadDistance = MathHelper.Clamp(lookAheadDistance, 0.5f, 5.0f);
                        
                        // If loading FriendlyShip or EnemyShip settings, also update all ships of that class
                        if (currentShipClassIndex == 1)
                        {
                            var friendlyShips = GetFriendlyShips?.Invoke();
                            if (friendlyShips != null)
                            {
                                foreach (var friendlyShip in friendlyShips)
                                {
                                    friendlyShip.LookAheadDistance = playerShip.LookAheadDistance;
                                }
                            }
                        }
                        else if (currentShipClassIndex == 2)
                        {
                            var enemyShips = GetEnemyShips?.Invoke();
                            if (enemyShips != null)
                            {
                                foreach (var enemyShip in enemyShips)
                                {
                                    enemyShip.LookAheadDistance = playerShip.LookAheadDistance;
                                }
                            }
                        }
                    }
                    
                    // Load look-ahead visible
                    if (settings.TryGetProperty("LookAheadVisible", out var lookAheadVisibleElement))
                    {
                        var lookAheadVisible = lookAheadVisibleElement.GetBoolean();
                        playerShip.LookAheadVisible = lookAheadVisible;
                        
                        // Update all ships of the current class with the checkbox state
                        if (currentShipClassIndex == 1)
                        {
                            var friendlyShips = GetFriendlyShips?.Invoke();
                            if (friendlyShips != null)
                            {
                                foreach (var friendlyShip in friendlyShips)
                                {
                                    friendlyShip.LookAheadVisible = lookAheadVisible;
                                }
                            }
                        }
                        else if (currentShipClassIndex == 2)
                        {
                            var enemyShips = GetEnemyShips?.Invoke();
                            if (enemyShips != null)
                            {
                                foreach (var enemyShip in enemyShips)
                                {
                                    enemyShip.LookAheadVisible = lookAheadVisible;
                                }
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
    }
}

