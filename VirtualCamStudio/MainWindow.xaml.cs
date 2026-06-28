using System;
using System.IO;
using System.Windows;
using System.Windows.Media.Imaging;
using VirtualCamStudio.Helpers;
using VirtualCamStudio.Services;

namespace VirtualCamStudio
{
    public partial class MainWindow : Window
    {
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

        private readonly RenderService _renderService = new();

        private void LoadImage(string path)
        {
            var frame = _renderService.RenderImage(path);

            PreviewImage.Source = MatToBitmapSource.Convert(frame);

            DropText.Visibility = Visibility.Collapsed;
        }
    }
}