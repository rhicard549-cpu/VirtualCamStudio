using System;
using System.Diagnostics;
using System.Threading.Tasks;
using OBSWebsocketDotNet;
using OBSWebsocketDotNet.Communication;

namespace VirtualCamStudio.Services.OBS
{
    /// <summary>
    /// Client for connecting to OBS Studio via WebSocket using obs-websocket-dotnet.
    /// Provides connection management and state reporting.
    /// Supports obs-websocket v5.x protocol.
    /// </summary>
    public class OBSClient : IDisposable
    {
        private readonly OBSWebsocket _obs;
        private bool _disposed = false;
        private bool _isConnected = false;

        /// <summary>
        /// Event raised when successfully connected to OBS.
        /// </summary>
        public event EventHandler? Connected;

        /// <summary>
        /// Event raised when disconnected from OBS.
        /// </summary>
        public event EventHandler? Disconnected;

        /// <summary>
        /// Gets whether the client is currently connected to OBS WebSocket.
        /// </summary>
        public bool IsConnected => _isConnected && _obs.IsConnected;

        /// <summary>
        /// Initializes a new instance of the OBSClient.
        /// </summary>
        public OBSClient()
        {
            _obs = new OBSWebsocket();

            // Wire up events from the underlying OBSWebsocket
            _obs.Connected += OnObsConnected;
            _obs.Disconnected += OnObsDisconnected;
        }

        /// <summary>
        /// Gets the internal OBSWebsocket instance for advanced operations.
        /// Used by companion services like OBSSceneService for scene/source management.
        /// </summary>
        /// <returns>The underlying OBSWebsocket instance</returns>
        internal OBSWebsocket GetInternalWebsocket()
        {
            return _obs;
        }

        /// <summary>
        /// Connects to the OBS WebSocket server asynchronously.
        /// </summary>
        /// <param name="url">WebSocket URL (default: ws://127.0.0.1:4455)</param>
        /// <param name="password">Optional password for authentication</param>
        /// <returns>True if connection successful, false otherwise</returns>
        public async Task<bool> ConnectAsync(string url = "ws://127.0.0.1:4455", string? password = null)
        {
            try
            {
                if (_isConnected)
                {
                    Debug.WriteLine("[OBSClient] Already connected.");
                    return true;
                }

                Debug.WriteLine($"[OBSClient] Connecting to {url}...");

                // Run connection in a task to make it async
                await Task.Run(() =>
                {
                    try
                    {
                        _obs.ConnectAsync(url, password ?? string.Empty);
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"[OBSClient] Connection attempt error: {ex.Message}");
                        throw;
                    }
                });

                // Give it a moment to establish connection
                await Task.Delay(1000);

                _isConnected = _obs.IsConnected;

                if (_isConnected)
                {
                    Debug.WriteLine("[OBSClient] Successfully connected to OBS.");
                    return true;
                }
                else
                {
                    Debug.WriteLine("[OBSClient] Connection failed - OBS may not be running or WebSocket plugin not enabled.");
                    return false;
                }
            }
            catch (AuthFailureException)
            {
                Debug.WriteLine("[OBSClient] Authentication failed - incorrect password.");
                _isConnected = false;
                return false;
            }
            catch (ErrorResponseException ex)
            {
                Debug.WriteLine($"[OBSClient] OBS error response: {ex.Message}");
                _isConnected = false;
                return false;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[OBSClient] Connection error: {ex.Message}");
                _isConnected = false;
                return false;
            }
        }

        /// <summary>
        /// Disconnects from the OBS WebSocket server asynchronously.
        /// </summary>
        public async Task DisconnectAsync()
        {
            try
            {
                if (!_isConnected)
                {
                    Debug.WriteLine("[OBSClient] Already disconnected.");
                    return;
                }

                Debug.WriteLine("[OBSClient] Disconnecting...");

                await Task.Run(() =>
                {
                    try
                    {
                        _obs.Disconnect();
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"[OBSClient] Disconnect error: {ex.Message}");
                    }
                });

                _isConnected = false;
                Debug.WriteLine("[OBSClient] Disconnected.");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[OBSClient] Disconnect error: {ex.Message}");
            }
        }

        /// <summary>
        /// Event handler for OBS connection established.
        /// </summary>
        private void OnObsConnected(object? sender, EventArgs e)
        {
            _isConnected = true;
            Debug.WriteLine("[OBSClient] Event: Connected");
            Connected?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// Event handler for OBS disconnection.
        /// </summary>
        private void OnObsDisconnected(object? sender, ObsDisconnectionInfo e)
        {
            _isConnected = false;
            Debug.WriteLine($"[OBSClient] Event: Disconnected (Reason: {e.DisconnectReason})");
            Disconnected?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// Gets information about the connected OBS instance.
        /// </summary>
        /// <returns>OBS version information, or null if not connected</returns>
        public string? GetConnectionInfo()
        {
            try
            {
                if (!IsConnected)
                    return null;

                var version = _obs.GetVersion();
                return $"OBS Studio {version.OBSStudioVersion}\n" +
                       $"WebSocket: {version.PluginVersion}\n" +
                       $"Available Requests: {version.SupportedImageFormats}";
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[OBSClient] Error getting connection info: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Gets the current status of OBS including version, scene, and streaming/recording states.
        /// </summary>
        /// <returns>OBSStatus object with current information, or null if not connected</returns>
        public async Task<Models.OBSStatus?> GetStatusAsync()
        {
            try
            {
                if (!IsConnected)
                {
                    Debug.WriteLine("[OBSClient] Cannot get status - not connected.");
                    return null;
                }

                return await Task.Run(() =>
                {
                    try
                    {
                        var status = new Models.OBSStatus();

                        // Get version information
                        var version = _obs.GetVersion();
                        status.OBSVersion = version.OBSStudioVersion ?? "Unknown";
                        status.WebSocketVersion = version.PluginVersion ?? "Unknown";

                        // Get current scene
                        var currentScene = _obs.GetCurrentProgramScene();
                        status.CurrentScene = currentScene ?? "Unknown";

                        // Get virtual camera status
                        var virtualCamStatus = _obs.GetVirtualCamStatus();
                        status.VirtualCameraActive = virtualCamStatus.IsActive;

                        // Get recording status
                        var recordStatus = _obs.GetRecordStatus();
                        status.RecordingActive = recordStatus.IsRecording;

                        // Get streaming status
                        var streamStatus = _obs.GetStreamStatus();
                        status.StreamingActive = streamStatus.IsActive;

                        Debug.WriteLine($"[OBSClient] Retrieved status: Scene={status.CurrentScene}, " +
                                      $"VCam={status.VirtualCameraActive}, " +
                                      $"Recording={status.RecordingActive}, " +
                                      $"Streaming={status.StreamingActive}");

                        return status;
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"[OBSClient] Error retrieving status: {ex.Message}");
                        return null;
                    }
                });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[OBSClient] Error in GetStatusAsync: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Starts the OBS virtual camera.
        /// </summary>
        /// <returns>True if started successfully, false otherwise</returns>
        public async Task<bool> StartVirtualCameraAsync()
        {
            try
            {
                if (!IsConnected)
                {
                    Debug.WriteLine("[OBSClient] Cannot start virtual camera - not connected.");
                    return false;
                }

                return await Task.Run(() =>
                {
                    try
                    {
                        _obs.StartVirtualCam();
                        Debug.WriteLine("[OBSClient] Virtual camera started successfully.");
                        return true;
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"[OBSClient] Error starting virtual camera: {ex.Message}");
                        return false;
                    }
                });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[OBSClient] Error in StartVirtualCameraAsync: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Stops the OBS virtual camera.
        /// </summary>
        /// <returns>True if stopped successfully, false otherwise</returns>
        public async Task<bool> StopVirtualCameraAsync()
        {
            try
            {
                if (!IsConnected)
                {
                    Debug.WriteLine("[OBSClient] Cannot stop virtual camera - not connected.");
                    return false;
                }

                return await Task.Run(() =>
                {
                    try
                    {
                        _obs.StopVirtualCam();
                        Debug.WriteLine("[OBSClient] Virtual camera stopped successfully.");
                        return true;
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"[OBSClient] Error stopping virtual camera: {ex.Message}");
                        return false;
                    }
                });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[OBSClient] Error in StopVirtualCameraAsync: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Checks if the OBS virtual camera is currently running.
        /// </summary>
        /// <returns>True if running, false otherwise</returns>
        public async Task<bool> IsVirtualCameraRunningAsync()
        {
            try
            {
                if (!IsConnected)
                {
                    Debug.WriteLine("[OBSClient] Cannot check virtual camera status - not connected.");
                    return false;
                }

                return await Task.Run(() =>
                {
                    try
                    {
                        var status = _obs.GetVirtualCamStatus();
                        bool isRunning = status.IsActive;
                        Debug.WriteLine($"[OBSClient] Virtual camera running: {isRunning}");
                        return isRunning;
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"[OBSClient] Error checking virtual camera status: {ex.Message}");
                        return false;
                    }
                });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[OBSClient] Error in IsVirtualCameraRunningAsync: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Disposes the client and releases resources.
        /// </summary>
        public void Dispose()
        {
            if (_disposed)
                return;

            try
            {
                // Unwire events
                _obs.Connected -= OnObsConnected;
                _obs.Disconnected -= OnObsDisconnected;

                // Disconnect if connected
                if (_isConnected)
                {
                    _obs.Disconnect();
                }

                _disposed = true;
                Debug.WriteLine("[OBSClient] Disposed.");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[OBSClient] Dispose error: {ex.Message}");
            }
        }
    }
}
