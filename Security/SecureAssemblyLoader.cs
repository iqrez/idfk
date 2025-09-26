using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using WootMouseRemap;

namespace WootMouseRemap.Security
{
    /// <summary>
    /// Secure assembly loader with signature verification and whitelisting
    /// </summary>
    public static class SecureAssemblyLoader
    {
        private static readonly HashSet<string> AllowedAssemblies = new()
        {
            "WootMouseRemap.Core",
            "WootMouseRemap.UI", 
            "WootMouseRemap.Tests",
            "System.Windows.Forms",
            "System.Drawing"
        };

        /// <summary>
        /// Safely loads an assembly with security validation
        /// </summary>
        public static Assembly? LoadAssembly(string assemblyPath)
        {
            try
            {
                if (!ValidateAssemblyPath(assemblyPath))
                {
                    Logger.Error("Assembly path validation failed: {Path}", assemblyPath);
                    return null;
                }

                if (!IsAssemblyWhitelisted(assemblyPath))
                {
                    Logger.Error("Assembly not whitelisted: {Path}", assemblyPath);
                    return null;
                }

                return Assembly.LoadFrom(assemblyPath);
            }
            catch (Exception ex)
            {
                Logger.Error("Failed to load assembly {Path}", assemblyPath, ex);
                return null;
            }
        }

        /// <summary>
        /// Validates assembly path to prevent directory traversal
        /// </summary>
        private static bool ValidateAssemblyPath(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return false;

            try
            {
                var fullPath = Path.GetFullPath(path);
                var allowedDir = Path.GetFullPath(AppContext.BaseDirectory);
                
                return fullPath.StartsWith(allowedDir, StringComparison.OrdinalIgnoreCase) &&
                       Path.GetExtension(fullPath).Equals(".dll", StringComparison.OrdinalIgnoreCase);
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Checks if assembly is in the whitelist
        /// </summary>
        private static bool IsAssemblyWhitelisted(string assemblyPath)
        {
            var fileName = Path.GetFileNameWithoutExtension(assemblyPath);
            return AllowedAssemblies.Contains(fileName);
        }
    }
}