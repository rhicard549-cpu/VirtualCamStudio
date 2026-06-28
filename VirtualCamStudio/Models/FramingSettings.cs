namespace VirtualCamStudio.Models
{
    public class FramingSettings
    {
        public double Zoom { get; set; } = 1.0;

        public double OffsetX { get; set; } = 0;

        public double OffsetY { get; set; } = 0;

        public double Rotation { get; set; } = 0;

        public bool MirrorHorizontal { get; set; }

        public bool MirrorVertical { get; set; }
    }
}