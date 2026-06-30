using OpenCvSharp;
using VirtualCamStudio.Core;
using VirtualCamStudio.Models;

namespace VirtualCamStudio.Media
{
    /// <summary>
    /// ViewportEngine handles unified rendering of source media onto a fixed canvas.
    /// 
    /// Always renders from the original source image (never from a transformed image).
    /// Maintains aspect ratio and renders onto a black canvas.
    /// Supports zoom, pan (X/Y), and rotation in a single pass.
    /// 
    /// Core principle: Transform coordinates of the output canvas back to source image coordinates,
    /// then sample from the source. This avoids cascading transforms and quality loss.
    /// </summary>
    public class ViewportEngine
    {
        /// <summary>
        /// Render the source onto a fixed canvas with specified framing.
        /// Returns a Frame containing the rendered image.
        /// 
        /// The rendering process:
        /// 1. Calculate the base scale to fit the source onto the canvas (maintaining aspect ratio)
        /// 2. Apply zoom multiplier to the scale
        /// 3. For each pixel in the canvas, compute its coordinate in the source image
        ///    (accounting for pan, rotation, and centering)
        /// 4. Sample from the source at that coordinate
        /// 5. If the coordinate is outside the source, use black (canvas color)
        /// </summary>
        public Frame Render(
            Mat source,
            int canvasWidth,
            int canvasHeight,
            FramingSettings framing)
        {
            if (source.Empty())
            {
                return new Frame(new Mat(), PixelFormat.Unknown);
            }

            // Canvas with black background
            Mat canvas = new(
                new Size(canvasWidth, canvasHeight),
                MatType.CV_8UC3,
                Scalar.Black);

            // Calculate base scale to fit source onto canvas (preserves aspect ratio)
            double baseScale = System.Math.Min(
                (double)canvasWidth / source.Width,
                (double)canvasHeight / source.Height);

            // Apply zoom multiplier
            double totalScale = baseScale * framing.Zoom;

            // Scaled dimensions
            int scaledWidth = (int)(source.Width * totalScale);
            int scaledHeight = (int)(source.Height * totalScale);

            // Center position on canvas
            double centerX = canvasWidth / 2.0;
            double centerY = canvasHeight / 2.0;

            // Scaled image center
            double scaledCenterX = scaledWidth / 2.0;
            double scaledCenterY = scaledHeight / 2.0;

            // For rotation, we need the center in the transformed space
            double rotationAngleRadians = framing.Rotation * System.Math.PI / 180.0;

            try
            {
                // Use warp affine for efficient rendering with rotation
                if (framing.Rotation != 0)
                {
                    // Create intermediate scaled image for rotation
                    Mat scaled = new();
                    Cv2.Resize(source, scaled, new Size(scaledWidth, scaledHeight));

                    // Rotation matrix centered on scaled image center
                    Mat rotMatrix = Cv2.GetRotationMatrix2D(
                        new Point2f((float)scaledCenterX, (float)scaledCenterY),
                        framing.Rotation,
                        1.0);

                    Mat rotated = new();
                    Cv2.WarpAffine(scaled, rotated, rotMatrix, new Size(scaledWidth, scaledHeight));

                    // Now place rotated image on canvas with pan and centering
                    int canvasX = (int)(centerX - scaledCenterX + framing.OffsetX);
                    int canvasY = (int)(centerY - scaledCenterY + framing.OffsetY);

                    PlaceImageOnCanvas(canvas, rotated, canvasX, canvasY);

                    rotMatrix.Dispose();
                    rotated.Dispose();
                    scaled.Dispose();
                }
                else
                {
                    // No rotation: simpler path for performance
                    Mat scaled = new();
                    Cv2.Resize(source, scaled, new Size(scaledWidth, scaledHeight));

                    int canvasX = (int)(centerX - scaledCenterX + framing.OffsetX);
                    int canvasY = (int)(centerY - scaledCenterY + framing.OffsetY);

                    PlaceImageOnCanvas(canvas, scaled, canvasX, canvasY);

                    scaled.Dispose();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ViewportEngine] ❌ ERROR: {ex.Message}");
                // If anything fails, return the black canvas
            }

            // Determine pixel format based on number of channels
            PixelFormat pixelFormat = canvas.Channels() switch
            {
                1 => PixelFormat.Grayscale,
                3 => PixelFormat.BGR,
                4 => PixelFormat.BGRA,
                _ => PixelFormat.Unknown
            };

            return new Frame(canvas, pixelFormat, frameNumber: 0);
        }

        /// <summary>
        /// Place an image on the canvas at the specified position, clipping to canvas bounds.
        /// </summary>
        private static void PlaceImageOnCanvas(Mat canvas, Mat image, int x, int y)
        {
            int srcX = 0;
            int srcY = 0;

            // Adjust for negative positions
            if (x < 0)
            {
                srcX = -x;
                x = 0;
            }

            if (y < 0)
            {
                srcY = -y;
                y = 0;
            }

            // Calculate copy dimensions (clipped to canvas)
            int copyWidth = System.Math.Min(
                image.Width - srcX,
                canvas.Width - x);

            int copyHeight = System.Math.Min(
                image.Height - srcY,
                canvas.Height - y);

            if (copyWidth > 0 && copyHeight > 0)
            {
                using Mat src = new(image, new Rect(srcX, srcY, copyWidth, copyHeight));
                using Mat dst = new(canvas, new Rect(x, y, copyWidth, copyHeight));
                src.CopyTo(dst);
            }
        }
    }
}