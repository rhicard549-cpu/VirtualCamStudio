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
            System.Diagnostics.Debug.WriteLine("╔══════════════════════════════════════════════════════════════════════════════");
            System.Diagnostics.Debug.WriteLine("║ ViewportEngine.Render() - START");
            System.Diagnostics.Debug.WriteLine("╚══════════════════════════════════════════════════════════════════════════════");

            if (source.Empty())
            {
                System.Diagnostics.Debug.WriteLine("[ViewportEngine] ❌ Source is EMPTY - returning empty frame");
                System.Diagnostics.Debug.WriteLine("[ViewportEngine] ❌ Fallback reason: Source Mat is empty");
                return new Frame(new Mat(), PixelFormat.Unknown);
            }

            System.Diagnostics.Debug.WriteLine($"[ViewportEngine] Source:");
            System.Diagnostics.Debug.WriteLine($"  - Width: {source.Width}");
            System.Diagnostics.Debug.WriteLine($"  - Height: {source.Height}");
            System.Diagnostics.Debug.WriteLine($"  - Channels: {source.Channels()}");
            System.Diagnostics.Debug.WriteLine($"  - Type: {source.Type()}");

            // Sample source pixel to verify input content
            unsafe
            {
                byte* data = (byte*)source.DataPointer;
                if (data != null)
                {
                    byte b = data[0];
                    byte g = data[1];
                    byte r = data[2];
                    System.Diagnostics.Debug.WriteLine($"  - Source Pixel[0,0] RGB: ({r}, {g}, {b})");
                }
            }

            System.Diagnostics.Debug.WriteLine($"[ViewportEngine] Canvas:");
            System.Diagnostics.Debug.WriteLine($"  - Width: {canvasWidth}");
            System.Diagnostics.Debug.WriteLine($"  - Height: {canvasHeight}");

            System.Diagnostics.Debug.WriteLine($"[ViewportEngine] Framing:");
            System.Diagnostics.Debug.WriteLine($"  - Zoom: {framing.Zoom}");
            System.Diagnostics.Debug.WriteLine($"  - OffsetX: {framing.OffsetX}");
            System.Diagnostics.Debug.WriteLine($"  - OffsetY: {framing.OffsetY}");
            System.Diagnostics.Debug.WriteLine($"  - Rotation: {framing.Rotation}");

            // Canvas with black background
            Mat canvas = new(
                new Size(canvasWidth, canvasHeight),
                MatType.CV_8UC3,
                Scalar.Black);

            System.Diagnostics.Debug.WriteLine($"[ViewportEngine] Canvas created:");
            System.Diagnostics.Debug.WriteLine($"  - Width: {canvas.Width}");
            System.Diagnostics.Debug.WriteLine($"  - Height: {canvas.Height}");
            System.Diagnostics.Debug.WriteLine($"  - Empty: {canvas.Empty()}");

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

            System.Diagnostics.Debug.WriteLine($"[ViewportEngine] Calculated:");
            System.Diagnostics.Debug.WriteLine($"  - BaseScale: {baseScale:F4}");
            System.Diagnostics.Debug.WriteLine($"  - TotalScale: {totalScale:F4}");
            System.Diagnostics.Debug.WriteLine($"  - ScaledWidth: {scaledWidth}");
            System.Diagnostics.Debug.WriteLine($"  - ScaledHeight: {scaledHeight}");
            System.Diagnostics.Debug.WriteLine($"  - CenterX: {centerX:F2}");
            System.Diagnostics.Debug.WriteLine($"  - CenterY: {centerY:F2}");
            System.Diagnostics.Debug.WriteLine($"  - ScaledCenterX: {scaledCenterX:F2}");
            System.Diagnostics.Debug.WriteLine($"  - ScaledCenterY: {scaledCenterY:F2}");

            // For rotation, we need the center in the transformed space
            double rotationAngleRadians = framing.Rotation * System.Math.PI / 180.0;

            try
            {
                // Use warp affine for efficient rendering with rotation
                if (framing.Rotation != 0)
                {
                    System.Diagnostics.Debug.WriteLine($"[ViewportEngine] Using ROTATION path (rotation != 0)");

                    // Create intermediate scaled image for rotation
                    Mat scaled = new();
                    Cv2.Resize(source, scaled, new Size(scaledWidth, scaledHeight));

                    System.Diagnostics.Debug.WriteLine($"[ViewportEngine] After Resize:");
                    System.Diagnostics.Debug.WriteLine($"  - scaled.Width: {scaled.Width}");
                    System.Diagnostics.Debug.WriteLine($"  - scaled.Height: {scaled.Height}");
                    System.Diagnostics.Debug.WriteLine($"  - scaled.Empty(): {scaled.Empty()}");

                    // Rotation matrix centered on scaled image center
                    Mat rotMatrix = Cv2.GetRotationMatrix2D(
                        new Point2f((float)scaledCenterX, (float)scaledCenterY),
                        framing.Rotation,
                        1.0);

                    Mat rotated = new();
                    Cv2.WarpAffine(scaled, rotated, rotMatrix, new Size(scaledWidth, scaledHeight));

                    System.Diagnostics.Debug.WriteLine($"[ViewportEngine] After WarpAffine:");
                    System.Diagnostics.Debug.WriteLine($"  - rotated.Width: {rotated.Width}");
                    System.Diagnostics.Debug.WriteLine($"  - rotated.Height: {rotated.Height}");
                    System.Diagnostics.Debug.WriteLine($"  - rotated.Empty(): {rotated.Empty()}");

                    // Now place rotated image on canvas with pan and centering
                    int canvasX = (int)(centerX - scaledCenterX + framing.OffsetX);
                    int canvasY = (int)(centerY - scaledCenterY + framing.OffsetY);

                    System.Diagnostics.Debug.WriteLine($"[ViewportEngine] Placement position:");
                    System.Diagnostics.Debug.WriteLine($"  - canvasX (TranslateX): {canvasX}");
                    System.Diagnostics.Debug.WriteLine($"  - canvasY (TranslateY): {canvasY}");

                    PlaceImageOnCanvas(canvas, rotated, canvasX, canvasY);

                    rotMatrix.Dispose();
                    rotated.Dispose();
                    scaled.Dispose();
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"[ViewportEngine] Using NO ROTATION path (rotation == 0)");

                    // No rotation: simpler path for performance
                    Mat scaled = new();
                    Cv2.Resize(source, scaled, new Size(scaledWidth, scaledHeight));

                    System.Diagnostics.Debug.WriteLine($"[ViewportEngine] After Resize:");
                    System.Diagnostics.Debug.WriteLine($"  - scaled.Width: {scaled.Width}");
                    System.Diagnostics.Debug.WriteLine($"  - scaled.Height: {scaled.Height}");
                    System.Diagnostics.Debug.WriteLine($"  - scaled.Empty(): {scaled.Empty()}");

                    int canvasX = (int)(centerX - scaledCenterX + framing.OffsetX);
                    int canvasY = (int)(centerY - scaledCenterY + framing.OffsetY);

                    System.Diagnostics.Debug.WriteLine($"[ViewportEngine] Placement position:");
                    System.Diagnostics.Debug.WriteLine($"  - canvasX (TranslateX): {canvasX}");
                    System.Diagnostics.Debug.WriteLine($"  - canvasY (TranslateY): {canvasY}");

                    System.Diagnostics.Debug.WriteLine($"[ViewportEngine] Calling PlaceImageOnCanvas...");
                    PlaceImageOnCanvas(canvas, scaled, canvasX, canvasY);
                    System.Diagnostics.Debug.WriteLine($"[ViewportEngine] PlaceImageOnCanvas completed");

                    scaled.Dispose();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ViewportEngine] ❌ EXCEPTION during rendering: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"[ViewportEngine] Stack trace: {ex.StackTrace}");
                // If anything fails, return the black canvas
            }

            System.Diagnostics.Debug.WriteLine($"[ViewportEngine] Final canvas state:");
            System.Diagnostics.Debug.WriteLine($"  - Width: {canvas.Width}");
            System.Diagnostics.Debug.WriteLine($"  - Height: {canvas.Height}");
            System.Diagnostics.Debug.WriteLine($"  - Empty: {canvas.Empty()}");
            System.Diagnostics.Debug.WriteLine($"  - Channels: {canvas.Channels()}");

            // Sample final canvas pixels to verify output content
            unsafe
            {
                byte* data = (byte*)canvas.DataPointer;
                if (data != null)
                {
                    byte b = data[0];
                    byte g = data[1];
                    byte r = data[2];
                    System.Diagnostics.Debug.WriteLine($"  - Output Pixel[0,0] RGB: ({r}, {g}, {b})");

                    // Check if output is solid blue (diagnostic indicator)
                    bool isSolidBlue = (r == 0 && g == 0 && b == 255);
                    if (isSolidBlue)
                    {
                        System.Diagnostics.Debug.WriteLine($"  - ⚠️ WARNING: ViewportEngine output is SOLID BLUE");
                    }
                }
            }

            // Determine pixel format based on number of channels
            PixelFormat pixelFormat = canvas.Channels() switch
            {
                1 => PixelFormat.Grayscale,
                3 => PixelFormat.BGR,
                4 => PixelFormat.BGRA,
                _ => PixelFormat.Unknown
            };

            System.Diagnostics.Debug.WriteLine($"[ViewportEngine] Returning Frame with PixelFormat: {pixelFormat}");
            System.Diagnostics.Debug.WriteLine("╔══════════════════════════════════════════════════════════════════════════════");
            System.Diagnostics.Debug.WriteLine("║ ViewportEngine.Render() - END");
            System.Diagnostics.Debug.WriteLine("╚══════════════════════════════════════════════════════════════════════════════");

            return new Frame(canvas, pixelFormat, frameNumber: 0);
        }

        /// <summary>
        /// Place an image on the canvas at the specified position, clipping to canvas bounds.
        /// </summary>
        private static void PlaceImageOnCanvas(Mat canvas, Mat image, int x, int y)
        {
            System.Diagnostics.Debug.WriteLine($"[PlaceImageOnCanvas] Called with:");
            System.Diagnostics.Debug.WriteLine($"  - canvas: {canvas.Width}x{canvas.Height}, empty: {canvas.Empty()}");
            System.Diagnostics.Debug.WriteLine($"  - image: {image.Width}x{image.Height}, empty: {image.Empty()}");
            System.Diagnostics.Debug.WriteLine($"  - position: x={x}, y={y}");

            int srcX = 0;
            int srcY = 0;

            // Adjust for negative positions
            if (x < 0)
            {
                srcX = -x;
                x = 0;
                System.Diagnostics.Debug.WriteLine($"  - Adjusted for negative x: srcX={srcX}, x={x}");
            }

            if (y < 0)
            {
                srcY = -y;
                y = 0;
                System.Diagnostics.Debug.WriteLine($"  - Adjusted for negative y: srcY={srcY}, y={y}");
            }

            // Calculate copy dimensions (clipped to canvas)
            int copyWidth = System.Math.Min(
                image.Width - srcX,
                canvas.Width - x);

            int copyHeight = System.Math.Min(
                image.Height - srcY,
                canvas.Height - y);

            System.Diagnostics.Debug.WriteLine($"[PlaceImageOnCanvas] Computed copy region:");
            System.Diagnostics.Debug.WriteLine($"  - srcX: {srcX}, srcY: {srcY}");
            System.Diagnostics.Debug.WriteLine($"  - dstX: {x}, dstY: {y}");
            System.Diagnostics.Debug.WriteLine($"  - copyWidth: {copyWidth}");
            System.Diagnostics.Debug.WriteLine($"  - copyHeight: {copyHeight}");

            if (copyWidth > 0 && copyHeight > 0)
            {
                System.Diagnostics.Debug.WriteLine($"[PlaceImageOnCanvas] ✓ Copy dimensions valid, performing CopyTo...");
                using Mat src = new(image, new Rect(srcX, srcY, copyWidth, copyHeight));
                using Mat dst = new(canvas, new Rect(x, y, copyWidth, copyHeight));
                src.CopyTo(dst);
                System.Diagnostics.Debug.WriteLine($"[PlaceImageOnCanvas] ✓ CopyTo completed successfully");
            }
            else
            {
                System.Diagnostics.Debug.WriteLine($"[PlaceImageOnCanvas] ❌ Copy dimensions invalid (width={copyWidth}, height={copyHeight}) - NO COPY PERFORMED!");
                System.Diagnostics.Debug.WriteLine($"[PlaceImageOnCanvas] This means the image is completely outside the canvas bounds!");
            }
        }
    }
}