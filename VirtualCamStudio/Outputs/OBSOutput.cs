using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using OpenCvSharp;
using VirtualCamStudio.Core;

namespace VirtualCamStudio.Outputs
{
    /// <summary>
    /// Output plugin that sends rendered frames to OBS Studio.
    /// Writes frames to a PNG file that OBS Image Source reads.
    /// VirtualCamStudio does ALL editing - OBS only displays the finished frame.
    /// </summary>
    [OutputPlugin("OBS Studio", "Sends rendered frames to OBS Virtual Camera", "Streaming", "2.0.0")]
    public class OBSOutput : IOutputTarget
    {
        private readonly string _outputPath;
        private readonly object _writeLock = new();
        private int _frameCount = 0;
        private bool _disposed = false;

        /// <summary>
        /// Gets the total number of frames sent to OBS
        /// </summary>
        public int FrameCount => _frameCount;

        /// <summary>
        /// Gets the path where frames are written for OBS consumption
        /// </summary>
        public string OutputPath => _outputPath;

        /// <summary>
        /// Initializes a new instance of OBSOutput
        /// </summary>
        public OBSOutput()
        {
            // Use a known location that OBS Image Source will monitor
            _outputPath = GetDefaultOutputPath();

            // Ensure directory exists
            string? directory = Path.GetDirectoryName(_outputPath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }
        }

        /// <summary>
        /// Sends a rendered frame to OBS by writing to a PNG file
        /// </summary>
        /// <param name="frame">The finished rendered frame</param>
        public Task SendFrameAsync(Frame frame)
        {
            if (_disposed)
                return Task.CompletedTask;

            if (frame == null || !frame.IsValid)
                return Task.CompletedTask;

            try
            {
                lock (_writeLock)
                {
                    // Write frame to PNG file
                    // OBS Image Source monitors this file and updates automatically
                    Cv2.ImWrite(_outputPath, frame.Image);
                    _frameCount++;
                }
            }
            catch (System.Exception ex)
            {
            }

            return Task.CompletedTask;
        }

        /// <summary>
        /// Gets the default output path in %LocalAppData%\VirtualCamStudio\Output
        /// </summary>
        private static string GetDefaultOutputPath()
        {
            string localAppData = System.Environment.GetFolderPath(System.Environment.SpecialFolder.LocalApplicationData);
            string outputDir = Path.Combine(localAppData, "VirtualCamStudio", "Output");
            return Path.Combine(outputDir, "obs_frame.png");
        }

        /// <summary>
        /// Disposes resources
        /// </summary>
        public void Dispose()
        {
            _disposed = true;
        }
    }
}
