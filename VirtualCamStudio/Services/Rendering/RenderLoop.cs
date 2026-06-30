using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace VirtualCamStudio.Services.Rendering
{
    /// <summary>
    /// Dedicated render loop service responsible for scheduling frame rendering.
    /// Pure timing/scheduling service with no knowledge of images, videos, OBS, or WPF.
    /// Runs on a background task and raises FrameRequested events at the target FPS.
    /// </summary>
    public class RenderLoop : IDisposable
    {
        // ============================================
        // Fields
        // ============================================

        private readonly object _lock = new();
        private readonly Stopwatch _stopwatch = new();
        private CancellationTokenSource? _cancellationTokenSource;
        private Task? _renderTask;

        private int _targetFps = 60;  // Increased from 30 to 60 for smoother updates
        private bool _isRunning;
        private bool _isPaused;
        private bool _disposed;

        // FPS measurement
        private int _frameCount;
        private double _actualFps;
        private long _lastFpsUpdateTicks;

        // ============================================
        // Events
        // ============================================

        /// <summary>
        /// Event raised when a frame should be rendered.
        /// Subscribers should handle frame rendering logic.
        /// </summary>
        public event EventHandler? FrameRequested;

        // ============================================
        // Properties
        // ============================================

        /// <summary>
        /// Gets whether the render loop is currently running.
        /// </summary>
        public bool IsRunning
        {
            get
            {
                lock (_lock)
                {
                    return _isRunning;
                }
            }
        }

        /// <summary>
        /// Gets or sets the target frames per second.
        /// Default is 30 FPS.
        /// </summary>
        public int TargetFPS
        {
            get
            {
                lock (_lock)
                {
                    return _targetFps;
                }
            }
            set
            {
                if (value <= 0)
                    throw new ArgumentOutOfRangeException(nameof(value), "Target FPS must be greater than 0.");

                lock (_lock)
                {
                    _targetFps = value;
                }
            }
        }

        /// <summary>
        /// Gets the actual measured FPS.
        /// Updated approximately once per second.
        /// </summary>
        public double ActualFPS
        {
            get
            {
                lock (_lock)
                {
                    return _actualFps;
                }
            }
        }

        // ============================================
        // Constructor
        // ============================================

        /// <summary>
        /// Creates a new render loop with default 30 FPS target.
        /// </summary>
        public RenderLoop()
        {
        }

        // ============================================
        // Public Methods
        // ============================================

        /// <summary>
        /// Starts the render loop on a background task.
        /// Thread-safe operation.
        /// </summary>
        public void Start()
        {
            lock (_lock)
            {
                if (_disposed)
                    throw new ObjectDisposedException(nameof(RenderLoop));

                if (_isRunning)
                {
                    return;
                }

                // Check if any subscribers exist
                int subscriberCount = FrameRequested?.GetInvocationList()?.Length ?? 0;

                if (subscriberCount == 0)
                {
                }

                _isRunning = true;
                _isPaused = false;
                _frameCount = 0;
                _actualFps = 0;
                _lastFpsUpdateTicks = 0;

                _cancellationTokenSource = new CancellationTokenSource();
                _renderTask = Task.Run(() => RenderLoopTask(_cancellationTokenSource.Token));
            }
        }

        /// <summary>
        /// Stops the render loop.
        /// Thread-safe operation.
        /// </summary>
        public void Stop()
        {
            lock (_lock)
            {
                if (!_isRunning)
                {
                    return;
                }

                _isRunning = false;
                _isPaused = false;

                _cancellationTokenSource?.Cancel();
            }

            // Wait for the task to complete (outside the lock)
            try
            {
                _renderTask?.Wait(TimeSpan.FromSeconds(2));
            }
            catch (Exception ex)
            {
            }

            lock (_lock)
            {
                _cancellationTokenSource?.Dispose();
                _cancellationTokenSource = null;
                _renderTask = null;
            }
        }

        /// <summary>
        /// Pauses the render loop without stopping it.
        /// Thread-safe operation.
        /// </summary>
        public void Pause()
        {
            lock (_lock)
            {
                if (!_isRunning)
                {
                    return;
                }

                if (_isPaused)
                {
                    return;
                }
                _isPaused = true;
            }
        }

        /// <summary>
        /// Resumes the render loop after pausing.
        /// Thread-safe operation.
        /// </summary>
        public void Resume()
        {
            lock (_lock)
            {
                if (!_isRunning)
                {
                    return;
                }

                if (!_isPaused)
                {
                    return;
                }
                _isPaused = false;
            }
        }

        // ============================================
        // Private Methods
        // ============================================

        /// <summary>
        /// Background render loop task.
        /// Uses Stopwatch for precise timing.
        /// </summary>
        private void RenderLoopTask(CancellationToken cancellationToken)
        {
            _stopwatch.Restart();

            long lastFrameTicks = 0;
            int frameCounter = 0;

            try
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    bool isPaused;
                    int targetFps;

                    lock (_lock)
                    {
                        isPaused = _isPaused;
                        targetFps = _targetFps;
                    }

                    // If paused, sleep and continue
                    if (isPaused)
                    {
                        Thread.Sleep(8); // ~120Hz check rate for faster resume
                        continue;
                    }

                    // Calculate target frame interval in ticks
                    long targetIntervalTicks = Stopwatch.Frequency / targetFps;
                    long currentTicks = _stopwatch.ElapsedTicks;
                    long elapsedSinceLastFrame = currentTicks - lastFrameTicks;

                    // If enough time has passed, request a frame
                    if (elapsedSinceLastFrame >= targetIntervalTicks)
                    {
                        lastFrameTicks = currentTicks;

                        frameCounter++;

                        // Raise frame requested event
                        RaiseFrameRequested();

                        // Update FPS measurement
                        UpdateFpsMeasurement(currentTicks);
                    }
                    else
                    {
                        // Calculate how long to sleep
                        long remainingTicks = targetIntervalTicks - elapsedSinceLastFrame;
                        int sleepMs = (int)(remainingTicks * 1000 / Stopwatch.Frequency);

                        if (sleepMs > 0)
                        {
                            Thread.Sleep(Math.Min(sleepMs, 8)); // Cap sleep at 8ms for faster responsiveness
                        }
                    }
                }
            }
            catch (Exception ex)
            {
            }
            finally
            {
                _stopwatch.Stop();
            }
        }

        /// <summary>
        /// Raises the FrameRequested event.
        /// </summary>
        private void RaiseFrameRequested()
        {
            try
            {
                var handler = FrameRequested;
                if (handler == null)
                {
                    return;
                }

                handler.Invoke(this, EventArgs.Empty);
            }
            catch (Exception ex)
            {
            }
        }

        /// <summary>
        /// Updates the actual FPS measurement.
        /// Calculates FPS approximately once per second.
        /// </summary>
        private void UpdateFpsMeasurement(long currentTicks)
        {
            _frameCount++;

            // Update FPS approximately once per second
            if (_lastFpsUpdateTicks == 0)
            {
                _lastFpsUpdateTicks = currentTicks;
                return;
            }

            long elapsedTicks = currentTicks - _lastFpsUpdateTicks;
            double elapsedSeconds = (double)elapsedTicks / Stopwatch.Frequency;

            if (elapsedSeconds >= 1.0)
            {
                lock (_lock)
                {
                    _actualFps = _frameCount / elapsedSeconds;
                }

                _frameCount = 0;
                _lastFpsUpdateTicks = currentTicks;
            }
        }

        // ============================================
        // IDisposable
        // ============================================

        /// <summary>
        /// Disposes the render loop and releases resources.
        /// </summary>
        public void Dispose()
        {
            if (!_disposed)
            {
                Stop();
                _disposed = true;
            }
        }
    }
}
