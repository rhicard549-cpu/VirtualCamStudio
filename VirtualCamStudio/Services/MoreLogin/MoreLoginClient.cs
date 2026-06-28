using System;
using System.Net.Http;
using System.Threading.Tasks;

namespace VirtualCamStudio.Services.MoreLogin
{
    /// <summary>
    /// API client for MoreLogin Local API.
    /// Provides a clean, async interface for communicating with the MoreLogin application.
    /// Centralizes HTTP communication, error handling, and connection state management.
    /// </summary>
    public class MoreLoginClient : IDisposable
    {
        private readonly HttpClient _httpClient;
        private bool _isConnected = false;
        private bool _disposed = false;

        /// <summary>
        /// Gets or sets the base URL for the MoreLogin Local API.
        /// Default: http://127.0.0.1:54345
        /// </summary>
        public string BaseUrl { get; set; }

        /// <summary>
        /// Gets whether the client is currently connected to the MoreLogin API.
        /// </summary>
        public bool IsConnected => _isConnected;

        /// <summary>
        /// Initializes a new instance of the MoreLoginClient with default settings.
        /// </summary>
        public MoreLoginClient() : this(new HttpClient())
        {
        }

        /// <summary>
        /// Initializes a new instance of the MoreLoginClient with a custom HttpClient.
        /// This constructor supports dependency injection scenarios.
        /// </summary>
        /// <param name="httpClient">The HttpClient instance to use for API requests</param>
        public MoreLoginClient(HttpClient httpClient)
        {
            _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
            _httpClient.Timeout = TimeSpan.FromSeconds(10);
            BaseUrl = "http://127.0.0.1:54345";
        }

        /// <summary>
        /// Attempts to connect to the MoreLogin Local API.
        /// Verifies connectivity by requesting the API version.
        /// </summary>
        /// <returns>True if connection successful, false otherwise</returns>
        public async Task<bool> ConnectAsync()
        {
            try
            {
                var version = await GetVersionAsync();
                _isConnected = !string.IsNullOrEmpty(version);

                if (_isConnected)
                {
                    System.Diagnostics.Debug.WriteLine($"[MoreLoginClient] Connected successfully. Version: {version}");
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine("[MoreLoginClient] Connection failed: Empty version response.");
                }

                return _isConnected;
            }
            catch (Exception ex)
            {
                _isConnected = false;
                System.Diagnostics.Debug.WriteLine($"[MoreLoginClient] Connection failed: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Disconnects from the MoreLogin Local API.
        /// </summary>
        public void Disconnect()
        {
            _isConnected = false;
            System.Diagnostics.Debug.WriteLine("[MoreLoginClient] Disconnected.");
        }

        /// <summary>
        /// Retrieves the version information from the MoreLogin Local API.
        /// </summary>
        /// <returns>Version string if successful, null if request fails</returns>
        public async Task<string?> GetVersionAsync()
        {
            return await ExecuteRequestAsync(async () =>
            {
                var response = await _httpClient.GetAsync($"{BaseUrl}/api/version");
                response.EnsureSuccessStatusCode();

                var version = await response.Content.ReadAsStringAsync();
                System.Diagnostics.Debug.WriteLine($"[MoreLoginClient] API Version: {version}");
                return version;
            }, "GetVersion");
        }

        /// <summary>
        /// Centralized error handling wrapper for all API requests.
        /// Catches and logs exceptions, returning null on failure.
        /// </summary>
        /// <typeparam name="T">The return type of the API operation</typeparam>
        /// <param name="operation">The async operation to execute</param>
        /// <param name="operationName">Name of the operation for logging purposes</param>
        /// <returns>Result of the operation, or default(T) on failure</returns>
        private async Task<T?> ExecuteRequestAsync<T>(Func<Task<T>> operation, string operationName)
        {
            try
            {
                return await operation();
            }
            catch (HttpRequestException ex)
            {
                System.Diagnostics.Debug.WriteLine($"[MoreLoginClient] {operationName} - HTTP error: {ex.Message}");
                return default;
            }
            catch (TaskCanceledException)
            {
                System.Diagnostics.Debug.WriteLine($"[MoreLoginClient] {operationName} - Request timed out.");
                return default;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[MoreLoginClient] {operationName} - Unexpected error: {ex.Message}");
                return default;
            }
        }

        /// <summary>
        /// Tests the connection to the MoreLogin Local API without changing connection state.
        /// Useful for diagnostics and connection validation.
        /// </summary>
        /// <returns>True if API is reachable, false otherwise</returns>
        public async Task<bool> TestConnectionAsync()
        {
            try
            {
                var response = await _httpClient.GetAsync($"{BaseUrl}/api/version");
                var isReachable = response.IsSuccessStatusCode;

                System.Diagnostics.Debug.WriteLine($"[MoreLoginClient] Connection test: {(isReachable ? "Success" : "Failed")}");
                return isReachable;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[MoreLoginClient] Connection test failed: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Disposes the HttpClient if it was created by this instance.
        /// </summary>
        public void Dispose()
        {
            if (_disposed)
                return;

            // Only dispose HttpClient if we created it (parameterless constructor)
            // If injected, the caller owns the lifecycle
            if (_httpClient != null)
            {
                _httpClient.Dispose();
            }

            _disposed = true;
            System.Diagnostics.Debug.WriteLine("[MoreLoginClient] Disposed.");
        }
    }
}
