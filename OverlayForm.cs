using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using WootMouseRemap.Core;
using WootMouseRemap.Input;
using WootMouseRemap.Features;
using WootMouseRemap.Security;
using WootMouseRemap.UI;

namespace WootMouseRemap
{
    /// <summary>
    /// Updated overlay form that composes core services and wires them together:
    /// - ProfileManager
    /// - StickMapper
    /// - AntiRecoil
    /// - RawInputService
    /// - Xbox360ControllerWrapper
    /// - TrayManager
    /// The form registers itself with SecureFormAccess so other UI dialogs can locate it.
    /// </summary>
    public sealed partial class OverlayForm : Form
    {
        private const string BackgroundImageFile = "sinon_bunny_fixed - Copy.png";
        private Image? _bgImage;

        // Core components
        private readonly RawInputService _rawService = new();
        private readonly Xbox360ControllerWrapper _pad = new();
        private readonly StickMapper _mapper = new();
    private readonly ProfileManager _profiles = new();
    private readonly WootMouseRemap.Core.Services.ProfileService? _profileService;
        private readonly AntiRecoil _antiRecoil = new();
    // ModeManager/ModeService live outside the UI project's compiled set in this workspace; avoid direct dependency here.

        // UI helpers
        private readonly System.Windows.Forms.Timer _uiRefresh = new() { Interval = 33 };
        private readonly System.Windows.Forms.Timer _updateTimer = new() { Interval = 16 };
        private TrayManager? _trayManager;

        // UI state
        private volatile string _status = string.Empty;
        private volatile bool _disposedFlag;
        private volatile bool _overlayHidden;
        private volatile bool _enabled = false;
        private DateTime _lastMouseMove = DateTime.UtcNow;

        // Controls
        private GroupBox? _settingsGroup;
        private CheckBox? _enableCheckBox;
        private TrackBar? _strengthTrack;
        private NumericUpDown? _delayNumeric;

    public ProfileManager ProfileManager => _profiles;
    public WootMouseRemap.Core.Services.ProfileService? ProfileService => _profileService;
        public StickMapper StickMapper => _mapper;
        public AntiRecoil AntiRecoil => _antiRecoil;

        public OverlayForm()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint, true);

            InitializeComponent();

            // Register overlay for secure access by other dialogs
            try { SecureFormAccess.RegisterOverlayForm(this); } catch { }

            // ModeService is initialized elsewhere in the application; overlay will query minimal status via available APIs.

            // Wire raw input
            _rawService.Attach(this, complianceMode: false, allowBackgroundCapture: true);
            _rawService.MouseEvent += OnRawMouseEvent;
            _rawService.KeyboardEvent += OnRawKeyboardEvent;

            // Timers
            _uiRefresh.Tick += (_, __) => Invalidate();
            _updateTimer.Tick += OnUpdateTick;

            // Resolve ProfileService from app-wide DI if available; keep ProfileManager for compatibility
            try
            {
                var provider = Program.AppServices;
                if (provider != null)
                {
                    try { _profileService = provider.GetService(typeof(WootMouseRemap.Core.Services.ProfileService)) as WootMouseRemap.Core.Services.ProfileService; } catch { _profileService = null; }
                }
                else
                {
                    _profileService = null;
                }
            }
            catch
            {
                _profileService = null;
            }

            // Tray manager
            try
            {
                _trayManager = new TrayManager(this, _antiRecoil, _pad);
                _trayManager.ShowConfigRequested += OnShowConfigRequested;
                _trayManager.ExitRequested += OnExitRequested;
            }
            catch (Exception ex) { Logger.Error("Failed to initialize TrayManager", ex); }

            // Connect virtual pad
            try { _pad.Connect(); } catch (Exception ex) { Logger.Error("Pad connect failed on startup", ex); }

            // Subscribe to AntiRecoil events to reflect status in overlay
            try
            {
                _antiRecoil.SettingsChanged += () => { UpdateStatus(); RefreshControlValues(); };
                _antiRecoil.CompensationApplied += (dx, dy) => { UpdateStatus(); };
                _antiRecoil.PatternListChanged += () => { /* no-op for overlay */ };
            }
            catch { }

            // Initialize UI state
            _status = _pad.IsConnected ? "Ready - Press F1 to enable" : "ViGEm driver not found - install ViGEm";

            LoadBackgroundAndResize();
            RepositionSettingsGroup();
        }

        protected override void WndProc(ref Message m)
        {
            if (_rawService.HandleMessage(ref m)) return;
            base.WndProc(ref m);
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            // Hotkeys (kept small and deterministic)
            switch (keyData)
            {
                case Keys.F1:
                    ToggleEnabled();
                    return true;
                case Keys.Oem5:
                    ToggleOverlay();
                    return true;
                case Keys.F9:
                    _antiRecoil.Enabled = !_antiRecoil.Enabled; UpdateStatus(); RefreshControlValues(); return true;
                case Keys.F10:
                    _antiRecoil.Strength = Math.Max(0f, _antiRecoil.Strength - 0.1f); UpdateStatus(); RefreshControlValues(); return true;
                case Keys.F11:
                    _antiRecoil.Strength = Math.Min(1f, _antiRecoil.Strength + 0.1f); UpdateStatus(); RefreshControlValues(); return true;
                case Keys.F12:
                    _antiRecoil.Strength = 0.5f; _antiRecoil.ActivationDelayMs = 50; UpdateStatus(); RefreshControlValues(); return true;
            }
            return base.ProcessCmdKey(ref msg, keyData);
        }

        private void OnRawMouseEvent(RawMouseEvent e)
        {
            try
            {
                if (!_enabled) return;

                // Mouse movement -> apply anti-recoil and map to right stick
                if (e.DeltaX != 0 || e.DeltaY != 0)
                {
                    _lastMouseMove = DateTime.UtcNow;
                    float pdx = e.DeltaX, pdy = e.DeltaY;
                    (pdx, pdy) = _antiRecoil.ProcessMouseMovement(pdx, pdy);
                    var (x, y) = _mapper.MouseToRightStick((int)pdx, (int)pdy);
                    _pad.SetRightStick(x, y);
                }

                // Buttons -> map via ProfileManager
                var leftDown = e.ButtonsDown.HasFlag(MouseButtons.Left);
                if (leftDown && _profiles.Current?.MouseMap?.TryGetValue(MouseInput.Left, out var control) == true)
                {
                    ExecuteControl(control, true);
                    ExecuteControl(control, false);
                }
            }
            catch (Exception ex)
            {
                Logger.Error("Error processing RawMouseEvent", ex);
            }
        }

        private void OnRawKeyboardEvent(RawKeyboardEvent e)
        {
            try
            {
                if (!e.IsBreak && e.VKey == (int)Keys.F1) { ToggleEnabled(); return; }
                if (!e.IsBreak && (e.VKey == (int)Keys.Oem5 || e.VKey == 0xDC)) { ToggleOverlay(); return; }

                if (!_enabled) return;

                // WASD -> left stick
                if (_profiles.Current?.WasdToLeftStick == true && (e.VKey == (int)Keys.W || e.VKey == (int)Keys.A || e.VKey == (int)Keys.S || e.VKey == (int)Keys.D))
                {
                    _mapper.UpdateKey(e.VKey, !e.IsBreak);
                    var (lx, ly) = _mapper.WasdToLeftStick();
                    _pad.SetLeftStick(lx, ly);
                    return;
                }

                // Key -> control mapping
                if (!e.IsBreak && _profiles.Current?.KeyMap?.TryGetValue(e.VKey, out var control) == true)
                {
                    ExecuteControl(control, true);
                }
            }
            catch (Exception ex)
            {
                Logger.Error("Error processing RawKeyboardEvent", ex);
            }
        }

        private void ExecuteControl(Xbox360Control control, bool down)
        {
            try
            {
                if (TryConvertToXbox360Button(control, out var btn)) _pad.SetButton(btn, down);
                else
                {
                    switch (control)
                    {
                        case Xbox360Control.LeftTrigger: _pad.SetTrigger(false, down ? (byte)255 : (byte)0); break;
                        case Xbox360Control.RightTrigger: _pad.SetTrigger(true, down ? (byte)255 : (byte)0); break;
                    }
                }
            }
            catch (Exception ex) { Logger.Error("Error executing control", ex); }
        }

        private void ToggleEnabled()
        {
            _enabled = !_enabled;
            if (_enabled)
            {
                _status = "ENABLED - Mouse/Keyboard → Controller (RawInput)";
            }
            else
            {
                try { _pad.ResetAll(); } catch { }
                _status = "DISABLED - Press F1 to enable";
            }
            Invalidate();
        }

        private void ToggleOverlay()
        {
            if (_overlayHidden)
            {
                Show(); _overlayHidden = false; TopMost = true; BringToFront();
            }
            else
            {
                Hide(); _overlayHidden = true;
            }
        }

        private static bool TryConvertToXbox360Button(Xbox360Control control, out Nefarius.ViGEm.Client.Targets.Xbox360.Xbox360Button button)
        {
            switch (control)
            {
                case Xbox360Control.A: button = Nefarius.ViGEm.Client.Targets.Xbox360.Xbox360Button.A; return true;
                case Xbox360Control.B: button = Nefarius.ViGEm.Client.Targets.Xbox360.Xbox360Button.B; return true;
                case Xbox360Control.X: button = Nefarius.ViGEm.Client.Targets.Xbox360.Xbox360Button.X; return true;
                case Xbox360Control.Y: button = Nefarius.ViGEm.Client.Targets.Xbox360.Xbox360Button.Y; return true;
                case Xbox360Control.LeftBumper: button = Nefarius.ViGEm.Client.Targets.Xbox360.Xbox360Button.LeftShoulder; return true;
                case Xbox360Control.RightBumper: button = Nefarius.ViGEm.Client.Targets.Xbox360.Xbox360Button.RightShoulder; return true;
                case Xbox360Control.Start: button = Nefarius.ViGEm.Client.Targets.Xbox360.Xbox360Button.Start; return true;
                case Xbox360Control.Back: button = Nefarius.ViGEm.Client.Targets.Xbox360.Xbox360Button.Back; return true;
                case Xbox360Control.LeftStick: button = Nefarius.ViGEm.Client.Targets.Xbox360.Xbox360Button.LeftThumb; return true;
                case Xbox360Control.RightStick: button = Nefarius.ViGEm.Client.Targets.Xbox360.Xbox360Button.RightThumb; return true;
                case Xbox360Control.DpadUp: button = Nefarius.ViGEm.Client.Targets.Xbox360.Xbox360Button.Up; return true;
                case Xbox360Control.DpadDown: button = Nefarius.ViGEm.Client.Targets.Xbox360.Xbox360Button.Down; return true;
                case Xbox360Control.DpadLeft: button = Nefarius.ViGEm.Client.Targets.Xbox360.Xbox360Button.Left; return true;
                case Xbox360Control.DpadRight: button = Nefarius.ViGEm.Client.Targets.Xbox360.Xbox360Button.Right; return true;
                default: button = Nefarius.ViGEm.Client.Targets.Xbox360.Xbox360Button.A; return false;
            }
        }

        private void OnUpdateTick(object? sender, EventArgs e)
        {
            try
            {
                if (!_enabled) return;
                var idleMs = (DateTime.UtcNow - _lastMouseMove).TotalMilliseconds;
                if (idleMs > 18)
                {
                    _pad.SetRightStick(0, 0);
                    _mapper.Curve.ResetSmoothing();
                }
            }
            catch (Exception ex) { Logger.Error("Error in update tick", ex); }
        }

        private void LoadBackgroundAndResize()
        {
            try
            {
                string localPath = Path.Combine(AppContext.BaseDirectory, BackgroundImageFile);
                // Only load a background image if it's present in the application directory. Avoid copying from user folders.
                if (File.Exists(localPath))
                {
                    try
                    {
                        _bgImage = Image.FromFile(localPath);
                        ClientSize = new Size(_bgImage.Width, _bgImage.Height);
                    }
                    catch (Exception ex)
                    {
                        Logger.Warn("Background load failed to read image: {Message}", ex.Message);
                    }
                }
            }
            catch (Exception ex) { Logger.Warn("Background load failed: {Message}", ex.Message); }
        }

        private void RepositionSettingsGroup()
        {
            if (_settingsGroup == null) return;
            int margin = 10;
            _settingsGroup.Location = new Point(ClientSize.Width - _settingsGroup.Width - margin, margin);
        }

        private void InitializeComponent()
        {
            SuspendLayout();
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(775, 414);
            FormBorderStyle = FormBorderStyle.FixedToolWindow;
            MaximizeBox = false; MinimizeBox = false;
            Name = "OverlayForm"; ShowInTaskbar = false; StartPosition = FormStartPosition.Manual;
            TopMost = true; Location = new Point(100, 100); BackColor = Color.Black;
            KeyPreview = true; // Enable form to receive key events
            AddAntiRecoilControls();
            ResumeLayout(false);
        }

        private void AddAntiRecoilControls()
        {
            var grp = new GroupBox { Text = "Anti-Recoil", Size = new Size(140, 240), ForeColor = Color.White };
            var chk = new CheckBox { Text = "Enable", Location = new Point(10, 20), AutoSize = true, ForeColor = Color.White, Checked = _antiRecoil.Enabled };
            chk.CheckedChanged += (_, __) => { _antiRecoil.Enabled = chk.Checked; UpdateStatus(); };
            var lblS = new Label { Text = "Strength:", Location = new Point(10, 50), AutoSize = true, ForeColor = Color.White };
            var track = new TrackBar { Location = new Point(10, 66), Size = new Size(120, 40), Minimum = 0, Maximum = 100, Value = (int)(_antiRecoil.Strength * 100), TickFrequency = 20 };
            track.ValueChanged += (_, __) => { _antiRecoil.Strength = track.Value / 100f; UpdateStatus(); };
            var lblD = new Label { Text = "Delay (ms):", Location = new Point(10, 113), AutoSize = true, ForeColor = Color.White };
            var num = new NumericUpDown { Location = new Point(10, 130), Size = new Size(120, 23), Minimum = 0, Maximum = 500, Value = _antiRecoil.ActivationDelayMs, Increment = 10 };
            num.ValueChanged += (_, __) => { _antiRecoil.ActivationDelayMs = (int)num.Value; UpdateStatus(); };

                    var btnAntiRecoil = new Button { Text = "Advanced", Location = new Point(10, 160), Size = new Size(120, 25), ForeColor = Color.Black };
                    btnAntiRecoil.Click += (_, __) => { try { OpenAdvancedAntiRecoilForm(); } catch (Exception ex) { Logger.Error("Error opening anti-recoil form", ex); } };

                    var btnMouse = new Button { Text = "Mouse", Location = new Point(10, 190), Size = new Size(120, 25), ForeColor = Color.Black };
                    btnMouse.Click += (_, __) => { try { OpenAdvancedMouseSettingsForm(); } catch (Exception ex) { Logger.Error("Error opening mouse settings form", ex); } };

            _enableCheckBox = chk; _strengthTrack = track; _delayNumeric = num; _settingsGroup = grp;
            grp.Controls.AddRange(new Control[] { chk, lblS, track, lblD, num, btnAntiRecoil, btnMouse });
            Controls.Add(grp);
        }

        private void RefreshControlValues()
        {
            if (_enableCheckBox != null) _enableCheckBox.Checked = _antiRecoil.Enabled;
            if (_strengthTrack != null) { int v = (int)(_antiRecoil.Strength * 100); if (v >= _strengthTrack.Minimum && v <= _strengthTrack.Maximum) _strengthTrack.Value = v; }
            if (_delayNumeric != null) _delayNumeric.Value = Math.Clamp(_antiRecoil.ActivationDelayMs, (int)_delayNumeric.Minimum, (int)_delayNumeric.Maximum);
        }

        private void UpdateStatus()
        {
            try
            {
                var mode = "N/A";
                _status = _enabled ? $"ENABLED | Mode: {mode} | Anti-Recoil: {_antiRecoil.GetStatusInfo()}" : "DISABLED - Press F1 to enable";
                Invalidate();
            }
            catch { }
        }

        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);
            try
            {
                RepositionSettingsGroup();
                _uiRefresh.Start();
                _updateTimer.Start();

                if (!_pad.IsConnected)
                {
                    _pad.Connect();
                }
            }
            catch (Exception ex)
            {
                Logger.Error("Startup error", ex);
                MessageBox.Show(this, ex.Message, "Startup Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            RepositionSettingsGroup();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            var g = e.Graphics;
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;

            if (_bgImage != null)
            {
                g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                g.DrawImage(_bgImage, new Rectangle(0, 0, ClientSize.Width, ClientSize.Height));
            }

            int panelWidth = Math.Min(360, Math.Max(260, ClientSize.Width / 2));
            var panelRect = new Rectangle(12, 12, panelWidth, 120);
            using var panelBg = new SolidBrush(Color.FromArgb(150, 15, 15, 15));
            using var borderPen = new Pen(Color.FromArgb(190, 70, 130, 255), 1.2f);
            g.FillRectangle(panelBg, panelRect);
            g.DrawRectangle(borderPen, panelRect);

            using var fontSmall = new Font(Font.FontFamily, Font.Size * .85f);
            using var fontTiny = new Font(Font.FontFamily, Font.Size * .70f);
            int x = panelRect.X + 10; int y = panelRect.Y + 10; int lh = (int)fontSmall.GetHeight(g) + 3;

            var statusColor = _enabled ? Brushes.LimeGreen : Brushes.Gray;
            g.DrawString(_status, fontSmall, statusColor, x, y); y += lh;
            g.DrawString($"ViGEm: {(_pad.IsConnected ? "OK" : "NO")}", fontSmall, Brushes.White, x, y); y += lh;

            if (_enabled)
            {
                var antiStatus = _antiRecoil.GetStatusInfo();
                g.DrawString($"Anti-Recoil: {antiStatus}", fontSmall, _antiRecoil.Enabled ? Brushes.LimeGreen : Brushes.Gray, x, y); y += lh;
                if (_antiRecoil.Enabled && _antiRecoil.IsActive)
                {
                    g.DrawString($"Active (compensation: {_antiRecoil.LastAppliedCompensation:F2})", fontTiny, Brushes.Orange, x, y);
                }
            }

            g.DrawString("F1 Enable/Disable  F9 Toggle Anti-Recoil  \\\\ Show/Hide", fontTiny, Brushes.LightSteelBlue, x, panelRect.Bottom - 20);
        }

        private void OnShowConfigRequested(object? sender, EventArgs e)
        {
            try { using var configForm = new AntiRecoilConfigForm(_antiRecoil); configForm.ShowDialog(this); } catch (Exception ex) { Logger.Error("Error showing configuration form", ex); }
        }

        private void OnExitRequested(object? sender, EventArgs e)
        {
            try { _trayManager?.Dispose(); Application.Exit(); } catch (Exception ex) { Logger.Error("Error during exit request", ex); Application.Exit(); }
        }

        private void OpenAdvancedAntiRecoilForm()
        {
            var type = AppDomain.CurrentDomain.GetAssemblies()
                .SelectMany(a => { try { return a.GetTypes(); } catch { return Array.Empty<Type>(); } })
                .FirstOrDefault(t => t.FullName == "WootMouseRemap.UI.AdvancedAntiRecoilOverlayCompactForm");

            if (type == null)
            {
                using var form = new UI.AdvancedAntiRecoilOverlayCompactForm();
                form.ShowDialog(this);
                return;
            }

            try
            {
                var ctor = type.GetConstructor(new Type[] { typeof(WootMouseRemap.Features.AntiRecoil) });
                Form? dlg = null;
                if (ctor != null)
                {
                    dlg = ctor.Invoke(new object[] { _antiRecoil }) as Form;
                }
                else
                {
                    ctor = type.GetConstructor(Type.EmptyTypes);
                    if (ctor != null)
                        dlg = ctor.Invoke(Array.Empty<object>()) as Form;
                }

                if (dlg != null)
                {
                    using (dlg)
                    {
                        dlg.ShowDialog(this);
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Error("Failed to open AdvancedAntiRecoil form via reflection", ex);
            }
        }

        private void OpenAdvancedMouseSettingsForm()
        {
            var type = AppDomain.CurrentDomain.GetAssemblies()
                .SelectMany(a => { try { return a.GetTypes(); } catch { return Array.Empty<Type>(); } })
                .FirstOrDefault(t => t.FullName == "WootMouseRemap.UI.AdvancedMouseSettingsCompactForm");

            if (type == null)
            {
                using var form = new UI.AdvancedMouseSettingsCompactForm();
                form.ShowDialog(this);
                return;
            }

            try
            {
                var ctor = type.GetConstructor(Type.EmptyTypes);
                Form? dlg = null;
                if (ctor != null)
                    dlg = ctor.Invoke(Array.Empty<object>()) as Form;

                if (dlg != null)
                {
                    using (dlg)
                    {
                        dlg.ShowDialog(this);
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Error("Failed to open AdvancedMouseSettings form via reflection", ex);
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (_disposedFlag) return;
            _disposedFlag = true;

            if (disposing)
            {
                try { SecureFormAccess.UnregisterOverlayForm(); } catch { }
                try { _uiRefresh.Stop(); } catch { }
                try { _updateTimer.Stop(); } catch { }
                try { _pad.ResetAll(); } catch { }
                _bgImage?.Dispose();
                _trayManager?.Dispose();
                _uiRefresh.Dispose();
                _updateTimer.Dispose();
                _rawService.Dispose();
                // ModeService / ModeManager not owned here - no dispose
                try { _antiRecoil.Dispose(); } catch { }
                try { _pad.Dispose(); } catch { }
            }
            base.Dispose(disposing);
        }
    }
}