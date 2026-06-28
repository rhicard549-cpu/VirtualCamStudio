using OpenCvSharp;

namespace VirtualCamStudio.Media
{
    public class ImageProcessor
    {
        public Mat Load(string path)
        {
            return Cv2.ImRead(path, ImreadModes.Color);
        }

        public bool IsPortrait(Mat image)
        {
            return image.Height >= image.Width;
        }
    }
}