using System;
using System.IO;
using System.Linq;

namespace WootMouseRemap.Security
{
    public static class SecurePathValidator
    {
        private static readonly char[] InvalidChars = Path.GetInvalidPathChars().Concat(new[] { '\0' }).ToArray();

        public static string? ValidateFilePath(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) return null;
            if (path.IndexOfAny(InvalidChars) >= 0) return null;
            if (path.Contains("..")) return null;
            
            try
            {
                var fullPath = Path.GetFullPath(path);
                return !string.IsNullOrEmpty(fullPath) ? fullPath : null;
            }
            catch
            {
                return null;
            }
        }

        public static bool IsValidFilePath(string path)
        {
            return ValidateFilePath(path) != null;
        }

        public static string SafeCombinePath(string path1, string path2)
        {
            var validPath1 = ValidateFilePath(path1);
            if (validPath1 == null) throw new ArgumentException("Invalid base path");

            // Treat path2 as a single filename or relative component; sanitize it if necessary
            var sanitizedComponent = SanitizePathComponent(path2);
            if (string.IsNullOrEmpty(sanitizedComponent)) throw new ArgumentException("Invalid second path component");

            return Path.Combine(validPath1, sanitizedComponent);
        }

        public static string SanitizePathComponent(string component)
        {
            if (string.IsNullOrWhiteSpace(component)) return string.Empty;
            
            var sanitized = component;
            foreach (var invalidChar in InvalidChars)
            {
                sanitized = sanitized.Replace(invalidChar, '_');
            }
            
            return sanitized.Replace("..", "_");
        }

        public static bool EnsureSafeDirectory(string directoryPath)
        {
            var validPath = ValidateFilePath(directoryPath);
            if (validPath == null) return false;
            
            // Additional security: Ensure directory is within allowed base paths
            var allowedBasePaths = new[] { 
                Path.GetFullPath("Logs"),
                Path.GetFullPath("Profiles"),
                Path.GetFullPath("Backups"),
                Path.GetFullPath(".") // Current directory
            };
            
            var fullPath = Path.GetFullPath(validPath);
            if (!allowedBasePaths.Any(basePath => fullPath.StartsWith(basePath, StringComparison.OrdinalIgnoreCase)))
            {
                return false;
            }
            
            try
            {
                if (!Directory.Exists(validPath))
                    Directory.CreateDirectory(validPath);
                return true;
            }
            catch (UnauthorizedAccessException)
            {
                return false;
            }
            catch (DirectoryNotFoundException)
            {
                return false;
            }
            catch (IOException)
            {
                return false;
            }
            catch
            {
                return false;
            }
        }
    }
}