using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Configuration;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using Microsoft.Win32;
using OpenCvSharp;
using VirtualCamStudio.Camera;
using VirtualCamStudio.Core;
using VirtualCamStudio.Helpers;
using VirtualCamStudio.Models;
using VirtualCamStudio.Services;
using VirtualCamStudio.Services.Rendering;
using VirtualCamStudio.Outputs;
using Cv2 = OpenCvSharp.Cv2;
using WpfPoint = System.Windows.Point;

namespace VirtualCamStudio
{
    /// <summary>
    /// Main application window.
    /// 
    /// Keyboard Shortcuts:
    /// - Delete: Remove selected media item
    /// - Ctrl+Delete: Clear entire media library
    /// - Arrow Up: Navigate to previous media item
    /// - Arrow Down: Navigate to next media item
    /// - Ctrl+A: Focus media library (select first item if none selected)
    /// </summary>
    public partial class MainWindow : System.Windows.Window
    {
        private readonly RenderService _renderService = new();
        private readonly CameraProfileService _profileService = new();
        private readonly Services.OutputManager _legacyOutputManager = new();  // Old Frame-based system (for OBS compatibility)
        private readonly Outputs.OutputManager _outputManager = new();  // New async Frame-based system (Commit 40/42)
        private readonly VirtualCameraService _virtualCamera = new();
        private Outputs.UnityCaptureOutput? _unityCaptureOutput;  // UnityCapture IPC output

        // Background sender process
        private readonly Services.SenderProcessManager _senderProcess = new();

        // Audio playback
        private readonly Services.AudioPlayerService _audioPlayer = new();

        // Rendering infrastructure (centralized via RenderPipeline)
        private readonly RenderLoop _renderLoop = new();
        private readonly Media.MediaController _mediaController = new();
        private readonly Media.ViewportEngine _viewportEngine = new();
        private RenderPipeline? _renderPipeline;

        // Video playback
        private readonly Media.VideoPlayer _videoPlayer = new();
        private Media.PlaybackEngine? _playbackEngine;

        // Current media state
        private Mat? _currentMediaFrame;  // Current frame in memory (image or video frame) - DEPRECATED, use MediaController
        private bool _isVideoActive = false;  // True if current media is a video
        // Framing settings now accessed via _renderPipeline.FramingSettings

        // Event suppression
        private bool _suppressSliderEvents = false;  // Prevent event cascade during programmatic updates

        // Mouse drag state
        private bool _isDragging = false;
        private WpfPoint _lastMousePosition = new WpfPoint(0, 0);

        /// <summary>
        /// The currently selected camera profile
        /// </summary>
        public CameraProfile? ActiveProfile { get; set; }

        /// <summary>
        /// Media library collection
        /// </summary>
        public ObservableCollection<MediaItem> MediaItems { get; set; } = new();

        public MainWindow()
        {
            try
            {

                InitializeComponent();

                MediaListBox.ItemsSource = MediaItems;

                // Register preview output (new Outputs system)
                var previewOutput = new Outputs.PreviewOutput(PreviewImage);
                _outputManager.Register(previewOutput);

                // Register legacy outputs for compatibility (old Services system)
                var legacyPreviewTarget = new Services.PreviewOutputTarget(PreviewImage);
                _legacyOutputManager.RegisterTarget(legacyPreviewTarget);
                _legacyOutputManager.RegisterTarget(_virtualCamera);

                // Initialize RenderPipeline (centralized rendering)
                _renderPipeline = new RenderPipeline(
                    _renderLoop,
                    _mediaController,
                    _viewportEngine,
                    _legacyOutputManager,  // Legacy Frame-based system for virtual camera compatibility
                    _outputManager);        // New async Frame-based system (Commit 40/42)

                // Share framing settings reference
                // The render pipeline will read from the shared _framingSettings

                // Register keyboard shortcuts
                KeyDown += MainWindow_KeyDown;

                // Register preview size changed handler for safe area overlay
                PreviewGrid.SizeChanged += PreviewGrid_SizeChanged;
            }
            catch (Exception ex)
            {
                string error = $"MainWindow Constructor Error: {ex.Message}\n\nStack Trace:\n{ex.StackTrace}";

                // Write to desktop log
                try
                {
                    string logPath = Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
                        "VirtualCamStudio_Error.log");
                    File.AppendAllText(logPath, $"\n[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {error}\n");
                }
                catch { }

                MessageBox.Show(
                    $"Failed to initialize main window:\n\n{ex.Message}\n\nCheck Desktop\\VirtualCamStudio_Error.log for details.",
                    "Initialization Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);

                throw; // Re-throw to let App.xaml.cs exception handler catch it
            }
        }

        private async void Window_Loaded(object sender, RoutedEventArgs e)
        {
            LoadAndPopulateCameraProfiles();

            // Load phone profiles for virtual camera simulation
            LoadAndPopulatePhoneProfiles();

            // Populate audio devices
            PopulateAudioDevices();

            // Start the background sender process asynchronously
            UnityCaptureSenderStatusText.Text = "Sender: Starting...";
            UnityCaptureSenderStatusText.Foreground = System.Windows.Media.Brushes.Gray;

            await System.Threading.Tasks.Task.Run(() =>
            {
                bool senderStarted = _senderProcess.Start();
                Dispatcher.Invoke(() =>
                {
                    if (senderStarted)
                    {
                        UnityCaptureSenderStatusText.Text = "Sender: Running";
                        UnityCaptureSenderStatusText.Foreground = System.Windows.Media.Brushes.Green;
                    }
                    else
                    {
                        UnityCaptureSenderStatusText.Text = "Sender: Failed to start";
                        UnityCaptureSenderStatusText.Foreground = System.Windows.Media.Brushes.Red;
                    }
                });
            });

            // Start the render pipeline
            if (_renderPipeline != null)
            {
                _renderPipeline.Start();
            }
        }

        private async void Window_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
        {
            // Stop the background sender process
            _senderProcess?.Stop();

            // Stop audio playback
            _audioPlayer?.Dispose();

            // Stop the render pipeline
            if (_renderPipeline != null)
            {
                _renderPipeline.Stop();
                _renderPipeline.Dispose();
            }

            // Dispose UnityCapture output
            if (_unityCaptureOutput != null)
            {
                _outputManager.Unregister(_unityCaptureOutput);
                _unityCaptureOutput.Dispose();
                _unityCaptureOutput = null;
            }

            // Stop video playback (async to prevent freeze)
            await StopVideoPlaybackAsync();

            // Dispose current media frame
            _currentMediaFrame?.Dispose();
            _currentMediaFrame = null;

            // Dispose video player
            _videoPlayer?.Dispose();

            // Dispose sender process manager
            _senderProcess?.Dispose();
        }

        /// <summary>
        /// Loads all camera profiles and populates the ComboBox.
        /// Creates a default profile if none exist.
        /// Restores the last selected profile from application settings.
        /// </summary>
        private void LoadAndPopulateCameraProfiles()
        {
            // Load all profiles from disk
            List<CameraProfile> profiles = _profileService.LoadProfiles();

            // If no profiles exist, create a default one
            if (profiles.Count == 0)
            {
                CreateDefaultProfile();
                profiles = _profileService.LoadProfiles();
            }

            // Clear existing ComboBox items
            CameraProfileComboBox.Items.Clear();

            // Populate ComboBox with profile names
            foreach (var profile in profiles)
            {
                CameraProfileComboBox.Items.Add(profile.Name);
            }

            // Restore last selected profile from settings file
            string lastSelectedProfile = LoadLastSelectedProfile();

            if (!string.IsNullOrEmpty(lastSelectedProfile))
            {
                int index = CameraProfileComboBox.Items.IndexOf(lastSelectedProfile);
                if (index >= 0)
                {
                    CameraProfileComboBox.SelectedIndex = index;
                }
                else if (CameraProfileComboBox.Items.Count > 0)
                {
                    // Fall back to first profile if last selected doesn't exist
                    CameraProfileComboBox.SelectedIndex = 0;
                }
            }
            else if (CameraProfileComboBox.Items.Count > 0)
            {
                // Select first profile if no previous selection
                CameraProfileComboBox.SelectedIndex = 0;
            }
        }

        /// <summary>
        /// Creates a default camera profile if none exist.
        /// </summary>
        private void CreateDefaultProfile()
        {
            var defaultProfile = new CameraProfile
            {
                Name = "Generic 1080x1920",
                Manufacturer = "Generic",
                Model = "Portrait 1080p",
                SensorWidth = 6.4,
                SensorHeight = 11.36,
                DisplayWidth = 1080,
                DisplayHeight = 1920,
                Rotation = 0,
                FPS = 30,
                DefaultZoom = 1.0,
                DefaultOffsetX = 0,
                DefaultOffsetY = 0,
                Notes = "Default profile for 1080x1920 portrait display"
            };

            _profileService.SaveProfile(defaultProfile);
        }

        /// <summary>
        /// Loads the last selected profile name from user settings.
        /// </summary>
        private string LoadLastSelectedProfile()
        {
            try
            {
                string settingsPath = GetSettingsFilePath();
                if (File.Exists(settingsPath))
                {
                    return File.ReadAllText(settingsPath).Trim();
                }
            }
            catch
            {
                // Silently fail if we can't read settings
            }
            return string.Empty;
        }

        /// <summary>
        /// Saves the last selected profile name to user settings.
        /// </summary>
        private void SaveLastSelectedProfile(string profileName)
        {
            try
            {
                string settingsPath = GetSettingsFilePath();
                string directory = Path.GetDirectoryName(settingsPath);
                if (!Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }
                File.WriteAllText(settingsPath, profileName);
            }
            catch
            {
                // Silently fail if we can't save settings
            }
        }

        /// <summary>
        /// Gets the path to the settings file in AppData.
        /// </summary>
        private string GetSettingsFilePath()
        {
            string appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            return Path.Combine(appDataPath, "VirtualCamStudio", "LastProfile.txt");
        }

        /// <summary>
        /// Loads all phone profiles and populates the PhoneProfileComboBox.
        /// Sets Redmi Flagship as the default.
        /// </summary>
        private void LoadAndPopulatePhoneProfiles()
        {
            // Get all available phone profiles
            List<PhoneProfile> profiles = PhoneProfileFactory.GetAllProfiles();

            // Clear existing items
            PhoneProfileComboBox.Items.Clear();

            // Populate ComboBox with profile names
            foreach (var profile in profiles)
            {
                PhoneProfileComboBox.Items.Add(profile.Name);
            }

            // Select Redmi Flagship as default (index 0)
            if (PhoneProfileComboBox.Items.Count > 0)
            {
                PhoneProfileComboBox.SelectedIndex = 0;

                // Load the default profile into the camera engine
                if (_renderPipeline != null)
                {
                    _renderPipeline.CameraEngine.LoadProfile(PhoneProfileFactory.GetDefault());
                }
            }
        }

        private void Window_DragOver(object sender, DragEventArgs e)
        {
            e.Effects = DragDropEffects.Copy;
            e.Handled = true;
        }

        private void Window_Drop(object sender, DragEventArgs e)
        {
            if (!e.Data.GetDataPresent(DataFormats.FileDrop))
                return;

            string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);

            if (files.Length == 0)
                return;

            bool anyAdded = false;

            foreach (string file in files)
            {
                string ext = Path.GetExtension(file).ToLowerInvariant();

                // Supported image formats
                if (ext == ".jpg" ||
                    ext == ".jpeg" ||
                    ext == ".png" ||
                    ext == ".bmp")
                {
                    AddMediaToLibrary(file);
                    anyAdded = true;
                }
                // Supported video formats
                else if (ext == ".mp4" ||
                         ext == ".mov" ||
                         ext == ".avi" ||
                         ext == ".mkv")
                {
                    AddMediaToLibrary(file);
                    anyAdded = true;
                }
                else
                {
                    // Unsupported file type - skip
                }
            }

            // If at least one file was added and we don't have an active source, load the first one
            if (anyAdded && MediaItems.Count > 0 && PreviewImage.Source == null)
            {
                MediaListBox.SelectedIndex = 0;
            }
        }

        /// <summary>
        /// Adds a media file to the library.
        /// Generates and caches thumbnail in memory at add time.
        /// Prevents duplicate entries by checking file path.
        /// </summary>
        private void AddMediaToLibrary(string filePath)
        {
            // Check if already in library
            foreach (var item in MediaItems)
            {
                if (item.FilePath.Equals(filePath, StringComparison.OrdinalIgnoreCase))
                    return;
            }

            // Create media item with thumbnail generated and stored in memory
            var mediaItem = new MediaItem
            {
                FilePath = filePath,
                FileName = Path.GetFileName(filePath),
                FileType = Path.GetExtension(filePath).TrimStart('.').ToUpper(),
                Thumbnail = CreateThumbnail(filePath) // Generated once, stored in memory
            };

            MediaItems.Add(mediaItem);
        }

        /// <summary>
        /// Creates a thumbnail for the given media file (image or video).
        /// For images: loads and scales the image.
        /// For videos: extracts and scales the first frame.
        /// Thumbnail is fully loaded into memory and the file handle is released.
        /// This ensures smooth scrolling without disk I/O.
        /// </summary>
        private BitmapSource? CreateThumbnail(string filePath)
        {
            try
            {
                string ext = Path.GetExtension(filePath).ToLowerInvariant();
                bool isVideo = ext == ".mp4" || ext == ".mov" || ext == ".avi" || ext == ".mkv";

                if (isVideo)
                {
                    return CreateVideoThumbnail(filePath);
                }
                else
                {
                    return CreateImageThumbnail(filePath);
                }
            }
            catch (Exception ex)
            {
                return null;
            }
        }

        /// <summary>
        /// Creates a thumbnail from an image file.
        /// </summary>
        private BitmapSource? CreateImageThumbnail(string filePath)
        {
            try
            {
                // Load image with explicit memory caching
                var bitmap = new BitmapImage();
                bitmap.BeginInit();

                // Use CreateOptions to ensure file handle is released
                bitmap.CreateOptions = BitmapCreateOptions.IgnoreColorProfile;

                // Load entire image into memory immediately
                bitmap.CacheOption = BitmapCacheOption.OnLoad;

                // Decode to thumbnail size to save memory
                bitmap.DecodePixelWidth = 150;

                // Load from file
                bitmap.UriSource = new Uri(filePath, UriKind.Absolute);

                bitmap.EndInit();

                // Freeze to make it cross-thread accessible and improve performance
                bitmap.Freeze();

                return bitmap;
            }
            catch (Exception ex)
            {
                return null;
            }
        }

        /// <summary>
        /// Creates a thumbnail from a video file by extracting the first frame.
        /// </summary>
        private BitmapSource? CreateVideoThumbnail(string filePath)
        {
            Media.VideoPlayer? videoPlayer = null;
            try
            {
                videoPlayer = new Media.VideoPlayer();

                if (!videoPlayer.Open(filePath))
                {
                    return null;
                }

                // Read first frame
                if (videoPlayer.ReadNextFrame(out Mat frame))
                {
                    try
                    {
                        // Scale to thumbnail size (150px width)
                        int thumbWidth = 150;
                        int thumbHeight = (int)(frame.Height * (thumbWidth / (double)frame.Width));

                        Mat thumbnail = new Mat();
                        Cv2.Resize(frame, thumbnail, new OpenCvSharp.Size(thumbWidth, thumbHeight));

                        // Convert to BitmapSource
                        var bitmapSource = MatToBitmapSource.Convert(thumbnail);

                        thumbnail.Dispose();
                        frame.Dispose();

                        return bitmapSource;
                    }
                    catch (Exception ex)
                    {
                        frame.Dispose();
                        return null;
                    }
                }
                else
                {
                    return null;
                }
            }
            catch (Exception ex)
            {
                return null;
            }
            finally
            {
                videoPlayer?.Close();
                videoPlayer?.Dispose();
            }
        }

        private async Task LoadImageAsync(string path)
        {
            // Determine if this is a video or image
            string extension = Path.GetExtension(path).ToLowerInvariant();
            bool isVideo = extension == ".mp4" || extension == ".mov" || extension == ".avi" || extension == ".mkv";

            if (isVideo)
            {
                await LoadVideoAsync(path);
            }
            else
            {
                await LoadImageFileAsync(path);
            }

            DropText.Visibility = Visibility.Collapsed;
            StatusText.Text = Path.GetFileName(path);
        }

        private async void LoadImage(string path)
        {
            // Fire-and-forget is intentional here for UI responsiveness from sync context
            await LoadImageAsync(path);
        }

        private async Task LoadImageFileAsync(string path)
        {

            // Stop any existing video playback
            await StopVideoPlaybackAsync();

            // Clear any previous video state
            _isVideoActive = false;

            // Dispose old frame if exists (deprecated path, remove later)
            _currentMediaFrame?.Dispose();
            _currentMediaFrame = null;

            // Load image via MediaController
            if (!_mediaController.Load(path))
            {
                StatusText.Text = "Failed to load image";
                System.Diagnostics.Debug.WriteLine($"[MainWindow] ? Failed to load image: {path}");
                return;
            }

            System.Diagnostics.Debug.WriteLine($"[MainWindow] ? Image loaded successfully: {Path.GetFileName(path)}");

            // Reset framing settings (via RenderPipeline)
            if (_renderPipeline != null)
            {
                _renderPipeline.ResetFraming();
            }

            // Update pan slider ranges for new media
            UpdatePanSliderRanges();

            // Disable video playback controls
            UpdatePlaybackControlsState(false);
        }

        private async Task LoadVideoAsync(string path)
        {

            // Stop any existing video playback
            await StopVideoPlaybackAsync();

            // Load video metadata via MediaController first
            if (!_mediaController.Load(path))
            {
                StatusText.Text = "Failed to load video metadata";
                System.Diagnostics.Debug.WriteLine($"[MainWindow] ? Failed to load video metadata: {path}");
                return;
            }

            System.Diagnostics.Debug.WriteLine($"[MainWindow] ? Video metadata loaded: {Path.GetFileName(path)}");

            // Open the video
            if (!_videoPlayer.Open(path))
            {
                string errorMsg = $"Failed to open video: {Path.GetFileName(path)}";
                StatusText.Text = errorMsg;

                MessageBox.Show(
                    $"Failed to open video file:\n\n{Path.GetFileName(path)}\n\nPossible reasons:\n• File is corrupted\n• Codec not supported\n• File is in use",
                    "Video Load Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
                return;
            }

            // Create new playback engine
            _playbackEngine = new Media.PlaybackEngine(_videoPlayer);
            _playbackEngine.FrameReady += PlaybackEngine_FrameReady;

            // Display the first frame
            bool firstFrameSuccess = DisplayFirstFrame();

            if (firstFrameSuccess)
            {
                StatusText.Text = $"{Path.GetFileName(path)} (Video ready - press Play)";
            }
            else
            {
                StatusText.Text = $"Video opened but first frame failed";
            }

            // Enable video playback controls
            UpdatePlaybackControlsState(true);
        }

        private bool DisplayFirstFrame()
        {
            if (!_videoPlayer.IsOpened)
            {
                return false;
            }

            // Seek to first frame
            if (!_videoPlayer.Seek(0))
            {
                return false;
            }

            // Read and store the first frame
            if (_videoPlayer.ReadNextFrame(out Mat frame))
            {
                try
                {

                    // Update MediaController with the first video frame
                    _mediaController.UpdateVideoFrame(frame);
                    _isVideoActive = true;

                    // Reset framing settings (via RenderPipeline)
                    if (_renderPipeline != null)
                    {
                        _renderPipeline.ResetFraming();
                    }

                    // Update pan slider ranges for new media
                    UpdatePanSliderRanges();
                }
                catch (Exception ex)
                {
                    frame.Dispose();
                    return false;
                }
                finally
                {
                    frame.Dispose();
                }

                // Reset to first frame for playback
                _videoPlayer.Seek(0);

                return true;
            }
            else
            {
                return false;
            }
        }

        private async Task StopVideoPlaybackAsync()
        {
            if (_playbackEngine != null)
            {
                await _playbackEngine.StopAsync();
                _playbackEngine.FrameReady -= PlaybackEngine_FrameReady;
                _playbackEngine.Dispose();
                _playbackEngine = null;
            }

            if (_videoPlayer.IsOpened)
            {
                _videoPlayer.Close();
            }
        }

        private void StopVideoPlayback()
        {
            // Synchronous wrapper for cases where async is not possible
            // This should be avoided when called from UI thread
            StopVideoPlaybackAsync().GetAwaiter().GetResult();
        }

        private void UpdatePlaybackControlsState(bool hasVideo)
        {
            // Enable/disable playback controls based on whether video is loaded
            PlayButton.IsEnabled = hasVideo;
            PauseButton.IsEnabled = hasVideo;
            StopButton.IsEnabled = hasVideo;
            LoopCheckBox.IsEnabled = hasVideo;

            // Reset loop checkbox when switching media
            if (hasVideo && _playbackEngine != null)
            {
                LoopCheckBox.IsChecked = _playbackEngine.Loop;
            }
            else
            {
                LoopCheckBox.IsChecked = false;
            }
        }

        private void PlaybackEngine_FrameReady(object? sender, Core.Frame frame)
        {
            // This event is raised from a background thread, so we need to dispatch to UI thread
            Dispatcher.Invoke(() =>
            {
                try
                {
                    if (frame == null || !frame.IsValid)
                        return;

                    // Update MediaController with the new video frame
                    // RenderLoop will pick it up automatically on the next render cycle
                    _mediaController.UpdateVideoFrame(frame.Image);
                }
                catch (Exception ex)
                {
                }
            });
        }

        // ============================================
        // Video Playback Control Handlers
        // ============================================

        private void PlayButton_Click(object sender, RoutedEventArgs e)
        {
            if (_playbackEngine != null)
            {
                _playbackEngine.Play();
            }
        }

        private async void PauseButton_Click(object sender, RoutedEventArgs e)
        {
            if (_playbackEngine != null)
            {
                await _playbackEngine.PauseAsync();
            }
        }

        private async void StopButton_Click(object sender, RoutedEventArgs e)
        {
            if (_playbackEngine != null)
            {
                await _playbackEngine.StopAsync();

                // Display first frame when stopped
                DisplayFirstFrame();
            }
        }

        private void LoopCheckBox_Changed(object sender, RoutedEventArgs e)
        {
            if (_playbackEngine != null)
            {
                _playbackEngine.Loop = LoopCheckBox.IsChecked == true;
            }
        }

        // ============================================
        // Audio Playback Control Handlers
        // ============================================

        private void PopulateAudioDevices()
        {
            try
            {
                var devices = Services.AudioPlayerService.GetAvailableDevices();
                AudioDeviceComboBox.ItemsSource = devices;
                AudioDeviceComboBox.DisplayMemberPath = "FriendlyName";
                AudioDeviceComboBox.SelectedValuePath = "DeviceNumber";

                // Select default device
                if (devices.Count > 0)
                {
                    AudioDeviceComboBox.SelectedIndex = 0;
                }
            }
            catch (Exception ex)
            {
                StatusText.Text = $"Failed to load audio devices: {ex.Message}";
            }
        }

        private void AudioDeviceComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (AudioDeviceComboBox.SelectedItem is Services.AudioDevice device)
            {
                _audioPlayer.SetOutputDevice(device.DeviceNumber);
                StatusText.Text = $"Audio output: {device.Name}";
            }
        }

        private void LoadAudioButton_Click(object sender, RoutedEventArgs e)
        {
            var openFileDialog = new OpenFileDialog
            {
                Title = "Select Audio File",
                Filter = "Audio Files|*.mp3;*.wav;*.m4a;*.aac;*.ogg;*.flac|All Files|*.*",
                Multiselect = false
            };

            if (openFileDialog.ShowDialog() == true)
            {
                if (_audioPlayer.LoadAudio(openFileDialog.FileName))
                {
                    // Enable audio controls
                    PlayAudioButton.IsEnabled = true;
                    StopAudioButton.IsEnabled = true;
                    LoopAudioCheckBox.IsEnabled = true;

                    StatusText.Text = $"Audio loaded: {_audioPlayer.CurrentFileName}";
                }
                else
                {
                    MessageBox.Show(
                        "Failed to load audio file. Make sure the file is a valid audio format.",
                        "Audio Load Error",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);
                }
            }
        }

        private void PlayAudioButton_Click(object sender, RoutedEventArgs e)
        {
            _audioPlayer.Play();
            PauseAudioButton.IsEnabled = true;
            StatusText.Text = $"Playing: {_audioPlayer.CurrentFileName}";
        }

        private void PauseAudioButton_Click(object sender, RoutedEventArgs e)
        {
            _audioPlayer.Pause();
            StatusText.Text = $"Audio paused: {_audioPlayer.CurrentFileName}";
        }

        private void StopAudioButton_Click(object sender, RoutedEventArgs e)
        {
            _audioPlayer.Stop();
            PauseAudioButton.IsEnabled = false;
            StatusText.Text = $"Audio stopped: {_audioPlayer.CurrentFileName}";
        }

        private void LoopAudioCheckBox_Changed(object sender, RoutedEventArgs e)
        {
            _audioPlayer.SetLoop(LoopAudioCheckBox.IsChecked == true);
        }

        private void MediaListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (MediaListBox.SelectedItem is MediaItem mediaItem)
            {
                LoadImage(mediaItem.FilePath);
            }
        }

        private void DeleteButton_Click(object sender, RoutedEventArgs e)
        {
            if (MediaListBox.SelectedItem is MediaItem mediaItem)
            {
                MediaItems.Remove(mediaItem);

                // If library is now empty, clear preview
                if (MediaItems.Count == 0)
                {
                    PreviewImage.Source = null;
                    DropText.Visibility = Visibility.Visible;
                    StatusText.Text = "Ready";
                }
            }
        }

        private void ClearButton_Click(object sender, RoutedEventArgs e)
        {
            MediaItems.Clear();
            PreviewImage.Source = null;
            DropText.Visibility = Visibility.Visible;
            StatusText.Text = "Ready";
        }

        /// <summary>
        /// Handles keyboard shortcuts for media library navigation and management.
        /// </summary>
        private void MainWindow_KeyDown(object sender, KeyEventArgs e)
        {
            // Check if MediaListBox has focus or is in focus scope
            bool mediaListHasFocus = MediaListBox.IsKeyboardFocusWithin || MediaListBox.IsFocused;

            // Delete - Remove selected media
            if (e.Key == Key.Delete && !e.KeyboardDevice.Modifiers.HasFlag(ModifierKeys.Control))
            {
                if (MediaListBox.SelectedItem != null)
                {
                    DeleteButton_Click(this, new RoutedEventArgs());
                    e.Handled = true;
                }
                return;
            }

            // Ctrl+Delete - Clear library
            if (e.Key == Key.Delete && e.KeyboardDevice.Modifiers.HasFlag(ModifierKeys.Control))
            {
                if (MediaItems.Count > 0)
                {
                    ClearButton_Click(this, new RoutedEventArgs());
                    e.Handled = true;
                }
                return;
            }

            // Arrow Up - Previous media
            if (e.Key == Key.Up && mediaListHasFocus)
            {
                if (MediaListBox.SelectedIndex > 0)
                {
                    MediaListBox.SelectedIndex--;
                    MediaListBox.ScrollIntoView(MediaListBox.SelectedItem);
                    e.Handled = true;
                }
                return;
            }

            // Arrow Down - Next media
            if (e.Key == Key.Down && mediaListHasFocus)
            {
                if (MediaListBox.SelectedIndex < MediaItems.Count - 1)
                {
                    MediaListBox.SelectedIndex++;
                    MediaListBox.ScrollIntoView(MediaListBox.SelectedItem);
                    e.Handled = true;
                }
                return;
            }

            // Ctrl+A - Select all (focus media list)
            if (e.Key == Key.A && e.KeyboardDevice.Modifiers.HasFlag(ModifierKeys.Control))
            {
                if (MediaItems.Count > 0)
                {
                    MediaListBox.Focus();
                    if (MediaListBox.SelectedIndex == -1)
                    {
                        MediaListBox.SelectedIndex = 0;
                    }
                    e.Handled = true;
                }
                return;
            }
        }

        private void ZoomSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (_suppressSliderEvents) return;

            // Guard: Skip if controls not fully initialized yet (during XAML loading)
            if (ZoomValueText == null || _renderPipeline == null) return;

            // ZoomSlider now controls camera distance (inverse relationship)
            // Slider value 1.0 = distance 1.0 (standard)
            // For UI consistency, we keep the slider representing "zoom" feel
            // but internally it controls distance
            _renderPipeline.CameraEngine.SetDistance(1.0 / ZoomSlider.Value);

            ZoomValueText.Text = $"{ZoomSlider.Value * 100:0}%";

            // Rendering will happen automatically via RenderLoop
        }

        private void XSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (_suppressSliderEvents) return;

            // Guard: Skip if controls not fully initialized yet (during XAML loading)
            if (HorizontalValueText == null || _renderPipeline == null) return;

            // XSlider now controls camera position X
            _renderPipeline.CameraEngine.SetPosition(XSlider.Value, _renderPipeline.CameraEngine.Target.PositionY);

            HorizontalValueText.Text = $"{XSlider.Value:0}";

            // Rendering will happen automatically via RenderLoop
        }

        private void YSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (_suppressSliderEvents) return;

            // Guard: Skip if controls not fully initialized yet (during XAML loading)
            if (VerticalValueText == null || _renderPipeline == null) return;

            // YSlider now controls camera position Y
            _renderPipeline.CameraEngine.SetPosition(_renderPipeline.CameraEngine.Target.PositionX, YSlider.Value);

            VerticalValueText.Text = $"{YSlider.Value:0}";

            // Rendering will happen automatically via RenderLoop
        }

        private void RotationSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (_suppressSliderEvents) return;

            // Guard: Skip if controls not fully initialized yet (during XAML loading)
            if (RotationValueText == null || _renderPipeline == null) return;

            // RotationSlider now controls camera roll
            _renderPipeline.CameraEngine.SetRoll(RotationSlider.Value);

            RotationValueText.Text = $"{RotationSlider.Value:0}°";

            // Rendering will happen automatically via RenderLoop
        }

        private void PitchSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (_suppressSliderEvents) return;

            // Guard: Skip if controls not fully initialized yet (during XAML loading)
            if (PitchValueText == null || _renderPipeline == null) return;

            // Update camera pitch
            _renderPipeline.CameraEngine.SetTilt(
                PitchSlider.Value,
                _renderPipeline.CameraEngine.Target.Yaw);

            PitchValueText.Text = $"{PitchSlider.Value:0}°";

            // Rendering will happen automatically via RenderLoop
        }

        private void YawSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (_suppressSliderEvents) return;

            // Guard: Skip if controls not fully initialized yet (during XAML loading)
            if (YawValueText == null || _renderPipeline == null) return;

            // Update camera yaw
            _renderPipeline.CameraEngine.SetTilt(
                _renderPipeline.CameraEngine.Target.Pitch,
                YawSlider.Value);

            YawValueText.Text = $"{YawSlider.Value:0}°";

            // Rendering will happen automatically via RenderLoop
        }

        private void ResetButton_Click(object sender, RoutedEventArgs e)
        {
            ResetView();
        }

        private void ResetView()
        {
            if (_renderPipeline != null)
            {
                _renderPipeline.CameraEngine.ResetCamera();
            }

            // Update slider values to match reset camera state
            _suppressSliderEvents = true;
            try
            {
                ZoomSlider.Value = 1;
                XSlider.Value = 0;
                YSlider.Value = 0;
                PitchSlider.Value = 0;
                YawSlider.Value = 0;
                RotationSlider.Value = 0;
            }
            finally
            {
                _suppressSliderEvents = false;
            }

            // Rendering will happen automatically via RenderLoop
        }

        /// <summary>
        /// Reset framing settings to default - DEPRECATED, use RenderPipeline.ResetFraming()
        /// </summary>
        private void ResetFramingSettings()
        {
            if (_renderPipeline != null)
            {
                _renderPipeline.ResetFraming();
            }
        }

        /// <summary>
        /// Calculate pan bounds based on media size, zoom level, and canvas size.
        /// Returns (minX, maxX, minY, maxY) that allow viewing all pixels of the zoomed media
        /// while preventing empty space around edges.
        /// </summary>
        private (double minX, double maxX, double minY, double maxY) CalculatePanBounds()
        {
            if (_mediaController == null || !_mediaController.HasMedia)
                return (0, 0, 0, 0);

            Mat? currentFrame = _mediaController.GetCurrentFrame();
            if (currentFrame == null || currentFrame.Empty())
                return (0, 0, 0, 0);

            // Get canvas dimensions
            int canvasWidth = ActiveProfile?.DisplayWidth ?? 1080;
            int canvasHeight = ActiveProfile?.DisplayHeight ?? 1920;

            // Get current zoom
            double zoom = ZoomSlider.Value;

            // Calculate base scale to fit source onto canvas (maintaining aspect ratio)
            double baseScale = System.Math.Min(
                (double)canvasWidth / currentFrame.Width,
                (double)canvasHeight / currentFrame.Height);

            // Apply zoom to get total scale
            double totalScale = baseScale * zoom;

            // Calculate scaled media dimensions
            double scaledWidth = currentFrame.Width * totalScale;
            double scaledHeight = currentFrame.Height * totalScale;

            // Calculate how much the scaled media extends beyond the canvas
            double excessWidth = scaledWidth - canvasWidth;
            double excessHeight = scaledHeight - canvasHeight;

            // Pan bounds: allow panning to show any part of the media, but no empty space
            // If media is smaller than canvas, no panning needed (excess is negative)
            // If media is larger than canvas, allow panning by half the excess in each direction
            double maxX = System.Math.Max(0, excessWidth / 2.0);
            double minX = -maxX;
            double maxY = System.Math.Max(0, excessHeight / 2.0);
            double minY = -maxY;

            return (minX, maxX, minY, maxY);
        }

        /// <summary>
        /// Update pan slider minimum/maximum based on current zoom and media size.
        /// This ensures sliders can reach all edges of the zoomed media.
        /// </summary>
        private void UpdatePanSliderRanges()
        {
            // Guard: Don't update if sliders not initialized yet or no media loaded
            if (XSlider == null || YSlider == null || _mediaController == null || !_mediaController.HasMedia)
                return;

            var (minX, maxX, minY, maxY) = CalculatePanBounds();

            // Suppress events during programmatic slider updates
            _suppressSliderEvents = true;
            try
            {
                // Update X slider range
                XSlider.Minimum = minX;
                XSlider.Maximum = maxX;

                // Update Y slider range
                YSlider.Minimum = minY;
                YSlider.Maximum = maxY;

                // Clamp current values to new ranges
                if (XSlider.Value < minX) XSlider.Value = minX;
                if (XSlider.Value > maxX) XSlider.Value = maxX;
                if (YSlider.Value < minY) YSlider.Value = minY;
                if (YSlider.Value > maxY) YSlider.Value = maxY;
            }
            finally
            {
                _suppressSliderEvents = false;
            }
        }

        // ============================================
        // Mouse Control Handlers
        // ============================================

        /// <summary>
        /// Mouse interaction with virtual camera preview.
        /// Left-click drag = Move camera position
        /// Right-click drag = Tilt camera (pitch/yaw)
        /// Middle-click drag = Fine camera movement
        /// Left double-click = Center camera smoothly
        /// </summary>
        private void PreviewBorder_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (_mediaController == null || !_mediaController.HasMedia)
                return;

            if (e.LeftButton == MouseButtonState.Pressed)
            {
                // Detect double-click = center camera
                if (e.ClickCount == 2)
                {
                    _renderPipeline?.CameraEngine.ResetCamera();
                    e.Handled = true;
                    return;
                }

                // Start camera position drag
                if (!_isDragging)
                {
                    _isDragging = true;
                    _lastMousePosition = e.GetPosition(PreviewBorder);
                    PreviewBorder.CaptureMouse();
                    PreviewBorder.Cursor = Cursors.SizeAll;
                    e.Handled = true;
                }
            }
            else if (e.RightButton == MouseButtonState.Pressed)
            {
                // Start camera tilt drag
                if (!_isDragging)
                {
                    _isDragging = true;
                    _lastMousePosition = e.GetPosition(PreviewBorder);
                    PreviewBorder.CaptureMouse();
                    PreviewBorder.Cursor = Cursors.Cross;
                    e.Handled = true;
                }
            }
            else if (e.MiddleButton == MouseButtonState.Pressed)
            {
                // Start fine movement drag
                if (!_isDragging)
                {
                    _isDragging = true;
                    _lastMousePosition = e.GetPosition(PreviewBorder);
                    PreviewBorder.CaptureMouse();
                    PreviewBorder.Cursor = Cursors.Hand;
                    e.Handled = true;
                }
            }
        }

        /// <summary>
        /// Release camera drag
        /// </summary>
        private void PreviewBorder_MouseUp(object sender, MouseButtonEventArgs e)
        {
            if (_isDragging)
            {
                _isDragging = false;
                PreviewBorder.ReleaseMouseCapture();
                PreviewBorder.Cursor = Cursors.Arrow;
                e.Handled = true;
            }
        }

        /// <summary>
        /// Camera control via mouse drag.
        /// Left drag = Move camera position
        /// Right drag = Tilt camera (pitch/yaw)
        /// Middle drag = Fine camera movement (half speed)
        /// </summary>
        private void PreviewBorder_MouseMove(object sender, MouseEventArgs e)
        {
            if (!_isDragging || _mediaController == null || !_mediaController.HasMedia || _renderPipeline == null)
                return;

            WpfPoint currentPosition = e.GetPosition(PreviewBorder);
            Vector delta = currentPosition - _lastMousePosition;

            if (e.LeftButton == MouseButtonState.Pressed)
            {
                // Move camera position (inverted: drag right = camera moves right = document appears to move left)
                _renderPipeline.CameraEngine.AdjustPosition(delta.X, delta.Y);
            }
            else if (e.RightButton == MouseButtonState.Pressed)
            {
                // Tilt camera (pitch/yaw)
                double pitchDelta = -delta.Y * 0.05;  // Vertical mouse = pitch
                double yawDelta = delta.X * 0.05;     // Horizontal mouse = yaw
                _renderPipeline.CameraEngine.AdjustTilt(pitchDelta, yawDelta);
            }
            else if (e.MiddleButton == MouseButtonState.Pressed)
            {
                // Fine movement (half speed)
                _renderPipeline.CameraEngine.AdjustPosition(delta.X * 0.5, delta.Y * 0.5);
            }

            _lastMousePosition = currentPosition;
            e.Handled = true;
        }

        /// <summary>
        /// Mouse wheel = Adjust camera distance (closer/farther from document)
        /// </summary>
        private void PreviewBorder_MouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (_mediaController == null || !_mediaController.HasMedia || _renderPipeline == null)
                return;

            // Camera distance step: +/-0.1 per wheel click (120 delta units)
            double distanceStep = -0.1 * (e.Delta / 120.0);  // Negative: wheel up = closer
            _renderPipeline.CameraEngine.AdjustDistance(distanceStep);

            e.Handled = true;
        }

        /// <summary>
        /// Auto Fit = Reset camera to centered position
        /// </summary>
        private void AutoFitPreview()
        {
            if (_mediaController == null || !_mediaController.HasMedia || _renderPipeline == null)
                return;

            _renderPipeline.CameraEngine.ResetCamera();

            // Rendering will happen automatically via RenderLoop
        }

        /// <summary>
        /// Handles camera profile selection.
        /// Stores the selected profile and remembers it for next startup.
        /// Immediately redraws the preview with the new profile's dimensions.
        /// </summary>
        private void CameraProfileComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (CameraProfileComboBox.SelectedItem is not string profileName)
                return;

            // Retrieve the selected profile
            ActiveProfile = _profileService.GetProfile(profileName);

            // Update RenderPipeline's active profile
            if (_renderPipeline != null)
            {
                _renderPipeline.ActiveProfile = ActiveProfile;
            }

            // Remember the selection
            SaveLastSelectedProfile(profileName);

            // Rendering will happen automatically via RenderLoop with new dimensions
        }

        /// <summary>
        /// Updates safe area overlay rectangles when preview size changes.
        /// </summary>
        private void PreviewGrid_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            UpdateSafeAreaOverlay();
        }

        /// <summary>
        /// Calculates and positions the safe area overlay rectangles.
        /// Action Safe: 90% of preview area
        /// Title Safe: 95% of preview area
        /// </summary>
        private void UpdateSafeAreaOverlay()
        {
            double width = PreviewGrid.ActualWidth;
            double height = PreviewGrid.ActualHeight;

            if (width <= 0 || height <= 0)
                return;

            // Action Safe Area (90%)
            double actionSafeWidth = width * 0.90;
            double actionSafeHeight = height * 0.90;
            double actionSafeLeft = (width - actionSafeWidth) / 2;
            double actionSafeTop = (height - actionSafeHeight) / 2;

            Canvas.SetLeft(ActionSafeRect, actionSafeLeft);
            Canvas.SetTop(ActionSafeRect, actionSafeTop);
            ActionSafeRect.Width = actionSafeWidth;
            ActionSafeRect.Height = actionSafeHeight;

            // Title Safe Area (95%)
            double titleSafeWidth = width * 0.95;
            double titleSafeHeight = height * 0.95;
            double titleSafeLeft = (width - titleSafeWidth) / 2;
            double titleSafeTop = (height - titleSafeHeight) / 2;

            Canvas.SetLeft(TitleSafeRect, titleSafeLeft);
            Canvas.SetTop(TitleSafeRect, titleSafeTop);
            TitleSafeRect.Width = titleSafeWidth;
            TitleSafeRect.Height = titleSafeHeight;
        }

        /// <summary>
        /// Toggles checkerboard background visibility.
        /// </summary>
        private void ShowCheckerboardCheckBox_Changed(object sender, RoutedEventArgs e)
        {
            if (CheckerboardBackground == null)
                return;

            CheckerboardBackground.Visibility = ShowCheckerboardCheckBox.IsChecked == true
                ? Visibility.Visible
                : Visibility.Collapsed;
        }

        /// <summary>
        /// Toggles safe area overlay visibility.
        /// </summary>
        private void ShowSafeAreaCheckBox_Changed(object sender, RoutedEventArgs e)
        {
            if (SafeAreaOverlay == null)
                return;

            SafeAreaOverlay.Visibility = ShowSafeAreaCheckBox.IsChecked == true
                ? Visibility.Visible
                : Visibility.Collapsed;
        }

        /// <summary>
        /// Toggles handheld mode for the virtual camera.
        /// </summary>
        private void HandheldModeCheckBox_Changed(object sender, RoutedEventArgs e)
        {
            if (_renderPipeline == null)
                return;

            _renderPipeline.CameraEngine.SetHandheldMode(HandheldModeCheckBox.IsChecked == true);
        }

        /// <summary>
        /// Handles phone profile selection changes.
        /// Loads the selected profile into the virtual camera engine.
        /// </summary>
        private void PhoneProfileComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (PhoneProfileComboBox.SelectedIndex < 0 || _renderPipeline == null)
                return;

            // Get all profiles
            List<PhoneProfile> profiles = PhoneProfileFactory.GetAllProfiles();

            if (PhoneProfileComboBox.SelectedIndex < profiles.Count)
            {
                PhoneProfile selectedProfile = profiles[PhoneProfileComboBox.SelectedIndex];
                _renderPipeline.CameraEngine.LoadProfile(selectedProfile);

                // Update status
                StatusText.Text = $"Loaded camera profile: {selectedProfile.Name}";
            }
        }

        /// <summary>
        /// Toggles verification mode for precise document capture.
        /// </summary>
        private void VerificationModeCheckBox_Changed(object sender, RoutedEventArgs e)
        {
            if (_renderPipeline == null)
                return;

            _renderPipeline.CameraEngine.VerificationMode = VerificationModeCheckBox.IsChecked == true;

            // Update status
            if (VerificationModeCheckBox.IsChecked == true)
            {
                StatusText.Text = "Verification Mode: Enabled (slower, more precise)";
            }
            else
            {
                StatusText.Text = "Verification Mode: Disabled";
            }
        }

        // ============================================
        // Export Handlers
        // ============================================

        /// <summary>
        /// Exports the current rendered frame to a PNG file.
        /// Captures exactly what ViewportEngine renders (zoom/pan/rotation/background)
        /// but excludes UI overlays like the safe area guide.
        /// </summary>
        private void ExportCurrentFrame_Click(object sender, RoutedEventArgs e)
        {
            if (!_renderService.HasImage)
            {
                StatusText.Text = "No image to export";
                return;
            }

            // Generate default filename with timestamp
            string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            string defaultFileName = $"frame_{timestamp}.png";

            // Show save file dialog
            var dialog = new SaveFileDialog
            {
                Title = "Export Current Frame",
                FileName = defaultFileName,
                Filter = "PNG Image (*.png)|*.png",
                DefaultExt = ".png",
                AddExtension = true
            };

            if (dialog.ShowDialog() == true)
            {
                try
                {
                    // Render the current frame with all transformations
                    using var frame = _renderService.Render(ActiveProfile);

                    if (!frame.IsValid)
                    {
                        StatusText.Text = "Export failed: Invalid frame";
                        return;
                    }

                    // Save to PNG using OpenCV
                    bool success = Cv2.ImWrite(dialog.FileName, frame.Image);

                    if (success)
                    {
                        StatusText.Text = $"Exported: {Path.GetFileName(dialog.FileName)}";
                    }
                    else
                    {
                        StatusText.Text = "Export failed: Could not write file";
                    }
                }
                catch (Exception ex)
                {
                    StatusText.Text = $"Export failed: {ex.Message}";
                }
            }
        }

        // ============================================
        // ============================================
        // Unity Video Capture Integration
        // ============================================

        /// <summary>
        /// Handles the Start UnityCapture button click
        /// </summary>
        private void StartUnityCaptureButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Check if media is loaded
                if (!_mediaController.HasMedia)
                {
                    StatusText.Text = "Please load an image or video first!";
                    MessageBox.Show("Please load an image or video before starting Unity Video Capture.",
                                    "No Media Loaded",
                                    MessageBoxButton.OK,
                                    MessageBoxImage.Warning);
                    return;
                }

                StatusText.Text = "Starting Unity Video Capture output...";
                StartUnityCaptureButton.IsEnabled = false;

                // Create and register UnityCapture output
                _unityCaptureOutput = new Outputs.UnityCaptureOutput();
                _outputManager.Register(_unityCaptureOutput);

                StatusText.Text = "Unity Video Capture started";
                StopUnityCaptureButton.IsEnabled = true;

                // Update status indicator
                UnityCaptureStatusIndicator.Fill = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.LimeGreen);
                UnityCaptureStatusText.Text = "Running";
            }
            catch (Exception ex)
            {
                StatusText.Text = $"Unity Video Capture error: {ex.Message}";
                StartUnityCaptureButton.IsEnabled = true;
                MessageBox.Show($"Error starting Unity Video Capture: {ex.Message}",
                                "Error",
                                MessageBoxButton.OK,
                                MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// Handles the Stop UnityCapture button click
        /// </summary>
        private void StopUnityCaptureButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                StatusText.Text = "Stopping Unity Video Capture output...";
                StopUnityCaptureButton.IsEnabled = false;

                if (_unityCaptureOutput != null)
                {
                    _outputManager.Unregister(_unityCaptureOutput);
                    _unityCaptureOutput.Dispose();
                    _unityCaptureOutput = null;
                }

                StatusText.Text = "Unity Video Capture stopped";
                StartUnityCaptureButton.IsEnabled = true;

                // Update status indicator
                UnityCaptureStatusIndicator.Fill = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.Gray);
                UnityCaptureStatusText.Text = "Stopped";
            }
            catch (Exception ex)
            {
                StatusText.Text = $"Unity Video Capture error: {ex.Message}";
                StopUnityCaptureButton.IsEnabled = true;
                MessageBox.Show($"Error stopping Unity Video Capture: {ex.Message}",
                                "Error",
                                MessageBoxButton.OK,
                                MessageBoxImage.Error);
            }
        }

        // ============================================
            }
        }


