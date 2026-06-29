using OpenCvSharp;
using System;
using System.Diagnostics;

namespace VirtualCamStudio.Media
{
    /// <summary>
    /// Controls media loading and management.
    /// Owns the currently loaded media item and notifies listeners when media changes.
    /// Acts as the central authority for media state without containing UI or OBS logic.
    /// </summary>
    public class MediaController
    {
        // ============================================
        // Fields
        // ============================================

        private readonly MediaLoader _mediaLoader = new();
        private MediaItem? _currentMedia;
        private Mat? _currentFrame;
        private readonly object _frameLock = new object();

        // ============================================
        // Events
        // ============================================

        /// <summary>
        /// Event raised when media is loaded successfully.
        /// Provides the loaded MediaItem and the first frame (for images or video).
        /// Subscribers should consume the frame data immediately or clone it.
        /// </summary>
        public event EventHandler<MediaLoadedEventArgs>? MediaLoaded;

        /// <summary>
        /// Event raised when media is unloaded.
        /// </summary>
        public event EventHandler? MediaUnloaded;

        // ============================================
        // Properties
        // ============================================

        /// <summary>
        /// Gets the currently loaded media item.
        /// Returns null if no media is loaded.
        /// </summary>
        public MediaItem? CurrentMedia => _currentMedia;

        /// <summary>
        /// Gets whether media is currently loaded.
        /// </summary>
        public bool HasMedia => _currentMedia != null;

        /// <summary>
        /// Gets whether the current media is an image.
        /// </summary>
        public bool IsImage => _currentMedia?.IsImage ?? false;

        /// <summary>
        /// Gets whether the current media is a video.
        /// </summary>
        public bool IsVideo => _currentMedia?.IsVideo ?? false;

        // ============================================
        // Public Methods
        // ============================================

        /// <summary>
        /// Loads a media file (image or video) and raises the MediaLoaded event.
        /// Automatically detects media type and loads metadata.
        /// For images: loads the image data.
        /// For videos: loads metadata only (playback handled separately).
        /// </summary>
        /// <param name="filePath">Full path to the media file</param>
        /// <returns>True if the media was loaded successfully, false otherwise</returns>
        public bool Load(string filePath)
        {
            try
            {
                Debug.WriteLine($"[MediaController] Loading media: {filePath}");

                // Unload any existing media first
                Unload();

                // Load media metadata using MediaLoader
                var mediaItem = _mediaLoader.Load(filePath);

                if (mediaItem == null)
                {
                    Debug.WriteLine($"[MediaController] Failed to load media: {filePath}");
                    return false;
                }

                _currentMedia = mediaItem;

                // For images, load the actual image data
                // For videos, we only have metadata (playback engine will handle frames)
                Mat? frame = null;

                if (mediaItem.IsImage)
                {
                    Debug.WriteLine($"[MediaController] Loading image data for: {mediaItem.FileName}");
                    frame = LoadImageData(filePath);

                    if (frame == null || frame.Empty())
                    {
                        Debug.WriteLine($"[MediaController] Failed to load image data.");
                        _currentMedia = null;
                        return false;
                    }

                    lock (_frameLock)
                    {
                        _currentFrame = frame;
                    }
                }
                else if (mediaItem.IsVideo)
                {
                    Debug.WriteLine($"[MediaController] Video metadata loaded: {mediaItem.FileName}");
                    // Video frames will be provided by PlaybackEngine
                    // We don't load a static frame here
                }

                // Notify subscribers
                Debug.WriteLine($"[MediaController] Media loaded successfully: {mediaItem}");
                MediaLoaded?.Invoke(this, new MediaLoadedEventArgs(mediaItem, frame));

                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[MediaController] Error loading media: {ex.Message}");
                Unload();
                return false;
            }
        }

        /// <summary>
        /// Unloads the current media and releases resources.
        /// Safe to call when no media is loaded.
        /// </summary>
        public void Unload()
        {
            try
            {
                if (_currentMedia != null)
                {
                    Debug.WriteLine($"[MediaController] Unloading media: {_currentMedia.FileName}");

                    _currentMedia = null;

                    lock (_frameLock)
                    {
                        // Dispose image frame if we own it
                        if (_currentFrame != null)
                        {
                            _currentFrame.Dispose();
                            _currentFrame = null;
                        }
                    }

                    // Notify subscribers
                    MediaUnloaded?.Invoke(this, EventArgs.Empty);

                    Debug.WriteLine("[MediaController] Media unloaded.");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[MediaController] Error unloading media: {ex.Message}");
            }
        }

        /// <summary>
        /// Gets the current frame for images and videos.
        /// Returns a clone of the internal frame to prevent cross-thread access issues.
        /// The caller is responsible for disposing the returned Mat.
        /// </summary>
        /// <returns>A clone of the current frame, or null if no media is loaded</returns>
        public Mat? GetCurrentFrame()
        {
            lock (_frameLock)
            {
                if (_currentFrame == null)
                    return null;

                try
                {
                    // Return a clone to prevent cross-thread access issues
                    // The render pipeline will dispose this clone after rendering
                    return _currentFrame.Clone();
                }
                catch
                {
                    // If cloning fails (e.g., frame was disposed during clone), return null
                    return null;
                }
            }
        }

        /// <summary>
        /// Updates the current frame for video playback.
        /// This should be called by PlaybackEngine when a new video frame is decoded.
        /// The MediaController takes ownership of the frame and will dispose the previous one.
        /// </summary>
        /// <param name="frame">The new video frame. Will be cloned internally.</param>
        public void UpdateVideoFrame(Mat frame)
        {
            if (frame == null || frame.Empty())
                return;

            lock (_frameLock)
            {
                // Dispose the previous frame
                if (_currentFrame != null)
                {
                    _currentFrame.Dispose();
                    _currentFrame = null;
                }

                try
                {
                    // Clone and store the new frame
                    _currentFrame = frame.Clone();
                }
                catch
                {
                    // If cloning fails, leave _currentFrame as null
                    _currentFrame = null;
                }
            }
        }

        // ============================================
        // Private Methods
        // ============================================

        /// <summary>
        /// Loads the actual image data from disk.
        /// Used for image files to get the pixel data for rendering.
        /// </summary>
        private Mat? LoadImageData(string filePath)
        {
            try
            {
                var imageProcessor = new ImageProcessor();
                Mat image = imageProcessor.Load(filePath);

                if (image == null || image.Empty())
                {
                    Debug.WriteLine($"[MediaController] Failed to load image data from: {filePath}");
                    return null;
                }

                Debug.WriteLine($"[MediaController] Image data loaded: {image.Width}x{image.Height}");
                return image;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[MediaController] Error loading image data: {ex.Message}");
                return null;
            }
        }
    }

    /// <summary>
    /// Event arguments for the MediaLoaded event.
    /// Contains the loaded media item and the first frame (if applicable).
    /// </summary>
    public class MediaLoadedEventArgs : EventArgs
    {
        /// <summary>
        /// The loaded media item with metadata.
        /// </summary>
        public MediaItem MediaItem { get; }

        /// <summary>
        /// The first frame of the media.
        /// For images: the loaded image.
        /// For videos: null (frames come from PlaybackEngine).
        /// This Mat is owned by MediaController - clone it if you need to keep it.
        /// </summary>
        public Mat? Frame { get; }

        /// <summary>
        /// Creates a new MediaLoadedEventArgs.
        /// </summary>
        public MediaLoadedEventArgs(MediaItem mediaItem, Mat? frame)
        {
            MediaItem = mediaItem ?? throw new ArgumentNullException(nameof(mediaItem));
            Frame = frame;
        }
    }
}
