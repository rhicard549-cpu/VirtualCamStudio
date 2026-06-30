using OpenCvSharp;
using VirtualCamStudio.Core;

namespace VirtualCamStudio.Outputs
{
    /// <summary>
    /// Output plugin that records rendered frames to a video file.
    /// Future implementation: Commit 44+
    /// </summary>
    [OutputPlugin("Video Recording", "Records rendered frames to a video file (MP4, AVI, etc.)", "Recording", "1.0.0")]
    public class RecordingOutput : IOutputTarget
    {
        private VideoWriter? _videoWriter;
        private readonly object _lock = new();
        private bool _isRecording;

        /// <summary>
        /// Gets whether recording is currently active.
        /// </summary>
        public bool IsRecording => _isRecording;

        /// <summary>
        /// Starts recording to the specified file path.
        /// </summary>
        /// <param name="filePath">The output video file path.</param>
        /// <param name="fps">Frames per second for the output video.</param>
        /// <param name="width">Video width in pixels.</param>
        /// <param name="height">Video height in pixels.</param>
        public void StartRecording(string filePath, double fps, int width, int height)
        {
            lock (_lock)
            {
                if (_isRecording)
                    throw new InvalidOperationException("Recording is already in progress.");

                // TODO: Initialize VideoWriter with codec (e.g., H.264, MJPEG)
                // _videoWriter = new VideoWriter(filePath, FourCC.H264, fps, new Size(width, height));

                _isRecording = true;
            }
        }

        /// <summary>
        /// Stops the current recording and closes the video file.
        /// </summary>
        public void StopRecording()
        {
            lock (_lock)
            {
                if (!_isRecording)
                    return;

                _videoWriter?.Release();
                _videoWriter?.Dispose();
                _videoWriter = null;
                _isRecording = false;
            }
        }

        /// <summary>
        /// Sends a rendered frame to the video file (if recording is active).
        /// </summary>
        /// <param name="frame">The rendered frame to write to the video file.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        public Task SendFrameAsync(Frame frame)
        {
            lock (_lock)
            {
                if (!_isRecording || _videoWriter == null)
                    return Task.CompletedTask;

                if (frame == null || !frame.IsValid)
                    return Task.CompletedTask;

                // TODO: Write frame to video file
                // _videoWriter.Write(frame.Image);
            }

            return Task.CompletedTask;
        }

        /// <summary>
        /// Releases resources used by the recording output.
        /// </summary>
        public void Dispose()
        {
            StopRecording();
        }
    }
}
