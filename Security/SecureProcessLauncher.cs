using System;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace WootMouseRemap.Security
{
    public static class SecureProcessLauncher
    {
        private static readonly string[] AllowedExtensions = { ".txt", ".json", ".log" };

        public static bool TryLaunchFile(string filePath)
        {
            var validatedPath = SecurePathValidator.ValidateFilePath(filePath);
            if (validatedPath == null)
            {
                return false;
            }

            if (!File.Exists(validatedPath))
            {
                return false;
            }

            var extension = Path.GetExtension(validatedPath).ToLowerInvariant();
            if (!AllowedExtensions.Contains(extension))
            {
                return false;
            }

            // Additional security: Ensure file is within allowed directories
            var allowedBasePaths = new[] { 
                Path.GetFullPath("Logs"),
                Path.GetFullPath("Profiles"),
                Path.GetFullPath("Backups")
            };
            
            var fullPath = Path.GetFullPath(validatedPath);
            if (!allowedBasePaths.Any(basePath => fullPath.StartsWith(basePath, StringComparison.OrdinalIgnoreCase)))
            {
                return false;
            }

            try
            {
                var startInfo = new ProcessStartInfo
                {
                    FileName = "notepad.exe", // Use specific application instead of shell execute
                    Arguments = $"\"{validatedPath}\"", // Properly quote the path
                    UseShellExecute = false,
                    CreateNoWindow = false
                };
                
                Process.Start(startInfo);
                return true;
            }
            catch (System.ComponentModel.Win32Exception ex)
            {
                // Log specific error for debugging
                System.Diagnostics.Debug.WriteLine($"Failed to launch file: {ex.Message}");
                return false;
            }
            catch (InvalidOperationException ex)
            {
                System.Diagnostics.Debug.WriteLine($"Invalid operation: {ex.Message}");
                return false;
            }
            catch
            {
                return false;
            }
        }
    }
}