using System;
using System.Drawing;
using System.Windows.Forms;

namespace WootMouseRemap.UI
{
    /// <summary>
    /// Advanced mouse settings configuration form with real-time adjustable parameters
    /// </summary>
    public partial class AdvancedMouseSettingsForm : Form
    {
        private readonly ProfileManager _profiles;
        private readonly StickMapper _mapper;

        // UI Controls
        private GroupBox? _sensitivityGroup;
        private GroupBox? _responseCurveGroup;
        private GroupBox? _advancedGroup;
        private GroupBox? _previewGroup;

        // Sensitivity Controls
        private TrackBar? _sensitivityTrackBar;
        private NumericUpDown? _sensitivityNumeric;
        private Label? _sensitivityValueLabel;

        // Response Curve Controls
        private TrackBar? _expoTrackBar;
        private NumericUpDown? _expoNumeric;
        private Label? _expoValueLabel;

        // Advanced Controls
        private TrackBar? _antiDeadzoneTrackBar;
        private NumericUpDown? _antiDeadzoneNumeric;
        private Label? _antiDeadzoneValueLabel;

        private TrackBar? _smoothingTrackBar;
        private NumericUpDown? _smoothingNumeric;
        private Label? _smoothingValueLabel;

        private TrackBar? _scaleXTrackBar;
        private NumericUpDown? _scaleXNumeric;
        private Label? _scaleXValueLabel;

        private TrackBar? _scaleYTrackBar;
        private NumericUpDown? _scaleYNumeric;
        private Label? _scaleYValueLabel;

        // DPI and additional settings
        private NumericUpDown? _mouseDpiNumeric;
        private Label? _mouseDpiLabel;

        // Preview and actions
        private Label? _previewLabel;
        private Button? _resetButton;
        private Button? _applyButton;
        private Button? _okButton;
        private Button? _cancelButton;

        // Status
        private StatusStrip? _statusStrip;
        private ToolStripStatusLabel? _statusLabel;

        private bool _updatingControls = false;

        public AdvancedMouseSettingsForm(ProfileManager profiles, StickMapper mapper)
        {
            _profiles = profiles ?? throw new ArgumentNullException(nameof(profiles));
            _mapper = mapper ?? throw new ArgumentNullException(nameof(mapper));

            InitializeComponent();
            LoadCurrentSettings();
        }

        private void InitializeComponent()
        {
            // Form properties
            Text = "Advanced Mouse Settings - WootMouseRemap";
            Size = new Size(600, 700);
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = false;

            // Main layout
            var mainPanel = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(10),
                RowCount = 5,
                ColumnCount = 1
            };

            // Configure row styles
            mainPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize)); // Sensitivity
            mainPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize)); // Response Curve
            mainPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize)); // Advanced
            mainPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); // Preview
            mainPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize)); // Buttons

            CreateSensitivityGroup();
            CreateResponseCurveGroup();
            CreateAdvancedGroup();
            CreatePreviewGroup();
            CreateButtonPanel();
            CreateStatusStrip();

            // Add groups to main panel
            if (_sensitivityGroup != null) mainPanel.Controls.Add(_sensitivityGroup, 0, 0);
            if (_responseCurveGroup != null) mainPanel.Controls.Add(_responseCurveGroup, 0, 1);
            if (_advancedGroup != null) mainPanel.Controls.Add(_advancedGroup, 0, 2);
            if (_previewGroup != null) mainPanel.Controls.Add(_previewGroup, 0, 3);
            mainPanel.Controls.Add(CreateButtonPanel(), 0, 4);

            Controls.Add(mainPanel);
            if (_statusStrip != null) Controls.Add(_statusStrip);
        }

        private void CreateSensitivityGroup()
        {
            _sensitivityGroup = new GroupBox
            {
                Text = "Mouse Sensitivity",
                Size = new Size(560, 80),
                Padding = new Padding(10)
            };

            var layout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                RowCount = 2,
                ColumnCount = 4
            };

            // Sensitivity slider and controls
            var sensitivityLabel = new Label { Text = "Sensitivity:", AutoSize = true, Anchor = AnchorStyles.Left };
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
            _sensitivityValueLabel = new Label { Text = "1.50", AutoSize = true, Anchor = AnchorStyles.Left };

            // Mouse DPI
            _mouseDpiLabel = new Label { Text = "Mouse DPI:", AutoSize = true, Anchor = AnchorStyles.Left };
            _mouseDpiNumeric = new NumericUpDown
            {
                Minimum = 100,
                Maximum = 10000,
                Value = 1600,
                Width = 80
            };

            layout.Controls.Add(sensitivityLabel, 0, 0);
            layout.Controls.Add(_sensitivityTrackBar!, 1, 0);
            layout.Controls.Add(_sensitivityNumeric!, 2, 0);
            layout.Controls.Add(_sensitivityValueLabel!, 3, 0);

            layout.Controls.Add(_mouseDpiLabel!, 0, 1);
            layout.Controls.Add(_mouseDpiNumeric!, 1, 1);

            _sensitivityGroup.Controls.Add(layout);

            // Wire up events
            _sensitivityTrackBar!.ValueChanged += SensitivityTrackBar_ValueChanged;
            _sensitivityNumeric!.ValueChanged += SensitivityNumeric_ValueChanged;
            _mouseDpiNumeric!.ValueChanged += SettingsChanged;
        }

        private void CreateResponseCurveGroup()
        {
            _responseCurveGroup = new GroupBox
            {
                Text = "Response Curve",
                Size = new Size(560, 80),
                Padding = new Padding(10)
            };

            var layout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                RowCount = 1,
                ColumnCount = 4
            };

            // Expo control
            var expoLabel = new Label { Text = "Expo:", AutoSize = true, Anchor = AnchorStyles.Left };
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
            _expoValueLabel = new Label { Text = "0.60", AutoSize = true, Anchor = AnchorStyles.Left };

            layout.Controls.Add(expoLabel, 0, 0);
            layout.Controls.Add(_expoTrackBar!, 1, 0);
            layout.Controls.Add(_expoNumeric!, 2, 0);
            layout.Controls.Add(_expoValueLabel!, 3, 0);

            _responseCurveGroup.Controls.Add(layout);

            // Wire up events
            _expoTrackBar!.ValueChanged += ExpoTrackBar_ValueChanged;
            _expoNumeric!.ValueChanged += ExpoNumeric_ValueChanged;
        }

        private void CreateAdvancedGroup()
        {
            _advancedGroup = new GroupBox
            {
                Text = "Advanced Settings",
                Size = new Size(560, 160),
                Padding = new Padding(10)
            };

            var layout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                RowCount = 4,
                ColumnCount = 4
            };

            // Anti-deadzone
            var antiDeadzoneLabel = new Label { Text = "Anti-Deadzone:", AutoSize = true };
            _antiDeadzoneTrackBar = new TrackBar { Minimum = 0, Maximum = 50, Value = 5, TickFrequency = 5 };
            _antiDeadzoneNumeric = new NumericUpDown { Minimum = 0.0m, Maximum = 0.5m, DecimalPlaces = 2, Increment = 0.01m, Value = 0.05m, Width = 80 };
            _antiDeadzoneValueLabel = new Label { Text = "0.05", AutoSize = true };

            // Smoothing (EmaAlpha)
            var smoothingLabel = new Label { Text = "Smoothing:", AutoSize = true };
            _smoothingTrackBar = new TrackBar { Minimum = 0, Maximum = 100, Value = 35, TickFrequency = 10 };
            _smoothingNumeric = new NumericUpDown { Minimum = 0.0m, Maximum = 1.0m, DecimalPlaces = 2, Increment = 0.05m, Value = 0.35m, Width = 80 };
            _smoothingValueLabel = new Label { Text = "0.35", AutoSize = true };

            // Scale X
            var scaleXLabel = new Label { Text = "X Scale:", AutoSize = true };
            _scaleXTrackBar = new TrackBar { Minimum = 10, Maximum = 300, Value = 100, TickFrequency = 20 };
            _scaleXNumeric = new NumericUpDown { Minimum = 0.1m, Maximum = 3.0m, DecimalPlaces = 2, Increment = 0.1m, Value = 1.0m, Width = 80 };
            _scaleXValueLabel = new Label { Text = "1.00", AutoSize = true };

            // Scale Y
            var scaleYLabel = new Label { Text = "Y Scale:", AutoSize = true };
            _scaleYTrackBar = new TrackBar { Minimum = 10, Maximum = 300, Value = 100, TickFrequency = 20 };
            _scaleYNumeric = new NumericUpDown { Minimum = 0.1m, Maximum = 3.0m, DecimalPlaces = 2, Increment = 0.1m, Value = 1.0m, Width = 80 };
            _scaleYValueLabel = new Label { Text = "1.00", AutoSize = true };

            // Add controls to layout
            layout.Controls.Add(antiDeadzoneLabel, 0, 0);
            layout.Controls.Add(_antiDeadzoneTrackBar!, 1, 0);
            layout.Controls.Add(_antiDeadzoneNumeric!, 2, 0);
            layout.Controls.Add(_antiDeadzoneValueLabel!, 3, 0);

            layout.Controls.Add(smoothingLabel, 0, 1);
            layout.Controls.Add(_smoothingTrackBar!, 1, 1);
            layout.Controls.Add(_smoothingNumeric!, 2, 1);
            layout.Controls.Add(_smoothingValueLabel!, 3, 1);

            layout.Controls.Add(scaleXLabel, 0, 2);
            layout.Controls.Add(_scaleXTrackBar!, 1, 2);
            layout.Controls.Add(_scaleXNumeric!, 2, 2);
            layout.Controls.Add(_scaleXValueLabel!, 3, 2);

            layout.Controls.Add(scaleYLabel, 0, 3);
            layout.Controls.Add(_scaleYTrackBar!, 1, 3);
            layout.Controls.Add(_scaleYNumeric!, 2, 3);
            layout.Controls.Add(_scaleYValueLabel!, 3, 3);

            _advancedGroup.Controls.Add(layout);

            // Wire up events
            _antiDeadzoneTrackBar!.ValueChanged += AntiDeadzoneTrackBar_ValueChanged;
            _antiDeadzoneNumeric!.ValueChanged += AntiDeadzoneNumeric_ValueChanged;
            _smoothingTrackBar!.ValueChanged += SmoothingTrackBar_ValueChanged;
            _smoothingNumeric!.ValueChanged += SmoothingNumeric_ValueChanged;
            _scaleXTrackBar!.ValueChanged += ScaleXTrackBar_ValueChanged;
            _scaleXNumeric!.ValueChanged += ScaleXNumeric_ValueChanged;
            _scaleYTrackBar!.ValueChanged += ScaleYTrackBar_ValueChanged;
            _scaleYNumeric!.ValueChanged += ScaleYNumeric_ValueChanged;
        }

        private void CreatePreviewGroup()
        {
            _previewGroup = new GroupBox
            {
                Text = "Preview & Information",
                Size = new Size(560, 100),
                Padding = new Padding(10)
            };

            _previewLabel = new Label
            {
                Text = "Move your mouse to see live preview of sensitivity settings...",
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleCenter,
                BorderStyle = BorderStyle.FixedSingle,
                BackColor = Color.LightGray
            };

            _previewGroup.Controls.Add(_previewLabel!);
        }

        private Panel CreateButtonPanel()
        {
            var buttonPanel = new FlowLayoutPanel
            {
                FlowDirection = FlowDirection.RightToLeft,
                Dock = DockStyle.Fill,
                Height = 40
            };

            _cancelButton = new Button { Text = "Cancel", DialogResult = DialogResult.Cancel, Width = 75 };
            _okButton = new Button { Text = "OK", DialogResult = DialogResult.OK, Width = 75 };
            _applyButton = new Button { Text = "Apply", Width = 75 };
            _resetButton = new Button { Text = "Reset to Defaults", Width = 120 };

            buttonPanel.Controls.Add(_cancelButton!);
            buttonPanel.Controls.Add(_okButton!);
            buttonPanel.Controls.Add(_applyButton!);
            buttonPanel.Controls.Add(_resetButton!);

            // Wire up events
            _okButton!.Click += OkButton_Click;
            _applyButton!.Click += ApplyButton_Click;
            _resetButton!.Click += ResetButton_Click;

            return buttonPanel;
        }

        private void CreateStatusStrip()
        {
            _statusStrip = new StatusStrip();
            _statusLabel = new ToolStripStatusLabel("Ready");
            _statusStrip!.Items.Add(_statusLabel!);
        }

        #region Event Handlers

        private void SensitivityTrackBar_ValueChanged(object? sender, EventArgs e)
        {
            if (_updatingControls || _sensitivityTrackBar == null || _sensitivityNumeric == null || _sensitivityValueLabel == null) return;

            _updatingControls = true;
            var value = _sensitivityTrackBar.Value / 100.0m;
            _sensitivityNumeric.Value = Math.Max(_sensitivityNumeric.Minimum, Math.Min(_sensitivityNumeric.Maximum, value));
            _sensitivityValueLabel.Text = value.ToString("F2");
            _updatingControls = false;

            SettingsChanged(sender, e);
        }

        private void SensitivityNumeric_ValueChanged(object? sender, EventArgs e)
        {
            if (_updatingControls || _sensitivityTrackBar == null || _sensitivityNumeric == null || _sensitivityValueLabel == null) return;

            _updatingControls = true;
            var value = (int)(_sensitivityNumeric.Value * 100);
            _sensitivityTrackBar.Value = Math.Max(_sensitivityTrackBar.Minimum, Math.Min(_sensitivityTrackBar.Maximum, value));
            _sensitivityValueLabel.Text = _sensitivityNumeric.Value.ToString("F2");
            _updatingControls = false;

            SettingsChanged(sender, e);
        }

        private void ExpoTrackBar_ValueChanged(object? sender, EventArgs e)
        {
            if (_updatingControls || _expoTrackBar == null || _expoNumeric == null || _expoValueLabel == null) return;

            _updatingControls = true;
            var value = _expoTrackBar.Value / 100.0m;
            _expoNumeric.Value = value;
            _expoValueLabel.Text = value.ToString("F2");
            _updatingControls = false;

            SettingsChanged(sender, e);
        }

        private void ExpoNumeric_ValueChanged(object? sender, EventArgs e)
        {
            if (_updatingControls || _expoTrackBar == null || _expoNumeric == null || _expoValueLabel == null) return;

            _updatingControls = true;
            var value = (int)(_expoNumeric.Value * 100);
            _expoTrackBar.Value = Math.Max(_expoTrackBar.Minimum, Math.Min(_expoTrackBar.Maximum, value));
            _expoValueLabel.Text = _expoNumeric.Value.ToString("F2");
            _updatingControls = false;

            SettingsChanged(sender, e);
        }

        private void AntiDeadzoneTrackBar_ValueChanged(object? sender, EventArgs e)
        {
            if (_updatingControls || _antiDeadzoneTrackBar == null || _antiDeadzoneNumeric == null || _antiDeadzoneValueLabel == null) return;

            _updatingControls = true;
            var value = _antiDeadzoneTrackBar.Value / 100.0m;
            _antiDeadzoneNumeric.Value = value;
            _antiDeadzoneValueLabel.Text = value.ToString("F2");
            _updatingControls = false;

            SettingsChanged(sender, e);
        }

        private void AntiDeadzoneNumeric_ValueChanged(object? sender, EventArgs e)
        {
            if (_updatingControls || _antiDeadzoneTrackBar == null || _antiDeadzoneNumeric == null || _antiDeadzoneValueLabel == null) return;

            _updatingControls = true;
            var value = (int)(_antiDeadzoneNumeric.Value * 100);
            _antiDeadzoneTrackBar.Value = Math.Max(_antiDeadzoneTrackBar.Minimum, Math.Min(_antiDeadzoneTrackBar.Maximum, value));
            _antiDeadzoneValueLabel.Text = _antiDeadzoneNumeric.Value.ToString("F2");
            _updatingControls = false;

            SettingsChanged(sender, e);
        }

        private void SmoothingTrackBar_ValueChanged(object? sender, EventArgs e)
        {
            if (_updatingControls || _smoothingTrackBar == null || _smoothingNumeric == null || _smoothingValueLabel == null) return;

            _updatingControls = true;
            var value = _smoothingTrackBar.Value / 100.0m;
            _smoothingNumeric.Value = value;
            _smoothingValueLabel.Text = value.ToString("F2");
            _updatingControls = false;

            SettingsChanged(sender, e);
        }

        private void SmoothingNumeric_ValueChanged(object? sender, EventArgs e)
        {
            if (_updatingControls || _smoothingTrackBar == null || _smoothingNumeric == null || _smoothingValueLabel == null) return;

            _updatingControls = true;
            var value = (int)(_smoothingNumeric.Value * 100);
            _smoothingTrackBar.Value = Math.Max(_smoothingTrackBar.Minimum, Math.Min(_smoothingTrackBar.Maximum, value));
            _smoothingValueLabel.Text = _smoothingNumeric.Value.ToString("F2");
            _updatingControls = false;

            SettingsChanged(sender, e);
        }

        private void ScaleXTrackBar_ValueChanged(object? sender, EventArgs e)
        {
            if (_updatingControls || _scaleXTrackBar == null || _scaleXNumeric == null || _scaleXValueLabel == null) return;

            _updatingControls = true;
            var value = _scaleXTrackBar.Value / 100.0m;
            _scaleXNumeric.Value = value;
            _scaleXValueLabel.Text = value.ToString("F2");
            _updatingControls = false;

            SettingsChanged(sender, e);
        }

        private void ScaleXNumeric_ValueChanged(object? sender, EventArgs e)
        {
            if (_updatingControls || _scaleXTrackBar == null || _scaleXNumeric == null || _scaleXValueLabel == null) return;

            _updatingControls = true;
            var value = (int)(_scaleXNumeric.Value * 100);
            _scaleXTrackBar.Value = Math.Max(_scaleXTrackBar.Minimum, Math.Min(_scaleXTrackBar.Maximum, value));
            _scaleXValueLabel.Text = _scaleXNumeric.Value.ToString("F2");
            _updatingControls = false;

            SettingsChanged(sender, e);
        }

        private void ScaleYTrackBar_ValueChanged(object? sender, EventArgs e)
        {
            if (_updatingControls || _scaleYTrackBar == null || _scaleYNumeric == null || _scaleYValueLabel == null) return;

            _updatingControls = true;
            var value = _scaleYTrackBar.Value / 100.0m;
            _scaleYNumeric.Value = value;
            _scaleYValueLabel.Text = value.ToString("F2");
            _updatingControls = false;

            SettingsChanged(sender, e);
        }

        private void ScaleYNumeric_ValueChanged(object? sender, EventArgs e)
        {
            if (_updatingControls || _scaleYTrackBar == null || _scaleYNumeric == null || _scaleYValueLabel == null) return;

            _updatingControls = true;
            var value = (int)(_scaleYNumeric.Value * 100);
            _scaleYTrackBar.Value = Math.Max(_scaleYTrackBar.Minimum, Math.Min(_scaleYTrackBar.Maximum, value));
            _scaleYValueLabel.Text = _scaleYNumeric.Value.ToString("F2");
            _updatingControls = false;

            SettingsChanged(sender, e);
        }

        private void SettingsChanged(object? sender, EventArgs e)
        {
            if (_statusLabel != null)
                _statusLabel.Text = "Settings modified - Click Apply to save changes";

            UpdatePreview();
        }

        private void OkButton_Click(object? sender, EventArgs e)
        {
            ApplySettings();
            DialogResult = DialogResult.OK;
            Close();
        }

        private void ApplyButton_Click(object? sender, EventArgs e)
        {
            ApplySettings();
        }

        private void ResetButton_Click(object? sender, EventArgs e)
        {
            ResetToDefaults();
        }

        #endregion

        #region Settings Management

        private void LoadCurrentSettings()
        {
            try
            {
                var profile = _profiles.Current;

                _updatingControls = true;

                // Load sensitivity
                if (_sensitivityNumeric != null && _sensitivityTrackBar != null && _sensitivityValueLabel != null)
                {
                    _sensitivityNumeric.Value = (decimal)profile.Curves.Sensitivity;
                    _sensitivityTrackBar.Value = (int)(profile.Curves.Sensitivity * 100);
                    _sensitivityValueLabel.Text = profile.Curves.Sensitivity.ToString("F2");
                }

                // Load expo
                if (_expoNumeric != null && _expoTrackBar != null && _expoValueLabel != null)
                {
                    _expoNumeric.Value = (decimal)profile.Curves.Expo;
                    _expoTrackBar.Value = (int)(profile.Curves.Expo * 100);
                    _expoValueLabel.Text = profile.Curves.Expo.ToString("F2");
                }

                // Load anti-deadzone
                if (_antiDeadzoneNumeric != null && _antiDeadzoneTrackBar != null && _antiDeadzoneValueLabel != null)
                {
                    _antiDeadzoneNumeric.Value = (decimal)profile.Curves.AntiDeadzone;
                    _antiDeadzoneTrackBar.Value = (int)(profile.Curves.AntiDeadzone * 100);
                    _antiDeadzoneValueLabel.Text = profile.Curves.AntiDeadzone.ToString("F2");
                }

                // Load smoothing (EmaAlpha)
                if (_smoothingNumeric != null && _smoothingTrackBar != null && _smoothingValueLabel != null)
                {
                    _smoothingNumeric.Value = (decimal)profile.Curves.EmaAlpha;
                    _smoothingTrackBar.Value = (int)(profile.Curves.EmaAlpha * 100);
                    _smoothingValueLabel.Text = profile.Curves.EmaAlpha.ToString("F2");
                }

                // Load scale values
                if (_scaleXNumeric != null && _scaleXTrackBar != null && _scaleXValueLabel != null)
                {
                    _scaleXNumeric.Value = (decimal)profile.Curves.ScaleX;
                    _scaleXTrackBar.Value = (int)(profile.Curves.ScaleX * 100);
                    _scaleXValueLabel.Text = profile.Curves.ScaleX.ToString("F2");
                }

                if (_scaleYNumeric != null && _scaleYTrackBar != null && _scaleYValueLabel != null)
                {
                    _scaleYNumeric.Value = (decimal)profile.Curves.ScaleY;
                    _scaleYTrackBar.Value = (int)(profile.Curves.ScaleY * 100);
                    _scaleYValueLabel.Text = profile.Curves.ScaleY.ToString("F2");
                }

                // Load mouse DPI
                if (_mouseDpiNumeric != null)
                {
                    _mouseDpiNumeric.Value = profile.MouseDpi;
                }

                _updatingControls = false;

                if (_statusLabel != null)
                    _statusLabel.Text = "Settings loaded from current profile";
            }
            catch (Exception ex)
            {
                Logger.Error("Error loading mouse settings", ex);
                if (_statusLabel != null)
                    _statusLabel.Text = $"Error loading settings: {ex.Message}";
            }
        }

        private void ApplySettings()
        {
            try
            {
                var profile = _profiles.Current;

                // Update profile with new values
                profile.Curves.Sensitivity = (float)(_sensitivityNumeric?.Value ?? 1.5m);
                profile.Curves.Expo = (float)(_expoNumeric?.Value ?? 0.6m);
                profile.Curves.AntiDeadzone = (float)(_antiDeadzoneNumeric?.Value ?? 0.05m);
                profile.Curves.EmaAlpha = (float)(_smoothingNumeric?.Value ?? 0.35m);
                profile.Curves.ScaleX = (float)(_scaleXNumeric?.Value ?? 1.0m);
                profile.Curves.ScaleY = (float)(_scaleYNumeric?.Value ?? 1.0m);
                profile.MouseDpi = (int)(_mouseDpiNumeric?.Value ?? 1600);

                // Save the profile
                _profiles.Save();

                if (_statusLabel != null)
                    _statusLabel.Text = "Settings applied and saved successfully";

                Logger.Info("Mouse settings applied: Sensitivity={Sensitivity:F2}, Expo={Expo:F2}, DPI={Dpi}", profile.Curves.Sensitivity, profile.Curves.Expo, profile.MouseDpi);
            }
            catch (Exception ex)
            {
                Logger.Error("Error applying mouse settings", ex);
                if (_statusLabel != null)
                    _statusLabel.Text = $"Error applying settings: {ex.Message}";

                MessageBox.Show($"Error applying settings: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void ResetToDefaults()
        {
            if (MessageBox.Show("Reset all mouse settings to defaults?", "Confirm Reset",
                MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
                return;

            _updatingControls = true;

            // Reset to MouseSettings.Defaults equivalent values
            if (_sensitivityNumeric != null) _sensitivityNumeric.Value = 1.0m;
            if (_expoNumeric != null) _expoNumeric.Value = 0.6m;
            if (_antiDeadzoneNumeric != null) _antiDeadzoneNumeric.Value = 0.05m;
            if (_smoothingNumeric != null) _smoothingNumeric.Value = 0.35m;
            if (_scaleXNumeric != null) _scaleXNumeric.Value = 1.0m;
            if (_scaleYNumeric != null) _scaleYNumeric.Value = 1.0m;
            if (_mouseDpiNumeric != null) _mouseDpiNumeric.Value = 1600m;

            _updatingControls = false;

            // Trigger updates
            SensitivityNumeric_ValueChanged(null, EventArgs.Empty);
            ExpoNumeric_ValueChanged(null, EventArgs.Empty);
            AntiDeadzoneNumeric_ValueChanged(null, EventArgs.Empty);
            SmoothingNumeric_ValueChanged(null, EventArgs.Empty);
            ScaleXNumeric_ValueChanged(null, EventArgs.Empty);
            ScaleYNumeric_ValueChanged(null, EventArgs.Empty);

            if (_statusLabel != null)
                _statusLabel.Text = "Settings reset to defaults - Click Apply to save";
        }

        private void UpdatePreview()
        {
            if (_previewLabel == null) return;

            try
            {
                var sensitivity = _sensitivityNumeric?.Value ?? 1.0m;
                var expo = _expoNumeric?.Value ?? 0.6m;
                var dpi = _mouseDpiNumeric?.Value ?? 1600m;

                var effectiveSensitivity = (float)(sensitivity * (dpi / 800.0m)); // Normalize to 800 DPI baseline

                string description;
                if (effectiveSensitivity < 1.0f)
                    description = "Low sensitivity - Precise movements";
                else if (effectiveSensitivity < 2.0f)
                    description = "Medium sensitivity - Balanced control";
                else if (effectiveSensitivity < 4.0f)
                    description = "High sensitivity - Fast movements";
                else
                    description = "Very high sensitivity - Quick response";

                _previewLabel.Text = $"Effective Sensitivity: {effectiveSensitivity:F2}\n{description}\n" +
                                   $"Expo: {expo:F2} (Response curve)\n" +
                                   $"DPI: {dpi} | Sensitivity: {sensitivity:F2}";
            }
            catch (Exception ex)
            {
                _previewLabel.Text = $"Preview error: {ex.Message}";
            }
        }

        #endregion
    }
}