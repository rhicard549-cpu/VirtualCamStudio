using System.Windows.Media.Imaging;

namespace VirtualCamStudio.Models
{
    /// <summary>
    /// Represents a media item in the library.
    /// Contains file information and thumbnail for display.
    /// Designed to support both images and videos (future).
    /// </summary>
    public class MediaItem
    {
        /// <summary>
        /// Full file path to the media file.
        /// </summary>
        public string FilePath { get; set; } = string.Empty;

        /// <summary>
        /// Filename without path (e.g., "photo.jpg").
        /// </summary>
        public string FileName { get; set; } = string.Empty;

        /// <summary>
        /// File type/extension (e.g., "JPG", "PNG", "MP4").
        /// </summary>
        public string FileType { get; set; } = string.Empty;

        /// <summary>
        /// Thumbnail image for display in the media library.
        /// Stored in memory for smooth scrolling without disk I/O.
        /// For images: decoded to 150px width.
        /// For videos (future): will be extracted from first frame.
        /// </summary>
        public BitmapSource? Thumbnail { get; set; }
    }
}
