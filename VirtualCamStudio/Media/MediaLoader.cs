using OpenCvSharp;
using System;
using System.Diagnostics;
using System.IO;

namespace VirtualCamStudio.Media
{
    /// <summary>
    /// Unified media loader for images and videos.
    /// Automatically detects media type by file extension and loads metadata.
    /// Uses ImageProcessor for images and VideoPlayer for videos.
    /// </summary>
    public class MediaLoader
    {
        // ============================================
        // Fields
        // ============================================

        private readonly ImageProcessor _imageProcessor = new();
        private readonly VideoPlayer _videoPlayer = new();

        // ============================================
        // Supported Extensions
        // ============================================

        private static readonly string[] ImageExtensions = { ".jpg", ".jpeg", ".png", ".bmp" };
        private static readonly string[] VideoExtensions = { ".mp4", ".mov", ".avi", ".mkv" };

        // ============================================
        // Public Methods
        // ============================================

        /// <summary>
        /// Loads a media file and returns its metadata.
        /// Automatically detects whether the file is an image or video based on extension.
        /// For images: loads using ImageProcessor and extracts dimensions.
        /// For videos: opens using VideoPlayer and extracts dimensions, duration, and FPS.
        /// </summary>
        /// <param name="filePath">Full path to the media file</param>
        /// <returns>MediaItem with populated metadata, or null if loading failed</returns>
        public MediaItem? Load(string filePath)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(filePath))
                {
                    return null;
                }

                if (!File.Exists(filePath))
                {
                    return null;
                }

                string extension = Path.GetExtension(filePath).ToLowerInvariant();
                string fileName = Path.GetFileName(filePath);

                Debug.WriteLine($"[MediaLoader] Loading media: {fileName} (Extension: {extension})");

                // Determine media type and load accordingly
                if (IsImageExtension(extension))
                {
                    return LoadImage(filePath, fileName);
                }
                else if (IsVideoExtension(extension))
                {
                    return LoadVideo(filePath, fileName);
                }
                else
                {
                    return null;
                }
            }
            catch (Exception ex)
            {
                return null;
            }
        }

        /// <summary>
        /// Checks if a file extension is supported by the media loader.
        /// </summary>
        /// <param name="filePath">File path to check</param>
        /// <returns>True if the file extension is supported, false otherwise</returns>
        public bool IsSupportedFile(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath))
                return false;

            string extension = Path.GetExtension(filePath).ToLowerInvariant();
            return IsImageExtension(extension) || IsVideoExtension(extension);
        }

        /// <summary>
        /// Gets the media type for a given file path.
        /// </summary>
        /// <param name="filePath">File path to check</param>
        /// <returns>MediaType, or null if not supported</returns>
        public MediaType? GetMediaType(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath))
                return null;

            string extension = Path.GetExtension(filePath).ToLowerInvariant();

            if (IsImageExtension(extension))
                return MediaType.Image;
            else if (IsVideoExtension(extension))
                return MediaType.Video;
            else
                return null;
        }

        // ============================================
        // Private Methods
        // ============================================

        /// <summary>
        /// Loads an image file and extracts its metadata.
        /// </summary>
        private MediaItem? LoadImage(string filePath, string fileName)
        {
            try
            {

                // Load the image using ImageProcessor
                Mat image = _imageProcessor.Load(filePath);

                if (image == null || image.Empty())
                {
                    return null;
                }

                // Extract metadata
                var mediaItem = new MediaItem
                {
                    FilePath = filePath,
                    FileName = fileName,
                    MediaType = MediaType.Image,
                    Width = image.Width,
                    Height = image.Height,
                    Duration = null,  // Images don't have duration
                    FPS = null        // Images don't have FPS
                };

                // Dispose the image - we only needed metadata
                image.Dispose();
                return mediaItem;
            }
            catch (Exception ex)
            {
                return null;
            }
        }

        /// <summary>
        /// Loads a video file and extracts its metadata.
        /// </summary>
        private MediaItem? LoadVideo(string filePath, string fileName)
        {
            try
            {

                // Open the video using VideoPlayer
                bool opened = _videoPlayer.Open(filePath);

                if (!opened || !_videoPlayer.IsOpened)
                {
                    return null;
                }

                // Extract metadata
                var mediaItem = new MediaItem
                {
                    FilePath = filePath,
                    FileName = fileName,
                    MediaType = MediaType.Video,
                    Width = _videoPlayer.Width,
                    Height = _videoPlayer.Height,
                    Duration = _videoPlayer.Duration,
                    FPS = _videoPlayer.FPS
                };

                // Close the video - we only needed metadata
                _videoPlayer.Close();
                return mediaItem;
            }
            catch (Exception ex)
            {
                return null;
            }
        }

        /// <summary>
        /// Checks if the extension is a supported image format.
        /// </summary>
        private static bool IsImageExtension(string extension)
        {
            foreach (var imageExt in ImageExtensions)
            {
                if (extension.Equals(imageExt, StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            return false;
        }

        /// <summary>
        /// Checks if the extension is a supported video format.
        /// </summary>
        private static bool IsVideoExtension(string extension)
        {
            foreach (var videoExt in VideoExtensions)
            {
                if (extension.Equals(videoExt, StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            return false;
        }
    }
}
