using System.Diagnostics;
using VirtualCamStudio.Core;
using System.Collections.Concurrent;

namespace VirtualCamStudio.Outputs
{
    /// <summary>
    /// Centralized manager responsible for distributing rendered frames to registered output plugins.
    /// Supports plugin registration/unregistration at runtime.
    /// Thread-safe. Does not modify frames or perform rendering.
    /// </summary>
    public class OutputManager
    {
        private readonly ConcurrentBag<IOutputTarget> _outputs = new();
        private readonly object _lock = new();

        /// <summary>
        /// Registers an output plugin to receive frames.
        /// </summary>
        /// <param name="output">The output plugin to register.</param>
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
        /// Unregisters an output plugin so it no longer receives frames.
        /// </summary>
        /// <param name="output">The output plugin to unregister.</param>
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
        /// Broadcasts a rendered frame to all registered output plugins.
        /// Does not modify or dispose the frame. Each plugin receives the frame concurrently.
        /// </summary>
        /// <param name="frame">The rendered frame to broadcast. Must not be null or invalid.</param>
        /// <returns>A task that completes when all plugins have processed the frame.</returns>
        public async Task SendFrameAsync(Frame frame)
        {

            if (frame == null || !frame.IsValid)
            {
                return;
            }

            IOutputTarget[] currentOutputs;
            lock (_lock)
            {
                currentOutputs = _outputs.ToArray();
            }

            if (currentOutputs.Length == 0)
            {
                return;
            }

            System.Diagnostics.Debug.WriteLine($"[Outputs.OutputManager.SendFrameAsync] Broadcasting to {currentOutputs.Length} output plugin(s)...");

            // Broadcast to all plugins concurrently
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
                    // Log error but continue broadcasting to other plugins
                    System.Diagnostics.Debug.WriteLine($"[Outputs.OutputManager.SendFrameAsync] ❌ {output.GetType().Name} error: {ex.Message}");
                }
            });

            await Task.WhenAll(tasks);
        }

        /// <summary>
        /// Gets the number of currently registered output plugins.
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

        /// <summary>
        /// Gets a snapshot of all currently registered output plugins.
        /// </summary>
        /// <returns>An array of registered output plugins.</returns>
        public IOutputTarget[] GetRegisteredOutputs()
        {
            lock (_lock)
            {
                return _outputs.ToArray();
            }
        }

        /// <summary>
        /// Checks if a specific output plugin is registered.
        /// </summary>
        /// <param name="output">The output plugin to check.</param>
        /// <returns>True if the plugin is registered, false otherwise.</returns>
        public bool IsRegistered(IOutputTarget output)
        {
            if (output == null)
                return false;

            lock (_lock)
            {
                return _outputs.Contains(output);
            }
        }

        /// <summary>
        /// Checks if any output plugin of the specified type is registered.
        /// </summary>
        /// <typeparam name="T">The output plugin type to check for.</typeparam>
        /// <returns>True if at least one plugin of the specified type is registered.</returns>
        public bool IsRegistered<T>() where T : IOutputTarget
        {
            lock (_lock)
            {
                return _outputs.Any(o => o is T);
            }
        }

        /// <summary>
        /// Gets all registered output plugins of a specific type.
        /// </summary>
        /// <typeparam name="T">The output plugin type to get.</typeparam>
        /// <returns>An array of registered plugins of the specified type.</returns>
        public T[] GetOutputs<T>() where T : IOutputTarget
        {
            lock (_lock)
            {
                return _outputs.OfType<T>().ToArray();
            }
        }
    }
}
