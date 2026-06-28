namespace VirtualCamStudio.Core
{
    /// <summary>
    /// Represents an output target that can receive rendered frames.
    /// Implementations include preview display, virtual camera output, file export, etc.
    /// </summary>
    public interface IOutputTarget
    {
        /// <summary>
        /// Receives a rendered frame for output.
        /// 
        /// IMPORTANT: Implementations must consume frame data immediately.
        /// Do not store the Frame reference as it may be disposed after this call.
        /// Extract any needed data (copy Mat, convert to BitmapSource, etc.) within this method.
        /// </summary>
        /// <param name="frame">The frame to output. Do not store this reference.</param>
        void Receive(Frame frame);
    }
}
