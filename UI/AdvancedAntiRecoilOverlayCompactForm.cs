using System;
using System.Drawing;
using System.Windows.Forms;
using System.IO;
using WootMouseRemap.Features;
using WootMouseRemap.Security;

namespace WootMouseRemap.UI
{
    public partial class AdvancedAntiRecoilOverlayCompactForm : Form
    {
        private readonly AntiRecoil _antiRecoil = null!;

        // UI Controls
        private GroupBox? _settingsGroup;
        private CheckBox? _enabledCheckBox;
        private TrackBar? _strengthTrackBar;
        private NumericUpDown? _strengthNumeric;
        private TrackBar? _delayTrackBar;
        private NumericUpDown? _delayNumeric;
        private Button? _applyButton;
        private Button? _resetButton;
        private Button? _closeButton;

        // Telemetry controls
        private GroupBox? _telemetryGroup;
        private Label? _rawInputLabel;
        private Label? _processedOutputLabel;
        private Label? _compensationLabel;
        private Label? _statusLabel;

        // Pattern recording controls
        private GroupBox? _patternGroup;
        private Button? _recordButton;
        private Button? _stopButton;
        private ComboBox? _patternComboBox;
        private Button? _playButton;
        private Label? _recordingStatusLabel;

        // Preview graph controls
        private GroupBox? _previewGroup;
        private Panel? _graphPanel;

        private bool _updatingControls = false;

        // Validation system
        private ValidationSystem? _validationSystem;

        // Settings persistence
        private CompactFormSettings _formSettings = new();

        // Telemetry
        private WootMouseRemap.Telemetry? _telemetry;
        private System.Windows.Forms.Timer? _telemetryTimer;

        // Tooltips
        private ToolTip? _toolTip;

        private static readonly Size FallbackClientSize = new Size(775, 550);

        public AdvancedAntiRecoilOverlayCompactForm()
        {
            try
            {
                // This constructor is for the OverlayForm button click
                // We need to get the AntiRecoil instance from the OverlayForm
                var overlay = SecureFormAccess.GetOverlayForm() as OverlayForm;
                if (overlay == null)
                {
                    Logger.Warn("AdvancedAntiRecoilOverlayCompactForm: OverlayForm not found in secure registry; creating local AntiRecoil fallback");
                    // Fall back to a local AntiRecoil instance so the settings form can open standalone
                    _antiRecoil = new AntiRecoil();
                }
                else
                {
                    _antiRecoil = overlay.AntiRecoil ?? new AntiRecoil();
                }

                // Remove unsafe reflection access - telemetry will be null
                _telemetry = null;
                Logger.Info("Telemetry access disabled for security");

                InitializeComponent();
                InitializeValidation();
                LoadFormSettings();
                LoadCurrentSettings();
                Logger.Info("AdvancedAntiRecoilOverlayCompactForm initialized successfully");

                // Set up accessibility
                SetupAccessibility();
            }
            catch (Exception ex)
            {
                Logger.Error("Failed to initialize AdvancedAntiRecoilOverlayCompactForm", ex);
                MessageBox.Show($"Failed to initialize anti-recoil settings form: {ex.Message}", "Initialization Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                Close();
            }
        }

        private void InitializeComponent()
        {
            // Form properties
            Text = "Anti-Recoil Settings (Compact)";
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
                Text = "Anti-Recoil Settings",
                Location = new Point(10, 10),
                Size = new Size(ClientSize.Width - 20, ClientSize.Height - 60),
                Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right
            };

            var layout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(10),
                RowCount = 4,
                ColumnCount = 3
            };

            // Configure column styles
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize)); // Labels
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 60)); // Sliders
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize)); // Numerics

            // Configure rows
            for (int i = 0; i < 4; i++)
                layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 50));

            int row = 0;

            // Enabled checkbox (spans all columns)
            _enabledCheckBox = new CheckBox
            {
                Text = "Enable Anti-Recoil",
                ForeColor = Color.White,
                Anchor = AnchorStyles.Left | AnchorStyles.Right
            };
            layout.Controls.Add(_enabledCheckBox, 0, row);
            layout.SetColumnSpan(_enabledCheckBox, 3);
            row++;

            // Strength
            var strengthLabel = new Label { Text = "Strength:", AutoSize = true };
            _strengthTrackBar = new TrackBar
            {
                Minimum = 0,
                Maximum = 100,
                Value = 60,
                TickFrequency = 10,
                Anchor = AnchorStyles.Left | AnchorStyles.Right
            };
            _strengthNumeric = new NumericUpDown
            {
                Minimum = 0.0m,
                Maximum = 1.0m,
                DecimalPlaces = 2,
                Increment = 0.05m,
                Value = 0.6m,
                Width = 80
            };

            // Activation Delay
            var delayLabel = new Label { Text = "Activation Delay:", AutoSize = true };
            _delayTrackBar = new TrackBar
            {
                Minimum = 0,
                Maximum = 1000,
                Value = 100,
                TickFrequency = 100,
                Anchor = AnchorStyles.Left | AnchorStyles.Right
            };
            _delayNumeric = new NumericUpDown
            {
                Minimum = 0,
                Maximum = 1000,
                Value = 100,
                Width = 80
            };

            // Add controls to layout
            layout.Controls.Add(strengthLabel, 0, row);
            layout.Controls.Add(_strengthTrackBar!, 1, row);
            layout.Controls.Add(_strengthNumeric!, 2, row);
            row++;

            layout.Controls.Add(delayLabel, 0, row);
            layout.Controls.Add(_delayTrackBar!, 1, row);
            layout.Controls.Add(_delayNumeric!, 2, row);
            row++;

            // Buttons
            _applyButton = new Button { Text = "Apply", Width = 75, Anchor = AnchorStyles.Right };
            _resetButton = new Button { Text = "Reset", Width = 75, Anchor = AnchorStyles.Right };
            _closeButton = new Button { Text = "Close", Width = 75, Anchor = AnchorStyles.Right };

            // Button row
            var buttonPanel = new FlowLayoutPanel
            {
                FlowDirection = FlowDirection.RightToLeft,
                Dock = DockStyle.Fill
            };
            buttonPanel.Controls.Add(_closeButton!);
            buttonPanel.Controls.Add(_applyButton!);
            buttonPanel.Controls.Add(_resetButton!);
            layout.Controls.Add(buttonPanel, 0, row);
            layout.SetColumnSpan(buttonPanel, 3);

            _settingsGroup.Controls.Add(layout);
            Controls.Add(_settingsGroup);

            // Add telemetry group
            InitializeTelemetryControls();

            // Add pattern recording group
            InitializePatternControls();

            // Add preview graph group
            InitializePreviewGraphControls();

            // Wire up events
            _enabledCheckBox!.CheckedChanged += SettingsChanged;
            _strengthTrackBar!.ValueChanged += StrengthTrackBar_ValueChanged;
            _strengthNumeric!.ValueChanged += StrengthNumeric_ValueChanged;
            _delayTrackBar!.ValueChanged += DelayTrackBar_ValueChanged;
            _delayNumeric!.ValueChanged += DelayNumeric_ValueChanged;

            _applyButton!.Click += ApplyButton_Click;
            _resetButton!.Click += ResetButton_Click;
            _closeButton!.Click += (s, e) => Close();

            // Add keyboard shortcuts
            KeyDown += CompactForm_KeyDown;
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
            if (_enabledCheckBox != null)
                _toolTip.SetToolTip(_enabledCheckBox, "Enable or disable the anti-recoil compensation system");

            if (_strengthTrackBar != null)
                _toolTip.SetToolTip(_strengthTrackBar, "Adjust the strength of anti-recoil compensation (0-100%)");

            if (_strengthNumeric != null)
                _toolTip.SetToolTip(_strengthNumeric, "Precise strength value (0.00-1.00). Higher values provide stronger compensation");

            if (_delayTrackBar != null)
                _toolTip.SetToolTip(_delayTrackBar, "Set the delay before anti-recoil activates after mouse movement (0-1000ms)");

            if (_delayNumeric != null)
                _toolTip.SetToolTip(_delayNumeric, "Precise activation delay in milliseconds. Longer delays may miss initial recoil");

            if (_applyButton != null)
                _toolTip.SetToolTip(_applyButton, "Apply the current settings to the anti-recoil system (Ctrl+A or Ctrl+S)");

            if (_resetButton != null)
                _toolTip.SetToolTip(_resetButton, "Reset all settings to their default values (Ctrl+R)");

            if (_closeButton != null)
                _toolTip.SetToolTip(_closeButton, "Close this settings window (Escape key)");

            // Pattern recording tooltips
            if (_recordButton != null)
                _toolTip.SetToolTip(_recordButton, "Start recording a new anti-recoil pattern from mouse movements");

            if (_stopButton != null)
                _toolTip.SetToolTip(_stopButton, "Stop the current pattern recording");

            if (_patternComboBox != null)
                _toolTip.SetToolTip(_patternComboBox, "Select a recorded pattern to play or analyze");

            if (_playButton != null)
                _toolTip.SetToolTip(_playButton, "Play the selected pattern to simulate anti-recoil compensation");

            // Telemetry tooltips
            if (_rawInputLabel != null)
                _toolTip.SetToolTip(_rawInputLabel, "Raw mouse input coordinates before any processing");

            if (_processedOutputLabel != null)
                _toolTip.SetToolTip(_processedOutputLabel, "Processed output sent to the virtual controller");

            if (_compensationLabel != null)
                _toolTip.SetToolTip(_compensationLabel, "Current anti-recoil compensation being applied");

            if (_statusLabel != null)
                _toolTip.SetToolTip(_statusLabel, "Current status of the anti-recoil system");

            // Graph panel tooltip
            if (_graphPanel != null)
                _toolTip.SetToolTip(_graphPanel, "Visual preview of current anti-recoil settings and status");
        }

        private void InitializeTelemetryControls()
        {
            // Telemetry group
            _telemetryGroup = new GroupBox
            {
                Text = "Live Telemetry",
                Location = new Point(10, _settingsGroup!.Bottom + 10),
                Size = new Size(ClientSize.Width - 20, 120),
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

            // Raw input
            var rawLabel = new Label { Text = "Raw Input:", AutoSize = true, ForeColor = Color.White };
            _rawInputLabel = new Label { Text = "X: 0, Y: 0", AutoSize = true, ForeColor = Color.Cyan };
            telemetryLayout.Controls.Add(rawLabel, 0, row);
            telemetryLayout.Controls.Add(_rawInputLabel, 1, row);
            row++;

            // Processed output
            var outputLabel = new Label { Text = "Stick Output:", AutoSize = true, ForeColor = Color.White };
            _processedOutputLabel = new Label { Text = "RX: 0, RY: 0", AutoSize = true, ForeColor = Color.Cyan };
            telemetryLayout.Controls.Add(outputLabel, 0, row);
            telemetryLayout.Controls.Add(_processedOutputLabel, 1, row);
            row++;

            // Compensation
            var compLabel = new Label { Text = "Compensation:", AutoSize = true, ForeColor = Color.White };
            _compensationLabel = new Label { Text = "0.00", AutoSize = true, ForeColor = Color.Yellow };
            telemetryLayout.Controls.Add(compLabel, 0, row);
            telemetryLayout.Controls.Add(_compensationLabel, 1, row);
            row++;

            // Status
            var statusLabel = new Label { Text = "Status:", AutoSize = true, ForeColor = Color.White };
            _statusLabel = new Label { Text = "Ready", AutoSize = true, ForeColor = Color.Lime };
            telemetryLayout.Controls.Add(statusLabel, 0, row);
            telemetryLayout.Controls.Add(_statusLabel, 1, row);

            _telemetryGroup.Controls.Add(telemetryLayout);
            Controls.Add(_telemetryGroup);

            // Set up telemetry timer
            _telemetryTimer = new System.Windows.Forms.Timer { Interval = 100 }; // 10 FPS
            _telemetryTimer.Tick += UpdateTelemetryDisplay;
            _telemetryTimer.Start();
        }

        private void InitializePatternControls()
        {
            // Pattern recording group
            _patternGroup = new GroupBox
            {
                Text = "Pattern Recording",
                Location = new Point(10, _telemetryGroup!.Bottom + 10),
                Size = new Size(ClientSize.Width - 20, 100),
                Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right,
                ForeColor = Color.White
            };

            var patternLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(10),
                RowCount = 3,
                ColumnCount = 4
            };

            // Configure columns
            patternLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25));
            patternLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25));
            patternLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25));
            patternLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25));

            int row = 0;

            // Recording buttons
            _recordButton = new Button { Text = "Record", Width = 70, Height = 30, ForeColor = Color.Black };
            _stopButton = new Button { Text = "Stop", Width = 70, Height = 30, ForeColor = Color.Black, Enabled = false };

            patternLayout.Controls.Add(_recordButton, 0, row);
            patternLayout.Controls.Add(_stopButton, 1, row);

            // Pattern selection
            var patternLabel = new Label { Text = "Pattern:", AutoSize = true, ForeColor = Color.White };
            _patternComboBox = new ComboBox { Width = 120, DropDownStyle = ComboBoxStyle.DropDownList };
            _playButton = new Button { Text = "Play", Width = 70, Height = 30, ForeColor = Color.Black };

            patternLayout.Controls.Add(patternLabel, 2, row);
            patternLayout.Controls.Add(_patternComboBox, 3, row);
            row++;

            // Play button and status
            patternLayout.Controls.Add(_playButton, 0, row);
            patternLayout.SetColumnSpan(_playButton, 2);

            _recordingStatusLabel = new Label { Text = "Ready", AutoSize = true, ForeColor = Color.Lime, Font = new Font(Font, FontStyle.Bold) };
            patternLayout.Controls.Add(_recordingStatusLabel, 2, row);
            patternLayout.SetColumnSpan(_recordingStatusLabel, 2);

            _patternGroup.Controls.Add(patternLayout);
            Controls.Add(_patternGroup);

            // Wire up pattern events
            _recordButton!.Click += RecordButton_Click;
            _stopButton!.Click += StopButton_Click;
            _playButton!.Click += PlayButton_Click;
            _patternComboBox!.SelectedIndexChanged += PatternComboBox_SelectedIndexChanged;

            // Subscribe to AntiRecoil events
            _antiRecoil.RecordingStarted += OnRecordingStarted;
            _antiRecoil.RecordingStopped += OnRecordingStopped;
            _antiRecoil.PatternListChanged += OnPatternListChanged;

            // Load initial patterns
            LoadPatterns();
        }

        private void InitializePreviewGraphControls()
        {
            // Preview graph group
            _previewGroup = new GroupBox
            {
                Text = "Settings Preview",
                Location = new Point(10, _patternGroup!.Bottom + 10),
                Size = new Size(ClientSize.Width - 20, 120),
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
            _strengthNumeric!.ValueChanged += (s, e) => _graphPanel?.Invalidate();
            _delayNumeric!.ValueChanged += (s, e) => _graphPanel?.Invalidate();
            _enabledCheckBox!.CheckedChanged += (s, e) => _graphPanel?.Invalidate();
        }

        private void GraphPanel_Paint(object? sender, PaintEventArgs e)
        {
            if (_graphPanel == null || _strengthNumeric == null || _delayNumeric == null || _enabledCheckBox == null) return;

            var g = e.Graphics;
            var rect = _graphPanel.ClientRectangle;
            rect.Inflate(-10, -10);

            // Clear background
            g.FillRectangle(Brushes.Black, rect);

            using var font = new Font("Arial", 8);
            using var brush = new SolidBrush(Color.White);

            // Draw strength bar
            var strength = (float)_strengthNumeric.Value;
            var strengthRect = new Rectangle(rect.Left, rect.Top + 20, (int)(rect.Width * strength), 20);
            using var strengthBrush = new SolidBrush(_enabledCheckBox.Checked ? Color.Red : Color.Gray);
            g.FillRectangle(strengthBrush, strengthRect);
            g.DrawRectangle(Pens.White, strengthRect);
            g.DrawString($"Strength: {strength:F2}", font, brush, rect.Left, rect.Top);

            // Draw delay indicator
            var delay = (int)_delayNumeric.Value;
            var delayX = rect.Left + (rect.Width * delay) / 1000; // Scale delay to graph width
            using var delayPen = new Pen(Color.Yellow, 2);
            g.DrawLine(delayPen, delayX, rect.Top + 50, delayX, rect.Bottom);
            g.DrawString($"Activation Delay: {delay}ms", font, brush, rect.Left, rect.Top + 45);

            // Draw status
            var status = _enabledCheckBox.Checked ? "ENABLED" : "DISABLED";
            var statusColor = _enabledCheckBox.Checked ? Color.Lime : Color.Gray;
            using var statusBrush = new SolidBrush(statusColor);
            g.DrawString($"Status: {status}", font, statusBrush, rect.Left, rect.Bottom - 15);
        }

        private void RecordButton_Click(object? sender, EventArgs e)
        {
            try
            {
                string patternName = $"Pattern_{DateTime.Now:HHmmss}";
                if (_antiRecoil.StartPatternRecording(patternName))
                {
                    _recordButton!.Enabled = false;
                    _stopButton!.Enabled = true;
                    _recordingStatusLabel!.Text = "Recording...";
                    _recordingStatusLabel!.ForeColor = Color.Red;
                    Logger.Info("Started recording pattern: {PatternName}", patternName);
                }
                else
                {
                    MessageBox.Show("Failed to start recording. Another recording may be in progress.", "Recording Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
            catch (Exception ex)
            {
                Logger.Error("Error starting pattern recording", ex);
                MessageBox.Show($"Error starting recording: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void StopButton_Click(object? sender, EventArgs e)
        {
            try
            {
                var pattern = _antiRecoil.StopPatternRecording(true);
                if (pattern != null)
                {
                    Logger.Info("Stopped recording pattern: {PatternName} ({SampleCount} samples)", pattern.Name, pattern.Samples.Count);
                    MessageBox.Show($"Pattern '{pattern.Name}' recorded with {pattern.Samples.Count} samples.", "Recording Complete", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                else
                {
                    Logger.Info("Recording stopped with no pattern saved");
                    MessageBox.Show("Recording stopped. No pattern was saved.", "Recording Stopped", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            }
            catch (Exception ex)
            {
                Logger.Error("Error stopping pattern recording", ex);
                MessageBox.Show($"Error stopping recording: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void PlayButton_Click(object? sender, EventArgs e)
        {
            if (_patternComboBox?.SelectedItem is not string patternName || string.IsNullOrEmpty(patternName))
            {
                MessageBox.Show("Please select a pattern to play.", "No Pattern Selected", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            try
            {
                var pattern = _antiRecoil.GetPattern(patternName);
                if (pattern == null)
                {
                    MessageBox.Show($"Pattern '{patternName}' not found.", "Pattern Not Found", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                // For now, just show simulation results
                var simulation = _antiRecoil.SimulatePattern(pattern);
                MessageBox.Show($"Pattern '{patternName}':\n" +
                    $"Samples: {pattern.Samples.Count}\n" +
                    $"Total Compensation: {simulation.TotalCompY:F2}\n" +
                    $"Average Compensation: {simulation.AvgCompY:F2}",
                    "Pattern Simulation", MessageBoxButtons.OK, MessageBoxIcon.Information);

                Logger.Info("Simulated pattern: {PatternName}", patternName);
            }
            catch (Exception ex)
            {
                Logger.Error("Error playing pattern", ex);
                MessageBox.Show($"Error playing pattern: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void PatternComboBox_SelectedIndexChanged(object? sender, EventArgs e)
        {
            // Could add pattern preview or details here
        }

        private void OnRecordingStarted()
        {
            if (InvokeRequired)
            {
                Invoke(new Action(OnRecordingStarted));
                return;
            }

            _recordButton!.Enabled = false;
            _stopButton!.Enabled = true;
            _recordingStatusLabel!.Text = "Recording...";
            _recordingStatusLabel!.ForeColor = Color.Red;
        }

        private void OnRecordingStopped()
        {
            if (InvokeRequired)
            {
                Invoke(new Action(OnRecordingStopped));
                return;
            }

            _recordButton!.Enabled = true;
            _stopButton!.Enabled = false;
            _recordingStatusLabel!.Text = "Ready";
            _recordingStatusLabel!.ForeColor = Color.Lime;
        }

        private void OnPatternListChanged()
        {
            if (InvokeRequired)
            {
                Invoke(new Action(OnPatternListChanged));
                return;
            }

            LoadPatterns();
        }

        private void LoadPatterns()
        {
            try
            {
                _patternComboBox!.Items.Clear();
                foreach (var pattern in _antiRecoil.Patterns)
                {
                    _patternComboBox.Items.Add(pattern.Name);
                }

                if (_patternComboBox.Items.Count > 0)
                {
                    _patternComboBox.SelectedIndex = 0;
                }

                Logger.Info("Loaded {PatternCount} patterns", _antiRecoil.Patterns.Count);
            }
            catch (Exception ex)
            {
                Logger.Error("Error loading patterns", ex);
            }
        }

        private void UpdateTelemetryDisplay(object? sender, EventArgs e)
        {
            if (_telemetry == null || _rawInputLabel == null || _processedOutputLabel == null || 
                _compensationLabel == null || _statusLabel == null) return;

            try
            {
                // Update raw input
                _rawInputLabel.Text = $"X: {_telemetry.RawDx}, Y: {_telemetry.RawDy}";

                // Update processed output
                _processedOutputLabel.Text = $"RX: {_telemetry.StickRX}, RY: {_telemetry.StickRY}";

                // Update compensation (from AntiRecoil)
                _compensationLabel.Text = _antiRecoil.LastAppliedCompensation.ToString("F2");

                // Update status
                var status = "Ready";
                var statusColor = Color.Lime;

                if (_antiRecoil.Enabled)
                {
                    if (_antiRecoil.IsActive)
                    {
                        status = "Active";
                        statusColor = Color.Yellow;
                    }
                    else
                    {
                        status = "Enabled";
                        statusColor = Color.Lime;
                    }
                }
                else
                {
                    status = "Disabled";
                    statusColor = Color.Gray;
                }

                _statusLabel.Text = status;
                _statusLabel.ForeColor = statusColor;
            }
            catch (Exception ex)
            {
                Logger.Warn("Error updating telemetry display: {Message}", ex.Message);
            }
        }

        private void InitializeValidation()
        {
            _validationSystem = new ValidationSystem(this);

            // Add validation rules
            if (_strengthNumeric != null)
            {
                _validationSystem.AddRule(new ValidationRule(_strengthNumeric, () =>
                {
                    var value = (float)_strengthNumeric.Value;
                    if (_enabledCheckBox?.Checked == true && value == 0)
                        return ValidationResult.Warning("Anti-recoil is enabled but strength is 0% - no compensation will be applied");
                    if (value > 0.9f)
                        return ValidationResult.Warning("Very high strength (>90%) may cause overcorrection");
                    return ValidationResult.Success();
                }, "Strength"));
            }

            if (_delayNumeric != null)
            {
                _validationSystem.AddRule(new ValidationRule(_delayNumeric, () =>
                {
                    var value = (int)_delayNumeric.Value;
                    if (value > 500)
                        return ValidationResult.Warning("High delay (>500ms) may miss initial recoil compensation");
                    return ValidationResult.Success();
                }, "ActivationDelay"));
            }

            // Wire up validation events
            _enabledCheckBox!.CheckedChanged += (s, e) => _validationSystem?.ValidateAll();
            _strengthNumeric!.ValueChanged += (s, e) => _validationSystem?.ValidateControl(_strengthNumeric);
            _delayNumeric!.ValueChanged += (s, e) => _validationSystem?.ValidateControl(_delayNumeric);

            // Initial validation
            _validationSystem.ValidateAll();
        }

        private void StrengthTrackBar_ValueChanged(object? sender, EventArgs e)
        {
            if (_updatingControls || _strengthTrackBar == null || _strengthNumeric == null) return;
            _updatingControls = true;
            var value = _strengthTrackBar.Value / 100.0m;
            _strengthNumeric.Value = Math.Max(_strengthNumeric.Minimum, Math.Min(_strengthNumeric.Maximum, value));
            _updatingControls = false;
        }

        private void StrengthNumeric_ValueChanged(object? sender, EventArgs e)
        {
            if (_updatingControls || _strengthTrackBar == null || _strengthNumeric == null) return;
            _updatingControls = true;
            var value = (int)(_strengthNumeric.Value * 100);
            _strengthTrackBar.Value = Math.Max(_strengthTrackBar.Minimum, Math.Min(_strengthTrackBar.Maximum, value));
            _updatingControls = false;
        }

        private void DelayTrackBar_ValueChanged(object? sender, EventArgs e)
        {
            if (_updatingControls || _delayTrackBar == null || _delayNumeric == null) return;
            _updatingControls = true;
            _delayNumeric.Value = _delayTrackBar.Value;
            _updatingControls = false;
        }

        private void DelayNumeric_ValueChanged(object? sender, EventArgs e)
        {
            if (_updatingControls || _delayTrackBar == null || _delayNumeric == null) return;
            _updatingControls = true;
            _delayTrackBar.Value = Math.Max(_delayTrackBar.Minimum, Math.Min(_delayTrackBar.Maximum, (int)_delayNumeric.Value));
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
                if (_antiRecoil == null)
                {
                    Logger.Error("LoadCurrentSettings: AntiRecoil instance is null");
                    MessageBox.Show("AntiRecoil system not available. Settings cannot be loaded.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                _updatingControls = true;

                // Load enabled state
                if (_enabledCheckBox != null)
                {
                    _enabledCheckBox.Checked = _antiRecoil.Enabled;
                }

                // Load strength
                if (_strengthNumeric != null && _strengthTrackBar != null)
                {
                    var strength = Math.Clamp(_antiRecoil.Strength, 0f, 1f);
                    _strengthNumeric.Value = (decimal)strength;
                    _strengthTrackBar.Value = (int)(strength * 100);
                }

                // Load activation delay
                if (_delayNumeric != null && _delayTrackBar != null)
                {
                    var delay = Math.Clamp(_antiRecoil.ActivationDelayMs, 0, 1000);
                    _delayNumeric.Value = delay;
                    _delayTrackBar.Value = delay;
                }

                _updatingControls = false;
                Logger.Info("Anti-recoil settings loaded successfully");
            }
            catch (Exception ex)
            {
                Logger.Error("Error loading anti-recoil settings", ex);
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
                if (_antiRecoil == null)
                {
                    Logger.Error("ApplySettings: AntiRecoil instance is null");
                    MessageBox.Show("AntiRecoil system not available. Settings cannot be applied.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                // Validate input values
                var enabled = _enabledCheckBox?.Checked ?? false;
                var strength = (float)(_strengthNumeric?.Value ?? 0.6m);
                var delay = (int)(_delayNumeric?.Value ?? 100);

                if (strength < 0f || strength > 1f)
                {
                    MessageBox.Show("Strength value is out of valid range (0.0 - 1.0).", "Invalid Value", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                if (delay < 0 || delay > 1000)
                {
                    MessageBox.Show("Activation delay is out of valid range (0 - 1000ms).", "Invalid Value", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                // Apply settings
                _antiRecoil.Enabled = enabled;
                _antiRecoil.Strength = strength;
                _antiRecoil.ActivationDelayMs = delay;

                // Save settings
                var overlay = SecureFormAccess.GetOverlayForm() as OverlayForm;
                if (overlay == null)
                {
                    Logger.Error("ApplySettings: Overlay not available for saving");
                    MessageBox.Show("Cannot save settings: Overlay not available.", "Save Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                // Prefer saving to ProfileService if available (new flow), otherwise fall back to legacy ProfileManager
                var svc = overlay.ProfileService;
                if (svc != null)
                {
                    try
                    {
                        // Map current anti-recoil settings into a legacy ConfigurationProfile via adapter
                        var currentConfig = overlay.ProfileManager.Current;
                        var mapped = WootMouseRemap.Core.Mapping.ProfileAdapters.MapToConfigurationProfile(svc.GetDefaultProfile());
                        // Update anti-recoil specific settings
                        mapped.Settings.Enabled = _antiRecoil.Enabled;
                        mapped.Settings.Strength = _antiRecoil.Strength;
                        mapped.Settings.ActivationDelayMs = _antiRecoil.ActivationDelayMs;
                        // Save via ProfileService by converting mapped back to InputMappingProfile
                        var toSave = WootMouseRemap.Core.Mapping.ProfileAdapters.MapFromConfigurationProfile(mapped);
                        svc.SaveProfile(toSave);
                    }
                    catch (Exception ex)
                    {
                        Logger.Error("Failed to save anti-recoil settings via ProfileService", ex);
                        MessageBox.Show("Failed to save settings via ProfileService.", "Save Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        return;
                    }
                }
                else if (overlay.ProfileManager != null)
                {
                    overlay.ProfileManager.Save();
                }
                else
                {
                    Logger.Error("ApplySettings: No profile persistence available");
                    MessageBox.Show("Cannot save settings: no profile persistence available.", "Save Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                MessageBox.Show("Anti-recoil settings applied successfully!", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
                Logger.Info("Anti-recoil settings applied: Enabled={Enabled}, Strength={Strength:F2}, Delay={Delay}ms", enabled, strength, delay);
            }
            catch (Exception ex)
            {
                Logger.Error("Error applying anti-recoil settings", ex);
                MessageBox.Show($"Error applying settings: {ex.Message}\n\nPlease check the application logs for more details.", "Apply Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void ResetToDefaults()
        {
            if (MessageBox.Show("Reset anti-recoil settings to defaults?", "Confirm Reset",
                MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
                return;

            _updatingControls = true;

            // Reset to defaults
            if (_enabledCheckBox != null) _enabledCheckBox.Checked = false;
            if (_strengthNumeric != null) _strengthNumeric.Value = 0.6m;
            if (_delayNumeric != null) _delayNumeric.Value = 100m;

            _updatingControls = false;

            // Trigger updates
            StrengthNumeric_ValueChanged(null, EventArgs.Empty);
            DelayNumeric_ValueChanged(null, EventArgs.Empty);
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
                string settingsFile = Path.Combine(AppContext.BaseDirectory, "compact_antirecoil_settings.json");

                // Path validation to prevent directory traversal attacks
                var fullPath = Path.GetFullPath(settingsFile);
                var allowedDir = Path.GetFullPath(Directory.GetCurrentDirectory());
                if (!fullPath.StartsWith(allowedDir, StringComparison.OrdinalIgnoreCase))
                    throw new UnauthorizedAccessException("Path traversal detected");

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
                        _formSettings = System.Text.Json.JsonSerializer.Deserialize<CompactFormSettings>(json, options) ?? new CompactFormSettings();
                    }
                    catch (System.Text.Json.JsonException) 
                    {
                        _formSettings = new CompactFormSettings(); // Safe fallback
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
                    if (_enabledCheckBox != null) _enabledCheckBox.Checked = _formSettings.Enabled;
                    if (_strengthNumeric != null) _strengthNumeric.Value = (decimal)_formSettings.Strength;
                    if (_delayNumeric != null) _delayNumeric.Value = _formSettings.ActivationDelayMs;

                    Logger.Info("Compact anti-recoil form settings loaded successfully");
                }
            }
            catch (Exception ex)
            {
                Logger.Warn("Failed to load compact anti-recoil form settings: {Message}", ex.Message);
                _formSettings = new CompactFormSettings(); // Reset to defaults
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
                _formSettings.Enabled = _enabledCheckBox?.Checked ?? false;
                _formSettings.Strength = (float)(_strengthNumeric?.Value ?? 0.6m);
                _formSettings.ActivationDelayMs = (int)(_delayNumeric?.Value ?? 100);

                string settingsFile = Path.Combine(AppContext.BaseDirectory, "compact_antirecoil_settings.json");

                // Path validation to prevent directory traversal attacks
                var fullPath = Path.GetFullPath(settingsFile);
                var allowedDir = Path.GetFullPath(Directory.GetCurrentDirectory());
                if (!fullPath.StartsWith(allowedDir, StringComparison.OrdinalIgnoreCase))
                    throw new UnauthorizedAccessException("Path traversal detected");

                string json = System.Text.Json.JsonSerializer.Serialize(_formSettings, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(settingsFile, json);

                Logger.Info("Compact anti-recoil form settings saved successfully");
            }
            catch (Exception ex)
            {
                Logger.Warn("Failed to save compact anti-recoil form settings: {Message}", ex.Message);
            }
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            // Unsubscribe from events
            _antiRecoil.RecordingStarted -= OnRecordingStarted;
            _antiRecoil.RecordingStopped -= OnRecordingStopped;
            _antiRecoil.PatternListChanged -= OnPatternListChanged;

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

        /// <summary>
        /// Settings for compact form persistence
        /// </summary>
        public class CompactFormSettings
        {
            public int X { get; set; } = -1;
            public int Y { get; set; } = -1;
            public int Width { get; set; } = -1;
            public int Height { get; set; } = -1;
            public bool Enabled { get; set; } = false;
            public float Strength { get; set; } = 0.6f;
            public int ActivationDelayMs { get; set; } = 100;
        }
    }

    /// <summary>
    /// Settings for compact form persistence
    /// </summary>
    public class CompactFormSettings
    {
        public int X { get; set; } = -1;
        public int Y { get; set; } = -1;
        public int Width { get; set; } = -1;
        public int Height { get; set; } = -1;
        public bool Enabled { get; set; } = false;
        public float Strength { get; set; } = 0.6f;
        public int ActivationDelayMs { get; set; } = 100;
    }
}
