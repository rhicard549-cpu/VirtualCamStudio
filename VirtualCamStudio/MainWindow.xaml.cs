using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Configuration;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using Microsoft.Win32;
using OpenCvSharp;
using VirtualCamStudio.Core;
using VirtualCamStudio.Helpers;
using VirtualCamStudio.Models;
using VirtualCamStudio.Services;
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
        private readonly OutputManager _outputManager = new();
        private readonly VirtualCameraService _virtualCamera = new();
        private readonly Services.OBS.OBSClient _obsClient = new();
        private readonly Services.OBS.OBSSceneService _obsSceneService;
        private readonly Services.OBS.OBSSourceService _obsSourceService;
        private readonly Services.OBS.OBSImageOutput _obsImageOutput;

        // Video playback
        private readonly Media.VideoPlayer _videoPlayer = new();
        private readonly Media.ViewportEngine _viewportEngine = new();
        private Media.PlaybackEngine? _playbackEngine;

        // Current media state
        private Mat? _currentMediaFrame;  // Current frame in memory (image or video frame)
        private bool _isVideoActive = false;  // True if current media is a video
        private readonly FramingSettings _framingSettings = new();  // Shared framing state

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
                System.Diagnostics.Debug.WriteLine("[MainWindow] Constructor started");

                InitializeComponent();
                System.Diagnostics.Debug.WriteLine("[MainWindow] InitializeComponent completed");

                MediaListBox.ItemsSource = MediaItems;
                System.Diagnostics.Debug.WriteLine("[MainWindow] MediaListBox bound");

                // Initialize OBS services
                _obsSceneService = new Services.OBS.OBSSceneService(_obsClient);
                _obsSourceService = new Services.OBS.OBSSourceService(_obsClient);
                _obsImageOutput = new Services.OBS.OBSImageOutput();
                System.Diagnostics.Debug.WriteLine("[MainWindow] OBS services initialized");

                // Register preview as an output target
                var previewTarget = new PreviewOutputTarget(PreviewImage);
                _outputManager.RegisterTarget(previewTarget);
                System.Diagnostics.Debug.WriteLine("[MainWindow] Preview target registered");

                // Register virtual camera as an output target
                _outputManager.RegisterTarget(_virtualCamera);
                System.Diagnostics.Debug.WriteLine("[MainWindow] Virtual camera registered");

                // Register keyboard shortcuts
                KeyDown += MainWindow_KeyDown;
                System.Diagnostics.Debug.WriteLine("[MainWindow] Keyboard shortcuts registered");

                // Register preview size changed handler for safe area overlay
                PreviewGrid.SizeChanged += PreviewGrid_SizeChanged;
                System.Diagnostics.Debug.WriteLine("[MainWindow] Size changed handler registered");

                System.Diagnostics.Debug.WriteLine("[MainWindow] Constructor completed successfully");
            }
            catch (Exception ex)
            {
                string error = $"MainWindow Constructor Error: {ex.Message}\n\nStack Trace:\n{ex.StackTrace}";
                System.Diagnostics.Debug.WriteLine(error);

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

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            LoadAndPopulateCameraProfiles();
        }

        private void Window_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
        {
            // Stop video playback
            StopVideoPlayback();

            // Dispose current media frame
            _currentMediaFrame?.Dispose();
            _currentMediaFrame = null;

            // Dispose video player
            _videoPlayer?.Dispose();
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
                    System.Diagnostics.Debug.WriteLine($"[Drag-Drop] Image detected: {file}");
                }
                // Supported video formats
                else if (ext == ".mp4" ||
                         ext == ".mov" ||
                         ext == ".avi" ||
                         ext == ".mkv")
                {
                    AddMediaToLibrary(file);
                    anyAdded = true;
                    System.Diagnostics.Debug.WriteLine($"[Drag-Drop] Video detected: {file}");
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"[Drag-Drop] Unsupported file type: {file} ({ext})");
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
                System.Diagnostics.Debug.WriteLine($"[CreateThumbnail] Error: {ex.Message}");
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
                System.Diagnostics.Debug.WriteLine($"[CreateImageThumbnail] Error: {ex.Message}");
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
                    System.Diagnostics.Debug.WriteLine($"[CreateVideoThumbnail] Failed to open video: {filePath}");
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
                        System.Diagnostics.Debug.WriteLine($"[CreateVideoThumbnail] Error converting frame: {ex.Message}");
                        frame.Dispose();
                        return null;
                    }
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"[CreateVideoThumbnail] Failed to read first frame: {filePath}");
                    return null;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[CreateVideoThumbnail] Error: {ex.Message}");
                return null;
            }
            finally
            {
                videoPlayer?.Close();
                videoPlayer?.Dispose();
            }
        }

        private void LoadImage(string path)
        {
            // Determine if this is a video or image
            string extension = Path.GetExtension(path).ToLowerInvariant();
            bool isVideo = extension == ".mp4" || extension == ".mov" || extension == ".avi" || extension == ".mkv";

            if (isVideo)
            {
                LoadVideo(path);
            }
            else
            {
                LoadImageFile(path);
            }

            DropText.Visibility = Visibility.Collapsed;
            StatusText.Text = Path.GetFileName(path);
        }

        private void LoadImageFile(string path)
        {
            System.Diagnostics.Debug.WriteLine($"========================================");
            System.Diagnostics.Debug.WriteLine($"[LoadImageFile] File detected: {path}");
            System.Diagnostics.Debug.WriteLine($"[LoadImageFile] Media type: IMAGE");

            // Stop any existing video playback
            StopVideoPlayback();

            // Clear any previous video state
            _isVideoActive = false;

            // Dispose old frame if exists
            _currentMediaFrame?.Dispose();
            _currentMediaFrame = null;

            // Load image directly into memory
            System.Diagnostics.Debug.WriteLine($"[LoadImageFile] Loading image into memory...");
            var imageProcessor = new Media.ImageProcessor();
            _currentMediaFrame = imageProcessor.Load(path);

            if (_currentMediaFrame == null || _currentMediaFrame.Empty())
            {
                System.Diagnostics.Debug.WriteLine($"[LoadImageFile] ❌ Failed to load image");
                StatusText.Text = "Failed to load image";
                return;
            }

            System.Diagnostics.Debug.WriteLine($"[LoadImageFile] ✓ Image loaded: {_currentMediaFrame.Width}x{_currentMediaFrame.Height}");

            // Reset framing settings
            ResetFramingSettings();

            // Update pan slider ranges for new media
            UpdatePanSliderRanges();

            // Render and display
            System.Diagnostics.Debug.WriteLine($"[LoadImageFile] Rendering image...");
            RenderCurrentMedia();

            System.Diagnostics.Debug.WriteLine($"[LoadImageFile] ✓ Image loaded and displayed successfully");

            // Disable video playback controls
            UpdatePlaybackControlsState(false);
            System.Diagnostics.Debug.WriteLine($"[LoadImageFile] Playback controls disabled");
            System.Diagnostics.Debug.WriteLine($"========================================");
        }

        private void LoadVideo(string path)
        {
            System.Diagnostics.Debug.WriteLine($"========================================");
            System.Diagnostics.Debug.WriteLine($"[LoadVideo] File detected: {path}");
            System.Diagnostics.Debug.WriteLine($"[LoadVideo] Media type: VIDEO");

            // Stop any existing video playback
            StopVideoPlayback();

            // Open the video
            System.Diagnostics.Debug.WriteLine($"[LoadVideo] Opening video with VideoPlayer...");
            if (!_videoPlayer.Open(path))
            {
                string errorMsg = $"Failed to open video: {Path.GetFileName(path)}";
                StatusText.Text = errorMsg;
                System.Diagnostics.Debug.WriteLine($"[LoadVideo] ❌ Open FAILED: {errorMsg}");
                System.Diagnostics.Debug.WriteLine($"[LoadVideo] Possible reasons:");
                System.Diagnostics.Debug.WriteLine($"  - File is corrupted");
                System.Diagnostics.Debug.WriteLine($"  - Codec not supported");
                System.Diagnostics.Debug.WriteLine($"  - File is in use by another application");
                System.Diagnostics.Debug.WriteLine($"========================================");

                MessageBox.Show(
                    $"Failed to open video file:\n\n{Path.GetFileName(path)}\n\nPossible reasons:\n• File is corrupted\n• Codec not supported\n• File is in use",
                    "Video Load Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
                return;
            }

            System.Diagnostics.Debug.WriteLine($"[LoadVideo] ✓ Open succeeded");
            System.Diagnostics.Debug.WriteLine($"[LoadVideo] Width: {_videoPlayer.Width}");
            System.Diagnostics.Debug.WriteLine($"[LoadVideo] Height: {_videoPlayer.Height}");
            System.Diagnostics.Debug.WriteLine($"[LoadVideo] FPS: {_videoPlayer.FPS:F2}");
            System.Diagnostics.Debug.WriteLine($"[LoadVideo] Frame Count: {_videoPlayer.FrameCount}");
            System.Diagnostics.Debug.WriteLine($"[LoadVideo] Duration: {_videoPlayer.Duration}");

            // Create new playback engine
            _playbackEngine = new Media.PlaybackEngine(_videoPlayer);
            _playbackEngine.FrameReady += PlaybackEngine_FrameReady;
            System.Diagnostics.Debug.WriteLine($"[LoadVideo] PlaybackEngine created");

            // Display the first frame
            System.Diagnostics.Debug.WriteLine($"[LoadVideo] Decoding first frame...");
            bool firstFrameSuccess = DisplayFirstFrame();

            if (firstFrameSuccess)
            {
                System.Diagnostics.Debug.WriteLine($"[LoadVideo] ✓ First frame decoded and displayed successfully");
                System.Diagnostics.Debug.WriteLine($"[LoadVideo] Preview should now show the first frame");
                StatusText.Text = $"{Path.GetFileName(path)} (Video ready - press Play)";
            }
            else
            {
                System.Diagnostics.Debug.WriteLine($"[LoadVideo] ❌ First frame decode FAILED");
                StatusText.Text = $"Video opened but first frame failed";
            }

            // Enable video playback controls
            UpdatePlaybackControlsState(true);
            System.Diagnostics.Debug.WriteLine($"[LoadVideo] Playback controls enabled");
            System.Diagnostics.Debug.WriteLine($"[LoadVideo] Video load complete - ready for playback");
            System.Diagnostics.Debug.WriteLine($"========================================");
        }

        private bool DisplayFirstFrame()
        {
            if (!_videoPlayer.IsOpened)
            {
                System.Diagnostics.Debug.WriteLine($"[DisplayFirstFrame] ❌ VideoPlayer not opened");
                return false;
            }

            // Seek to first frame
            System.Diagnostics.Debug.WriteLine($"[DisplayFirstFrame] Seeking to frame 0...");
            if (!_videoPlayer.Seek(0))
            {
                System.Diagnostics.Debug.WriteLine($"[DisplayFirstFrame] ❌ Seek to frame 0 failed");
                return false;
            }

            // Read and store the first frame
            System.Diagnostics.Debug.WriteLine($"[DisplayFirstFrame] Reading first frame...");
            if (_videoPlayer.ReadNextFrame(out Mat frame))
            {
                try
                {
                    System.Diagnostics.Debug.WriteLine($"[DisplayFirstFrame] ✓ Frame read: {frame.Width}x{frame.Height}");

                    // Store frame in memory for viewport operations
                    _currentMediaFrame?.Dispose();
                    _currentMediaFrame = frame.Clone();
                    _isVideoActive = true;

                    System.Diagnostics.Debug.WriteLine($"[DisplayFirstFrame] ✓ Frame stored in memory");

                    // Reset framing settings
                    ResetFramingSettings();

                    // Update pan slider ranges for new media
                    UpdatePanSliderRanges();

                    // Render and display
                    System.Diagnostics.Debug.WriteLine($"[DisplayFirstFrame] Rendering through ViewportEngine...");
                    RenderCurrentMedia();

                    System.Diagnostics.Debug.WriteLine($"[DisplayFirstFrame] ✓ Frame rendered and pushed to output targets");
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[DisplayFirstFrame] ❌ Error rendering/displaying: {ex.Message}");
                    frame.Dispose();
                    return false;
                }
                finally
                {
                    frame.Dispose();
                }

                // Reset to first frame for playback
                System.Diagnostics.Debug.WriteLine($"[DisplayFirstFrame] Resetting to frame 0 for playback...");
                _videoPlayer.Seek(0);

                return true;
            }
            else
            {
                System.Diagnostics.Debug.WriteLine($"[DisplayFirstFrame] ❌ Failed to read first frame");
                return false;
            }
        }

        private void StopVideoPlayback()
        {
            if (_playbackEngine != null)
            {
                _playbackEngine.Stop();
                _playbackEngine.FrameReady -= PlaybackEngine_FrameReady;
                _playbackEngine.Dispose();
                _playbackEngine = null;
            }

            if (_videoPlayer.IsOpened)
            {
                _videoPlayer.Close();
            }
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

                    // Update stored frame for viewport operations
                    _currentMediaFrame?.Dispose();
                    _currentMediaFrame = frame.Image.Clone();

                    // Render and display
                    RenderCurrentMedia();
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[PlaybackEngine_FrameReady] Error: {ex.Message}");
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
                System.Diagnostics.Debug.WriteLine("[VideoPlayback] Play clicked");
            }
        }

        private void PauseButton_Click(object sender, RoutedEventArgs e)
        {
            if (_playbackEngine != null)
            {
                _playbackEngine.Pause();
                System.Diagnostics.Debug.WriteLine("[VideoPlayback] Pause clicked");
            }
        }

        private void StopButton_Click(object sender, RoutedEventArgs e)
        {
            if (_playbackEngine != null)
            {
                _playbackEngine.Stop();

                // Display first frame when stopped
                DisplayFirstFrame();

                System.Diagnostics.Debug.WriteLine("[VideoPlayback] Stop clicked");
            }
        }

        private void LoopCheckBox_Changed(object sender, RoutedEventArgs e)
        {
            if (_playbackEngine != null)
            {
                _playbackEngine.Loop = LoopCheckBox.IsChecked == true;
                System.Diagnostics.Debug.WriteLine($"[VideoPlayback] Loop = {_playbackEngine.Loop}");
            }
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
            if (ZoomValueText == null) return;

            _framingSettings.Zoom = ZoomSlider.Value;

            ZoomValueText.Text = $"{ZoomSlider.Value * 100:0}%";

            // Update pan slider ranges based on new zoom level
            UpdatePanSliderRanges();

            RenderCurrentMedia();
        }

        private void XSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (_suppressSliderEvents) return;

            // Guard: Skip if controls not fully initialized yet (during XAML loading)
            if (HorizontalValueText == null) return;

            // Clamp to valid pan bounds
            var (minX, maxX, minY, maxY) = CalculatePanBounds();
            double clampedX = System.Math.Max(minX, System.Math.Min(maxX, XSlider.Value));
            double clampedY = System.Math.Max(minY, System.Math.Min(maxY, YSlider.Value));

            _framingSettings.OffsetX = clampedX;
            _framingSettings.OffsetY = clampedY;

            HorizontalValueText.Text = $"{clampedX:0}";

            RenderCurrentMedia();
        }

        private void YSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (_suppressSliderEvents) return;

            // Guard: Skip if controls not fully initialized yet (during XAML loading)
            if (VerticalValueText == null) return;

            // Clamp to valid pan bounds
            var (minX, maxX, minY, maxY) = CalculatePanBounds();
            double clampedX = System.Math.Max(minX, System.Math.Min(maxX, XSlider.Value));
            double clampedY = System.Math.Max(minY, System.Math.Min(maxY, YSlider.Value));

            _framingSettings.OffsetX = clampedX;
            _framingSettings.OffsetY = clampedY;

            VerticalValueText.Text = $"{clampedY:0}";

            RenderCurrentMedia();
        }

        private void RotationSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (_suppressSliderEvents) return;

            // Guard: Skip if controls not fully initialized yet (during XAML loading)
            if (RotationValueText == null) return;

            _framingSettings.Rotation = RotationSlider.Value;

            RotationValueText.Text = $"{RotationSlider.Value:0}°";

            RenderCurrentMedia();
        }

        private void ResetButton_Click(object sender, RoutedEventArgs e)
        {
            ResetView();
        }

        private void ResetView()
        {
            ResetFramingSettings();

            _suppressSliderEvents = true;
            try
            {
                ZoomSlider.Value = 1;
                XSlider.Value = 0;
                YSlider.Value = 0;
                RotationSlider.Value = 0;
            }
            finally
            {
                _suppressSliderEvents = false;
            }

            RenderCurrentMedia();
        }

        /// <summary>
        /// Reset framing settings to default
        /// </summary>
        private void ResetFramingSettings()
        {
            _framingSettings.Zoom = 1.0;
            _framingSettings.OffsetX = 0;
            _framingSettings.OffsetY = 0;
            _framingSettings.Rotation = 0;
        }

        /// <summary>
        /// Render the current media frame (image or video) with current framing settings
        /// </summary>
        private void RenderCurrentMedia()
        {
            if (_currentMediaFrame == null || _currentMediaFrame.Empty())
                return;

            // Get canvas dimensions
            int canvasWidth = ActiveProfile?.DisplayWidth ?? 1080;
            int canvasHeight = ActiveProfile?.DisplayHeight ?? 1920;

            // Render through ViewportEngine
            using var renderedFrame = _viewportEngine.Render(
                _currentMediaFrame,
                canvasWidth,
                canvasHeight,
                _framingSettings);

            // Push to output manager
            _outputManager.PushFrame(renderedFrame);
        }

        /// <summary>
        /// Calculate pan bounds based on media size, zoom level, and canvas size.
        /// Returns (minX, maxX, minY, maxY) that allow viewing all pixels of the zoomed media
        /// while preventing empty space around edges.
        /// </summary>
        private (double minX, double maxX, double minY, double maxY) CalculatePanBounds()
        {
            if (_currentMediaFrame == null || _currentMediaFrame.Empty())
                return (0, 0, 0, 0);

            // Get canvas dimensions
            int canvasWidth = ActiveProfile?.DisplayWidth ?? 1080;
            int canvasHeight = ActiveProfile?.DisplayHeight ?? 1920;

            // Get current zoom
            double zoom = ZoomSlider.Value;

            // Calculate base scale to fit source onto canvas (maintaining aspect ratio)
            double baseScale = System.Math.Min(
                (double)canvasWidth / _currentMediaFrame.Width,
                (double)canvasHeight / _currentMediaFrame.Height);

            // Apply zoom to get total scale
            double totalScale = baseScale * zoom;

            // Calculate scaled media dimensions
            double scaledWidth = _currentMediaFrame.Width * totalScale;
            double scaledHeight = _currentMediaFrame.Height * totalScale;

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
            if (XSlider == null || YSlider == null || _currentMediaFrame == null)
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
        /// Left-click drag = Pan
        /// Left double-click = Auto Fit (reset zoom and offsets only, keep rotation)
        /// Middle-click = Reset (same as Auto Fit)
        /// </summary>
        private void PreviewBorder_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (_currentMediaFrame == null || _currentMediaFrame.Empty())
                return;

            if (e.LeftButton == MouseButtonState.Pressed)
            {
                // Detect double-click
                if (e.ClickCount == 2)
                {
                    AutoFitPreview();
                    e.Handled = true;
                    return;
                }

                if (!_isDragging)
                {
                    _isDragging = true;
                    _lastMousePosition = e.GetPosition(PreviewBorder);
                    PreviewBorder.CaptureMouse();
                    PreviewBorder.Cursor = Cursors.Hand;
                    e.Handled = true;
                }
            }
            else if (e.MiddleButton == MouseButtonState.Pressed)
            {
                AutoFitPreview();
                e.Handled = true;
            }
        }

        /// <summary>
        /// Release left-click drag
        /// </summary>
        private void PreviewBorder_MouseUp(object sender, MouseButtonEventArgs e)
        {
            if (_isDragging && e.LeftButton == MouseButtonState.Released)
            {
                _isDragging = false;
                PreviewBorder.ReleaseMouseCapture();
                PreviewBorder.Cursor = Cursors.Arrow;
                e.Handled = true;
            }
        }

        /// <summary>
        /// Left-click drag = Pan with clamping to prevent empty space around media
        /// </summary>
        private void PreviewBorder_MouseMove(object sender, MouseEventArgs e)
        {
            if (!_isDragging || _currentMediaFrame == null || _currentMediaFrame.Empty())
                return;

            WpfPoint currentPosition = e.GetPosition(PreviewBorder);
            Vector delta = currentPosition - _lastMousePosition;

            // Apply pan offset based on mouse movement
            double newX = XSlider.Value + delta.X;
            double newY = YSlider.Value + delta.Y;

            // Calculate dynamic pan limits based on current zoom and media size
            var (minX, maxX, minY, maxY) = CalculatePanBounds();

            // Clamp pan to calculated bounds
            newX = System.Math.Max(minX, System.Math.Min(maxX, newX));
            newY = System.Math.Max(minY, System.Math.Min(maxY, newY));

            XSlider.Value = newX;
            YSlider.Value = newY;

            _lastMousePosition = currentPosition;
            e.Handled = true;
        }

        /// <summary>
        /// Mouse wheel = Smooth zoom with consistent steps
        /// </summary>
        private void PreviewBorder_MouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (_currentMediaFrame == null || _currentMediaFrame.Empty())
                return;

            // Smooth zoom step: +0.1 per wheel click (120 delta units)
            double zoomStep = 0.1 * (e.Delta / 120.0);
            double newZoom = ZoomSlider.Value + zoomStep;

            // Clamp to zoom range
            newZoom = System.Math.Max(ZoomSlider.Minimum, System.Math.Min(ZoomSlider.Maximum, newZoom));

            ZoomSlider.Value = newZoom;

            e.Handled = true;
        }

        /// <summary>
        /// Auto Fit = Reset zoom and offsets only (rotation stays unchanged)
        /// </summary>
        private void AutoFitPreview()
        {
            if (_currentMediaFrame == null || _currentMediaFrame.Empty())
                return;

            ZoomSlider.Value = 1.0;
            XSlider.Value = 0;
            YSlider.Value = 0;

            RenderCurrentMedia();
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

            // Remember the selection
            SaveLastSelectedProfile(profileName);

            // Immediately redraw with the new profile's dimensions
            RenderCurrentMedia();
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
        // OBS Connection (Temporary Test)
        // ============================================

        /// <summary>
        /// Handles the Connect OBS button click (temporary test).
        /// Attempts to connect to OBS WebSocket and displays the result.
        /// </summary>
        private async void ConnectOBSButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Disable button during connection attempt
                ConnectOBSButton.IsEnabled = false;
                ConnectOBSButton.Content = "Connecting...";
                StatusText.Text = "Connecting to OBS...";

                // Attempt to connect
                bool connected = await _obsClient.ConnectAsync();

                if (connected)
                {
                    ConnectOBSButton.Content = "Connected ✓";
                    ConnectOBSButton.Background = System.Windows.Media.Brushes.Green;
                    StatusText.Text = "Connected to OBS";
                    MessageBox.Show("Connected to OBS", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                    ConnectOBSButton.Content = "Connect OBS";
                    ConnectOBSButton.IsEnabled = true;
                    StatusText.Text = "Failed to connect to OBS";
                    MessageBox.Show("Failed to connect to OBS.\n\nMake sure OBS Studio is running and WebSocket server is enabled.", 
                                    "Connection Failed", 
                                    MessageBoxButton.OK, 
                                    MessageBoxImage.Warning);
                }
            }
            catch (Exception ex)
            {
                ConnectOBSButton.Content = "Connect OBS";
                ConnectOBSButton.IsEnabled = true;
                StatusText.Text = $"Error: {ex.Message}";
                MessageBox.Show($"Error connecting to OBS: {ex.Message}", 
                                "Error", 
                                MessageBoxButton.OK, 
                                MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// Handles the Refresh OBS Status button click (temporary test).
        /// Retrieves and displays the current OBS status information.
        /// </summary>
        private async void RefreshOBSStatusButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (!_obsClient.IsConnected)
                {
                    StatusText.Text = "Not connected to OBS";
                    MessageBox.Show("Please connect to OBS first.", 
                                    "Not Connected", 
                                    MessageBoxButton.OK, 
                                    MessageBoxImage.Warning);
                    return;
                }

                // Disable button during status retrieval
                RefreshOBSStatusButton.IsEnabled = false;
                RefreshOBSStatusButton.Content = "Refreshing...";
                StatusText.Text = "Retrieving OBS status...";

                // Get status
                var status = await _obsClient.GetStatusAsync();

                // Re-enable button
                RefreshOBSStatusButton.IsEnabled = true;
                RefreshOBSStatusButton.Content = "Refresh OBS Status";

                if (status != null)
                {
                    StatusText.Text = "OBS status retrieved";

                    // Display status in a message box
                    string statusMessage = $"OBS Studio Status\n" +
                                         $"━━━━━━━━━━━━━━━━━━━━━━━━━━━━\n\n" +
                                         $"Version: {status.OBSVersion}\n" +
                                         $"WebSocket: {status.WebSocketVersion}\n\n" +
                                         $"Current Scene: {status.CurrentScene}\n\n" +
                                         $"Virtual Camera: {(status.VirtualCameraActive ? "✓ Active" : "✗ Inactive")}\n" +
                                         $"Recording: {(status.RecordingActive ? "✓ Active" : "✗ Inactive")}\n" +
                                         $"Streaming: {(status.StreamingActive ? "✓ Active" : "✗ Inactive")}";

                    MessageBox.Show(statusMessage, 
                                    "OBS Status", 
                                    MessageBoxButton.OK, 
                                    MessageBoxImage.Information);
                }
                else
                {
                    StatusText.Text = "Failed to retrieve OBS status";
                    MessageBox.Show("Failed to retrieve OBS status.\n\nMake sure you are connected to OBS.", 
                                    "Error", 
                                    MessageBoxButton.OK, 
                                    MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                RefreshOBSStatusButton.IsEnabled = true;
                RefreshOBSStatusButton.Content = "Refresh OBS Status";
                StatusText.Text = $"Error: {ex.Message}";
                MessageBox.Show($"Error retrieving OBS status: {ex.Message}", 
                                "Error", 
                                MessageBoxButton.OK, 
                                MessageBoxImage.Error);
            }
        }

        // ============================================
        // OBS Virtual Camera Control (Temporary Test)
        // ============================================

        /// <summary>
        /// Handles the Start Virtual Camera button click (temporary test).
        /// Starts the OBS virtual camera and displays the result.
        /// </summary>
        private async void StartVirtualCameraButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (!_obsClient.IsConnected)
                {
                    StatusText.Text = "Not connected to OBS";
                    MessageBox.Show("Please connect to OBS first.", 
                                    "Not Connected", 
                                    MessageBoxButton.OK, 
                                    MessageBoxImage.Warning);
                    return;
                }

                // Disable button during operation
                StartVirtualCameraButton.IsEnabled = false;
                StartVirtualCameraButton.Content = "Starting...";
                StatusText.Text = "Starting virtual camera...";

                // Start virtual camera
                bool success = await _obsClient.StartVirtualCameraAsync();

                // Re-enable button
                StartVirtualCameraButton.IsEnabled = true;
                StartVirtualCameraButton.Content = "Start Virtual Camera";

                if (success)
                {
                    StatusText.Text = "Virtual camera started";
                    MessageBox.Show("OBS Virtual Camera started successfully!", 
                                    "Success", 
                                    MessageBoxButton.OK, 
                                    MessageBoxImage.Information);
                }
                else
                {
                    StatusText.Text = "Failed to start virtual camera";
                    MessageBox.Show("Failed to start OBS Virtual Camera.\n\nMake sure OBS is connected and the virtual camera is not already running.", 
                                    "Failed", 
                                    MessageBoxButton.OK, 
                                    MessageBoxImage.Warning);
                }
            }
            catch (Exception ex)
            {
                StartVirtualCameraButton.IsEnabled = true;
                StartVirtualCameraButton.Content = "Start Virtual Camera";
                StatusText.Text = $"Error: {ex.Message}";
                MessageBox.Show($"Error starting virtual camera: {ex.Message}", 
                                "Error", 
                                MessageBoxButton.OK, 
                                MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// Handles the Stop Virtual Camera button click (temporary test).
        /// Stops the OBS virtual camera and displays the result.
        /// </summary>
        private async void StopVirtualCameraButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (!_obsClient.IsConnected)
                {
                    StatusText.Text = "Not connected to OBS";
                    MessageBox.Show("Please connect to OBS first.", 
                                    "Not Connected", 
                                    MessageBoxButton.OK, 
                                    MessageBoxImage.Warning);
                    return;
                }

                // Disable button during operation
                StopVirtualCameraButton.IsEnabled = false;
                StopVirtualCameraButton.Content = "Stopping...";
                StatusText.Text = "Stopping virtual camera...";

                // Stop virtual camera
                bool success = await _obsClient.StopVirtualCameraAsync();

                // Re-enable button
                StopVirtualCameraButton.IsEnabled = true;
                StopVirtualCameraButton.Content = "Stop Virtual Camera";

                if (success)
                {
                    StatusText.Text = "Virtual camera stopped";
                    MessageBox.Show("OBS Virtual Camera stopped successfully!", 
                                    "Success", 
                                    MessageBoxButton.OK, 
                                    MessageBoxImage.Information);
                }
                else
                {
                    StatusText.Text = "Failed to stop virtual camera";
                    MessageBox.Show("Failed to stop OBS Virtual Camera.\n\nMake sure OBS is connected and the virtual camera is running.", 
                                    "Failed", 
                                    MessageBoxButton.OK, 
                                    MessageBoxImage.Warning);
                }
            }
            catch (Exception ex)
            {
                StopVirtualCameraButton.IsEnabled = true;
                StopVirtualCameraButton.Content = "Stop Virtual Camera";
                StatusText.Text = $"Error: {ex.Message}";
                MessageBox.Show($"Error stopping virtual camera: {ex.Message}", 
                                "Error", 
                                MessageBoxButton.OK, 
                                MessageBoxImage.Error);
            }
        }

        // ============================================
        // OBS Scene Setup (Temporary Test)
        // ============================================

        /// <summary>
        /// Handles the Setup OBS Scene button click (temporary test).
        /// Ensures the VirtualCam Studio scene exists and switches to it.
        /// </summary>
        private async void SetupOBSSceneButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (!_obsClient.IsConnected)
                {
                    StatusText.Text = "Not connected to OBS";
                    MessageBox.Show("Please connect to OBS first.", 
                                    "Not Connected", 
                                    MessageBoxButton.OK, 
                                    MessageBoxImage.Warning);
                    return;
                }

                // Disable button during operation
                SetupOBSSceneButton.IsEnabled = false;
                SetupOBSSceneButton.Content = "Setting up...";
                StatusText.Text = "Setting up OBS scene...";

                // Ensure and switch to VirtualCam Studio scene
                bool success = await _obsSceneService.EnsureVirtualCamSceneAsync();

                // Re-enable button
                SetupOBSSceneButton.IsEnabled = true;
                SetupOBSSceneButton.Content = "Setup OBS Scene";

                if (success)
                {
                    StatusText.Text = "Scene ready";
                    MessageBox.Show("Scene Ready\n\nThe 'VirtualCam Studio' scene is now active in OBS.", 
                                    "Success", 
                                    MessageBoxButton.OK, 
                                    MessageBoxImage.Information);
                }
                else
                {
                    StatusText.Text = "Scene setup failed";
                    MessageBox.Show("Scene Setup Failed\n\nUnable to create or switch to the VirtualCam Studio scene.\n\nMake sure OBS is connected and running properly.", 
                                    "Failed", 
                                    MessageBoxButton.OK, 
                                    MessageBoxImage.Warning);
                }
            }
            catch (Exception ex)
            {
                SetupOBSSceneButton.IsEnabled = true;
                SetupOBSSceneButton.Content = "Setup OBS Scene";
                StatusText.Text = $"Error: {ex.Message}";
                MessageBox.Show($"Error setting up OBS scene: {ex.Message}", 
                                "Error", 
                                MessageBoxButton.OK, 
                                MessageBoxImage.Error);
            }
        }

        // ============================================
        // OBS Source Setup (Temporary Test)
        // ============================================

        /// <summary>
        /// Handles the Setup Preview Source button click (temporary test).
        /// Ensures the VirtualCam Preview image source exists in the VirtualCam Studio scene.
        /// </summary>
        private async void SetupPreviewSourceButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (!_obsClient.IsConnected)
                {
                    StatusText.Text = "Not connected to OBS";
                    MessageBox.Show("Please connect to OBS first.", 
                                    "Not Connected", 
                                    MessageBoxButton.OK, 
                                    MessageBoxImage.Warning);
                    return;
                }

                // Check if we have an output image to use
                if (!_obsImageOutput.FileExists)
                {
                    StatusText.Text = "No preview image available";
                    MessageBox.Show("No Preview Image Available\n\nPlease load a media item first to generate a preview image.", 
                                    "No Image", 
                                    MessageBoxButton.OK, 
                                    MessageBoxImage.Warning);
                    return;
                }

                // Disable button during operation
                SetupPreviewSourceButton.IsEnabled = false;
                SetupPreviewSourceButton.Content = "Setting up...";
                StatusText.Text = "Setting up preview source...";

                // Ensure the preview source exists and is configured
                bool success = await _obsSourceService.EnsurePreviewSourceAsync(_obsImageOutput.OutputPath);

                // Re-enable button
                SetupPreviewSourceButton.IsEnabled = true;
                SetupPreviewSourceButton.Content = "Setup Preview Source";

                if (success)
                {
                    StatusText.Text = "Preview source ready";
                    MessageBox.Show("Preview Source Ready\n\nThe 'VirtualCam Preview' image source is now configured in OBS.", 
                                    "Success", 
                                    MessageBoxButton.OK, 
                                    MessageBoxImage.Information);
                }
                else
                {
                    StatusText.Text = "Source setup failed";
                    MessageBox.Show("Source Setup Failed\n\nUnable to create or update the preview source.\n\nMake sure OBS is connected and the VirtualCam Studio scene exists.", 
                                    "Failed", 
                                    MessageBoxButton.OK, 
                                    MessageBoxImage.Warning);
                }
            }
            catch (Exception ex)
            {
                SetupPreviewSourceButton.IsEnabled = true;
                SetupPreviewSourceButton.Content = "Setup Preview Source";
                StatusText.Text = $"Error: {ex.Message}";
                MessageBox.Show($"Error setting up preview source: {ex.Message}", 
                                "Error", 
                                MessageBoxButton.OK, 
                                MessageBoxImage.Error);
            }
        }

        // ============================================
            }
        }


