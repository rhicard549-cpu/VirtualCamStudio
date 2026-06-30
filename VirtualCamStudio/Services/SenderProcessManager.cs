using System;
using System.Diagnostics;
using System.IO;

namespace VirtualCamStudio.Services
{
    /// <summary>
    /// Manages the UnityCaptureSender.exe process lifecycle.
    /// Automatically starts the sender when Studio launches and stops it on shutdown.
    /// </summary>
    public class SenderProcessManager : IDisposable
    {
        private Process? _senderProcess;
        private readonly string _senderPath;
        private bool _disposed;

        public bool IsRunning => _senderProcess != null && !_senderProcess.HasExited;

        public SenderProcessManager()
        {
            // Path to UnityCaptureSender.exe relative to the solution root
            // Studio is typically at: Solution\VirtualCamStudio\bin\Debug\net8.0-windows\
            // Sender is at: Solution\UnityCaptureSender\UnityCaptureSender.exe
            // So we go up 3 levels (.., .., ..) to reach the solution root
            string baseDirectory = AppDomain.CurrentDomain.BaseDirectory;
            _senderPath = Path.Combine(baseDirectory, "..", "..", "..", "..", "UnityCaptureSender", "UnityCaptureSender.exe");
            _senderPath = Path.GetFullPath(_senderPath);
        }

        /// <summary>
        /// Starts the sender process in the background (no console window).
        /// </summary>
        public bool Start()
        {
            if (IsRunning)
            {
                return true; // Already running
            }

            // Kill any existing sender processes from previous sessions
            KillExistingSenders();

            if (!File.Exists(_senderPath))
            {
                System.Diagnostics.Debug.WriteLine($"[SenderProcessManager] Sender not found at: {_senderPath}");
                return false; // Sender not found
            }

            try
            {
                _senderProcess = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = _senderPath,
                        UseShellExecute = false,
                        CreateNoWindow = true, // No console window
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        WorkingDirectory = Path.GetDirectoryName(_senderPath)
                    }
                };

                // Optional: capture output for diagnostics (not displayed to user)
                _senderProcess.OutputDataReceived += (sender, args) =>
                {
                    if (!string.IsNullOrEmpty(args.Data))
                    {
                        // Could log to file if needed: File.AppendAllText("sender.log", args.Data + "\n");
                    }
                };

                _senderProcess.ErrorDataReceived += (sender, args) =>
                {
                    if (!string.IsNullOrEmpty(args.Data))
                    {
                        // Could log errors if needed
                    }
                };

                bool started = _senderProcess.Start();
                if (started)
                {
                    _senderProcess.BeginOutputReadLine();
                    _senderProcess.BeginErrorReadLine();

                    // Give the process a moment to initialize
                    System.Threading.Thread.Sleep(500);

                    // Check if it's still running after initialization
                    if (_senderProcess.HasExited)
                    {
                        System.Diagnostics.Debug.WriteLine($"[SenderProcessManager] Sender exited immediately with code: {_senderProcess.ExitCode}");
                        return false;
                    }
                }

                return started;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[SenderProcessManager] Failed to start: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Kills any existing UnityCaptureSender processes from previous sessions.
        /// </summary>
        private void KillExistingSenders()
        {
            try
            {
                var existingProcesses = Process.GetProcessesByName("UnityCaptureSender");
                foreach (var process in existingProcesses)
                {
                    try
                    {
                        System.Diagnostics.Debug.WriteLine($"[SenderProcessManager] Killing existing sender process (PID: {process.Id})");
                        process.Kill();
                        process.WaitForExit(1000); // Wait up to 1 second
                        process.Dispose();
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"[SenderProcessManager] Failed to kill process: {ex.Message}");
                    }
                }

                // Give the system a moment to release resources
                if (existingProcesses.Length > 0)
                {
                    System.Threading.Thread.Sleep(200);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[SenderProcessManager] Failed to check for existing senders: {ex.Message}");
            }
        }

        /// <summary>
        /// Stops the sender process gracefully.
        /// </summary>
        public void Stop()
        {
            if (_senderProcess != null && !_senderProcess.HasExited)
            {
                try
                {
                    _senderProcess.Kill();
                    _senderProcess.WaitForExit(2000); // Wait up to 2 seconds
                }
                catch (Exception)
                {
                    // Process may have already exited
                }
            }

            _senderProcess?.Dispose();
            _senderProcess = null;
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            Stop();
            _disposed = true;
        }
    }
}
