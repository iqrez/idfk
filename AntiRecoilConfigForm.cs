using System;
using System.Drawing;
using System.Windows.Forms;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Linq;
using System.Text.RegularExpressions;
using WootMouseRemap.Features;
// Logger is in WootMouseRemap namespace
using WootMouseRemap.Core;
using WinFormsTimer = System.Windows.Forms.Timer;

namespace WootMouseRemap.UI
{
    /// <summary>
    /// Advanced anti-recoil configuration form with tabbed interface
    /// </summary>
    public partial class AntiRecoilConfigForm : Form
    {
        private readonly AntiRecoil _antiRecoil;
        private readonly AntiRecoilViewModel _viewModel;
        private readonly ValidationSystem _validationSystem;
        private bool _updatingControls;
        private UiStateData? _uiState;

        // Main UI Components
        private Panel? _mainScrollPanel;
        private FlowLayoutPanel? _mainLayout;
        private ToolTip? _toolTip;
        private StatusStrip? _statusStrip;
        private ToolStripStatusLabel? _statusLabel;
        private ToolStripStatusLabel? _validationLabel;
        private ToolStripStatusLabel? _helpLabel;
        private WinFormsTimer? _telemetryTimer;

        // General Tab Controls
        private CheckBox? _enabledCheckBox;
        private TrackBar? _strengthTrackBar;
        private NumericUpDown? _strengthNumeric;
        private Label? _strengthValueLabel;
        private TrackBar? _activationDelayTrackBar;
        private NumericUpDown? _activationDelayNumeric;
        private TrackBar? _thresholdTrackBar;
        private NumericUpDown? _thresholdNumeric;
        private TrackBar? _horizontalCompTrackBar;
        private NumericUpDown? _horizontalCompNumeric;
        private CheckBox? _adaptiveCompCheckBox;

        // Advanced controls for General tab
        private TrackBar? _maxTickCompTrackBar;
        private NumericUpDown? _maxTickCompNumeric;
        private TrackBar? _maxTotalCompTrackBar;
        private NumericUpDown? _maxTotalCompNumeric;
        private NumericUpDown? _cooldownNumeric;
        private NumericUpDown? _decayNumeric;

        // Patterns Tab Controls
        private ListView? _patternsListView;
        private Button? _recordPatternButton;
        private Button? _stopRecordingButton;
        private Button? _deletePatternButton;
        private Button? _renamePatternButton;
        private Button? _exportPatternButton;
        private Button? _importPatternButton;
        private TextBox? _recordingNameTextBox;
        private TextBox? _patternNotesTextBox;
        private TextBox? _patternTagsTextBox;
        private PatternGraphControl? _patternGraphControl;

        // Simulation Tab Controls
        private ComboBox? _simulationPatternCombo;
        private Button? _simulateButton;
        private Button? _exportSimButton;
        private Label? _simulationResultsLabel;
        private PatternGraphControl? _simulationGraphControl;
        private ComboBox? _displayModeCombo;
        private Button? _playbackButton;
        private TrackBar? _playbackTrackBar;
        private WinFormsTimer? _playbackTimer;
        private AntiRecoilSimulationResult? _currentSimulation;

        // Telemetry Tab Controls
        private TelemetryControl? _telemetryControl;
        private Label? _telemetryStatusLabel;
        private Label? _lastDyLabel;
        private Label? _lastCompLabel;
        private Label? _accumCompLabel;
        private ComboBox? _telemetryModeCombo;
        private CheckBox? _autoScaleCheckBox;
        private Button? _clearTelemetryButton;
        private Label? _telemetryStatsLabel;
        private Queue<float> _recentVerticalMovements = new();
        private float _lastVerticalMovement = 0f;

        public AntiRecoilConfigForm(AntiRecoil antiRecoil)
        {
            _antiRecoil = antiRecoil ?? throw new ArgumentNullException(nameof(antiRecoil));
            _viewModel = new AntiRecoilViewModel();
            _validationSystem = new ValidationSystem(this);

            LoadUIState();
            InitializeComponent();
            InitializeTooltips();
            LoadSettings();
            SetupEventHandlers();
            SetupValidation();
            SetupKeyboardShortcuts();
            SetupTimers();
            ApplyUIState();
        }

        private void InitializeComponent()
        {
            Text = "Anti-Recoil Configuration";
            Size = new Size(950, 800);
            FormBorderStyle = FormBorderStyle.Sizable; // Allow resizing for better scrolling experience
            MaximizeBox = true;
            MinimizeBox = false;
            StartPosition = FormStartPosition.CenterParent;
            BackColor = Color.FromArgb(45, 45, 48);
            ForeColor = Color.White;
            MinimumSize = new Size(800, 600);

            CreateMainScrollLayout();
            CreateAllSections();
            CreateFormButtons();
        }

        private void CreateMainScrollLayout()
        {
            _mainScrollPanel = new Panel
            {
                Location = new Point(10, 10),
                Size = new Size(920, 730),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Bottom,
                BackColor = Color.FromArgb(45, 45, 48),
                AutoScroll = true
            };

            _mainLayout = new FlowLayoutPanel
            {
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                Dock = DockStyle.Top,
                Padding = new Padding(10),
                BackColor = Color.FromArgb(45, 45, 48)
            };

            _mainScrollPanel.Controls.Add(_mainLayout);
            Controls.Add(_mainScrollPanel);
        }

        private void CreateAllSections()
        {
            CreateGeneralSection();
            CreatePatternsSection();
            CreateSimulationSection();
            CreateTelemetrySection();
        }

        private void CreateGeneralSection()
        {
            var generalSection = CreateSectionGroupBox("General Settings", 900, 350);

            var layout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 3,
                RowCount = 10,
                Padding = new Padding(10)
            };

            // Configure columns: Label (30%), Control (50%), Value (20%)
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 30));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 20));

            // Configure rows
            for (int i = 0; i < 10; i++)
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
            CreateTrackBarRow(layout, row++, "Strength:", out _strengthTrackBar, out _strengthNumeric, out _strengthValueLabel, 0, 100, "%");

            // Activation Delay
            CreateTrackBarRow(layout, row++, "Activation Delay:", out _activationDelayTrackBar, out _activationDelayNumeric, out var delayLabel, 0, 1000, "ms");

            // Vertical Threshold
            CreateTrackBarRow(layout, row++, "Vertical Threshold:", out _thresholdTrackBar, out _thresholdNumeric, out var threshLabel, 0, 100, "px");

            // Horizontal Compensation
            CreateTrackBarRow(layout, row++, "Horizontal Compensation:", out _horizontalCompTrackBar, out _horizontalCompNumeric, out var hcompLabel, 0, 100, "%");

            // Adaptive Compensation checkbox
            _adaptiveCompCheckBox = new CheckBox
            {
                Text = "Adaptive Compensation",
                ForeColor = Color.White,
                Anchor = AnchorStyles.Left | AnchorStyles.Right
            };
            layout.Controls.Add(_adaptiveCompCheckBox, 0, row);
            layout.SetColumnSpan(_adaptiveCompCheckBox, 3);
            row++;

            // Advanced Settings Header
            var advancedHeader = new Label
            {
                Text = "Advanced Settings",
                ForeColor = Color.LightBlue,
                Font = new Font(Font, FontStyle.Bold),
                Anchor = AnchorStyles.Left | AnchorStyles.Right
            };
            layout.Controls.Add(advancedHeader, 0, row);
            layout.SetColumnSpan(advancedHeader, 3);
            row++;

            // Max Tick Compensation
            CreateTrackBarRow(layout, row++, "Max Tick Compensation:", out _maxTickCompTrackBar, out _maxTickCompNumeric, out var maxTickLabel, 0, 50, "px");

            // Max Total Compensation
            CreateTrackBarRow(layout, row++, "Max Total Compensation:", out _maxTotalCompTrackBar, out _maxTotalCompNumeric, out var maxTotalLabel, 0, 500, "px");

            // Cooldown and Decay (simple numeric inputs)
            layout.Controls.Add(new Label { Text = "Cooldown:", ForeColor = Color.White, Anchor = AnchorStyles.Left | AnchorStyles.Right }, 0, row);
            _cooldownNumeric = new NumericUpDown
            {
                Minimum = 0,
                Maximum = 5000,
                BackColor = Color.FromArgb(60, 60, 60),
                ForeColor = Color.White,
                Anchor = AnchorStyles.Left | AnchorStyles.Right
            };
            layout.Controls.Add(_cooldownNumeric, 1, row);
            layout.Controls.Add(new Label { Text = "ms", ForeColor = Color.LightGray, Anchor = AnchorStyles.Left }, 2, row);
            row++;

            layout.Controls.Add(new Label { Text = "Decay Per Ms:", ForeColor = Color.White, Anchor = AnchorStyles.Left | AnchorStyles.Right }, 0, row);
            _decayNumeric = new NumericUpDown
            {
                Minimum = 0,
                Maximum = 10,
                DecimalPlaces = 3,
                Increment = 0.001M,
                BackColor = Color.FromArgb(60, 60, 60),
                ForeColor = Color.White,
                Anchor = AnchorStyles.Left | AnchorStyles.Right
            };
            layout.Controls.Add(_decayNumeric, 1, row);
            layout.Controls.Add(new Label { Text = "px/ms", ForeColor = Color.LightGray, Anchor = AnchorStyles.Left }, 2, row);

            generalSection.Controls.Add(layout);
            _mainLayout!.Controls.Add(generalSection);
        }

        private GroupBox CreateSectionGroupBox(string title, int width, int height)
        {
            return new GroupBox
            {
                Text = title,
                Size = new Size(width, height),
                BackColor = Color.FromArgb(50, 50, 52),
                ForeColor = Color.White,
                Font = new Font(Font.FontFamily, 10f, FontStyle.Bold),
                Padding = new Padding(10),
                Margin = new Padding(0, 0, 0, 20)
            };
        }

        private void CreateTrackBarRow(TableLayoutPanel layout, int row, string labelText,
            out TrackBar trackBar, out NumericUpDown numeric, out Label valueLabel,
            int min, int max, string unit)
        {
            layout.Controls.Add(new Label
            {
                Text = labelText,
                ForeColor = Color.White,
                Anchor = AnchorStyles.Left | AnchorStyles.Right
            }, 0, row);

            trackBar = new TrackBar
            {
                Minimum = min,
                Maximum = max,
                TickFrequency = Math.Max(1, (max - min) / 10),
                Anchor = AnchorStyles.Left | AnchorStyles.Right
            };
            layout.Controls.Add(trackBar, 1, row);

            // Create a panel to hold numeric and label
            var valuePanel = new Panel
            {
                Dock = DockStyle.Fill
            };

            numeric = new NumericUpDown
            {
                Minimum = min,
                Maximum = max,
                DecimalPlaces = labelText.Contains("Threshold") ? 1 : 0,
                BackColor = Color.FromArgb(60, 60, 60),
                ForeColor = Color.White,
                Size = new Size(60, 23),
                Location = new Point(0, 12)
            };

            valueLabel = new Label
            {
                Text = $"0{unit}",
                ForeColor = Color.LightGray,
                Location = new Point(65, 15),
                Size = new Size(50, 20)
            };

            valuePanel.Controls.Add(numeric);
            valuePanel.Controls.Add(valueLabel);
            layout.Controls.Add(valuePanel, 2, row);
        }

        private void CreatePatternsSection()
        {
            var patternsSection = CreateSectionGroupBox("Pattern Management", 900, 450);

            // Split container for list and details
            var splitContainer = new SplitContainer
            {
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(45, 45, 48),
                SplitterDistance = 400
            };

            // Left panel - Pattern list and controls
            var leftPanel = new Panel { Dock = DockStyle.Fill, BackColor = Color.FromArgb(45, 45, 48) };

            _patternsListView = new ListView
            {
                Location = new Point(10, 10),
                Size = new Size(360, 300),
                View = View.Details,
                FullRowSelect = true,
                GridLines = true,
                BackColor = Color.FromArgb(40, 40, 40),
                ForeColor = Color.White,
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Bottom
            };

            _patternsListView.Columns.Add("Name", 150);
            _patternsListView.Columns.Add("Samples", 70);
            _patternsListView.Columns.Add("Created", 100);
            _patternsListView.Columns.Add("Tags", 100);

            // Recording name input
            leftPanel.Controls.Add(new Label
            {
                Text = "Recording Name:",
                Location = new Point(10, 320),
                ForeColor = Color.White,
                Size = new Size(100, 20)
            });

            _recordingNameTextBox = new TextBox
            {
                Location = new Point(120, 318),
                Size = new Size(150, 23),
                BackColor = Color.FromArgb(60, 60, 60),
                ForeColor = Color.White
            };

            // Pattern control buttons
            _recordPatternButton = CreateButton("Record", new Point(10, 350), Color.FromArgb(0, 120, 200));
            _stopRecordingButton = CreateButton("Stop", new Point(80, 350), Color.FromArgb(160, 80, 40));
            _deletePatternButton = CreateButton("Delete", new Point(150, 350), Color.FromArgb(150, 50, 50));
            _renamePatternButton = CreateButton("Rename", new Point(220, 350), Color.FromArgb(100, 100, 100));
            _exportPatternButton = CreateButton("Export", new Point(290, 350), Color.FromArgb(70, 70, 110));
            _importPatternButton = CreateButton("Import", new Point(10, 380), Color.FromArgb(70, 110, 70));

            // Transform buttons
            var normalizeButton = CreateButton("Normalize", new Point(80, 380), Color.FromArgb(80, 140, 80));
            var smoothButton = CreateButton("Smooth", new Point(150, 380), Color.FromArgb(80, 140, 80));
            var trimButton = CreateButton("Trim", new Point(220, 380), Color.FromArgb(80, 140, 80));
            var downsampleButton = CreateButton("Downsample", new Point(290, 380), Color.FromArgb(80, 140, 80));
            var moreTransformButton = CreateButton("More...", new Point(10, 410), Color.FromArgb(120, 120, 80));

            normalizeButton.Click += NormalizeButton_Click;
            smoothButton.Click += SmoothButton_Click;
            trimButton.Click += TrimButton_Click;
            downsampleButton.Click += DownsampleButton_Click;
            moreTransformButton.Click += MoreTransformButton_Click;

            leftPanel.Controls.AddRange(new Control[]
            {
                _patternsListView, _recordingNameTextBox, _recordPatternButton, _stopRecordingButton,
                _deletePatternButton, _renamePatternButton, _exportPatternButton, _importPatternButton,
                normalizeButton, smoothButton, trimButton, downsampleButton, moreTransformButton
            });

            // Right panel - Pattern details with graph
            var rightPanel = new Panel { Dock = DockStyle.Fill, BackColor = Color.FromArgb(45, 45, 48) };

            // Pattern visualization
            _patternGraphControl = new PatternGraphControl
            {
                Location = new Point(10, 10),
                Size = new Size(400, 200),
                BackColor = Color.Black,
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
                DisplayMode = GraphDisplayMode.InputOnly
            };
            _patternGraphControl!.SampleClicked += PatternGraph_SampleClicked;

            rightPanel.Controls.Add(new Label
            {
                Text = "Notes:",
                Location = new Point(10, 225),
                ForeColor = Color.White,
                Size = new Size(50, 20)
            });

            _patternNotesTextBox = new TextBox
            {
                Location = new Point(10, 250),
                Size = new Size(400, 80),
                Multiline = true,
                ScrollBars = ScrollBars.Vertical,
                BackColor = Color.FromArgb(60, 60, 60),
                ForeColor = Color.White,
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };

            rightPanel.Controls.Add(new Label
            {
                Text = "Tags (comma-separated):",
                Location = new Point(10, 345),
                ForeColor = Color.White,
                Size = new Size(150, 20)
            });

            _patternTagsTextBox = new TextBox
            {
                Location = new Point(10, 370),
                Size = new Size(400, 23),
                BackColor = Color.FromArgb(60, 60, 60),
                ForeColor = Color.White,
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };

            rightPanel.Controls.AddRange(new Control[] { _patternGraphControl, _patternNotesTextBox, _patternTagsTextBox });

            splitContainer.Panel1.Controls.Add(leftPanel);
            splitContainer.Panel2.Controls.Add(rightPanel);
            patternsSection.Controls.Add(splitContainer);
            _mainLayout!.Controls.Add(patternsSection);
        }

        private void CreateSimulationSection()
        {
            var simulationSection = CreateSectionGroupBox("Pattern Simulation", 900, 400);

            // Top control panel
            var controlPanel = new Panel
            {
                Location = new Point(0, 0),
                Size = new Size(870, 80),
                BackColor = Color.FromArgb(35, 35, 35),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };

            // Row 1: Pattern selection and simulation
            controlPanel.Controls.Add(new Label
            {
                Text = "Pattern:",
                Location = new Point(10, 15),
                ForeColor = Color.White,
                Size = new Size(60, 20)
            });

            _simulationPatternCombo = new ComboBox
            {
                Location = new Point(80, 12),
                Size = new Size(200, 23),
                DropDownStyle = ComboBoxStyle.DropDownList,
                BackColor = Color.FromArgb(60, 60, 60),
                ForeColor = Color.White
            };
            controlPanel.Controls.Add(_simulationPatternCombo);

            _simulateButton = CreateButton("Simulate", new Point(300, 12), Color.FromArgb(0, 160, 60));
            _simulateButton.Size = new Size(80, 25);
            controlPanel.Controls.Add(_simulateButton);

            _exportSimButton = CreateButton("Export CSV", new Point(390, 12), Color.FromArgb(70, 70, 110));
            _exportSimButton.Size = new Size(80, 25);
            controlPanel.Controls.Add(_exportSimButton);

            // Row 2: Display mode and controls
            controlPanel.Controls.Add(new Label
            {
                Text = "Display:",
                Location = new Point(10, 45),
                ForeColor = Color.White,
                Size = new Size(60, 20)
            });

            _displayModeCombo = new ComboBox
            {
                Location = new Point(80, 42),
                Size = new Size(150, 23),
                DropDownStyle = ComboBoxStyle.DropDownList,
                BackColor = Color.FromArgb(60, 60, 60),
                ForeColor = Color.White
            };
            _displayModeCombo.Items.AddRange(new[] { "Input Only", "Output Only", "Input & Output", "Compensation Only", "All" });
            _displayModeCombo.SelectedIndex = 2; // Input & Output
            _displayModeCombo.SelectedIndexChanged += DisplayModeCombo_SelectedIndexChanged;
            controlPanel.Controls.Add(_displayModeCombo);

            var resetViewButton = CreateButton("Reset View", new Point(240, 42), Color.FromArgb(100, 100, 100));
            resetViewButton.Size = new Size(80, 25);
            resetViewButton.Click += (s, e) => _simulationGraphControl?.ResetView();
            controlPanel.Controls.Add(resetViewButton);

            var fitToWindowButton = CreateButton("Fit Window", new Point(330, 42), Color.FromArgb(100, 100, 100));
            fitToWindowButton.Size = new Size(80, 25);
            fitToWindowButton.Click += (s, e) => _simulationGraphControl?.FitToWindow();
            controlPanel.Controls.Add(fitToWindowButton);

            // Playback controls
            controlPanel.Controls.Add(new Label
            {
                Text = "Playback:",
                Location = new Point(430, 45),
                ForeColor = Color.White,
                Size = new Size(60, 20)
            });

            _playbackButton = CreateButton("Play", new Point(500, 42), Color.FromArgb(60, 130, 60));
            _playbackButton.Size = new Size(50, 25);
            controlPanel.Controls.Add(_playbackButton);

            _playbackTrackBar = new TrackBar
            {
                Location = new Point(560, 35),
                Size = new Size(280, 45),
                Minimum = 0,
                Maximum = 100,
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };
            _playbackTrackBar.ValueChanged += PlaybackTrackBar_ValueChanged;
            controlPanel.Controls.Add(_playbackTrackBar);

            simulationSection.Controls.Add(controlPanel);

            // Results display
            _simulationResultsLabel = new Label
            {
                Location = new Point(10, 90),
                Size = new Size(850, 25),
                ForeColor = Color.LightGreen,
                Font = new Font(Font.FontFamily, 9f),
                Text = "No simulation results",
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };
            simulationSection.Controls.Add(_simulationResultsLabel);

            // Advanced graph control
            _simulationGraphControl = new PatternGraphControl
            {
                Location = new Point(10, 125),
                Size = new Size(850, 400),
                BackColor = Color.Black,
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Bottom,
                DisplayMode = GraphDisplayMode.InputAndOutput,
                ShowGrid = true,
                ShowCrosshair = true
            };
            _simulationGraphControl!.SampleClicked += SimulationGraph_SampleClicked;
            _simulationGraphControl!.SampleHovered += SimulationGraph_SampleHovered;
            simulationSection.Controls.Add(_simulationGraphControl);

            _mainLayout!.Controls.Add(simulationSection);
        }

        private void CreateTelemetrySection()
        {
            var telemetrySection = CreateSectionGroupBox("Live Telemetry", 900, 400);

            // Top control panel
            var controlPanel = new Panel
            {
                Location = new Point(0, 0),
                Size = new Size(870, 60),
                BackColor = Color.FromArgb(35, 35, 35),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };

            // Status display
            _telemetryStatusLabel = new Label
            {
                Location = new Point(10, 15),
                Size = new Size(200, 30),
                ForeColor = Color.LightGreen,
                Font = new Font(Font.FontFamily, 10f, FontStyle.Bold),
                Text = "Status: Standby"
            };
            controlPanel.Controls.Add(_telemetryStatusLabel);

            // Display mode selection
            controlPanel.Controls.Add(new Label
            {
                Text = "Display:",
                Location = new Point(220, 15),
                ForeColor = Color.White,
                Size = new Size(50, 20)
            });

            _telemetryModeCombo = new ComboBox
            {
                Location = new Point(280, 12),
                Size = new Size(150, 23),
                DropDownStyle = ComboBoxStyle.DropDownList,
                BackColor = Color.FromArgb(60, 60, 60),
                ForeColor = Color.White
            };
            _telemetryModeCombo.Items.AddRange(new[] { "Vertical Movements", "Compensation", "Accumulated", "All" });
            _telemetryModeCombo.SelectedIndex = 3; // All
            _telemetryModeCombo.SelectedIndexChanged += TelemetryModeCombo_SelectedIndexChanged;
            controlPanel.Controls.Add(_telemetryModeCombo);

            // Auto-scale checkbox
            _autoScaleCheckBox = new CheckBox
            {
                Text = "Auto Scale",
                Location = new Point(450, 15),
                Size = new Size(80, 20),
                ForeColor = Color.White,
                Checked = true
            };
            _autoScaleCheckBox!.CheckedChanged += (s, e) => _telemetryControl!.AutoScale = _autoScaleCheckBox!.Checked;
            controlPanel.Controls.Add(_autoScaleCheckBox);

            // Clear button
            _clearTelemetryButton = CreateButton("Clear", new Point(550, 12), Color.FromArgb(100, 100, 100));
            _clearTelemetryButton!.Click += (s, e) => _telemetryControl!.Clear();
            controlPanel.Controls.Add(_clearTelemetryButton);

            telemetrySection.Controls.Add(controlPanel);

            // Real-time metrics panel
            var metricsPanel = new Panel
            {
                Location = new Point(10, 70),
                Size = new Size(850, 90),
                BackColor = Color.FromArgb(35, 35, 35),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };

            _lastDyLabel = new Label
            {
                Location = new Point(10, 10),
                Size = new Size(200, 20),
                ForeColor = Color.White,
                Text = "Last Vertical Movement: 0.0"
            };

            _lastCompLabel = new Label
            {
                Location = new Point(220, 10),
                Size = new Size(200, 20),
                ForeColor = Color.White,
                Text = "Last Applied Compensation: 0.0"
            };

            _accumCompLabel = new Label
            {
                Location = new Point(10, 35),
                Size = new Size(200, 20),
                ForeColor = Color.White,
                Text = "Accumulated Compensation: 0.0"
            };

            _telemetryStatsLabel = new Label
            {
                Location = new Point(10, 60),
                Size = new Size(800, 20),
                ForeColor = Color.LightBlue,
                Text = "No statistics available",
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };

            metricsPanel.Controls.AddRange(new Control[] { _lastDyLabel, _lastCompLabel, _accumCompLabel, _telemetryStatsLabel });
            telemetrySection.Controls.Add(metricsPanel);

            // Telemetry visualization control
            _telemetryControl = new TelemetryControl
            {
                Location = new Point(10, 170),
                Size = new Size(850, 350),
                BackColor = Color.Black,
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Bottom,
                DisplayMode = TelemetryDisplayMode.All,
                AutoScale = true
            };
            _telemetryControl.DataPointAdded += TelemetryControl_DataPointAdded;
            telemetrySection.Controls.Add(_telemetryControl);

            _mainLayout!.Controls.Add(telemetrySection);
        }

        private Button CreateButton(string text, Point location, Color backColor)
        {
            return new Button
            {
                Text = text,
                Location = location,
                Size = new Size(60, 25),
                BackColor = backColor,
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat
            };
        }

        private void CreateFormButtons()
        {
            // Create status strip first
            _statusStrip = new StatusStrip
            {
                BackColor = Color.FromArgb(35, 35, 35),
                ForeColor = Color.White
            };

            _statusLabel = new ToolStripStatusLabel
            {
                Text = "Ready",
                Spring = true,
                TextAlign = ContentAlignment.MiddleLeft
            };

            _validationLabel = new ToolStripStatusLabel
            {
                Text = "",
                ForeColor = Color.Orange
            };

            _helpLabel = new ToolStripStatusLabel
            {
                Text = "Press F1 for keyboard shortcuts",
                ForeColor = Color.LightGray
            };

            _statusStrip.Items.AddRange(new ToolStripItem[] { _statusLabel, _validationLabel, _helpLabel });
            Controls.Add(_statusStrip);

            // Adjust button positions to account for status strip
            var applyButton = new Button
            {
                Text = "Apply (Ctrl+S)",
                Location = new Point(620, 625),
                Size = new Size(95, 30),
                BackColor = Color.FromArgb(0, 160, 60),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat
            };
            applyButton.Click += ApplyButton_Click;

            var revertButton = new Button
            {
                Text = "Revert (Ctrl+R)",
                Location = new Point(725, 625),
                Size = new Size(95, 30),
                BackColor = Color.FromArgb(160, 80, 40),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat
            };
            revertButton.Click += RevertButton_Click;

            var closeButton = new Button
            {
                Text = "Close (Esc)",
                Location = new Point(830, 625),
                Size = new Size(85, 30),
                BackColor = Color.FromArgb(0, 122, 204),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                DialogResult = DialogResult.OK
            };

            Controls.AddRange(new Control[] { applyButton, revertButton, closeButton });
        }

        private void InitializeTooltips()
        {
            _toolTip = new ToolTip
            {
                AutoPopDelay = 12000,
                InitialDelay = 500,
                ReshowDelay = 200,
                ShowAlways = true,
                ToolTipIcon = ToolTipIcon.Info,
                ToolTipTitle = "Anti-Recoil Help"
            };

            // Enhanced tooltips with detailed explanations
            _toolTip!.SetToolTip(_enabledCheckBox!,
                "Enable/Disable Anti-Recoil\n" +
                "When enabled, the system will automatically apply downward mouse compensation\n" +
                "to counteract vertical recoil when firing weapons.");

            _toolTip!.SetToolTip(_strengthTrackBar!,
                "Compensation Strength (0-100%)\n" +
                "Controls how much downward movement is applied to counter recoil.\n" +
                "• 0% = No compensation\n" +
                "• 50% = Half of detected recoil is compensated\n" +
                "• 100% = Full compensation (may overcorrect)");

            _toolTip!.SetToolTip(_activationDelayTrackBar!,
                "Activation Delay (0-1000ms)\n" +
                "Time to wait after firing starts before applying compensation.\n" +
                "• Lower values = Faster response\n" +
                "• Higher values = Avoids false triggers from initial aim adjustments");

            _toolTip!.SetToolTip(_thresholdTrackBar!,
                "Vertical Threshold (0.1-100px)\n" +
                "Minimum upward mouse movement required to trigger compensation.\n" +
                "• Lower values = More sensitive to small movements\n" +
                "• Higher values = Only triggers on significant recoil");

            _toolTip!.SetToolTip(_horizontalCompTrackBar!,
                "Horizontal Compensation (0-100%)\n" +
                "Percentage of horizontal recoil to compensate for side-to-side movement.\n" +
                "• 0% = No horizontal compensation\n" +
                "• 50% = Moderate side recoil control\n" +
                "• 100% = Full horizontal compensation");

            _toolTip!.SetToolTip(_adaptiveCompCheckBox!,
                "Adaptive Compensation\n" +
                "Dynamically adjusts compensation strength based on recent recoil patterns.\n" +
                "When enabled, the system learns from recent recoil and adjusts accordingly.");

            _toolTip!.SetToolTip(_maxTickCompTrackBar!,
                "Max Tick Compensation (0-50px)\n" +
                "Maximum compensation applied per mouse movement event.\n" +
                "Prevents single large corrections that could cause jerky movement.\n" +
                "• 0 = No limit\n" +
                "• 10-20 = Smooth compensation\n" +
                "• >30 = May feel sluggish");

            _toolTip!.SetToolTip(_maxTotalCompTrackBar!,
                "Max Total Compensation (0-500px)\n" +
                "Maximum accumulated compensation during a firing burst.\n" +
                "Prevents over-compensation during long spray patterns.\n" +
                "• 0 = No limit\n" +
                "• 100-200 = Good for most weapons\n" +
                "• >300 = For high-recoil weapons");

            _toolTip!.SetToolTip(_cooldownNumeric!,
                "Cooldown Period (0-5000ms)\n" +
                "Time to wait after compensation before it can trigger again.\n" +
                "Prevents rapid oscillation and allows for manual corrections.\n" +
                "• 0ms = No cooldown\n" +
                "• 100-300ms = Good for most scenarios");

            _toolTip!.SetToolTip(_decayNumeric!,
                "Decay Rate (0-10px/ms)\n" +
                "Rate at which accumulated compensation decays over time.\n" +
                "Helps reset compensation between firing bursts.\n" +
                "• 0 = No decay\n" +
                "• 0.01-0.1 = Gradual decay\n" +
                "• >0.5 = Rapid reset");

            // Add tooltips for pattern and simulation controls
            _toolTip!.SetToolTip(_recordPatternButton!,
                "Record Recoil Pattern (Ctrl+R)\n" +
                "Captures mouse movements while firing to create a recoil pattern.\n" +
                "Fire your weapon normally after clicking to record the pattern.");

            _toolTip!.SetToolTip(_simulateButton!,
                "Simulate Pattern\n" +
                "Runs the selected pattern through the current anti-recoil settings\n" +
                "to preview how compensation would be applied.");

            _toolTip!.SetToolTip(_telemetryControl!,
                "Live Telemetry Display\n" +
                "Real-time visualization of mouse movements and compensation.\n" +
                "• Cyan = Vertical movements\n" +
                "• Orange = Applied compensation\n" +
                "• Yellow = Accumulated compensation\n" +
                "• Green shading = Active compensation periods");
        }

        private void LoadSettings()
        {
            _viewModel.LoadFrom(_antiRecoil);
            UpdateControlsFromViewModel();
            UpdateFormTitle();
            RefreshPatternList();

            // Initialize pattern control states
            _stopRecordingButton!.Enabled = false;
            _patternNotesTextBox!.Enabled = false;
            _patternTagsTextBox!.Enabled = false;
        }

        private void UpdateControlsFromViewModel()
        {
            _updatingControls = true;

            _enabledCheckBox!.Checked = _viewModel.Enabled;
            _strengthTrackBar!.Value = (int)(_viewModel.Strength * 100);
            _strengthNumeric!.Value = (decimal)(_viewModel.Strength * 100);
            _strengthValueLabel!.Text = _viewModel.Strength.ToString("P0");

            _activationDelayTrackBar!.Value = Math.Min(1000, _viewModel.ActivationDelayMs);
            _activationDelayNumeric!.Value = _viewModel.ActivationDelayMs;

            _thresholdTrackBar!.Value = Math.Min(100, (int)(_viewModel.VerticalThreshold * 10));
            _thresholdNumeric!.Value = (decimal)_viewModel.VerticalThreshold;

            _horizontalCompTrackBar!.Value = (int)(_viewModel.HorizontalCompensation * 100);
            _horizontalCompNumeric!.Value = (decimal)(_viewModel.HorizontalCompensation * 100);

            _adaptiveCompCheckBox!.Checked = _viewModel.AdaptiveCompensation;

            _maxTickCompTrackBar!.Value = Math.Min(50, (int)_viewModel.MaxTickCompensation);
            _maxTickCompNumeric!.Value = (decimal)_viewModel.MaxTickCompensation;

            _maxTotalCompTrackBar!.Value = Math.Min(500, (int)_viewModel.MaxTotalCompensation);
            _maxTotalCompNumeric!.Value = (decimal)_viewModel.MaxTotalCompensation;

            _cooldownNumeric!.Value = _viewModel.CooldownMs;
            _decayNumeric!.Value = (decimal)_viewModel.DecayPerMs;

            _updatingControls = false;
        }

        private void UpdateFormTitle()
        {
            // This is now handled by ValidationSystem_ValidationChanged
            // but we'll trigger validation to update the title
            _validationSystem?.ValidateAll();
        }

        private void SetupEventHandlers()
        {
            // General tab event handlers

            _enabledCheckBox!.CheckedChanged += (s, e) => { if (!_updatingControls) { _viewModel.Enabled = _enabledCheckBox!.Checked; UpdateFormTitle(); } };

            _strengthTrackBar!.ValueChanged += (s, e) => {
                if (!_updatingControls) {
                    var val = _strengthTrackBar!.Value / 100f;
                    _viewModel.Strength = val;
                    _strengthNumeric!.Value = (decimal)(_viewModel.Strength * 100);
                    _strengthValueLabel!.Text = _viewModel.Strength.ToString("P0");
                    UpdateFormTitle();
                }
            };

            _strengthNumeric!.ValueChanged += (s, e) => {
                if (!_updatingControls) {
                    var val = (float)_strengthNumeric!.Value / 100f;
                    _viewModel.Strength = val;
                    _strengthTrackBar!.Value = (int)(_viewModel.Strength * 100);
                    _strengthValueLabel!.Text = _viewModel.Strength.ToString("P0");
                    UpdateFormTitle();
                }
            };

            // Add similar handlers for other controls...
            _activationDelayNumeric!.ValueChanged += (s, e) => { if (!_updatingControls) { _viewModel.ActivationDelayMs = (int)_activationDelayNumeric!.Value; UpdateFormTitle(); } };
            _thresholdNumeric!.ValueChanged += (s, e) => { if (!_updatingControls) { _viewModel.VerticalThreshold = (float)_thresholdNumeric!.Value; UpdateFormTitle(); } };
            _horizontalCompNumeric!.ValueChanged += (s, e) => { if (!_updatingControls) { _viewModel.HorizontalCompensation = (float)_horizontalCompNumeric!.Value / 100f; UpdateFormTitle(); } };
            _adaptiveCompCheckBox!.CheckedChanged += (s, e) => { if (!_updatingControls) { _viewModel.AdaptiveCompensation = _adaptiveCompCheckBox!.Checked; UpdateFormTitle(); } };
            _maxTickCompNumeric!.ValueChanged += (s, e) => { if (!_updatingControls) { _viewModel.MaxTickCompensation = (float)_maxTickCompNumeric!.Value; UpdateFormTitle(); } };
            _maxTotalCompNumeric!.ValueChanged += (s, e) => { if (!_updatingControls) { _viewModel.MaxTotalCompensation = (float)_maxTotalCompNumeric!.Value; UpdateFormTitle(); } };
            _cooldownNumeric!.ValueChanged += (s, e) => { if (!_updatingControls) { _viewModel.CooldownMs = (int)_cooldownNumeric!.Value; UpdateFormTitle(); } };
            _decayNumeric!.ValueChanged += (s, e) => { if (!_updatingControls) { _viewModel.DecayPerMs = (float)_decayNumeric!.Value; UpdateFormTitle(); } };

            // Pattern tab handlers
            _recordPatternButton!.Click += RecordPatternButton_Click;
            _stopRecordingButton!.Click += StopRecordingButton_Click;
            _deletePatternButton!.Click += DeletePatternButton_Click;
            _renamePatternButton!.Click += RenamePatternButton_Click;
            _exportPatternButton!.Click += ExportPatternButton_Click;
            _importPatternButton!.Click += ImportPatternButton_Click;
            _patternsListView!.SelectedIndexChanged += PatternsListView_SelectedIndexChanged;
            _patternNotesTextBox!.TextChanged += PatternNotesTextBox_TextChanged;
            _patternTagsTextBox!.TextChanged += PatternTagsTextBox_TextChanged;

            // Simulation tab handlers
            _simulateButton!.Click += SimulateButton_Click;
            _exportSimButton!.Click += ExportSimButton_Click;
            _playbackButton!.Click += PlaybackButton_Click;

            // Anti-recoil events
            _antiRecoil.PatternListChanged += () => BeginInvoke(RefreshPatternList);
            _antiRecoil.RecordingStarted += () => BeginInvoke(OnRecordingStarted);
            _antiRecoil.RecordingStopped += () => BeginInvoke(OnRecordingStopped);
        }

        private void SetupTimers()
        {
            _telemetryTimer = new WinFormsTimer { Interval = 100 }; // 10Hz telemetry updates
            _telemetryTimer.Tick += TelemetryTimer_Tick;
            _telemetryTimer.Start();

            _playbackTimer = new WinFormsTimer { Interval = 50 }; // 20Hz playback
            _playbackTimer!.Tick += PlaybackTimer_Tick;
        }

        #region Event Handlers

        private void ApplyButton_Click(object? sender, EventArgs e)
        {
            // Validate before applying
            _validationSystem.ValidateAll();

            if (_validationSystem.HasErrors)
            {
                MessageBox.Show(this,
                    $"Cannot apply changes due to {_validationSystem.ErrorCount} validation error{(_validationSystem.ErrorCount == 1 ? "" : "s")}.\n\n" +
                    "Please fix the highlighted errors and try again.",
                    "Validation Errors", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (_validationSystem.HasWarnings)
            {
                var result = MessageBox.Show(this,
                    $"There are {_validationSystem.WarningCount} validation warning{(_validationSystem.WarningCount == 1 ? "" : "s")}.\n\n" +
                    "Do you want to apply the changes anyway?",
                    "Validation Warnings", MessageBoxButtons.YesNo, MessageBoxIcon.Question);

                if (result != DialogResult.Yes)
                    return;
            }

            try
            {
                // Create backup before applying changes
                CreateAutoBackups();

                _viewModel.ApplyTo(_antiRecoil);
                UpdateFormTitle();
                _statusLabel!.Text = "Settings applied successfully";

                // Brief success indication
                var originalColor = _statusLabel!.ForeColor;
                _statusLabel!.ForeColor = Color.LightGreen;
                var timer = new System.Windows.Forms.Timer { Interval = 2000 };
                timer.Tick += (s, args) =>
                {
                    _statusLabel!.ForeColor = originalColor;
                    _statusLabel!.Text = "Ready";
                    timer.Dispose();
                };
                timer.Start();
            }
            catch (Exception ex)
            {
                MessageBox.Show(this,
                    $"Failed to apply settings: {ex.Message}",
                    "Apply Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                _statusLabel!.Text = "Failed to apply settings";
            }
        }

        private void RevertButton_Click(object? sender, EventArgs e)
        {
            _viewModel.LoadFrom(_antiRecoil);
            UpdateControlsFromViewModel();
            UpdateFormTitle();
        }

        private void RecordPatternButton_Click(object? sender, EventArgs e)
        {
            var name = string.IsNullOrWhiteSpace(_recordingNameTextBox!.Text)
                ? $"Pattern_{DateTime.Now:HHmmss}"
                : _recordingNameTextBox.Text.Trim();

            // Check for duplicate names
            if (_antiRecoil.GetPattern(name) != null)
            {
                var result = MessageBox.Show(this,
                    $"Pattern '{name}' already exists. Overwrite?",
                    "Duplicate Pattern",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Question);
                if (result != DialogResult.Yes) return;
            }

            if (_antiRecoil.StartPatternRecording(name))
            {
                MessageBox.Show(this,
                    "Recording started! Instructions:\n\n" +
                    "1. Start firing your weapon\n" +
                    "2. Let the recoil pattern develop naturally\n" +
                    "3. Click 'Stop' when you have enough samples\n\n" +
                    "The anti-recoil system will capture mouse movements during firing.",
                    "Pattern Recording Started",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
            }
            else
            {
                MessageBox.Show(this, "Failed to start recording. Make sure no other recording is in progress.", "Recording Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void StopRecordingButton_Click(object? sender, EventArgs e)
        {
            var pattern = _antiRecoil.StopPatternRecording(true);
            _recordPatternButton!.Enabled = true;
            _stopRecordingButton!.Enabled = false;

            if (pattern != null)
                MessageBox.Show(this, $"Pattern '{pattern.Name}' recorded with {pattern.Samples.Count} samples.", "Pattern Recording", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void DeletePatternButton_Click(object? sender, EventArgs e)
        {
            if (_patternsListView!.SelectedItems.Count == 0) return;
            var name = _patternsListView!.SelectedItems[0].Text;
            if (MessageBox.Show(this, $"Delete pattern '{name}'?", "Confirm Delete", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
                _antiRecoil.DeletePattern(name);
        }

        private void ExportPatternButton_Click(object? sender, EventArgs e)
        {
            if (_patternsListView!.SelectedItems.Count == 0) return;
            var name = _patternsListView!.SelectedItems[0].Text;
            var pattern = _antiRecoil.GetPattern(name);
            if (pattern == null) return;

            using var sfd = new SaveFileDialog { Filter = "JSON files (*.json)|*.json", FileName = $"{name}.json" };
            if (sfd.ShowDialog() == DialogResult.OK)
            {
                try
                {
                    _antiRecoil.ExportPattern(pattern, sfd.FileName);
                    MessageBox.Show(this, "Pattern exported successfully.", "Export", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show(this, $"Export failed: {ex.Message}", "Export Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        private void ImportPatternButton_Click(object? sender, EventArgs e)
        {
            using var ofd = new OpenFileDialog { Filter = "JSON files (*.json)|*.json" };
            if (ofd.ShowDialog() == DialogResult.OK)
            {
                var pattern = _antiRecoil.ImportPattern(ofd.FileName);
                if (pattern != null)
                    MessageBox.Show(this, $"Pattern '{pattern.Name}' imported successfully.", "Import", MessageBoxButtons.OK, MessageBoxIcon.Information);
                else
                    MessageBox.Show(this, "Failed to import pattern.", "Import Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void RenamePatternButton_Click(object? sender, EventArgs e)
        {
            if (_patternsListView!.SelectedItems.Count == 0) return;
            var oldName = _patternsListView!.SelectedItems[0].Text;
            var pattern = _antiRecoil.GetPattern(oldName);
            if (pattern == null) return;

            using var dialog = new TextInputDialog($"Rename '{oldName}' to:", "Rename Pattern", oldName);
            if (dialog.ShowDialog() == DialogResult.OK && !string.IsNullOrWhiteSpace(dialog.InputText))
            {
                var newName = dialog.InputText.Trim();
                if (newName != oldName)
                {
                    // Check if new name already exists
                    if (_antiRecoil.GetPattern(newName) != null)
                    {
                        MessageBox.Show(this, "A pattern with this name already exists.", "Rename Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        return;
                    }

                    // Delete old pattern and add with new name
                    _antiRecoil.DeletePattern(oldName);
                    pattern.Name = newName;

                    // Re-add the pattern
                    var patterns = typeof(AntiRecoil)
                        .GetField("_patterns", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                        ?.GetValue(_antiRecoil) as List<AntiRecoilPattern>;
                    patterns?.Add(pattern);

                    // Save and refresh
                    typeof(AntiRecoil)
                        .GetMethod("SavePatterns", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                        ?.Invoke(_antiRecoil, null);

                    RefreshPatternList();
                }
            }
        }

        private void PatternsListView_SelectedIndexChanged(object? sender, EventArgs e)
        {
            if (_patternsListView!.SelectedItems.Count == 0)
            {
                _patternNotesTextBox!.Text = "";
                _patternTagsTextBox!.Text = "";
                _patternNotesTextBox!.Enabled = false;
                _patternTagsTextBox!.Enabled = false;
                _patternGraphControl!.Pattern = null;
                return;
            }

            var selectedName = _patternsListView!.SelectedItems[0].Text;
            var pattern = _antiRecoil.GetPattern(selectedName);
            if (pattern != null)
            {
                _patternNotesTextBox!.Text = pattern.Notes ?? "";
                _patternTagsTextBox!.Text = string.Join(", ", pattern.Tags);
                _patternNotesTextBox!.Enabled = true;
                _patternTagsTextBox!.Enabled = true;
                _patternGraphControl!.Pattern = pattern;
            }
        }

        private void PatternNotesTextBox_TextChanged(object? sender, EventArgs e)
        {
            if (_patternsListView!.SelectedItems.Count == 0) return;
            var selectedName = _patternsListView!.SelectedItems[0].Text;
            var pattern = _antiRecoil.GetPattern(selectedName);
            if (pattern != null)
            {
                pattern.Notes = _patternNotesTextBox!.Text;
                SaveCurrentPattern();
            }
        }

        private void PatternTagsTextBox_TextChanged(object? sender, EventArgs e)
        {
            if (_patternsListView!.SelectedItems.Count == 0) return;
            var selectedName = _patternsListView!.SelectedItems[0].Text;
            var pattern = _antiRecoil.GetPattern(selectedName);
            if (pattern != null)
            {
                pattern.Tags.Clear();
                var tags = _patternTagsTextBox!.Text.Split(',', StringSplitOptions.RemoveEmptyEntries)
                    .Select(t => t.Trim())
                    .Where(t => !string.IsNullOrWhiteSpace(t));
                pattern.Tags.AddRange(tags);
                SaveCurrentPattern();
                RefreshPatternList(); // Refresh to show updated tags
            }
        }

        private void SaveCurrentPattern()
        {
            try
            {
                // Create backup before saving patterns
                BackupManager.CreatePatternsBackup("Profiles/anti_recoil_patterns.json");

                // Use reflection to call SavePatterns method
                typeof(AntiRecoil)
                    .GetMethod("SavePatterns", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                    ?.Invoke(_antiRecoil, null);
            }
            catch (Exception ex)
            {
                Logger.Error("Failed to save pattern changes", ex);
            }
        }

        private void CreateAutoBackups()
        {
            try
            {
                // Check if auto-backup is enabled in user preferences
                if (_uiState?.UserPrefs?.AutoBackup == true)
                {
                    // Create backups of all important files
                    BackupManager.CreateSettingsBackup("antirecoil_settings.json");
                    BackupManager.CreatePatternsBackup("Profiles/anti_recoil_patterns.json");

                    // Check if it's time for a full backup (based on interval)
                    var lastFullBackup = GetLastFullBackupTime();
                    var intervalHours = _uiState.UserPrefs.BackupIntervalHours;

                    if (DateTime.UtcNow.Subtract(lastFullBackup).TotalHours >= intervalHours)
                    {
                        BackupManager.CreateFullBackup();
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Info("Auto-backup failed: {Message}", ex.Message);
            }
        }

        private DateTime GetLastFullBackupTime()
        {
            try
            {
                const string backupDir = "Backups";
                if (!Directory.Exists(backupDir))
                    return DateTime.MinValue;

                var fullBackupDirs = Directory.GetDirectories(backupDir, "full_backup_*");
                if (fullBackupDirs.Length == 0)
                    return DateTime.MinValue;

                var lastDir = fullBackupDirs
                    .Select(d => new DirectoryInfo(d))
                    .OrderByDescending(d => d.CreationTime)
                    .First();

                return lastDir.CreationTime;
            }
            catch
            {
                return DateTime.MinValue;
            }
        }

        private void OnRecordingStarted()
        {
            _recordPatternButton!.Enabled = false;
            _stopRecordingButton!.Enabled = true;
            _recordPatternButton.Text = "Recording...";
            _recordPatternButton.BackColor = Color.FromArgb(100, 100, 100);
        }

        private void OnRecordingStopped()
        {
            _recordPatternButton!.Enabled = true;
            _stopRecordingButton!.Enabled = false;
            _recordPatternButton!.Text = "Record";
            _recordPatternButton!.BackColor = Color.FromArgb(0, 120, 200);
        }

        private void PatternGraph_SampleClicked(object? sender, SampleClickEventArgs e)
        {
            // Show sample details in a tooltip or status bar
            var message = $"Sample {e.SampleIndex}: Dx={e.Sample.Dx:F2}, Dy={e.Sample.Dy:F2}";
            // Could show in a label or tooltip
        }

        private void SimulationGraph_SampleClicked(object? sender, SampleClickEventArgs e)
        {
            _playbackTrackBar!.Value = Math.Min(_playbackTrackBar!.Maximum, e.SampleIndex);
            UpdateSimulationInfo(e.SampleIndex);
        }

        private void SimulationGraph_SampleHovered(object? sender, SampleHoverEventArgs e)
        {
            // Update hover info if needed
        }

        private void DisplayModeCombo_SelectedIndexChanged(object? sender, EventArgs e)
        {
            if (_simulationGraphControl != null)
            {
                _simulationGraphControl!.DisplayMode = _displayModeCombo!.SelectedIndex switch
                {
                    0 => GraphDisplayMode.InputOnly,
                    1 => GraphDisplayMode.OutputOnly,
                    2 => GraphDisplayMode.InputAndOutput,
                    3 => GraphDisplayMode.CompensationOnly,
                    4 => GraphDisplayMode.All,
                    _ => GraphDisplayMode.InputAndOutput
                };
            }
        }

        private void PlaybackTrackBar_ValueChanged(object? sender, EventArgs e)
        {
            if (_simulationGraphControl != null)
            {
                _simulationGraphControl!.HighlightSampleIndex = _playbackTrackBar!.Value;
                UpdateSimulationInfo(_playbackTrackBar.Value);
            }
        }

        private void UpdateSimulationInfo(int sampleIndex)
        {
            if (_currentSimulation?.Points != null && sampleIndex >= 0 && sampleIndex < _currentSimulation.Points.Count)
            {
                var point = _currentSimulation.Points[sampleIndex];
                _simulationResultsLabel!.Text = $"Pattern: {_currentSimulation!.PatternName} | Point [{sampleIndex}]: Input({point.InputDx:F1}, {point.InputDy:F1}) → Output({point.OutputDx:F1}, {point.OutputDy:F1}) | Compensation: {point.CompensationY:F2}";
            }
        }

        private void NormalizeButton_Click(object? sender, EventArgs e)
        {
            if (_patternsListView!.SelectedItems.Count == 0)
            {
                MessageBox.Show(this, "Please select a pattern to normalize.", "No Pattern Selected", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            var patternName = _patternsListView!.SelectedItems[0].Text;
            var pattern = _antiRecoil.GetPattern(patternName);
            if (pattern == null) return;

            using var dialog = new NumericInputDialog("Enter target peak vertical movement:", "Normalize Pattern", 10.0f);
            if (dialog.ShowDialog() == DialogResult.OK)
            {
                try
                {
                    var normalizedPattern = PatternTransforms.Normalize(pattern, dialog.Value);
                    AddTransformedPattern(normalizedPattern);
                }
                catch (Exception ex)
                {
                    MessageBox.Show(this, $"Normalization failed: {ex.Message}", "Transform Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        private void SmoothButton_Click(object? sender, EventArgs e)
        {
            if (_patternsListView!.SelectedItems.Count == 0)
            {
                MessageBox.Show(this, "Please select a pattern to smooth.", "No Pattern Selected", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            var patternName = _patternsListView!.SelectedItems[0].Text;
            var pattern = _antiRecoil.GetPattern(patternName);
            if (pattern == null) return;

            using var dialog = new NumericInputDialog("Enter smoothing window size:", "Smooth Pattern", 5, true);
            if (dialog.ShowDialog() == DialogResult.OK)
            {
                try
                {
                    var smoothedPattern = PatternTransforms.SmoothMovingAverage(pattern, (int)dialog.Value);
                    AddTransformedPattern(smoothedPattern);
                }
                catch (Exception ex)
                {
                    MessageBox.Show(this, $"Smoothing failed: {ex.Message}", "Transform Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        private void TrimButton_Click(object? sender, EventArgs e)
        {
            if (_patternsListView!.SelectedItems.Count == 0)
            {
                MessageBox.Show(this, "Please select a pattern to trim.", "No Pattern Selected", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            var patternName = _patternsListView!.SelectedItems[0].Text;
            var pattern = _antiRecoil.GetPattern(patternName);
            if (pattern == null) return;

            try
            {
                var trimmedPattern = PatternTransforms.TrimLeadingZeroDy(pattern);
                if (trimmedPattern.Samples.Count == pattern.Samples.Count)
                {
                    MessageBox.Show(this, "No leading zero samples found to trim.", "Trim Result", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }
                AddTransformedPattern(trimmedPattern);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, $"Trimming failed: {ex.Message}", "Transform Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void DownsampleButton_Click(object? sender, EventArgs e)
        {
            if (_patternsListView!.SelectedItems.Count == 0)
            {
                MessageBox.Show(this, "Please select a pattern to downsample.", "No Pattern Selected", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            var patternName = _patternsListView!.SelectedItems[0].Text;
            var pattern = _antiRecoil.GetPattern(patternName);
            if (pattern == null) return;

            using var dialog = new NumericInputDialog("Enter downsampling factor (2 = keep every 2nd sample):", "Downsample Pattern", 2, true);
            if (dialog.ShowDialog() == DialogResult.OK)
            {
                try
                {
                    var downsampledPattern = PatternTransforms.Downsample(pattern, (int)dialog.Value);
                    AddTransformedPattern(downsampledPattern);
                }
                catch (Exception ex)
                {
                    MessageBox.Show(this, $"Downsampling failed: {ex.Message}", "Transform Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        private void MoreTransformButton_Click(object? sender, EventArgs e)
        {
            if (_patternsListView!.SelectedItems.Count == 0)
            {
                MessageBox.Show(this, "Please select a pattern first.", "No Pattern Selected", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            var patternName = _patternsListView!.SelectedItems[0].Text;
            var pattern = _antiRecoil.GetPattern(patternName);
            if (pattern == null) return;

            using var dialog = new AdvancedTransformDialog(pattern);
            if (dialog.ShowDialog() == DialogResult.OK && dialog.TransformedPattern != null)
            {
                AddTransformedPattern(dialog.TransformedPattern);
            }
        }

        private void AddTransformedPattern(AntiRecoilPattern transformedPattern)
        {
            // Add to internal patterns list using reflection
            var patterns = typeof(AntiRecoil)
                .GetField("_patterns", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                ?.GetValue(_antiRecoil) as List<AntiRecoilPattern>;

            if (patterns != null)
            {
                // Check for duplicate names and append number if needed
                var originalName = transformedPattern.Name;
                var counter = 1;
                while (_antiRecoil.GetPattern(transformedPattern.Name) != null)
                {
                    transformedPattern.Name = $"{originalName}_{counter}";
                    counter++;
                }

                patterns.Add(transformedPattern);

                // Save patterns
                typeof(AntiRecoil)
                    .GetMethod("SavePatterns", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                    ?.Invoke(_antiRecoil, null);

                RefreshPatternList();
                MessageBox.Show(this, $"Transform completed! Created pattern '{transformedPattern.Name}'.", "Transform Complete", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }

        private void SimulateButton_Click(object? sender, EventArgs e)
        {
            if (_simulationPatternCombo?.SelectedItem == null) return;
            var patternName = _simulationPatternCombo!.SelectedItem?.ToString() ?? string.Empty;
            var pattern = _antiRecoil.GetPattern(patternName);
            if (pattern == null) return;

            try
            {
                _currentSimulation = _antiRecoil.SimulatePattern(pattern);
                _simulationResultsLabel!.Text = $"Pattern: {_currentSimulation!.PatternName} | Points: {_currentSimulation.Points.Count} | Total Y: {_currentSimulation.TotalCompY:F1} | Avg Y: {_currentSimulation.AvgCompY:F2}";

                // Update graph control
                _simulationGraphControl!.Pattern = pattern;
                _simulationGraphControl!.SimulationResult = _currentSimulation;

                // Setup playback
                _playbackTrackBar!.Maximum = Math.Max(1, _currentSimulation!.Points.Count - 1);
                _playbackTrackBar.Value = 0;
                _simulationGraphControl!.HighlightSampleIndex = 0;

                MessageBox.Show(this,
                    $"Simulation completed!\n\n" +
                    $"Pattern: {_currentSimulation.PatternName}\n" +
                    $"Samples processed: {_currentSimulation.Points.Count}\n" +
                    $"Total Y compensation: {_currentSimulation.TotalCompY:F1}\n" +
                    $"Average Y compensation: {_currentSimulation.AvgCompY:F2}\n\n" +
                    $"Use the playback controls to step through the simulation.",
                    "Simulation Complete", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, $"Simulation failed: {ex.Message}", "Simulation Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void ExportSimButton_Click(object? sender, EventArgs e)
        {
            if (_currentSimulation?.Points == null || _currentSimulation.Points.Count == 0)
            {
                MessageBox.Show(this, "No simulation data to export. Run a simulation first.", "No Data", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            using var sfd = new SaveFileDialog
            {
                Filter = "CSV files (*.csv)|*.csv|JSON files (*.json)|*.json",
                FileName = $"{_currentSimulation.PatternName}_simulation.csv"
            };

            if (sfd.ShowDialog() == DialogResult.OK)
            {
                try
                {
                    if (sfd.FileName.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
                    {
                        ExportSimulationAsJson(sfd.FileName);
                    }
                    else
                    {
                        ExportSimulationAsCsv(sfd.FileName);
                    }
                    MessageBox.Show(this, "Simulation data exported successfully.", "Export Complete", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show(this, $"Export failed: {ex.Message}", "Export Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        private void ExportSimulationAsCsv(string fileName)
        {
            using var writer = new System.IO.StreamWriter(fileName);
            writer.WriteLine("Index,InputDx,InputDy,OutputDx,OutputDy,CompensationY");

            for (int i = 0; i < _currentSimulation!.Points.Count; i++)
            {
                var point = _currentSimulation.Points[i];
                writer.WriteLine($"{i},{point.InputDx:F3},{point.InputDy:F3},{point.OutputDx:F3},{point.OutputDy:F3},{point.CompensationY:F3}");
            }

            writer.WriteLine();
            writer.WriteLine($"# Pattern: {_currentSimulation.PatternName}");
            writer.WriteLine($"# Total Points: {_currentSimulation.Points.Count}");
            writer.WriteLine($"# Total Y Compensation: {_currentSimulation.TotalCompY:F3}");
            writer.WriteLine($"# Average Y Compensation: {_currentSimulation.AvgCompY:F3}");
        }

        private void ExportSimulationAsJson(string fileName)
        {
            // Path validation to prevent directory traversal attacks
            var fullPath = Path.GetFullPath(fileName);
            var allowedDir = Path.GetFullPath(Directory.GetCurrentDirectory());
            if (!fullPath.StartsWith(allowedDir, StringComparison.OrdinalIgnoreCase))
                throw new UnauthorizedAccessException("Path traversal detected");

            var json = System.Text.Json.JsonSerializer.Serialize(_currentSimulation, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(fileName, json);
        }

        private void PlaybackButton_Click(object? sender, EventArgs e)
        {
            if (_playbackTimer!.Enabled)
            {
                _playbackTimer!.Stop();
                _playbackButton!.Text = "Play";
            }
            else
            {
                _playbackTimer!.Start();
                _playbackButton!.Text = "Pause";
            }
        }

        private void PlaybackTimer_Tick(object? sender, EventArgs e)
        {
            if (_playbackTrackBar!.Value < _playbackTrackBar!.Maximum)
            {
                _playbackTrackBar.Value++;
                _simulationGraphControl!.HighlightSampleIndex = _playbackTrackBar!.Value;
                UpdateSimulationInfo(_playbackTrackBar.Value);
            }
            else
            {
                _playbackTimer!.Stop();
                _playbackButton!.Text = "Play";
            }
        }

        private void TelemetryTimer_Tick(object? sender, EventArgs e)
        {
            // Update telemetry display
            var status = _antiRecoil.GetStatusInfo();
            if (_telemetryStatusLabel != null) _telemetryStatusLabel.Text = $"Status: {status}";

            // Get current values
            var lastComp = _antiRecoil.LastAppliedCompensation;
            var accumComp = _antiRecoil.AccumulatedCompensation;
            var isActive = _antiRecoil.IsActive;
            var threshold = _antiRecoil.VerticalThreshold;

            // Update labels
            if (_lastDyLabel != null) _lastDyLabel.Text = $"Last Vertical Movement: {_lastVerticalMovement:F2}";
            if (_lastCompLabel != null) _lastCompLabel.Text = $"Last Applied Compensation: {lastComp:F2}";
            if (_accumCompLabel != null) _accumCompLabel.Text = $"Accumulated Compensation: {accumComp:F2}";

            // Add data point to telemetry control
            _telemetryControl?.AddDataPoint(_lastVerticalMovement, lastComp, accumComp, isActive, threshold);

            // Update statistics
            var stats = _telemetryControl?.GetStatistics();
            if (stats != null && _telemetryStatsLabel != null)
                _telemetryStatsLabel.Text = $"Duration: {stats.TimeSpan.TotalSeconds:F1}s | Max Movement: {stats.MaxVerticalMovement:F1} | Total Compensation: {stats.TotalCompensation:F1} | Efficiency: {(stats.TotalCompensation > 0 ? stats.AvgCompensation / stats.MaxVerticalMovement * 100 : 0):F1}%";

            // Update recent movements for backwards compatibility
            var recentMovements = _antiRecoil.GetRecentVerticalMovements();
            if (recentMovements.Count > 0)
            {
                _lastVerticalMovement = recentMovements.LastOrDefault();
            }
        }

        private void TelemetryModeCombo_SelectedIndexChanged(object? sender, EventArgs e)
        {
            if (_telemetryControl != null && _telemetryModeCombo != null)
            {
                _telemetryControl.DisplayMode = _telemetryModeCombo.SelectedIndex switch
                {
                    0 => TelemetryDisplayMode.VerticalMovements,
                    1 => TelemetryDisplayMode.Compensation,
                    2 => TelemetryDisplayMode.AccumulatedCompensation,
                    3 => TelemetryDisplayMode.All,
                    _ => TelemetryDisplayMode.All
                };
            }
        }

        private void TelemetryControl_DataPointAdded(object? sender, TelemetryDataPoint e)
        {
            // Could add additional processing here if needed
        }

        private void SetupValidation()
        {
            // Add validation rules for all controls
            _validationSystem.AddRule(AntiRecoilValidationRules.CreateStrengthValidation(_viewModel, _strengthNumeric!));
            _validationSystem.AddRule(AntiRecoilValidationRules.CreateThresholdValidation(_viewModel, _thresholdNumeric!));
            _validationSystem.AddRule(AntiRecoilValidationRules.CreateActivationDelayValidation(_viewModel, _activationDelayNumeric!));
            _validationSystem.AddRule(AntiRecoilValidationRules.CreateMaxTickCompensationValidation(_viewModel, _maxTickCompNumeric!));
            _validationSystem.AddRule(AntiRecoilValidationRules.CreateMaxTotalCompensationValidation(_viewModel, _maxTotalCompNumeric!));
            _validationSystem.AddRule(AntiRecoilValidationRules.CreateCooldownValidation(_viewModel, _cooldownNumeric!));
            _validationSystem.AddRule(AntiRecoilValidationRules.CreateDecayValidation(_viewModel, _decayNumeric!));
            _validationSystem.AddRule(AntiRecoilValidationRules.CreateConsistencyValidation(_viewModel, _enabledCheckBox!));

            // Listen for validation changes
            _validationSystem.ValidationChanged += ValidationSystem_ValidationChanged;

            // Hook up validation to control events
            _strengthNumeric!.ValueChanged += (s, e) => { if (!_updatingControls) _validationSystem.ValidateAll(); };
            _thresholdNumeric!.ValueChanged += (s, e) => { if (!_updatingControls) _validationSystem.ValidateAll(); };
            _activationDelayNumeric!.ValueChanged += (s, e) => { if (!_updatingControls) _validationSystem.ValidateAll(); };
            _maxTickCompNumeric!.ValueChanged += (s, e) => { if (!_updatingControls) _validationSystem.ValidateAll(); };
            _maxTotalCompNumeric!.ValueChanged += (s, e) => { if (!_updatingControls) _validationSystem.ValidateAll(); };
            _cooldownNumeric!.ValueChanged += (s, e) => { if (!_updatingControls) _validationSystem.ValidateAll(); };
            _decayNumeric!.ValueChanged += (s, e) => { if (!_updatingControls) _validationSystem.ValidateAll(); };
            _enabledCheckBox!.CheckedChanged += (s, e) => { if (!_updatingControls) _validationSystem.ValidateAll(); };
        }

        private void ValidationSystem_ValidationChanged(object? sender, ValidationEventArgs e)
        {
            // Update form title to show validation status
            var baseTitle = "Anti-Recoil Configuration";
            var dirtyIndicator = _viewModel.IsDirty ? " *" : "";
            var validationIndicator = "";

            if (e.HasErrors)
                validationIndicator = $" ({e.ErrorCount} errors)";
            else if (e.HasWarnings)
                validationIndicator = $" ({e.WarningCount} warnings)";

            Text = baseTitle + dirtyIndicator + validationIndicator;

            // Update status bar
            if (_validationLabel != null)
            {
                if (e.HasErrors)
                {
                    _validationLabel.Text = $"❌ {e.ErrorCount} error{(e.ErrorCount == 1 ? "" : "s")}";
                    _validationLabel.ForeColor = Color.Red;
                }
                else if (e.HasWarnings)
                {
                    _validationLabel.Text = $"⚠ {e.WarningCount} warning{(e.WarningCount == 1 ? "" : "s")}";
                    _validationLabel.ForeColor = Color.Orange;
                }
                else
                {
                    _validationLabel.Text = "✓ All settings valid";
                    _validationLabel.ForeColor = Color.LightGreen;
                }
            }

            // Update main status
            if (_statusLabel != null)
            {
                var status = "Ready";
                if (_viewModel.IsDirty)
                    status = "Modified - click Apply to save changes";
                if (e.HasErrors)
                    status = "Please fix errors before applying changes";

                _statusLabel.Text = status;
            }
        }

        private void SetupKeyboardShortcuts()
        {
            KeyPreview = true;
            KeyDown += AntiRecoilConfigForm_KeyDown;
        }

        private void AntiRecoilConfigForm_KeyDown(object? sender, KeyEventArgs e)
        {
            // Handle keyboard shortcuts
            if (e.Control)
            {
                switch (e.KeyCode)
                {
                    case Keys.S:
                        ApplyButton_Click(this, EventArgs.Empty);
                        e.Handled = true;
                        break;
                    case Keys.R:
                        // Toggle pattern recording or revert if not recording
                        if (_antiRecoil.IsRecordingPattern)
                            StopRecordingButton_Click(this, EventArgs.Empty);
                        else if (e.Shift)
                            RecordPatternButton_Click(this, EventArgs.Empty);
                        else
                            RevertButton_Click(this, EventArgs.Empty);
                        e.Handled = true;
                        break;
                    case Keys.T:
                        // Clear telemetry
                        _clearTelemetryButton?.PerformClick();
                        e.Handled = true;
                        break;
                    case Keys.P:
                        // Focus patterns section - scroll to patterns
                        ScrollToSection(_patternsListView!);
                        e.Handled = true;
                        break;
                }
            }
            else
            {
                switch (e.KeyCode)
                {
                    case Keys.F1:
                        ShowKeyboardShortcutsHelp();
                        e.Handled = true;
                        break;
                    case Keys.Delete:
                        if (_patternsListView!.SelectedItems.Count > 0)
                        {
                            DeletePatternButton_Click(this, EventArgs.Empty);
                            e.Handled = true;
                        }
                        break;
                    case Keys.Space:
                        if (_currentSimulation != null)
                        {
                            PlaybackButton_Click(this, EventArgs.Empty);
                            e.Handled = true;
                        }
                        break;
                    case Keys.Escape:
                        Close();
                        e.Handled = true;
                        break;
                }
            }
        }

        private void ScrollToSection(Control control)
        {
            if (control == null) return;

            // Find the section containing this control
            var parent = control.Parent;
            while (parent != null && parent != _mainLayout)
            {
                parent = parent.Parent;
            }

            if (parent == _mainLayout && control.Parent is GroupBox section)
            {
                // Scroll to the section
                var scrollPosition = section.Location.Y - 20; // Small offset for better viewing
                if (_mainScrollPanel != null) _mainScrollPanel.AutoScrollPosition = new Point(0, scrollPosition);
            }
        }

        private void ShowKeyboardShortcutsHelp()
        {
            var helpText = "Keyboard Shortcuts:\n\n" +
                          "General:\n" +
                          "  Ctrl+S - Apply changes\n" +
                          "  Ctrl+R - Revert changes\n" +
                          "  Ctrl+Shift+R - Start/Stop pattern recording\n" +
                          "  Ctrl+T - Clear telemetry\n" +
                          "  Ctrl+P - Scroll to patterns section\n" +
                          "  F1 - Show this help\n" +
                          "  Esc - Close form\n\n" +
                          "Pattern Management:\n" +
                          "  Del - Delete selected pattern\n\n" +
                          "Simulation:\n" +
                          "  Space - Play/Pause playback";

            MessageBox.Show(this, helpText, "Keyboard Shortcuts", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void RefreshPatternList()
        {
            _patternsListView!.Items.Clear();
            _simulationPatternCombo!.Items.Clear();

            foreach (var pattern in _antiRecoil.Patterns)
            {
                var item = new ListViewItem(pattern.Name);
                item.SubItems.Add(pattern.Samples.Count.ToString());
                item.SubItems.Add(pattern.CreatedUtc.ToLocalTime().ToString("MM/dd HH:mm"));
                item.SubItems.Add(string.Join(", ", pattern.Tags));
                _patternsListView!.Items.Add(item);

                _simulationPatternCombo!.Items.Add(pattern.Name);
            }
        }

        #endregion

        #region Paint Handlers

        // SimulationGraphPanel_Paint method removed - now using PatternGraphControl

        // SparklinePanel_Paint method removed - now using TelemetryControl

        #endregion

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            SaveUIState();
            _telemetryTimer?.Stop();
            _telemetryTimer?.Dispose();
            _playbackTimer?.Stop();
            _playbackTimer?.Dispose();
            _toolTip?.Dispose();
            _validationSystem?.Dispose();
            base.OnFormClosed(e);
        }

        #region UI State Persistence

        private void LoadUIState()
        {
            try
            {
                const string uiStateFile = "ui_state.json";

                // Path validation to prevent directory traversal attacks
                var fullPath = Path.GetFullPath(uiStateFile);
                var allowedDir = Path.GetFullPath(Directory.GetCurrentDirectory());
                if (!fullPath.StartsWith(allowedDir, StringComparison.OrdinalIgnoreCase))
                    throw new UnauthorizedAccessException("Path traversal detected");

                if (File.Exists(uiStateFile))
                {
                    var json = File.ReadAllText(uiStateFile);
                    var options = new JsonSerializerOptions 
                    { 
                        MaxDepth = 5,
                        PropertyNameCaseInsensitive = false,
                        AllowTrailingCommas = false
                    };
                    try 
                    {
                        _uiState = JsonSerializer.Deserialize<UiStateData>(json, options) ?? new UiStateData();
                    }
                    catch (JsonException) 
                    {
                        _uiState = new UiStateData(); // Safe fallback
                    }
                }
                else
                {
                    _uiState = new UiStateData();
                }

                // Ensure all required state objects are initialized
                EnsureUIStateDefaults();
            }
            catch (Exception ex)
            {
                Logger.Info("Failed to load UI state: {Message}", ex.Message);
                _uiState = new UiStateData();
            }
        }

        private void ApplyUIState()
        {
            try
            {
                // Apply form state
                if (_uiState != null && _uiState.FormState != null)
                {
                    var formState = _uiState.FormState;

                    // Restore size
                    if (formState.Width > 0 && formState.Height > 0)
                    {
                        Size = new Size(formState.Width, formState.Height);
                    }

                    // Restore position (if not centered)
                    if (formState.X >= 0 && formState.Y >= 0)
                    {
                        StartPosition = FormStartPosition.Manual;
                        Location = new Point(formState.X, formState.Y);
                    }

                    // Restore maximized state
                    if (formState.Maximized)
                    {
                        WindowState = FormWindowState.Maximized;
                    }
                }

                // Tab selection no longer needed in single-page layout

                // Apply display preferences
                if (_uiState?.DisplayPrefs is DisplayPreferences prefs)
                {

                    // Telemetry display mode
                    if (_telemetryModeCombo != null && !string.IsNullOrEmpty(prefs.TelemetryDisplayMode))
                    {
                        var index = _telemetryModeCombo.Items.IndexOf(prefs.TelemetryDisplayMode);
                        if (index >= 0) _telemetryModeCombo.SelectedIndex = index;
                    }

                    // Simulation display mode
                    if (_displayModeCombo != null && !string.IsNullOrEmpty(prefs.SimulationDisplayMode))
                    {
                        var index = _displayModeCombo.Items.IndexOf(prefs.SimulationDisplayMode);
                        if (index >= 0) _displayModeCombo.SelectedIndex = index;
                    }

                    // Auto-scale setting
                    if (_autoScaleCheckBox != null)
                    {
                        _autoScaleCheckBox.Checked = prefs.TelemetryAutoScale;
                    }

                    // Apply telemetry control settings
                    if (_telemetryControl != null)
                    {
                        _telemetryControl.AutoScale = prefs.TelemetryAutoScale;
                    }

                    // Apply pattern graph settings
                    if (_patternGraphControl != null)
                    {
                        _patternGraphControl.ShowGrid = prefs.ShowGrid;
                        _patternGraphControl.ShowCrosshair = prefs.ShowCrosshair;
                    }

                    if (_simulationGraphControl != null)
                    {
                        _simulationGraphControl.ShowGrid = prefs.ShowGrid;
                        _simulationGraphControl.ShowCrosshair = prefs.ShowCrosshair;
                    }
                }

                // Apply recent patterns
                if (_uiState != null && _uiState.RecentPatterns != null && _uiState.RecentPatterns.Count > 0)
                {
                    // Populate recent patterns in simulation combo
                    if (_simulationPatternCombo != null)
                    {
                        foreach (var pattern in _uiState.RecentPatterns.Take(5))
                        {
                            if (!_simulationPatternCombo.Items.Contains(pattern))
                            {
                                _simulationPatternCombo.Items.Add(pattern);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Info("Failed to apply UI state: {Message}", ex.Message);
            }
        }

        private void SaveUIState()
        {
            try
            {
                // Update form state
                if (_uiState == null) _uiState = new UiStateData();
                _uiState.FormState = new FormStateData
                {
                    Width = WindowState == FormWindowState.Normal ? Width : RestoreBounds.Width,
                    Height = WindowState == FormWindowState.Normal ? Height : RestoreBounds.Height,
                    X = WindowState == FormWindowState.Normal ? Left : RestoreBounds.X,
                    Y = WindowState == FormWindowState.Normal ? Top : RestoreBounds.Y,
                    Maximized = WindowState == FormWindowState.Maximized
                };

                // Tab selection no longer needed in single-page layout

                // Update display preferences
                if (_uiState.DisplayPrefs == null)
                    _uiState.DisplayPrefs = new DisplayPreferences();

                var prefs = _uiState.DisplayPrefs;

                if (_telemetryModeCombo?.SelectedItem != null)
                    prefs.TelemetryDisplayMode = _telemetryModeCombo.SelectedItem.ToString() ?? "All";

                if (_displayModeCombo?.SelectedItem != null)
                    prefs.SimulationDisplayMode = _displayModeCombo.SelectedItem.ToString() ?? "InputAndOutput";

                if (_autoScaleCheckBox != null)
                    prefs.TelemetryAutoScale = _autoScaleCheckBox.Checked;

                if (_patternGraphControl != null)
                {
                    prefs.ShowGrid = _patternGraphControl.ShowGrid;
                    prefs.ShowCrosshair = _patternGraphControl.ShowCrosshair;
                }

                // Update timestamp
                _uiState.LastSaved = DateTime.UtcNow;

                // Save to file
                const string uiStateFile = "ui_state.json";

                // Path validation to prevent directory traversal attacks
                var fullPath = Path.GetFullPath(uiStateFile);
                var allowedDir = Path.GetFullPath(Directory.GetCurrentDirectory());
                if (!fullPath.StartsWith(allowedDir, StringComparison.OrdinalIgnoreCase))
                    throw new UnauthorizedAccessException("Path traversal detected");

                var json = JsonSerializer.Serialize(_uiState, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(uiStateFile, json);

                // Create backup of UI state
                BackupManager.CreateUiStateBackup(_uiState);
            }
            catch (Exception ex)
            {
                Logger.Error("Failed to save UI state", ex);
            }
        }

        private void EnsureUIStateDefaults()
        {
            if (_uiState == null) _uiState = new UiStateData();
            _uiState.FormState ??= new FormStateData();
            _uiState.DisplayPrefs ??= new DisplayPreferences();
            _uiState.UserPrefs ??= new UserPreferences();
            _uiState.RecentPatterns ??= new List<string>();
        }

        #endregion
    }

    // Helper dialog for text input
    public class TextInputDialog : Form
    {
        public string InputText { get; private set; } = string.Empty;
        private TextBox _textBox;

        public TextInputDialog(string prompt, string title, string initialText = "")
        {
            Text = title;
            Size = new Size(400, 150);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            StartPosition = FormStartPosition.CenterParent;
            BackColor = Color.FromArgb(45, 45, 48);
            ForeColor = Color.White;

            var promptLabel = new Label
            {
                Text = prompt,
                Location = new Point(10, 10),
                Size = new Size(370, 20),
                ForeColor = Color.White
            };

            _textBox = new TextBox
            {
                Location = new Point(10, 40),
                Size = new Size(370, 23),
                BackColor = Color.FromArgb(60, 60, 60),
                ForeColor = Color.White,
                Text = initialText
            };

            var okButton = new Button
            {
                Text = "OK",
                Location = new Point(225, 80),
                Size = new Size(75, 25),
                BackColor = Color.FromArgb(0, 122, 204),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                DialogResult = DialogResult.OK
            };

            var cancelButton = new Button
            {
                Text = "Cancel",
                Location = new Point(305, 80),
                Size = new Size(75, 25),
                BackColor = Color.FromArgb(60, 60, 60),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                DialogResult = DialogResult.Cancel
            };

            okButton.Click += (_, __) => { InputText = _textBox.Text; Close(); };
            cancelButton.Click += (_, __) => Close();

            Controls.AddRange(new Control[] { promptLabel, _textBox, okButton, cancelButton });

            // Select all text if initial text is provided
            if (!string.IsNullOrEmpty(initialText))
            {
                _textBox.SelectAll();
            }
        }
    }

    // Helper dialog for numeric input
    public class NumericInputDialog : Form
    {
        public float Value { get; private set; }
        private NumericUpDown _numericUpDown;

        public NumericInputDialog(string prompt, string title, float initialValue, bool integerOnly = false)
        {
            Text = title;
            Size = new Size(400, 150);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            StartPosition = FormStartPosition.CenterParent;
            BackColor = Color.FromArgb(45, 45, 48);
            ForeColor = Color.White;

            var promptLabel = new Label
            {
                Text = prompt,
                Location = new Point(10, 10),
                Size = new Size(370, 20),
                ForeColor = Color.White
            };

            _numericUpDown = new NumericUpDown
            {
                Location = new Point(10, 40),
                Size = new Size(370, 23),
                BackColor = Color.FromArgb(60, 60, 60),
                ForeColor = Color.White,
                Value = (decimal)initialValue,
                Minimum = integerOnly ? 1 : 0.001M,
                Maximum = 10000,
                DecimalPlaces = integerOnly ? 0 : 3,
                Increment = integerOnly ? 1 : 0.1M
            };

            var okButton = new Button
            {
                Text = "OK",
                Location = new Point(225, 80),
                Size = new Size(75, 25),
                BackColor = Color.FromArgb(0, 122, 204),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                DialogResult = DialogResult.OK
            };

            var cancelButton = new Button
            {
                Text = "Cancel",
                Location = new Point(305, 80),
                Size = new Size(75, 25),
                BackColor = Color.FromArgb(60, 60, 60),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                DialogResult = DialogResult.Cancel
            };

            okButton.Click += (_, __) => { Value = (float)_numericUpDown.Value; Close(); };
            cancelButton.Click += (_, __) => Close();

            Controls.AddRange(new Control[] { promptLabel, _numericUpDown, okButton, cancelButton });
        }
    }

    // Advanced transform dialog
    public class AdvancedTransformDialog : Form
    {
        public AntiRecoilPattern? TransformedPattern { get; private set; }
        private readonly AntiRecoilPattern _sourcePattern;
        private ComboBox? _transformTypeCombo;
        private Panel? _parametersPanel;
        private Label? _previewLabel;

        public AdvancedTransformDialog(AntiRecoilPattern sourcePattern)
        {
            _sourcePattern = sourcePattern;
            InitializeAdvancedDialog();
        }

        private void InitializeAdvancedDialog()
        {
            Text = "Advanced Pattern Transforms";
            Size = new Size(500, 350);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            StartPosition = FormStartPosition.CenterParent;
            BackColor = Color.FromArgb(45, 45, 48);
            ForeColor = Color.White;

            // Transform type selection
            Controls.Add(new Label
            {
                Text = "Transform Type:",
                Location = new Point(10, 15),
                Size = new Size(100, 20),
                ForeColor = Color.White
            });

            _transformTypeCombo = new ComboBox
            {
                Location = new Point(120, 12),
                Size = new Size(200, 23),
                DropDownStyle = ComboBoxStyle.DropDownList,
                BackColor = Color.FromArgb(60, 60, 60),
                ForeColor = Color.White
            };
            _transformTypeCombo.Items.AddRange(new[] { "Gaussian Smooth", "Scale", "Invert", "Remove Outliers", "Statistics" });
            _transformTypeCombo.SelectedIndexChanged += TransformTypeCombo_SelectedIndexChanged;
            Controls.Add(_transformTypeCombo);

            // Parameters panel
            _parametersPanel = new Panel
            {
                Location = new Point(10, 50),
                Size = new Size(470, 150),
                BackColor = Color.FromArgb(35, 35, 35)
            };
            Controls.Add(_parametersPanel);

            // Preview
            _previewLabel = new Label
            {
                Location = new Point(10, 210),
                Size = new Size(470, 60),
                ForeColor = Color.LightGray,
                Text = "Select a transform type to see options."
            };
            Controls.Add(_previewLabel);

            // Buttons
            var okButton = new Button
            {
                Text = "Apply",
                Location = new Point(325, 285),
                Size = new Size(75, 30),
                BackColor = Color.FromArgb(0, 160, 60),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat
            };
            okButton.Click += OkButton_Click;

            var cancelButton = new Button
            {
                Text = "Cancel",
                Location = new Point(410, 285),
                Size = new Size(75, 30),
                BackColor = Color.FromArgb(60, 60, 60),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                DialogResult = DialogResult.Cancel
            };

            Controls.AddRange(new Control[] { okButton, cancelButton });
        }

        private void TransformTypeCombo_SelectedIndexChanged(object? sender, EventArgs e)
        {
            _parametersPanel!.Controls.Clear();

            switch (_transformTypeCombo!.SelectedItem?.ToString())
            {
                case "Gaussian Smooth":
                    CreateGaussianSmoothUI();
                    break;
                case "Scale":
                    CreateScaleUI();
                    break;
                case "Invert":
                    CreateInvertUI();
                    break;
                case "Remove Outliers":
                    CreateRemoveOutliersUI();
                    break;
                case "Statistics":
                    CreateStatisticsUI();
                    break;
            }
        }

        private void CreateGaussianSmoothUI()
        {
            _parametersPanel!.Controls.Add(new Label { Text = "Sigma:", Location = new Point(10, 15), ForeColor = Color.White, Size = new Size(50, 20) });
            var sigmaNumeric = new NumericUpDown
            {
                Location = new Point(70, 12),
                Size = new Size(80, 23),
                BackColor = Color.FromArgb(60, 60, 60),
                ForeColor = Color.White,
                Value = 1.0M,
                Minimum = 0.1M,
                Maximum = 10,
                DecimalPlaces = 1,
                Increment = 0.1M
            };
            _parametersPanel!.Controls.Add(sigmaNumeric);
            _parametersPanel!.Tag = sigmaNumeric;

            _previewLabel!.Text = "Applies Gaussian smoothing to reduce noise while preserving signal characteristics.";
        }

        private void CreateScaleUI()
        {
            _parametersPanel!.Controls.Add(new Label { Text = "X Scale:", Location = new Point(10, 15), ForeColor = Color.White, Size = new Size(60, 20) });
            var xScaleNumeric = new NumericUpDown
            {
                Location = new Point(80, 12),
                Size = new Size(80, 23),
                BackColor = Color.FromArgb(60, 60, 60),
                ForeColor = Color.White,
                Value = 1.0M,
                Minimum = 0.1M,
                Maximum = 10,
                DecimalPlaces = 2,
                Increment = 0.1M
            };

            _parametersPanel!.Controls.Add(xScaleNumeric);

            _parametersPanel!.Controls.Add(new Label { Text = "Y Scale:", Location = new Point(180, 15), ForeColor = Color.White, Size = new Size(60, 20) });
            var yScaleNumeric = new NumericUpDown
            {
                Location = new Point(250, 12),
                Size = new Size(80, 23),
                BackColor = Color.FromArgb(60, 60, 60),
                ForeColor = Color.White,
                Value = 1.0M,
                Minimum = 0.1M,
                Maximum = 10,
                DecimalPlaces = 2,
                Increment = 0.1M
            };
            _parametersPanel!.Controls.Add(yScaleNumeric);
            _parametersPanel!.Tag = new[] { xScaleNumeric, yScaleNumeric };

            _previewLabel!.Text = "Scales the pattern by separate X and Y factors. Use values < 1 to reduce movement, > 1 to amplify.";
        }

        private void CreateInvertUI()
        {
            _parametersPanel!.Controls.Add(new Label
            {
                Text = "This will flip all Y values (upward becomes downward movement).",
                Location = new Point(10, 15),
                Size = new Size(400, 40),
                ForeColor = Color.White
            });

            _previewLabel!.Text = "Inverts the vertical direction of the recoil pattern.";
        }

        private void CreateRemoveOutliersUI()
        {
            _parametersPanel!.Controls.Add(new Label { Text = "Threshold:", Location = new Point(10, 15), ForeColor = Color.White, Size = new Size(80, 20) });
            var thresholdNumeric = new NumericUpDown
            {
                Location = new Point(100, 12),
                Size = new Size(80, 23),
                BackColor = Color.FromArgb(60, 60, 60),
                ForeColor = Color.White,
                Value = 50.0M,
                Minimum = 1,
                Maximum = 1000,
                DecimalPlaces = 1,
                Increment = 5
            };
            _parametersPanel!.Controls.Add(thresholdNumeric);
            _parametersPanel!.Tag = thresholdNumeric;

            _previewLabel!.Text = "Removes samples with movement magnitude exceeding the threshold. Helps eliminate erratic movements.";
        }

        private void CreateStatisticsUI()
        {
            var stats = PatternTransforms.GetStatistics(_sourcePattern);
            var statsText = new TextBox
            {
                Location = new Point(10, 10),
                Size = new Size(450, 130),
                Multiline = true,
                ScrollBars = ScrollBars.Vertical,
                ReadOnly = true,
                BackColor = Color.FromArgb(30, 30, 30),
                ForeColor = Color.White,
                Font = new Font("Consolas", 9),
                Text = $"Pattern Statistics for '{_sourcePattern.Name}':\n\n" +
                       $"Sample Count: {stats.SampleCount}\n\n" +
                       $"Horizontal (DX):\n" +
                       $"  Range: {stats.DxMin:F2} to {stats.DxMax:F2}\n" +
                       $"  Mean: {stats.DxMean:F2} ± {stats.DxStdDev:F2}\n" +
                       $"  Total: {stats.TotalDx:F2}\n\n" +
                       $"Vertical (DY):\n" +
                       $"  Range: {stats.DyMin:F2} to {stats.DyMax:F2}\n" +
                       $"  Mean: {stats.DyMean:F2} ± {stats.DyStdDev:F2}\n" +
                       $"  Total: {stats.TotalDy:F2}\n\n" +
                       $"Magnitude:\n" +
                       $"  Range: {stats.MagnitudeMin:F2} to {stats.MagnitudeMax:F2}\n" +
                       $"  Mean: {stats.MagnitudeMean:F2} ± {stats.MagnitudeStdDev:F2}\n" +
                       $"  Total: {stats.TotalMagnitude:F2}"
            };
            _parametersPanel!.Controls.Add(statsText);

            _previewLabel!.Text = "Displays detailed statistics about the selected pattern. No transformation will be applied.";
        }

        private void OkButton_Click(object? sender, EventArgs e)
        {
            try
            {
                if (_transformTypeCombo == null || _parametersPanel == null)
                {
                    MessageBox.Show("Transform UI not initialized.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                switch (_transformTypeCombo.SelectedItem?.ToString())
                {
                    case "Gaussian Smooth":
                    var sigmaControl = _parametersPanel.Tag as NumericUpDown;
                    if (sigmaControl == null) throw new InvalidOperationException("Sigma control missing");
                        TransformedPattern = PatternTransforms.SmoothGaussian(_sourcePattern, (float)sigmaControl.Value);
                        break;
                    case "Scale":
                        var scaleControls = _parametersPanel.Tag as NumericUpDown[];
                        if (scaleControls == null || scaleControls.Length < 2) throw new InvalidOperationException("Scale controls missing");
                        TransformedPattern = PatternTransforms.Scale(_sourcePattern, (float)scaleControls[0].Value, (float)scaleControls[1].Value);
                        break;
                    case "Invert":
                        TransformedPattern = PatternTransforms.Invert(_sourcePattern);
                        break;
                    case "Remove Outliers":
                        var thresholdControl = _parametersPanel.Tag as NumericUpDown;
                        if (thresholdControl == null) throw new InvalidOperationException("Threshold control missing");
                        TransformedPattern = PatternTransforms.RemoveOutliers(_sourcePattern, (float)thresholdControl.Value);
                        break;
                    case "Statistics":
                        // No transformation for statistics view
                        DialogResult = DialogResult.Cancel;
                        return;
                    default:
                        MessageBox.Show("Please select a transform type.", "No Transform Selected", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        return;
                }

                DialogResult = DialogResult.OK;
                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Transform failed: {ex.Message}", "Transform Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        /// <summary>
        /// Sanitizes strings for logging to prevent log injection attacks
        /// </summary>
        private static string SanitizeForLogging(string input)
        {
            if (string.IsNullOrEmpty(input))
                return "[null_or_empty]";

            // Remove control characters and limit length
            var sanitized = Regex.Replace(input, @"[\r\n\t\x00-\x1F\x7F-\x9F]", "_", RegexOptions.None, TimeSpan.FromMilliseconds(100));
            if (sanitized.Length > 100)
                sanitized = sanitized.Substring(0, 100) + "...[truncated]";
            
            return sanitized;
        }
    }
}
