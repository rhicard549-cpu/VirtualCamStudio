using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace VirtualCamStudio.Services.OBS
{
    /// <summary>
    /// Manages OBS sources for VirtualCam Studio.
    /// Handles creation and updating of image sources in the VirtualCam Studio scene.
    /// Never deletes, renames, or modifies user sources or other scenes.
    /// </summary>
    public class OBSSourceService
    {
        // ============================================
        // Constants
        // ============================================

        private const string VirtualCamSceneName = "VirtualCam Studio";
        private const string PreviewSourceName = "VirtualCam Preview";

        // ============================================
        // Fields
        // ============================================

        private readonly OBSClient _obsClient;

        // ============================================
        // Constructor
        // ============================================

        /// <summary>
        /// Creates a new OBS source service.
        /// </summary>
        /// <param name="obsClient">The OBS client to use for WebSocket communication</param>
        public OBSSourceService(OBSClient obsClient)
        {
            _obsClient = obsClient ?? throw new ArgumentNullException(nameof(obsClient));
        }

        // ============================================
        // Public API
        // ============================================

        /// <summary>
        /// Checks if a source with the specified name exists in the specified scene.
        /// </summary>
        /// <param name="sceneName">The name of the scene to check</param>
        /// <param name="sourceName">The name of the source to look for</param>
        /// <returns>True if the source exists in the scene, false otherwise</returns>
        public async Task<bool> SourceExistsAsync(string sceneName, string sourceName)
        {
            try
            {
                if (!_obsClient.IsConnected)
                {
                    Debug.WriteLine("[OBSSourceService] Cannot check source existence - not connected to OBS.");
                    return false;
                }

                if (string.IsNullOrWhiteSpace(sceneName) || string.IsNullOrWhiteSpace(sourceName))
                {
                    Debug.WriteLine("[OBSSourceService] Invalid scene or source name provided.");
                    return false;
                }

                return await Task.Run(() =>
                {
                    try
                    {
                        var sceneItems = _obsClient.GetInternalWebsocket().GetSceneItemList(sceneName);
                        bool exists = sceneItems.Any(item => item.SourceName == sourceName);

                        Debug.WriteLine($"[OBSSourceService] Source '{sourceName}' in scene '{sceneName}' exists: {exists}");
                        return exists;
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"[OBSSourceService] Error checking source existence: {ex.Message}");
                        return false;
                    }
                });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[OBSSourceService] Error in SourceExistsAsync: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Creates a new image source in the specified scene.
        /// </summary>
        /// <param name="sceneName">The name of the scene to add the source to</param>
        /// <param name="sourceName">The name for the new source</param>
        /// <param name="imagePath">The full path to the image file</param>
        /// <returns>True if the source was created successfully, false otherwise</returns>
        public async Task<bool> CreateImageSourceAsync(string sceneName, string sourceName, string imagePath)
        {
            try
            {
                if (!_obsClient.IsConnected)
                {
                    Debug.WriteLine("[OBSSourceService] Cannot create source - not connected to OBS.");
                    return false;
                }

                if (string.IsNullOrWhiteSpace(sceneName) || string.IsNullOrWhiteSpace(sourceName) || string.IsNullOrWhiteSpace(imagePath))
                {
                    Debug.WriteLine("[OBSSourceService] Invalid scene name, source name, or image path provided.");
                    return false;
                }

                // Check if source already exists
                bool exists = await SourceExistsAsync(sceneName, sourceName);
                if (exists)
                {
                    Debug.WriteLine($"[OBSSourceService] Source '{sourceName}' already exists in scene '{sceneName}'. Skipping creation.");
                    return true;
                }

                Debug.WriteLine($"[OBSSourceService] Creating image source '{sourceName}' in scene '{sceneName}' with path: {imagePath}");

                return await Task.Run(() =>
                {
                    try
                    {
                        // Create input settings for image source
                        var inputSettings = new JObject
                        {
                            ["file"] = imagePath,
                            ["unload"] = false // Keep image loaded in memory
                        };

                        // Create the image source
                        _obsClient.GetInternalWebsocket().CreateInput(
                            sceneName: sceneName,
                            inputName: sourceName,
                            inputKind: "image_source",
                            inputSettings: inputSettings,
                            sceneItemEnabled: true
                        );

                        Debug.WriteLine($"[OBSSourceService] Successfully created image source '{sourceName}'.");
                        return true;
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"[OBSSourceService] Error creating image source: {ex.Message}");
                        return false;
                    }
                });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[OBSSourceService] Error in CreateImageSourceAsync: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Updates an existing image source with a new image path.
        /// </summary>
        /// <param name="sourceName">The name of the source to update</param>
        /// <param name="imagePath">The new image path</param>
        /// <returns>True if the source was updated successfully, false otherwise</returns>
        public async Task<bool> UpdateImageSourceAsync(string sourceName, string imagePath)
        {
            try
            {
                if (!_obsClient.IsConnected)
                {
                    Debug.WriteLine("[OBSSourceService] Cannot update source - not connected to OBS.");
                    return false;
                }

                if (string.IsNullOrWhiteSpace(sourceName) || string.IsNullOrWhiteSpace(imagePath))
                {
                    Debug.WriteLine("[OBSSourceService] Invalid source name or image path provided.");
                    return false;
                }

                Debug.WriteLine($"[OBSSourceService] Updating image source '{sourceName}' with path: {imagePath}");

                return await Task.Run(() =>
                {
                    try
                    {
                        // Create updated settings
                        var inputSettings = new JObject
                        {
                            ["file"] = imagePath,
                            ["unload"] = false
                        };

                        // Update the source settings
                        _obsClient.GetInternalWebsocket().SetInputSettings(sourceName, inputSettings);

                        Debug.WriteLine($"[OBSSourceService] Successfully updated image source '{sourceName}'.");
                        return true;
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"[OBSSourceService] Error updating image source: {ex.Message}");
                        return false;
                    }
                });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[OBSSourceService] Error in UpdateImageSourceAsync: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Ensures the VirtualCam Preview source exists in the VirtualCam Studio scene.
        /// Creates the source if it doesn't exist, updates it if it does.
        /// This is the primary method for managing the preview image source.
        /// </summary>
        /// <param name="imagePath">The full path to the preview image file</param>
        /// <returns>True if the source is ready and configured, false otherwise</returns>
        public async Task<bool> EnsurePreviewSourceAsync(string imagePath)
        {
            try
            {
                if (!_obsClient.IsConnected)
                {
                    Debug.WriteLine("[OBSSourceService] Cannot ensure preview source - not connected to OBS.");
                    return false;
                }

                if (string.IsNullOrWhiteSpace(imagePath))
                {
                    Debug.WriteLine("[OBSSourceService] Invalid image path provided.");
                    return false;
                }

                Debug.WriteLine($"[OBSSourceService] Ensuring preview source exists with path: {imagePath}");

                // Check if source already exists
                bool exists = await SourceExistsAsync(VirtualCamSceneName, PreviewSourceName);

                if (exists)
                {
                    // Source exists - update it
                    Debug.WriteLine($"[OBSSourceService] Preview source exists. Updating image path...");
                    bool updated = await UpdateImageSourceAsync(PreviewSourceName, imagePath);
                    if (!updated)
                    {
                        Debug.WriteLine("[OBSSourceService] Failed to update preview source.");
                        return false;
                    }
                }
                else
                {
                    // Source doesn't exist - create it
                    Debug.WriteLine($"[OBSSourceService] Preview source does not exist. Creating...");
                    bool created = await CreateImageSourceAsync(VirtualCamSceneName, PreviewSourceName, imagePath);
                    if (!created)
                    {
                        Debug.WriteLine("[OBSSourceService] Failed to create preview source.");
                        return false;
                    }
                }

                Debug.WriteLine("[OBSSourceService] Preview source is ready and configured.");
                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[OBSSourceService] Error in EnsurePreviewSourceAsync: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Gets the list of all sources in a scene.
        /// Useful for debugging and verification.
        /// </summary>
        /// <param name="sceneName">The name of the scene</param>
        /// <returns>Read-only list of source names, or empty list on error</returns>
        public async Task<IReadOnlyList<string>> GetSourceNamesAsync(string sceneName)
        {
            try
            {
                if (!_obsClient.IsConnected)
                {
                    Debug.WriteLine("[OBSSourceService] Cannot get source names - not connected to OBS.");
                    return Array.Empty<string>();
                }

                if (string.IsNullOrWhiteSpace(sceneName))
                {
                    Debug.WriteLine("[OBSSourceService] Invalid scene name provided.");
                    return Array.Empty<string>();
                }

                return await Task.Run<IReadOnlyList<string>>(() =>
                {
                    try
                    {
                        var sceneItems = _obsClient.GetInternalWebsocket().GetSceneItemList(sceneName);
                        var sourceNames = sceneItems.Select(item => item.SourceName).ToList();

                        Debug.WriteLine($"[OBSSourceService] Found {sourceNames.Count} sources in scene '{sceneName}': {string.Join(", ", sourceNames)}");

                        return sourceNames.AsReadOnly();
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"[OBSSourceService] Error listing sources: {ex.Message}");
                        return Array.Empty<string>();
                    }
                });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[OBSSourceService] Error in GetSourceNamesAsync: {ex.Message}");
                return Array.Empty<string>();
            }
        }

        /// <summary>
        /// Creates a Window Capture source in the specified scene with automatic VirtualCamStudio window detection.
        /// Configures capture method, cursor visibility, and client area settings.
        /// </summary>
        /// <param name="sceneName">The name of the scene to add the source to</param>
        /// <param name="sourceName">The name for the new Window Capture source</param>
        /// <returns>True if the source was created successfully, false otherwise</returns>
        public async Task<bool> CreateWindowCaptureSourceAsync(string sceneName, string sourceName)
        {
            try
            {
                if (!_obsClient.IsConnected)
                {
                    Debug.WriteLine("[OBSSourceService] Cannot create Window Capture - not connected to OBS.");
                    return false;
                }

                if (string.IsNullOrWhiteSpace(sceneName) || string.IsNullOrWhiteSpace(sourceName))
                {
                    Debug.WriteLine("[OBSSourceService] Invalid scene or source name provided.");
                    return false;
                }

                Debug.WriteLine($"[OBSSourceService] Creating Window Capture source '{sourceName}' in scene '{sceneName}'...");

                // Step 1: Find the VirtualCamStudio window
                Debug.WriteLine("[OBSSourceService] Searching for VirtualCamStudio window...");
                string? windowIdentifier = await FindVirtualCamStudioWindowAsync();

                if (string.IsNullOrEmpty(windowIdentifier))
                {
                    Debug.WriteLine("[OBSSourceService] Failed to locate VirtualCamStudio window.");
                    return false;
                }

                Debug.WriteLine($"[OBSSourceService] Window found: {windowIdentifier}");

                return await Task.Run(() =>
                {
                    try
                    {
                        var obs = _obsClient.GetInternalWebsocket();

                        // Step 2: Create Window Capture source with settings
                        var settings = new JObject
                        {
                            ["window"] = windowIdentifier,
                            ["capture_cursor"] = false,      // Cursor OFF
                            ["client_area"] = true,          // Client Area ON
                            ["compatibility"] = false         // Compatibility Mode OFF
                        };

                        Debug.WriteLine($"[OBSSourceService] Creating source with settings: {settings}");

                        // Create the input (source)
                        obs.CreateInput(sceneName, sourceName, "window_capture", settings, true);

                        Debug.WriteLine($"[OBSSourceService] Window Capture source '{sourceName}' created successfully.");
                        return true;
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"[OBSSourceService] Error creating Window Capture source: {ex.Message}");
                        Debug.WriteLine($"[OBSSourceService] OBS WebSocket request failed - Scene: '{sceneName}', Source: '{sourceName}', Type: 'window_capture'");
                        return false;
                    }
                });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[OBSSourceService] Error in CreateWindowCaptureSourceAsync: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Finds the VirtualCamStudio window identifier for OBS Window Capture.
        /// Tries multiple strategies: process name, window title matching.
        /// </summary>
        /// <returns>Window identifier string in OBS format, or null if not found</returns>
        private async Task<string?> FindVirtualCamStudioWindowAsync()
        {
            return await Task.Run<string?>(() =>
            {
                try
                {
                    // Strategy 1: Find by process name "VirtualCamStudio.exe"
                    var processes = System.Diagnostics.Process.GetProcessesByName("VirtualCamStudio");
                    if (processes.Length > 0)
                    {
                        var process = processes[0];
                        var windowTitle = process.MainWindowTitle;

                        if (!string.IsNullOrEmpty(windowTitle))
                        {
                            // OBS window_capture format on Windows: "[executable name]:Window Title"
                            string identifier = $"[VirtualCamStudio.exe]:{windowTitle}";
                            Debug.WriteLine($"[OBSSourceService] Found window by process: {identifier}");
                            return identifier;
                        }
                    }

                    // Strategy 2: Try finding by window title containing "VirtualCam Studio"
                    processes = System.Diagnostics.Process.GetProcesses();
                    foreach (var proc in processes)
                    {
                        try
                        {
                            if (!string.IsNullOrEmpty(proc.MainWindowTitle) &&
                                proc.MainWindowTitle.Contains("VirtualCam Studio", StringComparison.OrdinalIgnoreCase))
                            {
                                string exeName = System.IO.Path.GetFileName(proc.MainModule?.FileName ?? "");
                                if (!string.IsNullOrEmpty(exeName))
                                {
                                    string identifier = $"[{exeName}]:{proc.MainWindowTitle}";
                                    Debug.WriteLine($"[OBSSourceService] Found window by title: {identifier}");
                                    return identifier;
                                }
                            }
                        }
                        catch
                        {
                            // Skip processes we can't access
                            continue;
                        }
                    }

                    Debug.WriteLine("[OBSSourceService] VirtualCamStudio window not found.");
                    return null;
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[OBSSourceService] Error finding VirtualCamStudio window: {ex.Message}");
                    return null;
                }
            });
        }

        /// <summary>
        /// Fits a source to the OBS canvas (equivalent to Right Click > Transform > Fit To Screen).
        /// Calculates and applies the transform to scale the source to fill the canvas.
        /// </summary>
        /// <param name="sceneName">The name of the scene containing the source</param>
        /// <param name="sourceName">The name of the source to fit</param>
        /// <returns>True if the source was fitted successfully, false otherwise</returns>
        public async Task<bool> FitSourceToCanvasAsync(string sceneName, string sourceName)
        {
            try
            {
                if (!_obsClient.IsConnected)
                {
                    Debug.WriteLine("[OBSSourceService] Cannot fit source - not connected to OBS.");
                    return false;
                }

                if (string.IsNullOrWhiteSpace(sceneName) || string.IsNullOrWhiteSpace(sourceName))
                {
                    Debug.WriteLine("[OBSSourceService] Invalid scene or source name provided.");
                    return false;
                }

                Debug.WriteLine($"[OBSSourceService] Fitting source '{sourceName}' to canvas in scene '{sceneName}'...");

                return await Task.Run(() =>
                {
                    try
                    {
                        var obs = _obsClient.GetInternalWebsocket();

                        // Step 1: Get video settings to know canvas size
                        var videoSettings = obs.GetVideoSettings();
                        int canvasWidth = videoSettings.BaseWidth;
                        int canvasHeight = videoSettings.BaseHeight;

                        Debug.WriteLine($"[OBSSourceService] Canvas size: {canvasWidth}x{canvasHeight}");

                        // Step 2: Get the scene item ID for the source
                        var sceneItems = obs.GetSceneItemList(sceneName);
                        var targetItem = sceneItems.FirstOrDefault(item => item.SourceName == sourceName);

                        if (targetItem == null)
                        {
                            Debug.WriteLine($"[OBSSourceService] Source '{sourceName}' not found in scene '{sceneName}'.");
                            return false;
                        }

                        int sceneItemId = targetItem.ItemId;
                        Debug.WriteLine($"[OBSSourceService] Scene item ID: {sceneItemId}");

                        // Step 3: Get current transform to read source dimensions
                        var transform = obs.GetSceneItemTransform(sceneName, sceneItemId);
                        double sourceWidth = transform.SourceWidth;
                        double sourceHeight = transform.SourceHeight;

                        Debug.WriteLine($"[OBSSourceService] Source size: {sourceWidth}x{sourceHeight}");

                        // Step 4: Calculate scale to fit (preserve aspect ratio, fill canvas)
                        double scaleX = canvasWidth / sourceWidth;
                        double scaleY = canvasHeight / sourceHeight;
                        double scale = Math.Min(scaleX, scaleY); // Fit inside canvas

                        Debug.WriteLine($"[OBSSourceService] Calculated scale: {scale:F3} (scaleX: {scaleX:F3}, scaleY: {scaleY:F3})");

                        // Step 5: Calculate centered position
                        double scaledWidth = sourceWidth * scale;
                        double scaledHeight = sourceHeight * scale;
                        double posX = (canvasWidth - scaledWidth) / 2.0;
                        double posY = (canvasHeight - scaledHeight) / 2.0;

                        Debug.WriteLine($"[OBSSourceService] Position: ({posX:F1}, {posY:F1})");

                        // Step 6: Apply transform
                        var newTransform = new JObject
                        {
                            ["positionX"] = posX,
                            ["positionY"] = posY,
                            ["scaleX"] = scale,
                            ["scaleY"] = scale,
                            ["alignment"] = 0,  // Top-left alignment
                            ["boundsType"] = "OBS_BOUNDS_NONE"
                        };

                        obs.SetSceneItemTransform(sceneName, sceneItemId, newTransform);

                        Debug.WriteLine($"[OBSSourceService] Source '{sourceName}' fitted to canvas successfully.");
                        return true;
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"[OBSSourceService] Error fitting source to canvas: {ex.Message}");
                        Debug.WriteLine($"[OBSSourceService] OBS WebSocket request failed - Scene: '{sceneName}', Source: '{sourceName}'");
                        return false;
                    }
                });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[OBSSourceService] Error in FitSourceToCanvasAsync: {ex.Message}");
                return false;
            }
        }
    }
}

