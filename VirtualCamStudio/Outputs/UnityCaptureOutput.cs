using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using VirtualCamStudio.Core;
using OpenCvSharp;

namespace VirtualCamStudio.Outputs
{
    /// <summary>
    /// Output target that sends frames to UnityCaptureSender via named pipe IPC.
    /// UnityCaptureSender forwards frames to Unity Video Capture using the native UnityCapture protocol.
    /// This class is a frame producer only - it does not communicate with UnityCapture directly.
    /// </summary>
    public class UnityCaptureOutput : IOutputTarget, IDisposable
    {
        private const string PipeName = "VirtualCamStudio_Frames";
        private const int MaxReconnectAttempts = 3;
        private const int ReconnectDelayMs = 1000;

        private NamedPipeClientStream? _pipeClient;
        private bool _isConnected;
        private readonly object _lock = new();
        private bool _disposed;
        private int _framesSent;
        private int _framesFailed;
        private DateTime _lastReportTime = DateTime.UtcNow;

        /// <summary>
        /// Initializes the UnityCapture output and attempts to connect to UnityCaptureSender.
        /// </summary>
        public UnityCaptureOutput()
        {
            Debug.WriteLine("[UnityCaptureOutput] Initializing...");
            ConnectToPipe();
        }

        /// <summary>
        /// Attempts to connect to the UnityCaptureSender named pipe.
        /// </summary>
        private void ConnectToPipe()
        {
            lock (_lock)
            {
                try
                {
                    _pipeClient?.Dispose();
                    _pipeClient = new NamedPipeClientStream(
                        ".",
                        PipeName,
                        PipeDirection.Out,
                        PipeOptions.Asynchronous);

                    Debug.WriteLine("[UnityCaptureOutput] Connecting to UnityCaptureSender...");
                    _pipeClient.Connect(500); // 500ms timeout
                    _isConnected = true;
                    Debug.WriteLine("[UnityCaptureOutput] ✓ Connected to UnityCaptureSender");
                }
                catch (TimeoutException)
                {
                    Debug.WriteLine("[UnityCaptureOutput] ⚠️ Connection timeout - UnityCaptureSender not available");
                    _isConnected = false;
                    _pipeClient?.Dispose();
                    _pipeClient = null;
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[UnityCaptureOutput] ❌ Connection error: {ex.Message}");
                    _isConnected = false;
                    _pipeClient?.Dispose();
                    _pipeClient = null;
                }
            }
        }

        /// <summary>
        /// Sends a rendered frame to UnityCaptureSender via IPC.
        /// </summary>
        /// <param name="frame">The rendered frame to send.</param>
        public async Task SendFrameAsync(Frame frame)
        {
            // DIAGNOSTIC: Log incoming frame state
            Debug.WriteLine($"[UnityCaptureOutput.SendFrameAsync] Received frame - null: {frame == null}, IsValid: {frame?.IsValid ?? false}, Width: {frame?.Width ?? 0}x{frame?.Height ?? 0}");

            if (_disposed || frame == null || !frame.IsValid)
            {
                Debug.WriteLine($"[UnityCaptureOutput.SendFrameAsync] ⚠️ Skipping - disposed: {_disposed}, null: {frame == null}, invalid: {!(frame?.IsValid ?? false)}");
                return;
            }

            try
            {
                // Ensure we have a connection and capture the pipe reference
                NamedPipeClientStream? currentPipe;
                lock (_lock)
                {
                    if (!_isConnected || _pipeClient == null || !_pipeClient.IsConnected)
                    {
                        ConnectToPipe();
                        if (!_isConnected || _pipeClient == null)
                        {
                            _framesFailed++;
                            return;
                        }
                    }
                    currentPipe = _pipeClient;
                }

                // If pipe was disposed between lock and here, bail out
                if (currentPipe == null)
                {
                    _framesFailed++;
                    return;
                }

                // Convert frame to BGRA format (UnityCapture expects BGRA despite FORMAT_UINT8 name)
                Mat bgraFrame;
                if (frame.Image.Type() == MatType.CV_8UC4)
                {
                    // Already 4-channel
                    Debug.WriteLine($"[UnityCaptureOutput] 4-channel frame, PixelFormat={frame.PixelFormat}");
                    if (frame.PixelFormat == PixelFormat.BGRA)
                    {
                        bgraFrame = frame.Image;
                    }
                    else
                    {
                        // Convert RGBA to BGRA
                        bgraFrame = new Mat();
                        Cv2.CvtColor(frame.Image, bgraFrame, ColorConversionCodes.RGBA2BGRA);
                    }
                }
                else if (frame.Image.Type() == MatType.CV_8UC3)
                {
                    // BGR to BGRA (just add alpha channel)
                    Debug.WriteLine($"[UnityCaptureOutput] 3-channel frame, converting BGR to BGRA");
                    bgraFrame = new Mat();
                    Cv2.CvtColor(frame.Image, bgraFrame, ColorConversionCodes.BGR2BGRA);
                }
                else
                {
                    Debug.WriteLine($"[UnityCaptureOutput] ⚠️ Unsupported frame format: {frame.Image.Type()}");
                    _framesFailed++;
                    return;
                }

                try
                {
                    // Get frame data
                    int width = bgraFrame.Width;
                    int height = bgraFrame.Height;
                    int stride = width; // Pixels per row
                    int dataSize = width * height * 4;

                    // UnityCapture expects RGBA format (it then converts RGBA→BGRA internally)
                    // Convert BGRA to RGBA
                    Mat rgbaFrame = new Mat();
                    Cv2.CvtColor(bgraFrame, rgbaFrame, ColorConversionCodes.BGRA2RGBA);

                    // Prepare header
                    byte[] headerBytes = new byte[20];
                    BitConverter.GetBytes(width).CopyTo(headerBytes, 0);
                    BitConverter.GetBytes(height).CopyTo(headerBytes, 4);
                    BitConverter.GetBytes(stride).CopyTo(headerBytes, 8);
                    BitConverter.GetBytes(dataSize).CopyTo(headerBytes, 12);
                    BitConverter.GetBytes(0).CopyTo(headerBytes, 16); // PixelFormat: 0 = RGBA32

                    // Get pixel data (RGBA format as expected by UnityCapture)
                    byte[] pixelData = new byte[dataSize];
                    System.Runtime.InteropServices.Marshal.Copy(
                        rgbaFrame.Data,
                        pixelData,
                        0,
                        dataSize);

                    // DIAGNOSTIC: Check actual pixel values (RGBA format)
                    Debug.WriteLine($"[UnityCaptureOutput] Frame: {width}x{height}, First pixel RGBA: ({pixelData[0]}, {pixelData[1]}, {pixelData[2]}, {pixelData[3]})");
                    Debug.WriteLine($"[UnityCaptureOutput] Frame: Center pixel RGBA: ({pixelData[dataSize/2]}, {pixelData[dataSize/2+1]}, {pixelData[dataSize/2+2]}, {pixelData[dataSize/2+3]})");

                    // Send header then pixel data using captured pipe reference
                    await currentPipe.WriteAsync(headerBytes, 0, headerBytes.Length);
                    await currentPipe.WriteAsync(pixelData, 0, pixelData.Length);
                    await currentPipe.FlushAsync();

                    _framesSent++;
                    ReportStats();

                    // Clean up
                    rgbaFrame.Dispose();
                }
                finally
                {
                    if (bgraFrame != frame.Image)
                    {
                        bgraFrame.Dispose();
                    }
                }
            }
            catch (IOException ex)
            {
                Debug.WriteLine($"[UnityCaptureOutput] ❌ Pipe error: {ex.Message}");
                _framesFailed++;
                lock (_lock)
                {
                    _isConnected = false;
                    _pipeClient?.Dispose();
                    _pipeClient = null;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[UnityCaptureOutput] ❌ Send error: {ex.Message}");
                _framesFailed++;
            }
        }

        /// <summary>
        /// Converts BGRA to RGBA format.
        /// </summary>
        private Mat ConvertToRGBA(Mat bgra)
        {
            var rgba = new Mat();
            Cv2.CvtColor(bgra, rgba, ColorConversionCodes.BGRA2RGBA);
            return rgba;
        }

        /// <summary>
        /// Reports statistics once per second.
        /// </summary>
        private void ReportStats()
        {
            var now = DateTime.UtcNow;
            if ((now - _lastReportTime).TotalSeconds >= 1.0)
            {
                Debug.WriteLine($"[UnityCaptureOutput] FPS Sent: {_framesSent} | Failed: {_framesFailed}");
                _framesSent = 0;
                _framesFailed = 0;
                _lastReportTime = now;
            }
        }

        /// <summary>
        /// Checks if currently connected to UnityCaptureSender.
        /// </summary>
        public bool IsConnected
        {
            get
            {
                lock (_lock)
                {
                    return _isConnected && _pipeClient != null && _pipeClient.IsConnected;
                }
            }
        }

        /// <summary>
        /// Disposes the pipe connection and resources.
        /// </summary>
        public void Dispose()
        {
            if (_disposed)
                return;

            lock (_lock)
            {
                Debug.WriteLine("[UnityCaptureOutput] Shutting down...");
                _pipeClient?.Dispose();
                _pipeClient = null;
                _isConnected = false;
                _disposed = true;
            }

            GC.SuppressFinalize(this);
        }
    }
}
