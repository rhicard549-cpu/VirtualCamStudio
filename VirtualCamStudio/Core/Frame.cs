using OpenCvSharp;
using System;

namespace VirtualCamStudio.Core
{
    /// <summary>
    /// Represents a single frame of video or image data.
    /// Wraps OpenCvSharp.Mat with metadata for timestamp, dimensions, and format.
    /// Prepares architecture for video frame sequences while supporting static images.
    /// </summary>
    public class Frame : IDisposable
    {
        /// <summary>
        /// The image data as an OpenCV Mat.
        /// This Frame owns the Mat and will dispose it.
        /// </summary>
        public Mat Image { get; private set; }

        /// <summary>
        /// Timestamp when this frame was created or captured.
        /// For static images, this is the time of loading/rendering.
        /// For video, this will be the presentation timestamp.
        /// </summary>
        public DateTime Timestamp { get; set; }

        /// <summary>
        /// Width of the frame in pixels.
        /// </summary>
        public int Width => Image?.Width ?? 0;

        /// <summary>
        /// Height of the frame in pixels.
        /// </summary>
        public int Height => Image?.Height ?? 0;

        /// <summary>
        /// Pixel format of the frame (BGR, BGRA, RGB, etc.)
        /// </summary>
        public PixelFormat PixelFormat { get; set; }

        /// <summary>
        /// Frame number in a sequence.
        /// For static images, this is 0.
        /// For video, this increments with each frame.
        /// </summary>
        public long FrameNumber { get; set; }

        /// <summary>
        /// Creates a new Frame wrapping an OpenCV Mat.
        /// </summary>
        /// <param name="image">The image data (Frame takes ownership)</param>
        /// <param name="pixelFormat">The pixel format of the image</param>
        /// <param name="frameNumber">Frame number (default 0 for static images)</param>
        public Frame(Mat image, PixelFormat pixelFormat = PixelFormat.BGR, long frameNumber = 0)
        {
            Image = image ?? throw new ArgumentNullException(nameof(image));
            Timestamp = DateTime.UtcNow;
            PixelFormat = pixelFormat;
            FrameNumber = frameNumber;
        }

        /// <summary>
        /// Checks if the frame contains valid image data.
        /// </summary>
        public bool IsValid => Image != null && !Image.Empty();

        private bool _disposed = false;

        /// <summary>
        /// Disposes the underlying Mat image data.
        /// </summary>
        public void Dispose()
        {
            if (!_disposed)
            {
                Image?.Dispose();
                Image = null!;
                _disposed = true;
            }

            GC.SuppressFinalize(this);
        }

        ~Frame()
        {
            Dispose();
        }
    }
}
