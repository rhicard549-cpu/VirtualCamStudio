using OpenCvSharp;

namespace VirtualCamStudio.Media
{
    /// <summary>
    /// DEPRECATED: SmartFitEngine is no longer used.
    /// 
    /// The rendering pipeline has been refactored. ViewportEngine now handles
    /// all canvas fitting and rendering in a single unified pass directly from
    /// the original source image.
    /// 
    /// This class is kept for reference and potential utility functions.
    /// Consider removing if no other code depends on it.
    /// </summary>
    [System.Obsolete("Use ViewportEngine instead. This class is no longer part of the active rendering pipeline.")]
    public class SmartFitEngine
    {
        public Mat Fit(Mat source, int targetWidth, int targetHeight)
        {
            if (source.Empty())
                return source;

            // Scale to completely fill the target canvas
            double scale = System.Math.Max(
                (double)targetWidth / source.Width,
                (double)targetHeight / source.Height);

            int resizedWidth = (int)(source.Width * scale);
            int resizedHeight = (int)(source.Height * scale);

            Mat resized = new();
            Cv2.Resize(source, resized, new Size(resizedWidth, resizedHeight));

            // Center crop
            int x = System.Math.Max((resizedWidth - targetWidth) / 2, 0);
            int y = System.Math.Max((resizedHeight - targetHeight) / 2, 0);

            Rect roi = new(x, y, targetWidth, targetHeight);

            return new Mat(resized, roi).Clone();
        }
    }
}