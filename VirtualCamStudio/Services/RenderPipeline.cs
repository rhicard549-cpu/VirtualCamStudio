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
        /// <param name="outputManager">The output manager to push frames to</param>
        public RenderPipeline(
            RenderLoop renderLoop,
            MediaController mediaController,
            ViewportEngine viewportEngine,
            OutputManager outputManager)
        {
            _renderLoop = renderLoop ?? throw new ArgumentNullException(nameof(renderLoop));
            _mediaController = mediaController ?? throw new ArgumentNullException(nameof(mediaController));
            _viewportEngine = viewportEngine ?? throw new ArgumentNullException(nameof(viewportEngine));
            _outputManager = outputManager ?? throw new ArgumentNullException(nameof(outputManager));

            // Subscribe to render loop events
            _renderLoop.FrameRequested += OnFrameRequested;

            Debug.WriteLine("[RenderPipeline] Initialized.");
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
        private void OnFrameRequested(object? sender, EventArgs e)
        {
            try
            {
                // Only render if we have media loaded
                if (!_mediaController.HasMedia)
                {
                    return;
                }

                // Get the current frame from MediaController (no disk reload)
                Mat? sourceFrame = _mediaController.GetCurrentFrame();

                if (sourceFrame == null || sourceFrame.Empty())
                {
                    return;
                }

                // Get canvas dimensions from active profile
                int canvasWidth = _activeProfile?.DisplayWidth ?? 1080;
                int canvasHeight = _activeProfile?.DisplayHeight ?? 1920;

                // Render through ViewportEngine (single source of rendering logic)
                Frame renderedFrame = _viewportEngine.Render(
                    sourceFrame,
                    canvasWidth,
                    canvasHeight,
                    _framingSettings);

                // Push to all output targets (preview, virtual camera, OBS, etc.)
                _outputManager.PushFrame(renderedFrame);

                // Clean up rendered frame
                renderedFrame.Dispose();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[RenderPipeline] Error rendering frame: {ex.Message}");
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
