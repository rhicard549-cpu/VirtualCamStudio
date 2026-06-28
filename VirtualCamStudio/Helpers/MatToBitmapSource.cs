using OpenCvSharp;
using OpenCvSharp.WpfExtensions;
using System.Windows.Media.Imaging;

namespace VirtualCamStudio.Helpers
{
    public static class MatToBitmapSource
    {
        public static BitmapSource Convert(Mat mat)
        {
            return BitmapSourceConverter.ToBitmapSource(mat);
        }
    }
}