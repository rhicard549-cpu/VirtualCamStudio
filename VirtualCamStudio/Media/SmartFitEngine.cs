using OpenCvSharp;

namespace VirtualCamStudio.Media
{
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