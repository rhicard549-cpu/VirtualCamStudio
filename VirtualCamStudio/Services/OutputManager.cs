using System;
using System.Collections.Generic;
using VirtualCamStudio.Core;

namespace VirtualCamStudio.Services
{
    /// <summary>
    /// Manages frame distribution to multiple output targets.
    /// Provides thread-safe registration/unregistration and frame pushing.
    /// Acts as a hub between frame generation (RenderService) and consumption (preview, virtual camera, export).
    /// </summary>
    public class OutputManager
    {
        private readonly List<IOutputTarget> _targets = new();
        private readonly object _lock = new();

        /// <summary>
        /// Registers an output target to receive frames.
        /// Thread-safe operation.
        /// </summary>
        /// <param name="target">The target to register</param>
        public void RegisterTarget(IOutputTarget target)
        {
            if (target == null)
                throw new ArgumentNullException(nameof(target));

            lock (_lock)
            {
                if (!_targets.Contains(target))
                {
                    _targets.Add(target);
                }
            }
        }

        /// <summary>
        /// Unregisters an output target so it no longer receives frames.
        /// Thread-safe operation.
        /// </summary>
        /// <param name="target">The target to unregister</param>
        public void UnregisterTarget(IOutputTarget target)
        {
            if (target == null)
                return;

            lock (_lock)
            {
                _targets.Remove(target);
            }
        }

        /// <summary>
        /// Pushes a frame to all registered output targets.
        /// Thread-safe operation.
        /// 
        /// If a target throws an exception, it is caught and logged (to console for now),
        /// and the frame continues to be pushed to remaining targets.
        /// </summary>
        /// <param name="frame">The frame to distribute. Targets must consume immediately.</param>
        public void PushFrame(Frame frame)
        {
            if (frame == null || !frame.IsValid)
                return;

            // Create a snapshot of targets under lock to minimize lock duration
            IOutputTarget[] snapshot;
            lock (_lock)
            {
                snapshot = _targets.ToArray();
            }

            // Push to each target outside the lock
            foreach (var target in snapshot)
            {
                try
                {
                    target.Receive(frame);
                }
                catch (Exception ex)
                {
                    // Log error but continue to other targets
                    // In future, could use proper logging framework
                    Console.WriteLine($"OutputManager: Target {target.GetType().Name} failed: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// Gets the current number of registered targets.
        /// Thread-safe operation.
        /// </summary>
        public int TargetCount
        {
            get
            {
                lock (_lock)
                {
                    return _targets.Count;
                }
            }
        }
    }
}
