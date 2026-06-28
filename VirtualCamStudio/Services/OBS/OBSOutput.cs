using OpenCvSharp;
using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using VirtualCamStudio.Core;

namespace VirtualCamStudio.Services.OBS
{
    /// <summary>
    /// OBS output target that integrates with OutputManager.
    /// Receives rendered frames and maintains the latest frame for OBS transport.
    /// 
    /// Responsibilities:
    /// - Receive rendered frames from OutputManager
    /// - Maintain the latest rendered frame
    /// - Provide async frame sending for future transport implementation
    /// 
    /// Does NOT:
    /// - Modify the renderer
    /// - Duplicate rendering logic
    /// - Perform image processing
    /// - Manage OBS scenes
    /// - Control virtual camera
    /// - Contain UI code
    /// 
    /// Thread-safe and lightweight design ready for future transport implementation.
    /// </summary>
    public class OBSOutput : IOutputTarget, IDisposable
    {
        // ============================================
        // Fields
        // ============================================

        private readonly object _lock = new();
        private Mat? _latestFrame;
        private bool _enabled;
        private bool _disposed;

        // ============================================
        // Properties
        // ============================================

        /// <summary>
        /// Gets or sets whether this output target is enabled.
        /// When disabled, frames are still received but not stored.
        /// </summary>
        public bool Enabled
        {
            get
            {
                lock (_lock)
                {
                    return _enabled;
                }
            }
            set
            {
                lock (_lock)
                {
                    _enabled = value;

                    // Clear latest frame when disabled
                    if (!_enabled && _latestFrame != null)
                    {
                        _latestFrame.Dispose();
                        _latestFrame = null;
                        Debug.WriteLine("[OBSOutput] Disabled and cleared latest frame.");
                    }
                }

                Debug.WriteLine($"[OBSOutput] Enabled = {value}");
            }
        }

        /// <summary>
        /// Gets whether a frame is currently available.
        /// </summary>
        public bool HasFrame
        {
            get
            {
                lock (_lock)
                {
                    return _latestFrame != null && !_latestFrame.Empty();
                }
            }
        }

        // ============================================
        // Constructor
        // ============================================

        /// <summary>
        /// Creates a new OBS output target.
        /// Default state is disabled.
        /// </summary>
        public OBSOutput()
        {
            _enabled = false;
            Debug.WriteLine("[OBSOutput] Initialized (disabled by default).");
        }

        // ============================================
        // IOutputTarget Implementation
        // ============================================

        /// <summary>
        /// Receives a rendered frame from OutputManager.
        /// Clones and stores the latest frame if enabled.
        /// Thread-safe operation.
        /// </summary>
        /// <param name="frame">The frame to receive</param>
        public void Receive(Frame frame)
        {
            if (_disposed)
                return;

            if (frame == null || !frame.IsValid)
                return;

            lock (_lock)
            {
                // Only store frames when enabled
                if (!_enabled)
                    return;

                try
                {
                    // Dispose previous frame
                    _latestFrame?.Dispose();

                    // Clone the frame data (do not store the original reference)
                    _latestFrame = frame.Image.Clone();

                    Debug.WriteLine($"[OBSOutput] Received frame: {frame.Width}x{frame.Height}");
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[OBSOutput] Error receiving frame: {ex.Message}");
                    _latestFrame?.Dispose();
                    _latestFrame = null;
                }
            }
        }

        // ============================================
        // Public Methods
        // ============================================

        /// <summary>
        /// Sends the latest frame asynchronously.
        /// Placeholder for future transport implementation (WebSocket, pipe, etc.).
        /// Thread-safe operation.
        /// </summary>
        /// <param name="frame">The frame to send (optional, uses latest if null)</param>
        /// <returns>Task representing the async send operation</returns>
        public async Task SendFrameAsync(Frame? frame = null)
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(OBSOutput));

            Mat? frameToSend = null;

            try
            {
                lock (_lock)
                {
                    if (!_enabled)
                    {
                        Debug.WriteLine("[OBSOutput] SendFrameAsync called but output is disabled.");
                        return;
                    }

                    // Use provided frame or latest stored frame
                    if (frame != null && frame.IsValid)
                    {
                        frameToSend = frame.Image.Clone();
                    }
                    else if (_latestFrame != null && !_latestFrame.Empty())
                    {
                        frameToSend = _latestFrame.Clone();
                    }
                    else
                    {
                        Debug.WriteLine("[OBSOutput] SendFrameAsync: No frame available.");
                        return;
                    }
                }

                // Simulate async transport operation
                // Future implementation will send via WebSocket, shared memory, pipe, etc.
                await Task.Run(() =>
                {
                    Debug.WriteLine($"[OBSOutput] SendFrameAsync: Would send frame {frameToSend.Width}x{frameToSend.Height}");

                    // Placeholder for future transport implementation:
                    // - Encode frame (e.g., PNG, JPEG, raw)
                    // - Send via WebSocket/pipe/shared memory
                    // - Handle transport errors

                    Thread.Sleep(1); // Simulate minimal async work
                });

                Debug.WriteLine("[OBSOutput] Frame sent successfully.");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[OBSOutput] Error sending frame: {ex.Message}");
                throw;
            }
            finally
            {
                // Clean up cloned frame
                frameToSend?.Dispose();
            }
        }

        /// <summary>
        /// Gets a clone of the latest frame.
        /// Returns null if no frame is available or output is disabled.
        /// Thread-safe operation.
        /// Caller is responsible for disposing the returned Mat.
        /// </summary>
        /// <returns>Cloned Mat or null</returns>
        public Mat? GetLatestFrame()
        {
            lock (_lock)
            {
                if (!_enabled || _latestFrame == null || _latestFrame.Empty())
                    return null;

                return _latestFrame.Clone();
            }
        }

        /// <summary>
        /// Clears the stored latest frame.
        /// Thread-safe operation.
        /// </summary>
        public void ClearFrame()
        {
            lock (_lock)
            {
                _latestFrame?.Dispose();
                _latestFrame = null;
                Debug.WriteLine("[OBSOutput] Cleared latest frame.");
            }
        }

        // ============================================
        // IDisposable Implementation
        // ============================================

        /// <summary>
        /// Disposes the OBS output and releases resources.
        /// </summary>
        public void Dispose()
        {
            if (!_disposed)
            {
                lock (_lock)
                {
                    _latestFrame?.Dispose();
                    _latestFrame = null;
                    _enabled = false;
                }

                _disposed = true;
                Debug.WriteLine("[OBSOutput] Disposed.");
            }
        }
    }
}
