using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using VirtualCamStudio.Models;
using VirtualCamStudio.Services.MoreLogin;

namespace VirtualCamStudio.Services
{
    /// <summary>
    /// Service for discovering and managing cloud phone devices from MoreLogin.
    /// Provides methods to query available devices and their properties.
    /// </summary>
    public class CloudPhoneService
    {
        private readonly MoreLoginClient _client;

        /// <summary>
        /// Initializes a new instance of the CloudPhoneService.
        /// </summary>
        public CloudPhoneService() : this(new MoreLoginClient())
        {
        }

        /// <summary>
        /// Initializes a new instance of the CloudPhoneService with a custom client.
        /// Supports dependency injection scenarios.
        /// </summary>
        /// <param name="client">The MoreLoginClient to use for API communication</param>
        public CloudPhoneService(MoreLoginClient client)
        {
            _client = client ?? throw new ArgumentNullException(nameof(client));
        }

        /// <summary>
        /// Retrieves a list of all available cloud phones from MoreLogin.
        /// </summary>
        /// <returns>Read-only list of cloud phones, or empty list if request fails</returns>
        public async Task<IReadOnlyList<CloudPhone>> GetCloudPhonesAsync()
        {
            try
            {
                // Ensure we're connected
                if (!_client.IsConnected)
                {
                    var connected = await _client.ConnectAsync();
                    if (!connected)
                    {
                        return Array.Empty<CloudPhone>();
                    }
                }

                // Make API request to get cloud phones
                var response = await GetCloudPhonesFromApiAsync();

                if (response != null && response.Count > 0)
                {
                    System.Diagnostics.Debug.WriteLine($"[CloudPhoneService] Retrieved {response.Count} cloud phone(s).");
                }
                else
                {
                }

                return response ?? Array.Empty<CloudPhone>();
            }
            catch (Exception ex)
            {
                return Array.Empty<CloudPhone>();
            }
        }

        /// <summary>
        /// Internal method to retrieve cloud phones from the MoreLogin API.
        /// Parses JSON response and constructs CloudPhone objects.
        /// </summary>
        private async Task<IReadOnlyList<CloudPhone>?> GetCloudPhonesFromApiAsync()
        {
            try
            {
                // This would be the actual API endpoint for cloud phones
                // For now, using a placeholder endpoint structure
                var url = $"{_client.BaseUrl}/api/phones";

                var httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
                var response = await httpClient.GetAsync(url);

                if (!response.IsSuccessStatusCode)
                {
                    return null;
                }

                var json = await response.Content.ReadAsStringAsync();

                // Parse JSON response
                var phones = ParseCloudPhonesFromJson(json);
                return phones;
            }
            catch (HttpRequestException ex)
            {
                return null;
            }
            catch (TaskCanceledException)
            {
                return null;
            }
            catch (Exception ex)
            {
                return null;
            }
        }

        /// <summary>
        /// Parses JSON response from the API into CloudPhone objects.
        /// </summary>
        private List<CloudPhone> ParseCloudPhonesFromJson(string json)
        {
            var phones = new List<CloudPhone>();

            try
            {
                // Parse JSON array of phones
                using var document = JsonDocument.Parse(json);
                var root = document.RootElement;

                // Handle both array and object with data property
                var phonesArray = root.ValueKind == JsonValueKind.Array 
                    ? root 
                    : root.TryGetProperty("data", out var dataElement) 
                        ? dataElement 
                        : root;

                if (phonesArray.ValueKind == JsonValueKind.Array)
                {
                    foreach (var phoneElement in phonesArray.EnumerateArray())
                    {
                        var phone = new CloudPhone
                        {
                            Id = GetJsonString(phoneElement, "id") ?? GetJsonString(phoneElement, "phoneId") ?? string.Empty,
                            Name = GetJsonString(phoneElement, "name") ?? GetJsonString(phoneElement, "phoneName") ?? "Unknown",
                            Status = GetJsonString(phoneElement, "status") ?? "Unknown",
                            Manufacturer = GetJsonString(phoneElement, "manufacturer") ?? GetJsonString(phoneElement, "brand") ?? "Unknown",
                            Model = GetJsonString(phoneElement, "model") ?? "Unknown",
                            AndroidVersion = GetJsonString(phoneElement, "androidVersion") ?? GetJsonString(phoneElement, "osVersion") ?? "Unknown",
                            Resolution = GetJsonString(phoneElement, "resolution") ?? GetJsonString(phoneElement, "screenResolution") ?? "Unknown"
                        };

                        phones.Add(phone);
                    }
                }
            }
            catch (JsonException ex)
            {
            }

            return phones;
        }

        /// <summary>
        /// Safely extracts a string property from a JSON element.
        /// </summary>
        private string? GetJsonString(JsonElement element, string propertyName)
        {
            if (element.TryGetProperty(propertyName, out var property))
            {
                return property.GetString();
            }
            return null;
        }
    }
}
