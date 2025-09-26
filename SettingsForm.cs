using System;
using System.IO;
using System.Text.Json;
using System.Windows.Forms;
using WootMouseRemap.Core;
// Logger is in WootMouseRemap namespace

namespace WootMouseRemap.UI
{
    public partial class SettingsForm : Form
    {
        private const string UiStateFile = "ui_state.json";
        private UiStateData _currentSettings = new();

        // Controls
        private CheckBox _showTooltipsCheckBox = null!;
        private CheckBox _showWarningsCheckBox = null!;
        private CheckBox _autoBackupCheckBox = null!;
        private NumericUpDown _backupIntervalNumeric = null!;
        private ComboBox _themeComboBox = null!;
        private CheckBox _enableLowLevelHooksCheckBox = null!;
        private CheckBox _complianceModeCheckBox = null!;
        private CheckBox _allowBackgroundCaptureCheckBox = null!;
        private Button _okButton = null!;
        private Button _cancelButton = null!;
        private Button _resetButton = null!;

        public SettingsForm()
        {
            InitializeComponent();
            LoadCurrentSettings();
            PopulateControls();
        }

        private void InitializeComponent()
        {
            this.Text = "Settings";
            this.Size = new System.Drawing.Size(400, 300);
            this.StartPosition = FormStartPosition.CenterParent;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;

            // Create controls
            var mainPanel = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 10,
                Padding = new Padding(10)
            };

            // Show Tooltips
            var tooltipsLabel = new Label { Text = "Show Tooltips:", Anchor = AnchorStyles.Left };
            _showTooltipsCheckBox = new CheckBox { Anchor = AnchorStyles.Left };
            mainPanel.Controls.Add(tooltipsLabel, 0, 0);
            mainPanel.Controls.Add(_showTooltipsCheckBox, 1, 0);

            // Show Warnings
            var warningsLabel = new Label { Text = "Show Warnings:", Anchor = AnchorStyles.Left };
            _showWarningsCheckBox = new CheckBox { Anchor = AnchorStyles.Left };
            mainPanel.Controls.Add(warningsLabel, 0, 1);
            mainPanel.Controls.Add(_showWarningsCheckBox, 1, 1);

            // Auto Backup
            var autoBackupLabel = new Label { Text = "Auto Backup:", Anchor = AnchorStyles.Left };
            _autoBackupCheckBox = new CheckBox { Anchor = AnchorStyles.Left };
            mainPanel.Controls.Add(autoBackupLabel, 0, 2);
            mainPanel.Controls.Add(_autoBackupCheckBox, 1, 2);

            // Backup Interval
            var intervalLabel = new Label { Text = "Backup Interval (hours):", Anchor = AnchorStyles.Left };
            _backupIntervalNumeric = new NumericUpDown 
            { 
                Minimum = 1, 
                Maximum = 168, 
                Value = 24,
                Anchor = AnchorStyles.Left,
                Width = 60
            };
            mainPanel.Controls.Add(intervalLabel, 0, 3);
            mainPanel.Controls.Add(_backupIntervalNumeric, 1, 3);

            // Theme
            var themeLabel = new Label { Text = "Theme:", Anchor = AnchorStyles.Left };
            _themeComboBox = new ComboBox 
            { 
                DropDownStyle = ComboBoxStyle.DropDownList,
                Anchor = AnchorStyles.Left,
                Width = 100
            };
            _themeComboBox.Items.AddRange(new[] { "Dark", "Light", "Auto" });
            mainPanel.Controls.Add(themeLabel, 0, 4);
            mainPanel.Controls.Add(_themeComboBox, 1, 4);

            // Enable Low-Level Hooks
            var hooksLabel = new Label { Text = "Enable Low-Level Hooks:", Anchor = AnchorStyles.Left };
            _enableLowLevelHooksCheckBox = new CheckBox { Anchor = AnchorStyles.Left };
            mainPanel.Controls.Add(hooksLabel, 0, 5);
            mainPanel.Controls.Add(_enableLowLevelHooksCheckBox, 1, 5);

            // Compliance Mode
            var complianceLabel = new Label { Text = "Compliance Mode:", Anchor = AnchorStyles.Left };
            _complianceModeCheckBox = new CheckBox { Anchor = AnchorStyles.Left };
            mainPanel.Controls.Add(complianceLabel, 0, 6);
            mainPanel.Controls.Add(_complianceModeCheckBox, 1, 6);

            // Allow Background Capture
            var backgroundLabel = new Label { Text = "Allow Background Capture:", Anchor = AnchorStyles.Left };
            _allowBackgroundCaptureCheckBox = new CheckBox { Anchor = AnchorStyles.Left };
            mainPanel.Controls.Add(backgroundLabel, 0, 7);
            mainPanel.Controls.Add(_allowBackgroundCaptureCheckBox, 1, 7);

            // Buttons panel
            var buttonPanel = new FlowLayoutPanel
            {
                FlowDirection = FlowDirection.RightToLeft,
                Dock = DockStyle.Bottom,
                Height = 40,
                Padding = new Padding(10, 5, 10, 5)
            };

            _cancelButton = new Button { Text = "Cancel", DialogResult = DialogResult.Cancel };
            _okButton = new Button { Text = "OK", DialogResult = DialogResult.OK };
            _resetButton = new Button { Text = "Reset to Defaults" };

            _okButton.Click += OkButton_Click;
            _resetButton.Click += ResetButton_Click;

            buttonPanel.Controls.Add(_cancelButton);
            buttonPanel.Controls.Add(_okButton);
            buttonPanel.Controls.Add(_resetButton);

            // Set column styles for proper layout
            mainPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 60F));
            mainPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 40F));

            this.Controls.Add(mainPanel);
            this.Controls.Add(buttonPanel);

            this.AcceptButton = _okButton;
            this.CancelButton = _cancelButton;
        }

        private void LoadCurrentSettings()
        {
            try
            {
                // Path validation to prevent directory traversal attacks
                var fullPath = Path.GetFullPath(UiStateFile);
                var allowedDir = Path.GetFullPath(Directory.GetCurrentDirectory());
                if (!fullPath.StartsWith(allowedDir, StringComparison.OrdinalIgnoreCase))
                    throw new UnauthorizedAccessException("Path traversal detected");

                if (File.Exists(UiStateFile))
                {
                    var json = File.ReadAllText(UiStateFile);
                    var options = new JsonSerializerOptions 
                    { 
                        MaxDepth = 5,
                        PropertyNameCaseInsensitive = false,
                        AllowTrailingCommas = false
                    };
                    try 
                    {
                        _currentSettings = JsonSerializer.Deserialize<UiStateData>(json, options) ?? new UiStateData();
                    }
                    catch (JsonException) 
                    {
                        _currentSettings = new UiStateData(); // Safe fallback
                    }
                }
                else
                {
                    _currentSettings = new UiStateData();
                }

                // Ensure UserPrefs exists
                _currentSettings.UserPrefs ??= new UserPreferences();
            }
            catch (Exception ex)
            {
                Logger.Error("Failed to load settings", ex);
                _currentSettings = new UiStateData { UserPrefs = new UserPreferences() };
            }
        }

        private void PopulateControls()
        {
            var prefs = _currentSettings.UserPrefs!;
            
            _showTooltipsCheckBox.Checked = prefs.ShowTooltips;
            _showWarningsCheckBox.Checked = prefs.ShowWarnings;
            _autoBackupCheckBox.Checked = prefs.AutoBackup;
            _backupIntervalNumeric.Value = Math.Max(1, Math.Min(168, prefs.BackupIntervalHours));
            
            // Set theme, default to "Dark" if not found
            var themeIndex = _themeComboBox.Items.IndexOf(prefs.Theme);
            _themeComboBox.SelectedIndex = themeIndex >= 0 ? themeIndex : 0;
            _enableLowLevelHooksCheckBox.Checked = prefs.EnableLowLevelHooks;
            _complianceModeCheckBox.Checked = prefs.ComplianceMode;
            _allowBackgroundCaptureCheckBox.Checked = prefs.AllowBackgroundCapture;
        }

        private void OkButton_Click(object? sender, EventArgs e)
        {
            try
            {
                // Update settings from controls
                var prefs = _currentSettings.UserPrefs!;
                prefs.ShowTooltips = _showTooltipsCheckBox.Checked;
                prefs.ShowWarnings = _showWarningsCheckBox.Checked;
                prefs.AutoBackup = _autoBackupCheckBox.Checked;
                prefs.BackupIntervalHours = (int)_backupIntervalNumeric.Value;
                prefs.Theme = _themeComboBox.SelectedItem?.ToString() ?? "Dark";
                prefs.EnableLowLevelHooks = _enableLowLevelHooksCheckBox.Checked;
                prefs.ComplianceMode = _complianceModeCheckBox.Checked;
                prefs.AllowBackgroundCapture = _allowBackgroundCaptureCheckBox.Checked;

                // Update timestamp
                _currentSettings.LastSaved = DateTime.UtcNow;

                // Save to file
                SaveSettings();

                this.DialogResult = DialogResult.OK;
                this.Close();
            }
            catch (Exception ex)
            {
                Logger.Error("Failed to save settings", ex);
                MessageBox.Show("Failed to save settings. Please try again.", "Error", 
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void ResetButton_Click(object? sender, EventArgs e)
        {
            var result = MessageBox.Show(
                "Reset all settings to default values?", 
                "Reset Settings", 
                MessageBoxButtons.YesNo, 
                MessageBoxIcon.Question);

            if (result == DialogResult.Yes)
            {
                _currentSettings.UserPrefs = new UserPreferences();
                PopulateControls();
            }
        }

        private void SaveSettings()
        {
            try
            {
                // Path validation to prevent directory traversal attacks
                var fullPath = Path.GetFullPath(UiStateFile);
                var allowedDir = Path.GetFullPath(Directory.GetCurrentDirectory());
                if (!fullPath.StartsWith(allowedDir, StringComparison.OrdinalIgnoreCase))
                    throw new UnauthorizedAccessException("Path traversal detected");

                var options = new JsonSerializerOptions { WriteIndented = true };
                var json = JsonSerializer.Serialize(_currentSettings, options);
                File.WriteAllText(UiStateFile, json);
                
                Logger.Info("Settings saved successfully");
            }
            catch (Exception ex)
            {
                Logger.Error("Failed to write settings file", ex);
                throw;
            }
        }

        public static void ShowSettingsDialog(IWin32Window? owner = null)
        {
            try
            {
                using var form = new SettingsForm();
                form.ShowDialog(owner);
            }
            catch (Exception ex)
            {
                Logger.Error("Failed to show settings dialog", ex);
                MessageBox.Show("Failed to open settings dialog.", "Error", 
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }
}



