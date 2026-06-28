using OpenCvSharp;

namespace VirtualCamStudio.Media
{
    public class RenderEngine
    {
        private readonly SmartFitEngine _smartFit = new();

        public Mat Render(Mat source, CanvasSettings canvas, FitMode mode)
        {
            return mode switch
            {
                FitMode.Fill => _smartFit.Fit(source, canvas.Width, canvas.Height),

                FitMode.Fit => _smartFit.Fit(source, canvas.Width, canvas.Height),

                FitMode.SmartCrop => _smartFit.Fit(source, canvas.Width, canvas.Height),

                _ => _smartFit.Fit(source, canvas.Width, canvas.Height)
            };
        }
    }
}