using OpenCvSharp;
using VirtualCamStudio.Media;
using VirtualCamStudio.Models;

namespace VirtualCamStudio.Services
{
    /// <summary>
    /// RenderService orchestrates the complete rendering pipeline.
    /// 
    /// Responsibilities:
    /// 1. Load and cache the original image (never reloaded during viewport adjustments)
    /// 2. Own the framing settings (zoom, pan, rotation)
    /// 3. Accumulate viewport state as sliders are moved
    /// 4. Pass the original image + framing settings to RenderEngine for each frame
    /// 
    /// Key Principle: The original image is loaded once and cached. Only the framing
    /// settings change as the user interacts with sliders. This eliminates redundant
    /// image reloading and ensures consistent rendering from the source.
    /// </summary>
    public class RenderService
    {
        private readonly ImageProcessor _imageProcessor = new();
        private readonly RenderEngine _renderEngine = new();

        private readonly CanvasSettings _canvas =
            CanvasSettings.Portrait1080;

        /// <summary>
        /// Framing settings owned by RenderService.
        /// Accumulates zoom, pan, and rotation as the user interacts.
        /// </summary>
        private readonly FramingSettings _framing = new();

        /// <summary>
        /// Cached original image. Loaded once, never transformed.
        /// Always passed to RenderEngine for rendering.
        /// </summary>
        private Mat? _currentImage;

        public bool HasImage =>
            _currentImage != null && !_currentImage.Empty();

        public void LoadImage(string filePath)
        {
            _currentImage = _imageProcessor.Load(filePath);
        }

        public void SetZoom(double zoom)
        {
            _framing.Zoom = zoom;
        }

        public void SetOffset(double x, double y)
        {
            _framing.OffsetX = x;
            _framing.OffsetY = y;
        }

        public void SetRotation(double rotation)
        {
            _framing.Rotation = rotation;
        }

        public void Reset()
        {
            _framing.Zoom = 1.0;
            _framing.OffsetX = 0;
            _framing.OffsetY = 0;
            _framing.Rotation = 0;
        }

        /// <summary>
        /// Render the current image with accumulated framing settings.
        /// 
        /// The original cached image is passed to RenderEngine along with
        /// the current framing settings. No image reloading occurs; only
        /// the viewport state has changed since the last render call.
        /// </summary>
        public Mat Render()
        {
            if (!HasImage)
                return new Mat();

            return _renderEngine.Render(
                _currentImage!,
                _canvas,
                FitMode.Fill,
                _framing);
        }
    }
}
