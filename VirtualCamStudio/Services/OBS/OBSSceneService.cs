using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;

namespace VirtualCamStudio.Services.OBS
{
    /// <summary>
    /// Manages OBS scenes for VirtualCam Studio.
    /// Ensures the "VirtualCam Studio" scene exists and can be switched to.
    /// Never modifies or deletes user scenes.
    /// </summary>
    public class OBSSceneService
    {
        // ============================================
        // Constants
        // ============================================

        private const string VirtualCamSceneName = "VirtualCam Studio";

        // ============================================
        // Fields
        // ============================================

        private readonly OBSClient _obsClient;

        // ============================================
        // Constructor
        // ============================================

        /// <summary>
        /// Creates a new OBS scene service.
        /// </summary>
        /// <param name="obsClient">The OBS client to use for WebSocket communication</param>
        public OBSSceneService(OBSClient obsClient)
        {
            _obsClient = obsClient ?? throw new ArgumentNullException(nameof(obsClient));
        }

        // ============================================
        // Public API
        // ============================================

        /// <summary>
        /// Checks if a scene with the specified name exists in OBS.
        /// </summary>
        /// <param name="sceneName">The name of the scene to check</param>
        /// <returns>True if the scene exists, false otherwise</returns>
        public async Task<bool> SceneExistsAsync(string sceneName)
        {
            try
            {
                if (!_obsClient.IsConnected)
                {
                    Debug.WriteLine("[OBSSceneService] Cannot check scene existence - not connected to OBS.");
                    return false;
                }

                if (string.IsNullOrWhiteSpace(sceneName))
                {
                    Debug.WriteLine("[OBSSceneService] Invalid scene name provided.");
                    return false;
                }

                var sceneNames = await GetSceneNamesAsync();
                bool exists = sceneNames.Contains(sceneName);

                Debug.WriteLine($"[OBSSceneService] Scene '{sceneName}' exists: {exists}");
                return exists;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[OBSSceneService] Error in SceneExistsAsync: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Creates a new scene in OBS with the specified name.
        /// Does not switch to the scene after creation.
        /// </summary>
        /// <param name="sceneName">The name of the scene to create</param>
        /// <returns>True if the scene was created successfully, false otherwise</returns>
        public async Task<bool> CreateSceneAsync(string sceneName)
        {
            try
            {
                if (!_obsClient.IsConnected)
                {
                    Debug.WriteLine("[OBSSceneService] Cannot create scene - not connected to OBS.");
                    return false;
                }

                if (string.IsNullOrWhiteSpace(sceneName))
                {
                    Debug.WriteLine("[OBSSceneService] Invalid scene name provided.");
                    return false;
                }

                // Check if scene already exists
                bool exists = await SceneExistsAsync(sceneName);
                if (exists)
                {
                    Debug.WriteLine($"[OBSSceneService] Scene '{sceneName}' already exists. Skipping creation.");
                    return true;
                }

                Debug.WriteLine($"[OBSSceneService] Creating scene '{sceneName}'...");

                return await Task.Run(() =>
                {
                    try
                    {
                        _obsClient.GetInternalWebsocket().CreateScene(sceneName);
                        Debug.WriteLine($"[OBSSceneService] Successfully created scene '{sceneName}'.");
                        return true;
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"[OBSSceneService] Error creating scene: {ex.Message}");
                        return false;
                    }
                });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[OBSSceneService] Error in CreateSceneAsync: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Switches OBS to the specified scene.
        /// Does not create the scene if it doesn't exist.
        /// </summary>
        /// <param name="sceneName">The name of the scene to switch to</param>
        /// <returns>True if successfully switched to the scene, false otherwise</returns>
        public async Task<bool> SwitchToSceneAsync(string sceneName)
        {
            try
            {
                if (!_obsClient.IsConnected)
                {
                    Debug.WriteLine("[OBSSceneService] Cannot switch scene - not connected to OBS.");
                    return false;
                }

                if (string.IsNullOrWhiteSpace(sceneName))
                {
                    Debug.WriteLine("[OBSSceneService] Invalid scene name provided.");
                    return false;
                }

                // Verify scene exists before attempting to switch
                bool exists = await SceneExistsAsync(sceneName);
                if (!exists)
                {
                    Debug.WriteLine($"[OBSSceneService] Cannot switch to '{sceneName}' - scene does not exist.");
                    return false;
                }

                Debug.WriteLine($"[OBSSceneService] Switching to scene '{sceneName}'...");

                return await Task.Run(() =>
                {
                    try
                    {
                        _obsClient.GetInternalWebsocket().SetCurrentProgramScene(sceneName);
                        Debug.WriteLine($"[OBSSceneService] Successfully switched to scene '{sceneName}'.");
                        return true;
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"[OBSSceneService] Error switching scene: {ex.Message}");
                        return false;
                    }
                });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[OBSSceneService] Error in SwitchToSceneAsync: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Gets the names of all scenes in OBS.
        /// </summary>
        /// <returns>Read-only list of scene names, or empty list on error</returns>
        public async Task<IReadOnlyList<string>> GetScenesAsync()
        {
            // Delegate to existing method
            return await GetSceneNamesAsync();
        }

        /// <summary>
        /// Ensures the "VirtualCam Studio" scene exists and switches to it.
        /// This is the primary method for preparing OBS for VirtualCam Studio output.
        /// Steps:
        /// 1. Check if the scene exists
        /// 2. Create it if it doesn't exist
        /// 3. Switch to the scene
        /// </summary>
        /// <returns>True if the scene is ready and active, false otherwise</returns>
        public async Task<bool> EnsureVirtualCamSceneAsync()
        {
            try
            {
                if (!_obsClient.IsConnected)
                {
                    Debug.WriteLine("[OBSSceneService] Cannot ensure scene - not connected to OBS.");
                    return false;
                }

                Debug.WriteLine($"[OBSSceneService] Ensuring scene '{VirtualCamSceneName}' exists and is active...");

                // Step 1: Check if scene exists
                bool exists = await SceneExistsAsync(VirtualCamSceneName);

                // Step 2: Create if it doesn't exist
                if (!exists)
                {
                    Debug.WriteLine($"[OBSSceneService] Scene '{VirtualCamSceneName}' not found. Creating...");
                    bool created = await CreateSceneAsync(VirtualCamSceneName);
                    if (!created)
                    {
                        Debug.WriteLine($"[OBSSceneService] Failed to create scene '{VirtualCamSceneName}'.");
                        return false;
                    }
                }
                else
                {
                    Debug.WriteLine($"[OBSSceneService] Scene '{VirtualCamSceneName}' already exists.");
                }

                // Step 3: Switch to the scene
                bool switched = await SwitchToSceneAsync(VirtualCamSceneName);
                if (!switched)
                {
                    Debug.WriteLine($"[OBSSceneService] Failed to switch to scene '{VirtualCamSceneName}'.");
                    return false;
                }

                Debug.WriteLine($"[OBSSceneService] Scene '{VirtualCamSceneName}' is ready and active.");
                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[OBSSceneService] Error in EnsureVirtualCamSceneAsync: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Switches OBS to the "VirtualCam Studio" scene.
        /// Ensures the scene exists before switching.
        /// </summary>
        /// <returns>True if successfully switched to the scene, false otherwise</returns>
        public async Task<bool> SwitchToVirtualCamSceneAsync()
        {
            try
            {
                if (!_obsClient.IsConnected)
                {
                    Debug.WriteLine("[OBSSceneService] Cannot switch scene - not connected to OBS.");
                    return false;
                }

                // First ensure the scene exists
                bool sceneExists = await EnsureVirtualCamSceneAsync();
                if (!sceneExists)
                {
                    Debug.WriteLine($"[OBSSceneService] Cannot switch to '{VirtualCamSceneName}' - scene does not exist and could not be created.");
                    return false;
                }

                Debug.WriteLine($"[OBSSceneService] Switching to scene '{VirtualCamSceneName}'...");

                return await Task.Run(() =>
                {
                    try
                    {
                        _obsClient.GetInternalWebsocket().SetCurrentProgramScene(VirtualCamSceneName);
                        Debug.WriteLine($"[OBSSceneService] Successfully switched to scene '{VirtualCamSceneName}'.");
                        return true;
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"[OBSSceneService] Error switching scene: {ex.Message}");
                        return false;
                    }
                });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[OBSSceneService] Error in SwitchToVirtualCamSceneAsync: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Gets the names of all scenes in OBS.
        /// </summary>
        /// <returns>Read-only list of scene names, or empty list on error</returns>
        public async Task<IReadOnlyList<string>> GetSceneNamesAsync()
        {
            try
            {
                if (!_obsClient.IsConnected)
                {
                    Debug.WriteLine("[OBSSceneService] Cannot get scene names - not connected to OBS.");
                    return Array.Empty<string>();
                }

                return await Task.Run<IReadOnlyList<string>>(() =>
                {
                    try
                    {
                        var scenes = _obsClient.GetInternalWebsocket().ListScenes();
                        var sceneNames = scenes.Select(scene => scene.Name).ToList();

                        Debug.WriteLine($"[OBSSceneService] Found {sceneNames.Count} scenes: {string.Join(", ", sceneNames)}");

                        return sceneNames.AsReadOnly();
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"[OBSSceneService] Error listing scenes: {ex.Message}");
                        return Array.Empty<string>();
                    }
                });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[OBSSceneService] Error in GetSceneNamesAsync: {ex.Message}");
                return Array.Empty<string>();
            }
        }

        /// <summary>
        /// Gets the name of the currently active scene in OBS.
        /// </summary>
        /// <returns>Current scene name, or null if not connected or error occurs</returns>
        public async Task<string?> GetCurrentSceneNameAsync()
        {
            try
            {
                if (!_obsClient.IsConnected)
                {
                    Debug.WriteLine("[OBSSceneService] Cannot get current scene - not connected to OBS.");
                    return null;
                }

                return await Task.Run(() =>
                {
                    try
                    {
                        var sceneName = _obsClient.GetInternalWebsocket().GetCurrentProgramScene();
                        Debug.WriteLine($"[OBSSceneService] Current scene: {sceneName}");
                        return sceneName;
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"[OBSSceneService] Error getting current scene: {ex.Message}");
                        return null;
                    }
                });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[OBSSceneService] Error in GetCurrentSceneNameAsync: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Checks if the "VirtualCam Studio" scene exists.
        /// </summary>
        /// <returns>True if the scene exists, false otherwise</returns>
        public async Task<bool> VirtualCamSceneExistsAsync()
        {
            var sceneNames = await GetSceneNamesAsync();
            return sceneNames.Contains(VirtualCamSceneName);
        }
    }
}
