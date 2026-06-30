using OpenCvSharp;
using System;
using System.Diagnostics;
using VirtualCamStudio.Core;
using VirtualCamStudio.Media;
using VirtualCamStudio.Models;
using VirtualCamStudio.Services.Rendering;

namespace VirtualCamStudio.Services
{
    /// <summary>
    /// Integrates RenderLoop with the existing rendering pipeline.
    /// Connects the pure scheduling service to MediaController, ViewportEngine, and OutputManager.
    /// 
    /// When RenderLoop fires FrameRequested:
    /// 1. Get the current media frame from MediaController
    /// 2. Apply ViewportEngine rendering with framing settings
    /// 3. Push the rendered frame to OutputManager
    /// 
    /// This makes the renderer the single source of rendered frames.
    /// No media is reloaded from disk during rendering.
    /// No rendering logic is duplicated.
    /// </summary>
    public class RenderPipeline : IDisposable
    {
        // ============================================
        // Fields
        // ============================================

        private readonly RenderLoop _renderLoop;
        private readonly MediaController _mediaController;
        private readonly ViewportEngine _viewportEngine;
        private readonly OutputManager _outputManager;
        private readonly Outputs.OutputManager? _newOutputManager;  // New Frame-based output system

        private FramingSettings _framingSettings = new();
        private CameraProfile? _activeProfile;
        private bool _disposed;

        // ============================================
        // Properties
        // ============================================

        /// <summary>
        /// Gets the render loop instance.
        /// </summary>
        public RenderLoop RenderLoop => _renderLoop;

        /// <summary>
        /// Gets or sets the active camera profile for canvas dimensions.
        /// </summary>
        public CameraProfile? ActiveProfile
        {
            get => _activeProfile;
            set => _activeProfile = value;
        }

        /// <summary>
        /// Gets the framing settings (zoom, pan, rotation).
        /// </summary>
        public FramingSettings FramingSettings => _framingSettings;

        // ============================================
        // Constructor
        // ============================================

        /// <summary>
        /// Creates a new render pipeline integration.
        /// </summary>
        /// <param name="renderLoop">The render loop for frame scheduling</param>
        /// <param name="mediaController">The media controller to get frames from</param>
        /// <param name="viewportEngine">The viewport engine for rendering</param>
        /// <param name="outputManager">The legacy output manager to push frames to</param>
        /// <param name="newOutputManager">Optional new Frame-based output manager</param>
        public RenderPipeline(
            RenderLoop renderLoop,
            MediaController mediaController,
            ViewportEngine viewportEngine,
            OutputManager outputManager,
            Outputs.OutputManager? newOutputManager = null)
        {
            _renderLoop = renderLoop ?? throw new ArgumentNullException(nameof(renderLoop));
            _mediaController = mediaController ?? throw new ArgumentNullException(nameof(mediaController));
            _viewportEngine = viewportEngine ?? throw new ArgumentNullException(nameof(viewportEngine));
            _outputManager = outputManager ?? throw new ArgumentNullException(nameof(outputManager));
            _newOutputManager = newOutputManager;

            // Subscribe to render loop events
            Debug.WriteLine("[RenderPipeline] Subscribing to RenderLoop.FrameRequested event...");
            _renderLoop.FrameRequested += OnFrameRequested;

            Debug.WriteLine($"[RenderPipeline] ✓ Initialized.");
            Debug.WriteLine($"[RenderPipeline]   - Legacy OutputManager: {_outputManager.TargetCount} targets");
            Debug.WriteLine($"[RenderPipeline]   - New OutputManager: {(_newOutputManager != null ? $"{_newOutputManager.OutputCount} targets" : "not configured")}");
        }

        // ============================================
        // Public Methods
        // ============================================

        /// <summary>
        /// Starts the render pipeline.
        /// </summary>
        public void Start()
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(RenderPipeline));

            Debug.WriteLine("[RenderPipeline] Starting...");
            _renderLoop.Start();
        }

        /// <summary>
        /// Stops the render pipeline.
        /// </summary>
        public void Stop()
        {
            Debug.WriteLine("[RenderPipeline] Stopping...");
            _renderLoop.Stop();
        }

        /// <summary>
        /// Pauses the render pipeline.
        /// </summary>
        public void Pause()
        {
            Debug.WriteLine("[RenderPipeline] Pausing...");
            _renderLoop.Pause();
        }

        /// <summary>
        /// Resumes the render pipeline.
        /// </summary>
        public void Resume()
        {
            Debug.WriteLine("[RenderPipeline] Resuming...");
            _renderLoop.Resume();
        }

        /// <summary>
        /// Sets the framing settings (zoom, pan, rotation).
        /// </summary>
        public void SetFraming(FramingSettings framing)
        {
            _framingSettings = framing ?? throw new ArgumentNullException(nameof(framing));
        }

        /// <summary>
        /// Updates individual framing properties.
        /// </summary>
        public void SetZoom(double zoom)
        {
            _framingSettings.Zoom = zoom;
        }

        public void SetOffset(double x, double y)
        {
            _framingSettings.OffsetX = x;
            _framingSettings.OffsetY = y;
        }

        public void SetRotation(double rotation)
        {
            _framingSettings.Rotation = rotation;
        }

        public void ResetFraming()
        {
            _framingSettings.Zoom = 1.0;
            _framingSettings.OffsetX = 0;
            _framingSettings.OffsetY = 0;
            _framingSettings.Rotation = 0;
            _framingSettings.MirrorHorizontal = false;
            _framingSettings.MirrorVertical = false;
        }

        // ============================================
        // Private Methods
        // ============================================

        /// <summary>
        /// Handles the FrameRequested event from RenderLoop.
        /// Gets the current media frame, applies ViewportEngine, and pushes to OutputManager.
        /// </summary>
        private async void OnFrameRequested(object? sender, EventArgs e)
        {
            Debug.WriteLine("[2] RenderPipeline entered");

            Mat? sourceFrame = null;

            try
            {
                // Only render if we have media loaded
                if (!_mediaController.HasMedia)
                {
                    Debug.WriteLine("[2] RenderPipeline - SKIPPED: No media loaded");
                    Debug.WriteLine("[RenderPipeline] ❌ Fallback reason: No media loaded");
                    return;  // Silent - no spam when no media
                }

                // Get the current frame from MediaController (returns a clone)
                sourceFrame = _mediaController.GetCurrentFrame();

                Debug.WriteLine($"[3] MediaController returned frame - null: {sourceFrame == null}, width: {sourceFrame?.Width ?? 0}, height: {sourceFrame?.Height ?? 0}");

                if (sourceFrame == null || sourceFrame.Empty())
                {
                    Debug.WriteLine("[RenderPipeline.OnFrameRequested] ⚠️ Source frame is null/empty");
                    Debug.WriteLine("[RenderPipeline] ❌ Fallback reason: MediaController returned null/empty frame");
                    return;
                }

                Debug.WriteLine($"[RenderPipeline.OnFrameRequested] Source frame: {sourceFrame.Width}x{sourceFrame.Height}");

                // Get canvas dimensions from active profile
                int canvasWidth = _activeProfile?.DisplayWidth ?? 1080;
                int canvasHeight = _activeProfile?.DisplayHeight ?? 1920;

                if (_activeProfile == null)
                {
                    Debug.WriteLine("[RenderPipeline.OnFrameRequested] ⚠️ No active profile - using defaults");
                    Debug.WriteLine("[RenderPipeline] ⚠️ Note: Using default canvas dimensions (no active profile)");
                }

                Debug.WriteLine($"[RenderPipeline.OnFrameRequested] Rendering to canvas: {canvasWidth}x{canvasHeight}");

                // Render through ViewportEngine (single source of rendering logic)
                Frame renderedFrame = _viewportEngine.Render(
                    sourceFrame,
                    canvasWidth,
                    canvasHeight,
                    _framingSettings);

                Debug.WriteLine($"[4] Viewport render finished - width: {renderedFrame.Width}, height: {renderedFrame.Height}");
                Debug.WriteLine($"[RenderPipeline.OnFrameRequested] ✓ Frame rendered");

                // DIAGNOSTIC: Check pixel data in rendered frame
                if (renderedFrame != null && renderedFrame.IsValid && renderedFrame.Image != null && !renderedFrame.Image.Empty())
                {
                    unsafe
                    {
                        byte* ptr = (byte*)renderedFrame.Image.DataPointer;
                        int channels = renderedFrame.Image.Channels();
                        Debug.WriteLine($"[RenderPipeline] 🔍 PIXEL CHECK: First pixel = ({ptr[0]},{ptr[1]},{ptr[2]},{(channels > 3 ? ptr[3] : 255)}), Channels: {channels}");

                        // Check center pixel too
                        int centerOffset = ((renderedFrame.Height / 2) * renderedFrame.Width + (renderedFrame.Width / 2)) * channels;
                        byte* centerPtr = ptr + centerOffset;
                        Debug.WriteLine($"[RenderPipeline] 🔍 PIXEL CHECK: Center pixel = ({centerPtr[0]},{centerPtr[1]},{centerPtr[2]},{(channels > 3 ? centerPtr[3] : 255)})");
                    }
                }

                // DEBUG: Save rendered frame to disk for inspection
                try
                {
                    string debugPath = @"C:\Temp\render_debug.png";
                    System.IO.Directory.CreateDirectory(@"C:\Temp");

                    if (renderedFrame != null && renderedFrame.IsValid && renderedFrame.Image != null && !renderedFrame.Image.Empty())
                    {
                        Cv2.ImWrite(debugPath, renderedFrame.Image);
                        Debug.WriteLine($"[RenderPipeline] 🔍 DEBUG: Saved rendered frame to {debugPath}");
                        Debug.WriteLine($"[RenderPipeline] 🔍 Frame info: {renderedFrame.Width}x{renderedFrame.Height}, channels: {renderedFrame.Image.Channels()}");
                    }
                    else
                    {
                        Debug.WriteLine($"[RenderPipeline] ❌ DEBUG: Cannot save - rendered frame is invalid or empty");
                    }
                }
                catch (Exception debugEx)
                {
                    Debug.WriteLine($"[RenderPipeline] ❌ DEBUG: Failed to save debug frame: {debugEx.Message}");
                }

                // Push to legacy output manager (preview, virtual camera, OBS, etc.)
                Debug.WriteLine($"[5] Legacy OutputManager - about to push frame ({_outputManager.TargetCount} targets)");
                Debug.WriteLine($"[RenderPipeline.OnFrameRequested] Pushing to legacy OutputManager ({_outputManager.TargetCount} targets)...");
                _outputManager.PushFrame(renderedFrame);

                // Push to new output manager if available (MUST await before disposing frame!)
                if (_newOutputManager != null)
                {
                    Debug.WriteLine($"[6] New OutputManager - about to send frame ({_newOutputManager.OutputCount} outputs)");
                    Debug.WriteLine($"[RenderPipeline.OnFrameRequested] Sending to new OutputManager ({_newOutputManager.OutputCount} outputs)...");
                    await _newOutputManager.SendFrameAsync(renderedFrame);
                    Debug.WriteLine($"[RenderPipeline.OnFrameRequested] ✓ New OutputManager completed");
                }
                else
                {
                    Debug.WriteLine($"[6] New OutputManager - SKIPPED: Not configured");
                }

                // Clean up rendered frame (AFTER all async operations complete)
                renderedFrame.Dispose();
                Debug.WriteLine($"[RenderPipeline.OnFrameRequested] ✓ Frame disposed");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[RenderPipeline.OnFrameRequested] ❌ ERROR: {ex.Message}\n{ex.StackTrace}");
            }
            finally
            {
                // Always dispose the source frame clone
                sourceFrame?.Dispose();
            }
        }

        // ============================================
        // IDisposable
        // ============================================

        /// <summary>
        /// Disposes the render pipeline and releases resources.
        /// </summary>
        public void Dispose()
        {
            if (!_disposed)
            {
                _renderLoop.FrameRequested -= OnFrameRequested;
                _renderLoop.Dispose();
                _disposed = true;
                Debug.WriteLine("[RenderPipeline] Disposed.");
            }
        }
    }
}
