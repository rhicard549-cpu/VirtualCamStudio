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
    }
}
