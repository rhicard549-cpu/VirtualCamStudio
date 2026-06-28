using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using VirtualCamStudio.Helpers;
using VirtualCamStudio.Models;
using VirtualCamStudio.Services;

namespace VirtualCamStudio
{
    public partial class MainWindow : Window
    {
        private readonly RenderService _renderService = new();
        private readonly CameraProfileService _profileService = new();

        // Mouse drag state
        private bool _isDragging = false;
        private Point _lastMousePosition = new Point(0, 0);

        /// <summary>
        /// The currently selected camera profile
        /// </summary>
        public CameraProfile? ActiveProfile { get; set; }

        public MainWindow()
        {
            InitializeComponent();
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

            string file = files[0];

            string ext = Path.GetExtension(file).ToLower();

            if (ext == ".jpg" ||
                ext == ".jpeg" ||
                ext == ".png" ||
                ext == ".bmp")
            {
                LoadImage(file);
            }
        }

        private void LoadImage(string path)
        {
            _renderService.LoadImage(path);

            PreviewImage.Source =
                MatToBitmapSource.Convert(_renderService.Render(ActiveProfile));

            DropText.Visibility = Visibility.Collapsed;

            StatusText.Text = Path.GetFileName(path);
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

            PreviewImage.Source =
                MatToBitmapSource.Convert(_renderService.Render(ActiveProfile));
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
                e.Handled = true;
            }
        }

        /// <summary>
        /// Left-click drag = Pan
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

            // Clamp to slider ranges
            newX = System.Math.Max(XSlider.Minimum, System.Math.Min(XSlider.Maximum, newX));
            newY = System.Math.Max(YSlider.Minimum, System.Math.Min(YSlider.Maximum, newY));

            XSlider.Value = newX;
            YSlider.Value = newY;

            _lastMousePosition = currentPosition;
            e.Handled = true;
        }

        /// <summary>
        /// Mouse wheel = Zoom at cursor
        /// </summary>
        private void PreviewBorder_MouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (!_renderService.HasImage)
                return;

            // Zoom step: +0.1 per wheel click
            double zoomStep = 0.1;
            double newZoom = ZoomSlider.Value + (e.Delta > 0 ? zoomStep : -zoomStep);

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

        // ============================================
            }
        }


