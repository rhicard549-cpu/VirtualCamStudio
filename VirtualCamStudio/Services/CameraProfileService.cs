using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using VirtualCamStudio.Models;

namespace VirtualCamStudio.Services
{
    /// <summary>
    /// Manages camera profile persistence and operations.
    /// 
    /// Responsibilities:
    /// 1. Load all profiles from disk
    /// 2. Save a profile to disk
    /// 3. Delete a profile from disk
    /// 4. Retrieve a specific profile by name
    /// 5. Auto-create the profiles directory if it doesn't exist
    /// </summary>
    public class CameraProfileService
    {
        private readonly string _profilesDirectory;
        private readonly JsonSerializerOptions _jsonOptions;

        public CameraProfileService()
        {
            // Create profiles directory path: %AppData%\VirtualCamStudio\Profiles
            string appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            _profilesDirectory = Path.Combine(appDataPath, "VirtualCamStudio", "Profiles");

            // Auto-create directory if it doesn't exist
            if (!Directory.Exists(_profilesDirectory))
            {
                Directory.CreateDirectory(_profilesDirectory);
            }

            // Configure JSON serialization options
            _jsonOptions = new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNameCaseInsensitive = true
            };
        }

        /// <summary>
        /// Gets the profiles directory path.
        /// </summary>
        public string ProfilesDirectory => _profilesDirectory;

        /// <summary>
        /// Loads all profiles from disk.
        /// </summary>
        /// <returns>List of camera profiles, or empty list if none exist</returns>
        public List<CameraProfile> LoadProfiles()
        {
            var profiles = new List<CameraProfile>();

            if (!Directory.Exists(_profilesDirectory))
                return profiles;

            try
            {
                foreach (var filePath in Directory.GetFiles(_profilesDirectory, "*.json"))
                {
                    var profile = LoadProfileFromFile(filePath);
                    if (profile != null)
                    {
                        profiles.Add(profile);
                    }
                }
            }
            catch (Exception ex)
            {
            }

            return profiles;
        }

        /// <summary>
        /// Saves a profile to disk.
        /// </summary>
        /// <param name="profile">Camera profile to save</param>
        /// <returns>True if successful, false otherwise</returns>
        public bool SaveProfile(CameraProfile profile)
        {
            if (profile == null || string.IsNullOrWhiteSpace(profile.Name))
                return false;

            try
            {
                string fileName = SanitizeFileName(profile.Name);
                string filePath = Path.Combine(_profilesDirectory, $"{fileName}.json");

                string json = JsonSerializer.Serialize(profile, _jsonOptions);
                File.WriteAllText(filePath, json);

                return true;
            }
            catch (Exception ex)
            {
                return false;
            }
        }

        /// <summary>
        /// Deletes a profile from disk.
        /// </summary>
        /// <param name="profileName">Name of the profile to delete</param>
        /// <returns>True if successful, false otherwise</returns>
        public bool DeleteProfile(string profileName)
        {
            if (string.IsNullOrWhiteSpace(profileName))
                return false;

            try
            {
                string fileName = SanitizeFileName(profileName);
                string filePath = Path.Combine(_profilesDirectory, $"{fileName}.json");

                if (File.Exists(filePath))
                {
                    File.Delete(filePath);
                    return true;
                }

                return false;
            }
            catch (Exception ex)
            {
                return false;
            }
        }

        /// <summary>
        /// Retrieves a specific profile by name.
        /// </summary>
        /// <param name="profileName">Name of the profile to retrieve</param>
        /// <returns>Camera profile if found, null otherwise</returns>
        public CameraProfile? GetProfile(string profileName)
        {
            if (string.IsNullOrWhiteSpace(profileName))
                return null;

            try
            {
                string fileName = SanitizeFileName(profileName);
                string filePath = Path.Combine(_profilesDirectory, $"{fileName}.json");

                if (File.Exists(filePath))
                {
                    return LoadProfileFromFile(filePath);
                }
            }
            catch (Exception ex)
            {
            }

            return null;
        }

        /// <summary>
        /// Loads a profile from a specific file path.
        /// </summary>
        private CameraProfile? LoadProfileFromFile(string filePath)
        {
            try
            {
                string json = File.ReadAllText(filePath);
                return JsonSerializer.Deserialize<CameraProfile>(json, _jsonOptions);
            }
            catch (Exception ex)
            {
                return null;
            }
        }

        /// <summary>
        /// Sanitizes a profile name for use as a filename.
        /// Removes invalid filename characters.
        /// </summary>
        private static string SanitizeFileName(string fileName)
        {
            foreach (var invalidChar in Path.GetInvalidFileNameChars())
            {
                fileName = fileName.Replace(invalidChar, '_');
            }
            return fileName;
        }
    }
}
