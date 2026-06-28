using System;
using System.Diagnostics;
using VirtualCamStudio.Core;

namespace VirtualCamStudio.Services
{
    /// <summary>
    /// Virtual camera service stub.
    /// Receives rendered frames from OutputManager and logs frame information.
    /// Does not create an actual camera device yet - this is preparation for future implementation.
    /// </summary>
    public class VirtualCameraService : IOutputTarget
    {
        private long _frameCount = 0;
        private readonly object _statsLock = new();

        /// <summary>
        /// Total number of frames received by the virtual camera.
        /// </summary>
        public long FrameCount
        {
            get
            {
                lock (_statsLock)
                {
                    return _frameCount;
                }
            }
        }

        /// <summary>
        /// Receives a frame from the output manager.
        /// Logs frame metadata and increments the frame counter.
        /// </summary>
        public void Receive(Frame frame)
        {
            if (frame == null || !frame.IsValid)
            {
                Debug.WriteLine("[VirtualCamera] Received invalid frame, skipping.");
                return;
            }

            long currentCount;
            lock (_statsLock)
            {
                _frameCount++;
                currentCount = _frameCount;
            }

            // Log frame information
            Debug.WriteLine($"[VirtualCamera] Frame #{currentCount}: " +
                          $"Size={frame.Width}x{frame.Height}, " +
                          $"Timestamp={frame.Timestamp:HH:mm:ss.fff}, " +
                          $"Format={frame.PixelFormat}");
        }

        /// <summary>
        /// Resets the frame counter.
        /// Useful for testing or when restarting the virtual camera.
        /// </summary>
        public void ResetStats()
        {
            lock (_statsLock)
            {
                _frameCount = 0;
            }
            Debug.WriteLine("[VirtualCamera] Stats reset.");
        }
    }
}
