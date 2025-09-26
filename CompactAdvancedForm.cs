using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using WootMouseRemap.Features;
// Logger is in WootMouseRemap namespace

namespace WootMouseRemap.UI
{
    /// <summary>
    /// Compact advanced features form with essential tools and diagnostics
    /// </summary>
    public partial class CompactAdvancedForm : Form
    {
        private readonly AntiRecoil _antiRecoil;
        private TabControl? _tabControl;
        private ToolTip? _toolTip;

        // Quick Tools Tab
        private GroupBox? _quickToolsGroup;
        private Button? _optimizeButton;
        private Button? _resetButton;
        private Button? _exportSettingsButton;
        private Button? _importSettingsButton;
        private CheckBox? _autoOptimizeCheckBox;

        // Pattern Analysis Tab
        private GroupBox? _patternGroup;
        private ComboBox? _patternCombo;
        private Button? _analyzeButton;
        private Label? _stabilityLabel;
        private Label? _predictabilityLabel;
        private Label? _recommendationsLabel;
        private ProgressBar? _qualityBar;

        // Diagnostics Tab
        private GroupBox? _diagnosticsGroup;
        private Button? _runDiagnosticsButton;
        private Button? _exportDiagnosticsButton;
        private TextBox? _diagnosticsOutput;
        private Label? _performanceLabel;

        public CompactAdvancedForm(AntiRecoil antiRecoil)
        {
            _antiRecoil = antiRecoil ?? throw new ArgumentNullException(nameof(antiRecoil));
            InitializeForm();
            CreateControls();
            LoadData();
        }

        private void InitializeForm()
        {
            Text = "Advanced Tools - WootMouseRemap";
            Size = new Size(520, 380);
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            BackColor = Color.FromArgb(45, 45, 48);
            ForeColor = Color.White;

            _toolTip = new ToolTip
            {
                AutoPopDelay = 5000,
                InitialDelay = 1000,
                ReshowDelay = 500
            };
        }

        private void CreateControls()
        {
            _tabControl = new TabControl
            {
                Location = new Point(10, 10),
                Size = new Size(490, 320),
                Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right
            };

            CreateQuickToolsTab();
            CreatePatternAnalysisTab();
            CreateDiagnosticsTab();

            Controls.Add(_tabControl);

            // Close button
            var closeButton = new Button
            {
                Text = "Close",
                Location = new Point(420, 340),
                Size = new Size(75, 25),
                Anchor = AnchorStyles.Bottom | AnchorStyles.Right,
                DialogResult = DialogResult.OK
            };
            closeButton.Click += (s, e) => Close();
            Controls.Add(closeButton);
        }

        private void CreateQuickToolsTab()
        {
            var tabPage = new TabPage("Quick Tools");

            _quickToolsGroup = new GroupBox
            {
                Text = "Quick Actions",
                Location = new Point(10, 10),
                Size = new Size(460, 100),
                ForeColor = Color.White
            };

            _optimizeButton = new Button
            {
                Text = "Auto Optimize",
                Location = new Point(15, 25),
                Size = new Size(100, 30),
                BackColor = Color.FromArgb(0, 120, 215),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat
            };
            _optimizeButton.Click += OnOptimizeClick;
            _toolTip!.SetToolTip(_optimizeButton, "Automatically optimize settings based on current patterns");

            _resetButton = new Button
            {
                Text = "Reset All",
                Location = new Point(125, 25),
                Size = new Size(100, 30),
                BackColor = Color.FromArgb(196, 43, 28),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat
            };
            _resetButton.Click += OnResetClick;
            _toolTip.SetToolTip(_resetButton, "Reset all anti-recoil settings to defaults");

            _exportSettingsButton = new Button
            {
                Text = "Export",
                Location = new Point(235, 25),
                Size = new Size(75, 30),
                BackColor = Color.FromArgb(16, 124, 16),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat
            };
            _exportSettingsButton.Click += OnExportClick;
            _toolTip.SetToolTip(_exportSettingsButton, "Export current settings to file");

            _importSettingsButton = new Button
            {
                Text = "Import",
                Location = new Point(320, 25),
                Size = new Size(75, 30),
                BackColor = Color.FromArgb(16, 124, 16),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat
            };
            _importSettingsButton.Click += OnImportClick;
            _toolTip.SetToolTip(_importSettingsButton, "Import settings from file");

            _autoOptimizeCheckBox = new CheckBox
            {
                Text = "Auto-optimize on pattern change",
                Location = new Point(15, 65),
                Size = new Size(200, 20),
                ForeColor = Color.White
            };
            _autoOptimizeCheckBox.CheckedChanged += OnAutoOptimizeChanged;

            _quickToolsGroup.Controls.AddRange(new Control[]
            {
                _optimizeButton, _resetButton, _exportSettingsButton,
                _importSettingsButton, _autoOptimizeCheckBox
            });

            tabPage.Controls.Add(_quickToolsGroup);

            // Performance indicator
            var perfGroup = new GroupBox
            {
                Text = "Performance",
                Location = new Point(10, 120),
                Size = new Size(460, 60),
                ForeColor = Color.White
            };

            _performanceLabel = new Label
            {
                Text = "Performance: Calculating...",
                Location = new Point(15, 25),
                Size = new Size(430, 20),
                ForeColor = Color.LightGreen
            };

            perfGroup.Controls.Add(_performanceLabel);
            tabPage.Controls.Add(perfGroup);

            _tabControl!.TabPages.Add(tabPage);
        }

        private void CreatePatternAnalysisTab()
        {
            var tabPage = new TabPage("Pattern Analysis");

            _patternGroup = new GroupBox
            {
                Text = "Pattern Analysis",
                Location = new Point(10, 10),
                Size = new Size(460, 260),
                ForeColor = Color.White
            };

            var selectLabel = new Label
            {
                Text = "Pattern:",
                Location = new Point(15, 25),
                Size = new Size(60, 20),
                ForeColor = Color.White
            };

            _patternCombo = new ComboBox
            {
                Location = new Point(80, 23),
                Size = new Size(200, 25),
                DropDownStyle = ComboBoxStyle.DropDownList
            };

            _analyzeButton = new Button
            {
                Text = "Analyze",
                Location = new Point(290, 22),
                Size = new Size(80, 27),
                BackColor = Color.FromArgb(0, 120, 215),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat
            };
            _analyzeButton.Click += OnAnalyzeClick;

            // Analysis results
            _stabilityLabel = new Label
            {
                Text = "Stability: Not analyzed",
                Location = new Point(15, 60),
                Size = new Size(430, 20),
                ForeColor = Color.White
            };

            _predictabilityLabel = new Label
            {
                Text = "Predictability: Not analyzed",
                Location = new Point(15, 85),
                Size = new Size(430, 20),
                ForeColor = Color.White
            };

            var qualityLabel = new Label
            {
                Text = "Overall Quality:",
                Location = new Point(15, 115),
                Size = new Size(100, 20),
                ForeColor = Color.White
            };

            _qualityBar = new ProgressBar
            {
                Location = new Point(120, 115),
                Size = new Size(200, 20),
                Style = ProgressBarStyle.Continuous
            };

            var recommendationsHeading = new Label
            {
                Text = "Recommendations:",
                Location = new Point(15, 145),
                Size = new Size(120, 20),
                ForeColor = Color.White,
                Font = new Font(Font, FontStyle.Bold)
            };

            _recommendationsLabel = new Label
            {
                Text = "Select a pattern and click Analyze for recommendations",
                Location = new Point(15, 170),
                Size = new Size(430, 80),
                ForeColor = Color.LightGray,
                AutoSize = false
            };

            _patternGroup.Controls.AddRange(new Control[]
            {
                selectLabel, _patternCombo, _analyzeButton,
                _stabilityLabel, _predictabilityLabel,
                qualityLabel, _qualityBar, recommendationsHeading, _recommendationsLabel
            });

            tabPage.Controls.Add(_patternGroup);
            _tabControl!.TabPages.Add(tabPage);
        }

        private void CreateDiagnosticsTab()
        {
            var tabPage = new TabPage("Diagnostics");

            _diagnosticsGroup = new GroupBox
            {
                Text = "System Diagnostics",
                Location = new Point(10, 10),
                Size = new Size(460, 260),
                ForeColor = Color.White
            };

            var buttonPanel = new Panel
            {
                Location = new Point(10, 25),
                Size = new Size(440, 35),
                BackColor = Color.Transparent
            };

            _runDiagnosticsButton = new Button
            {
                Text = "Run Diagnostics",
                Location = new Point(0, 5),
                Size = new Size(120, 25),
                BackColor = Color.FromArgb(0, 120, 215),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat
            };
            _runDiagnosticsButton.Click += OnRunDiagnosticsClick;

            _exportDiagnosticsButton = new Button
            {
                Text = "Export Report",
                Location = new Point(130, 5),
                Size = new Size(100, 25),
                BackColor = Color.FromArgb(16, 124, 16),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Enabled = false
            };
            _exportDiagnosticsButton.Click += OnExportDiagnosticsClick;

            buttonPanel.Controls.AddRange(new Control[] { _runDiagnosticsButton, _exportDiagnosticsButton });

            _diagnosticsOutput = new TextBox
            {
                Location = new Point(10, 70),
                Size = new Size(440, 180),
                Multiline = true,
                ReadOnly = true,
                ScrollBars = ScrollBars.Vertical,
                BackColor = Color.FromArgb(30, 30, 30),
                ForeColor = Color.White,
                Font = new Font("Consolas", 8),
                Text = "Click 'Run Diagnostics' to perform system analysis..."
            };

            _diagnosticsGroup.Controls.AddRange(new Control[] { buttonPanel, _diagnosticsOutput });

            tabPage.Controls.Add(_diagnosticsGroup);
            _tabControl!.TabPages.Add(tabPage);
        }

        private void LoadData()
        {
            // Load available patterns
            var patterns = _antiRecoil.Patterns.Select(p => p.Name).ToArray();
            _patternCombo!.Items.Clear();
            _patternCombo!.Items.AddRange(patterns);
            if (_patternCombo!.Items.Count > 0)
                _patternCombo!.SelectedIndex = 0;

            UpdatePerformanceDisplay();
        }

        private void UpdatePerformanceDisplay()
        {
            try
            {
                var metrics = AdvancedFeatures.PerformanceMonitor.GetMetrics();
                if (metrics.SampleCount > 0)
                {
                    _performanceLabel!.Text = $"Avg: {metrics.AverageProcessingTime.TotalMilliseconds:F2}ms | " +
                                           $"Max: {metrics.MaxProcessingTime.TotalMilliseconds:F2}ms | " +
                                           $"Samples: {metrics.SampleCount}";

                    // Color code based on performance
                    if (metrics.AverageProcessingTime.TotalMilliseconds < 1.0)
                        _performanceLabel.ForeColor = Color.LimeGreen;
                    else if (metrics.AverageProcessingTime.TotalMilliseconds < 2.0)
                        _performanceLabel.ForeColor = Color.Yellow;
                    else
                        _performanceLabel.ForeColor = Color.OrangeRed;
                }
                else
                {
                    _performanceLabel!.Text = "Performance: No data available";
                    _performanceLabel!.ForeColor = Color.Gray;
                }
            }
            catch (Exception ex)
            {
                _performanceLabel!.Text = $"Performance: Error - {ex.Message}";
                _performanceLabel!.ForeColor = Color.Red;
            }
        }

        private void OnOptimizeClick(object? sender, EventArgs e)
        {
            try
            {
                // Simple optimization based on current usage
                var currentStrength = _antiRecoil.Strength;
                var currentThreshold = _antiRecoil.VerticalThreshold;

                // Apply conservative optimizations
                if (currentStrength > 0.8f)
                {
                    _antiRecoil.Strength = 0.75f;
                    MessageBox.Show("Reduced strength to 75% for better stability.", "Optimization", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                else if (currentThreshold < 1.0f)
                {
                    _antiRecoil.VerticalThreshold = 1.5f;
                    MessageBox.Show("Increased threshold to 1.5 for better accuracy.", "Optimization", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                else
                {
                    MessageBox.Show("Settings are already optimized.", "Optimization", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error during optimization: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void OnResetClick(object? sender, EventArgs e)
        {
            if (MessageBox.Show("Reset all anti-recoil settings to defaults?", "Confirm Reset",
                MessageBoxButtons.YesNo, MessageBoxIcon.Warning) == DialogResult.Yes)
            {
                try
                {
                    _antiRecoil.Strength = 0.5f;
                    _antiRecoil.ActivationDelayMs = 50;
                    _antiRecoil.VerticalThreshold = 2.0f;
                    _antiRecoil.HorizontalCompensation = 0.0f;

                    MessageBox.Show("Settings reset to defaults.", "Reset Complete", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error resetting settings: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        private void OnExportClick(object? sender, EventArgs e)
        {
            using var dialog = new SaveFileDialog
            {
                Filter = "JSON files (*.json)|*.json|All files (*.*)|*.*",
                DefaultExt = "json",
                FileName = $"antiRecoil_settings_{DateTime.Now:yyyyMMdd_HHmmss}.json"
            };

            if (dialog.ShowDialog() == DialogResult.OK)
            {
                try
                {
                    _antiRecoil.ExportSettings(dialog.FileName);
                    MessageBox.Show("Settings exported successfully.", "Export Complete", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error exporting settings: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        private void OnImportClick(object? sender, EventArgs e)
        {
            using var dialog = new OpenFileDialog
            {
                Filter = "JSON files (*.json)|*.json|All files (*.*)|*.*",
                Multiselect = false
            };

            if (dialog.ShowDialog() == DialogResult.OK)
            {
                try
                {
                    _antiRecoil.ImportSettings(dialog.FileName);
                    MessageBox.Show("Settings imported successfully.", "Import Complete", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error importing settings: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        private void OnAutoOptimizeChanged(object? sender, EventArgs e)
        {
            // This would hook into pattern change events for auto-optimization
            MessageBox.Show($"Auto-optimize {(_autoOptimizeCheckBox!.Checked ? "enabled" : "disabled")}.",
                          "Auto-Optimize", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void OnAnalyzeClick(object? sender, EventArgs e)
        {
            if (_patternCombo!.SelectedItem is string patternName)
            {
                try
                {
                    var pattern = _antiRecoil.GetPattern(patternName);
                    if (pattern != null)
                    {
                        var analysis = AdvancedFeatures.PatternAnalyzer.AnalyzePattern(pattern);

                        _stabilityLabel!.Text = $"Stability: {analysis.StabilityScore:F1}%";
                        _stabilityLabel!.ForeColor = analysis.StabilityScore > 70 ? Color.LimeGreen :
                                                   analysis.StabilityScore > 40 ? Color.Yellow : Color.OrangeRed;

                        _predictabilityLabel!.Text = $"Predictability: {analysis.PredictabilityScore:F1}%";
                        _predictabilityLabel!.ForeColor = analysis.PredictabilityScore > 70 ? Color.LimeGreen :
                                                        analysis.PredictabilityScore > 40 ? Color.Yellow : Color.OrangeRed;

                        var overallQuality = (analysis.StabilityScore + analysis.PredictabilityScore) / 2;
                        _qualityBar!.Value = Math.Min(100, (int)overallQuality);

                        _recommendationsLabel!.Text = string.Join("\n• ", analysis.Recommendations);
                        if (analysis.Recommendations.Count == 0)
                            _recommendationsLabel.Text = "No specific recommendations. Pattern looks good!";
                    }
                    else
                    {
                        MessageBox.Show("Pattern not found.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error analyzing pattern: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
            else
            {
                MessageBox.Show("Please select a pattern to analyze.", "No Pattern Selected", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private void OnRunDiagnosticsClick(object? sender, EventArgs e)
        {
            _diagnosticsOutput!.Text = "Running diagnostics...\n";

            try
            {
                var diagnostics = new System.Text.StringBuilder();
                diagnostics.AppendLine("=== Anti-Recoil System Diagnostics ===");
                diagnostics.AppendLine($"Timestamp: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
                diagnostics.AppendLine();

                // Basic settings
                diagnostics.AppendLine("Current Settings:");
                diagnostics.AppendLine($"  Enabled: {_antiRecoil.Enabled}");
                diagnostics.AppendLine($"  Strength: {_antiRecoil.Strength:P0}");
                diagnostics.AppendLine($"  Threshold: {_antiRecoil.VerticalThreshold}");
                diagnostics.AppendLine($"  Delay: {_antiRecoil.ActivationDelayMs}ms");
                diagnostics.AppendLine();

                // Performance metrics
                var metrics = AdvancedFeatures.PerformanceMonitor.GetMetrics();
                diagnostics.AppendLine("Performance:");
                diagnostics.AppendLine($"  Avg Processing: {metrics.AverageProcessingTime.TotalMilliseconds:F2}ms");
                diagnostics.AppendLine($"  Max Processing: {metrics.MaxProcessingTime.TotalMilliseconds:F2}ms");
                diagnostics.AppendLine($"  Sample Count: {metrics.SampleCount}");
                diagnostics.AppendLine();

                // Pattern count
                var patterns = _antiRecoil.Patterns;
                diagnostics.AppendLine($"Patterns: {patterns.Count} available");
                diagnostics.AppendLine();

                diagnostics.AppendLine("System Status: OK");

                _diagnosticsOutput!.Text = diagnostics.ToString();
                _exportDiagnosticsButton!.Enabled = true;
            }
            catch (Exception ex)
            {
                _diagnosticsOutput!.Text = $"Error running diagnostics: {ex.Message}";
            }
        }

        private void OnExportDiagnosticsClick(object? sender, EventArgs e)
        {
            using var dialog = new SaveFileDialog
            {
                Filter = "Text files (*.txt)|*.txt|All files (*.*)|*.*",
                DefaultExt = "txt",
                FileName = $"antiRecoil_diagnostics_{DateTime.UtcNow:yyyyMMdd_HHmmss}.txt"
            };

            if (dialog.ShowDialog() == DialogResult.OK)
            {
                try
                {
                    // Path validation to prevent directory traversal attacks
                    var fullPath = Path.GetFullPath(dialog.FileName);
                    var allowedDir = Path.GetFullPath(Directory.GetCurrentDirectory());
                    if (!fullPath.StartsWith(allowedDir, StringComparison.OrdinalIgnoreCase))
                        throw new UnauthorizedAccessException("Path traversal detected");

                    System.IO.File.WriteAllText(dialog.FileName, _diagnosticsOutput!.Text);
                    MessageBox.Show("Diagnostics exported successfully.", "Export Complete", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error exporting diagnostics: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _toolTip?.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}