using System;

namespace VirtualCamStudio.Media
{
    /// <summary>
    /// Represents a loaded media file with its metadata.
    /// Unified model for both images and videos.
    /// Contains dimensions, duration, and type information.
    /// </summary>
    public class MediaItem
    {
        /// <summary>
        /// Full file path to the media file.
        /// </summary>
        public string FilePath { get; set; } = string.Empty;

        /// <summary>
        /// Filename without path (e.g., "photo.jpg", "video.mp4").
        /// </summary>
        public string FileName { get; set; } = string.Empty;

        /// <summary>
        /// Type of media (Image or Video).
        /// </summary>
        public MediaType MediaType { get; set; }

        /// <summary>
        /// Width of the media in pixels.
        /// For images: original image width.
        /// For videos: video frame width.
        /// </summary>
        public int Width { get; set; }

        /// <summary>
        /// Height of the media in pixels.
        /// For images: original image height.
        /// For videos: video frame height.
        /// </summary>
        public int Height { get; set; }

        /// <summary>
        /// Duration of the media.
        /// For images: null (no duration concept).
        /// For videos: total video duration.
        /// </summary>
        public TimeSpan? Duration { get; set; }

        /// <summary>
        /// Frames per second.
        /// For images: null (not applicable).
        /// For videos: video FPS.
        /// </summary>
        public double? FPS { get; set; }

        /// <summary>
        /// Gets whether this media item is a video.
        /// </summary>
        public bool IsVideo => MediaType == MediaType.Video;

        /// <summary>
        /// Gets whether this media item is an image.
        /// </summary>
        public bool IsImage => MediaType == MediaType.Image;

        /// <summary>
        /// Returns a string representation of the media item.
        /// </summary>
        public override string ToString()
        {
            if (MediaType == MediaType.Image)
            {
                return $"Image: {FileName} ({Width}x{Height})";
            }
            else
            {
                return $"Video: {FileName} ({Width}x{Height}, {Duration}, {FPS:F2} FPS)";
            }
        }
    }
}
