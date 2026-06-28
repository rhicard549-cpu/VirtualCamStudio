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
        /// </summary>
        public static BitmapSource Convert(Mat mat)
        {
            return BitmapSourceConverter.ToBitmapSource(mat);
        }

        /// <summary>
        /// Converts a Frame to a WPF BitmapSource.
        /// Extracts the Mat image from the Frame and converts it.
        /// </summary>
        public static BitmapSource Convert(Frame frame)
        {
            if (frame == null || !frame.IsValid)
                return null!;

            return BitmapSourceConverter.ToBitmapSource(frame.Image);
        }
    }
}
