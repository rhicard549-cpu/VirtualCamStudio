using System.Diagnostics;
using System.Windows;
using System.Windows.Media.Imaging;
using VirtualCamStudio.Core;
using VirtualCamStudio.Helpers;
using WpfImage = System.Windows.Controls.Image;

namespace VirtualCamStudio.Outputs
{
    /// <summary>
    /// Output target that displays rendered frames in a WPF Image control (preview).
    /// Receives Frame objects and updates the preview on the UI thread.
    /// </summary>
    [OutputPlugin("Preview", "Displays rendered frames in the application preview window", "Display", "1.0.0")]
    public class PreviewOutput : IOutputTarget
    {
        private readonly WpfImage _previewImage;

        /// <summary>
        /// Creates a new preview output.
        /// </summary>
        /// <param name="previewImage">The WPF Image control to display frames in.</param>
        public PreviewOutput(WpfImage previewImage)
        {
            _previewImage = previewImage ?? throw new ArgumentNullException(nameof(previewImage));
        }

        /// <summary>
        /// Receives a rendered frame and updates the WPF preview.
        /// Converts the Frame's Mat to BitmapSource and updates on the UI dispatcher thread.
        /// Does not dispose the frame (caller owns the frame lifecycle).
        /// </summary>
        /// <param name="frame">The rendered frame to display. Must not be null or invalid.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        public Task SendFrameAsync(Frame frame)
        {
            Debug.WriteLine("[8] PreviewOutput received frame");
            System.Diagnostics.Debug.WriteLine($"[PreviewOutput.SendFrameAsync] Received frame");

            if (frame == null || !frame.IsValid)
            {
                System.Diagnostics.Debug.WriteLine($"[PreviewOutput.SendFrameAsync] ⚠️ Invalid frame");
                return Task.CompletedTask;
            }

            System.Diagnostics.Debug.WriteLine($"[PreviewOutput.SendFrameAsync] Frame is valid, converting to BitmapSource...");

            // Convert Mat to BitmapSource immediately (while Mat is still valid)
            BitmapSource? bitmapSource = null;
            try
            {
                bitmapSource = MatToBitmapSource.Convert(frame.Image);
                Debug.WriteLine("[9] Bitmap conversion - SUCCESS");
                System.Diagnostics.Debug.WriteLine($"[PreviewOutput.SendFrameAsync] ✓ Conversion successful");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[9] Bitmap conversion - FAILED: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"[PreviewOutput.SendFrameAsync] ❌ Conversion failed: {ex.Message}");
                return Task.CompletedTask;
            }

            if (bitmapSource == null)
            {
                Debug.WriteLine("[9] Bitmap conversion - FAILED: result is null");
                System.Diagnostics.Debug.WriteLine($"[PreviewOutput.SendFrameAsync] ⚠️ BitmapSource is null");
                return Task.CompletedTask;
            }

            // Update preview on UI thread
            if (_previewImage.Dispatcher.CheckAccess())
            {
                System.Diagnostics.Debug.WriteLine($"[PreviewOutput.SendFrameAsync] Already on UI thread, updating preview directly");
                _previewImage.Source = bitmapSource;
                Debug.WriteLine("[10] UI update - Preview updated successfully (direct)");
                System.Diagnostics.Debug.WriteLine($"[PreviewOutput.SendFrameAsync] ✓ Preview updated (direct)");
                return Task.CompletedTask;
            }
            else
            {
                System.Diagnostics.Debug.WriteLine($"[PreviewOutput.SendFrameAsync] Invoking on UI thread...");
                return _previewImage.Dispatcher.InvokeAsync(() =>
                {
                    _previewImage.Source = bitmapSource;
                    Debug.WriteLine("[10] UI update - Preview updated successfully (dispatcher)");
                    System.Diagnostics.Debug.WriteLine($"[PreviewOutput.SendFrameAsync] ✓ Preview updated (dispatcher)");
                }).Task;
            }
        }
    }
}
