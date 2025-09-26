using System;
using System.Drawing;
using System.Windows.Forms;
using WootMouseRemap.Core;
using WootMouseRemap.Features;
using WootMouseRemap;
using WootMouseRemap.Diagnostics;

namespace WootMouseRemap.UI
{
    /// <summary>
    /// Mode diagnostic form to help troubleshoot mode issues
    /// </summary>
    public partial class ModeDiagnosticForm : Form
    {
        private readonly ModeService _modeService;
        private readonly Xbox360ControllerWrapper _pad;
        private readonly XInputPassthrough _xpass;
        private readonly ControllerDetector _detector;
        private readonly AntiRecoil _antiRecoil;

        private TextBox? _diagnosticOutput;
        private Button? _runDiagnosticsButton;
        private Button? _quickCheckButton;
        private Button? _refreshButton;
        private Button? _exportButton;
        private System.Windows.Forms.Timer? _autoRefreshTimer;

        public ModeDiagnosticForm(
            ModeService modeService,
            Xbox360ControllerWrapper pad,
            XInputPassthrough xpass,
            ControllerDetector detector,
            AntiRecoil antiRecoil)
        {
            _modeService = modeService ?? throw new ArgumentNullException(nameof(modeService));
            _pad = pad ?? throw new ArgumentNullException(nameof(pad));
            _xpass = xpass ?? throw new ArgumentNullException(nameof(xpass));
            _detector = detector ?? throw new ArgumentNullException(nameof(detector));
            _antiRecoil = antiRecoil ?? throw new ArgumentNullException(nameof(antiRecoil));

            InitializeForm();
            CreateControls();
            RunQuickCheck();
        }

        private void InitializeForm()
        {
            Text = "Mode Diagnostics - WootMouseRemap";
            Size = new Size(800, 600);
            StartPosition = FormStartPosition.CenterParent;
            BackColor = Color.FromArgb(45, 45, 48);
            ForeColor = Color.White;

            _autoRefreshTimer = new System.Windows.Forms.Timer
            {
                Interval = 5000, // 5 seconds
                Enabled = false
            };
            _autoRefreshTimer.Tick += (s, e) => RunQuickCheck();
        }

        private void CreateControls()
        {
            // Button panel
            var buttonPanel = new Panel
            {
                Location = new Point(10, 10),
                Size = new Size(770, 40),
                BackColor = Color.Transparent
            };

            _quickCheckButton = new Button
            {
                Text = "Quick Check",
                Location = new Point(0, 5),
                Size = new Size(100, 30),
                BackColor = Color.FromArgb(0, 120, 215),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat
            };
            _quickCheckButton.Click += (s, e) => RunQuickCheck();

            _runDiagnosticsButton = new Button
            {
                Text = "Full Diagnostics",
                Location = new Point(110, 5),
                Size = new Size(120, 30),
                BackColor = Color.FromArgb(16, 124, 16),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat
            };
            _runDiagnosticsButton.Click += (s, e) => RunFullDiagnostics();

            _refreshButton = new Button
            {
                Text = "Auto-Refresh",
                Location = new Point(240, 5),
                Size = new Size(100, 30),
                BackColor = Color.FromArgb(108, 117, 125),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat
            };
            _refreshButton.Click += OnAutoRefreshToggle;

            _exportButton = new Button
            {
                Text = "Export Report",
                Location = new Point(350, 5),
                Size = new Size(100, 30),
                BackColor = Color.FromArgb(40, 167, 69),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat
            };
            _exportButton.Click += OnExportReport;

            var closeButton = new Button
            {
                Text = "Close",
                Location = new Point(680, 5),
                Size = new Size(80, 30),
                BackColor = Color.FromArgb(108, 117, 125),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                DialogResult = DialogResult.OK
            };

            buttonPanel.Controls.AddRange(new Control[]
            {
                _quickCheckButton, _runDiagnosticsButton, _refreshButton, _exportButton, closeButton
            });

            // Output text box
            _diagnosticOutput = new TextBox
            {
                Location = new Point(10, 60),
                Size = new Size(770, 520),
                Multiline = true,
                ReadOnly = true,
                ScrollBars = ScrollBars.Vertical,
                BackColor = Color.FromArgb(30, 30, 30),
                ForeColor = Color.White,
                Font = new Font("Consolas", 9),
                Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right
            };

            Controls.AddRange(new Control[] { buttonPanel, _diagnosticOutput });
        }

        private void RunQuickCheck()
        {
            try
            {
                _diagnosticOutput!.Text = "Running quick mode check...\r\n\r\n";
                Application.DoEvents();

                var quickCheck = ModeDiagnosticTool.QuickModeCheck(_modeService, _pad, _detector);
                _diagnosticOutput.Text = $"=== QUICK MODE CHECK ===\r\n";
                _diagnosticOutput.Text += $"Current Mode: {GetModeDisplayName(_modeService.CurrentMode)}\r\n";
                _diagnosticOutput.Text += $"Timestamp: {DateTime.Now:HH:mm:ss}\r\n\r\n";
                _diagnosticOutput.Text += quickCheck;

                // Add status summary
                _diagnosticOutput.Text += "\r\n=== STATUS SUMMARY ===\r\n";
                _diagnosticOutput.Text += $"Virtual Controller: {(_pad.IsConnected ? "✅ Connected" : "❌ Disconnected")}\r\n";
                _diagnosticOutput.Text += $"Physical Controller: {(_detector.Connected ? "✅ Detected" : "❌ Not Detected")}\r\n";
                _diagnosticOutput.Text += $"XInput Passthrough: {(_xpass.IsRunning ? "✅ Running" : "⏸️ Stopped")}\r\n";
                _diagnosticOutput.Text += $"Anti-Recoil: {(_antiRecoil.Enabled ? "✅ Enabled" : "⏸️ Disabled")}\r\n";

                // Scroll to top
                _diagnosticOutput.SelectionStart = 0;
                _diagnosticOutput.ScrollToCaret();
            }
            catch (Exception ex)
            {
                _diagnosticOutput!.Text = $"Error during quick check: {ex.Message}";
            }
        }

        private void RunFullDiagnostics()
        {
            try
            {
                _diagnosticOutput!.Text = "Running full diagnostics...\r\n";
                Application.DoEvents();

                var fullReport = ModeDiagnosticTool.GenerateModeDiagnosticReport(
                    _modeService, _pad, _xpass, _detector, _antiRecoil);

                _diagnosticOutput.Text = fullReport;
                _diagnosticOutput.SelectionStart = 0;
                _diagnosticOutput.ScrollToCaret();
            }
            catch (Exception ex)
            {
                _diagnosticOutput!.Text = $"Error during full diagnostics: {ex.Message}";
            }
        }

        private void OnAutoRefreshToggle(object? sender, EventArgs e)
        {
            _autoRefreshTimer!.Enabled = !_autoRefreshTimer!.Enabled;
            _refreshButton!.Text = _autoRefreshTimer.Enabled ? "Stop Auto-Refresh" : "Auto-Refresh";
            _refreshButton.BackColor = _autoRefreshTimer.Enabled ?
                Color.FromArgb(220, 53, 69) : Color.FromArgb(108, 117, 125);
        }

        private void OnExportReport(object? sender, EventArgs e)
        {
            using var dialog = new SaveFileDialog
            {
                Filter = "Text files (*.txt)|*.txt|All files (*.*)|*.*",
                DefaultExt = "txt",
                FileName = $"mode_diagnostics_{DateTime.Now:yyyyMMdd_HHmmss}.txt"
            };

            if (dialog.ShowDialog() == DialogResult.OK)
            {
                try
                {
                    System.IO.File.WriteAllText(dialog.FileName, _diagnosticOutput!.Text);
                    MessageBox.Show("Diagnostics exported successfully.", "Export Complete",
                        MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error exporting diagnostics: {ex.Message}", "Error",
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        private string GetModeDisplayName(InputMode mode)
        {
            return mode switch
            {
                InputMode.Native => "Native",
                InputMode.MnKConvert => "MnK Convert",
                InputMode.ControllerPass => "Controller Pass",
                _ => "Unknown"
            };
        }
    }
}