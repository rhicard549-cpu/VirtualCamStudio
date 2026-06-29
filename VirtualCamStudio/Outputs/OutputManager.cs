using System.Diagnostics;
using VirtualCamStudio.Core;
using System.Collections.Concurrent;

namespace VirtualCamStudio.Outputs
{
    /// <summary>
    /// Centralized manager responsible for distributing rendered frames to registered output targets.
    /// Thread-safe. Does not modify frames or perform rendering.
    /// </summary>
    public class OutputManager
    {
        private readonly ConcurrentBag<IOutputTarget> _outputs = new();
        private readonly object _lock = new();

        /// <summary>
        /// Registers an output target to receive frames.
        /// </summary>
        /// <param name="output">The output target to register.</param>
        public void Register(IOutputTarget output)
        {
            if (output == null)
                throw new ArgumentNullException(nameof(output));

            lock (_lock)
            {
                _outputs.Add(output);
                System.Diagnostics.Debug.WriteLine($"[Outputs.OutputManager.Register] ✓ Registered {output.GetType().Name}, total outputs: {_outputs.Count}");
            }
        }

        /// <summary>
        /// Unregisters an output target so it no longer receives frames.
        /// </summary>
        /// <param name="output">The output target to unregister.</param>
        public void Unregister(IOutputTarget output)
        {
            if (output == null)
                throw new ArgumentNullException(nameof(output));

            lock (_lock)
            {
                // ConcurrentBag doesn't support direct removal, so rebuild without the target
                var remainingOutputs = _outputs.Where(o => o != output).ToList();
                _outputs.Clear();
                foreach (var o in remainingOutputs)
                {
                    _outputs.Add(o);
                }
            }
        }

        /// <summary>
        /// Broadcasts a rendered frame to all registered output targets.
        /// Does not modify or dispose the frame. Each target receives the frame concurrently.
        /// </summary>
        /// <param name="frame">The rendered frame to broadcast. Must not be null or invalid.</param>
        /// <returns>A task that completes when all targets have processed the frame.</returns>
        public async Task SendFrameAsync(Frame frame)
        {
            Debug.WriteLine($"[7] OutputManager - registered outputs: {_outputs.Count}");

            if (frame == null || !frame.IsValid)
            {
                System.Diagnostics.Debug.WriteLine($"[Outputs.OutputManager.SendFrameAsync] ⚠️ Invalid frame");
                return;
            }

            IOutputTarget[] currentOutputs;
            lock (_lock)
            {
                currentOutputs = _outputs.ToArray();
            }

            if (currentOutputs.Length == 0)
            {
                System.Diagnostics.Debug.WriteLine($"[Outputs.OutputManager.SendFrameAsync] ⚠️ No outputs registered!");
                return;
            }

            System.Diagnostics.Debug.WriteLine($"[Outputs.OutputManager.SendFrameAsync] Broadcasting to {currentOutputs.Length} output(s)...");

            // Broadcast to all targets concurrently
            var tasks = currentOutputs.Select(async output =>
            {
                try
                {
                    System.Diagnostics.Debug.WriteLine($"[Outputs.OutputManager.SendFrameAsync] → Sending to {output.GetType().Name}");
                    await output.SendFrameAsync(frame);
                    System.Diagnostics.Debug.WriteLine($"[Outputs.OutputManager.SendFrameAsync] ✓ {output.GetType().Name} completed");
                }
                catch (Exception ex)
                {
                    // Log error but continue broadcasting to other targets
                    System.Diagnostics.Debug.WriteLine($"[Outputs.OutputManager.SendFrameAsync] ❌ {output.GetType().Name} error: {ex.Message}");
                }
            });

            await Task.WhenAll(tasks);
        }

        /// <summary>
        /// Gets the number of currently registered output targets.
        /// </summary>
        public int OutputCount
        {
            get
            {
                lock (_lock)
                {
                    return _outputs.Count;
                }
            }
        }
    }
}
