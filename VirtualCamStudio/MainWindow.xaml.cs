using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using VirtualCamStudio.Helpers;
using VirtualCamStudio.Services;

namespace VirtualCamStudio
{
    public partial class MainWindow : Window
    {
        private readonly RenderService _renderService = new();

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
    }
}