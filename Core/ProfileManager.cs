using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using WootMouseRemap;
using WootMouseRemap.Features;
using WootMouseRemap.Diagnostics;
using WootMouseRemap.UI;
using WootMouseRemap.Security;

namespace WootMouseRemap.Core
{
    /// <summary>
    /// Manages configuration profiles for different games and scenarios
    /// </summary>
    public class ProfileManager
    {
        private const string ProfilesDirectory = "Profiles";
        private const string ProfilesFile = "configuration_profiles.json";
        private readonly Dictionary<string, ConfigurationProfile> _profiles = new();
        private string? _activeProfileName;

        public event Action<string>? ProfileActivated;
        public event Action? ProfilesChanged;

        public IReadOnlyDictionary<string, ConfigurationProfile> Profiles => _profiles;
        public string? ActiveProfileName => _activeProfileName;
        public ConfigurationProfile? Current => _activeProfileName != null && _profiles.TryGetValue(_activeProfileName, out var profile) ? profile : null;

        /// <summary>
        /// Sanitizes strings for logging to prevent log injection attacks
        /// </summary>
        private static string SanitizeForLogging(string input)
        {
            if (string.IsNullOrEmpty(input))
                return "[null_or_empty]";

            // Remove control characters and limit length
            var sanitized = Regex.Replace(input, @"[\r\n\t\x00-\x1F\x7F-\x9F]", "_", RegexOptions.None, TimeSpan.FromMilliseconds(100));
            if (sanitized.Length > 100)
                sanitized = sanitized.Substring(0, 100) + "...[truncated]";
            
            return sanitized;
        }

        public ProfileManager()
        {
            LoadProfiles();
        }

        public void CreateProfile(string name, string description, AntiRecoilViewModel viewModel)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("Profile name cannot be empty", nameof(name));

            if (_profiles.ContainsKey(name))
                throw new InvalidOperationException($"Profile '{name}' already exists");

            var profile = new ConfigurationProfile
            {
                Name = name,
                Description = description,
                CreatedUtc = DateTime.UtcNow,
                LastModifiedUtc = DateTime.UtcNow,
                Settings = new AntiRecoilSettings
                {
                    Enabled = viewModel.Enabled,
                    Strength = viewModel.Strength,
                    VerticalThreshold = viewModel.VerticalThreshold,
                    ActivationDelayMs = viewModel.ActivationDelayMs,
                    HorizontalCompensation = viewModel.HorizontalCompensation,
                    AdaptiveCompensation = viewModel.AdaptiveCompensation,
                    MaxTickCompensation = viewModel.MaxTickCompensation,
                    MaxTotalCompensation = viewModel.MaxTotalCompensation,
                    CooldownMs = viewModel.CooldownMs,
                    DecayPerMs = viewModel.DecayPerMs
                }
            };

            _profiles[name] = profile;
            SaveProfiles();
            ProfilesChanged?.Invoke();

            Logger.Info("Created configuration profile: {ProfileName}", SanitizeForLogging(name));
        }

        public void UpdateProfile(string name, AntiRecoilViewModel viewModel)
        {
            if (!_profiles.TryGetValue(name, out var profile))
                throw new KeyNotFoundException($"Profile '{name}' not found");

            profile.Settings.Enabled = viewModel.Enabled;
            profile.Settings.Strength = viewModel.Strength;
            profile.Settings.VerticalThreshold = viewModel.VerticalThreshold;
            profile.Settings.ActivationDelayMs = viewModel.ActivationDelayMs;
            profile.Settings.HorizontalCompensation = viewModel.HorizontalCompensation;
            profile.Settings.AdaptiveCompensation = viewModel.AdaptiveCompensation;
            profile.Settings.MaxTickCompensation = viewModel.MaxTickCompensation;
            profile.Settings.MaxTotalCompensation = viewModel.MaxTotalCompensation;
            profile.Settings.CooldownMs = viewModel.CooldownMs;
            profile.Settings.DecayPerMs = viewModel.DecayPerMs;
            profile.LastModifiedUtc = DateTime.UtcNow;

            SaveProfiles();
            Logger.Info("Updated configuration profile: {ProfileName}", SanitizeForLogging(name));
        }

        public void DeleteProfile(string name)
        {
            if (!_profiles.Remove(name))
                throw new KeyNotFoundException($"Profile '{name}' not found");

            if (_activeProfileName == name)
                _activeProfileName = null;

            SaveProfiles();
            ProfilesChanged?.Invoke();

            Logger.Info("Deleted configuration profile: {ProfileName}", SanitizeForLogging(name));
        }

        public void ActivateProfile(string name, AntiRecoilViewModel viewModel)
        {
            if (!_profiles.TryGetValue(name, out var profile))
                throw new KeyNotFoundException($"Profile '{name}' not found");

            // Apply profile settings to view model
            viewModel.Enabled = profile.Settings.Enabled;
            viewModel.Strength = profile.Settings.Strength;
            viewModel.VerticalThreshold = profile.Settings.VerticalThreshold;
            viewModel.ActivationDelayMs = profile.Settings.ActivationDelayMs;
            viewModel.HorizontalCompensation = profile.Settings.HorizontalCompensation;
            viewModel.AdaptiveCompensation = profile.Settings.AdaptiveCompensation;
            viewModel.MaxTickCompensation = profile.Settings.MaxTickCompensation;
            viewModel.MaxTotalCompensation = profile.Settings.MaxTotalCompensation;
            viewModel.CooldownMs = profile.Settings.CooldownMs;
            viewModel.DecayPerMs = profile.Settings.DecayPerMs;

            _activeProfileName = name;
            profile.LastUsedUtc = DateTime.UtcNow;

            SaveProfiles();
            ProfileActivated?.Invoke(name);

            Logger.Info("Activated configuration profile: {ProfileName}", SanitizeForLogging(name));
        }

        public void RenameProfile(string oldName, string newName)
        {
            if (string.IsNullOrWhiteSpace(newName))
                throw new ArgumentException("New profile name cannot be empty", nameof(newName));

            if (!_profiles.TryGetValue(oldName, out var profile))
                throw new KeyNotFoundException($"Profile '{oldName}' not found");

            if (_profiles.ContainsKey(newName))
                throw new InvalidOperationException($"Profile '{newName}' already exists");

            _profiles.Remove(oldName);
            profile.Name = newName;
            profile.LastModifiedUtc = DateTime.UtcNow;
            _profiles[newName] = profile;

            if (_activeProfileName == oldName)
                _activeProfileName = newName;

            SaveProfiles();
            ProfilesChanged?.Invoke();

            Logger.Info("Renamed configuration profile: {OldName} -> {NewName}", SanitizeForLogging(oldName), SanitizeForLogging(newName));
        }

        public ConfigurationProfile? GetProfile(string name)
        {
            return _profiles.TryGetValue(name, out var profile) ? profile : null;
        }

        public List<ConfigurationProfile> GetRecentProfiles(int count = 5)
        {
            return _profiles.Values
                .Where(p => p.LastUsedUtc.HasValue)
                .OrderByDescending(p => p.LastUsedUtc)
                .Take(count)
                .ToList();
        }

        public string ExportProfile(string name)
        {
            if (!_profiles.TryGetValue(name, out var profile))
                throw new KeyNotFoundException($"Profile '{name}' not found");

            return JsonSerializer.Serialize(profile, new JsonSerializerOptions { WriteIndented = true });
        }

        public void ImportProfile(string json, bool overwrite = false)
        {
            ConfigurationProfile profile;
            var options = new JsonSerializerOptions 
            { 
                MaxDepth = 5,
                PropertyNameCaseInsensitive = false,
                AllowTrailingCommas = false
            };
            profile = SecureJsonDeserializer.DeserializeSecure<ConfigurationProfile>(json) ?? new ConfigurationProfile();
            if (profile == null)
                throw new InvalidOperationException("Failed to deserialize profile");

            if (_profiles.ContainsKey(profile.Name) && !overwrite)
                throw new InvalidOperationException($"Profile '{profile.Name}' already exists");

            _profiles[profile.Name] = profile;
            SaveProfiles();
            ProfilesChanged?.Invoke();

            Logger.Info("Imported configuration profile: {ProfileName}", SanitizeForLogging(profile.Name));
        }

        public List<string> GetAutoDetectedGames()
        {
            var games = new List<string>();

            // Simple game detection based on common process names
            var commonGames = new[]
            {
                "csgo", "cs2", "valorant", "apex", "pubg", "fortnite", "cod", "overwatch",
                "rainbow6", "battlefield", "destiny", "warframe", "rust", "tarkov"
            };

            foreach (var game in commonGames)
            {
                // In a real implementation, you would check for running processes
                // For now, we'll just return the list of known games
                games.Add(game);
            }

            return games;
        }

        private void LoadProfiles()
        {
            try
            {
                if (!SecurePathValidator.EnsureSafeDirectory(ProfilesDirectory))
                {
                    Logger.Error("Failed to create or validate profiles directory: {Directory}", ProfilesDirectory);
                    CreateDefaultProfiles();
                    return;
                }
                var profilesPath = SecurePathValidator.SafeCombinePath(ProfilesDirectory, ProfilesFile);
                if (profilesPath == null)
                {
                    Logger.Error("Invalid profiles file path");
                    CreateDefaultProfiles();
                    return;
                }

                if (!File.Exists(profilesPath))
                {
                    CreateDefaultProfiles();
                    return;
                }

                var json = File.ReadAllText(profilesPath);
                Dictionary<string, ConfigurationProfile> profileData;
                var options = new JsonSerializerOptions 
                { 
                    MaxDepth = 5,
                    PropertyNameCaseInsensitive = false,
                    AllowTrailingCommas = false
                };
                profileData = SecureJsonDeserializer.DeserializeSecure<Dictionary<string, ConfigurationProfile>>(json) ?? new Dictionary<string, ConfigurationProfile>();

                if (profileData != null)
                {
                    _profiles.Clear();
                    foreach (var kvp in profileData)
                    {
                        var profile = kvp.Value;
                        
                        // Validate and migrate profile
                        if (ValidateAndMigrateProfile(profile))
                        {
                            _profiles[kvp.Key] = profile;
                        }
                        else
                        {
                            Logger.Warn("Skipping invalid profile: {ProfileName}", SanitizeForLogging(kvp.Key));
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Error("Failed to load configuration profiles", ex);
                CreateDefaultProfiles();
            }
        }

        /// <summary>
        /// Validates and migrates a profile to the current version
        /// </summary>
        private bool ValidateAndMigrateProfile(ConfigurationProfile profile)
        {
            if (profile == null)
                return false;

            // Basic validation
            if (string.IsNullOrWhiteSpace(profile.Name))
            {
                Logger.Warn("Profile validation failed: missing name");
                return false;
            }

            if (profile.Settings == null)
            {
                Logger.Warn("Profile '{ProfileName}' validation failed: missing settings", SanitizeForLogging(profile.Name));
                return false;
            }

            // Migration logic
            if (profile.Version < 1)
            {
                // Version 0 to 1: Ensure all required fields are initialized
                profile.Version = 1;
                profile.LastModifiedUtc = DateTime.UtcNow;
                
                // Ensure settings have valid defaults
                if (profile.Settings.Strength < 0 || profile.Settings.Strength > 1)
                    profile.Settings.Strength = Math.Clamp(profile.Settings.Strength, 0, 1);
                
                if (profile.Settings.VerticalThreshold < 0)
                    profile.Settings.VerticalThreshold = 0;
                
                if (profile.Settings.HorizontalCompensation < -1 || profile.Settings.HorizontalCompensation > 1)
                    profile.Settings.HorizontalCompensation = Math.Clamp(profile.Settings.HorizontalCompensation, -1, 1);
                
                if (profile.Settings.MaxTickCompensation < 0)
                    profile.Settings.MaxTickCompensation = 0;
                
                if (profile.Settings.MaxTotalCompensation < 0)
                    profile.Settings.MaxTotalCompensation = 0;
                
                if (profile.Settings.DecayPerMs < 0)
                    profile.Settings.DecayPerMs = 0;

                Logger.Info("Migrated profile '{ProfileName}' to version 1", SanitizeForLogging(profile.Name));
            }

            // Future migrations can be added here
            // if (profile.Version < 2) { ... }

            return true;
        }

        private void SaveProfiles()
        {
            try
            {
                if (!SecurePathValidator.EnsureSafeDirectory(ProfilesDirectory))
                {
                    Logger.Error("Failed to create or validate profiles directory: {Directory}", ProfilesDirectory);
                    return;
                }
                var profilesPath = SecurePathValidator.SafeCombinePath(ProfilesDirectory, ProfilesFile);
                if (profilesPath == null)
                {
                    Logger.Error("Invalid profiles file path");
                    return;
                }

                var json = JsonSerializer.Serialize(_profiles, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(profilesPath, json);

                // Create backup
                BackupManager.CreateSettingsBackup(profilesPath);
            }
            catch (Exception ex)
            {
                Logger.Error("Failed to save configuration profiles", ex);
            }
        }

        private void CreateDefaultProfiles()
        {
            // Default profile for general use
            var defaultProfile = new ConfigurationProfile
            {
                Version = 1,
                Name = "Default",
                Description = "General purpose anti-recoil configuration",
                CreatedUtc = DateTime.UtcNow,
                LastModifiedUtc = DateTime.UtcNow,
                Settings = new AntiRecoilSettings
                {
                    Enabled = false,
                    Strength = 0.5f,
                    VerticalThreshold = 2.0f,
                    ActivationDelayMs = 0,
                    HorizontalCompensation = 0.0f,
                    AdaptiveCompensation = false,
                    MaxTickCompensation = 10.0f,
                    MaxTotalCompensation = 100.0f,
                    CooldownMs = 0,
                    DecayPerMs = 0.0f
                }
            };

            // CS2/CSGO profile
            var cs2Profile = new ConfigurationProfile
            {
                Version = 1,
                Name = "CS2",
                Description = "Optimized for Counter-Strike 2",
                CreatedUtc = DateTime.UtcNow,
                LastModifiedUtc = DateTime.UtcNow,
                Settings = new AntiRecoilSettings
                {
                    Enabled = false,
                    Strength = 0.65f,
                    VerticalThreshold = 3.0f,
                    ActivationDelayMs = 0,
                    HorizontalCompensation = 0.2f,
                    AdaptiveCompensation = true,
                    MaxTickCompensation = 8.0f,
                    MaxTotalCompensation = 120.0f,
                    CooldownMs = 50,
                    DecayPerMs = 0.1f
                }
            };

            // Valorant profile
            var valorantProfile = new ConfigurationProfile
            {
                Version = 1,
                Name = "Valorant",
                Description = "Optimized for Valorant",
                CreatedUtc = DateTime.UtcNow,
                LastModifiedUtc = DateTime.UtcNow,
                Settings = new AntiRecoilSettings
                {
                    Enabled = false,
                    Strength = 0.4f,
                    VerticalThreshold = 2.5f,
                    ActivationDelayMs = 0,
                    HorizontalCompensation = 0.15f,
                    AdaptiveCompensation = false,
                    MaxTickCompensation = 6.0f,
                    MaxTotalCompensation = 80.0f,
                    CooldownMs = 0,
                    DecayPerMs = 0.05f
                }
            };

            _profiles[defaultProfile.Name] = defaultProfile;
            _profiles[cs2Profile.Name] = cs2Profile;
            _profiles[valorantProfile.Name] = valorantProfile;

            SaveProfiles();
        }
    }

    public class ConfigurationProfile
    {
        public int Version { get; set; } = 1; // Current version for migration
        public string Name { get; set; } = "";
        public string Description { get; set; } = "";
        public DateTime CreatedUtc { get; set; }
        public DateTime LastModifiedUtc { get; set; }
        public DateTime? LastUsedUtc { get; set; }
        public AntiRecoilSettings Settings { get; set; } = new();
        public List<string> AssociatedPatterns { get; set; } = new();
        public Dictionary<string, object> Metadata { get; set; } = new();
        
        // Input mapping properties
        public bool WasdToLeftStick { get; set; } = true;
        public Dictionary<int, Xbox360Control> KeyMap { get; set; } = new();
        public Dictionary<MouseInput, Xbox360Control> MouseMap { get; set; } = new();
    }

    public class AntiRecoilSettings
    {
        public bool Enabled { get; set; }
        public float Strength { get; set; }
        public float VerticalThreshold { get; set; }
        public int ActivationDelayMs { get; set; }
        public float HorizontalCompensation { get; set; }
        public bool AdaptiveCompensation { get; set; }
        public float MaxTickCompensation { get; set; }
        public float MaxTotalCompensation { get; set; }
        public int CooldownMs { get; set; }
        public float DecayPerMs { get; set; }
    }
}