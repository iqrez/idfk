using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
// Logger is in WootMouseRemap namespace

namespace WootMouseRemap.Core
{
    /// <summary>
    /// Manages backup and restore operations for anti-recoil data
    /// </summary>
    public class BackupManager
    {
        private const string BackupDirectory = "Backups";
        private const string PatternsBackupPrefix = "patterns_backup_";
        private const string SettingsBackupPrefix = "settings_backup_";
        private const string UiStateBackupPrefix = "ui_state_backup_";
        private const int MaxBackups = 10;

        /// <summary>
        /// Sanitizes strings for logging to prevent log injection attacks
        /// </summary>
        private static string SanitizeForLogging(string input)
        {
            if (string.IsNullOrEmpty(input))
                return "[null_or_empty]";

            // Remove control characters and limit length
            var sanitized = Regex.Replace(input, @"[\r\n\t\x00-\x1F\x7F-\x9F]", "_", RegexOptions.None, TimeSpan.FromMilliseconds(100));
            if (sanitized.Length > 200)
                sanitized = sanitized.Substring(0, 200) + "...[truncated]";
            
            return sanitized;
        }

        /// <summary>
        /// Validates file path to prevent path traversal attacks
        /// </summary>
        private static bool IsValidFilePath(string filePath, out string sanitizedPath)
        {
            sanitizedPath = "";
            
            if (string.IsNullOrWhiteSpace(filePath))
                return false;

            try
            {
                // Get absolute paths
                var fullPath = Path.GetFullPath(filePath);
                var currentDir = Path.GetFullPath(Directory.GetCurrentDirectory());
                
                // Ensure the path is within the current directory
                if (!fullPath.StartsWith(currentDir, StringComparison.OrdinalIgnoreCase))
                    return false;

                // Additional checks for suspicious patterns
                if (filePath.Contains("..") || 
                    filePath.Contains("~") ||
                    Path.IsPathRooted(filePath) && !fullPath.StartsWith(currentDir, StringComparison.OrdinalIgnoreCase))
                    return false;

                sanitizedPath = fullPath;
                return true;
            }
            catch
            {
                return false;
            }
        }

        public static void CreatePatternsBackup(string sourceFile)
        {
            try
            {
                // Ensure backup directory exists even if source is missing so callers/tests can safely enumerate it
                Directory.CreateDirectory(BackupDirectory);

                if (!File.Exists(sourceFile)) return;

                // Validate source file path to prevent path traversal
                if (!IsValidFilePath(sourceFile, out string fullSourcePath))
                {
                    Logger.Error("Invalid source file path: {SourceFile}", SanitizeForLogging(sourceFile));
                    return;
                }

                var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                var backupFileName = $"{PatternsBackupPrefix}{timestamp}.json";
                var backupPath = Path.Combine(BackupDirectory, backupFileName);

                File.Copy(fullSourcePath, backupPath, true);

                Logger.Info("Created patterns backup: {FileName}", SanitizeForLogging(backupFileName));

                // Clean up old backups
                CleanupOldBackups(PatternsBackupPrefix);
            }
            catch (Exception ex)
            {
                Logger.Error("Failed to create patterns backup", ex);
            }
        }

        public static void CreateSettingsBackup(string sourceFile)
        {
            try
            {
                // Ensure backup directory exists for callers/tests
                Directory.CreateDirectory(BackupDirectory);

                if (!File.Exists(sourceFile)) return;

                // Validate source file path to prevent path traversal
                if (!IsValidFilePath(sourceFile, out string fullSourcePath))
                {
                    Logger.Error("Invalid source file path: {SourceFile}", SanitizeForLogging(sourceFile));
                    return;
                }

                var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                var backupFileName = $"{SettingsBackupPrefix}{timestamp}.json";
                var backupPath = Path.Combine(BackupDirectory, backupFileName);

                File.Copy(fullSourcePath, backupPath, true);

                Logger.Info("Created settings backup: {FileName}", SanitizeForLogging(backupFileName));

                // Clean up old backups
                CleanupOldBackups(SettingsBackupPrefix);
            }
            catch (Exception ex)
            {
                Logger.Error("Failed to create settings backup", ex);
            }
        }

        public static void CreateUiStateBackup(UiStateData uiState)
        {
            try
            {
                Directory.CreateDirectory(BackupDirectory);

                var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                var backupFileName = $"{UiStateBackupPrefix}{timestamp}.json";
                var backupPath = Path.Combine(BackupDirectory, backupFileName);

                var json = JsonSerializer.Serialize(uiState, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(backupPath, json);

                Logger.Info("Created UI state backup: {FileName}", SanitizeForLogging(backupFileName));

                // Clean up old backups
                CleanupOldBackups(UiStateBackupPrefix);
            }
            catch (Exception ex)
            {
                Logger.Error("Failed to create UI state backup", ex);
            }
        }

        public static List<BackupInfo> GetAvailableBackups()
        {
            var backups = new List<BackupInfo>();

            try
            {
                if (!Directory.Exists(BackupDirectory)) return backups;

                var files = Directory.GetFiles(BackupDirectory, "*.json");

                foreach (var file in files)
                {
                    var fileName = Path.GetFileName(file);
                    var fileInfo = new FileInfo(file);

                    BackupType type;
                    if (fileName.StartsWith(PatternsBackupPrefix))
                        type = BackupType.Patterns;
                    else if (fileName.StartsWith(SettingsBackupPrefix))
                        type = BackupType.Settings;
                    else if (fileName.StartsWith(UiStateBackupPrefix))
                        type = BackupType.UiState;
                    else
                        continue; // Unknown backup type

                    backups.Add(new BackupInfo
                    {
                        FileName = fileName,
                        FilePath = file,
                        Type = type,
                        CreatedTime = fileInfo.CreationTime,
                        Size = fileInfo.Length
                    });
                }

                // Sort by creation time (newest first)
                backups.Sort((a, b) => b.CreatedTime.CompareTo(a.CreatedTime));
            }
            catch (Exception ex)
            {
                Logger.Error("Failed to get available backups", ex);
            }

            return backups;
        }

        public static bool RestoreBackup(BackupInfo backup, string targetFile)
        {
            try
            {
                if (!File.Exists(backup.FilePath))
                {
                    Logger.Error("Backup file not found: {FilePath}", SanitizeForLogging(backup.FilePath));
                    return false;
                }

                // Validate target file path to prevent path traversal
                if (!IsValidFilePath(targetFile, out string fullTargetPath))
                {
                    Logger.Error("Invalid target file path: {TargetFile}", SanitizeForLogging(targetFile));
                    return false;
                }

                // Create backup of current file before restoring
                if (File.Exists(fullTargetPath))
                {
                    var tempBackup = fullTargetPath + ".pre_restore_backup";
                    File.Copy(fullTargetPath, tempBackup, true);
                }

                File.Copy(backup.FilePath, fullTargetPath, true);
                Logger.Info("Restored backup {BackupFile} to {TargetFile}", SanitizeForLogging(backup.FileName), SanitizeForLogging(Path.GetFileName(fullTargetPath)));
                return true;
            }
            catch (Exception ex)
            {
                Logger.Error("Failed to restore backup {FileName}", SanitizeForLogging(backup.FileName), ex);
                return false;
            }
        }

        public static bool DeleteBackup(BackupInfo backup)
        {
            try
            {
                if (File.Exists(backup.FilePath))
                {
                    File.Delete(backup.FilePath);
                    Logger.Info("Deleted backup: {FileName}", SanitizeForLogging(backup.FileName));
                    return true;
                }
                return false;
            }
            catch (Exception ex)
            {
                Logger.Error("Failed to delete backup {FileName}", SanitizeForLogging(backup.FileName), ex);
                return false;
            }
        }

        public static void CreateFullBackup()
        {
            try
            {
                var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                var fullBackupDir = Path.Combine(BackupDirectory, $"full_backup_{timestamp}");
                Directory.CreateDirectory(fullBackupDir);

                // Backup all important files
                var filesToBackup = new[]
                {
                    "antirecoil_settings.json",
                    "Profiles/anti_recoil_patterns.json",
                    "ui_state.json"
                };

                foreach (var file in filesToBackup)
                {
                    if (File.Exists(file))
                    {
                        var targetPath = Path.Combine(fullBackupDir, Path.GetFileName(file));
                        File.Copy(file, targetPath, true);
                    }
                }

                // Create manifest
                var manifest = new FullBackupManifest
                {
                    CreatedTime = DateTime.UtcNow,
                    Version = "1.0",
                    Files = filesToBackup.Where(File.Exists).ToList()
                };

                var manifestJson = JsonSerializer.Serialize(manifest, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(Path.Combine(fullBackupDir, "manifest.json"), manifestJson);

                Logger.Info("Created full backup: {DirectoryName}", SanitizeForLogging(Path.GetFileName(fullBackupDir)));
            }
            catch (Exception ex)
            {
                Logger.Error("Failed to create full backup", ex);
            }
        }

        private static void CleanupOldBackups(string prefix)
        {
            try
            {
                if (!Directory.Exists(BackupDirectory)) return;

                var backupFiles = Directory.GetFiles(BackupDirectory, $"{prefix}*.json")
                    .Select(f => new FileInfo(f))
                    .OrderByDescending(f => f.CreationTime)
                    .ToList();

                // Keep only the most recent MaxBackups files
                for (int i = MaxBackups; i < backupFiles.Count; i++)
                {
                    try
                    {
                        backupFiles[i].Delete();
                        Logger.Info("Deleted old backup: {FileName}", SanitizeForLogging(backupFiles[i].Name));
                    }
                    catch (Exception ex)
                    {
                        Logger.Info("Failed to delete old backup {FileName}: {Message}", SanitizeForLogging(backupFiles[i].Name), SanitizeForLogging(ex.Message));
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Error("Failed to cleanup old backups", ex);
            }
        }
    }

    public class BackupInfo
    {
        public string FileName { get; set; } = "";
        public string FilePath { get; set; } = "";
        public BackupType Type { get; set; }
        public DateTime CreatedTime { get; set; }
        public long Size { get; set; }

        public string DisplayName => $"{Type} - {CreatedTime:yyyy-MM-dd HH:mm:ss} ({Size / 1024.0:F1} KB)";
    }

    public enum BackupType
    {
        Patterns,
        Settings,
        UiState,
        Full
    }

    public class FullBackupManifest
    {
        public DateTime CreatedTime { get; set; }
        public string Version { get; set; } = "";
        public List<string> Files { get; set; } = new();
    }

    /// <summary>
    /// UI state data for persistence
    /// </summary>
    public class UiStateData
    {
        public int Version { get; set; } = 1;
        public DateTime LastSaved { get; set; } = DateTime.UtcNow;

        // Form state
        public FormStateData? FormState { get; set; }

        // Tab preferences
        public int SelectedTabIndex { get; set; }

        // Display preferences
        public DisplayPreferences? DisplayPrefs { get; set; }

        // Recent patterns
        public List<string> RecentPatterns { get; set; } = new();

        // User preferences
        public UserPreferences? UserPrefs { get; set; }
    }

    public class FormStateData
    {
        public int Width { get; set; } = 900;
        public int Height { get; set; } = 700;
        public int X { get; set; } = -1; // -1 = center
        public int Y { get; set; } = -1; // -1 = center
        public bool Maximized { get; set; }
    }

    public class DisplayPreferences
    {
        public string TelemetryDisplayMode { get; set; } = "All";
        public string SimulationDisplayMode { get; set; } = "InputAndOutput";
        public bool TelemetryAutoScale { get; set; } = true;
        public bool ShowGrid { get; set; } = true;
        public bool ShowCrosshair { get; set; } = true;
    }

    public class UserPreferences
    {
        public bool ShowTooltips { get; set; } = true;
        public bool ShowWarnings { get; set; } = true;
        public bool AutoBackup { get; set; } = true;
        public int BackupIntervalHours { get; set; } = 24;
        public string Theme { get; set; } = "Dark";
        public bool EnableLowLevelHooks { get; set; } = false;
        public bool ComplianceMode { get; set; } = false;
        public bool AllowBackgroundCapture { get; set; } = false;
    }
}
