using System.Text.Json;
using WootMouseRemap.Core.Mapping;
using Microsoft.Extensions.Logging;
using WootMouseRemap.Security;

namespace WootMouseRemap.Core.Services;

/// <summary>
/// Service for managing input mapping profiles with JSON persistence
/// </summary>
public sealed class ProfileService
{
    private readonly ILogger<ProfileService> _logger;
    private readonly string _profilesPath;
    private readonly Dictionary<string, InputMappingProfile> _profiles = new();
    
    public ProfileService(ILogger<ProfileService> logger, string profilesPath = "Profiles")
    {
        _logger = logger;
        _profilesPath = profilesPath;
        try
        {
            var asmPath = typeof(ProfileService).Assembly.Location ?? "<unknown>";
            if (File.Exists(asmPath))
            {
                var info = new FileInfo(asmPath);
                _logger.LogInformation("ProfileService ctor - assembly: {AssemblyPath} (LastWrite: {LastWrite})", asmPath, info.LastWriteTimeUtc);
            }
            else
            {
                _logger.LogInformation("ProfileService ctor - assembly path not found: {AssemblyPath}", asmPath);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to inspect ProfileService assembly location");
        }
        // Try to ensure the profiles directory exists using secure validator; if it fails, fall back to creating it directly
        if (!SecurePathValidator.EnsureSafeDirectory(_profilesPath))
        {
            try
            {
                Directory.CreateDirectory(_profilesPath);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to create or validate profiles directory: {ProfilesPath}", _profilesPath);
            }
        }
        LoadProfiles();
    }
    
    public IReadOnlyDictionary<string, InputMappingProfile> GetAllProfiles() => _profiles;
    
    public InputMappingProfile? GetProfile(string name) => 
        _profiles.TryGetValue(name, out var profile) ? profile : null;
    
    public InputMappingProfile GetDefaultProfile()
    {
        if (_profiles.TryGetValue("Default", out var defaultProfile))
            return defaultProfile;
            
        // Create default profile
        var profile = new InputMappingProfile
        {
            Name = "Default",
            Description = "Default keyboard/mouse to Xbox 360 controller mapping",
            KeyboardMap = new Dictionary<int, ControllerInput>
            {
                [0x57] = ControllerInput.LeftStickUp,    // W
                [0x53] = ControllerInput.LeftStickDown,  // S
                [0x41] = ControllerInput.LeftStickLeft,  // A
                [0x44] = ControllerInput.LeftStickRight, // D
                [0x20] = ControllerInput.A,              // Space
                [0x0D] = ControllerInput.Start,          // Enter
                [0x1B] = ControllerInput.Back,           // Escape
                [0x10] = ControllerInput.LeftBumper,     // Shift
                [0x11] = ControllerInput.RightBumper,    // Ctrl
            },
            MouseConfig = new MouseMappingConfig
            {
                EnableMouseToRightStick = true,
                LeftClickMapping = MouseButton.RightTrigger,
                RightClickMapping = MouseButton.LeftTrigger,
                MiddleClickMapping = MouseButton.B
            },
            CurveSettings = new CurveSettings
            {
                Sensitivity = 0.35f,
                Expo = 0.6f,
                AntiDeadzone = 0.05f,
                MaxSpeed = 1.0f,
                EmaAlpha = 0.35f,
                ScaleX = 1.0f,
                ScaleY = 1.0f,
                JitterFloor = 0.0f
            }
        };
        
        SaveProfile(profile);
        return profile;
    }
    
    public bool SaveProfile(InputMappingProfile profile)
    {
        if (profile?.Name == null) return false;
        
        try
        {
            var safeName = SecurePathValidator.SanitizePathComponent(profile.Name);
            if (string.IsNullOrEmpty(safeName)) return false;
            
            _profiles[profile.Name] = profile;
            
            var json = JsonSerializer.Serialize(profile, new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });
            
            var filePath = SecurePathValidator.SafeCombinePath(_profilesPath, $"{safeName}.json");
            if (filePath == null) return false;
            
            File.WriteAllText(filePath, json);
            _logger.LogInformation("Saved profile: {ProfileName}", profile.Name);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save profile: {ProfileName}", profile.Name);
            return false;
        }
    }
    
    public void DeleteProfile(string name)
    {
        if (name == "Default") return; // Protect default profile
        
        try
        {
            _profiles.Remove(name);
            var safeName = SecurePathValidator.SanitizePathComponent(name);
            if (safeName != null)
            {
                var filePath = SecurePathValidator.SafeCombinePath(_profilesPath, $"{safeName}.json");
                if (filePath != null && File.Exists(filePath))
                    File.Delete(filePath);
            }
                
            _logger.LogInformation("Deleted profile: {ProfileName}", name);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete profile: {ProfileName}", name);
        }
    }
    
    public InputMappingProfile CreateGameProfile(string gameName)
    {
        var profile = gameName.ToLowerInvariant() switch
        {
            "cs2" or "counter-strike" => CreateCS2Profile(),
            "valorant" => CreateValorantProfile(),
            "apex" or "apex legends" => CreateApexProfile(),
            _ => GetDefaultProfile()
        };
        
        SaveProfile(profile);
        return profile;
    }
    
    private void LoadProfiles()
    {
        try
        {
            if (!Directory.Exists(_profilesPath)) return;
            
            foreach (var file in Directory.GetFiles(_profilesPath, "*.json"))
            {
                try
                {
                    var json = File.ReadAllText(file);
                    InputMappingProfile profile;
                    var options = new JsonSerializerOptions
                    {
                        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                        MaxDepth = 5,
                        PropertyNameCaseInsensitive = false,
                        AllowTrailingCommas = false
                    };
                    profile = SecureJsonDeserializer.DeserializeSecure<InputMappingProfile>(json) ?? new InputMappingProfile();
                    
                    if (profile != null)
                    {
                        _profiles[profile.Name] = profile;
                        _logger.LogDebug("Loaded profile: {ProfileName}", profile.Name);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to load profile from: {FilePath}", file);
                }
            }
            
            _logger.LogInformation("Loaded {Count} profiles", _profiles.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load profiles from directory: {ProfilesPath}", _profilesPath);
        }
    }
    
    private static InputMappingProfile CreateCS2Profile()
    {
        return new InputMappingProfile
        {
            Name = "CS2",
            Description = "Counter-Strike 2 optimized mapping",
            KeyboardMap = new Dictionary<int, ControllerInput>
            {
                [0x57] = ControllerInput.LeftStickUp,    // W
                [0x53] = ControllerInput.LeftStickDown,  // S
                [0x41] = ControllerInput.LeftStickLeft,  // A
                [0x44] = ControllerInput.LeftStickRight, // D
                [0x20] = ControllerInput.A,              // Space (jump)
                [0x11] = ControllerInput.B,              // Ctrl (crouch)
                [0x10] = ControllerInput.LeftBumper,     // Shift (walk)
                [0x52] = ControllerInput.X,              // R (reload)
                [0x47] = ControllerInput.Y,              // G (drop)
            },
            MouseConfig = new MouseMappingConfig
            {
                EnableMouseToRightStick = true,
                LeftClickMapping = MouseButton.RightTrigger,
                RightClickMapping = MouseButton.LeftTrigger,
                MiddleClickMapping = MouseButton.B
            },
            CurveSettings = new CurveSettings
            {
                Sensitivity = 0.25f,
                Expo = 0.4f,
                AntiDeadzone = 0.02f,
                EmaAlpha = 0.2f,
                ScaleX = 1.0f,
                ScaleY = 0.9f
            }
        };
    }
    
    private static InputMappingProfile CreateValorantProfile()
    {
        return new InputMappingProfile
        {
            Name = "Valorant",
            Description = "Valorant optimized mapping",
            KeyboardMap = new Dictionary<int, ControllerInput>
            {
                [0x57] = ControllerInput.LeftStickUp,
                [0x53] = ControllerInput.LeftStickDown,
                [0x41] = ControllerInput.LeftStickLeft,
                [0x44] = ControllerInput.LeftStickRight,
                [0x20] = ControllerInput.A,
                [0x11] = ControllerInput.B,
                [0x10] = ControllerInput.LeftBumper,
            },
            MouseConfig = new MouseMappingConfig
            {
                EnableMouseToRightStick = true,
                LeftClickMapping = MouseButton.RightTrigger,
                RightClickMapping = MouseButton.LeftTrigger,
                MiddleClickMapping = MouseButton.B
            },
            CurveSettings = new CurveSettings
            {
                Sensitivity = 0.3f,
                Expo = 0.5f,
                AntiDeadzone = 0.03f,
                EmaAlpha = 0.25f,
                ScaleX = 1.0f,
                ScaleY = 0.95f
            }
        };
    }
    
    private static InputMappingProfile CreateApexProfile()
    {
        return new InputMappingProfile
        {
            Name = "Apex Legends",
            Description = "Apex Legends optimized mapping",
            KeyboardMap = new Dictionary<int, ControllerInput>
            {
                [0x57] = ControllerInput.LeftStickUp,
                [0x53] = ControllerInput.LeftStickDown,
                [0x41] = ControllerInput.LeftStickLeft,
                [0x44] = ControllerInput.LeftStickRight,
                [0x20] = ControllerInput.A,
                [0x11] = ControllerInput.B,
                [0x10] = ControllerInput.LeftBumper,
            },
            MouseConfig = new MouseMappingConfig
            {
                EnableMouseToRightStick = true,
                LeftClickMapping = MouseButton.RightTrigger,
                RightClickMapping = MouseButton.LeftTrigger,
                MiddleClickMapping = MouseButton.B
            },
            CurveSettings = new CurveSettings
            {
                Sensitivity = 0.4f,
                Expo = 0.7f,
                AntiDeadzone = 0.08f,
                EmaAlpha = 0.4f,
                ScaleX = 1.0f,
                ScaleY = 1.0f
            }
        };
    }
}