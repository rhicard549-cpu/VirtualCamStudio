using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using VirtualCamStudio.Helpers;
using VirtualCamStudio.Services;

namespace VirtualCamStudio
{
    public partial class MainWindow : Window
    {
        private readonly RenderService _renderService = new();

        // Mouse drag state
        private bool _isDragging = false;
        private Point _lastMousePosition = new Point(0, 0);
        private DateTime _lastClickTime = DateTime.MinValue;
        private const double DoubleClickThresholdMs = 300;

        public MainWindow()
        {
            InitializeComponent();
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
                MatToBitmapSource.Convert(_renderService.Render());

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
                MatToBitmapSource.Convert(_renderService.Render());
        }

        // ============================================
        // Mouse Control Handlers
        // ============================================

        /// <summary>
        /// Left-click drag = Pan
        /// Middle-click = Reset View
        /// Double-click = Auto Fit
        /// </summary>
        private void PreviewBorder_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (!_renderService.HasImage)
                return;

            // Detect double-click
            DateTime now = DateTime.Now;
            if ((now - _lastClickTime).TotalMilliseconds < DoubleClickThresholdMs)
            {
                // This is a double-click
                AutoFit();
                _lastClickTime = DateTime.MinValue; // Reset to prevent triple-click
                e.Handled = true;
                return;
            }
            _lastClickTime = now;

            if (e.LeftButton == MouseButtonState.Pressed)
            {
                _isDragging = true;
                _lastMousePosition = e.GetPosition(PreviewBorder);
                PreviewBorder.CaptureMouse();
                e.Handled = true;
            }
            else if (e.MiddleButton == MouseButtonState.Pressed)
            {
                ResetView();
                e.Handled = true;
            }
        }

        /// <summary>
        /// Left-click drag = Pan
        /// </summary>
        private void PreviewBorder_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (!_renderService.HasImage)
                return;

            if (e.LeftButton == MouseButtonState.Pressed && !_isDragging)
            {
                _isDragging = true;
                _lastMousePosition = e.GetPosition(PreviewBorder);
                PreviewBorder.CaptureMouse();
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
        /// Auto Fit = Reset zoom and pan to center
        /// </summary>
        private void AutoFit()
        {
            if (!_renderService.HasImage)
                return;

            ZoomSlider.Value = 1.0;
            XSlider.Value = 0;
            YSlider.Value = 0;

            RenderPreview();
        }
    }
}

