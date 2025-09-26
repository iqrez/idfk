using System.Text.Json;
// Logger is in WootMouseRemap namespace
using WootMouseRemap.Features;

namespace WootMouseRemap.Core.Services;

/// <summary>
/// Handles migration of settings between versions
/// </summary>
public static class SettingsMigrator
{
    private const int CurrentVersion = 2;
    
    public static AntiRecoilSettings MigrateSettings(string json)
    {
        try
        {
            // Validate JSON structure before parsing to prevent injection attacks
            if (!IsValidSettingsJson(json))
            {
                Logger.Error("Invalid settings JSON structure detected, using defaults");
                return new AntiRecoilSettings();
            }
            
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;
            
            var version = root.TryGetProperty("Version", out var versionElement) ? versionElement.GetInt32() : 1;
            
            return version switch
            {
                1 => MigrateFromV1(root),
                2 => DeserializeV2(json),
                _ => throw new NotSupportedException($"Settings version {version} is not supported")
            };
            
            AntiRecoilSettings DeserializeV2(string json)
            {
                var options = new JsonSerializerOptions 
                { 
                    MaxDepth = 5,
                    PropertyNameCaseInsensitive = false,
                    AllowTrailingCommas = false
                };
                try 
                {
                    return JsonSerializer.Deserialize<AntiRecoilSettings>(json, options) ?? new AntiRecoilSettings();
                }
                catch (JsonException) 
                {
                    return new AntiRecoilSettings(); // Safe fallback
                }
            }
        }
        catch (Exception ex)
        {
            Logger.Error("Settings migration failed, using defaults", ex);
            return new AntiRecoilSettings();
        }
    }
    
    private static AntiRecoilSettings MigrateFromV1(JsonElement root)
    {
        Logger.Info("Migrating settings from version 1 to version 2");
        
        var settings = new AntiRecoilSettings
        {
            Enabled = root.TryGetProperty("Enabled", out var enabled) && enabled.ValueKind == JsonValueKind.True,
            Strength = root.TryGetProperty("Strength", out var strength) && strength.TryGetSingle(out var strengthVal) ? strengthVal : 0.5f,
            ActivationDelayMs = root.TryGetProperty("ActivationDelayMs", out var delay) && delay.TryGetInt32(out var delayVal) ? delayVal : 50,
            VerticalThreshold = root.TryGetProperty("VerticalThreshold", out var threshold) && threshold.TryGetSingle(out var thresholdVal) ? thresholdVal : 2.0f,
            HorizontalCompensation = root.TryGetProperty("HorizontalCompensation", out var hComp) && hComp.TryGetSingle(out var hCompVal) ? hCompVal : 0.0f,
            AdaptiveCompensation = root.TryGetProperty("AdaptiveCompensation", out var adaptive) && adaptive.ValueKind == JsonValueKind.True,
            
            // New V2 settings with defaults
            MaxTickCompensation = 10.0f,
            MaxTotalCompensation = 100.0f,
            CooldownMs = 0,
            DecayPerMs = 0.0f
        };
        
        Logger.Info("Settings migration from V1 to V2 completed successfully");
        return settings;
    }
    
    public static AntiRecoilPattern MigratePattern(string json)
    {
        try
        {
            // Validate JSON structure before parsing to prevent injection attacks
            if (!IsValidPatternJson(json))
            {
                Logger.Error("Invalid pattern JSON structure detected");
                throw new InvalidOperationException("Invalid pattern JSON structure");
            }
            
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;
            
            var version = root.TryGetProperty("Version", out var versionElement) ? versionElement.GetInt32() : 1;
            
            return version switch
            {
                1 => MigratePatternFromV1(root),
                _ => DeserializePattern(json)
            };
            
            AntiRecoilPattern DeserializePattern(string json)
            {
                var options = new JsonSerializerOptions 
                { 
                    MaxDepth = 5,
                    PropertyNameCaseInsensitive = false,
                    AllowTrailingCommas = false
                };
                try 
                {
                    return JsonSerializer.Deserialize<AntiRecoilPattern>(json, options) ?? new AntiRecoilPattern();
                }
                catch (JsonException) 
                {
                    return new AntiRecoilPattern(); // Safe fallback
                }
            }
        }
        catch (Exception ex)
        {
            Logger.Error("Pattern migration failed", ex);
            throw;
        }
    }
    
    private static AntiRecoilPattern MigratePatternFromV1(JsonElement root)
    {
        Logger.Info("Migrating pattern from version 1");
        
        var pattern = new AntiRecoilPattern
        {
            Version = 1,
            Name = root.TryGetProperty("Name", out var name) ? name.GetString() ?? "Migrated Pattern" : "Migrated Pattern",
            CreatedUtc = root.TryGetProperty("CreatedUtc", out var created) ? created.GetDateTime() : DateTime.UtcNow,
            Notes = root.TryGetProperty("Notes", out var notes) ? notes.GetString() : null,
            Tags = new List<string> { "migrated" },
            Samples = new List<AntiRecoilSample>()
        };
        
        if (root.TryGetProperty("Samples", out var samplesElement) && samplesElement.ValueKind == JsonValueKind.Array)
        {
            foreach (var sampleElement in samplesElement.EnumerateArray())
            {
                var sample = new AntiRecoilSample
                {
                    Dx = sampleElement.TryGetProperty("Dx", out var dx) && dx.TryGetSingle(out var dxVal) ? dxVal : 0f,
                    Dy = sampleElement.TryGetProperty("Dy", out var dy) && dy.TryGetSingle(out var dyVal) ? dyVal : 0f
                };
                pattern.Samples.Add(sample);
            }
        }
        
        Logger.Info("Pattern migration completed: {SampleCount} samples migrated", pattern.Samples.Count);
        return pattern;
    }
    
    /// <summary>
    /// Validates that the JSON contains only expected properties for AntiRecoilSettings
    /// to prevent deserialization attacks
    /// </summary>
    private static bool IsValidSettingsJson(string json)
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;
            
            if (root.ValueKind != JsonValueKind.Object)
                return false;
            
            // Define allowed properties for AntiRecoilSettings
            var allowedProperties = new HashSet<string>
            {
                "Enabled", "Strength", "ActivationDelayMs", "VerticalThreshold", 
                "HorizontalCompensation", "AdaptiveCompensation", "MaxTickCompensation",
                "MaxTotalCompensation", "CooldownMs", "DecayPerMs", "Version"
            };
            
            foreach (var property in root.EnumerateObject())
            {
                if (!allowedProperties.Contains(property.Name))
                    return false;
                
                // Validate property types
                switch (property.Name)
                {
                    case "Enabled":
                        if (property.Value.ValueKind != JsonValueKind.True && property.Value.ValueKind != JsonValueKind.False)
                            return false;
                        break;
                    case "Strength":
                    case "VerticalThreshold":
                    case "HorizontalCompensation":
                    case "MaxTickCompensation":
                    case "MaxTotalCompensation":
                    case "DecayPerMs":
                        if (property.Value.ValueKind != JsonValueKind.Number)
                            return false;
                        break;
                    case "ActivationDelayMs":
                    case "CooldownMs":
                    case "Version":
                        if (property.Value.ValueKind != JsonValueKind.Number || !property.Value.TryGetInt32(out _))
                            return false;
                        break;
                    case "AdaptiveCompensation":
                        if (property.Value.ValueKind != JsonValueKind.True && property.Value.ValueKind != JsonValueKind.False)
                            return false;
                        break;
                }
            }
            
            return true;
        }
        catch
        {
            return false;
        }
    }
    
    /// <summary>
    /// Validates that the JSON contains only expected properties for AntiRecoilPattern
    /// to prevent deserialization attacks
    /// </summary>
    private static bool IsValidPatternJson(string json)
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;
            
            if (root.ValueKind != JsonValueKind.Object)
                return false;
            
            // Define allowed properties for AntiRecoilPattern
            var allowedProperties = new HashSet<string>
            {
                "Version", "Name", "CreatedUtc", "Notes", "Tags", "Samples"
            };
            
            foreach (var property in root.EnumerateObject())
            {
                if (!allowedProperties.Contains(property.Name))
                    return false;
                
                // Validate specific property types
                switch (property.Name)
                {
                    case "Version":
                        if (property.Value.ValueKind != JsonValueKind.Number || !property.Value.TryGetInt32(out _))
                            return false;
                        break;
                    case "Name":
                        if (property.Value.ValueKind != JsonValueKind.String)
                            return false;
                        break;
                    case "CreatedUtc":
                        if (property.Value.ValueKind != JsonValueKind.String)
                            return false;
                        break;
                    case "Notes":
                        if (property.Value.ValueKind != JsonValueKind.String && property.Value.ValueKind != JsonValueKind.Null)
                            return false;
                        break;
                    case "Tags":
                        if (property.Value.ValueKind != JsonValueKind.Array)
                            return false;
                        // Validate each tag is a string
                        foreach (var tag in property.Value.EnumerateArray())
                        {
                            if (tag.ValueKind != JsonValueKind.String)
                                return false;
                        }
                        break;
                    case "Samples":
                        if (property.Value.ValueKind != JsonValueKind.Array)
                            return false;
                        // Validate each sample has correct structure
                        foreach (var sample in property.Value.EnumerateArray())
                        {
                            if (sample.ValueKind != JsonValueKind.Object)
                                return false;
                            if (!sample.TryGetProperty("Dx", out _) || !sample.TryGetProperty("Dy", out _))
                                return false;
                        }
                        break;
                }
            }
            
            return true;
        }
        catch
        {
            return false;
        }
    }
}