using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace Planet9.Managers
{
    /// <summary>
    /// Manages keyboard and mouse input state and provides callbacks for input events
    /// </summary>
    public class InputManager
    {
        private KeyboardState _previousKeyboardState;
        private MouseState _previousMouseState;
        private bool _wasLeftButtonPressed = false;
        private bool _wasRightButtonPressed = false;
        private bool _isFollowingMouse = false;
        private Vector2 _clickStartPosition;
        
        // Callbacks for coordinate conversion
        public Func<Vector2, Vector2>? ScreenToWorld { get; set; }
        
        // Current input state
        public KeyboardState CurrentKeyboardState { get; private set; }
        public MouseState CurrentMouseState { get; private set; }
        
        // Input state flags
        public bool IsLeftButtonPressed => CurrentMouseState.LeftButton == ButtonState.Pressed;
        public bool IsRightButtonPressed => CurrentMouseState.RightButton == ButtonState.Pressed;
        public bool WasLeftButtonJustPressed => IsLeftButtonPressed && !_wasLeftButtonPressed;
        public bool WasLeftButtonJustReleased => !IsLeftButtonPressed && _wasLeftButtonPressed;
        public bool WasRightButtonJustPressed => IsRightButtonPressed && !_wasRightButtonPressed;
        public bool IsFollowingMouse => _isFollowingMouse;
        public Vector2 MouseScreenPosition => new Vector2(CurrentMouseState.X, CurrentMouseState.Y);
        public Vector2 MouseWorldPosition
        {
            get
            {
                if (ScreenToWorld != null)
                {
                    return ScreenToWorld(MouseScreenPosition);
                }
                return MouseScreenPosition;
            }
        }
        
        /// <summary>
        /// Check if a key was just pressed (not held)
        /// </summary>
        public bool IsKeyJustPressed(Keys key)
        {
            return CurrentKeyboardState.IsKeyDown(key) && !_previousKeyboardState.IsKeyDown(key);
        }
        
        /// <summary>
        /// Check if a key is currently held down
        /// </summary>
        public bool IsKeyDown(Keys key)
        {
            return CurrentKeyboardState.IsKeyDown(key);
        }
        
        /// <summary>
        /// Check if mouse is within window bounds
        /// </summary>
        public bool IsMouseInWindow(Viewport viewport)
        {
            return CurrentMouseState.X >= 0 && CurrentMouseState.X < viewport.Width &&
                   CurrentMouseState.Y >= 0 && CurrentMouseState.Y < viewport.Height;
        }
        
        /// <summary>
        /// Check if mouse is over UI area
        /// </summary>
        public bool IsMouseOverUI(Viewport viewport, bool uiVisible)
        {
            if (!uiVisible) return false;
            
            // UI area is roughly 0-250 width, 0-800 height in top-left corner
            bool isMouseOverUI = CurrentMouseState.X >= 0 && CurrentMouseState.X <= 250 && 
                                CurrentMouseState.Y >= 0 && CurrentMouseState.Y <= 800;
            
            // Check if mouse is over save button area (bottom right)
            bool isMouseOverSaveButton = CurrentMouseState.X >= viewport.Width - 160 && 
                                        CurrentMouseState.X <= viewport.Width &&
                                        CurrentMouseState.Y >= viewport.Height - 50 && 
                                        CurrentMouseState.Y <= viewport.Height;
            
            return isMouseOverUI || isMouseOverSaveButton;
        }
        
        /// <summary>
        /// Get mouse wheel scroll delta
        /// </summary>
        public int GetScrollDelta()
        {
            return CurrentMouseState.ScrollWheelValue - _previousMouseState.ScrollWheelValue;
        }
        
        /// <summary>
        /// Update input state - call this at the start of each frame
        /// </summary>
        public void Update()
        {
            CurrentKeyboardState = Keyboard.GetState();
            CurrentMouseState = Mouse.GetState();
        }
        
        /// <summary>
        /// Update input state and handle mouse following logic
        /// </summary>
        public void UpdateMouseFollowing(bool isMouseOverUI, float dragThreshold = 5f)
        {
            if (isMouseOverUI && _isFollowingMouse)
            {
                _isFollowingMouse = false;
            }
            
            if (!isMouseOverUI && IsLeftButtonPressed)
            {
                var screenPos = MouseScreenPosition;
                
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
                    if (moveDistance > dragThreshold)
                    {
                        _isFollowingMouse = true;
                    }
                }
            }
            else if (!isMouseOverUI && WasLeftButtonJustReleased)
            {
                _isFollowingMouse = false;
            }
            
            // Only update left button state here (for mouse following)
            // Right button state is updated in SaveState() at the end of the frame
            _wasLeftButtonPressed = IsLeftButtonPressed;
        }
        
        /// <summary>
        /// Save current state as previous - call this at the end of each frame
        /// </summary>
        public void SaveState()
        {
            _previousKeyboardState = CurrentKeyboardState;
            _previousMouseState = CurrentMouseState;
            // Update button states for next frame's "just pressed" detection
            _wasLeftButtonPressed = IsLeftButtonPressed;
            _wasRightButtonPressed = IsRightButtonPressed;
        }
        
        /// <summary>
        /// Convert screen coordinates to world coordinates
        /// </summary>
        public Vector2 ConvertScreenToWorld(Vector2 screenPos, Viewport viewport, float cameraZoom, Vector2 cameraPosition)
        {
            if (ScreenToWorld != null)
            {
                return ScreenToWorld(screenPos);
            }
            
            // Fallback conversion
            var worldX = (screenPos.X - viewport.Width / 2f) / cameraZoom + cameraPosition.X;
            var worldY = (screenPos.Y - viewport.Height / 2f) / cameraZoom + cameraPosition.Y;
            return new Vector2(worldX, worldY);
        }
        
        /// <summary>
        /// Reset mouse following state
        /// </summary>
        public void ResetMouseFollowing()
        {
            _isFollowingMouse = false;
        }
    }
}

