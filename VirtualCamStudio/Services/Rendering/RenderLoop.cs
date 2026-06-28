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

        private int _targetFps = 30;
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
                    Debug.WriteLine($"[RenderLoop] Target FPS set to: {value}");
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
            Debug.WriteLine("[RenderLoop] Initialized with 30 FPS target.");
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
                    Debug.WriteLine("[RenderLoop] Already running.");
                    return;
                }

                Debug.WriteLine("[RenderLoop] Starting render loop...");

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
                    Debug.WriteLine("[RenderLoop] Not running.");
                    return;
                }

                Debug.WriteLine("[RenderLoop] Stopping render loop...");

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
                Debug.WriteLine($"[RenderLoop] Error waiting for task: {ex.Message}");
            }

            lock (_lock)
            {
                _cancellationTokenSource?.Dispose();
                _cancellationTokenSource = null;
                _renderTask = null;

                Debug.WriteLine($"[RenderLoop] Stopped. Final measured FPS: {_actualFps:F2}");
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
                    Debug.WriteLine("[RenderLoop] Cannot pause - not running.");
                    return;
                }

                if (_isPaused)
                {
                    Debug.WriteLine("[RenderLoop] Already paused.");
                    return;
                }

                Debug.WriteLine("[RenderLoop] Pausing render loop...");
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
                    Debug.WriteLine("[RenderLoop] Cannot resume - not running.");
                    return;
                }

                if (!_isPaused)
                {
                    Debug.WriteLine("[RenderLoop] Not paused.");
                    return;
                }

                Debug.WriteLine("[RenderLoop] Resuming render loop...");
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
            Debug.WriteLine("[RenderLoop] Background task started.");
            _stopwatch.Restart();

            long lastFrameTicks = 0;

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
                        Thread.Sleep(16); // ~60Hz check rate
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
                            Thread.Sleep(Math.Min(sleepMs, 16)); // Cap sleep at 16ms for responsiveness
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[RenderLoop] Error in background task: {ex.Message}");
            }
            finally
            {
                _stopwatch.Stop();
                Debug.WriteLine("[RenderLoop] Background task stopped.");
            }
        }

        /// <summary>
        /// Raises the FrameRequested event.
        /// </summary>
        private void RaiseFrameRequested()
        {
            try
            {
                FrameRequested?.Invoke(this, EventArgs.Empty);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[RenderLoop] Error in FrameRequested handler: {ex.Message}");
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

                Debug.WriteLine($"[RenderLoop] Actual FPS: {_actualFps:F2}");
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
                Debug.WriteLine("[RenderLoop] Disposed.");
            }
        }
    }
}
