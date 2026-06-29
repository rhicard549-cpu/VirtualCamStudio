using System;
using System.Windows.Media.Imaging;
using VirtualCamStudio.Core;
using VirtualCamStudio.Helpers;
using WpfImage = System.Windows.Controls.Image;

namespace VirtualCamStudio.Services
{
    /// <summary>
    /// Output target that displays frames in a WPF Image control (preview).
    /// Converts Frame to BitmapSource and updates the Image.Source property.
    /// </summary>
    public class PreviewOutputTarget : IOutputTarget
    {
        private readonly WpfImage _previewImage;

        /// <summary>
        /// Creates a new preview output target.
        /// </summary>
        /// <param name="previewImage">The WPF Image control to update with frames</param>
        public PreviewOutputTarget(WpfImage previewImage)
        {
            _previewImage = previewImage ?? throw new ArgumentNullException(nameof(previewImage));
        }

        /// <summary>
        /// Receives a frame and displays it in the preview image.
        /// Converts the Frame to BitmapSource immediately (does not store Frame reference).
        /// Thread-safe: marshals to UI thread if needed.
        /// </summary>
        public void Receive(Frame frame)
        {
            if (frame == null || !frame.IsValid)
                return;

            // Convert frame to BitmapSource immediately (while Mat is still valid)
            var bitmapSource = MatToBitmapSource.Convert(frame);

            if (bitmapSource != null)
            {
                // Update preview on UI thread
                if (_previewImage.Dispatcher.CheckAccess())
                {
                    _previewImage.Source = bitmapSource;
                }
                else
                {
                    _previewImage.Dispatcher.InvokeAsync(() =>
                    {
                        _previewImage.Source = bitmapSource;
                    });
                }
            }
        }
    }
}
