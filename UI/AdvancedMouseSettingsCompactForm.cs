using System;
using System.Drawing;
using System.Windows.Forms;
using System.IO;
using WootMouseRemap.Core;
using WootMouseRemap;
using WootMouseRemap.Security;
using WootMouseRemap.Core.Mapping;

namespace WootMouseRemap.UI
{
    public partial class AdvancedMouseSettingsCompactForm : Form
    {
    private readonly ProfileManager _profiles = null!;
    private WootMouseRemap.Core.Services.ProfileService? _profileServiceFallback;
        private readonly StickMapper _mapper = null!;

        // UI Controls
        private GroupBox? _settingsGroup;
        private TrackBar? _sensitivityTrackBar;
        private NumericUpDown? _sensitivityNumeric;
        private TrackBar? _expoTrackBar;
        private NumericUpDown? _expoNumeric;
        private NumericUpDown? _mouseDpiNumeric;
        private Button? _applyButton;
        private Button? _resetButton;
        private Button? _closeButton;

        // Import/Export controls
        private Button? _importButton;
        private Button? _exportButton;

        // Telemetry controls
        private GroupBox? _telemetryGroup;
        private Label? _currentSensitivityLabel;
        private Label? _currentExpoLabel;
        private Label? _effectiveDpiLabel;
        private Label? _connectionStatusLabel;

        // Preview graph controls
        private GroupBox? _previewGroup;
        private Panel? _graphPanel;

        private bool _updatingControls = false;

        // Validation system
        private ValidationSystem? _validationSystem;

        // Settings persistence
        private CompactMouseFormSettings _formSettings = new();

        // Telemetry
        private WootMouseRemap.Telemetry? _telemetry;
        private System.Windows.Forms.Timer? _telemetryTimer;

        // Tooltips
        private ToolTip? _toolTip;

        private static readonly Size FallbackClientSize = new Size(775, 414);

        public AdvancedMouseSettingsCompactForm()
        {
            try
            {
                // This constructor is for the OverlayForm button click
                // We need to get the ProfileManager and StickMapper from the OverlayForm
                var overlay = SecureFormAccess.GetOverlayForm() as OverlayForm;
                if (overlay == null)
                {
                    Logger.Warn("AdvancedMouseSettingsCompactForm: OverlayForm not found in secure registry; creating local ProfileManager/StickMapper fallbacks");
                    _profiles = new ProfileManager();
                    _mapper = new StickMapper();
                }
                else
                {
                    // Prefer ProfileService if available; fall back to existing ProfileManager
                    var svc = overlay.ProfileService;
                    if (svc != null)
                    {
                        // We still keep a ProfileManager for compatibility but prefer the service for profile reads/saves
                        _profiles = overlay.ProfileManager ?? new ProfileManager();
                        // store the service instance to use where possible via reflection adapter
                        // (compact form will call svc.GetDefaultProfile() when available)
                        _profileServiceFallback = svc;
                    }
                    else
                    {
                        _profiles = overlay.ProfileManager ?? new ProfileManager();
                    }

                    _mapper = overlay.StickMapper ?? new StickMapper();
                }

                // Remove unsafe reflection access - telemetry will be null
                _telemetry = null;
                Logger.Info("Telemetry access disabled for security");

                InitializeComponent();
                InitializeValidation();
                LoadFormSettings();
                LoadCurrentSettings();
                Logger.Info("AdvancedMouseSettingsCompactForm initialized successfully");
            }
            catch (Exception ex)
            {
                Logger.Error("Failed to initialize AdvancedMouseSettingsCompactForm", ex);
                MessageBox.Show($"Failed to initialize mouse settings form: {ex.Message}", "Initialization Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                Close();
            }
        }

        private void InitializeComponent()
        {
            // Form properties
            Text = "Mouse Settings (Compact)";
            StartPosition = FormStartPosition.CenterParent;
            AutoScaleMode = AutoScaleMode.Dpi;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = false;

            ApplyOverlayClientSizeOrFallback();

            // Main settings group
            _settingsGroup = new GroupBox
            {
                Text = "Mouse Settings",
                Location = new Point(10, 10),
                Size = new Size(ClientSize.Width - 20, ClientSize.Height - 60),
                Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right
            };

            var layout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(10),
                RowCount = 5,
                ColumnCount = 3
            };

            // Configure column styles
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize)); // Labels
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 60)); // Sliders
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize)); // Numerics

            // Sensitivity
            var sensitivityLabel = new Label { Text = "Sensitivity:", AutoSize = true };
            _sensitivityTrackBar = new TrackBar
            {
                Minimum = 1,
                Maximum = 1000,
                Value = 150,
                TickFrequency = 100,
                Anchor = AnchorStyles.Left | AnchorStyles.Right
            };
            _sensitivityNumeric = new NumericUpDown
            {
                Minimum = 0.01m,
                Maximum = 10.0m,
                DecimalPlaces = 2,
                Increment = 0.05m,
                Value = 1.5m,
                Width = 80
            };

            // Expo
            var expoLabel = new Label { Text = "Expo:", AutoSize = true };
            _expoTrackBar = new TrackBar
            {
                Minimum = 0,
                Maximum = 100,
                Value = 60,
                TickFrequency = 10,
                Anchor = AnchorStyles.Left | AnchorStyles.Right
            };
            _expoNumeric = new NumericUpDown
            {
                Minimum = 0.0m,
                Maximum = 1.0m,
                DecimalPlaces = 2,
                Increment = 0.05m,
                Value = 0.6m,
                Width = 80
            };

            // Mouse DPI
            var dpiLabel = new Label { Text = "Mouse DPI:", AutoSize = true };
            _mouseDpiNumeric = new NumericUpDown
            {
                Minimum = 100,
                Maximum = 10000,
                Value = 1600,
                Width = 80
            };

            // Buttons
            _applyButton = new Button { Text = "Apply", Width = 75, Anchor = AnchorStyles.Right };
            _resetButton = new Button { Text = "Reset", Width = 75, Anchor = AnchorStyles.Right };
            _closeButton = new Button { Text = "Close", Width = 75, Anchor = AnchorStyles.Right };

            // Import/Export buttons
            _importButton = new Button { Text = "Import", Width = 70, Anchor = AnchorStyles.Left };
            _exportButton = new Button { Text = "Export", Width = 70, Anchor = AnchorStyles.Left };

            // Add controls to layout
            layout.Controls.Add(sensitivityLabel, 0, 0);
            layout.Controls.Add(_sensitivityTrackBar!, 1, 0);
            layout.Controls.Add(_sensitivityNumeric!, 2, 0);

            layout.Controls.Add(expoLabel, 0, 1);
            layout.Controls.Add(_expoTrackBar!, 1, 1);
            layout.Controls.Add(_expoNumeric!, 2, 1);

            layout.Controls.Add(dpiLabel, 0, 2);
            layout.SetColumnSpan(dpiLabel, 1);
            layout.Controls.Add(_mouseDpiNumeric!, 1, 2);
            layout.SetColumnSpan(_mouseDpiNumeric!, 2);

            // Import/Export row
            var importExportPanel = new FlowLayoutPanel
            {
                FlowDirection = FlowDirection.LeftToRight,
                Dock = DockStyle.Fill
            };
            importExportPanel.Controls.Add(_importButton!);
            importExportPanel.Controls.Add(_exportButton!);
            layout.Controls.Add(importExportPanel, 0, 3);
            layout.SetColumnSpan(importExportPanel, 3);

            // Button row
            var buttonPanel = new FlowLayoutPanel
            {
                FlowDirection = FlowDirection.RightToLeft,
                Dock = DockStyle.Fill
            };
            buttonPanel.Controls.Add(_closeButton!);
            buttonPanel.Controls.Add(_applyButton!);
            buttonPanel.Controls.Add(_resetButton!);
            layout.Controls.Add(buttonPanel, 0, 4);
            layout.SetColumnSpan(buttonPanel, 3);

            _settingsGroup.Controls.Add(layout);
            Controls.Add(_settingsGroup);

            // Add telemetry group
            InitializeTelemetryControls();

            // Add preview graph group
            InitializePreviewGraphControls();

            // Wire up events
            _sensitivityTrackBar!.ValueChanged += SensitivityTrackBar_ValueChanged;
            _sensitivityNumeric!.ValueChanged += SensitivityNumeric_ValueChanged;
            _expoTrackBar!.ValueChanged += ExpoTrackBar_ValueChanged;
            _expoNumeric!.ValueChanged += ExpoNumeric_ValueChanged;
            _mouseDpiNumeric!.ValueChanged += SettingsChanged;

            _applyButton!.Click += ApplyButton_Click;
            _resetButton!.Click += ResetButton_Click;
            _closeButton!.Click += (s, e) => Close();

            // Import/Export events
            _importButton!.Click += ImportButton_Click;
            _exportButton!.Click += ExportButton_Click;

            // Add keyboard shortcuts
            KeyDown += CompactForm_KeyDown;

            // Set up accessibility
            SetupAccessibility();
        }

        private void InitializeTelemetryControls()
        {
            // Telemetry group
            _telemetryGroup = new GroupBox
            {
                Text = "Mouse Telemetry",
                Location = new Point(10, _settingsGroup!.Bottom + 10),
                Size = new Size(ClientSize.Width - 20, 100),
                Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right,
                ForeColor = Color.White
            };

            var telemetryLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(10),
                RowCount = 4,
                ColumnCount = 2
            };

            // Configure columns
            telemetryLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            telemetryLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));

            int row = 0;

            // Current sensitivity
            var sensLabel = new Label { Text = "Sensitivity:", AutoSize = true, ForeColor = Color.White };
            _currentSensitivityLabel = new Label { Text = "1.50", AutoSize = true, ForeColor = Color.Cyan };
            telemetryLayout.Controls.Add(sensLabel, 0, row);
            telemetryLayout.Controls.Add(_currentSensitivityLabel, 1, row);
            row++;

            // Current expo
            var expoLabel = new Label { Text = "Expo:", AutoSize = true, ForeColor = Color.White };
            _currentExpoLabel = new Label { Text = "0.60", AutoSize = true, ForeColor = Color.Cyan };
            telemetryLayout.Controls.Add(expoLabel, 0, row);
            telemetryLayout.Controls.Add(_currentExpoLabel, 1, row);
            row++;

            // Effective DPI
            var dpiLabel = new Label { Text = "Effective DPI:", AutoSize = true, ForeColor = Color.White };
            _effectiveDpiLabel = new Label { Text = "1600", AutoSize = true, ForeColor = Color.Cyan };
            telemetryLayout.Controls.Add(dpiLabel, 0, row);
            telemetryLayout.Controls.Add(_effectiveDpiLabel, 1, row);
            row++;

            // Connection status
            var connLabel = new Label { Text = "Connection:", AutoSize = true, ForeColor = Color.White };
            _connectionStatusLabel = new Label { Text = "Unknown", AutoSize = true, ForeColor = Color.Gray };
            telemetryLayout.Controls.Add(connLabel, 0, row);
            telemetryLayout.Controls.Add(_connectionStatusLabel, 1, row);

            _telemetryGroup.Controls.Add(telemetryLayout);
            Controls.Add(_telemetryGroup);

            // Set up telemetry timer
            _telemetryTimer = new System.Windows.Forms.Timer { Interval = 500 }; // 2 FPS, less frequent updates
            _telemetryTimer.Tick += UpdateTelemetryDisplay;
            _telemetryTimer.Start();
        }

        private void UpdateTelemetryDisplay(object? sender, EventArgs e)
        {
            if (_currentSensitivityLabel == null || _currentExpoLabel == null || 
                _effectiveDpiLabel == null || _connectionStatusLabel == null) return;

            try
            {
                // Update current values from profile
                if (_profiles?.Current != null)
                {
                    var currentProfile = _profileServiceFallback != null ? (InputMappingProfile?)_profileServiceFallback.GetDefaultProfile() : null;
                    if (currentProfile != null)
                    {
                        _currentSensitivityLabel.Text = currentProfile.CurveSettings.Sensitivity.ToString("F2");
                        _currentExpoLabel.Text = currentProfile.CurveSettings.Expo.ToString("F2");
                        _effectiveDpiLabel.Text = currentProfile.MouseDpi.ToString();
                    }
                    else
                    {
                        _currentSensitivityLabel.Text = _profiles.Current.Curves.Sensitivity.ToString("F2");
                        _currentExpoLabel.Text = _profiles.Current.Curves.Expo.ToString("F2");
                        _effectiveDpiLabel.Text = _profiles.Current.MouseDpi.ToString();
                    }
                }

                // Update connection status
                if (_telemetry != null)
                {
                    var status = _telemetry.ViGEmConnected ? "Connected" : "Disconnected";
                    var statusColor = _telemetry.ViGEmConnected ? Color.Lime : Color.Red;
                    _connectionStatusLabel.Text = status;
                    _connectionStatusLabel.ForeColor = statusColor;
                }
                else
                {
                    _connectionStatusLabel.Text = "N/A";
                    _connectionStatusLabel.ForeColor = Color.Gray;
                }
            }
            catch (Exception ex)
            {
                Logger.Warn("Error updating mouse telemetry display: {Message}", ex.Message);
            }
        }

        private void InitializeValidation()
        {
            _validationSystem = new ValidationSystem(this);

            // Add validation rules
            if (_sensitivityNumeric != null)
            {
                _validationSystem.AddRule(new ValidationRule(_sensitivityNumeric, () =>
                {
                    var value = (float)_sensitivityNumeric.Value;
                    if (value < 0.1f)
                        return ValidationResult.Warning("Very low sensitivity (<0.1) may make aiming difficult");
                    if (value > 5.0f)
                        return ValidationResult.Warning("Very high sensitivity (>5.0) may cause over-sensitive aiming");
                    return ValidationResult.Success();
                }, "Sensitivity"));
            }

            if (_expoNumeric != null)
            {
                _validationSystem.AddRule(new ValidationRule(_expoNumeric, () =>
                {
                    var value = (float)_expoNumeric.Value;
                    if (value > 0.8f)
                        return ValidationResult.Warning("High expo (>0.8) may cause inconsistent sensitivity at different speeds");
                    return ValidationResult.Success();
                }, "Expo"));
            }

            if (_mouseDpiNumeric != null)
            {
                _validationSystem.AddRule(new ValidationRule(_mouseDpiNumeric, () =>
                {
                    var value = (int)_mouseDpiNumeric.Value;
                    if (value < 400)
                        return ValidationResult.Warning("Low DPI (<400) may limit precision in high-sensitivity setups");
                    if (value > 5000)
                        return ValidationResult.Warning("Very high DPI (>5000) may cause tracking issues with some mice");
                    return ValidationResult.Success();
                }, "MouseDPI"));
            }

            // Wire up validation events
            _sensitivityNumeric!.ValueChanged += (s, e) => _validationSystem?.ValidateControl(_sensitivityNumeric);
            _expoNumeric!.ValueChanged += (s, e) => _validationSystem?.ValidateControl(_expoNumeric);
            _mouseDpiNumeric!.ValueChanged += (s, e) => _validationSystem?.ValidateControl(_mouseDpiNumeric);

            // Initial validation
            _validationSystem.ValidateAll();
        }

        private void SensitivityTrackBar_ValueChanged(object? sender, EventArgs e)
        {
            if (_updatingControls || _sensitivityTrackBar == null || _sensitivityNumeric == null) return;
            _updatingControls = true;
            var value = _sensitivityTrackBar.Value / 100.0m;
            _sensitivityNumeric.Value = Math.Max(_sensitivityNumeric.Minimum, Math.Min(_sensitivityNumeric.Maximum, value));
            _updatingControls = false;
        }

        private void SensitivityNumeric_ValueChanged(object? sender, EventArgs e)
        {
            if (_updatingControls || _sensitivityTrackBar == null || _sensitivityNumeric == null) return;
            _updatingControls = true;
            var value = (int)(_sensitivityNumeric.Value * 100);
            _sensitivityTrackBar.Value = Math.Max(_sensitivityTrackBar.Minimum, Math.Min(_sensitivityTrackBar.Maximum, value));
            _updatingControls = false;
        }

        private void ExpoTrackBar_ValueChanged(object? sender, EventArgs e)
        {
            if (_updatingControls || _expoTrackBar == null || _expoNumeric == null) return;
            _updatingControls = true;
            var value = _expoTrackBar.Value / 100.0m;
            _expoNumeric.Value = value;
            _updatingControls = false;
        }

        private void ExpoNumeric_ValueChanged(object? sender, EventArgs e)
        {
            if (_updatingControls || _expoTrackBar == null || _expoNumeric == null) return;
            _updatingControls = true;
            var value = (int)(_expoNumeric.Value * 100);
            _expoTrackBar.Value = Math.Max(_expoTrackBar.Minimum, Math.Min(_expoTrackBar.Maximum, value));
            _updatingControls = false;
        }

        private void SettingsChanged(object? sender, EventArgs e)
        {
            // Could add status update here if needed
        }

        private void ApplyButton_Click(object? sender, EventArgs e)
        {
            ApplySettings();
        }

        private void ResetButton_Click(object? sender, EventArgs e)
        {
            ResetToDefaults();
        }

        private void LoadCurrentSettings()
        {
            try
            {
                if (_profiles?.Current == null)
                {
                    Logger.Error("LoadCurrentSettings: ProfileManager or current profile is null");
                    MessageBox.Show("Profile system not available. Settings cannot be loaded.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                var profile = _profileServiceFallback != null ? _profileServiceFallback.GetDefaultProfile() as InputMappingProfile : null;
                if (profile == null) profile = ProfileAdapters.MapFromConfigurationProfile(_profiles.Current);

                _updatingControls = true;

                // Load sensitivity
                if (_sensitivityNumeric != null && _sensitivityTrackBar != null)
                {
                    var sensitivity = Math.Clamp(profile.Curves.Sensitivity, 0.01f, 10.0f);
                    _sensitivityNumeric.Value = (decimal)sensitivity;
                    _sensitivityTrackBar.Value = (int)(sensitivity * 100);
                }

                // Load expo
                if (_expoNumeric != null && _expoTrackBar != null)
                {
                    var expo = Math.Clamp(profile.Curves.Expo, 0.0f, 1.0f);
                    _expoNumeric.Value = (decimal)expo;
                    _expoTrackBar.Value = (int)(expo * 100);
                }

                // Load mouse DPI
                if (_mouseDpiNumeric != null)
                {
                    var dpi = Math.Clamp(profile.MouseDpi, 100, 10000);
                    _mouseDpiNumeric.Value = dpi;
                }

                _updatingControls = false;
                Logger.Info("Mouse settings loaded successfully");
            }
            catch (Exception ex)
            {
                Logger.Error("Error loading mouse settings", ex);
                MessageBox.Show($"Error loading settings: {ex.Message}\n\nPlease check the application logs for more details.", "Load Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                _updatingControls = false; // Ensure controls are not stuck in updating state
            }
        }

        private void ApplySettings()
        {
            // Validate before applying
            _validationSystem?.ValidateAll();
            if (_validationSystem?.HasErrors == true)
            {
                MessageBox.Show("Please fix the validation errors before applying settings.", "Validation Error", 
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            try
            {
                if (_profiles?.Current == null)
                {
                    Logger.Error("ApplySettings: ProfileManager or current profile is null");
                    MessageBox.Show("Profile system not available. Settings cannot be applied.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                var profile = _profileServiceFallback != null ? _profileServiceFallback.GetDefaultProfile() as InputMappingProfile : null;
                if (profile == null) profile = ProfileAdapters.MapFromConfigurationProfile(_profiles.Current);

                // Validate input values
                var sensitivity = (float)(_sensitivityNumeric?.Value ?? 1.5m);
                var expo = (float)(_expoNumeric?.Value ?? 0.6m);
                var dpi = (int)(_mouseDpiNumeric?.Value ?? 1600);

                if (sensitivity < 0.01f || sensitivity > 10.0f)
                {
                    MessageBox.Show("Sensitivity value is out of valid range (0.01 - 10.0).", "Invalid Value", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                if (expo < 0.0f || expo > 1.0f)
                {
                    MessageBox.Show("Expo value is out of valid range (0.0 - 1.0).", "Invalid Value", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                if (dpi < 100 || dpi > 10000)
                {
                    MessageBox.Show("Mouse DPI is out of valid range (100 - 10000).", "Invalid Value", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                // Update profile with new values
                profile.Curves.Sensitivity = sensitivity;
                profile.Curves.Expo = expo;
                profile.MouseDpi = dpi;

                // Save the profile
                if (_profileServiceFallback != null)
                {
                    var currentProfile = ProfileAdapters.MapFromConfigurationProfile(_profiles.Current);
                    _profileServiceFallback.SaveProfile(currentProfile);
                }
                else
                {
                    _profiles.Save();
                }

                MessageBox.Show("Settings applied successfully!", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
                Logger.Info("Mouse settings applied: Sensitivity={Sensitivity:F2}, Expo={Expo:F2}, DPI={Dpi}", sensitivity, expo, dpi);
            }
            catch (Exception ex)
            {
                Logger.Error("Error applying mouse settings", ex);
                MessageBox.Show($"Error applying settings: {ex.Message}\n\nPlease check the application logs for more details.", "Apply Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void ResetToDefaults()
        {
            if (MessageBox.Show("Reset mouse settings to defaults?", "Confirm Reset",
                MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
                return;

            _updatingControls = true;

            // Reset to defaults
            if (_sensitivityNumeric != null) _sensitivityNumeric.Value = 1.0m;
            if (_expoNumeric != null) _expoNumeric.Value = 0.6m;
            if (_mouseDpiNumeric != null) _mouseDpiNumeric.Value = 1600m;

            _updatingControls = false;

            // Trigger updates
            SensitivityNumeric_ValueChanged(null, EventArgs.Empty);
            ExpoNumeric_ValueChanged(null, EventArgs.Empty);
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            ApplyOverlayClientSizeOrFallback();
        }

        private void ApplyOverlayClientSizeOrFallback()
        {
            Size target = FallbackClientSize;

            if (Owner is Form owner && owner.BackgroundImage is Image img1 && img1.Width > 100 && img1.Height > 100)
                target = img1.Size;
            else
            {
                var overlay = SecureFormAccess.GetOverlayForm();
                if (overlay?.BackgroundImage is Image img2 && img2.Width > 100 && img2.Height > 100)
                    target = img2.Size;
            }

            var wa = Screen.FromControl(this).WorkingArea;
            target = new Size(Math.Min(target.Width, wa.Width), Math.Min(target.Height, wa.Height));

            if (ClientSize != target)
                ClientSize = target;
        }

        private void LoadFormSettings()
        {
            try
            {
                string settingsFile = Path.Combine(AppContext.BaseDirectory, "compact_mouse_settings.json");
                if (File.Exists(settingsFile))
                {
                    string json = File.ReadAllText(settingsFile);
                    var options = new System.Text.Json.JsonSerializerOptions 
                    { 
                        MaxDepth = 5,
                        PropertyNameCaseInsensitive = false,
                        AllowTrailingCommas = false
                    };
                    try 
                    {
                        _formSettings = System.Text.Json.JsonSerializer.Deserialize<CompactMouseFormSettings>(json, options) ?? new CompactMouseFormSettings();
                    }
                    catch (System.Text.Json.JsonException) 
                    {
                        _formSettings = new CompactMouseFormSettings(); // Safe fallback
                    }

                    // Apply form position and size
                    if (_formSettings.Width > 0 && _formSettings.Height > 0)
                    {
                        Size = new Size(_formSettings.Width, _formSettings.Height);
                    }
                    if (_formSettings.X >= 0 && _formSettings.Y >= 0)
                    {
                        Location = new Point(_formSettings.X, _formSettings.Y);
                    }

                    // Apply last used values to controls
                    if (_sensitivityNumeric != null) _sensitivityNumeric.Value = (decimal)_formSettings.Sensitivity;
                    if (_expoNumeric != null) _expoNumeric.Value = (decimal)_formSettings.Expo;
                    if (_mouseDpiNumeric != null) _mouseDpiNumeric.Value = _formSettings.MouseDpi;

                    Logger.Info("Compact mouse form settings loaded successfully");
                }
            }
            catch (Exception ex)
            {
                Logger.Warn("Failed to load compact mouse form settings: {Message}", ex.Message);
                _formSettings = new CompactMouseFormSettings(); // Reset to defaults
            }
        }

        private void SaveFormSettings()
        {
            try
            {
                // Update settings from current form state
                _formSettings.X = Location.X;
                _formSettings.Y = Location.Y;
                _formSettings.Width = Size.Width;
                _formSettings.Height = Size.Height;

                // Update settings from current control values
                _formSettings.Sensitivity = (float)(_sensitivityNumeric?.Value ?? 1.5m);
                _formSettings.Expo = (float)(_expoNumeric?.Value ?? 0.6m);
                _formSettings.MouseDpi = (int)(_mouseDpiNumeric?.Value ?? 1600);

                string settingsFile = Path.Combine(AppContext.BaseDirectory, "compact_mouse_settings.json");
                string json = System.Text.Json.JsonSerializer.Serialize(_formSettings, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(settingsFile, json);

                Logger.Info("Compact mouse form settings saved successfully");
            }
            catch (Exception ex)
            {
                Logger.Warn("Failed to save compact mouse form settings: {Message}", ex.Message);
            }
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            _telemetryTimer?.Stop();
            _telemetryTimer?.Dispose();
            SaveFormSettings();
            _validationSystem?.Dispose();
            base.OnFormClosed(e);
        }

        private void CompactForm_KeyDown(object? sender, KeyEventArgs e)
        {
            // Keyboard shortcuts
            if (e.Control && e.KeyCode == Keys.A) // Ctrl+A: Apply
            {
                e.Handled = true;
                ApplyButton_Click(null, EventArgs.Empty);
            }
            else if (e.Control && e.KeyCode == Keys.R) // Ctrl+R: Reset
            {
                e.Handled = true;
                ResetButton_Click(null, EventArgs.Empty);
            }
            else if (e.KeyCode == Keys.Escape) // Escape: Close
            {
                e.Handled = true;
                Close();
            }
            else if (e.Control && e.KeyCode == Keys.S) // Ctrl+S: Save (same as Apply)
            {
                e.Handled = true;
                ApplyButton_Click(null, EventArgs.Empty);
            }
        }

        private void ImportButton_Click(object? sender, EventArgs e)
        {
            try
            {
                using var openFileDialog = new OpenFileDialog
                {
                    Title = "Import Mouse Settings",
                    Filter = "JSON files (*.json)|*.json|All files (*.*)|*.*",
                    DefaultExt = "json",
                    CheckFileExists = true
                };

                if (openFileDialog.ShowDialog() == DialogResult.OK)
                {
                    // Path validation to prevent directory traversal attacks
                    var fullPath = Path.GetFullPath(openFileDialog.FileName);
                    var allowedDir = Path.GetFullPath(Directory.GetCurrentDirectory());
                    if (!fullPath.StartsWith(allowedDir, StringComparison.OrdinalIgnoreCase))
                        throw new UnauthorizedAccessException("Path traversal detected");

                    string json = File.ReadAllText(openFileDialog.FileName);
                    CompactMouseFormSettings importedSettings;
                    var options = new System.Text.Json.JsonSerializerOptions 
                    { 
                        MaxDepth = 5,
                        PropertyNameCaseInsensitive = false,
                        AllowTrailingCommas = false
                    };
                    try 
                    {
                        importedSettings = System.Text.Json.JsonSerializer.Deserialize<CompactMouseFormSettings>(json, options) ?? new CompactMouseFormSettings();
                    }
                    catch (System.Text.Json.JsonException) 
                    {
                        importedSettings = new CompactMouseFormSettings(); // Safe fallback
                    }

                    if (importedSettings != null)
                    {
                        // Apply imported settings to controls
                        _updatingControls = true;
                        if (_sensitivityNumeric != null) _sensitivityNumeric.Value = (decimal)importedSettings.Sensitivity;
                        if (_expoNumeric != null) _expoNumeric.Value = (decimal)importedSettings.Expo;
                        if (_mouseDpiNumeric != null) _mouseDpiNumeric.Value = importedSettings.MouseDpi;
                        _updatingControls = false;

                        // Trigger updates
                        SensitivityNumeric_ValueChanged(null, EventArgs.Empty);
                        ExpoNumeric_ValueChanged(null, EventArgs.Empty);

                        MessageBox.Show("Mouse settings imported successfully!", "Import Complete", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        Logger.Info("Mouse settings imported from file");
                    }
                    else
                    {
                        MessageBox.Show("Invalid settings file format.", "Import Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Error("Error importing mouse settings", ex);
                MessageBox.Show($"Error importing settings: {ex.Message}", "Import Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void ExportButton_Click(object? sender, EventArgs e)
        {
            try
            {
                using var saveFileDialog = new SaveFileDialog
                {
                    Title = "Export Mouse Settings",
                    Filter = "JSON files (*.json)|*.json|All files (*.*)|*.*",
                    DefaultExt = "json",
                    FileName = "mouse_settings.json"
                };

                if (saveFileDialog.ShowDialog() == DialogResult.OK)
                {
                    // Path validation to prevent directory traversal attacks
                    var fullPath = Path.GetFullPath(saveFileDialog.FileName);
                    var allowedDir = Path.GetFullPath(Directory.GetCurrentDirectory());
                    if (!fullPath.StartsWith(allowedDir, StringComparison.OrdinalIgnoreCase))
                        throw new UnauthorizedAccessException("Path traversal detected");

                    var exportSettings = new CompactMouseFormSettings
                    {
                        Sensitivity = (float)(_sensitivityNumeric?.Value ?? 1.5m),
                        Expo = (float)(_expoNumeric?.Value ?? 0.6m),
                        MouseDpi = (int)(_mouseDpiNumeric?.Value ?? 1600)
                    };

                    string json = System.Text.Json.JsonSerializer.Serialize(exportSettings, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
                    File.WriteAllText(saveFileDialog.FileName, json);

                    MessageBox.Show("Mouse settings exported successfully!", "Export Complete", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    Logger.Info("Mouse settings exported to file");
                }
            }
            catch (Exception ex)
            {
                Logger.Error("Error exporting mouse settings", ex);
                MessageBox.Show($"Error exporting settings: {ex.Message}", "Export Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void InitializePreviewGraphControls()
        {
            // Preview graph group
            _previewGroup = new GroupBox
            {
                Text = "Sensitivity Curve Preview",
                Location = new Point(10, _telemetryGroup!.Bottom + 10),
                Size = new Size(ClientSize.Width - 20, 150),
                Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right,
                ForeColor = Color.White
            };

            _graphPanel = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.Black,
                BorderStyle = BorderStyle.FixedSingle
            };
            _graphPanel.Paint += GraphPanel_Paint;

            _previewGroup.Controls.Add(_graphPanel);
            Controls.Add(_previewGroup);

            // Update graph when settings change
            _sensitivityNumeric!.ValueChanged += (s, e) => _graphPanel?.Invalidate();
            _expoNumeric!.ValueChanged += (s, e) => _graphPanel?.Invalidate();
        }

        private void GraphPanel_Paint(object? sender, PaintEventArgs e)
        {
            if (_graphPanel == null || _sensitivityNumeric == null || _expoNumeric == null) return;

            var g = e.Graphics;
            var rect = _graphPanel.ClientRectangle;
            rect.Inflate(-5, -5);

            // Clear background
            g.FillRectangle(Brushes.Black, rect);

            // Draw grid
            using var gridPen = new Pen(Color.DarkGray, 1);
            for (int i = 0; i <= 10; i++)
            {
                int x = rect.Left + (rect.Width * i) / 10;
                int y = rect.Top + (rect.Height * i) / 10;
                g.DrawLine(gridPen, x, rect.Top, x, rect.Bottom);
                g.DrawLine(gridPen, rect.Left, y, rect.Right, y);
            }

            // Draw axes labels
            using var font = new Font("Arial", 8);
            using var brush = new SolidBrush(Color.White);
            g.DrawString("Input", font, brush, rect.Left + rect.Width / 2 - 20, rect.Bottom - 15);
            g.DrawString("Output", font, brush, rect.Left - 30, rect.Top + rect.Height / 2 - 10);

            // Draw curve
            var sensitivity = (float)_sensitivityNumeric.Value;
            var expo = (float)_expoNumeric.Value;

            var points = new List<PointF>();
            for (int i = 0; i <= rect.Width; i++)
            {
                var input = (float)i / rect.Width; // 0 to 1
                var output = CalculateSensitivityCurve(input, sensitivity, expo);
                var x = rect.Left + i;
                var y = rect.Bottom - (output * rect.Height);
                points.Add(new PointF(x, y));
            }

            if (points.Count > 1)
            {
                using var curvePen = new Pen(Color.Cyan, 2);
                g.DrawLines(curvePen, points.ToArray());
            }
        }

        private float CalculateSensitivityCurve(float input, float sensitivity, float expo)
        {
            // Simplified sensitivity curve calculation
            // This is a basic approximation - the actual curve calculation would be more complex
            var curved = (float)Math.Pow(input, 1.0f - expo);
            return Math.Min(curved * sensitivity, 1.0f);
        }

        private void SetupAccessibility()
        {
            _toolTip = new ToolTip
            {
                AutoPopDelay = 5000,
                InitialDelay = 1000,
                ReshowDelay = 500,
                ShowAlways = true
            };

            // Set tooltips for main controls
            if (_sensitivityTrackBar != null)
                _toolTip.SetToolTip(_sensitivityTrackBar, "Adjust mouse sensitivity multiplier (0.01-10.0). Higher values make the mouse more sensitive");

            if (_sensitivityNumeric != null)
                _toolTip.SetToolTip(_sensitivityNumeric, "Precise sensitivity value (0.01-10.0). Controls how much the mouse moves for each input");

            if (_expoTrackBar != null)
                _toolTip.SetToolTip(_expoTrackBar, "Adjust exponential curve (0-1). Higher values create more acceleration at higher speeds");

            if (_expoNumeric != null)
                _toolTip.SetToolTip(_expoNumeric, "Precise expo value (0.0-1.0). 0 = linear response, 1 = maximum acceleration");

            if (_mouseDpiNumeric != null)
                _toolTip.SetToolTip(_mouseDpiNumeric, "Set your mouse DPI for accurate sensitivity calculations (100-10000)");

            if (_applyButton != null)
                _toolTip.SetToolTip(_applyButton, "Apply the current settings to the mouse system (Ctrl+A or Ctrl+S)");

            if (_resetButton != null)
                _toolTip.SetToolTip(_resetButton, "Reset all settings to their default values (Ctrl+R)");

            if (_closeButton != null)
                _toolTip.SetToolTip(_closeButton, "Close this settings window (Escape key)");

            if (_importButton != null)
                _toolTip.SetToolTip(_importButton, "Import mouse settings from a JSON file");

            if (_exportButton != null)
                _toolTip.SetToolTip(_exportButton, "Export current mouse settings to a JSON file");

            // Telemetry tooltips
            if (_currentSensitivityLabel != null)
                _toolTip.SetToolTip(_currentSensitivityLabel, "Current active sensitivity setting");

            if (_currentExpoLabel != null)
                _toolTip.SetToolTip(_currentExpoLabel, "Current active expo setting");

            if (_effectiveDpiLabel != null)
                _toolTip.SetToolTip(_effectiveDpiLabel, "Effective DPI based on current settings");

            if (_connectionStatusLabel != null)
                _toolTip.SetToolTip(_connectionStatusLabel, "Connection status to virtual controller");

            // Graph panel tooltip
            if (_graphPanel != null)
                _toolTip.SetToolTip(_graphPanel, "Visual preview of the sensitivity curve based on current settings");

            // Set accessible names and descriptions for screen readers
            _sensitivityTrackBar!.AccessibleName = "Mouse Sensitivity";
            _sensitivityTrackBar!.AccessibleDescription = "Adjust mouse sensitivity multiplier (0.01-10.0)";
            _sensitivityNumeric!.AccessibleName = "Sensitivity Value";
            _sensitivityNumeric!.AccessibleDescription = "Numeric input for mouse sensitivity";

            _expoTrackBar!.AccessibleName = "Mouse Expo";
            _expoTrackBar!.AccessibleDescription = "Adjust mouse exponential curve (0-1)";
            _expoNumeric!.AccessibleName = "Expo Value";
            _expoNumeric!.AccessibleDescription = "Numeric input for mouse expo";

            _mouseDpiNumeric!.AccessibleName = "Mouse DPI";
            _mouseDpiNumeric!.AccessibleDescription = "Set mouse DPI for sensitivity calculations (100-10000)";

            _applyButton!.AccessibleName = "Apply Settings";
            _applyButton!.AccessibleDescription = "Apply the current mouse settings";
            _resetButton!.AccessibleName = "Reset to Defaults";
            _resetButton!.AccessibleDescription = "Reset all settings to their default values";
            _closeButton!.AccessibleName = "Close Window";
            _closeButton!.AccessibleDescription = "Close the mouse settings window";

            _importButton!.AccessibleName = "Import Settings";
            _importButton!.AccessibleDescription = "Import mouse settings from a file";
            _exportButton!.AccessibleName = "Export Settings";
            _exportButton!.AccessibleDescription = "Export current mouse settings to a file";

            // Set tab order
            _sensitivityTrackBar!.TabIndex = 0;
            _sensitivityNumeric!.TabIndex = 1;
            _expoTrackBar!.TabIndex = 2;
            _expoNumeric!.TabIndex = 3;
            _mouseDpiNumeric!.TabIndex = 4;
            _importButton!.TabIndex = 5;
            _exportButton!.TabIndex = 6;
            _applyButton!.TabIndex = 7;
            _resetButton!.TabIndex = 8;
            _closeButton!.TabIndex = 9;

            // Set form accessibility
            AccessibleName = "Mouse Settings (Compact)";
            AccessibleDescription = "Configure mouse sensitivity, expo, and DPI settings";
        }
    }

    /// <summary>
    /// Settings for compact mouse form persistence
    /// </summary>
    public class CompactMouseFormSettings
    {
        public int X { get; set; } = -1;
        public int Y { get; set; } = -1;
        public int Width { get; set; } = -1;
        public int Height { get; set; } = -1;
        public float Sensitivity { get; set; } = 1.5f;
        public float Expo { get; set; } = 0.6f;
        public int MouseDpi { get; set; } = 1600;
    }
}
