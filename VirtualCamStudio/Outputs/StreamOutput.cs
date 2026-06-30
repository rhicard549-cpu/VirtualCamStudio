using VirtualCamStudio.Core;

namespace VirtualCamStudio.Outputs
{
    /// <summary>
    /// Output plugin that streams rendered frames to a network destination (RTMP, SRT, etc.).
    /// Future implementation: Commit 46+
    /// </summary>
    [OutputPlugin("Network Stream", "Streams rendered frames to RTMP, SRT, or other network destinations", "Streaming", "1.0.0")]
    public class StreamOutput : IOutputTarget
    {
        private readonly object _lock = new();
        private bool _isStreaming;
        private string? _streamUrl;

        /// <summary>
        /// Gets whether streaming is currently active.
        /// </summary>
        public bool IsStreaming => _isStreaming;

        /// <summary>
        /// Gets the current stream URL (if streaming).
        /// </summary>
        public string? StreamUrl => _streamUrl;

        /// <summary>
        /// Starts streaming to the specified URL.
        /// </summary>
        /// <param name="url">The stream destination URL (e.g., rtmp://server/stream).</param>
        /// <param name="fps">Frames per second for the stream.</param>
        /// <param name="width">Stream width in pixels.</param>
        /// <param name="height">Stream height in pixels.</param>
        /// <param name="bitrate">Stream bitrate in kbps.</param>
        public void StartStreaming(string url, double fps, int width, int height, int bitrate)
        {
            lock (_lock)
            {
                if (_isStreaming)
                    throw new InvalidOperationException("Streaming is already in progress.");

                _streamUrl = url ?? throw new ArgumentNullException(nameof(url));

                // TODO: Initialize FFmpeg streaming pipeline
                // - Create FFmpeg process with RTMP output
                // - Configure H.264 encoder with specified bitrate
                // - Set resolution and frame rate
                // - Connect stdin pipe for frame input

                _isStreaming = true;
            }
        }

        /// <summary>
        /// Stops the current stream.
        /// </summary>
        public void StopStreaming()
        {
            lock (_lock)
            {
                if (!_isStreaming)
                    return;

                // TODO: Close FFmpeg process gracefully
                // - Flush remaining frames
                // - Close stdin pipe
                // - Wait for process exit

                _isStreaming = false;
                _streamUrl = null;
            }
        }

        /// <summary>
        /// Sends a rendered frame to the stream (if streaming is active).
        /// </summary>
        /// <param name="frame">The rendered frame to stream.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        public Task SendFrameAsync(Frame frame)
        {
            lock (_lock)
            {
                if (!_isStreaming)
                    return Task.CompletedTask;

                if (frame == null || !frame.IsValid)
                    return Task.CompletedTask;

                // TODO: Send frame to FFmpeg stdin
                // - Convert Mat to raw BGR/RGB bytes
                // - Write to FFmpeg stdin pipe
                // - Handle backpressure if encoder is slow
            }

            return Task.CompletedTask;
        }

        /// <summary>
        /// Releases resources used by the stream output.
        /// </summary>
        public void Dispose()
        {
            StopStreaming();
        }
    }
}
