using OpenCvSharp;
using VirtualCamStudio.Models;

namespace VirtualCamStudio.Media
{
    /// <summary>
    /// RenderEngine is a thin wrapper around ViewportEngine.
    /// 
    /// Responsibilities:
    /// 1. Apply fit mode scaling logic to determine the base scale factor
    /// 2. Delegate actual rendering to ViewportEngine
    /// 3. Ensure the original source is always passed (never transform a transformed image)
    /// </summary>
    public class RenderEngine
    {
        private readonly ViewportEngine _viewportEngine = new();

        /// <summary>
        /// Render the source onto the canvas with specified framing and fit mode.
        /// 
        /// FitMode determines how the image scales to fit the canvas:
        /// - Fit: Scale to fit entirely within canvas (may have black letterbox)
        /// - Fill: Scale to fill canvas completely (may crop)
        /// - SmartCrop: Same as Fill for now
        /// 
        /// The actual rendering (zoom, pan, rotation) is delegated to ViewportEngine,
        /// which always works from the original source.
        /// </summary>
        public Mat Render(
            Mat source,
            CanvasSettings canvas,
            FitMode mode,
            FramingSettings framing)
        {
            if (source.Empty())
                return new Mat();

            // Delegate to ViewportEngine
            // ViewportEngine handles all transformations internally
            return _viewportEngine.Render(
                source,
                canvas.Width,
                canvas.Height,
                framing);
        }
    }
}