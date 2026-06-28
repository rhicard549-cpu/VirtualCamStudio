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
using VirtualCamStudio.Helpers;
using VirtualCamStudio.Models;
using VirtualCamStudio.Services;
using Cv2 = OpenCvSharp.Cv2;

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
    public partial class MainWindow : Window
    {
        private readonly RenderService _renderService = new();
        private readonly CameraProfileService _profileService = new();
        private readonly OutputManager _outputManager = new();
        private readonly VirtualCameraService _virtualCamera = new();
        private readonly Services.OBS.OBSClient _obsClient = new();

        // Mouse drag state
        private bool _isDragging = false;
        private Point _lastMousePosition = new Point(0, 0);

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
            InitializeComponent();
            MediaListBox.ItemsSource = MediaItems;

            // Register preview as an output target
            var previewTarget = new PreviewOutputTarget(PreviewImage);
            _outputManager.RegisterTarget(previewTarget);

            // Register virtual camera as an output target
            _outputManager.RegisterTarget(_virtualCamera);

            // Register keyboard shortcuts
            KeyDown += MainWindow_KeyDown;

            // Register preview size changed handler for safe area overlay
            PreviewGrid.SizeChanged += PreviewGrid_SizeChanged;
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            LoadAndPopulateCameraProfiles();
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
                string ext = Path.GetExtension(file).ToLower();

                if (ext == ".jpg" ||
                    ext == ".jpeg" ||
                    ext == ".png" ||
                    ext == ".bmp")
                {
                    AddMediaToLibrary(file);
                    anyAdded = true;
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
        /// Creates a thumbnail for the given image file.
        /// Thumbnail is fully loaded into memory and the file handle is released.
        /// This ensures smooth scrolling without disk I/O.
        /// Future-compatible: can be extended to generate video thumbnails.
        /// </summary>
        private BitmapSource? CreateThumbnail(string filePath)
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
            catch (Exception)
            {
                // Return null if thumbnail generation fails
                // Could be extended to return a placeholder image in the future
                return null;
            }
        }

        private void LoadImage(string path)
        {
            _renderService.LoadImage(path);

            using var frame = _renderService.Render(ActiveProfile);
            _outputManager.PushFrame(frame);

            DropText.Visibility = Visibility.Collapsed;

            StatusText.Text = Path.GetFileName(path);
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
            _renderService.SetZoom(ZoomSlider.Value);

            ZoomValueText.Text = $"{ZoomSlider.Value * 100:0}%";

            RenderPreview();
        }

        private void XSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            _renderService.SetOffset(
                XSlider.Value,
                YSlider.Value);

            HorizontalValueText.Text = $"{XSlider.Value:0}";

            RenderPreview();
        }

        private void YSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            _renderService.SetOffset(
                XSlider.Value,
                YSlider.Value);

            VerticalValueText.Text = $"{YSlider.Value:0}";

            RenderPreview();
        }

        private void RotationSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            _renderService.SetRotation(RotationSlider.Value);

            RotationValueText.Text = $"{RotationSlider.Value:0}°";

            RenderPreview();
        }

        private void ResetButton_Click(object sender, RoutedEventArgs e)
        {
            ResetView();
        }

        private void ResetView()
        {
            _renderService.Reset();

            ZoomSlider.Value = 1;
            XSlider.Value = 0;
            YSlider.Value = 0;
            RotationSlider.Value = 0;

            RenderPreview();
        }

        private void RenderPreview()
        {
            if (!_renderService.HasImage)
                return;

            using var frame = _renderService.Render(ActiveProfile);
            _outputManager.PushFrame(frame);
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
            if (!_renderService.HasImage)
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
        /// Left-click drag = Pan with clamping to keep image partially visible
        /// </summary>
        private void PreviewBorder_MouseMove(object sender, MouseEventArgs e)
        {
            if (!_isDragging || !_renderService.HasImage)
                return;

            Point currentPosition = e.GetPosition(PreviewBorder);
            Vector delta = currentPosition - _lastMousePosition;

            // Apply pan offset based on mouse movement
            double newX = XSlider.Value + delta.X;
            double newY = YSlider.Value + delta.Y;

            // Calculate dynamic pan limits based on zoom level to keep image partially visible
            // At higher zoom, allow more pan; at lower zoom, restrict pan range
            double zoom = ZoomSlider.Value;
            double panLimitFactor = zoom * 600; // Base limit that scales with zoom

            // Clamp pan to keep at least 20% of the image visible
            double maxPan = panLimitFactor;
            double minPan = -panLimitFactor;

            newX = System.Math.Max(minPan, System.Math.Min(maxPan, newX));
            newY = System.Math.Max(minPan, System.Math.Min(maxPan, newY));

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
            if (!_renderService.HasImage)
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
            if (!_renderService.HasImage)
                return;

            ZoomSlider.Value = 1.0;
            XSlider.Value = 0;
            YSlider.Value = 0;

            RenderPreview();
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
            RenderPreview();
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

        // ============================================
            }
        }


