namespace VirtualCamStudio.Models
{
    /// <summary>
    /// Represents the current status of OBS Studio.
    /// Contains version information, active scene, and streaming/recording states.
    /// </summary>
    public class OBSStatus
    {
        /// <summary>
        /// OBS Studio version.
        /// </summary>
        public string OBSVersion { get; set; } = string.Empty;

        /// <summary>
        /// OBS WebSocket plugin version.
        /// </summary>
        public string WebSocketVersion { get; set; } = string.Empty;

        /// <summary>
        /// Name of the currently active scene.
        /// </summary>
        public string CurrentScene { get; set; } = string.Empty;

        /// <summary>
        /// Whether the virtual camera is currently active.
        /// </summary>
        public bool VirtualCameraActive { get; set; }

        /// <summary>
        /// Whether recording is currently active.
        /// </summary>
        public bool RecordingActive { get; set; }

        /// <summary>
        /// Whether streaming is currently active.
        /// </summary>
        public bool StreamingActive { get; set; }

        /// <summary>
        /// Returns a formatted string representation of the OBS status.
        /// </summary>
        public override string ToString()
        {
            return $"OBS Status:\n" +
                   $"  Version: {OBSVersion}\n" +
                   $"  WebSocket: {WebSocketVersion}\n" +
                   $"  Current Scene: {CurrentScene}\n" +
                   $"  Virtual Camera: {(VirtualCameraActive ? "Active" : "Inactive")}\n" +
                   $"  Recording: {(RecordingActive ? "Active" : "Inactive")}\n" +
                   $"  Streaming: {(StreamingActive ? "Active" : "Inactive")}";
        }
    }
}
