namespace VirtualCamStudio.Models
{
    /// <summary>
    /// Represents a cloud phone device from MoreLogin.
    /// Contains device information including hardware specs and connection status.
    /// </summary>
    public class CloudPhone
    {
        /// <summary>
        /// Unique identifier for the cloud phone.
        /// </summary>
        public string Id { get; set; } = string.Empty;

        /// <summary>
        /// Display name of the cloud phone.
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Current status of the cloud phone (e.g., "Online", "Offline", "Running", "Stopped").
        /// </summary>
        public string Status { get; set; } = string.Empty;

        /// <summary>
        /// Manufacturer of the device (e.g., "Samsung", "Google", "Xiaomi").
        /// </summary>
        public string Manufacturer { get; set; } = string.Empty;

        /// <summary>
        /// Device model name (e.g., "Galaxy S21", "Pixel 6").
        /// </summary>
        public string Model { get; set; } = string.Empty;

        /// <summary>
        /// Android version running on the device (e.g., "11", "12", "13").
        /// </summary>
        public string AndroidVersion { get; set; } = string.Empty;

        /// <summary>
        /// Screen resolution of the device (e.g., "1080x1920", "1440x3200").
        /// </summary>
        public string Resolution { get; set; } = string.Empty;

        /// <summary>
        /// Returns a string representation of the cloud phone.
        /// </summary>
        public override string ToString()
        {
            return $"{Name} ({Model}) - {Status}";
        }
    }
}
