using NAudio.Wave;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace VirtualCamStudio.Services
{
    /// <summary>
    /// Represents an audio output device.
    /// </summary>
    public class AudioDevice
    {
        public int DeviceNumber { get; set; }
        public string Name { get; set; } = string.Empty;
        public string FriendlyName => $"{Name}";
    }

    /// <summary>
    /// Manages audio playback to a selectable audio device for CloudPhone microphone input.
    /// </summary>
    public class AudioPlayerService : IDisposable
    {
        private AudioFileReader? _audioFile;
        private WaveOutEvent? _outputDevice;
        private string? _currentFilePath;
        private bool _isLooping;
        private bool _disposed;
        private int _selectedDeviceNumber = -1; // -1 = default device

        public bool IsPlaying => _outputDevice?.PlaybackState == PlaybackState.Playing;
        public bool IsPaused => _outputDevice?.PlaybackState == PlaybackState.Paused;
        public bool HasAudio => _audioFile != null;
        public string? CurrentFileName => _currentFilePath != null ? Path.GetFileName(_currentFilePath) : null;

        public event EventHandler? PlaybackStopped;

        public AudioPlayerService()
        {
        }

        /// <summary>
        /// Gets all available audio output devices.
        /// </summary>
        public static List<AudioDevice> GetAvailableDevices()
        {
            var devices = new List<AudioDevice>();

            // Add default device
            devices.Add(new AudioDevice
            {
                DeviceNumber = -1,
                Name = "Default Audio Device"
            });

            // Add all available WaveOut devices
            for (int i = 0; i < WaveOut.DeviceCount; i++)
            {
                try
                {
                    var capabilities = WaveOut.GetCapabilities(i);
                    devices.Add(new AudioDevice
                    {
                        DeviceNumber = i,
                        Name = capabilities.ProductName
                    });
                }
                catch
                {
                    // Skip devices that can't be queried
                }
            }

            return devices;
        }

        /// <summary>
        /// Sets the audio output device.
        /// </summary>
        public void SetOutputDevice(int deviceNumber)
        {
            _selectedDeviceNumber = deviceNumber;

            // If audio is currently loaded, reload it with the new device
            if (_currentFilePath != null && _audioFile != null)
            {
                var wasPlaying = IsPlaying;
                var position = _audioFile.Position;

                LoadAudio(_currentFilePath);

                if (_audioFile != null)
                {
                    _audioFile.Position = Math.Min(position, _audioFile.Length);
                    if (wasPlaying)
                    {
                        Play();
                    }
                }
            }
        }

        /// <summary>
        /// Loads an audio file (MP3, WAV, etc.).
        /// </summary>
        public bool LoadAudio(string filePath)
        {
            try
            {
                // Stop and dispose previous audio
                Stop();
                _audioFile?.Dispose();
                _outputDevice?.Dispose();

                // Load new audio file
                _audioFile = new AudioFileReader(filePath);
                _currentFilePath = filePath;

                // Create output device with selected device number
                _outputDevice = new WaveOutEvent();
                if (_selectedDeviceNumber >= 0)
                {
                    _outputDevice.DeviceNumber = _selectedDeviceNumber;
                }
                // else use default device (DeviceNumber defaults to -1)

                _outputDevice.Init(_audioFile);
                _outputDevice.PlaybackStopped += OnPlaybackStopped;

                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>
        /// Starts or resumes audio playback.
        /// </summary>
        public void Play()
        {
            if (_outputDevice == null || _audioFile == null)
                return;

            if (_outputDevice.PlaybackState == PlaybackState.Stopped)
            {
                // Restart from beginning if stopped
                _audioFile.Position = 0;
            }

            _outputDevice.Play();
        }

        /// <summary>
        /// Pauses audio playback.
        /// </summary>
        public void Pause()
        {
            _outputDevice?.Pause();
        }

        /// <summary>
        /// Stops audio playback and resets to beginning.
        /// </summary>
        public void Stop()
        {
            if (_outputDevice == null || _audioFile == null)
                return;

            _outputDevice.Stop();
            _audioFile.Position = 0;
        }

        /// <summary>
        /// Enables or disables loop mode.
        /// </summary>
        public void SetLoop(bool enabled)
        {
            _isLooping = enabled;
        }

        private void OnPlaybackStopped(object? sender, StoppedEventArgs e)
        {
            if (_isLooping && _audioFile != null)
            {
                // Loop: restart from beginning
                _audioFile.Position = 0;
                _outputDevice?.Play();
            }
            else
            {
                // Not looping: notify UI
                PlaybackStopped?.Invoke(this, EventArgs.Empty);
            }
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            Stop();
            _audioFile?.Dispose();
            _outputDevice?.Dispose();
            _disposed = true;
        }
    }
}
