using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using WootMouseRemap.Core;

namespace WootMouseRemap.Tests
{
    /// <summary>
    /// Unit tests for BackupManager functionality
    /// </summary>
    [TestClass]
    public class BackupManagerTests
    {
        private const string TestBackupDir = "TestBackups";
        private const string TestSourceFile = "test_source.json";

        [TestInitialize]
        public void Setup()
        {
            // Clean up any existing test files
            CleanupTestFiles();

            // Create test source file
            File.WriteAllText(TestSourceFile, "{\"test\": \"data\"}");
        }

        [TestCleanup]
        public void Cleanup()
        {
            CleanupTestFiles();
        }

        private void CleanupTestFiles()
        {
            try
            {
                if (Directory.Exists(TestBackupDir))
                {
                    Directory.Delete(TestBackupDir, true);
                }
                if (File.Exists(TestSourceFile))
                {
                    File.Delete(TestSourceFile);
                }
            }
            catch
            {
                // Ignore cleanup errors
            }
        }

        [TestMethod]
        public void BackupManager_CreatePatternsBackup_ShouldCreateBackupFile()
        {
            BackupManager.CreatePatternsBackup(TestSourceFile);

            Assert.IsTrue(Directory.Exists("Backups"), "Backup directory should be created");

            var backupFiles = Directory.GetFiles("Backups", "patterns_backup_*.json");
            Assert.IsTrue(backupFiles.Length > 0, "Backup file should be created");

            var backupContent = File.ReadAllText(backupFiles[0]);
            var sourceContent = File.ReadAllText(TestSourceFile);
            Assert.AreEqual(sourceContent, backupContent, "Backup content should match source");
        }

        [TestMethod]
        public void BackupManager_CreateSettingsBackup_ShouldCreateBackupFile()
        {
            BackupManager.CreateSettingsBackup(TestSourceFile);

            var backupFiles = Directory.GetFiles("Backups", "settings_backup_*.json");
            Assert.IsTrue(backupFiles.Length > 0, "Settings backup file should be created");
        }

        [TestMethod]
        public void BackupManager_CreateUiStateBackup_ShouldCreateBackupFile()
        {
            var uiState = new UiStateData
            {
                Version = 1,
                LastSaved = DateTime.UtcNow,
                SelectedTabIndex = 2,
                FormState = new FormStateData { Width = 800, Height = 600 }
            };

            BackupManager.CreateUiStateBackup(uiState);

            var backupFiles = Directory.GetFiles("Backups", "ui_state_backup_*.json");
            Assert.IsTrue(backupFiles.Length > 0, "UI state backup file should be created");
        }

        [TestMethod]
        public void BackupManager_CreateBackupNonExistentFile_ShouldNotCreateBackup()
        {
            BackupManager.CreatePatternsBackup("nonexistent_file.json");

            var backupFiles = Directory.GetFiles("Backups", "patterns_backup_*.json");
            Assert.AreEqual(0, backupFiles.Length, "No backup should be created for non-existent file");
        }

        [TestMethod]
        public void BackupManager_GetAvailableBackups_ShouldReturnBackupInfo()
        {
            // Create some backups
            BackupManager.CreatePatternsBackup(TestSourceFile);
            BackupManager.CreateSettingsBackup(TestSourceFile);

            var backups = BackupManager.GetAvailableBackups();

            Assert.IsTrue(backups.Count >= 2, "Should return at least 2 backups");
            Assert.IsTrue(backups.Any(b => b.Type == BackupType.Patterns), "Should include patterns backup");
            Assert.IsTrue(backups.Any(b => b.Type == BackupType.Settings), "Should include settings backup");

            // Check that backups are sorted by creation time (newest first)
            for (int i = 1; i < backups.Count; i++)
            {
                Assert.IsTrue(backups[i - 1].CreatedTime >= backups[i].CreatedTime, "Backups should be sorted by creation time (newest first)");
            }
        }

        [TestMethod]
        public void BackupManager_RestoreBackup_ShouldRestoreFileContent()
        {
            // Create backup
            BackupManager.CreatePatternsBackup(TestSourceFile);

            var backups = BackupManager.GetAvailableBackups();
            var backup = backups.First(b => b.Type == BackupType.Patterns);

            // Modify original file
            File.WriteAllText(TestSourceFile, "{\"modified\": \"data\"}");

            // Restore backup
            var targetFile = "restored_file.json";
            var success = BackupManager.RestoreBackup(backup, targetFile);

            Assert.IsTrue(success, "Restore should succeed");
            Assert.IsTrue(File.Exists(targetFile), "Restored file should exist");

            var restoredContent = File.ReadAllText(targetFile);
            Assert.AreEqual("{\"test\": \"data\"}", restoredContent, "Restored content should match original");

            // Cleanup
            File.Delete(targetFile);
        }

        [TestMethod]
        public void BackupManager_RestoreBackupWithExistingTarget_ShouldCreatePreRestoreBackup()
        {
            // Create backup
            BackupManager.CreatePatternsBackup(TestSourceFile);

            var backups = BackupManager.GetAvailableBackups();
            var backup = backups.First(b => b.Type == BackupType.Patterns);

            // Create target file with different content
            var targetFile = "target_file.json";
            File.WriteAllText(targetFile, "{\"existing\": \"content\"}");

            // Restore backup
            var success = BackupManager.RestoreBackup(backup, targetFile);

            Assert.IsTrue(success, "Restore should succeed");
            Assert.IsTrue(File.Exists(targetFile + ".pre_restore_backup"), "Pre-restore backup should be created");

            // Cleanup
            File.Delete(targetFile);
            File.Delete(targetFile + ".pre_restore_backup");
        }

        [TestMethod]
        public void BackupManager_DeleteBackup_ShouldRemoveBackupFile()
        {
            // Create backup
            BackupManager.CreatePatternsBackup(TestSourceFile);

            var backups = BackupManager.GetAvailableBackups();
            var backup = backups.First(b => b.Type == BackupType.Patterns);

            // Verify backup exists
            Assert.IsTrue(File.Exists(backup.FilePath), "Backup file should exist before deletion");

            // Delete backup
            var success = BackupManager.DeleteBackup(backup);

            Assert.IsTrue(success, "Delete should succeed");
            Assert.IsFalse(File.Exists(backup.FilePath), "Backup file should not exist after deletion");
        }

        [TestMethod]
        public void BackupManager_CreateFullBackup_ShouldCreateManifestAndBackupFiles()
        {
            // Create some source files
            Directory.CreateDirectory("Profiles");
            File.WriteAllText("antirecoil_settings.json", "{\"setting\": \"value\"}");
            File.WriteAllText("Profiles/anti_recoil_patterns.json", "{\"patterns\": []}");
            File.WriteAllText("ui_state.json", "{\"ui\": \"state\"}");

            BackupManager.CreateFullBackup();

            var fullBackupDirs = Directory.GetDirectories("Backups", "full_backup_*");
            Assert.IsTrue(fullBackupDirs.Length > 0, "Full backup directory should be created");

            var backupDir = fullBackupDirs[0];
            Assert.IsTrue(File.Exists(Path.Combine(backupDir, "manifest.json")), "Manifest file should exist");

            // Check that files were backed up
            var expectedFiles = new[] { "antirecoil_settings.json", "anti_recoil_patterns.json", "ui_state.json" };
            foreach (var expectedFile in expectedFiles)
            {
                var backupFilePath = Path.Combine(backupDir, expectedFile);
                Assert.IsTrue(File.Exists(backupFilePath), $"Backup should contain {expectedFile}");
            }

            // Cleanup
            Directory.Delete("Profiles", true);
            File.Delete("antirecoil_settings.json");
            File.Delete("ui_state.json");
        }

        [TestMethod]
        public void BackupManager_CleanupOldBackups_ShouldKeepOnlyRecentBackups()
        {
            // Create multiple backups (more than MaxBackups = 10)
            for (int i = 0; i < 15; i++)
            {
                BackupManager.CreatePatternsBackup(TestSourceFile);
                System.Threading.Thread.Sleep(10); // Ensure different creation times
            }

            var backups = BackupManager.GetAvailableBackups();
            var patternsBackups = backups.Where(b => b.Type == BackupType.Patterns).ToList();

            Assert.IsTrue(patternsBackups.Count <= 10, "Should keep only 10 most recent backups");
        }

        [TestMethod]
        public void BackupInfo_DisplayName_ShouldFormatCorrectly()
        {
            var backupInfo = new BackupInfo
            {
                Type = BackupType.Patterns,
                CreatedTime = new DateTime(2023, 10, 15, 14, 30, 0),
                Size = 2048
            };

            var displayName = backupInfo.DisplayName;

            Assert.IsTrue(displayName.Contains("Patterns"), "Display name should contain backup type");
            Assert.IsTrue(displayName.Contains("2023-10-15"), "Display name should contain date");
            Assert.IsTrue(displayName.Contains("14:30:00"), "Display name should contain time");
            Assert.IsTrue(displayName.Contains("2.0 KB"), "Display name should contain size in KB");
        }

        [TestMethod]
        public void FullBackupManifest_DefaultValues_ShouldBeCorrect()
        {
            var manifest = new FullBackupManifest();

            Assert.AreEqual(string.Empty, manifest.Version, "Version should default to empty string");
            Assert.IsNotNull(manifest.Files, "Files should not be null");
            Assert.AreEqual(0, manifest.Files.Count, "Files should be empty by default");
        }

        [TestMethod]
        public void UiStateData_DefaultValues_ShouldBeCorrect()
        {
            var uiState = new UiStateData();

            Assert.AreEqual(1, uiState.Version, "Version should default to 1");
            Assert.IsNotNull(uiState.RecentPatterns, "RecentPatterns should not be null");
            Assert.AreEqual(0, uiState.RecentPatterns.Count, "RecentPatterns should be empty by default");
            Assert.AreEqual(0, uiState.SelectedTabIndex, "SelectedTabIndex should default to 0");
        }

        [TestMethod]
        public void FormStateData_DefaultValues_ShouldBeCorrect()
        {
            var formState = new FormStateData();

            Assert.AreEqual(900, formState.Width, "Width should default to 900");
            Assert.AreEqual(700, formState.Height, "Height should default to 700");
            Assert.AreEqual(-1, formState.X, "X should default to -1 (center)");
            Assert.AreEqual(-1, formState.Y, "Y should default to -1 (center)");
            Assert.IsFalse(formState.Maximized, "Maximized should default to false");
        }

        [TestMethod]
        public void UserPreferences_DefaultValues_ShouldBeCorrect()
        {
            var userPrefs = new UserPreferences();

            Assert.IsTrue(userPrefs.ShowTooltips, "ShowTooltips should default to true");
            Assert.IsTrue(userPrefs.ShowWarnings, "ShowWarnings should default to true");
            Assert.IsTrue(userPrefs.AutoBackup, "AutoBackup should default to true");
            Assert.AreEqual(24, userPrefs.BackupIntervalHours, "BackupIntervalHours should default to 24");
            Assert.AreEqual("Dark", userPrefs.Theme, "Theme should default to Dark");
        }
    }
}