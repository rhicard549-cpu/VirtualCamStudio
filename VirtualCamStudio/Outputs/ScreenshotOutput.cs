using OpenCvSharp;
using System.IO;
using VirtualCamStudio.Core;

namespace VirtualCamStudio.Outputs
{
    /// <summary>
    /// Output plugin that saves rendered frames as image files (screenshots).
    /// Future implementation: Commit 45+
    /// </summary>
    [OutputPlugin("Screenshot", "Saves rendered frames as image files (PNG, JPG)", "Recording", "1.0.0")]
    public class ScreenshotOutput : IOutputTarget
    {
        private readonly string _outputDirectory;
        private readonly object _lock = new();
        private bool _captureNextFrame;

        /// <summary>
        /// Creates a new screenshot output plugin.
        /// </summary>
        /// <param name="outputDirectory">Directory where screenshots will be saved.</param>
        public ScreenshotOutput(string outputDirectory)
        {
            _outputDirectory = outputDirectory ?? throw new ArgumentNullException(nameof(outputDirectory));

            // Ensure output directory exists
            if (!Directory.Exists(_outputDirectory))
            {
                Directory.CreateDirectory(_outputDirectory);
            }
        }

        /// <summary>
        /// Requests that the next frame be saved as a screenshot.
        /// </summary>
        public void CaptureNextFrame()
        {
            lock (_lock)
            {
                _captureNextFrame = true;
            }
        }

        /// <summary>
        /// Saves a rendered frame as an image file (if capture was requested).
        /// </summary>
        /// <param name="frame">The rendered frame to save.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        public Task SendFrameAsync(Frame frame)
        {
            bool shouldCapture;
            lock (_lock)
            {
                shouldCapture = _captureNextFrame;
                _captureNextFrame = false;
            }

            if (!shouldCapture)
                return Task.CompletedTask;

            if (frame == null || !frame.IsValid)
                return Task.CompletedTask;

            // Generate filename with timestamp
            string timestamp = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss-fff");
            string fileName = $"Screenshot_{timestamp}.png";
            string fullPath = Path.Combine(_outputDirectory, fileName);

            // Save frame to disk
            try
            {
                Cv2.ImWrite(fullPath, frame.Image);
                System.Diagnostics.Debug.WriteLine($"[ScreenshotOutput] ✓ Saved screenshot: {fullPath}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ScreenshotOutput] ❌ Failed to save screenshot: {ex.Message}");
            }

            return Task.CompletedTask;
        }
    }
}
