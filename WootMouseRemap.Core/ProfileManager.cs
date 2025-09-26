using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using WootMouseRemap.Features;
// Logger is in WootMouseRemap namespace

namespace WootMouseRemap.Core
{
    /// <summary>
    /// Manages configuration profiles for different games and scenarios
    /// </summary>
    public class ProfileManager
    {
        private const string ProfilesDirectory = "Profiles";
        private const string ProfilesFile = "configuration_profiles.json";
        private const int CurrentConfigVersion = 2; // Increment when schema changes
        private readonly Dictionary<string, ConfigurationProfile> _profiles = new();
        private string? _activeProfileName;

        public event Action<string>? ProfileActivated;
        public event Action? ProfilesChanged;

        public IReadOnlyDictionary<string, ConfigurationProfile> Profiles => _profiles;
        public string? ActiveProfileName => _activeProfileName;
        public ConfigurationProfile Current => _activeProfileName != null && _profiles.TryGetValue(_activeProfileName, out var p) ? p : GetDefaultProfile();

        public void Save()
        {
            SaveProfiles();
        }

        /// <summary>
        /// Sanitizes strings for logging to prevent log injection attacks
        /// </summary>
        private static string SanitizeForLogging(string input)
        {
            if (string.IsNullOrEmpty(input))
                return "[null_or_empty]";

            // Remove control characters and limit length with timeout protection
            try
            {
                var sanitized = Regex.Replace(input, @"[\r\n\t\x00-\x1F\x7F-\x9F]", "_", RegexOptions.None, TimeSpan.FromMilliseconds(100));
                if (sanitized.Length > 100)
                    sanitized = sanitized.Substring(0, 100) + "...[truncated]";
                
                return sanitized;
            }
            catch (RegexMatchTimeoutException)
            {
                // If regex times out, return a safe truncated version
                return input.Length > 100 ? input.Substring(0, 100) + "...[regex_timeout]" : input + "[regex_timeout]";
            }
        }

        public ProfileManager()
        {
            try
            {
                var asmPath = typeof(ProfileManager).Assembly.Location ?? "<unknown>";
                if (File.Exists(asmPath))
                {
                    var fi = new FileInfo(asmPath);
                    Logger.Info("ProfileManager ctor - assembly: {AssemblyPath} (LastWrite: {LastWrite})", asmPath, fi.LastWriteTimeUtc);
                }
                else
                {
                    Logger.Info("ProfileManager ctor - assembly path not found: {AssemblyPath}", asmPath);
                }
            }
            catch (Exception ex)
            {
                Logger.Warn("Failed to inspect ProfileManager assembly location: {Message}", ex.Message);
            }

            LoadProfiles();
        }

        /// <summary>
        /// Validates a configuration profile for data integrity
        /// </summary>
        private static bool ValidateProfile(ConfigurationProfile profile, out List<string> errors)
        {
            errors = new List<string>();

            if (string.IsNullOrWhiteSpace(profile.Name))
                errors.Add("Profile name is required");

            if (profile.Settings == null)
            {
                errors.Add("Profile settings are missing");
                return errors.Count == 0;
            }

            // Validate anti-recoil settings
            if (profile.Settings.Strength < 0f || profile.Settings.Strength > 1f)
                errors.Add($"Strength must be between 0 and 1, got {profile.Settings.Strength}");

            if (profile.Settings.VerticalThreshold < 0f)
                errors.Add($"Vertical threshold must be non-negative, got {profile.Settings.VerticalThreshold}");

            if (profile.Settings.ActivationDelayMs < 0 || profile.Settings.ActivationDelayMs > 10000)
                errors.Add($"Activation delay must be between 0 and 10000ms, got {profile.Settings.ActivationDelayMs}");

            if (profile.Settings.HorizontalCompensation < -1f || profile.Settings.HorizontalCompensation > 1f)
                errors.Add($"Horizontal compensation must be between -1 and 1, got {profile.Settings.HorizontalCompensation}");

            if (profile.Settings.MaxTickCompensation < 0f)
                errors.Add($"Max tick compensation must be non-negative, got {profile.Settings.MaxTickCompensation}");

            if (profile.Settings.MaxTotalCompensation < 0f)
                errors.Add($"Max total compensation must be non-negative, got {profile.Settings.MaxTotalCompensation}");

            if (profile.Settings.CooldownMs < 0)
                errors.Add($"Cooldown must be non-negative, got {profile.Settings.CooldownMs}");

            if (profile.Settings.DecayPerMs < 0f)
                errors.Add($"Decay per ms must be non-negative, got {profile.Settings.DecayPerMs}");

            // Validate curves
            if (profile.Curves != null)
            {
                if (profile.Curves.Sensitivity < 0.01f || profile.Curves.Sensitivity > 10f)
                    errors.Add($"Sensitivity must be between 0.01 and 10, got {profile.Curves.Sensitivity}");

                if (profile.Curves.Expo < 0f || profile.Curves.Expo > 1f)
                    errors.Add($"Expo must be between 0 and 1, got {profile.Curves.Expo}");

                if (profile.Curves.Deadzone < 0f || profile.Curves.Deadzone > 0.5f)
                    errors.Add($"Deadzone must be between 0 and 0.5, got {profile.Curves.Deadzone}");
            }

            // Validate mouse DPI
            if (profile.MouseDpi < 100 || profile.MouseDpi > 50000)
                errors.Add($"Mouse DPI must be between 100 and 50000, got {profile.MouseDpi}");

            return errors.Count == 0;
        }

        /// <summary>
        /// Migrates a configuration profile from an older version to the current version
        /// </summary>
        private static void MigrateProfile(ConfigurationProfile profile)
        {
            // Migration logic for different versions
            // Version 1 -> 2: Add new properties with defaults
            if (!profile.Metadata.ContainsKey("ConfigVersion") || 
                (int)(profile.Metadata["ConfigVersion"] ?? 1) < 2)
            {
                // Ensure new properties have defaults
                if (profile.Curves == null)
                    profile.Curves = new ResponseCurves();

                if (profile.MouseDpi == 0)
                    profile.MouseDpi = 1600;

                // Update version
                profile.Metadata["ConfigVersion"] = CurrentConfigVersion;
                profile.LastModifiedUtc = DateTime.UtcNow;

                Logger.Info("Migrated profile '{ProfileName}' to version {Version}", SanitizeForLogging(profile.Name), CurrentConfigVersion);
            }
        }

        /// <summary>
        /// Sanitizes and repairs invalid profile data
        /// </summary>
        private static void RepairProfile(ConfigurationProfile profile)
        {
            // Repair anti-recoil settings
            if (profile.Settings != null)
            {
                profile.Settings.Strength = Math.Clamp(profile.Settings.Strength, 0f, 1f);
                profile.Settings.VerticalThreshold = Math.Max(0f, profile.Settings.VerticalThreshold);
                profile.Settings.ActivationDelayMs = Math.Clamp(profile.Settings.ActivationDelayMs, 0, 10000);
                profile.Settings.HorizontalCompensation = Math.Clamp(profile.Settings.HorizontalCompensation, -1f, 1f);
                profile.Settings.MaxTickCompensation = Math.Max(0f, profile.Settings.MaxTickCompensation);
                profile.Settings.MaxTotalCompensation = Math.Max(0f, profile.Settings.MaxTotalCompensation);
                profile.Settings.CooldownMs = Math.Max(0, profile.Settings.CooldownMs);
                profile.Settings.DecayPerMs = Math.Max(0f, profile.Settings.DecayPerMs);
            }

            // Repair curves
            if (profile.Curves != null)
            {
                profile.Curves.Sensitivity = Math.Clamp(profile.Curves.Sensitivity, 0.01f, 10f);
                profile.Curves.Expo = Math.Clamp(profile.Curves.Expo, 0f, 1f);
                profile.Curves.Deadzone = Math.Clamp(profile.Curves.Deadzone, 0f, 0.5f);
            }

            // Repair mouse DPI
            profile.MouseDpi = Math.Clamp(profile.MouseDpi, 100, 50000);

            Logger.Info("Repaired invalid data in profile '{ProfileName}'", SanitizeForLogging(profile.Name));
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
            try 
            {
                profile = JsonSerializer.Deserialize<ConfigurationProfile>(json, options) ?? new ConfigurationProfile();
            }
            catch (JsonException) 
            {
                profile = new ConfigurationProfile(); // Safe fallback
            }
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
                Directory.CreateDirectory(ProfilesDirectory);
                var profilesPath = Path.Combine(ProfilesDirectory, ProfilesFile);

                // Path validation to prevent directory traversal attacks
                var fullPath = Path.GetFullPath(profilesPath);
                var allowedDir = Path.GetFullPath(Directory.GetCurrentDirectory());
                if (!fullPath.StartsWith(allowedDir, StringComparison.OrdinalIgnoreCase))
                    throw new UnauthorizedAccessException("Path traversal detected");

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
                try 
                {
                    profileData = JsonSerializer.Deserialize<Dictionary<string, ConfigurationProfile>>(json, options) ?? new Dictionary<string, ConfigurationProfile>();
                }
                catch (JsonException) 
                {
                    profileData = new Dictionary<string, ConfigurationProfile>(); // Safe fallback
                }

                if (profileData != null)
                {
                    _profiles.Clear();
                    int loadedCount = 0;
                    int migratedCount = 0;
                    int repairedCount = 0;

                    foreach (var kvp in profileData)
                    {
                        var profile = kvp.Value;

                        // Migrate if necessary
                        MigrateProfile(profile);

                        // Validate and repair if necessary
                        if (!ValidateProfile(profile, out var errors))
                        {
                            Logger.Warn("Profile '{ProfileName}' has validation errors: {Errors}", SanitizeForLogging(profile.Name), string.Join(", ", errors));
                            RepairProfile(profile);
                            repairedCount++;

                            // Re-validate after repair
                            if (!ValidateProfile(profile, out errors))
                            {
                                Logger.Error("Profile '{ProfileName}' could not be repaired: {Errors}", SanitizeForLogging(profile.Name), string.Join(", ", errors));
                                continue; // Skip invalid profile
                            }
                        }

                        _profiles[kvp.Key] = profile;
                        loadedCount++;
                    }

                    Logger.Info("Loaded {LoadedCount} profiles, migrated {MigratedCount}, repaired {RepairedCount}", loadedCount, migratedCount, repairedCount);

                    // Save migrated/repaired profiles
                    if (migratedCount > 0 || repairedCount > 0)
                    {
                        SaveProfiles();
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Error("Failed to load configuration profiles", ex);
                CreateDefaultProfiles();
            }
        }

        private void SaveProfiles()
        {
            try
            {
                Directory.CreateDirectory(ProfilesDirectory);
                var profilesPath = Path.Combine(ProfilesDirectory, ProfilesFile);

                // Path validation to prevent directory traversal attacks
                var fullPath = Path.GetFullPath(profilesPath);
                var allowedDir = Path.GetFullPath(Directory.GetCurrentDirectory());
                if (!fullPath.StartsWith(allowedDir, StringComparison.OrdinalIgnoreCase))
                    throw new UnauthorizedAccessException("Path traversal detected");

                // Ensure all profiles have current version metadata
                foreach (var profile in _profiles.Values)
                {
                    profile.Metadata["ConfigVersion"] = CurrentConfigVersion;
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
                Name = "Default",
                Description = "General purpose anti-recoil configuration",
                CreatedUtc = DateTime.UtcNow,
                LastModifiedUtc = DateTime.UtcNow,
                WasdToLeftStick = true,
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
                },
                MouseMap = new Dictionary<MouseInput, Xbox360Control>
                {
                    { MouseInput.Left, Xbox360Control.RightTrigger },
                    { MouseInput.Right, Xbox360Control.LeftTrigger },
                    { MouseInput.Middle, Xbox360Control.B },
                    { MouseInput.XButton1, Xbox360Control.X },
                    { MouseInput.XButton2, Xbox360Control.Y },
                    { MouseInput.ScrollUp, Xbox360Control.DpadUp },
                    { MouseInput.ScrollDown, Xbox360Control.DpadDown }
                },
                KeyMap = new Dictionary<int, Xbox360Control>
                {
                    { (int)Keys.Space, Xbox360Control.A },
                    { (int)Keys.LShiftKey, Xbox360Control.LeftBumper },
                    { (int)Keys.LControlKey, Xbox360Control.RightBumper },
                    { (int)Keys.R, Xbox360Control.X },
                    { (int)Keys.F, Xbox360Control.Y },
                    { (int)Keys.Tab, Xbox360Control.Back },
                    { (int)Keys.Escape, Xbox360Control.Start }
                }
            };

            // CS2/CSGO profile
            var cs2Profile = new ConfigurationProfile
            {
                Name = "CS2",
                Description = "Optimized for Counter-Strike 2",
                CreatedUtc = DateTime.UtcNow,
                LastModifiedUtc = DateTime.UtcNow,
                WasdToLeftStick = true,
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
                },
                MouseMap = new Dictionary<MouseInput, Xbox360Control>
                {
                    { MouseInput.Left, Xbox360Control.RightTrigger },
                    { MouseInput.Right, Xbox360Control.LeftTrigger },
                    { MouseInput.Middle, Xbox360Control.B },
                    { MouseInput.XButton1, Xbox360Control.X },
                    { MouseInput.XButton2, Xbox360Control.Y },
                    { MouseInput.ScrollUp, Xbox360Control.DpadUp },
                    { MouseInput.ScrollDown, Xbox360Control.DpadDown }
                },
                KeyMap = new Dictionary<int, Xbox360Control>
                {
                    { (int)Keys.Space, Xbox360Control.A },
                    { (int)Keys.LShiftKey, Xbox360Control.LeftBumper },
                    { (int)Keys.LControlKey, Xbox360Control.RightBumper },
                    { (int)Keys.R, Xbox360Control.X },
                    { (int)Keys.F, Xbox360Control.Y },
                    { (int)Keys.Tab, Xbox360Control.Back },
                    { (int)Keys.Escape, Xbox360Control.Start }
                }
            };

            // Valorant profile
            var valorantProfile = new ConfigurationProfile
            {
                Name = "Valorant",
                Description = "Optimized for Valorant",
                CreatedUtc = DateTime.UtcNow,
                LastModifiedUtc = DateTime.UtcNow,
                WasdToLeftStick = true,
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
                },
                MouseMap = new Dictionary<MouseInput, Xbox360Control>
                {
                    { MouseInput.Left, Xbox360Control.RightTrigger },
                    { MouseInput.Right, Xbox360Control.LeftTrigger },
                    { MouseInput.Middle, Xbox360Control.B },
                    { MouseInput.XButton1, Xbox360Control.X },
                    { MouseInput.XButton2, Xbox360Control.Y },
                    { MouseInput.ScrollUp, Xbox360Control.DpadUp },
                    { MouseInput.ScrollDown, Xbox360Control.DpadDown }
                },
                KeyMap = new Dictionary<int, Xbox360Control>
                {
                    { (int)Keys.Space, Xbox360Control.A },
                    { (int)Keys.LShiftKey, Xbox360Control.LeftBumper },
                    { (int)Keys.LControlKey, Xbox360Control.RightBumper },
                    { (int)Keys.R, Xbox360Control.X },
                    { (int)Keys.F, Xbox360Control.Y },
                    { (int)Keys.Tab, Xbox360Control.Back },
                    { (int)Keys.Escape, Xbox360Control.Start }
                }
            };

            _profiles[defaultProfile.Name] = defaultProfile;
            _profiles[cs2Profile.Name] = cs2Profile;
            _profiles[valorantProfile.Name] = valorantProfile;

            SaveProfiles();
        }

        private ConfigurationProfile GetDefaultProfile()
        {
            // Return Default profile if it exists, otherwise create a minimal one
            if (_profiles.TryGetValue("Default", out var defaultProfile))
                return defaultProfile;

            // Fallback profile if Default doesn't exist
            return new ConfigurationProfile
            {
                Name = "Fallback",
                Description = "Fallback profile",
                WasdToLeftStick = true,
                MouseMap = new Dictionary<MouseInput, Xbox360Control>
                {
                    { MouseInput.Left, Xbox360Control.RightTrigger },
                    { MouseInput.Right, Xbox360Control.LeftTrigger },
                    { MouseInput.Middle, Xbox360Control.B },
                    { MouseInput.XButton1, Xbox360Control.X },
                    { MouseInput.XButton2, Xbox360Control.Y }
                },
                KeyMap = new Dictionary<int, Xbox360Control>
                {
                    { (int)Keys.Space, Xbox360Control.A }
                }
            };
        }
    }

    public class ConfigurationProfile
    {
        public string Name { get; set; } = "";
        public string Description { get; set; } = "";
        public DateTime CreatedUtc { get; set; }
        public DateTime LastModifiedUtc { get; set; }
        public DateTime? LastUsedUtc { get; set; }
        public AntiRecoilSettings Settings { get; set; } = new();
        public List<string> AssociatedPatterns { get; set; } = new();
        public Dictionary<string, object> Metadata { get; set; } = new();
        
        // Mapping properties for controller output mode
        public bool WasdToLeftStick { get; set; } = false;
        public Dictionary<int, Xbox360Control> KeyMap { get; set; } = new();
        public Dictionary<MouseInput, Xbox360Control> MouseMap { get; set; } = new();
        
        // Additional properties for UI
        public int PreferredXInputIndex { get; set; } = 0;
        public ResponseCurves Curves { get; set; } = new();
        public int MouseDpi { get; set; } = 1600;
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

    public class ResponseCurves
    {
        public float Acceleration { get; set; } = 1.0f;
        public float Deceleration { get; set; } = 1.0f;
        public float Sensitivity { get; set; } = 1.0f;
        public float Deadzone { get; set; } = 0.0f;
        public float MaxOutput { get; set; } = 1.0f;
        public float MinOutput { get; set; } = 0.0f;
        public float CurveExponent { get; set; } = 1.0f;
        public bool UseCurve { get; set; } = false;
        public float Expo { get; set; } = 0.0f;
        public float AntiDeadzone { get; set; } = 0.0f;
        public float EmaAlpha { get; set; } = 0.5f;
        public float ScaleX { get; set; } = 1.0f;
        public float ScaleY { get; set; } = 1.0f;
    }
}