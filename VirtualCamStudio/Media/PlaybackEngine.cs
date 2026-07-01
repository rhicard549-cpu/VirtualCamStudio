using OpenCvSharp;
using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using VirtualCamStudio.Core;
using VirtualCamStudio.Models;

namespace VirtualCamStudio.Media
{
    /// <summary>
    /// Timer-based video playback engine.
    /// Decodes video frames using VideoPlayer and sends them through ViewportEngine for rendering.
    /// Respects the video's FPS and provides playback controls (play, pause, stop, loop).
    /// Does not modify frames - passes them through the rendering pipeline as-is.
    /// </summary>
    public class PlaybackEngine : IDisposable
    {
        // ============================================
        // Fields
        // ============================================

        private readonly VideoPlayer _videoPlayer;
        private readonly ViewportEngine _viewportEngine = new();
        private CancellationTokenSource? _playbackCancellation;
        private Task? _playbackTask;
        private bool _disposed;

        private double _playbackSpeed = 1.0;
        private bool _loop = false;
        private PlaybackState _state = PlaybackState.Stopped;

        // ============================================
        // Events
        // ============================================

        /// <summary>
        /// Event raised when a new frame is ready to be displayed.
        /// Subscribe to this event to receive rendered frames.
        /// The frame must be consumed immediately - do not store the reference.
        /// </summary>
        public event EventHandler<Frame>? FrameReady;

        /// <summary>
        /// Event raised when playback state changes.
        /// </summary>
        public event EventHandler<PlaybackState>? StateChanged;

        /// <summary>
        /// Event raised when playback reaches the end of the video.
        /// </summary>
        public event EventHandler? EndOfPlayback;

        // ============================================
        // Properties
        // ============================================

        /// <summary>
        /// Gets or sets whether playback should loop when reaching the end.
        /// </summary>
        public bool Loop
        {
            get => _loop;
            set => _loop = value;
        }

        /// <summary>
        /// Gets or sets the playback speed multiplier.
        /// 1.0 = normal speed, 2.0 = double speed, 0.5 = half speed.
        /// Must be greater than 0.
        /// </summary>
        public double PlaybackSpeed
        {
            get => _playbackSpeed;
            set
            {
                if (value <= 0)
                    throw new ArgumentOutOfRangeException(nameof(value), "Playback speed must be greater than 0.");
                _playbackSpeed = value;
            }
        }

        /// <summary>
        /// Gets the current playback time.
        /// </summary>
        public TimeSpan CurrentTime
        {
            get
            {
                if (!_videoPlayer.IsOpened)
                    return TimeSpan.Zero;

                double fps = _videoPlayer.FPS;
                if (fps <= 0)
                    return TimeSpan.Zero;

                double seconds = _videoPlayer.CurrentFrame / fps;
                return TimeSpan.FromSeconds(seconds);
            }
        }

        /// <summary>
        /// Gets the total duration of the video.
        /// </summary>
        public TimeSpan Duration => _videoPlayer.Duration;

        /// <summary>
        /// Gets the current playback state.
        /// </summary>
        public PlaybackState State => _state;

        /// <summary>
        /// Gets the current frame number.
        /// </summary>
        public long CurrentFrame => _videoPlayer.CurrentFrame;

        /// <summary>
        /// Gets the total frame count.
        /// </summary>
        public long FrameCount => _videoPlayer.FrameCount;

        /// <summary>
        /// Gets whether a video is loaded.
        /// </summary>
        public bool IsVideoLoaded => _videoPlayer.IsOpened;

        // ============================================
        // Constructor
        // ============================================

        /// <summary>
        /// Creates a new playback engine.
        /// </summary>
        /// <param name="videoPlayer">The video player to use for decoding</param>
        public PlaybackEngine(VideoPlayer videoPlayer)
        {
            _videoPlayer = videoPlayer ?? throw new ArgumentNullException(nameof(videoPlayer));
        }

        // ============================================
        // Public Methods
        // ============================================

        /// <summary>
        /// Starts or resumes video playback.
        /// If already playing, this method has no effect.
        /// </summary>
        public void Play()
        {
            if (_state == PlaybackState.Playing)
            {
                Debug.WriteLine("[PlaybackEngine] Already playing.");
                return;
            }

            if (!_videoPlayer.IsOpened)
            {
                Debug.WriteLine("[PlaybackEngine] Cannot play - no video loaded.");
                return;
            }

            // If at end of video and not looping, reset to beginning
            if (_videoPlayer.EndOfVideo && !_loop)
            {
                _videoPlayer.Seek(0);
            }

            Debug.WriteLine("[PlaybackEngine] Starting playback...");

            _playbackCancellation = new CancellationTokenSource();
            _playbackTask = Task.Run(() => PlaybackLoop(_playbackCancellation.Token));

            SetState(PlaybackState.Playing);
        }

        /// <summary>
        /// Pauses video playback (async version for UI).
        /// The current position is maintained.
        /// </summary>
        public async Task PauseAsync()
        {
            if (_state != PlaybackState.Playing)
            {
                Debug.WriteLine("[PlaybackEngine] Not playing - cannot pause.");
                return;
            }

            Debug.WriteLine("[PlaybackEngine] Pausing playback...");

            _playbackCancellation?.Cancel();

            // Don't block - await the task asynchronously
            if (_playbackTask != null)
            {
                try
                {
                    await _playbackTask;
                }
                catch (OperationCanceledException)
                {
                    // Expected when cancelling
                }
            }

            SetState(PlaybackState.Paused);
        }

        /// <summary>
        /// Pauses video playback (synchronous version for internal use).
        /// The current position is maintained.
        /// WARNING: Do not call from UI thread - use PauseAsync() instead.
        /// </summary>
        private void Pause()
        {
            if (_state != PlaybackState.Playing)
            {
                Debug.WriteLine("[PlaybackEngine] Not playing - cannot pause.");
                return;
            }

            Debug.WriteLine("[PlaybackEngine] Pausing playback...");

            _playbackCancellation?.Cancel();

            // Blocking wait - only safe for internal use
            try
            {
                _playbackTask?.Wait();
            }
            catch (AggregateException ex) when (ex.InnerException is OperationCanceledException)
            {
                // Expected when cancelling
            }

            SetState(PlaybackState.Paused);
        }

        /// <summary>
        /// Stops video playback and resets to the beginning (async version for UI).
        /// </summary>
        public async Task StopAsync()
        {
            if (_state == PlaybackState.Stopped)
            {
                Debug.WriteLine("[PlaybackEngine] Already stopped.");
                return;
            }

            Debug.WriteLine("[PlaybackEngine] Stopping playback...");

            _playbackCancellation?.Cancel();

            // Don't block - await the task asynchronously
            if (_playbackTask != null)
            {
                try
                {
                    await _playbackTask;
                }
                catch (OperationCanceledException)
                {
                    // Expected when cancelling
                }
            }

            // Reset to beginning
            if (_videoPlayer.IsOpened)
            {
                _videoPlayer.Seek(0);
            }

            SetState(PlaybackState.Stopped);
        }

        /// <summary>
        /// Stops video playback and resets to the beginning (synchronous version for internal use).
        /// WARNING: Do not call from UI thread - use StopAsync() instead.
        /// </summary>
        private void Stop()
        {
            if (_state == PlaybackState.Stopped)
            {
                Debug.WriteLine("[PlaybackEngine] Already stopped.");
                return;
            }

            Debug.WriteLine("[PlaybackEngine] Stopping playback...");

            _playbackCancellation?.Cancel();

            // Blocking wait - only safe for internal use
            try
            {
                _playbackTask?.Wait();
            }
            catch (AggregateException ex) when (ex.InnerException is OperationCanceledException)
            {
                // Expected when cancelling
            }

            // Reset to beginning
            if (_videoPlayer.IsOpened)
            {
                _videoPlayer.Seek(0);
            }

            SetState(PlaybackState.Stopped);
        }

        /// <summary>
        /// Seeks to a specific frame number (async version for UI).
        /// Playback continues from the new position if playing.
        /// </summary>
        /// <param name="frameNumber">The frame number to seek to</param>
        public async Task<bool> SeekAsync(long frameNumber)
        {
            try
            {
                Debug.WriteLine($"[PlaybackEngine] SeekAsync to frame {frameNumber}, current state: {_state}");

                bool wasPlaying = _state == PlaybackState.Playing;

                if (wasPlaying)
                {
                    Debug.WriteLine($"[PlaybackEngine] Pausing before seek...");
                    await PauseAsync();
                }

                Debug.WriteLine($"[PlaybackEngine] Executing VideoPlayer.Seek({frameNumber})...");
                bool success = _videoPlayer.Seek(frameNumber);
                Debug.WriteLine($"[PlaybackEngine] VideoPlayer.Seek returned: {success}");

                if (wasPlaying && success)
                {
                    Debug.WriteLine($"[PlaybackEngine] Resuming playback after seek...");
                    Play();
                }

                return success;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[PlaybackEngine] ✗ SeekAsync error: {ex.Message}");
                Debug.WriteLine($"[PlaybackEngine] Stack: {ex.StackTrace}");
                return false;
            }
        }

        /// <summary>
        /// Seeks to a specific frame number (synchronous version for internal use).
        /// Playback continues from the new position if playing.
        /// WARNING: Do not call from UI thread - use SeekAsync() instead.
        /// </summary>
        /// <param name="frameNumber">The frame number to seek to</param>
        public bool Seek(long frameNumber)
        {
            bool wasPlaying = _state == PlaybackState.Playing;

            if (wasPlaying)
            {
                Pause();
            }

            bool success = _videoPlayer.Seek(frameNumber);

            if (wasPlaying && success)
            {
                Play();
            }

            return success;
        }

        /// <summary>
        /// Seeks to a specific time position.
        /// </summary>
        /// <param name="time">The time position to seek to</param>
        public bool SeekToTime(TimeSpan time)
        {
            if (!_videoPlayer.IsOpened)
                return false;

            double fps = _videoPlayer.FPS;
            if (fps <= 0)
                return false;

            long frameNumber = (long)(time.TotalSeconds * fps);
            return Seek(frameNumber);
        }

        // ============================================
        // Private Methods
        // ============================================

        /// <summary>
        /// Main playback loop that runs on a background thread.
        /// Reads frames at the correct timing based on FPS and playback speed.
        /// </summary>
        private void PlaybackLoop(CancellationToken cancellationToken)
        {
            try
            {
                double fps = _videoPlayer.FPS;
                if (fps <= 0)
                {
                    Debug.WriteLine("[PlaybackEngine] Invalid FPS - cannot play.");
                    return;
                }

                // Calculate frame interval in milliseconds
                double frameIntervalMs = (1000.0 / fps) / _playbackSpeed;

                var stopwatch = Stopwatch.StartNew();
                long frameCount = 0;

                Debug.WriteLine($"[PlaybackEngine] Playback loop started. FPS: {fps:F2}, Interval: {frameIntervalMs:F2}ms, Speed: {_playbackSpeed}x");

                while (!cancellationToken.IsCancellationRequested)
                {
                    // Check if we've reached the end
                    if (_videoPlayer.EndOfVideo)
                    {
                        if (_loop)
                        {
                            Debug.WriteLine("[PlaybackEngine] End of video reached - looping...");
                            _videoPlayer.Seek(0);
                            frameCount = 0;
                            stopwatch.Restart();
                        }
                        else
                        {
                            Debug.WriteLine("[PlaybackEngine] End of video reached - stopping...");
                            EndOfPlayback?.Invoke(this, EventArgs.Empty);
                            break;
                        }
                    }

                    // Read the next frame
                    if (_videoPlayer.ReadNextFrame(out Mat frame))
                    {
                        try
                        {
                            // Wrap in Frame and send to subscribers
                            // Note: ViewportEngine will be integrated when we have canvas settings
                            // For now, we just pass the raw frame through
                            var videoFrame = new Frame(frame, PixelFormat.BGR)
                            {
                                Timestamp = DateTime.UtcNow,
                                FrameNumber = _videoPlayer.CurrentFrame - 1
                            };

                            FrameReady?.Invoke(this, videoFrame);

                            // Frame is disposed by the event handler or Frame itself
                            videoFrame.Dispose();
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine($"[PlaybackEngine] Error processing frame: {ex.Message}");
                            frame.Dispose();
                        }
                    }
                    else
                    {
                        Debug.WriteLine("[PlaybackEngine] Failed to read frame.");
                        break;
                    }

                    frameCount++;

                    // Calculate how long to wait for the next frame
                    double expectedTimeMs = frameCount * frameIntervalMs;
                    double actualTimeMs = stopwatch.Elapsed.TotalMilliseconds;
                    double waitTimeMs = expectedTimeMs - actualTimeMs;

                    if (waitTimeMs > 0)
                    {
                        // Wait for the remaining time
                        Thread.Sleep((int)waitTimeMs);
                    }
                    else if (waitTimeMs < -frameIntervalMs)
                    {
                        // We're falling behind - reset timing
                        Debug.WriteLine($"[PlaybackEngine] Frame timing drift detected. Resetting. Behind by: {-waitTimeMs:F2}ms");
                        stopwatch.Restart();
                        frameCount = 0;
                    }
                }

                Debug.WriteLine("[PlaybackEngine] Playback loop ended.");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[PlaybackEngine] Playback loop error: {ex.Message}");
            }
        }

        /// <summary>
        /// Updates the playback state and raises the StateChanged event.
        /// </summary>
        private void SetState(PlaybackState newState)
        {
            if (_state != newState)
            {
                _state = newState;
                Debug.WriteLine($"[PlaybackEngine] State changed to: {newState}");
                StateChanged?.Invoke(this, newState);
            }
        }

        // ============================================
        // IDisposable
        // ============================================

        /// <summary>
        /// Disposes the playback engine and stops playback.
        /// </summary>
        public void Dispose()
        {
            if (!_disposed)
            {
                Stop();
                _playbackCancellation?.Dispose();
                _disposed = true;
                Debug.WriteLine("[PlaybackEngine] Disposed.");
            }
        }
    }

    /// <summary>
    /// Represents the current state of video playback.
    /// </summary>
    public enum PlaybackState
    {
        /// <summary>
        /// Playback is stopped. Position is at the beginning.
        /// </summary>
        Stopped,

        /// <summary>
        /// Playback is active and frames are being decoded.
        /// </summary>
        Playing,

        /// <summary>
        /// Playback is paused. Position is maintained.
        /// </summary>
        Paused
    }
}
