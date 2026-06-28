using OpenCvSharp;

namespace VirtualCamStudio.Media
{
    /// <summary>
    /// DEPRECATED: TransformEngine is no longer used.
    /// 
    /// The rendering pipeline has been refactored. ViewportEngine now handles
    /// all viewport transformations (zoom, pan, rotation) in a single unified pass
    /// directly from the original source image.
    /// 
    /// This class is kept for reference and potential utility functions.
    /// Consider removing if no other code depends on it.
    /// </summary>
    [System.Obsolete("Use ViewportEngine instead. This class is no longer part of the active rendering pipeline.")]
    public class TransformEngine
    {
        public Mat Apply(
            Mat source,
            double zoom,
            double offsetX,
            double offsetY,
            double rotation)
        {
            if (source.Empty())
                return source;

            Mat result = source.Clone();

            // -----------------------------
            // Zoom
            // -----------------------------
            if (zoom != 1.0)
            {
                int newWidth = (int)(result.Width * zoom);
                int newHeight = (int)(result.Height * zoom);

                Mat resized = new();

                Cv2.Resize(
                    result,
                    resized,
                    new Size(newWidth, newHeight),
                    0,
                    0,
                    InterpolationFlags.Linear);

                result.Dispose();
                result = resized;
            }

            // -----------------------------
            // Pan
            // -----------------------------
            if (offsetX != 0 || offsetY != 0)
            {
                Mat translated = new();

                Mat matrix = new Mat(2, 3, MatType.CV_64FC1);

                matrix.Set(0, 0, 1.0);
                matrix.Set(0, 1, 0.0);
                matrix.Set(0, 2, offsetX);

                matrix.Set(1, 0, 0.0);
                matrix.Set(1, 1, 1.0);
                matrix.Set(1, 2, offsetY);

                Cv2.WarpAffine(
                    result,
                    translated,
                    matrix,
                    result.Size());

                result.Dispose();
                matrix.Dispose();

                result = translated;
            }

            // -----------------------------
            // Rotation
            // -----------------------------
            if (rotation != 0)
            {
                Point2f center = new(
                    result.Width / 2f,
                    result.Height / 2f);

                Mat rot =
                    Cv2.GetRotationMatrix2D(
                        center,
                        rotation,
                        1.0);

                Mat rotated = new();

                Cv2.WarpAffine(
                    result,
                    rotated,
                    rot,
                    result.Size());

                result.Dispose();
                rot.Dispose();

                result = rotated;
            }

            return result;
        }
    }
}