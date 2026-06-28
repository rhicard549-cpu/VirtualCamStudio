using System;
using System.Net.Http;
using System.Threading.Tasks;

namespace VirtualCamStudio.Services
{
    /// <summary>
    /// Service for connecting to MoreLogin Local API.
    /// Handles authentication, connection management, and basic API operations.
    /// </summary>
    public class MoreLoginService : IDisposable
    {
        private readonly HttpClient _httpClient;
        private string _apiBaseUrl = "http://127.0.0.1:54345";
        private bool _isConnected = false;
        private bool _disposed = false;

        /// <summary>
        /// Gets or sets the MoreLogin Local API base URL.
        /// Default: http://127.0.0.1:54345
        /// </summary>
        public string ApiBaseUrl
        {
            get => _apiBaseUrl;
            set
            {
                if (string.IsNullOrWhiteSpace(value))
                    throw new ArgumentException("API base URL cannot be empty.", nameof(value));

                _apiBaseUrl = value.TrimEnd('/');
            }
        }

        /// <summary>
        /// Gets whether the service is currently connected to MoreLogin Local API.
        /// </summary>
        public bool IsConnected => _isConnected;

        /// <summary>
        /// Initializes a new instance of the MoreLoginService.
        /// </summary>
        public MoreLoginService()
        {
            _httpClient = new HttpClient
            {
                Timeout = TimeSpan.FromSeconds(10)
            };
        }

        /// <summary>
        /// Connects to the MoreLogin Local API and verifies the connection.
        /// </summary>
        /// <returns>True if connection successful, false otherwise</returns>
        public async Task<bool> ConnectAsync()
        {
            try
            {
                // Test connection by getting version
                var version = await GetVersionAsync();
                _isConnected = !string.IsNullOrEmpty(version);
                return _isConnected;
            }
            catch (Exception ex)
            {
                _isConnected = false;
                System.Diagnostics.Debug.WriteLine($"[MoreLogin] Connection failed: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Disconnects from the MoreLogin Local API.
        /// </summary>
        public void Disconnect()
        {
            _isConnected = false;
            System.Diagnostics.Debug.WriteLine("[MoreLogin] Disconnected from API.");
        }

        /// <summary>
        /// Gets the version information from the MoreLogin Local API.
        /// </summary>
        /// <returns>Version string, or null if request fails</returns>
        public async Task<string?> GetVersionAsync()
        {
            try
            {
                var response = await _httpClient.GetAsync($"{_apiBaseUrl}/api/version");

                if (response.IsSuccessStatusCode)
                {
                    var version = await response.Content.ReadAsStringAsync();
                    System.Diagnostics.Debug.WriteLine($"[MoreLogin] API Version: {version}");
                    return version;
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"[MoreLogin] Version request failed: {response.StatusCode}");
                    return null;
                }
            }
            catch (HttpRequestException ex)
            {
                System.Diagnostics.Debug.WriteLine($"[MoreLogin] Version request error: {ex.Message}");
                return null;
            }
            catch (TaskCanceledException)
            {
                System.Diagnostics.Debug.WriteLine("[MoreLogin] Version request timed out.");
                return null;
            }
        }

        /// <summary>
        /// Tests the connection to the MoreLogin Local API without changing connection state.
        /// </summary>
        /// <returns>True if API is reachable, false otherwise</returns>
        public async Task<bool> TestConnectionAsync()
        {
            try
            {
                var response = await _httpClient.GetAsync($"{_apiBaseUrl}/api/version");
                var isReachable = response.IsSuccessStatusCode;

                System.Diagnostics.Debug.WriteLine($"[MoreLogin] Connection test: {(isReachable ? "Success" : "Failed")}");
                return isReachable;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[MoreLogin] Connection test failed: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Disposes the HttpClient and releases resources.
        /// </summary>
        public void Dispose()
        {
            if (_disposed)
                return;

            _httpClient?.Dispose();
            _disposed = true;
            System.Diagnostics.Debug.WriteLine("[MoreLogin] Service disposed.");
        }
    }
}
