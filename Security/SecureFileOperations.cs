using System;
using System.IO;
using System.Text;
using System.Text.Json;

namespace WootMouseRemap.Security
{
    public static class SecureFileOperations
    {
        private static readonly string[] AllowedDirectories = { "Logs", "Profiles", "Backups", "." };
        
        public static bool TryReadAllText(string filePath, out string content)
        {
            content = string.Empty;
            
            var validatedPath = SecurePathValidator.ValidateFilePath(filePath);
            if (validatedPath == null)
            {
                return false;
            }
            
            if (!IsPathAllowed(validatedPath))
            {
                return false;
            }
            
            try
            {
                content = File.ReadAllText(validatedPath, Encoding.UTF8);
                return true;
            }
            catch (UnauthorizedAccessException)
            {
                return false;
            }
            catch (FileNotFoundException)
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
        
        public static bool TryWriteAllText(string filePath, string content)
        {
            var validatedPath = SecurePathValidator.ValidateFilePath(filePath);
            if (validatedPath == null)
            {
                return false;
            }
            
            if (!IsPathAllowed(validatedPath))
            {
                return false;
            }
            
            try
            {
                var directory = Path.GetDirectoryName(validatedPath);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }
                
                File.WriteAllText(validatedPath, content, Encoding.UTF8);
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
        
        public static T? TryDeserializeJson<T>(string filePath) where T : class
        {
            if (!TryReadAllText(filePath, out var json))
            {
                return null;
            }
            
            return SecureJsonDeserializer.TryDeserialize<T>(json);
        }
        
        public static bool TrySerializeJson<T>(string filePath, T obj) where T : class
        {
            try
            {
                var options = new JsonSerializerOptions
                {
                    WriteIndented = true,
                    MaxDepth = 10
                };
                
                var json = JsonSerializer.Serialize(obj, options);
                return TryWriteAllText(filePath, json);
            }
            catch
            {
                return false;
            }
        }
        
        private static bool IsPathAllowed(string path)
        {
            try
            {
                var fullPath = Path.GetFullPath(path);
                
                foreach (var allowedDir in AllowedDirectories)
                {
                    var allowedFullPath = Path.GetFullPath(allowedDir);
                    if (fullPath.StartsWith(allowedFullPath, StringComparison.OrdinalIgnoreCase))
                    {
                        return true;
                    }
                }
                
                return false;
            }
            catch
            {
                return false;
            }
        }
    }
}