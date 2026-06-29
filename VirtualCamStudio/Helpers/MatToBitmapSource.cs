using OpenCvSharp;
using OpenCvSharp.WpfExtensions;
using System.Windows.Media.Imaging;
using VirtualCamStudio.Core;

namespace VirtualCamStudio.Helpers
{
    public static class MatToBitmapSource
    {
        /// <summary>
        /// Converts an OpenCV Mat to a WPF BitmapSource.
        /// The returned BitmapSource is frozen and can be safely passed across threads.
        /// </summary>
        public static BitmapSource Convert(Mat mat)
        {
            var bitmap = BitmapSourceConverter.ToBitmapSource(mat);

            // Freeze the bitmap so it can be safely passed to the UI thread
            if (bitmap != null && !bitmap.IsFrozen)
            {
                bitmap.Freeze();
            }

            return bitmap;
        }

        /// <summary>
        /// Converts a Frame to a WPF BitmapSource.
        /// Extracts the Mat image from the Frame and converts it.
        /// The returned BitmapSource is frozen and can be safely passed across threads.
        /// </summary>
        public static BitmapSource Convert(Frame frame)
        {
            if (frame == null || !frame.IsValid)
                return null!;

            var bitmap = BitmapSourceConverter.ToBitmapSource(frame.Image);

            // Freeze the bitmap so it can be safely passed to the UI thread
            if (bitmap != null && !bitmap.IsFrozen)
            {
                bitmap.Freeze();
            }

            return bitmap;
        }
    }
}
