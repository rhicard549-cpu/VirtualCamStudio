namespace VirtualCamStudio.Media
{
    public class CanvasSettings
    {
        public int Width { get; set; } = 1080;
        public int Height { get; set; } = 1920;

        public static CanvasSettings Portrait1080 =>
            new CanvasSettings
            {
                Width = 1080,
                Height = 1920
            };
    }
}