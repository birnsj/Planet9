using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Audio;
using Microsoft.Xna.Framework.Content;
using Planet9.Entities;

namespace Planet9.Managers
{
    /// <summary>
    /// Manages all audio (music and SFX) in the game scene
    /// </summary>
    public class AudioManager
    {
        private SoundEffectInstance? _backgroundMusicInstance;
        private SoundEffectInstance? _shipFlySound;
        private SoundEffectInstance? _shipIdleSound;
        
        // Callbacks for getting UI settings
        public Func<float>? GetMusicVolume { get; set; }
        public Func<float>? GetSFXVolume { get; set; }
        public Func<bool>? GetMusicEnabled { get; set; }
        public Func<bool>? GetSFXEnabled { get; set; }
        
        /// <summary>
        /// Get background music instance
        /// </summary>
        public SoundEffectInstance? BackgroundMusicInstance => _backgroundMusicInstance;
        
        /// <summary>
        /// Get ship fly sound instance
        /// </summary>
        public SoundEffectInstance? ShipFlySound => _shipFlySound;
        
        /// <summary>
        /// Get ship idle sound instance
        /// </summary>
        public SoundEffectInstance? ShipIdleSound => _shipIdleSound;
        
        /// <summary>
        /// Load and initialize background music
        /// </summary>
        public void LoadBackgroundMusic(ContentManager content, string musicAssetName)
        {
            try
            {
                var musicEffect = content.Load<SoundEffect>(musicAssetName);
                if (musicEffect != null)
                {
                    _backgroundMusicInstance = musicEffect.CreateInstance();
                    _backgroundMusicInstance.IsLooped = true;
                    var musicVolume = GetMusicVolume?.Invoke() ?? 0.5f;
                    _backgroundMusicInstance.Volume = musicVolume;
                    _backgroundMusicInstance.Play();
                    System.Console.WriteLine($"[MUSIC] {musicAssetName} loaded and playing. State: {_backgroundMusicInstance.State}, Volume: {_backgroundMusicInstance.Volume}");
                }
            }
            catch (Exception ex)
            {
                System.Console.WriteLine($"[MUSIC ERROR] Failed to load background music: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Load ship sound effects (idle and fly)
        /// </summary>
        public void LoadShipSounds(ContentManager content, string idleSoundAssetName, string flySoundAssetName)
        {
            try
            {
                var idleSound = content.Load<SoundEffect>(idleSoundAssetName);
                System.Console.WriteLine($"[SHIP SOUND] Idle sound loaded: {idleSound != null}");
                if (idleSound != null)
                {
                    _shipIdleSound = idleSound.CreateInstance();
                    _shipIdleSound.IsLooped = true;
                    var sfxEnabled = GetSFXEnabled?.Invoke() ?? true;
                    var sfxVolume = GetSFXVolume?.Invoke() ?? 1.0f;
                    _shipIdleSound.Volume = sfxEnabled ? sfxVolume : 0f;
                    if (sfxEnabled)
                    {
                        _shipIdleSound.Play();
                    }
                    System.Console.WriteLine($"[SHIP SOUND] Idle sound playing. State: {_shipIdleSound.State}, Volume: {_shipIdleSound.Volume}");
                }
                
                var flySound = content.Load<SoundEffect>(flySoundAssetName);
                System.Console.WriteLine($"[SHIP SOUND] Fly sound loaded: {flySound != null}");
                if (flySound != null)
                {
                    _shipFlySound = flySound.CreateInstance();
                    _shipFlySound.IsLooped = true;
                    var sfxEnabled2 = GetSFXEnabled?.Invoke() ?? true;
                    var sfxVolume2 = GetSFXVolume?.Invoke() ?? 1.0f;
                    _shipFlySound.Volume = sfxEnabled2 ? sfxVolume2 * 0.8f : 0f; // 20% lower than SFX volume
                    System.Console.WriteLine($"[SHIP SOUND] Fly sound instance created. Volume: {_shipFlySound.Volume}");
                }
            }
            catch (Exception ex)
            {
                System.Console.WriteLine($"[SHIP SOUND ERROR] Failed to load ship sound effects: {ex.Message}");
                System.Console.WriteLine($"[SHIP SOUND ERROR] Stack trace: {ex.StackTrace}");
            }
        }
        
        /// <summary>
        /// Update music volume
        /// </summary>
        public void UpdateMusicVolume()
        {
            if (_backgroundMusicInstance != null)
            {
                var musicEnabled = GetMusicEnabled?.Invoke() ?? true;
                var musicVolume = GetMusicVolume?.Invoke() ?? 0.5f;
                _backgroundMusicInstance.Volume = musicEnabled ? musicVolume : 0f;
            }
        }
        
        /// <summary>
        /// Update SFX volume
        /// </summary>
        public void UpdateSFXVolume()
        {
            var sfxEnabled = GetSFXEnabled?.Invoke() ?? true;
            var sfxVolume = GetSFXVolume?.Invoke() ?? 1.0f;
            
            if (_shipIdleSound != null)
            {
                _shipIdleSound.Volume = sfxEnabled ? sfxVolume : 0f;
            }
            
            if (_shipFlySound != null)
            {
                _shipFlySound.Volume = sfxEnabled ? sfxVolume * 0.8f : 0f; // 20% lower than SFX volume
            }
        }
        
        /// <summary>
        /// Restart music if it stops unexpectedly
        /// </summary>
        public void UpdateMusic()
        {
            if (_backgroundMusicInstance != null && _backgroundMusicInstance.State == SoundState.Stopped)
            {
                try
                {
                    var musicEnabled = GetMusicEnabled?.Invoke() ?? true;
                    if (musicEnabled)
                    {
                        _backgroundMusicInstance.Play();
                        System.Console.WriteLine($"[MUSIC] Restarted galaxy music (was stopped)");
                    }
                }
                catch (Exception ex)
                {
                    System.Console.WriteLine($"[MUSIC ERROR] Failed to restart: {ex.Message}");
                }
            }
        }
        
        /// <summary>
        /// Update ship sound effects based on movement state
        /// </summary>
        public void UpdateShipSounds(PlayerShip? playerShip)
        {
            if (playerShip == null) return;
            
            var sfxEnabled = GetSFXEnabled?.Invoke() ?? true;
            if (!sfxEnabled) return;
            
            // Check if ship is actively moving forward (not just coasting from inertia)
            bool isMovingForward = playerShip.IsActivelyMoving();
            
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
        
        /// <summary>
        /// Dispose all audio resources
        /// </summary>
        public void Dispose()
        {
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
        }
    }
}

