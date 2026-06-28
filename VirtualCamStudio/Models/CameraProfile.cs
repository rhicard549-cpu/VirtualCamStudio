namespace VirtualCamStudio.Models
{
    /// <summary>
    /// Represents a camera configuration profile.
    /// Includes sensor dimensions, display dimensions, framing defaults, and metadata.
    /// </summary>
    public class CameraProfile
    {
        /// <summary>
        /// Profile name (e.g., "iPhone 15 Pro")
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Camera manufacturer (e.g., "Apple")
        /// </summary>
        public string Manufacturer { get; set; } = string.Empty;

        /// <summary>
        /// Camera model (e.g., "iPhone 15 Pro")
        /// </summary>
        public string Model { get; set; } = string.Empty;

        /// <summary>
        /// Sensor width in mm
        /// </summary>
        public double SensorWidth { get; set; }

        /// <summary>
        /// Sensor height in mm
        /// </summary>
        public double SensorHeight { get; set; }

        /// <summary>
        /// Display/viewport width in pixels
        /// </summary>
        public int DisplayWidth { get; set; }

        /// <summary>
        /// Display/viewport height in pixels
        /// </summary>
        public int DisplayHeight { get; set; }

        /// <summary>
        /// Default rotation in degrees (0, 90, 180, 270)
        /// </summary>
        public double Rotation { get; set; }

        /// <summary>
        /// Frames per second
        /// </summary>
        public int FPS { get; set; } = 30;

        /// <summary>
        /// Default zoom level (1.0 to 5.0)
        /// </summary>
        public double DefaultZoom { get; set; } = 1.0;

        /// <summary>
        /// Default horizontal offset
        /// </summary>
        public double DefaultOffsetX { get; set; }

        /// <summary>
        /// Default vertical offset
        /// </summary>
        public double DefaultOffsetY { get; set; }

        /// <summary>
        /// Additional notes about the profile
        /// </summary>
        public string Notes { get; set; } = string.Empty;
    }
}
