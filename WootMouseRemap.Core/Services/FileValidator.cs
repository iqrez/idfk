using System.Text.Json;
using WootMouseRemap.Features;
using WootMouseRemap.Security;

namespace WootMouseRemap.Core.Services;

/// <summary>
/// Validates imported files for security and data integrity
/// </summary>
public static class FileValidator
{
    private static readonly string[] AllowedExtensions = { ".json", ".txt" };
    private const long MaxFileSize = 10 * 1024 * 1024; // 10MB
    
    public static ValidationResult ValidatePatternFile(string filePath)
    {
        var basicValidation = ValidateBasicFile(filePath);
        if (!basicValidation.IsValid)
            return basicValidation;
            
        try
        {
            var json = File.ReadAllText(filePath);
            var options = new JsonSerializerOptions 
            { 
                MaxDepth = 5,
                PropertyNameCaseInsensitive = false,
                AllowTrailingCommas = false
            };
            var pattern = SecureJsonDeserializer.DeserializeSecure<AntiRecoilPattern>(json);
            
            if (pattern == null)
                return ValidationResult.Invalid("File does not contain a valid pattern");
                
            if (string.IsNullOrWhiteSpace(pattern.Name))
                return ValidationResult.Invalid("Pattern name is required");
                
            if (pattern.Samples == null || pattern.Samples.Count == 0)
                return ValidationResult.Invalid("Pattern must contain samples");
                
            if (!SecureArithmetic.IsWithinBounds(pattern.Samples.Count, 0, 50000))
                return ValidationResult.Invalid("Pattern contains too many samples (max: 50,000)");
                
            // Validate sample values
            foreach (var sample in pattern.Samples)
            {
                if (float.IsNaN(sample.Dx) || float.IsInfinity(sample.Dx) ||
                    float.IsNaN(sample.Dy) || float.IsInfinity(sample.Dy))
                    return ValidationResult.Invalid("Pattern contains invalid sample values");
                    
                if (Math.Abs(sample.Dx) > 1000 || Math.Abs(sample.Dy) > 1000)
                    return ValidationResult.Invalid("Pattern contains extreme sample values");
            }
            
            return ValidationResult.Valid();
        }
        catch (JsonException ex)
        {
            return ValidationResult.Invalid($"Invalid JSON format: {ex.Message}");
        }
        catch (Exception ex)
        {
            return ValidationResult.Invalid($"File validation error: {ex.Message}");
        }
    }
    
    public static ValidationResult ValidateSettingsFile<T>(string filePath) where T : class
    {
        var basicValidation = ValidateBasicFile(filePath);
        if (!basicValidation.IsValid)
            return basicValidation;
            
        try
        {
            var json = File.ReadAllText(filePath);
            var options = new JsonSerializerOptions 
            { 
                MaxDepth = 5,
                PropertyNameCaseInsensitive = false,
                AllowTrailingCommas = false
            };
            var settings = SecureJsonDeserializer.DeserializeSecure<T>(json);
            
            if (settings == null)
                return ValidationResult.Invalid("File does not contain valid settings");
                
            return ValidationResult.Valid();
        }
        catch (JsonException ex)
        {
            return ValidationResult.Invalid($"Invalid JSON format: {ex.Message}");
        }
        catch (Exception ex)
        {
            return ValidationResult.Invalid($"Settings validation error: {ex.Message}");
        }
    }
    
    private static ValidationResult ValidateBasicFile(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
            return ValidationResult.Invalid("File path is required");
            
        if (!File.Exists(filePath))
            return ValidationResult.Invalid("File does not exist");
            
        // Check file extension
        var extension = Path.GetExtension(filePath).ToLowerInvariant();
        if (!AllowedExtensions.Contains(extension))
            return ValidationResult.Invalid($"File type not allowed. Allowed types: {string.Join(", ", AllowedExtensions)}");
            
        // Check file size
        var fileInfo = new FileInfo(filePath);
        if (fileInfo.Length > MaxFileSize)
            return ValidationResult.Invalid($"File too large. Maximum size: {MaxFileSize / (1024 * 1024)}MB");
            
        // Basic path traversal protection
        var fullPath = Path.GetFullPath(filePath);
        var allowedDirectory = Path.GetFullPath(Environment.CurrentDirectory);
        if (!fullPath.StartsWith(allowedDirectory, StringComparison.OrdinalIgnoreCase))
            return ValidationResult.Invalid("File path not allowed");
            
        return ValidationResult.Valid();
    }
    
    public static ValidationResult ValidateExportFilePath(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
            return ValidationResult.Invalid("File path is required");
            
        // Check file extension
        var extension = Path.GetExtension(filePath).ToLowerInvariant();
        if (!AllowedExtensions.Contains(extension))
            return ValidationResult.Invalid($"File type not allowed. Allowed types: {string.Join(", ", AllowedExtensions)}");
            
        // For export, we allow writing to any path the user specifies (within reason)
        // but we should still prevent obvious path traversal attempts
        try
        {
            var fullPath = Path.GetFullPath(filePath);
            var currentDir = Path.GetFullPath(Environment.CurrentDirectory);
            
            // Allow writing to current directory and subdirectories
            // But prevent going up too many levels (basic protection)
            var relativePath = Path.GetRelativePath(currentDir, fullPath);
            if (relativePath.StartsWith("..\\..\\..\\", StringComparison.OrdinalIgnoreCase) || 
                relativePath.StartsWith("../../../", StringComparison.OrdinalIgnoreCase))
            {
                return ValidationResult.Invalid("File path goes too far outside the allowed directory");
            }
            
            // Ensure the directory exists or can be created
            var directory = Path.GetDirectoryName(fullPath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                // For export, we'll allow creating directories, but limit depth
                var depth = relativePath.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar).Length;
                if (depth > 10)
                {
                    return ValidationResult.Invalid("Directory path too deep");
                }
            }
        }
        catch (Exception ex)
        {
            return ValidationResult.Invalid($"Invalid file path: {ex.Message}");
        }
            
        return ValidationResult.Valid();
    }
}

public class ValidationResult
{
    public bool IsValid { get; private set; }
    public string? ErrorMessage { get; private set; }
    
    private ValidationResult(bool isValid, string? errorMessage = null)
    {
        IsValid = isValid;
        ErrorMessage = errorMessage;
    }
    
    public static ValidationResult Valid() => new(true);
    public static ValidationResult Invalid(string errorMessage) => new(false, errorMessage);
}