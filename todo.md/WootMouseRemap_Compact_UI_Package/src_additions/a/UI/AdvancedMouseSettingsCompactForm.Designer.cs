using System;
using System.Drawing;
using System.Windows.Forms;

namespace WootMouseRemap.UI
{
    partial class AdvancedMouseSettingsCompactForm
    {
        private System.ComponentModel.IContainer components = null;

        private Panel panelTop;
        private Label lblTitle;
        private CheckBox chkEnableMouseRemap;
        private Button btnSave;
        private Button btnClose;

        private TabControl tabs;
        private TabPage tabAxes;
        private TabPage tabCurves;
        private TabPage tabDeadzones;
        private TabPage tabWheelButtons;
        private TabPage tabPreview;

        private TableLayoutPanel tlpAxes;
        private Label lblXSens;
        private TrackBar tbXSens;
        private NumericUpDown numXSens;
        private Label lblYSens;
        private TrackBar tbYSens;
        private NumericUpDown numYSens;
        private CheckBox chkSeparateXY;
        private CheckBox chkInvertX;
        private CheckBox chkInvertY;
        private Label lblPreset;
        private ComboBox cboPreset;
        private Button btnResetAxes;

        private TableLayoutPanel tlpCurves;
        private Label lblCurveType;
        private ComboBox cboCurveType;
        private Label lblAccelGain;
        private TrackBar tbAccelGain;
        private NumericUpDown numAccelGain;
        private Label lblAccelThresh;
        private NumericUpDown numAccelThreshMs;
        private PictureBox picCurvePreview;
        private Button btnEditCurve;

        private TableLayoutPanel tlpDeadzones;
        private Label lblInnerDead;
        private NumericUpDown numInnerDead;
        private Label lblOuterDead;
        private NumericUpDown numOuterDead;
        private Label lblNoiseGate;
        private NumericUpDown numNoiseGate;
        private Label lblSmoothingWin;
        private NumericUpDown numSmoothingWinMs;
        private Label lblSmoothingMode;
        private ComboBox cboSmoothingMode;
        private Label lblOneEuroMinCutoff;
        private NumericUpDown numOneEuroMinCutoff;
        private Label lblOneEuroBeta;
        private NumericUpDown numOneEuroBeta;

        private TableLayoutPanel tlpWheelButtons;
        private Label lblWheelMap;
        private ComboBox cboWheelMap;
        private Label lblWheelStep;
        private NumericUpDown numWheelStep;
        private CheckBox chkWheelAccel;
        private Label lblButtonMap;
        private ListView lvButtonMap;
        private ColumnHeader colBtnName;
        private ColumnHeader colBtnAction;
        private FlowLayoutPanel flpBtnMapActions;
        private Button btnAddButtonMap;
        private Button btnEditButtonMap;
        private Button btnRemoveButtonMap;
        private Label lblDpadMode;
        private ComboBox cboDpadMode;

        private TableLayoutPanel tlpPreview;
        private PictureBox picStickPreview;
        private Label lblSampleRate;
        private Label lblSampleRateValue;
        private CheckBox chkCaptureTestInput;
        private Button btnRestoreDefaults;
        private Label lblDelta;
        private Label lblDeltaValue;

        private Panel panelBottom;
        private Button btnApply;
        private Button btnExportProfile;
        private Button btnImportProfile;
        private Button btnResetAll;
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
            lblTitle = new Label { Text = "Mouse Settings", AutoSize = true, Font = new Font("Segoe UI", 10F, FontStyle.Bold), Dock = DockStyle.Left };
            chkEnableMouseRemap = new CheckBox { Text = "Enable", AutoSize = true, Dock = DockStyle.Right, Padding = new Padding(0, 4, 0, 0) };
            btnSave = new Button { Text = "Save", Dock = DockStyle.Right, Width = 80 };
            btnClose = new Button { Text = "Close", Dock = DockStyle.Right, Width = 80, DialogResult = DialogResult.Cancel };
            panelTop.Controls.Add(chkEnableMouseRemap);
            panelTop.Controls.Add(btnSave);
            panelTop.Controls.Add(btnClose);
            panelTop.Controls.Add(lblTitle);

            tabs = new TabControl { Dock = DockStyle.Fill, Padding = new Point(12, 6) };
            tabAxes = new TabPage { Text = "Axes & Sensitivity" };
            tabCurves = new TabPage { Text = "Curves & Accel" };
            tabDeadzones = new TabPage { Text = "Deadzones & Smooth" };
            tabWheelButtons = new TabPage { Text = "Wheel & Buttons" };
            tabPreview = new TabPage { Text = "Preview & Test" };
            tabs.TabPages.AddRange(new[] { tabAxes, tabCurves, tabDeadzones, tabWheelButtons, tabPreview });

            tlpAxes = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 3,
                RowCount = 8,
                Padding = new Padding(8),
            };
            tlpAxes.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 160F));
            tlpAxes.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            tlpAxes.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 80F));

            lblXSens = new Label { Text = "X Sensitivity", Anchor = AnchorStyles.Left, AutoSize = true };
            tbXSens = new TrackBar { Minimum = 1, Maximum = 500, Value = 100, TickFrequency = 25, Anchor = AnchorStyles.Left | AnchorStyles.Right };
            numXSens = new NumericUpDown { Minimum = 1, Maximum = 500, Value = 100, Anchor = AnchorStyles.Right, DecimalPlaces = 0, Increment = 1 };

            lblYSens = new Label { Text = "Y Sensitivity", Anchor = AnchorStyles.Left, AutoSize = true };
            tbYSens = new TrackBar { Minimum = 1, Maximum = 500, Value = 100, TickFrequency = 25, Anchor = AnchorStyles.Left | AnchorStyles.Right };
            numYSens = new NumericUpDown { Minimum = 1, Maximum = 500, Value = 100, Anchor = AnchorStyles.Right, DecimalPlaces = 0, Increment = 1 };

            chkSeparateXY = new CheckBox { Text = "Separate X/Y", Anchor = AnchorStyles.Left, AutoSize = true };
            chkInvertX = new CheckBox { Text = "Invert X", Anchor = AnchorStyles.Left, AutoSize = true };
            chkInvertY = new CheckBox { Text = "Invert Y", Anchor = AnchorStyles.Left, AutoSize = true };

            lblPreset = new Label { Text = "Sensitivity Preset", Anchor = AnchorStyles.Left, AutoSize = true };
            cboPreset = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Anchor = AnchorStyles.Left | AnchorStyles.Right };
            cboPreset.Items.AddRange(new object[] { "Linear 1.0", "Linear 1.5", "Expo 2.0", "Logistic A", "Custom…" });

            btnResetAxes = new Button { Text = "Reset", Anchor = AnchorStyles.Right };

            tlpAxes.Controls.Add(lblXSens, 0, 0);
            tlpAxes.Controls.Add(tbXSens, 1, 0);
            tlpAxes.Controls.Add(numXSens, 2, 0);
            tlpAxes.Controls.Add(lblYSens, 0, 1);
            tlpAxes.Controls.Add(tbYSens, 1, 1);
            tlpAxes.Controls.Add(numYSens, 2, 1);
            tlpAxes.Controls.Add(chkSeparateXY, 0, 2);
            tlpAxes.SetColumnSpan(chkSeparateXY, 2);
            tlpAxes.Controls.Add(chkInvertX, 0, 3);
            tlpAxes.Controls.Add(chkInvertY, 1, 3);
            tlpAxes.Controls.Add(lblPreset, 0, 4);
            tlpAxes.Controls.Add(cboPreset, 1, 4);
            tlpAxes.Controls.Add(btnResetAxes, 2, 4);
            tabAxes.Controls.Add(tlpAxes);

            tlpCurves = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 3,
                RowCount = 6,
                Padding = new Padding(8),
            };
            tlpCurves.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 160F));
            tlpCurves.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            tlpCurves.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 80F));

            lblCurveType = new Label { Text = "Curve", Anchor = AnchorStyles.Left, AutoSize = true };
            cboCurveType = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Anchor = AnchorStyles.Left | AnchorStyles.Right };
            cboCurveType.Items.AddRange(new object[] { "Linear", "Exponential", "Logistic", "Custom…" });

            lblAccelGain = new Label { Text = "Acceleration Gain", Anchor = AnchorStyles.Left, AutoSize = true };
            tbAccelGain = new TrackBar { Minimum = 0, Maximum = 300, Value = 0, TickFrequency = 10, Anchor = AnchorStyles.Left | AnchorStyles.Right };
            numAccelGain = new NumericUpDown { Minimum = 0, Maximum = 300, Value = 0, Anchor = AnchorStyles.Right, DecimalPlaces = 0, Increment = 1 };

            lblAccelThresh = new Label { Text = "Accel Threshold (ms)", Anchor = AnchorStyles.Left, AutoSize = true };
            numAccelThreshMs = new NumericUpDown { Minimum = 0, Maximum = 1000, Value = 0, Anchor = AnchorStyles.Left, DecimalPlaces = 0, Increment = 5 };

            picCurvePreview = new PictureBox
            {
                BorderStyle = BorderStyle.FixedSingle,
                SizeMode = PictureBoxSizeMode.StretchImage,
                Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Top,
                Height = 150
            };
            btnEditCurve = new Button { Text = "Edit…", Anchor = AnchorStyles.Right };

            tlpCurves.Controls.Add(lblCurveType, 0, 0);
            tlpCurves.Controls.Add(cboCurveType, 1, 0);
            tlpCurves.Controls.Add(btnEditCurve, 2, 0);
            tlpCurves.Controls.Add(lblAccelGain, 0, 1);
            tlpCurves.Controls.Add(tbAccelGain, 1, 1);
            tlpCurves.Controls.Add(numAccelGain, 2, 1);
            tlpCurves.Controls.Add(lblAccelThresh, 0, 2);
            tlpCurves.Controls.Add(numAccelThreshMs, 1, 2);
            tlpCurves.Controls.Add(picCurvePreview, 0, 3);
            tlpCurves.SetColumnSpan(picCurvePreview, 3);
            tabCurves.Controls.Add(tlpCurves);

            tlpDeadzones = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 8,
                Padding = new Padding(8),
            };
            tlpDeadzones.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 230F));
            tlpDeadzones.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));

            lblInnerDead = new Label { Text = "Inner Deadzone (%)", Anchor = AnchorStyles.Left, AutoSize = true };
            numInnerDead = new NumericUpDown { Minimum = 0, Maximum = 50, Value = 0, Anchor = AnchorStyles.Left, DecimalPlaces = 0 };
            lblOuterDead = new Label { Text = "Outer Deadzone (%)", Anchor = AnchorStyles.Left, AutoSize = true };
            numOuterDead = new NumericUpDown { Minimum = 0, Maximum = 50, Value = 0, Anchor = AnchorStyles.Left, DecimalPlaces = 0 };
            lblNoiseGate = new Label { Text = "Noise Gate", Anchor = AnchorStyles.Left, AutoSize = true };
            numNoiseGate = new NumericUpDown { Minimum = 0, Maximum = 50, Value = 0, Anchor = AnchorStyles.Left, DecimalPlaces = 0 };
            lblSmoothingWin = new Label { Text = "Smoothing Window (ms)", Anchor = AnchorStyles.Left, AutoSize = true };
            numSmoothingWinMs = new NumericUpDown { Minimum = 0, Maximum = 200, Value = 0, Anchor = AnchorStyles.Left };
            lblSmoothingMode = new Label { Text = "Smoothing Mode", Anchor = AnchorStyles.Left, AutoSize = true };
            cboSmoothingMode = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Anchor = AnchorStyles.Left | AnchorStyles.Right };
            cboSmoothingMode.Items.AddRange(new object[] { "Moving Average", "One Euro" });
            lblOneEuroMinCutoff = new Label { Text = "OneEuro MinCutoff", Anchor = AnchorStyles.Left, AutoSize = true };
            numOneEuroMinCutoff = new NumericUpDown { Minimum = 0, Maximum = 50, DecimalPlaces = 2, Increment = 0.05M, Value = 1.00M, Anchor = AnchorStyles.Left };
            lblOneEuroBeta = new Label { Text = "OneEuro Beta", Anchor = AnchorStyles.Left, AutoSize = true };
            numOneEuroBeta = new NumericUpDown { Minimum = 0, Maximum = 5, DecimalPlaces = 2, Increment = 0.05M, Value = 0.30M, Anchor = AnchorStyles.Left };

            tlpDeadzones.Controls.Add(lblInnerDead, 0, 0);
            tlpDeadzones.Controls.Add(numInnerDead, 1, 0);
            tlpDeadzones.Controls.Add(lblOuterDead, 0, 1);
            tlpDeadzones.Controls.Add(numOuterDead, 1, 1);
            tlpDeadzones.Controls.Add(lblNoiseGate, 0, 2);
            tlpDeadzones.Controls.Add(numNoiseGate, 1, 2);
            tlpDeadzones.Controls.Add(lblSmoothingWin, 0, 3);
            tlpDeadzones.Controls.Add(numSmoothingWinMs, 1, 3);
            tlpDeadzones.Controls.Add(lblSmoothingMode, 0, 4);
            tlpDeadzones.Controls.Add(cboSmoothingMode, 1, 4);
            tlpDeadzones.Controls.Add(lblOneEuroMinCutoff, 0, 5);
            tlpDeadzones.Controls.Add(numOneEuroMinCutoff, 1, 5);
            tlpDeadzones.Controls.Add(lblOneEuroBeta, 0, 6);
            tlpDeadzones.Controls.Add(numOneEuroBeta, 1, 6);
            tabDeadzones.Controls.Add(tlpDeadzones);

            tlpWheelButtons = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 8,
                Padding = new Padding(8),
            };
            tlpWheelButtons.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 230F));
            tlpWheelButtons.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));

            lblWheelMap = new Label { Text = "Wheel → Trigger", Anchor = AnchorStyles.Left, AutoSize = true };
            cboWheelMap = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Anchor = AnchorStyles.Left | AnchorStyles.Right };
            cboWheelMap.Items.AddRange(new object[] { "Off", "Left Trigger", "Right Trigger", "Both Triggers" });
            lblWheelStep = new Label { Text = "Wheel Step Size", Anchor = AnchorStyles.Left, AutoSize = true };
            numWheelStep = new NumericUpDown { Minimum = 1, Maximum = 120, Value = 15, Anchor = AnchorStyles.Left };
            chkWheelAccel = new CheckBox { Text = "Wheel Acceleration", Anchor = AnchorStyles.Left, AutoSize = true };
            lblButtonMap = new Label { Text = "Button Map", Anchor = AnchorStyles.Left, AutoSize = true };
            lvButtonMap = new ListView { View = View.Details, FullRowSelect = true, HideSelection = false, Dock = DockStyle.Fill };
            colBtnName = new ColumnHeader { Text = "Input", Width = 140 };
            colBtnAction = new ColumnHeader { Text = "Action", Width = 220 };
            lvButtonMap.Columns.AddRange(new[] { colBtnName, colBtnAction });
            flpBtnMapActions = new FlowLayoutPanel { FlowDirection = FlowDirection.LeftToRight, Dock = DockStyle.Fill, AutoSize = true, WrapContents = false };
            btnAddButtonMap = new Button { Text = "Add", Width = 80 };
            btnEditButtonMap = new Button { Text = "Edit", Width = 80 };
            btnRemoveButtonMap = new Button { Text = "Remove", Width = 80 };
            flpBtnMapActions.Controls.AddRange(new Control[] { btnAddButtonMap, btnEditButtonMap, btnRemoveButtonMap });
            lblDpadMode = new Label { Text = "D-pad Emulation", Anchor = AnchorStyles.Left, AutoSize = true };
            cboDpadMode = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Anchor = AnchorStyles.Left | AnchorStyles.Right };
            cboDpadMode.Items.AddRange(new object[] { "Off", "8-way", "4-way (Cardinal)", "4-way (Diagonal)" });

            tlpWheelButtons.Controls.Add(lblWheelMap, 0, 0);
            tlpWheelButtons.Controls.Add(cboWheelMap, 1, 0);
            tlpWheelButtons.Controls.Add(lblWheelStep, 0, 1);
            tlpWheelButtons.Controls.Add(numWheelStep, 1, 1);
            tlpWheelButtons.Controls.Add(chkWheelAccel, 1, 2);
            tlpWheelButtons.Controls.Add(lblButtonMap, 0, 3);
            tlpWheelButtons.SetColumnSpan(lblButtonMap, 2);
            tlpWheelButtons.Controls.Add(lvButtonMap, 0, 4);
            tlpWheelButtons.SetColumnSpan(lvButtonMap, 2);
            tlpWheelButtons.Controls.Add(flpBtnMapActions, 1, 5);
            tlpWheelButtons.Controls.Add(lblDpadMode, 0, 6);
            tlpWheelButtons.Controls.Add(cboDpadMode, 1, 6);
            tabWheelButtons.Controls.Add(tlpWheelButtons);

            tlpPreview = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 5,
                Padding = new Padding(8),
            };
            tlpPreview.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 230F));
            tlpPreview.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));

            picStickPreview = new PictureBox { BorderStyle = BorderStyle.FixedSingle, SizeMode = PictureBoxSizeMode.StretchImage, Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Top, Height = 150 };
            lblSampleRate = new Label { Text = "Sample Rate (Hz)", Anchor = AnchorStyles.Left, AutoSize = true };
            lblSampleRateValue = new Label { Text = "—", Anchor = AnchorStyles.Left, AutoSize = true };
            chkCaptureTestInput = new CheckBox { Text = "Capture test input", Anchor = AnchorStyles.Left, AutoSize = true };
            btnRestoreDefaults = new Button { Text = "Restore defaults", Anchor = AnchorStyles.Right, Width = 140 };
            lblDelta = new Label { Text = "ΔX / ΔY (post-processing)", Anchor = AnchorStyles.Left, AutoSize = true };
            lblDeltaValue = new Label { Text = "0 / 0", Anchor = AnchorStyles.Left, AutoSize = true };

            tlpPreview.Controls.Add(picStickPreview, 0, 0);
            tlpPreview.SetColumnSpan(picStickPreview, 2);
            tlpPreview.Controls.Add(lblSampleRate, 0, 1);
            tlpPreview.Controls.Add(lblSampleRateValue, 1, 1);
            tlpPreview.Controls.Add(chkCaptureTestInput, 0, 2);
            tlpPreview.Controls.Add(btnRestoreDefaults, 1, 2);
            tlpPreview.Controls.Add(lblDelta, 0, 3);
            tlpPreview.Controls.Add(lblDeltaValue, 1, 3);
            tabPreview.Controls.Add(tlpPreview);

            panelBottom = new Panel { Dock = DockStyle.Bottom, Height = 48, Padding = new Padding(8, 6, 8, 2) };
            btnApply = new Button { Text = "Apply", Width = 100, Anchor = AnchorStyles.Right | AnchorStyles.Top };
            btnExportProfile = new Button { Text = "Export Profile", Width = 120, Anchor = AnchorStyles.Right | AnchorStyles.Top };
            btnImportProfile = new Button { Text = "Import Profile", Width = 120, Anchor = AnchorStyles.Right | AnchorStyles.Top };
            btnResetAll = new Button { Text = "Reset All", Width = 100, Anchor = AnchorStyles.Right | AnchorStyles.Top };

            btnResetAll.Location = new Point(Width - 8 - btnResetAll.Width, 8);
            btnImportProfile.Location = new Point(btnResetAll.Left - 8 - btnImportProfile.Width, 8);
            btnExportProfile.Location = new Point(btnImportProfile.Left - 8 - btnExportProfile.Width, 8);
            btnApply.Location = new Point(btnExportProfile.Left - 8 - btnApply.Width, 8);

            panelBottom.Controls.Add(btnApply);
            panelBottom.Controls.Add(btnExportProfile);
            panelBottom.Controls.Add(btnImportProfile);
            panelBottom.Controls.Add(btnResetAll);

            statusStrip = new StatusStrip();
            statusLabel = new ToolStripStatusLabel { Spring = true, TextAlign = ContentAlignment.MiddleLeft, Text = "Adjust settings and click Apply." };
            statusStrip.Items.Add(statusLabel);
            statusStrip.Dock = DockStyle.Bottom;

            AcceptButton = btnApply;
            CancelButton = btnClose;
            Text = "Mouse Settings (Compact)";
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
