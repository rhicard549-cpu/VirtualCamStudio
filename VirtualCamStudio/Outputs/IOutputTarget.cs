using VirtualCamStudio.Core;

namespace VirtualCamStudio.Outputs
{
    /// <summary>
    /// Represents an output target that can receive rendered frames.
    /// </summary>
    public interface IOutputTarget
    {
        /// <summary>
        /// Sends a rendered frame to this output target.
        /// </summary>
        /// <param name="frame">The rendered frame to send. Must not be modified or disposed by the target.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task SendFrameAsync(Frame frame);
    }
}
