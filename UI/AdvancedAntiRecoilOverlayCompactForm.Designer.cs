using System;
using System.Drawing;
using System.Windows.Forms;

namespace WootMouseRemap.UI
{
    partial class AdvancedAntiRecoilOverlayCompactForm
    {
        private System.ComponentModel.IContainer components = null;

        private Panel panelTop;
        private Label lblTitle;
        private CheckBox chkEnable;
        private Button btnSave;
        private Button btnClose;
        private Button btnMouseSettings;

        private TabControl tabs;
        private TabPage tabBasics;
        private TabPage tabPatterns;
        private TabPage tabTransforms;
        private TabPage tabTelemetry;
        private TabPage tabSim;

        private TableLayoutPanel tlpBasics;
        private Label lblStrength;
        private TrackBar tbStrength;
        private NumericUpDown numStrength;
        private Label lblActivationDelay;
        private NumericUpDown numActivationDelay;
        private Label lblVertThreshold;
        private NumericUpDown numVertThreshold;
        private Label lblHorizComp;
        private NumericUpDown numHorizComp;
        private CheckBox chkAdaptiveComp;
        private Label lblMaxPerTick;
        private NumericUpDown numMaxPerTick;
        private Label lblMaxTotal;
        private NumericUpDown numMaxTotal;
        private Label lblCooldown;
        private NumericUpDown numCooldown;
        private Label lblDecay;
        private NumericUpDown numDecay;

        private TableLayoutPanel tlpPatterns;
        private ListView lvPatterns;
        private ColumnHeader colPatName;
        private ColumnHeader colPatTags;
        private FlowLayoutPanel flpPatternActions;
        private Button btnRecordToggle;
        private Button btnRenamePattern;
        private Button btnDeletePattern;
        private Button btnImportPattern;
        private Button btnExportPattern;
        private Label lblNotes;
        private TextBox txtNotes;
        private Label lblTags;
        private TextBox txtTags;
        private PictureBox picPatternGraph;
        private ComboBox cboGraphSource;

        private TableLayoutPanel tlpTransforms;
        private Label lblTransformType;
        private ComboBox cboTransformType;
        private Panel pnlTransformParams;
        private Button btnApplyTransform;
        private PictureBox picTransformPreview;
        private CheckBox chkPreviewBeforeAfter;

        private TableLayoutPanel tlpTelemetry;
        private ComboBox cboTelemetryMode;
        private CheckBox chkTelemetryAutoScale;
        private Button btnTelemetryClear;
        private PictureBox picTelemetry;
        private Label lblLastDy;
        private Label lblLastDyValue;
        private Label lblLastComp;
        private Label lblLastCompValue;
        private Label lblAccumComp;
        private Label lblAccumCompValue;

        private TableLayoutPanel tlpSim;
        private Label lblSimPattern;
        private ComboBox cboSimPattern;
        private Button btnSimulate;
        private TrackBar tbPlayback;
        private Button btnSimExport;
        private PictureBox picSimGraph;

        private Panel panelBottom;
        private Button btnApply;
        private Button btnSim;
        private Button btnExport;
        private Button btnReset;
        private StatusStrip statusStrip;
        private ToolStripStatusLabel statusLabel;

        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null)) components.Dispose();
            base.Dispose(disposing);
        }

        private void InitializeComponent()
        {
            components = new System.ComponentModel.Container();

            panelTop = new Panel { Dock = DockStyle.Top, Height = 36, Padding = new Padding(8, 6, 8, 4) };
            lblTitle = new Label { Text = "Advanced Anti-Recoil", AutoSize = true, Font = new Font("Segoe UI", 10F, FontStyle.Bold), Dock = DockStyle.Left };
            chkEnable = new CheckBox { Text = "Enable", AutoSize = true, Dock = DockStyle.Right, Padding = new Padding(0, 4, 0, 0) };
            btnSave = new Button { Text = "Save", Dock = DockStyle.Right, Width = 80 };
            btnClose = new Button { Text = "Close", Dock = DockStyle.Right, Width = 80, DialogResult = DialogResult.Cancel };
            btnMouseSettings = new Button { Text = "Mouse Settings", Dock = DockStyle.Right, Width = 120 };
            panelTop.Controls.Add(chkEnable);
            panelTop.Controls.Add(btnSave);
            panelTop.Controls.Add(btnClose);
            panelTop.Controls.Add(btnMouseSettings);
            panelTop.Controls.Add(lblTitle);

            tabs = new TabControl { Dock = DockStyle.Fill, Padding = new Point(12, 6) };
            tabBasics = new TabPage { Text = "Basics" };
            tabPatterns = new TabPage { Text = "Patterns" };
            tabTransforms = new TabPage { Text = "Transforms" };
            tabTelemetry = new TabPage { Text = "Telemetry" };
            tabSim = new TabPage { Text = "Sim" };
            tabs.TabPages.AddRange(new[] { tabBasics, tabPatterns, tabTransforms, tabTelemetry, tabSim });

            tlpBasics = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 3,
                RowCount = 10,
                Padding = new Padding(8)
            };
            tlpBasics.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 200F));
            tlpBasics.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            tlpBasics.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 80F));

            lblStrength = new Label { Text = "Strength", Anchor = AnchorStyles.Left, AutoSize = true };
            tbStrength = new TrackBar { Minimum = 0, Maximum = 200, TickFrequency = 10, Value = 100, Anchor = AnchorStyles.Left | AnchorStyles.Right };
            numStrength = new NumericUpDown { Minimum = 0, Maximum = 200, Value = 100, Anchor = AnchorStyles.Right };
            lblActivationDelay = new Label { Text = "Activation Delay (ms)", Anchor = AnchorStyles.Left, AutoSize = true };
            numActivationDelay = new NumericUpDown { Minimum = 0, Maximum = 1000, Value = 0, Anchor = AnchorStyles.Left };
            lblVertThreshold = new Label { Text = "Vertical Threshold", Anchor = AnchorStyles.Left, AutoSize = true };
            numVertThreshold = new NumericUpDown { Minimum = 0, Maximum = 100, Value = 0, Anchor = AnchorStyles.Left };
            lblHorizComp = new Label { Text = "Horizontal Compensation", Anchor = AnchorStyles.Left, AutoSize = true };
            numHorizComp = new NumericUpDown { Minimum = -100, Maximum = 100, Value = 0, Anchor = AnchorStyles.Left };
            chkAdaptiveComp = new CheckBox { Text = "Adaptive Compensation", Anchor = AnchorStyles.Left, AutoSize = true };
            lblMaxPerTick = new Label { Text = "Max Per-Tick", Anchor = AnchorStyles.Left, AutoSize = true };
            numMaxPerTick = new NumericUpDown { Minimum = 0, Maximum = 100, Value = 0, Anchor = AnchorStyles.Left };
            lblMaxTotal = new Label { Text = "Max Total", Anchor = AnchorStyles.Left, AutoSize = true };
            numMaxTotal = new NumericUpDown { Minimum = 0, Maximum = 500, Value = 0, Anchor = AnchorStyles.Left };
            lblCooldown = new Label { Text = "Cooldown (ms)", Anchor = AnchorStyles.Left, AutoSize = true };
            numCooldown = new NumericUpDown { Minimum = 0, Maximum = 2000, Value = 0, Anchor = AnchorStyles.Left };
            lblDecay = new Label { Text = "Decay (%)", Anchor = AnchorStyles.Left, AutoSize = true };
            numDecay = new NumericUpDown { Minimum = 0, Maximum = 100, Value = 0, Anchor = AnchorStyles.Left };

            tlpBasics.Controls.Add(lblStrength, 0, 0);
            tlpBasics.Controls.Add(tbStrength, 1, 0);
            tlpBasics.Controls.Add(numStrength, 2, 0);
            tlpBasics.Controls.Add(lblActivationDelay, 0, 1);
            tlpBasics.Controls.Add(numActivationDelay, 1, 1);
            tlpBasics.Controls.Add(lblVertThreshold, 0, 2);
            tlpBasics.Controls.Add(numVertThreshold, 1, 2);
            tlpBasics.Controls.Add(lblHorizComp, 0, 3);
            tlpBasics.Controls.Add(numHorizComp, 1, 3);
            tlpBasics.Controls.Add(chkAdaptiveComp, 1, 4);
            tlpBasics.Controls.Add(lblMaxPerTick, 0, 5);
            tlpBasics.Controls.Add(numMaxPerTick, 1, 5);
            tlpBasics.Controls.Add(lblMaxTotal, 0, 6);
            tlpBasics.Controls.Add(numMaxTotal, 1, 6);
            tlpBasics.Controls.Add(lblCooldown, 0, 7);
            tlpBasics.Controls.Add(numCooldown, 1, 7);
            tlpBasics.Controls.Add(lblDecay, 0, 8);
            tlpBasics.Controls.Add(numDecay, 1, 8);
            tabBasics.Controls.Add(tlpBasics);

            tlpPatterns = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 8, Padding = new Padding(8) };
            tlpPatterns.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 230F));
            tlpPatterns.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));

            lvPatterns = new ListView { View = View.Details, FullRowSelect = true, HideSelection = false, Dock = DockStyle.Fill };
            colPatName = new ColumnHeader { Text = "Pattern", Width = 160 };
            colPatTags = new ColumnHeader { Text = "Tags", Width = 180 };
            lvPatterns.Columns.AddRange(new[] { colPatName, colPatTags });

            flpPatternActions = new FlowLayoutPanel { FlowDirection = FlowDirection.LeftToRight, Dock = DockStyle.Fill, AutoSize = true, WrapContents = false };
            btnRecordToggle = new Button { Text = "Record / Stop", Width = 110 };
            btnRenamePattern = new Button { Text = "Rename", Width = 90 };
            btnDeletePattern = new Button { Text = "Delete", Width = 90 };
            btnImportPattern = new Button { Text = "Import", Width = 90 };
            btnExportPattern = new Button { Text = "Export", Width = 90 };
            flpPatternActions.Controls.AddRange(new Control[] { btnRecordToggle, btnRenamePattern, btnDeletePattern, btnImportPattern, btnExportPattern });

            lblNotes = new Label { Text = "Notes", Anchor = AnchorStyles.Left, AutoSize = true };
            txtNotes = new TextBox { Anchor = AnchorStyles.Left | AnchorStyles.Right, MaxLength = 4096 };
            lblTags = new Label { Text = "Tags", Anchor = AnchorStyles.Left, AutoSize = true };
            txtTags = new TextBox { Anchor = AnchorStyles.Left | AnchorStyles.Right, MaxLength = 512 };

            picPatternGraph = new PictureBox { BorderStyle = BorderStyle.FixedSingle, SizeMode = PictureBoxSizeMode.StretchImage, Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Top, Height = 150 };
            cboGraphSource = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Anchor = AnchorStyles.Left };
            cboGraphSource.Items.AddRange(new object[] { "Current Pattern", "Selection" });

            tlpPatterns.Controls.Add(lvPatterns, 0, 0);
            tlpPatterns.SetColumnSpan(lvPatterns, 2);
            tlpPatterns.Controls.Add(flpPatternActions, 0, 1);
            tlpPatterns.SetColumnSpan(flpPatternActions, 2);
            tlpPatterns.Controls.Add(lblNotes, 0, 2);
            tlpPatterns.Controls.Add(txtNotes, 1, 2);
            tlpPatterns.Controls.Add(lblTags, 0, 3);
            tlpPatterns.Controls.Add(txtTags, 1, 3);
            tlpPatterns.Controls.Add(cboGraphSource, 0, 4);
            tlpPatterns.Controls.Add(picPatternGraph, 0, 5);
            tlpPatterns.SetColumnSpan(picPatternGraph, 2);
            tabPatterns.Controls.Add(tlpPatterns);

            tlpTransforms = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 6, Padding = new Padding(8) };
            tlpTransforms.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 230F));
            tlpTransforms.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));

            lblTransformType = new Label { Text = "Transform", Anchor = AnchorStyles.Left, AutoSize = true };
            cboTransformType = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Anchor = AnchorStyles.Left | AnchorStyles.Right };
            cboTransformType.Items.AddRange(new object[] { "Gaussian Smoothing", "Remove Outliers", "Scale", "Invert" });
            pnlTransformParams = new Panel { Dock = DockStyle.Fill, BorderStyle = BorderStyle.None };
            btnApplyTransform = new Button { Text = "Apply Transform", Width = 140, Anchor = AnchorStyles.Right };
            picTransformPreview = new PictureBox { BorderStyle = BorderStyle.FixedSingle, SizeMode = PictureBoxSizeMode.StretchImage, Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Top, Height = 150 };
            chkPreviewBeforeAfter = new CheckBox { Text = "Before/After", Anchor = AnchorStyles.Left, AutoSize = true };

            tlpTransforms.Controls.Add(lblTransformType, 0, 0);
            tlpTransforms.Controls.Add(cboTransformType, 1, 0);
            tlpTransforms.Controls.Add(pnlTransformParams, 0, 1);
            tlpTransforms.SetColumnSpan(pnlTransformParams, 2);
            tlpTransforms.Controls.Add(chkPreviewBeforeAfter, 0, 2);
            tlpTransforms.Controls.Add(btnApplyTransform, 1, 2);
            tlpTransforms.Controls.Add(picTransformPreview, 0, 3);
            tlpTransforms.SetColumnSpan(picTransformPreview, 2);
            tabTransforms.Controls.Add(tlpTransforms);

            tlpTelemetry = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 7, Padding = new Padding(8) };
            tlpTelemetry.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 230F));
            tlpTelemetry.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));

            cboTelemetryMode = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Anchor = AnchorStyles.Left | AnchorStyles.Right };
            cboTelemetryMode.Items.AddRange(new object[] { "Off", "Live", "Averaged" });
            chkTelemetryAutoScale = new CheckBox { Text = "Auto-scale", Anchor = AnchorStyles.Left, AutoSize = true };
            btnTelemetryClear = new Button { Text = "Clear", Width = 90, Anchor = AnchorStyles.Right };
            picTelemetry = new PictureBox { BorderStyle = BorderStyle.FixedSingle, SizeMode = PictureBoxSizeMode.StretchImage, Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Top, Height = 150 };
            lblLastDy = new Label { Text = "Last ΔY", Anchor = AnchorStyles.Left, AutoSize = true };
            lblLastDyValue = new Label { Text = "0", Anchor = AnchorStyles.Left, AutoSize = true };
            lblLastComp = new Label { Text = "Last Comp", Anchor = AnchorStyles.Left, AutoSize = true };
            lblLastCompValue = new Label { Text = "0", Anchor = AnchorStyles.Left, AutoSize = true };
            lblAccumComp = new Label { Text = "Accum Comp", Anchor = AnchorStyles.Left, AutoSize = true };
            lblAccumCompValue = new Label { Text = "0", Anchor = AnchorStyles.Left, AutoSize = true };

            tlpTelemetry.Controls.Add(new Label { Text = "Telemetry Mode", Anchor = AnchorStyles.Left, AutoSize = true }, 0, 0);
            tlpTelemetry.Controls.Add(cboTelemetryMode, 1, 0);
            tlpTelemetry.Controls.Add(chkTelemetryAutoScale, 0, 1);
            tlpTelemetry.Controls.Add(btnTelemetryClear, 1, 1);
            tlpTelemetry.Controls.Add(picTelemetry, 0, 2);
            tlpTelemetry.SetColumnSpan(picTelemetry, 2);
            tlpTelemetry.Controls.Add(lblLastDy, 0, 3);
            tlpTelemetry.Controls.Add(lblLastDyValue, 1, 3);
            tlpTelemetry.Controls.Add(lblLastComp, 0, 4);
            tlpTelemetry.Controls.Add(lblLastCompValue, 1, 4);
            tlpTelemetry.Controls.Add(lblAccumComp, 0, 5);
            tlpTelemetry.Controls.Add(lblAccumCompValue, 1, 5);
            tabTelemetry.Controls.Add(tlpTelemetry);

            tlpSim = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 6, Padding = new Padding(8) };
            tlpSim.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 230F));
            tlpSim.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));

            lblSimPattern = new Label { Text = "Pattern", Anchor = AnchorStyles.Left, AutoSize = true };
            cboSimPattern = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Anchor = AnchorStyles.Left | AnchorStyles.Right };
            btnSimulate = new Button { Text = "Simulate", Width = 100, Anchor = AnchorStyles.Right };
            tbPlayback = new TrackBar { Minimum = 0, Maximum = 1000, TickFrequency = 50, Value = 0, Anchor = AnchorStyles.Left | AnchorStyles.Right };
            btnSimExport = new Button { Text = "Export", Width = 100, Anchor = AnchorStyles.Right };
            picSimGraph = new PictureBox { BorderStyle = BorderStyle.FixedSingle, SizeMode = PictureBoxSizeMode.StretchImage, Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Top, Height = 150 };

            tlpSim.Controls.Add(lblSimPattern, 0, 0);
            tlpSim.Controls.Add(cboSimPattern, 1, 0);
            tlpSim.Controls.Add(btnSimulate, 1, 1);
            tlpSim.Controls.Add(tbPlayback, 0, 2);
            tlpSim.SetColumnSpan(tbPlayback, 2);
            tlpSim.Controls.Add(btnSimExport, 1, 3);
            tlpSim.Controls.Add(picSimGraph, 0, 4);
            tlpSim.SetColumnSpan(picSimGraph, 2);
            tabSim.Controls.Add(tlpSim);

            panelBottom = new Panel { Dock = DockStyle.Bottom, Height = 48, Padding = new Padding(8, 6, 8, 2) };
            btnApply = new Button { Text = "Apply", Width = 100, Anchor = AnchorStyles.Right | AnchorStyles.Top };
            btnSim = new Button { Text = "Simulate", Width = 100, Anchor = AnchorStyles.Right | AnchorStyles.Top };
            btnExport = new Button { Text = "Export", Width = 100, Anchor = AnchorStyles.Right | AnchorStyles.Top };
            btnReset = new Button { Text = "Reset", Width = 100, Anchor = AnchorStyles.Right | AnchorStyles.Top };

            btnReset.Location = new Point(Width - 8 - btnReset.Width, 8);
            btnExport.Location = new Point(btnReset.Left - 8 - btnExport.Width, 8);
            btnSim.Location = new Point(btnExport.Left - 8 - btnSim.Width, 8);
            btnApply.Location = new Point(btnSim.Left - 8 - btnApply.Width, 8);

            panelBottom.Controls.Add(btnApply);
            panelBottom.Controls.Add(btnSim);
            panelBottom.Controls.Add(btnExport);
            panelBottom.Controls.Add(btnReset);

            statusStrip = new StatusStrip();
            statusLabel = new ToolStripStatusLabel { Spring = true, TextAlign = ContentAlignment.MiddleLeft, Text = "Configure anti-recoil and apply." };
            statusStrip.Items.Add(statusLabel);
            statusStrip.Dock = DockStyle.Bottom;

            AcceptButton = btnApply;
            CancelButton = btnClose;
            Text = "Anti-Recoil (Compact)";
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowIcon = false;

            Controls.Add(tabs);
            Controls.Add(panelTop);
            Controls.Add(panelBottom);
            Controls.Add(statusStrip);

            ClientSize = new Size(775, 414);
        }
    }
}