using OpenCvSharp;

namespace VirtualCamStudio.Media
{
    public class ImageProcessor
    {
        public Mat Load(string path)
        {
            return Cv2.ImRead(path, ImreadModes.Color);
        }

        public Mat ResizeToPortrait(Mat source, int width, int height)
        {
            if (source.Empty())
                return source;

            double scale = System.Math.Max(
                (double)width / source.Width,
                (double)height / source.Height);

            int newWidth = (int)(source.Width * scale);
            int newHeight = (int)(source.Height * scale);

            Mat resized = new();

            Cv2.Resize(source, resized, new Size(newWidth, newHeight));

            int x = (newWidth - width) / 2;
            int y = (newHeight - height) / 2;

            Rect roi = new(x, y, width, height);

            return new Mat(resized, roi).Clone();
        }
    }
}