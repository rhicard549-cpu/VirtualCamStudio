namespace VirtualCamStudio.Core
{
    /// <summary>
    /// Represents the pixel format of a frame.
    /// Used to describe the color space and channel layout.
    /// </summary>
    public enum PixelFormat
    {
        /// <summary>
        /// Unknown or unspecified pixel format
        /// </summary>
        Unknown = 0,

        /// <summary>
        /// 8-bit grayscale (1 channel)
        /// </summary>
        Grayscale = 1,

        /// <summary>
        /// BGR color (3 channels: Blue, Green, Red)
        /// OpenCV default format
        /// </summary>
        BGR = 2,

        /// <summary>
        /// BGRA color (4 channels: Blue, Green, Red, Alpha)
        /// Includes transparency channel
        /// </summary>
        BGRA = 3,

        /// <summary>
        /// RGB color (3 channels: Red, Green, Blue)
        /// </summary>
        RGB = 4,

        /// <summary>
        /// RGBA color (4 channels: Red, Green, Blue, Alpha)
        /// Includes transparency channel
        /// </summary>
        RGBA = 5
    }
}
