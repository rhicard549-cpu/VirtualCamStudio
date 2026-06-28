using OpenCvSharp;
using System;
using System.Diagnostics;
using System.IO;
using VirtualCamStudio.Core;

namespace VirtualCamStudio.Services.OBS
{
    /// <summary>
    /// Exports rendered frames to a temporary PNG file for OBS Image Source consumption.
    /// Maintains a single overwrite-on-update file at a known location.
    /// </summary>
    public class OBSImageOutput : IOutputTarget, IDisposable
    {
        // ============================================
        // Fields
        // ============================================

        private readonly string _outputPath;
        private readonly object _writeLock = new object();
        private bool _disposed;

        // ============================================
        // Properties
        // ============================================

        /// <summary>
        /// Gets the full path to the output PNG file.
        /// </summary>
        public string OutputPath => _outputPath;

        /// <summary>
        /// Gets whether the output file currently exists.
        /// </summary>
        public bool FileExists => File.Exists(_outputPath);

        // ============================================
        // Constructor
        // ============================================

        /// <summary>
        /// Creates a new OBS image output exporter.
        /// Automatically creates the output directory if it doesn't exist.
        /// </summary>
        /// <param name="outputPath">Optional custom output path. If null, uses default location in %LocalAppData%\VirtualCamStudio\Output\preview.png</param>
        public OBSImageOutput(string? outputPath = null)
        {
            // Use default path if none provided
            _outputPath = outputPath ?? GetDefaultOutputPath();

            // Ensure output directory exists
            string? directory = Path.GetDirectoryName(_outputPath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
                Debug.WriteLine($"[OBSImageOutput] Created output directory: {directory}");
            }

            Debug.WriteLine($"[OBSImageOutput] Initialized with output path: {_outputPath}");
        }

        // ============================================
        // Public Methods
        // ============================================

        /// <summary>
        /// Updates the output file with a new frame.
        /// Overwrites the existing file if present.
        /// Thread-safe.
        /// </summary>
        /// <param name="frame">The frame to export as PNG</param>
        public void UpdateFrame(Mat frame)
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(OBSImageOutput));
            }

            if (frame == null || frame.Empty())
            {
                Debug.WriteLine("[OBSImageOutput] Skipping empty frame");
                return;
            }

            try
            {
                lock (_writeLock)
                {
                    // Write directly to output path
                    // OpenCV Cv2.ImWrite is efficient and handles BGR/BGRA formats automatically
                    Cv2.ImWrite(_outputPath, frame);
                    Debug.WriteLine($"[OBSImageOutput] Updated frame: {frame.Width}x{frame.Height} -> {_outputPath}");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[OBSImageOutput] Error writing frame: {ex.Message}");
                // Swallow exception to prevent crashes - output is non-critical
            }
        }

        /// <summary>
        /// Receives a rendered frame from the output manager.
        /// Implements IOutputTarget.Receive.
        /// </summary>
        /// <param name="frame">The frame to export</param>
        public void Receive(Frame frame)
        {
            if (frame?.Image != null)
            {
                UpdateFrame(frame.Image);
            }
        }

        /// <summary>
        /// Deletes the output file if it exists.
        /// Useful for cleanup when stopping output.
        /// </summary>
        public void ClearOutput()
        {
            try
            {
                lock (_writeLock)
                {
                    if (File.Exists(_outputPath))
                    {
                        File.Delete(_outputPath);
                        Debug.WriteLine($"[OBSImageOutput] Cleared output file: {_outputPath}");
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[OBSImageOutput] Error clearing output: {ex.Message}");
            }
        }

        // ============================================
        // Helper Methods
        // ============================================

        /// <summary>
        /// Gets the default output path in %LocalAppData%\VirtualCamStudio\Output\preview.png
        /// </summary>
        private static string GetDefaultOutputPath()
        {
            string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            return Path.Combine(localAppData, "VirtualCamStudio", "Output", "preview.png");
        }

        // ============================================
        // IDisposable
        // ============================================

        /// <summary>
        /// Disposes the output target.
        /// Optionally clears the output file on disposal.
        /// </summary>
        public void Dispose()
        {
            if (!_disposed)
            {
                // Note: We don't auto-clear the file on disposal
                // The file should persist for OBS to keep displaying
                // User can call ClearOutput() explicitly if needed

                _disposed = true;
                Debug.WriteLine("[OBSImageOutput] Disposed");
            }
        }
    }
}
