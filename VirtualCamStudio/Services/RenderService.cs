using OpenCvSharp;
using VirtualCamStudio.Media;

namespace VirtualCamStudio.Services
{
    public class RenderService
    {
        private readonly ImageProcessor _imageProcessor = new();
        private readonly RenderEngine _renderEngine = new();

        private readonly CanvasSettings _canvas =
            CanvasSettings.Portrait1080;

        public Mat RenderImage(string filePath)
        {
            Mat image = _imageProcessor.Load(filePath);

            return _renderEngine.Render(
                image,
                _canvas,
                FitMode.Fill);
        }
    }
}