using OpenCvSharp;

namespace VirtualCamStudio.Models
{
    public class Frame
    {
        public Mat Image { get; set; } = new();

        public DateTime Timestamp { get; set; } = DateTime.Now;
    }
}