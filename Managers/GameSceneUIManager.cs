using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Audio;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Myra.Graphics2D.UI;
using Planet9.Entities;

namespace Planet9.Managers
{
    /// <summary>
    /// Manages all UI elements for GameScene including sliders, labels, buttons, and settings
    /// </summary>
    public class GameSceneUIManager
    {
        // UI Desktops
        private Desktop? _desktop;
        private Desktop? _saveButtonDesktop;
        private Desktop? _coordinateDesktop;
        private Desktop? _previewDesktop;
        
        // Main UI Labels
        private Label? _zoomLabel;
        private Label? _speedLabel;
        private Label? _turnRateLabel;
        private Label? _cameraSpeedLabel;
        private Label? _panSpeedLabel;
        private Label? _inertiaLabel;
        private Label? _driftLabel;
        private Label? _avoidanceRangeLabel;
        private Label? _shipIdleRateLabel;
        private Label? _lookAheadLabel;
        private Label? _cameraInertiaLabel;
        private Label? _aimRotationSpeedLabel;
        private Label? _musicVolumeLabel;
        private Label? _sfxVolumeLabel;
        private Label? _gridSizeLabel;
        private Label? _mouseCoordinateLabel;
        private Label? _saveConfirmationLabel;
        
        // Sliders
        private HorizontalSlider? _speedSlider;
        private HorizontalSlider? _turnRateSlider;
        private HorizontalSlider? _cameraSpeedSlider;
        private HorizontalSlider? _panSpeedSlider;
        private HorizontalSlider? _inertiaSlider;
        private HorizontalSlider? _driftSlider;
        private HorizontalSlider? _avoidanceRangeSlider;
        private HorizontalSlider? _shipIdleRateSlider;
        private HorizontalSlider? _lookAheadSlider;
        private HorizontalSlider? _cameraInertiaSlider;
        private HorizontalSlider? _aimRotationSpeedSlider;
        private HorizontalSlider? _musicVolumeSlider;
        private HorizontalSlider? _sfxVolumeSlider;
        
        // Buttons
        private TextButton? _gridSizeLeftButton;
        private TextButton? _gridSizeRightButton;
        private TextButton? _saveButton;
        private TextButton? _previewLeftButton;
        private TextButton? _previewRightButton;
        
        // Checkboxes
        private CheckBox? _gridVisibleCheckBox;
        private CheckBox? _avoidanceRangeVisibleCheckBox;
        private CheckBox? _enemyPathVisibleCheckBox;
        private CheckBox? _enemyTargetPathVisibleCheckBox;
        private CheckBox? _lookAheadVisibleCheckBox;
        private CheckBox? _musicEnabledCheckBox;
        private CheckBox? _sfxEnabledCheckBox;
        
        // Panels
        private Panel? _cameraSettingsPanel;
        private Panel? _previewPanel;
        
        // Preview UI
        private Label? _previewCoordinateLabel;
        private Label? _previewShipLabel;
        private Texture2D? _previewShip1Texture;
        private Texture2D? _previewShip2Texture;
        
        // UI State
        private bool _uiVisible = true;
        private bool _behaviorTextVisible = true;
        private bool _isPreviewActive = false;
        private int _previewShipIndex = 0;
        private float _saveConfirmationTimer = 0f;
        private const float SaveConfirmationDuration = 2f;
        
        // Settings values (managed by UI)
        private int _gridSize = 128;
        private float _avoidanceDetectionRange = 300f;
        private float _shipIdleRate = 0.3f;
        private float _musicVolume = 0.5f;
        private float _sfxVolume = 1.0f;
        private bool _musicEnabled = true;
        private bool _sfxEnabled = true;
        private bool _gridVisible = false;
        private bool _uiGridVisible = false;
        private bool _pathfindingGridVisible = false;
        private bool _avoidanceRangeVisible = false;
        private bool _enemyPathVisible = false;
        private bool _enemyTargetPathVisible = false;
        
        // Dependencies
        private readonly GraphicsDevice _graphicsDevice;
        private readonly ContentManager _content;
        private readonly SpriteFont? _font;
        
        // Callbacks for GameScene operations
        public Action? OnSaveCurrentShipSettings { get; set; }
        public Action? OnSaveSettings { get; set; }
        public Action? OnSavePanelSettings { get; set; }
        public Action<int>? OnSwitchShipClass { get; set; }
        public Action<float>? OnShipIdleRateChanged { get; set; }
        public Action<float, bool>? OnSFXSettingsChanged { get; set; }
        public Func<PlayerShip?>? GetPlayerShip { get; set; }
        public Func<int>? GetCurrentShipClassIndex { get; set; }
        public Func<List<FriendlyShip>>? GetFriendlyShips { get; set; }
        public Func<List<EnemyShip>>? GetEnemyShips { get; set; }
        public Func<float>? GetCameraZoom { get; set; }
        public Func<float>? GetCameraSpeed { get; set; }
        public Func<float>? GetCameraPanSpeed { get; set; }
        public Func<float>? GetCameraInertia { get; set; }
        public Action<float>? SetCameraSpeed { get; set; }
        public Action<float>? SetCameraPanSpeed { get; set; }
        public Action<float>? SetCameraInertia { get; set; }
        public Action<float>? SetCameraZoom { get; set; }
        public Func<SoundEffectInstance?>? GetBackgroundMusicInstance { get; set; }
        public Func<SoundEffectInstance?>? GetShipFlySound { get; set; }
        public Func<SoundEffectInstance?>? GetShipIdleSound { get; set; }
        
        // Public properties for state access
        public bool UIVisible { get => _uiVisible; set => _uiVisible = value; }
        public bool BehaviorTextVisible { get => _behaviorTextVisible; set => _behaviorTextVisible = value; }
        public bool IsPreviewActive { get => _isPreviewActive; set => _isPreviewActive = value; }
        public int PreviewShipIndex { get => _previewShipIndex; set => _previewShipIndex = value; }
        public int GridSize { get => _gridSize; set => _gridSize = value; }
        public bool GridVisible { get => _gridVisible; set => _gridVisible = value; }
        public bool UIGridVisible { get => _uiGridVisible; set => _uiGridVisible = value; }
        public bool PathfindingGridVisible { get => _pathfindingGridVisible; set => _pathfindingGridVisible = value; }
        public bool AvoidanceRangeVisible { get => _avoidanceRangeVisible; set => _avoidanceRangeVisible = value; }
        public bool EnemyPathVisible { get => _enemyPathVisible; set => _enemyPathVisible = value; }
        public bool EnemyTargetPathVisible { get => _enemyTargetPathVisible; set => _enemyTargetPathVisible = value; }
        public float AvoidanceDetectionRange { get => _avoidanceDetectionRange; set => _avoidanceDetectionRange = value; }
        public float ShipIdleRate { get => _shipIdleRate; set => _shipIdleRate = value; }
        public float MusicVolume { get => _musicVolume; set => _musicVolume = value; }
        public float SFXVolume { get => _sfxVolume; set => _sfxVolume = value; }
        public bool MusicEnabled { get => _musicEnabled; set => _musicEnabled = value; }
        public bool SFXEnabled { get => _sfxEnabled; set => _sfxEnabled = value; }
        
        public Desktop? Desktop => _desktop;
        public Desktop? SaveButtonDesktop => _saveButtonDesktop;
        public Desktop? CoordinateDesktop => _coordinateDesktop;
        public Desktop? PreviewDesktop => _previewDesktop;
        public Label? MouseCoordinateLabel => _mouseCoordinateLabel;
        public Label? ZoomLabel => _zoomLabel;
        public Texture2D? PreviewShip1Texture => _previewShip1Texture;
        public Texture2D? PreviewShip2Texture => _previewShip2Texture;
        public Texture2D? GridPixelTexture { get; set; }
        
        public GameSceneUIManager(GraphicsDevice graphicsDevice, ContentManager content, SpriteFont? font)
        {
            _graphicsDevice = graphicsDevice;
            _content = content;
            _font = font;
        }
        
        /// <summary>
        /// Initialize all UI elements
        /// </summary>
        public void Initialize(
            PlayerShip? playerShip,
            float cameraZoom,
            float cameraSpeed,
            float cameraPanSpeed,
            float cameraInertia)
        {
            _desktop = new Desktop();
            
            // Use a Grid layout to organize UI elements properly
            var grid = new Grid
            {
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top,
                ColumnSpacing = 0,
                RowSpacing = 8
            };
            
            // Define columns (one column for all elements)
            grid.ColumnsProportions.Add(new Proportion(ProportionType.Auto));
            
            // Define rows for each UI element
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
            
            // Speed label
            _speedLabel = new Label
            {
                Text = $"Ship Speed: {playerShip?.MoveSpeed ?? 300f:F0}",
                TextColor = Color.Lime,
                GridColumn = 0,
                GridRow = 0,
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top,
                Padding = new Myra.Graphics2D.Thickness(0, 0, 0, 0)
            };
            grid.Widgets.Add(_speedLabel);
            
            // Speed slider
            _speedSlider = new HorizontalSlider
            {
                Minimum = 50f,
                Maximum = 1000f,
                Value = playerShip?.MoveSpeed ?? 300f,
                Width = 200,
                Height = 10,
                GridColumn = 0,
                GridRow = 1,
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top,
                Padding = new Myra.Graphics2D.Thickness(0, 0, 0, 0)
            };
            _speedSlider.ValueChanged += (s, a) =>
            {
                if (GetPlayerShip?.Invoke() != null)
                {
                    var ship = GetPlayerShip();
                    ship!.MoveSpeed = _speedSlider!.Value;
                    _speedLabel!.Text = $"Ship Speed: {_speedSlider.Value:F0}";
                    
                    int classIndex = GetCurrentShipClassIndex?.Invoke() ?? 0;
                    if (classIndex == 1)
                    {
                        GetFriendlyShips?.Invoke()?.ForEach(fs => fs.MoveSpeed = _speedSlider.Value);
                    }
                    else if (classIndex == 2)
                    {
                        GetEnemyShips?.Invoke()?.ForEach(es => es.MoveSpeed = _speedSlider.Value);
                    }
                    
                    OnSaveCurrentShipSettings?.Invoke();
                }
            };
            grid.Widgets.Add(_speedSlider);
            
            // Turn rate label
            _turnRateLabel = new Label
            {
                Text = $"Ship Turn Rate: {playerShip?.RotationSpeed ?? 5f:F1}",
                TextColor = Color.Cyan,
                GridColumn = 0,
                GridRow = 2,
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top,
                Padding = new Myra.Graphics2D.Thickness(0, 0, 0, 0)
            };
            grid.Widgets.Add(_turnRateLabel);
            
            // Turn rate slider
            _turnRateSlider = new HorizontalSlider
            {
                Minimum = 0.5f,
                Maximum = 10f,
                Value = playerShip?.RotationSpeed ?? 3f,
                Width = 200,
                Height = 10,
                GridColumn = 0,
                GridRow = 3,
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top,
                Padding = new Myra.Graphics2D.Thickness(0, 0, 0, 0)
            };
            _turnRateSlider.ValueChanged += (s, a) =>
            {
                if (GetPlayerShip?.Invoke() != null)
                {
                    var ship = GetPlayerShip();
                    ship!.RotationSpeed = _turnRateSlider!.Value;
                    _turnRateLabel!.Text = $"Ship Turn Rate: {_turnRateSlider.Value:F1}";
                    
                    int classIndex = GetCurrentShipClassIndex?.Invoke() ?? 0;
                    if (classIndex == 1)
                    {
                        GetFriendlyShips?.Invoke()?.ForEach(fs => fs.RotationSpeed = _turnRateSlider.Value);
                    }
                    else if (classIndex == 2)
                    {
                        GetEnemyShips?.Invoke()?.ForEach(es => es.RotationSpeed = _turnRateSlider.Value);
                    }
                    
                    OnSaveCurrentShipSettings?.Invoke();
                }
            };
            grid.Widgets.Add(_turnRateSlider);
            
            // Grid size controls
            var gridSizeControlsContainer = new HorizontalStackPanel
            {
                GridColumn = 0,
                GridRow = 4,
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top,
                Spacing = 10,
                Padding = new Myra.Graphics2D.Thickness(0, 0, 0, 0)
            };
            
            _gridSizeLabel = new Label
            {
                Text = $"Grid Size: {_gridSize}",
                TextColor = Color.Magenta
            };
            gridSizeControlsContainer.Widgets.Add(_gridSizeLabel);
            
            _gridVisibleCheckBox = new CheckBox
            {
                Text = "Show Grid",
                IsChecked = _gridVisible
            };
            _gridVisibleCheckBox.Click += (s, a) =>
            {
                _gridVisible = _gridVisibleCheckBox.IsChecked;
                OnSavePanelSettings?.Invoke();
            };
            gridSizeControlsContainer.Widgets.Add(_gridVisibleCheckBox);
            grid.Widgets.Add(gridSizeControlsContainer);
            
            // Grid size buttons
            var gridSizeButtonContainer = new HorizontalStackPanel
            {
                GridColumn = 0,
                GridRow = 5,
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top,
                Spacing = 5,
                Padding = new Myra.Graphics2D.Thickness(0, 0, 0, 0)
            };
            
            int[] gridSizeValues = { 64, 128, 256, 512, 1024 };
            
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
                    _gridSizeLabel!.Text = $"Grid Size: {_gridSize}";
                }
            };
            gridSizeButtonContainer.Widgets.Add(_gridSizeLeftButton);
            
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
                    _gridSizeLabel!.Text = $"Grid Size: {_gridSize}";
                }
            };
            gridSizeButtonContainer.Widgets.Add(_gridSizeRightButton);
            grid.Widgets.Add(gridSizeButtonContainer);
            
            // Aim rotation speed label
            _aimRotationSpeedLabel = new Label
            {
                Text = $"Ship Idle Rotation Speed: {playerShip?.AimRotationSpeed ?? 5f:F1}",
                TextColor = Color.Lime,
                GridColumn = 0,
                GridRow = 6,
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top,
                Padding = new Myra.Graphics2D.Thickness(0, 0, 0, 0)
            };
            grid.Widgets.Add(_aimRotationSpeedLabel);
            
            // Aim rotation speed slider
            _aimRotationSpeedSlider = new HorizontalSlider
            {
                Minimum = 0.5f,
                Maximum = 10f,
                Value = playerShip?.AimRotationSpeed ?? 3f,
                Width = 200,
                Height = 10,
                GridColumn = 0,
                GridRow = 7,
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top,
                Padding = new Myra.Graphics2D.Thickness(0, 0, 0, 0)
            };
            _aimRotationSpeedSlider.ValueChanged += (s, a) =>
            {
                if (GetPlayerShip?.Invoke() != null)
                {
                    var ship = GetPlayerShip();
                    ship!.AimRotationSpeed = _aimRotationSpeedSlider!.Value;
                    _aimRotationSpeedLabel!.Text = $"Ship Idle Rotation Speed: {_aimRotationSpeedSlider.Value:F1}";
                    
                    int classIndex = GetCurrentShipClassIndex?.Invoke() ?? 0;
                    if (classIndex == 1)
                    {
                        GetFriendlyShips?.Invoke()?.ForEach(fs => fs.AimRotationSpeed = _aimRotationSpeedSlider.Value);
                    }
                    else if (classIndex == 2)
                    {
                        GetEnemyShips?.Invoke()?.ForEach(es => es.AimRotationSpeed = _aimRotationSpeedSlider.Value);
                    }
                    
                    OnSaveCurrentShipSettings?.Invoke();
                }
            };
            grid.Widgets.Add(_aimRotationSpeedSlider);
            
            // Inertia label
            _inertiaLabel = new Label
            {
                Text = $"Ship Inertia: {playerShip?.Inertia ?? 0.9f:F2}",
                TextColor = new Color(255, 100, 255),
                GridColumn = 0,
                GridRow = 8,
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top,
                Padding = new Myra.Graphics2D.Thickness(0, 0, 0, 0)
            };
            grid.Widgets.Add(_inertiaLabel);
            
            // Inertia slider
            _inertiaSlider = new HorizontalSlider
            {
                Minimum = 0f,
                Maximum = 0.995f,
                Value = playerShip?.Inertia ?? 0.9f,
                Width = 200,
                Height = 10,
                GridColumn = 0,
                GridRow = 9,
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top,
                Padding = new Myra.Graphics2D.Thickness(0, 0, 0, 0)
            };
            _inertiaSlider.ValueChanged += (s, a) =>
            {
                if (GetPlayerShip?.Invoke() != null)
                {
                    var ship = GetPlayerShip();
                    ship!.Inertia = _inertiaSlider!.Value;
                    _inertiaLabel!.Text = $"Ship Inertia: {_inertiaSlider.Value:F2}";
                    
                    int classIndex = GetCurrentShipClassIndex?.Invoke() ?? 0;
                    if (classIndex == 1)
                    {
                        GetFriendlyShips?.Invoke()?.ForEach(fs => fs.Inertia = _inertiaSlider.Value);
                    }
                    else if (classIndex == 2)
                    {
                        GetEnemyShips?.Invoke()?.ForEach(es => es.Inertia = _inertiaSlider.Value);
                    }
                    
                    OnSaveCurrentShipSettings?.Invoke();
                }
            };
            grid.Widgets.Add(_inertiaSlider);
            
            // Drift label
            _driftLabel = new Label
            {
                Text = $"Ship Drift: {playerShip?.Drift ?? 0f:F2}",
                TextColor = new Color(150, 255, 150),
                GridColumn = 0,
                GridRow = 10,
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top,
                Padding = new Myra.Graphics2D.Thickness(0, 0, 0, 0)
            };
            grid.Widgets.Add(_driftLabel);
            
            // Drift slider
            _driftSlider = new HorizontalSlider
            {
                Minimum = 0f,
                Maximum = 10f,
                Value = playerShip?.Drift ?? 0f,
                Width = 200,
                Height = 10,
                GridColumn = 0,
                GridRow = 11,
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top,
                Padding = new Myra.Graphics2D.Thickness(0, 0, 0, 0)
            };
            _driftSlider.ValueChanged += (s, a) =>
            {
                if (GetPlayerShip?.Invoke() != null)
                {
                    var ship = GetPlayerShip();
                    ship!.Drift = _driftSlider!.Value;
                    _driftLabel!.Text = $"Ship Drift: {_driftSlider.Value:F2}";
                    
                    int classIndex = GetCurrentShipClassIndex?.Invoke() ?? 0;
                    if (classIndex == 1)
                    {
                        GetFriendlyShips?.Invoke()?.ForEach(fs => fs.Drift = _driftSlider.Value);
                    }
                    else if (classIndex == 2)
                    {
                        GetEnemyShips?.Invoke()?.ForEach(es => es.Drift = _driftSlider.Value);
                    }
                    
                    OnSaveCurrentShipSettings?.Invoke();
                }
            };
            grid.Widgets.Add(_driftSlider);
            
            // Avoidance Detection Range label
            _avoidanceRangeLabel = new Label
            {
                Text = $"Avoidance Detection Range: {_avoidanceDetectionRange:F0}",
                TextColor = new Color(100, 255, 100),
                GridColumn = 0,
                GridRow = 12,
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top,
                Padding = new Myra.Graphics2D.Thickness(0, 0, 0, 0)
            };
            grid.Widgets.Add(_avoidanceRangeLabel);
            
            // Avoidance Detection Range slider and checkbox
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
                Height = 10,
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top,
                Padding = new Myra.Graphics2D.Thickness(0, 0, 0, 0)
            };
            _avoidanceRangeSlider.ValueChanged += (s, a) =>
            {
                _avoidanceDetectionRange = _avoidanceRangeSlider!.Value;
                _avoidanceRangeLabel!.Text = $"Avoidance Detection Range: {_avoidanceDetectionRange:F0}";
                
                int classIndex = GetCurrentShipClassIndex?.Invoke() ?? 0;
                if (classIndex == 1)
                {
                    GetFriendlyShips?.Invoke()?.ForEach(fs => fs.AvoidanceDetectionRange = _avoidanceDetectionRange);
                }
                else if (classIndex == 2)
                {
                    GetEnemyShips?.Invoke()?.ForEach(es => es.AvoidanceDetectionRange = _avoidanceDetectionRange);
                }
                else if (GetPlayerShip?.Invoke() != null)
                {
                    GetPlayerShip()!.AvoidanceDetectionRange = _avoidanceDetectionRange;
                }
                
                OnSaveCurrentShipSettings?.Invoke();
            };
            avoidanceRangeContainer.Widgets.Add(_avoidanceRangeSlider);
            
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
                OnSavePanelSettings?.Invoke();
            };
            avoidanceRangeContainer.Widgets.Add(_avoidanceRangeVisibleCheckBox);
            grid.Widgets.Add(avoidanceRangeContainer);
            
            // Ship Idle Rate label
            _shipIdleRateLabel = new Label
            {
                Text = $"Ship Idle Rate: {(_shipIdleRate * 100f):F0}%",
                TextColor = new Color(255, 200, 100),
                GridColumn = 0,
                GridRow = 14,
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top,
                Padding = new Myra.Graphics2D.Thickness(0, 0, 0, 0)
            };
            grid.Widgets.Add(_shipIdleRateLabel);
            
            // Ship Idle Rate slider
            _shipIdleRateSlider = new HorizontalSlider
            {
                Minimum = 0f,
                Maximum = 1f,
                Value = _shipIdleRate,
                Width = 200,
                Height = 10,
                GridColumn = 0,
                GridRow = 15,
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top,
                Padding = new Myra.Graphics2D.Thickness(0, 0, 0, 0)
            };
            _shipIdleRateSlider.ValueChanged += (s, a) =>
            {
                _shipIdleRate = _shipIdleRateSlider!.Value;
                _shipIdleRateLabel!.Text = $"Ship Idle Rate: {(_shipIdleRate * 100f):F0}%";
                OnShipIdleRateChanged?.Invoke(_shipIdleRate);
                OnSaveCurrentShipSettings?.Invoke();
            };
            grid.Widgets.Add(_shipIdleRateSlider);
            
            // Look-ahead label
            _lookAheadLabel = new Label
            {
                Text = $"Look-Ahead Distance: {playerShip?.LookAheadDistance ?? 1.5f:F2}x",
                TextColor = new Color(255, 200, 100),
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
            
            _lookAheadSlider = new HorizontalSlider
            {
                Minimum = 0.5f,
                Maximum = 5.0f,
                Value = playerShip?.LookAheadDistance ?? 1.5f,
                Width = 200,
                Height = 10,
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top
            };
            _lookAheadSlider.ValueChanged += (s, a) =>
            {
                if (GetPlayerShip?.Invoke() != null)
                {
                    var ship = GetPlayerShip();
                    ship!.LookAheadDistance = _lookAheadSlider!.Value;
                    _lookAheadLabel!.Text = $"Look-Ahead Distance: {_lookAheadSlider.Value:F2}x";
                    
                    int classIndex = GetCurrentShipClassIndex?.Invoke() ?? 0;
                    if (classIndex == 1)
                    {
                        GetFriendlyShips?.Invoke()?.ForEach(fs => fs.LookAheadDistance = _lookAheadSlider.Value);
                    }
                    else if (classIndex == 2)
                    {
                        GetEnemyShips?.Invoke()?.ForEach(es => es.LookAheadDistance = _lookAheadSlider.Value);
                    }
                    
                    OnSaveCurrentShipSettings?.Invoke();
                }
            };
            lookAheadContainer.Widgets.Add(_lookAheadSlider);
            
            _lookAheadVisibleCheckBox = new CheckBox
            {
                Text = "Show Line",
                IsChecked = playerShip?.LookAheadVisible ?? false
            };
            _lookAheadVisibleCheckBox.Click += (s, a) =>
            {
                if (GetPlayerShip?.Invoke() != null)
                {
                    var ship = GetPlayerShip();
                    ship!.LookAheadVisible = _lookAheadVisibleCheckBox!.IsChecked;
                    GetFriendlyShips?.Invoke()?.ForEach(fs => fs.LookAheadVisible = _lookAheadVisibleCheckBox.IsChecked);
                    OnSaveCurrentShipSettings?.Invoke();
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
                OnSavePanelSettings?.Invoke();
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
                OnSavePanelSettings?.Invoke();
            };
            grid.Widgets.Add(_enemyTargetPathVisibleCheckBox);
            
            // Music volume label
            _musicVolumeLabel = new Label
            {
                Text = $"Music Volume: {(_musicVolume * 100f):F0}%",
                TextColor = new Color(100, 200, 255),
                GridColumn = 0,
                GridRow = 20,
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top,
                Padding = new Myra.Graphics2D.Thickness(0, 0, 0, 0)
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
                Padding = new Myra.Graphics2D.Thickness(0, 0, 0, 0)
            };
            
            _musicVolumeSlider = new HorizontalSlider
            {
                Minimum = 0f,
                Maximum = 1f,
                Value = _musicVolume,
                Width = 200,
                Height = 10,
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top
            };
            _musicVolumeSlider.ValueChanged += (s, a) =>
            {
                _musicVolume = _musicVolumeSlider!.Value;
                _musicVolumeLabel!.Text = $"Music Volume: {(_musicVolume * 100f):F0}%";
                var musicInstance = GetBackgroundMusicInstance?.Invoke();
                if (musicInstance != null && _musicEnabled)
                {
                    musicInstance.Volume = _musicVolume;
                }
            };
            musicVolumeContainer.Widgets.Add(_musicVolumeSlider);
            
            _musicEnabledCheckBox = new CheckBox
            {
                Text = "Music",
                IsChecked = _musicEnabled
            };
            _musicEnabledCheckBox.Click += (s, a) =>
            {
                _musicEnabled = _musicEnabledCheckBox.IsChecked;
                var musicInstance = GetBackgroundMusicInstance?.Invoke();
                if (musicInstance != null)
                {
                    if (_musicEnabled)
                    {
                        musicInstance.Volume = _musicVolume;
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
                OnSavePanelSettings?.Invoke();
            };
            musicVolumeContainer.Widgets.Add(_musicEnabledCheckBox);
            grid.Widgets.Add(musicVolumeContainer);
            
            // SFX volume label
            _sfxVolumeLabel = new Label
            {
                Text = $"SFX Volume: {(_sfxVolume * 100f):F0}%",
                TextColor = new Color(255, 150, 100),
                GridColumn = 0,
                GridRow = 22,
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top,
                Padding = new Myra.Graphics2D.Thickness(0, 0, 0, 0)
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
                Padding = new Myra.Graphics2D.Thickness(0, 0, 0, 0)
            };
            
            _sfxVolumeSlider = new HorizontalSlider
            {
                Minimum = 0f,
                Maximum = 1f,
                Value = _sfxVolume,
                Width = 200,
                Height = 10,
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top
            };
            _sfxVolumeSlider.ValueChanged += (s, a) =>
            {
                _sfxVolume = _sfxVolumeSlider!.Value;
                _sfxVolumeLabel!.Text = $"SFX Volume: {(_sfxVolume * 100f):F0}%";
                OnSFXSettingsChanged?.Invoke(_sfxVolume, _sfxEnabled);
                
                var flySound = GetShipFlySound?.Invoke();
                var idleSound = GetShipIdleSound?.Invoke();
                if (flySound != null && _sfxEnabled)
                {
                    flySound.Volume = _sfxVolume * 0.8f;
                }
                if (idleSound != null && _sfxEnabled)
                {
                    idleSound.Volume = _sfxVolume;
                }
            };
            sfxVolumeContainer.Widgets.Add(_sfxVolumeSlider);
            
            _sfxEnabledCheckBox = new CheckBox
            {
                Text = "SFX",
                IsChecked = _sfxEnabled
            };
            _sfxEnabledCheckBox.Click += (s, a) =>
            {
                _sfxEnabled = _sfxEnabledCheckBox.IsChecked;
                OnSFXSettingsChanged?.Invoke(_sfxVolume, _sfxEnabled);
                
                var flySound = GetShipFlySound?.Invoke();
                var idleSound = GetShipIdleSound?.Invoke();
                if (flySound != null)
                {
                    flySound.Volume = _sfxEnabled ? _sfxVolume * 0.8f : 0f;
                }
                if (idleSound != null)
                {
                    idleSound.Volume = _sfxEnabled ? _sfxVolume : 0f;
                }
                OnSavePanelSettings?.Invoke();
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
            
            cameraSettingsGrid.RowsProportions.Add(new Proportion(ProportionType.Auto)); // Zoom label
            cameraSettingsGrid.RowsProportions.Add(new Proportion(ProportionType.Auto)); // Camera speed label
            cameraSettingsGrid.RowsProportions.Add(new Proportion(ProportionType.Auto)); // Camera speed slider
            cameraSettingsGrid.RowsProportions.Add(new Proportion(ProportionType.Auto)); // Cam to Player Speed label
            cameraSettingsGrid.RowsProportions.Add(new Proportion(ProportionType.Auto)); // Cam to Player Speed slider
            cameraSettingsGrid.RowsProportions.Add(new Proportion(ProportionType.Auto)); // Camera inertia label
            cameraSettingsGrid.RowsProportions.Add(new Proportion(ProportionType.Auto)); // Camera inertia slider
            
            // Zoom label
            _zoomLabel = new Label
            {
                Text = $"Zoom: {cameraZoom:F2}x",
                TextColor = Color.Yellow,
                GridColumn = 0,
                GridRow = 0,
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top,
                Padding = new Myra.Graphics2D.Thickness(0, 0, 0, 0)
            };
            cameraSettingsGrid.Widgets.Add(_zoomLabel);
            
            // Camera speed label
            _cameraSpeedLabel = new Label
            {
                Text = $"Camera Speed: {cameraSpeed:F0}",
                TextColor = Color.Orange,
                GridColumn = 0,
                GridRow = 1,
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top,
                Padding = new Myra.Graphics2D.Thickness(0, 0, 0, 0)
            };
            cameraSettingsGrid.Widgets.Add(_cameraSpeedLabel);
            
            // Camera speed slider
            _cameraSpeedSlider = new HorizontalSlider
            {
                Minimum = 50f,
                Maximum = 1000f,
                Value = cameraSpeed,
                Width = 200,
                Height = 10,
                GridColumn = 0,
                GridRow = 2,
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top,
                Padding = new Myra.Graphics2D.Thickness(0, 0, 0, 0)
            };
            _cameraSpeedSlider.ValueChanged += (s, a) =>
            {
                SetCameraSpeed?.Invoke(_cameraSpeedSlider!.Value);
                _cameraSpeedLabel!.Text = $"Camera Speed: {_cameraSpeedSlider.Value:F0}";
            };
            cameraSettingsGrid.Widgets.Add(_cameraSpeedSlider);
            
            // Cam to Player Speed label
            _panSpeedLabel = new Label
            {
                Text = $"Cam to Player Speed: {cameraPanSpeed:F0}",
                TextColor = Color.Yellow,
                GridColumn = 0,
                GridRow = 3,
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top,
                Padding = new Myra.Graphics2D.Thickness(0, 0, 0, 0)
            };
            cameraSettingsGrid.Widgets.Add(_panSpeedLabel);
            
            // Cam to Player Speed slider
            _panSpeedSlider = new HorizontalSlider
            {
                Minimum = 200f,
                Maximum = 2000f,
                Value = cameraPanSpeed,
                Width = 200,
                Height = 10,
                GridColumn = 0,
                GridRow = 4,
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top,
                Padding = new Myra.Graphics2D.Thickness(0, 0, 0, 0)
            };
            _panSpeedSlider.ValueChanged += (s, a) =>
            {
                SetCameraPanSpeed?.Invoke(_panSpeedSlider!.Value);
                _panSpeedLabel!.Text = $"Cam to Player Speed: {_panSpeedSlider.Value:F0}";
            };
            cameraSettingsGrid.Widgets.Add(_panSpeedSlider);
            
            // Camera inertia label
            _cameraInertiaLabel = new Label
            {
                Text = $"Camera Inertia: {cameraInertia:F2}",
                TextColor = Color.Cyan,
                GridColumn = 0,
                GridRow = 5,
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top,
                Padding = new Myra.Graphics2D.Thickness(0, 0, 0, 0)
            };
            cameraSettingsGrid.Widgets.Add(_cameraInertiaLabel);
            
            // Camera inertia slider
            _cameraInertiaSlider = new HorizontalSlider
            {
                Minimum = 0f,
                Maximum = 0.995f,
                Value = cameraInertia,
                Width = 200,
                Height = 10,
                GridColumn = 0,
                GridRow = 6,
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top,
                Padding = new Myra.Graphics2D.Thickness(0, 0, 0, 0)
            };
            _cameraInertiaSlider.ValueChanged += (s, a) =>
            {
                SetCameraInertia?.Invoke(_cameraInertiaSlider!.Value);
                _cameraInertiaLabel!.Text = $"Camera Inertia: {_cameraInertiaSlider.Value:F2}";
            };
            cameraSettingsGrid.Widgets.Add(_cameraInertiaSlider);
            
            // Wrap camera settings grid in a panel
            _cameraSettingsPanel = new Panel
            {
                Background = new Myra.Graphics2D.Brushes.SolidBrush(new Microsoft.Xna.Framework.Color(20, 20, 20, 220)),
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top,
                Padding = new Myra.Graphics2D.Thickness(15, 15, 15, 15),
                Margin = new Myra.Graphics2D.Thickness(20, 0, 0, 0)
            };
            _cameraSettingsPanel.Widgets.Add(cameraSettingsGrid);
            
            // Wrap grid in a panel with background
            var uiPanel = new Panel
            {
                Background = new Myra.Graphics2D.Brushes.SolidBrush(new Microsoft.Xna.Framework.Color(20, 20, 20, 220)),
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top,
                Padding = new Myra.Graphics2D.Thickness(15, 15, 15, 15)
            };
            uiPanel.Widgets.Add(grid);
            
            // Create a container panel to hold both UI panel and camera settings panel
            var containerPanel = new HorizontalStackPanel
            {
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top,
                Spacing = 0,
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
                Padding = new Myra.Graphics2D.Thickness(0, 0, 0, 0),
                Width = 200, // Ensure panel is wide enough
                Height = 100 // Ensure panel is tall enough to show label above button
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
            _saveButton.Click += (s, a) => OnSaveSettings?.Invoke();
            saveButtonPanel.Widgets.Add(_saveButton);
            
            // Save confirmation label (initially hidden) - positioned above save button
            _saveConfirmationLabel = new Label
            {
                Text = "Settings Saved!",
                TextColor = Color.Lime,
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Bottom,
                Padding = new Myra.Graphics2D.Thickness(0, 0, 10, 50), // 50px above bottom (above button)
                Margin = new Myra.Graphics2D.Thickness(0, 0, 0, 0),
                Visible = false
            };
            saveButtonPanel.Widgets.Add(_saveConfirmationLabel);
            
            _saveButtonDesktop.Root = saveButtonPanel;
            
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
            
            _previewShipLabel = new Label
            {
                Text = "PlayerShip (1/3)",
                TextColor = Color.Yellow,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Top,
                Margin = new Myra.Graphics2D.Thickness(0, 35, 0, 0)
            };
            _previewPanel.Widgets.Add(_previewShipLabel);
            
            // Left arrow button
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
                    _previewShipIndex = 2;
                UpdatePreviewShipLabel();
                OnSwitchShipClass?.Invoke(_previewShipIndex);
            };
            _previewPanel.Widgets.Add(_previewLeftButton);
            
            // Right arrow button
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
                    _previewShipIndex = 0;
                UpdatePreviewShipLabel();
                OnSwitchShipClass?.Invoke(_previewShipIndex);
            };
            _previewPanel.Widgets.Add(_previewRightButton);
            
            // Close button
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
                _previewShip1Texture = _content.Load<Texture2D>("ship1-256");
                _previewShip2Texture = _content.Load<Texture2D>("ship2-256");
            }
            catch (System.Exception ex)
            {
                System.Console.WriteLine($"Failed to load preview ship textures: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Update UI state (called each frame)
        /// </summary>
        public void Update(GameTime gameTime)
        {
            var deltaTime = (float)gameTime.ElapsedGameTime.TotalSeconds;
            
            // Update save confirmation timer
            if (_saveConfirmationTimer > 0f)
            {
                _saveConfirmationTimer -= deltaTime;
                if (_saveConfirmationTimer <= 0f && _saveConfirmationLabel != null)
                {
                    _saveConfirmationLabel.Visible = false;
                }
            }
            
            // Update Myra input
            _desktop?.UpdateInput();
            _saveButtonDesktop?.UpdateInput();
            _coordinateDesktop?.UpdateInput();
            _previewDesktop?.UpdateInput();
        }
        
        /// <summary>
        /// Update coordinate label for UI grid mode
        /// </summary>
        public void UpdateCoordinateLabel()
        {
            if (!_uiGridVisible || _mouseCoordinateLabel == null) return;
            
            var mouseState = Mouse.GetState();
            var mouseX = mouseState.X;
            var mouseY = mouseState.Y;
            
            const int UIGridSize = 10;
            int snappedX = (mouseX / UIGridSize) * UIGridSize;
            int snappedY = (mouseY / UIGridSize) * UIGridSize;
            
            _mouseCoordinateLabel.Text = $"({snappedX}, {snappedY})";
            _mouseCoordinateLabel.Margin = new Myra.Graphics2D.Thickness(mouseX, mouseY - 25, 0, 0);
            _mouseCoordinateLabel.Visible = true;
        }
        
        /// <summary>
        /// Update zoom label
        /// </summary>
        public void UpdateZoomLabel(float zoom)
        {
            if (_zoomLabel != null)
            {
                _zoomLabel.Text = $"Zoom: {zoom:F2}x";
            }
        }
        
        /// <summary>
        /// Update preview ship label
        /// </summary>
        public void UpdatePreviewShipLabel()
        {
            if (_previewShipLabel == null) return;
            
            string className = _previewShipIndex == 0 ? "PlayerShip" : (_previewShipIndex == 1 ? "FriendlyShip" : "EnemyShip");
            _previewShipLabel.Text = $"{className} ({_previewShipIndex + 1}/3)";
        }
        
        /// <summary>
        /// Show save confirmation message
        /// </summary>
        public void ShowSaveConfirmation(bool success = true)
        {
            if (_saveConfirmationLabel == null) return;
            
            if (success)
            {
                _saveConfirmationLabel.Text = "Settings Saved!";
                _saveConfirmationLabel.TextColor = Color.Lime;
            }
            else
            {
                _saveConfirmationLabel.Text = "Save Failed!";
                _saveConfirmationLabel.TextColor = Color.Red;
            }
            
            // Ensure label is visible and positioned correctly
            _saveConfirmationLabel.Visible = true;
            _saveConfirmationTimer = SaveConfirmationDuration;
            
            // Force update to ensure label is rendered
            System.Console.WriteLine($"Save confirmation shown: {_saveConfirmationLabel.Text}, Visible: {_saveConfirmationLabel.Visible}");
        }
        
        /// <summary>
        /// Load settings and update UI
        /// </summary>
        public void LoadSettings(
            ref int currentShipClassIndex,
            ref float cameraZoom,
            ref float cameraSpeed,
            ref float cameraPanSpeed,
            ref float cameraInertia,
            ref int gridSize,
            ref float musicVolume,
            ref float sfxVolume)
        {
            try
            {
                var filePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "settings.json");
                if (File.Exists(filePath))
                {
                    var json = File.ReadAllText(filePath);
                    var settings = JsonSerializer.Deserialize<JsonElement>(json);
                    
                    // Load current ship class
                    if (settings.TryGetProperty("CurrentShipClass", out var shipClassElement))
                    {
                        currentShipClassIndex = shipClassElement.GetInt32();
                    }
                    
                    // Load camera speed
                    if (settings.TryGetProperty("CameraSpeed", out var cameraSpeedElement))
                    {
                        cameraSpeed = cameraSpeedElement.GetSingle();
                        if (_cameraSpeedSlider != null) _cameraSpeedSlider.Value = cameraSpeed;
                        if (_cameraSpeedLabel != null) _cameraSpeedLabel.Text = $"Camera Speed: {cameraSpeed:F0}";
                        SetCameraSpeed?.Invoke(cameraSpeed);
                    }
                    
                    // Load zoom
                    if (settings.TryGetProperty("Zoom", out var zoomElement))
                    {
                        cameraZoom = MathHelper.Clamp(zoomElement.GetSingle(), 0.40f, 1.10f);
                        if (_zoomLabel != null) _zoomLabel.Text = $"Zoom: {cameraZoom:F2}x";
                        SetCameraZoom?.Invoke(cameraZoom);
                    }
                    
                    // Load grid size
                    if (settings.TryGetProperty("GridSize", out var gridSizeElement))
                    {
                        var gridSizeValue = gridSizeElement.GetInt32();
                        int[] validGridSizes = { 64, 128, 256, 512, 1024 };
                        int closestSize = validGridSizes[0];
                        int minDiff = Math.Abs(gridSizeValue - closestSize);
                        foreach (int size in validGridSizes)
                        {
                            int diff = Math.Abs(gridSizeValue - size);
                            if (diff < minDiff)
                            {
                                minDiff = diff;
                                closestSize = size;
                            }
                        }
                        gridSize = closestSize;
                        _gridSize = closestSize;
                        if (_gridSizeLabel != null) _gridSizeLabel.Text = $"Grid Size: {gridSize}";
                    }
                    
                    // Load pan speed
                    if (settings.TryGetProperty("PanSpeed", out var panSpeedElement))
                    {
                        cameraPanSpeed = MathHelper.Clamp(panSpeedElement.GetSingle(), 200f, 2000f);
                        if (_panSpeedSlider != null) _panSpeedSlider.Value = cameraPanSpeed;
                        if (_panSpeedLabel != null) _panSpeedLabel.Text = $"Cam to Player Speed: {cameraPanSpeed:F0}";
                        SetCameraPanSpeed?.Invoke(cameraPanSpeed);
                    }
                    
                    // Load camera inertia
                    if (settings.TryGetProperty("CameraInertia", out var cameraInertiaElement))
                    {
                        cameraInertia = MathHelper.Clamp(cameraInertiaElement.GetSingle(), 0f, 0.995f);
                        if (_cameraInertiaSlider != null) _cameraInertiaSlider.Value = cameraInertia;
                        if (_cameraInertiaLabel != null) _cameraInertiaLabel.Text = $"Camera Inertia: {cameraInertia:F2}";
                        SetCameraInertia?.Invoke(cameraInertia);
                    }
                    
                    // Load music volume
                    if (settings.TryGetProperty("MusicVolume", out var musicVolumeElement))
                    {
                        musicVolume = MathHelper.Clamp(musicVolumeElement.GetSingle(), 0f, 1f);
                        _musicVolume = musicVolume;
                        if (_musicVolumeSlider != null) _musicVolumeSlider.Value = musicVolume;
                        if (_musicVolumeLabel != null) _musicVolumeLabel.Text = $"Music Volume: {(musicVolume * 100f):F0}%";
                        var musicInstance = GetBackgroundMusicInstance?.Invoke();
                        if (musicInstance != null) musicInstance.Volume = musicVolume;
                    }
                    
                    // Load SFX volume
                    if (settings.TryGetProperty("SFXVolume", out var sfxVolumeElement))
                    {
                        sfxVolume = MathHelper.Clamp(sfxVolumeElement.GetSingle(), 0f, 1f);
                        _sfxVolume = sfxVolume;
                        if (_sfxVolumeSlider != null) _sfxVolumeSlider.Value = sfxVolume;
                        if (_sfxVolumeLabel != null) _sfxVolumeLabel.Text = $"SFX Volume: {(sfxVolume * 100f):F0}%";
                        OnSFXSettingsChanged?.Invoke(sfxVolume, _sfxEnabled);
                    }
                }
            }
            catch (Exception ex)
            {
                System.Console.WriteLine($"Failed to load settings: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Save settings to file
        /// </summary>
        public void SaveSettings(
            int currentShipClassIndex,
            float cameraSpeed,
            float cameraZoom,
            int gridSize,
            float cameraPanSpeed,
            float cameraInertia,
            float musicVolume,
            float sfxVolume)
        {
            try
            {
                var settings = new
                {
                    CurrentShipClass = currentShipClassIndex,
                    CameraSpeed = cameraSpeed,
                    Zoom = cameraZoom,
                    GridSize = gridSize,
                    PanSpeed = cameraPanSpeed,
                    CameraInertia = cameraInertia,
                    MusicVolume = musicVolume,
                    SFXVolume = sfxVolume
                };
                
                var json = JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true });
                var filePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "settings.json");
                File.WriteAllText(filePath, json);
                
                ShowSaveConfirmation(true);
            }
            catch (Exception ex)
            {
                System.Console.WriteLine($"Failed to save settings: {ex.Message}");
                ShowSaveConfirmation(false);
            }
        }
        
        /// <summary>
        /// Load panel settings
        /// </summary>
        public void LoadPanelSettings(
            ref bool uiVisible,
            ref bool behaviorTextVisible,
            ref bool enemyPathVisible,
            ref bool enemyTargetPathVisible,
            ref bool avoidanceRangeVisible,
            ref bool pathfindingGridVisible,
            ref bool gridVisible,
            ref bool minimapVisible,
            ref bool musicEnabled,
            ref bool sfxEnabled,
            Action<bool>? setMinimapVisible = null)
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
                        _uiVisible = uiVisible;
                        if (_desktop?.Root != null) _desktop.Root.Visible = uiVisible;
                        if (_saveButtonDesktop?.Root != null) _saveButtonDesktop.Root.Visible = uiVisible;
                    }
                    
                    // Load behavior text visibility
                    if (settings.TryGetProperty("BehaviorTextVisible", out var behaviorTextVisibleElement))
                    {
                        behaviorTextVisible = behaviorTextVisibleElement.GetBoolean();
                        _behaviorTextVisible = behaviorTextVisible;
                    }
                    
                    // Load enemy path visibility
                    if (settings.TryGetProperty("EnemyPathVisible", out var enemyPathVisibleElement))
                    {
                        enemyPathVisible = enemyPathVisibleElement.GetBoolean();
                        _enemyPathVisible = enemyPathVisible;
                        if (_enemyPathVisibleCheckBox != null) _enemyPathVisibleCheckBox.IsChecked = enemyPathVisible;
                    }
                    
                    // Load enemy target path visibility
                    if (settings.TryGetProperty("EnemyTargetPathVisible", out var enemyTargetPathVisibleElement))
                    {
                        enemyTargetPathVisible = enemyTargetPathVisibleElement.GetBoolean();
                        _enemyTargetPathVisible = enemyTargetPathVisible;
                        if (_enemyTargetPathVisibleCheckBox != null) _enemyTargetPathVisibleCheckBox.IsChecked = enemyTargetPathVisible;
                    }
                    
                    // Load avoidance range visibility
                    if (settings.TryGetProperty("AvoidanceRangeVisible", out var avoidanceRangeVisibleElement))
                    {
                        avoidanceRangeVisible = avoidanceRangeVisibleElement.GetBoolean();
                        _avoidanceRangeVisible = avoidanceRangeVisible;
                        if (_avoidanceRangeVisibleCheckBox != null) _avoidanceRangeVisibleCheckBox.IsChecked = avoidanceRangeVisible;
                    }
                    
                    // Load pathfinding grid visibility
                    if (settings.TryGetProperty("PathfindingGridVisible", out var pathfindingGridVisibleElement))
                    {
                        pathfindingGridVisible = pathfindingGridVisibleElement.GetBoolean();
                        _pathfindingGridVisible = pathfindingGridVisible;
                    }
                    
                    // Load grid visibility
                    if (settings.TryGetProperty("GridVisible", out var gridVisibleElement))
                    {
                        gridVisible = gridVisibleElement.GetBoolean();
                        _gridVisible = gridVisible;
                        if (_gridVisibleCheckBox != null) _gridVisibleCheckBox.IsChecked = gridVisible;
                    }
                    
                    // Load minimap visibility
                    if (settings.TryGetProperty("MinimapVisible", out var minimapVisibleElement))
                    {
                        minimapVisible = minimapVisibleElement.GetBoolean();
                        setMinimapVisible?.Invoke(minimapVisible);
                    }
                    
                    // Load music enabled
                    if (settings.TryGetProperty("MusicEnabled", out var musicEnabledElement))
                    {
                        musicEnabled = musicEnabledElement.GetBoolean();
                        _musicEnabled = musicEnabled;
                        if (_musicEnabledCheckBox != null) _musicEnabledCheckBox.IsChecked = musicEnabled;
                        var musicInstance = GetBackgroundMusicInstance?.Invoke();
                        if (musicInstance != null)
                        {
                            if (musicEnabled)
                            {
                                musicInstance.Volume = _musicVolume;
                                if (musicInstance.State == SoundState.Stopped) musicInstance.Play();
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
                        sfxEnabled = sfxEnabledElement.GetBoolean();
                        _sfxEnabled = sfxEnabled;
                        if (_sfxEnabledCheckBox != null) _sfxEnabledCheckBox.IsChecked = sfxEnabled;
                        OnSFXSettingsChanged?.Invoke(_sfxVolume, sfxEnabled);
                        
                        var flySound = GetShipFlySound?.Invoke();
                        var idleSound = GetShipIdleSound?.Invoke();
                        if (flySound != null) flySound.Volume = sfxEnabled ? _sfxVolume * 0.8f : 0f;
                        if (idleSound != null) idleSound.Volume = sfxEnabled ? _sfxVolume : 0f;
                    }
                }
            }
            catch (Exception ex)
            {
                System.Console.WriteLine($"Failed to load panel settings: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Save panel settings
        /// </summary>
        public void SavePanelSettings(
            bool uiVisible,
            bool behaviorTextVisible,
            bool enemyPathVisible,
            bool enemyTargetPathVisible,
            bool avoidanceRangeVisible,
            bool pathfindingGridVisible,
            bool gridVisible,
            bool minimapVisible,
            bool musicEnabled,
            bool sfxEnabled)
        {
            try
            {
                var panelSettings = new
                {
                    UIVisible = uiVisible,
                    BehaviorTextVisible = behaviorTextVisible,
                    EnemyPathVisible = enemyPathVisible,
                    EnemyTargetPathVisible = enemyTargetPathVisible,
                    AvoidanceRangeVisible = avoidanceRangeVisible,
                    PathfindingGridVisible = pathfindingGridVisible,
                    GridVisible = gridVisible,
                    MinimapVisible = minimapVisible,
                    MusicEnabled = musicEnabled,
                    SFXEnabled = sfxEnabled
                };
                
                var json = JsonSerializer.Serialize(panelSettings, new JsonSerializerOptions { WriteIndented = true });
                var filePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "settings_Panel.json");
                File.WriteAllText(filePath, json);
            }
            catch (Exception ex)
            {
                System.Console.WriteLine($"Failed to save panel settings: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Load current ship settings and update UI
        /// </summary>
        public void LoadCurrentShipSettings(
            PlayerShip? playerShip,
            int currentShipClassIndex,
            ref float avoidanceDetectionRange,
            ref float shipIdleRate)
        {
            if (playerShip == null) return;
            
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
                        if (_speedSlider != null) _speedSlider.Value = shipSpeed;
                        if (_speedLabel != null) _speedLabel.Text = $"Ship Speed: {shipSpeed:F0}";
                    }
                    
                    // Load turn rate
                    if (settings.TryGetProperty("TurnRate", out var turnRateElement))
                    {
                        var turnRate = turnRateElement.GetSingle();
                        playerShip.RotationSpeed = turnRate;
                        if (_turnRateSlider != null) _turnRateSlider.Value = turnRate;
                        if (_turnRateLabel != null) _turnRateLabel.Text = $"Ship Turn Rate: {turnRate:F1}";
                    }
                    
                    // Load inertia
                    if (settings.TryGetProperty("Inertia", out var inertiaElement))
                    {
                        var inertia = inertiaElement.GetSingle();
                        playerShip.Inertia = inertia;
                        if (_inertiaSlider != null) _inertiaSlider.Value = inertia;
                        if (_inertiaLabel != null) _inertiaLabel.Text = $"Ship Inertia: {inertia:F2}";
                    }
                    
                    // Load aim rotation speed
                    if (settings.TryGetProperty("AimRotationSpeed", out var aimRotationSpeedElement))
                    {
                        var aimRotationSpeed = aimRotationSpeedElement.GetSingle();
                        playerShip.AimRotationSpeed = aimRotationSpeed;
                        if (_aimRotationSpeedSlider != null) _aimRotationSpeedSlider.Value = aimRotationSpeed;
                        if (_aimRotationSpeedLabel != null) _aimRotationSpeedLabel.Text = $"Ship Idle Rotation Speed: {aimRotationSpeed:F1}";
                    }
                    
                    // Load drift
                    if (settings.TryGetProperty("Drift", out var driftElement))
                    {
                        var drift = driftElement.GetSingle();
                        playerShip.Drift = drift;
                        if (_driftSlider != null) _driftSlider.Value = drift;
                        if (_driftLabel != null) _driftLabel.Text = $"Ship Drift: {drift:F2}";
                    }
                    
                    // Load avoidance detection range
                    if (settings.TryGetProperty("AvoidanceDetectionRange", out var avoidanceRangeElement))
                    {
                        avoidanceDetectionRange = MathHelper.Clamp(avoidanceRangeElement.GetSingle(), 100f, 1000f);
                        _avoidanceDetectionRange = avoidanceDetectionRange;
                        if (_avoidanceRangeSlider != null) _avoidanceRangeSlider.Value = avoidanceDetectionRange;
                        if (_avoidanceRangeLabel != null) _avoidanceRangeLabel.Text = $"Avoidance Detection Range: {avoidanceDetectionRange:F0}";
                    }
                    
                    // Load ship idle rate
                    if (settings.TryGetProperty("ShipIdleRate", out var shipIdleRateElement))
                    {
                        shipIdleRate = MathHelper.Clamp(shipIdleRateElement.GetSingle(), 0f, 1f);
                        _shipIdleRate = shipIdleRate;
                        if (_shipIdleRateSlider != null) _shipIdleRateSlider.Value = shipIdleRate;
                        if (_shipIdleRateLabel != null) _shipIdleRateLabel.Text = $"Ship Idle Rate: {(shipIdleRate * 100f):F0}%";
                    }
                    
                    // Load look-ahead distance
                    if (settings.TryGetProperty("LookAheadDistance", out var lookAheadDistanceElement))
                    {
                        var lookAheadDistance = MathHelper.Clamp(lookAheadDistanceElement.GetSingle(), 0.5f, 5.0f);
                        playerShip.LookAheadDistance = lookAheadDistance;
                        if (_lookAheadSlider != null) _lookAheadSlider.Value = lookAheadDistance;
                        if (_lookAheadLabel != null) _lookAheadLabel.Text = $"Look-Ahead Distance: {lookAheadDistance:F2}x";
                    }
                    
                    // Load look-ahead visible
                    if (settings.TryGetProperty("LookAheadVisible", out var lookAheadVisibleElement))
                    {
                        var lookAheadVisible = lookAheadVisibleElement.GetBoolean();
                        playerShip.LookAheadVisible = lookAheadVisible;
                        if (_lookAheadVisibleCheckBox != null) _lookAheadVisibleCheckBox.IsChecked = lookAheadVisible;
                    }
                }
            }
            catch (Exception ex)
            {
                System.Console.WriteLine($"Failed to load {className} settings: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Save current ship settings
        /// </summary>
        public void SaveCurrentShipSettings(
            PlayerShip? playerShip,
            int currentShipClassIndex,
            float avoidanceDetectionRange,
            float shipIdleRate)
        {
            if (playerShip == null) return;
            
            string className = currentShipClassIndex == 0 ? "PlayerShip" : (currentShipClassIndex == 1 ? "FriendlyShip" : "EnemyShip");
            var filePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, $"settings_{className}.json");
            
            try
            {
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
                }
            }
            catch (Exception ex)
            {
                System.Console.WriteLine($"Failed to save {className} settings: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Update UI sliders when ship class changes
        /// </summary>
        public void UpdateUIForShipClass(PlayerShip? playerShip)
        {
            if (playerShip == null) return;
            
            if (_speedSlider != null)
            {
                _speedSlider.Value = playerShip.MoveSpeed;
                if (_speedLabel != null) _speedLabel.Text = $"Ship Speed: {playerShip.MoveSpeed:F0}";
            }
            
            if (_turnRateSlider != null)
            {
                _turnRateSlider.Value = playerShip.RotationSpeed;
                if (_turnRateLabel != null) _turnRateLabel.Text = $"Ship Turn Rate: {playerShip.RotationSpeed:F1}";
            }
            
            if (_aimRotationSpeedSlider != null)
            {
                _aimRotationSpeedSlider.Value = playerShip.AimRotationSpeed;
                if (_aimRotationSpeedLabel != null) _aimRotationSpeedLabel.Text = $"Ship Idle Rotation Speed: {playerShip.AimRotationSpeed:F1}";
            }
            
            if (_inertiaSlider != null)
            {
                _inertiaSlider.Value = playerShip.Inertia;
                if (_inertiaLabel != null) _inertiaLabel.Text = $"Ship Inertia: {playerShip.Inertia:F2}";
            }
            
            if (_driftSlider != null)
            {
                _driftSlider.Value = playerShip.Drift;
                if (_driftLabel != null) _driftLabel.Text = $"Ship Drift: {playerShip.Drift:F2}";
            }
            
            if (_lookAheadSlider != null)
            {
                _lookAheadSlider.Value = playerShip.LookAheadDistance;
                if (_lookAheadLabel != null) _lookAheadLabel.Text = $"Look-Ahead Distance: {playerShip.LookAheadDistance:F2}x";
            }
            
            if (_lookAheadVisibleCheckBox != null)
            {
                _lookAheadVisibleCheckBox.IsChecked = playerShip.LookAheadVisible;
            }
        }
        
        /// <summary>
        /// Render UI
        /// </summary>
        public void Render()
        {
            if (_uiVisible)
            {
                _desktop?.Render();
                _saveButtonDesktop?.Render();
            }
            
            if (_uiGridVisible && _coordinateDesktop != null)
            {
                _coordinateDesktop.Render();
            }
            
            if (_isPreviewActive && _previewDesktop != null)
            {
                _previewDesktop.Render();
            }
        }
        
        /// <summary>
        /// Draw preview screen
        /// </summary>
        public void DrawPreview(SpriteBatch spriteBatch, GraphicsDevice graphicsDevice)
        {
            if (!_isPreviewActive) return;
            
            Texture2D? shipTexture = _previewShipIndex == 0 ? _previewShip1Texture : 
                                     (_previewShipIndex == 1 ? _previewShip2Texture : _previewShip1Texture);
            
            if (shipTexture == null || _previewDesktop == null) return;
            
            spriteBatch.Begin();
            
            // Draw semi-transparent background
            var bgTexture = new Texture2D(graphicsDevice, 1, 1);
            bgTexture.SetData(new[] { new Color(0, 0, 0, 200) });
            int panelX = (graphicsDevice.Viewport.Width - 500) / 2;
            int panelY = (graphicsDevice.Viewport.Height - 500) / 2;
            spriteBatch.Draw(bgTexture, new Rectangle(panelX, panelY, 500, 500), Color.White);
            
            // Draw ship sprite centered in preview panel
            int spriteX = panelX + 250 - (shipTexture.Width / 2);
            int spriteY = panelY + 250 - (shipTexture.Height / 2);
            
            // Draw box around sprite
            if (GridPixelTexture != null)
            {
                int borderThickness = 2;
                Color borderColor = Color.White;
                
                // Top line
                spriteBatch.Draw(
                    GridPixelTexture,
                    new Rectangle(spriteX - borderThickness, spriteY - borderThickness, shipTexture.Width + borderThickness * 2, borderThickness),
                    borderColor
                );
                
                // Bottom line
                spriteBatch.Draw(
                    GridPixelTexture,
                    new Rectangle(spriteX - borderThickness, spriteY + shipTexture.Height, shipTexture.Width + borderThickness * 2, borderThickness),
                    borderColor
                );
                
                // Left line
                spriteBatch.Draw(
                    GridPixelTexture,
                    new Rectangle(spriteX - borderThickness, spriteY - borderThickness, borderThickness, shipTexture.Height + borderThickness * 2),
                    borderColor
                );
                
                // Right line
                spriteBatch.Draw(
                    GridPixelTexture,
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
            
            // Render preview UI
            _previewDesktop.Render();
        }
    }
}

