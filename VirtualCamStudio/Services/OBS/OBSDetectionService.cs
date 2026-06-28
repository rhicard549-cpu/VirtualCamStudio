using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Microsoft.Win32;

namespace VirtualCamStudio.Services.OBS
{
    /// <summary>
    /// Service for detecting OBS Studio installation and running state.
    /// Provides methods to check if OBS is installed, locate its installation path,
    /// and determine if it's currently running.
    /// </summary>
    public class OBSDetectionService
    {
        private const string OBS_PROCESS_NAME = "obs64";
        private const string OBS_EXECUTABLE = "obs64.exe";

        // Common OBS installation locations
        private static readonly string[] CommonInstallPaths = new[]
        {
            @"C:\Program Files\obs-studio\bin\64bit\obs64.exe",
            @"C:\Program Files (x86)\obs-studio\bin\64bit\obs64.exe",
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), @"obs-studio\bin\64bit\obs64.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), @"obs-studio\bin\64bit\obs64.exe"),
        };

        // Registry paths to check for OBS installation
        private static readonly string[] RegistryPaths = new[]
        {
            @"SOFTWARE\OBS Studio",
            @"SOFTWARE\WOW6432Node\OBS Studio",
        };

        /// <summary>
        /// Detects if OBS Studio is installed on the system.
        /// Checks common installation locations and registry entries.
        /// </summary>
        /// <returns>True if OBS Studio is found, false otherwise</returns>
        public bool IsInstalled()
        {
            try
            {
                var installPath = GetInstallPath();
                var isInstalled = !string.IsNullOrEmpty(installPath);

                Debug.WriteLine($"[OBSDetection] OBS Studio installed: {isInstalled}");
                if (isInstalled)
                {
                    Debug.WriteLine($"[OBSDetection] Install path: {installPath}");
                }

                return isInstalled;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[OBSDetection] Error checking installation: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Detects if OBS Studio (obs64.exe) is currently running.
        /// </summary>
        /// <returns>True if OBS is running, false otherwise</returns>
        public bool IsRunning()
        {
            try
            {
                var processes = Process.GetProcessesByName(OBS_PROCESS_NAME);
                var isRunning = processes.Length > 0;

                // Clean up process references
                foreach (var process in processes)
                {
                    process.Dispose();
                }

                Debug.WriteLine($"[OBSDetection] OBS Studio running: {isRunning}");
                return isRunning;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[OBSDetection] Error checking running state: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Gets the installation path of OBS Studio if found.
        /// Checks common installation locations first, then falls back to registry lookup.
        /// </summary>
        /// <returns>Full path to obs64.exe if found, null otherwise</returns>
        public string? GetInstallPath()
        {
            try
            {
                // First, check common installation paths
                var pathFromCommonLocations = CheckCommonInstallPaths();
                if (pathFromCommonLocations != null)
                {
                    return pathFromCommonLocations;
                }

                // Fall back to registry lookup
                var pathFromRegistry = CheckRegistryForInstallPath();
                if (pathFromRegistry != null)
                {
                    return pathFromRegistry;
                }

                // Last resort: check if OBS is running and get path from process
                var pathFromRunningProcess = GetPathFromRunningProcess();
                if (pathFromRunningProcess != null)
                {
                    return pathFromRunningProcess;
                }

                Debug.WriteLine("[OBSDetection] OBS Studio installation path not found.");
                return null;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[OBSDetection] Error getting install path: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Checks common installation paths for obs64.exe.
        /// </summary>
        private string? CheckCommonInstallPaths()
        {
            foreach (var path in CommonInstallPaths)
            {
                if (File.Exists(path))
                {
                    Debug.WriteLine($"[OBSDetection] Found OBS at common location: {path}");
                    return path;
                }
            }
            return null;
        }

        /// <summary>
        /// Checks Windows registry for OBS installation path.
        /// </summary>
        private string? CheckRegistryForInstallPath()
        {
            try
            {
                foreach (var registryPath in RegistryPaths)
                {
                    // Check HKEY_LOCAL_MACHINE
                    using (var key = Registry.LocalMachine.OpenSubKey(registryPath))
                    {
                        if (key != null)
                        {
                            var installPath = key.GetValue("") as string;
                            if (!string.IsNullOrEmpty(installPath))
                            {
                                var obsPath = Path.Combine(installPath, @"bin\64bit\obs64.exe");
                                if (File.Exists(obsPath))
                                {
                                    Debug.WriteLine($"[OBSDetection] Found OBS via registry (HKLM): {obsPath}");
                                    return obsPath;
                                }
                            }
                        }
                    }

                    // Check HKEY_CURRENT_USER
                    using (var key = Registry.CurrentUser.OpenSubKey(registryPath))
                    {
                        if (key != null)
                        {
                            var installPath = key.GetValue("") as string;
                            if (!string.IsNullOrEmpty(installPath))
                            {
                                var obsPath = Path.Combine(installPath, @"bin\64bit\obs64.exe");
                                if (File.Exists(obsPath))
                                {
                                    Debug.WriteLine($"[OBSDetection] Found OBS via registry (HKCU): {obsPath}");
                                    return obsPath;
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[OBSDetection] Error reading registry: {ex.Message}");
            }

            return null;
        }

        /// <summary>
        /// Gets the installation path from a running OBS process.
        /// </summary>
        private string? GetPathFromRunningProcess()
        {
            try
            {
                var processes = Process.GetProcessesByName(OBS_PROCESS_NAME);
                if (processes.Length > 0)
                {
                    var process = processes[0];
                    var mainModule = process.MainModule;
                    var path = mainModule?.FileName;

                    // Clean up
                    foreach (var p in processes)
                    {
                        p.Dispose();
                    }

                    if (!string.IsNullOrEmpty(path) && File.Exists(path))
                    {
                        Debug.WriteLine($"[OBSDetection] Found OBS via running process: {path}");
                        return path;
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[OBSDetection] Error getting path from running process: {ex.Message}");
            }

            return null;
        }

        /// <summary>
        /// Gets detailed information about the OBS installation.
        /// </summary>
        /// <returns>String with installation details, or null if not found</returns>
        public string? GetInstallationInfo()
        {
            try
            {
                var path = GetInstallPath();
                if (string.IsNullOrEmpty(path))
                {
                    return null;
                }

                var fileInfo = new FileInfo(path);
                var version = FileVersionInfo.GetVersionInfo(path);

                return $"OBS Studio\n" +
                       $"  Path: {path}\n" +
                       $"  Version: {version.FileVersion}\n" +
                       $"  Size: {fileInfo.Length / (1024 * 1024)} MB\n" +
                       $"  Modified: {fileInfo.LastWriteTime}";
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[OBSDetection] Error getting installation info: {ex.Message}");
                return null;
            }
        }
    }
}
