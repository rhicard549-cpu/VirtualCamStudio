using System;
using System.Diagnostics;
using System.Threading.Tasks;
using VirtualCamStudio.Services.OBS;

namespace VirtualCamStudio.Services
{
    /// <summary>
    /// Centralized manager for OBS Studio integration via WebSocket.
    /// Handles connection, scene management, and virtual camera control.
    /// VirtualCamStudio completely controls OBS - the user never edits OBS manually.
    /// </summary>
    public class OBSManager : IDisposable
    {
        private readonly OBSClient _client;
        private readonly OBSSceneService _sceneService;
        private bool _disposed = false;
        private bool _isVirtualCameraRunning = false;

        private const string STUDIO_SCENE_NAME = "VirtualCamStudio";

        /// <summary>
        /// Event raised when OBS connection state changes
        /// </summary>
        public event EventHandler<OBSConnectionState>? ConnectionStateChanged;

        /// <summary>
        /// Event raised when virtual camera state changes
        /// </summary>
        public event EventHandler<bool>? VirtualCameraStateChanged;

        /// <summary>
        /// Gets whether OBS is currently connected
        /// </summary>
        public bool IsConnected => _client.IsConnected;

        /// <summary>
        /// Gets whether the virtual camera is running
        /// </summary>
        public bool IsVirtualCameraRunning => _isVirtualCameraRunning;

        /// <summary>
        /// Represents the connection state of OBS
        /// </summary>
        public enum OBSConnectionState
        {
            Disconnected,
            Connected,
            VirtualCameraRunning
        }

        /// <summary>
        /// Initializes a new instance of OBSManager
        /// </summary>
        public OBSManager()
        {
            _client = new OBSClient();
            _sceneService = new OBSSceneService(_client);

            // Wire up events
            _client.Connected += OnClientConnected;
            _client.Disconnected += OnClientDisconnected;
        }

        /// <summary>
        /// Connects to OBS Studio asynchronously and performs complete automatic setup
        /// </summary>
        /// <param name="url">WebSocket URL (default: ws://127.0.0.1:4455)</param>
        /// <param name="password">Optional password for authentication</param>
        /// <returns>True if connection and setup successful, false otherwise</returns>
        public async Task<bool> ConnectAsync(string url = "ws://127.0.0.1:4455", string? password = null)
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(OBSManager));

            try
            {

                bool connected = await _client.ConnectAsync(url, password);

                if (connected)
                {

                    // Perform complete setup (scene, source, canvas, etc.)
                    bool initialized = await InitializeOBSSceneAsync();

                    if (initialized)
                    {
                        return true;
                    }
                    else
                    {
                        return false;
                    }
                }
                else
                {
                    return false;
                }
            }
            catch (Exception ex)
            {
                return false;
            }
        }

        /// <summary>
        /// Disconnects from OBS Studio
        /// </summary>
        public async Task DisconnectAsync()
        {
            if (_disposed)
                return;

            try
            {

                // Stop virtual camera if running
                if (_isVirtualCameraRunning)
                {
                    await StopVirtualCameraAsync();
                }

                await _client.DisconnectAsync();
            }
            catch (Exception ex)
            {
            }
        }

        /// <summary>
        /// Initializes the OBS scene for VirtualCamStudio with complete automatic setup.
        /// Performs 10-step initialization: connection check, scene creation, Window Capture source creation,
        /// window detection, capture configuration, canvas fitting, resolution/FPS setup.
        /// </summary>
        private async Task<bool> InitializeOBSSceneAsync()
        {
            try
            {
                const string WINDOW_CAPTURE_SOURCE_NAME = "VirtualCamStudio Preview";

                // Step 1: Connection check (already done in ConnectAsync)

                // Step 2: Check if scene exists
                var scenes = await _sceneService.GetScenesAsync();

                bool sceneExists = false;
                foreach (var scene in scenes)
                {
                    if (scene == STUDIO_SCENE_NAME)
                    {
                        sceneExists = true;
                        break;
                    }
                }

                if (!sceneExists)
                {
                    await _sceneService.CreateSceneAsync(STUDIO_SCENE_NAME);
                }
                else
                {
                }

                // Switch to the scene
                await _sceneService.SwitchToSceneAsync(STUDIO_SCENE_NAME);

                // Step 3: Check if Window Capture source exists
                var sourceService = new OBSSourceService(_client);
                bool sourceExists = await sourceService.SourceExistsAsync(STUDIO_SCENE_NAME, WINDOW_CAPTURE_SOURCE_NAME);

                if (!sourceExists)
                {

                    // Steps 4-5: Create Window Capture and auto-locate VirtualCamStudio window

                    bool created = await sourceService.CreateWindowCaptureSourceAsync(STUDIO_SCENE_NAME, WINDOW_CAPTURE_SOURCE_NAME);

                    if (!created)
                    {
                        return false;
                    }
                }
                else
                {
                }

                // Step 6: Configure Window Capture (already done in CreateWindowCaptureSourceAsync)
                Debug.WriteLine("[OBSManager] Step 6: Window Capture configured (Cursor: OFF, Client Area: ON) ✓");

                // Step 7: Fit source to canvas
                bool fitted = await sourceService.FitSourceToCanvasAsync(STUDIO_SCENE_NAME, WINDOW_CAPTURE_SOURCE_NAME);

                if (!fitted)
                {
                    // Continue anyway - not critical
                }
                else
                {
                }

                // Step 8: Set canvas to 1080x1920 @ 30 FPS
                Debug.WriteLine("[OBSManager] Step 8: Configuring canvas (1080x1920 @ 30 FPS)...");
                bool videoSettings = await _client.SetCanvasAndOutputSettingsAsync(1080, 1920, 30);

                if (!videoSettings)
                {
                    // Continue anyway - not critical
                }
                else
                {
                }
                return true;
            }
            catch (Exception ex)
            {
                return false;
            }
        }

        /// <summary>
        /// Starts the OBS virtual camera
        /// </summary>
        public async Task<bool> StartVirtualCameraAsync()
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(OBSManager));

            if (!_client.IsConnected)
            {
                return false;
            }

            try
            {

                var obs = _client.GetInternalWebsocket();
                obs.StartVirtualCam();

                _isVirtualCameraRunning = true;

                VirtualCameraStateChanged?.Invoke(this, true);
                ConnectionStateChanged?.Invoke(this, OBSConnectionState.VirtualCameraRunning);

                return true;
            }
            catch (Exception ex)
            {
                return false;
            }
        }

        /// <summary>
        /// Stops the OBS virtual camera
        /// </summary>
        public async Task<bool> StopVirtualCameraAsync()
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(OBSManager));

            if (!_client.IsConnected)
            {
                return false;
            }

            try
            {

                var obs = _client.GetInternalWebsocket();
                obs.StopVirtualCam();

                _isVirtualCameraRunning = false;

                VirtualCameraStateChanged?.Invoke(this, false);
                ConnectionStateChanged?.Invoke(this, OBSConnectionState.Connected);

                return true;
            }
            catch (Exception ex)
            {
                return false;
            }
        }

        /// <summary>
        /// Gets the internal OBS client for advanced operations
        /// </summary>
        internal OBSClient GetClient() => _client;

        /// <summary>
        /// Gets the internal scene service for advanced operations
        /// </summary>
        internal OBSSceneService GetSceneService() => _sceneService;

        /// <summary>
        /// Handles OBS client connected event.
        /// Automatically re-initializes scene and sources on reconnection.
        /// </summary>
        private void OnClientConnected(object? sender, EventArgs e)
        {
            ConnectionStateChanged?.Invoke(this, OBSConnectionState.Connected);

            // Automatically reinitialize when OBS reconnects
            Task.Run(async () =>
            {
                bool initialized = await InitializeOBSSceneAsync();

                if (initialized)
                {
                }
                else
                {
                }
            });
        }

        /// <summary>
        /// Handles OBS client disconnected event
        /// </summary>
        private void OnClientDisconnected(object? sender, EventArgs e)
        {
            _isVirtualCameraRunning = false;
            ConnectionStateChanged?.Invoke(this, OBSConnectionState.Disconnected);
        }

        /// <summary>
        /// Disposes resources
        /// </summary>
        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;

            // Unwire events
            _client.Connected -= OnClientConnected;
            _client.Disconnected -= OnClientDisconnected;

            // Disconnect if connected
            if (_client.IsConnected)
            {
                Task.Run(async () => await DisconnectAsync()).Wait();
            }

            _client.Dispose();
        }
    }
}
