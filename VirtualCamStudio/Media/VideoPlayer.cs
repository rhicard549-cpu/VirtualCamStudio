using OpenCvSharp;
using System;
using System.Diagnostics;

namespace VirtualCamStudio.Media
{
    /// <summary>
    /// Video file loader and decoder using OpenCvSharp.VideoCapture.
    /// Reads video metadata (dimensions, FPS, frame count, duration).
    /// Decodes video frames sequentially or by seeking to specific frame numbers.
    /// Does not implement playback - focuses on video information extraction and frame decoding.
    /// </summary>
    public class VideoPlayer : IDisposable
    {
        // ============================================
        // Fields
        // ============================================

        private VideoCapture? _capture;
        private bool _disposed;
        private string _currentPath = string.Empty;

        // ============================================
        // Properties
        // ============================================

        /// <summary>
        /// Gets whether a video file is currently opened.
        /// </summary>
        public bool IsOpened => _capture?.IsOpened() ?? false;

        /// <summary>
        /// Gets the width of the video in pixels.
        /// Returns 0 if no video is opened.
        /// </summary>
        public int Width
        {
            get
            {
                if (!IsOpened) return 0;
                return (int)_capture!.Get(VideoCaptureProperties.FrameWidth);
            }
        }

        /// <summary>
        /// Gets the height of the video in pixels.
        /// Returns 0 if no video is opened.
        /// </summary>
        public int Height
        {
            get
            {
                if (!IsOpened) return 0;
                return (int)_capture!.Get(VideoCaptureProperties.FrameHeight);
            }
        }

        /// <summary>
        /// Gets the frames per second (FPS) of the video.
        /// Returns 0 if no video is opened.
        /// </summary>
        public double FPS
        {
            get
            {
                if (!IsOpened) return 0;
                return _capture!.Get(VideoCaptureProperties.Fps);
            }
        }

        /// <summary>
        /// Gets the total number of frames in the video.
        /// Returns 0 if no video is opened.
        /// </summary>
        public long FrameCount
        {
            get
            {
                if (!IsOpened) return 0;
                return (long)_capture!.Get(VideoCaptureProperties.FrameCount);
            }
        }

        /// <summary>
        /// Gets the duration of the video.
        /// Calculated from frame count and FPS.
        /// Returns TimeSpan.Zero if no video is opened or FPS is invalid.
        /// </summary>
        public TimeSpan Duration
        {
            get
            {
                if (!IsOpened) return TimeSpan.Zero;

                double fps = FPS;
                long frameCount = FrameCount;

                // Avoid division by zero
                if (fps <= 0 || frameCount <= 0)
                    return TimeSpan.Zero;

                double totalSeconds = frameCount / fps;
                return TimeSpan.FromSeconds(totalSeconds);
            }
        }

        /// <summary>
        /// Gets the path of the currently opened video file.
        /// Returns empty string if no video is opened.
        /// </summary>
        public string CurrentPath => _currentPath;

        /// <summary>
        /// Gets the current frame position in the video.
        /// Returns 0 if no video is opened.
        /// This is the index of the next frame that will be read (0-based).
        /// </summary>
        public long CurrentFrame
        {
            get
            {
                if (!IsOpened) return 0;
                return (long)_capture!.Get(VideoCaptureProperties.PosFrames);
            }
        }

        /// <summary>
        /// Gets whether the video has reached the end.
        /// Returns true if CurrentFrame >= FrameCount or no video is opened.
        /// </summary>
        public bool EndOfVideo
        {
            get
            {
                if (!IsOpened) return true;
                return CurrentFrame >= FrameCount;
            }
        }

        // ============================================
        // Public Methods
        // ============================================

        /// <summary>
        /// Opens a video file and reads its metadata.
        /// Closes any previously opened video.
        /// Supported formats: MP4, MOV, AVI, MKV (depends on OpenCV build and codecs).
        /// </summary>
        /// <param name="path">Full path to the video file</param>
        /// <returns>True if the video was opened successfully, false otherwise</returns>
        public bool Open(string path)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(path))
                {
                    Debug.WriteLine("[VideoPlayer] Invalid path provided.");
                    return false;
                }

                // Close any existing video
                Close();

                Debug.WriteLine($"[VideoPlayer] Opening video: {path}");

                // Create new VideoCapture
                _capture = new VideoCapture(path);

                if (!_capture.IsOpened())
                {
                    Debug.WriteLine($"[VideoPlayer] Failed to open video: {path}");
                    _capture.Dispose();
                    _capture = null;
                    _currentPath = string.Empty;
                    return false;
                }

                _currentPath = path;

                // Log video information
                Debug.WriteLine($"[VideoPlayer] Video opened successfully:");
                Debug.WriteLine($"  - Resolution: {Width}x{Height}");
                Debug.WriteLine($"  - FPS: {FPS:F2}");
                Debug.WriteLine($"  - Frame Count: {FrameCount}");
                Debug.WriteLine($"  - Duration: {Duration}");

                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[VideoPlayer] Error opening video: {ex.Message}");
                Close();
                return false;
            }
        }

        /// <summary>
        /// Closes the currently opened video and releases resources.
        /// Safe to call multiple times or when no video is opened.
        /// </summary>
        public void Close()
        {
            try
            {
                if (_capture != null)
                {
                    Debug.WriteLine($"[VideoPlayer] Closing video: {_currentPath}");
                    _capture.Dispose();
                    _capture = null;
                    _currentPath = string.Empty;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[VideoPlayer] Error closing video: {ex.Message}");
            }
        }

        /// <summary>
        /// Reads the next frame from the video sequentially.
        /// Returns the original frame without any modifications (no resize, no processing).
        /// The frame must be disposed by the caller when no longer needed.
        /// </summary>
        /// <param name="frame">The decoded frame, or an empty Mat if reading failed</param>
        /// <returns>True if the frame was read successfully, false if end of video or error</returns>
        public bool ReadNextFrame(out Mat frame)
        {
            frame = new Mat();

            try
            {
                if (!IsOpened)
                {
                    Debug.WriteLine("[VideoPlayer] Cannot read frame - no video opened.");
                    return false;
                }

                if (EndOfVideo)
                {
                    Debug.WriteLine("[VideoPlayer] Cannot read frame - end of video reached.");
                    return false;
                }

                // Read the next frame
                bool success = _capture!.Read(frame);

                if (!success || frame.Empty())
                {
                    Debug.WriteLine($"[VideoPlayer] Failed to read frame at position {CurrentFrame}.");
                    return false;
                }

                Debug.WriteLine($"[VideoPlayer] Read frame {CurrentFrame - 1}: {frame.Width}x{frame.Height}");
                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[VideoPlayer] Error reading frame: {ex.Message}");
                frame.Dispose();
                frame = new Mat();
                return false;
            }
        }

        /// <summary>
        /// Seeks to a specific frame number in the video.
        /// The next call to ReadNextFrame() will read this frame.
        /// </summary>
        /// <param name="frameNumber">The frame number to seek to (0-based index)</param>
        /// <returns>True if seek was successful, false otherwise</returns>
        public bool Seek(long frameNumber)
        {
            try
            {
                if (!IsOpened)
                {
                    Debug.WriteLine("[VideoPlayer] Cannot seek - no video opened.");
                    return false;
                }

                if (frameNumber < 0)
                {
                    Debug.WriteLine($"[VideoPlayer] Invalid frame number: {frameNumber} (must be >= 0).");
                    return false;
                }

                if (frameNumber >= FrameCount)
                {
                    Debug.WriteLine($"[VideoPlayer] Frame number {frameNumber} exceeds frame count {FrameCount}.");
                    return false;
                }

                Debug.WriteLine($"[VideoPlayer] Seeking to frame {frameNumber}...");

                // Set the frame position
                _capture!.Set(VideoCaptureProperties.PosFrames, frameNumber);

                // Verify the seek was successful
                long actualPosition = CurrentFrame;
                if (actualPosition != frameNumber)
                {
                    Debug.WriteLine($"[VideoPlayer] Seek verification failed. Requested: {frameNumber}, Actual: {actualPosition}");
                    // Note: Some video formats may not seek to the exact frame
                    // This is a limitation of the codec/container format
                }

                Debug.WriteLine($"[VideoPlayer] Seek completed. Current frame: {actualPosition}");
                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[VideoPlayer] Error seeking to frame {frameNumber}: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Reads the first frame of the video for thumbnail generation.
        /// Returns null if no video is opened or the frame cannot be read.
        /// Does not change the current playback position.
        /// </summary>
        /// <returns>The first frame as a Mat, or null on failure</returns>
        public Mat? ReadFirstFrame()
        {
            try
            {
                if (!IsOpened)
                {
                    Debug.WriteLine("[VideoPlayer] Cannot read frame - no video opened.");
                    return null;
                }

                // Save current position
                double originalPosition = _capture!.Get(VideoCaptureProperties.PosFrames);

                // Seek to first frame
                _capture.Set(VideoCaptureProperties.PosFrames, 0);

                // Read first frame
                var frame = new Mat();
                bool success = _capture.Read(frame);

                // Restore original position
                _capture.Set(VideoCaptureProperties.PosFrames, originalPosition);

                if (!success || frame.Empty())
                {
                    Debug.WriteLine("[VideoPlayer] Failed to read first frame.");
                    frame.Dispose();
                    return null;
                }

                Debug.WriteLine($"[VideoPlayer] Read first frame: {frame.Width}x{frame.Height}");
                return frame;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[VideoPlayer] Error reading first frame: {ex.Message}");
                return null;
            }
        }

        // ============================================
        // IDisposable
        // ============================================

        /// <summary>
        /// Disposes the video player and releases all resources.
        /// </summary>
        public void Dispose()
        {
            if (!_disposed)
            {
                Close();
                _disposed = true;
                Debug.WriteLine("[VideoPlayer] Disposed.");
            }
        }
    }
}
