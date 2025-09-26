#pragma warning disable CS8600,CS8601,CS8602,CS8604,CS8618,CS8625
using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using WootMouseRemap.Core;
using System.Text.Json;
using WootMouseRemap.Modes;
using WootMouseRemap.Diagnostics;
using WootMouseRemap.Features;
using WootMouseRemap.UI;
using WootMouseRemap.Interop;
using WootMouseRemap.Security;
using System.Threading.Tasks;

namespace WootMouseRemap
{
    public sealed partial class OverlayForm : Form
    {
        private const string BackgroundImageFile = "sinon_bunny_fixed - Copy.png"; // target file placed in project folder
        private const string SourceAbsoluteImagePath = @"C:\Users\IQRez.YUI\Downloads\sinon_bunny_fixed - Copy.png"; // will copy from here if present
        private Image? _bgImage;

        private readonly ModeDiagnostics _modeDiagnostics = new();
        private readonly ModeStateValidator _modeValidator = new();
        private readonly ModeManager _modeManager;
        private readonly ModeService _modeService;

        private readonly InputEventHub _inputHub = new();
        private readonly HotkeyService _hotkeys = new();
        private readonly RawInputMsgWindow _rawWin = new();
        private readonly RawInput _raw; // direct raw input for routing

        private readonly Xbox360ControllerWrapper _pad = new();
        private readonly XInputPassthrough _xpass;
        private readonly StickMapper _mapper = new();
    private readonly ProfileManager _profiles = new();
    private readonly WootMouseRemap.Core.Services.ProfileService? _profileService;
        private readonly Telemetry _telemetry = new();
        private ControllerDetector _detector; // injected virtual pad reference
        private readonly AntiRecoil _antiRecoil = new();

        private readonly System.Windows.Forms.Timer _uiRefresh = new() { Interval = 33 }; // ~30 FPS repaint
        private readonly System.Windows.Forms.Timer _modeUpdate = new() { Interval = 100 }; // logic tick
        private readonly System.Windows.Forms.Timer _diagTimer = new() { Interval = 1000 }; // overlay diagnostics collector

        // Simple on-overlay diagnostic log (most recent entries shown)
        private readonly object _overlayLogLock = new();
        private readonly System.Collections.Generic.List<string> _overlayLog = new();
        private const int OverlayLogMaxEntries = 12;

        private volatile string _status = "";
        private volatile bool _disposedFlag;
        private volatile bool _overlayHidden;
        private bool _hooksSubscribed;

    private readonly MnKConvertMode _mnkConvertMode;
    private readonly ControllerPassMode _controllerPassMode;
    private readonly NativeMode _nativeMode;

    // Backwards-compatible aliases used throughout the codebase
    // Some older code refers to _outputMode/_passthroughMode; wire those to the concrete handlers
    private readonly WootMouseRemap.Modes.IModeHandler _outputMode;
    private readonly WootMouseRemap.Modes.IModeHandler _passthroughMode;

        private TrayManager? _trayManager;

        private GroupBox? _settingsGroup; // reposition after resize
        private CheckBox? _enableCheckBox; private TrackBar? _strengthTrack; private NumericUpDown? _delayNumeric;

        // Device hide settings
        private bool _autoHidePhysicalController = true; // Enable by default for consistent player slot assignment
        private string? _selectedPhysicalControllerInstanceId;
        private string? _currentlyHiddenInstanceId;
        private InputMode _lastMode; // track mode changes

        // Public properties for external access
    public ProfileManager ProfileManager => _profiles;
    public WootMouseRemap.Core.Services.ProfileService? ProfileService => _profileService;
        public StickMapper StickMapper => _mapper;

        public OverlayForm()
        {
            // Enable double buffering for smooth background rendering
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint, true);

            InitializeComponent();
            Text = "WootMouseRemap - Anti-Recoil";

            LoadBackgroundAndResize(); // set size to background image if available
            RepositionSettingsGroup();

            _raw = new RawInput(_rawWin);
            _xpass = new XInputPassthrough(_pad);
            _detector = new ControllerDetector(startIndex: 0, autoIndex: true, periodMs: 500, virtualController: _pad);
            // Initialize detector/player index from persisted profile preference
            try
            {
                int pref = -1;
                try { pref = _profiles?.Current.PreferredXInputIndex ?? -1; } catch { }
                if (pref >= 0)
                {
                    pref = Math.Clamp(pref, 0, 3);
                    try
                    {
                        // honor the saved manual preference: disable auto-indexing so manual choice is respected
                        _detector.AutoIndex = false;
                        _detector.SetIndex(pref);
                        _xpass.SetPlayerIndex(pref);
                        try { _xpass.SetAutoIndex(false); } catch { }
                        Logger.Info("Initialized passthrough player index from profile: P{PlayerIndex}", pref + 1);
                    }
                    catch { }
                }
            }
            catch { }

            _modeManager = new ModeManager(_modeDiagnostics);

            // Construct ProfileService and keep existing ProfileManager for backward compatibility
            try
            {
                // Use Microsoft.Extensions.Logging.Abstractions.NullLogger if available
                var nullLoggerType = Type.GetType("Microsoft.Extensions.Logging.Abstractions.NullLogger, Microsoft.Extensions.Logging.Abstractions");
                Microsoft.Extensions.Logging.ILoggerFactory? loggerFactory = null;
                var logger = null as Microsoft.Extensions.Logging.ILogger<WootMouseRemap.Core.Services.ProfileService>;
                try
                {
                    // Try resolved NullLogger via static Instance property if present
                    var nullLoggerGeneric = Type.GetType("Microsoft.Extensions.Logging.Abstractions.NullLogger`1, Microsoft.Extensions.Logging.Abstractions");
                    if (nullLoggerGeneric != null)
                    {
                        var closed = nullLoggerGeneric.MakeGenericType(typeof(WootMouseRemap.Core.Services.ProfileService));
                        var instanceProp = closed.GetProperty("Instance", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                        if (instanceProp != null)
                        {
                            var inst = instanceProp.GetValue(null);
                            logger = inst as Microsoft.Extensions.Logging.ILogger<WootMouseRemap.Core.Services.ProfileService>;
                        }
                    }
                }
                catch { }

                // Fallback to NullLogger via LoggerFactory if we couldn't get the generic instance
                if (logger == null)
                {
                    try
                    {
                        logger = Microsoft.Extensions.Logging.Abstractions.NullLogger<WootMouseRemap.Core.Services.ProfileService>.Instance;
                    }
                    catch { logger = null; }
                }

                try { _profileService = new WootMouseRemap.Core.Services.ProfileService(logger!, "Profiles"); } catch { _profileService = null; }
            }
            catch
            {
                _profileService = null;
            }

            // Pass a real request-mode-change callback that defers to the ModeService at invocation time.
            // We use an instance method so modes can call it even though _modeService is initialized later.
            _mnkConvertMode = new MnKConvertMode(_pad, _mapper, _profiles, _telemetry, _xpass, _modeValidator, _antiRecoil, RequestModeChange);
            _controllerPassMode = new ControllerPassMode(_xpass, _detector, RequestModeChange, _modeValidator);
            _nativeMode = new NativeMode();

            // Wire aliases so legacy references work
            // assign after concrete instances created
            // note: fields are readonly and set here in constructor
            _outputMode = _mnkConvertMode;
            _passthroughMode = _controllerPassMode;

            _modeManager.RegisterMode(_mnkConvertMode);
            _modeManager.RegisterMode(_controllerPassMode);
            _modeManager.RegisterMode(_nativeMode);

            _modeService = new ModeService("mode.json", _modeManager);
            // Track mode changes to support optional auto-hide of physical controller
            _lastMode = _modeService.CurrentMode;
            _modeService.ModeChanged += (_, __) =>
            {
                var prev = _lastMode;
                var current = _modeService.CurrentMode;
                _lastMode = current;
                HandleAutoHideOnModeChange(prev, current);
                _status = ModeStatusFormatter.Format(_modeService, $"Anti-Recoil: {_antiRecoil.GetStatusInfo()}");
                Invalidate();
            };

            _detector.ConnectionChanged += OnControllerConnectionChanged;
            _hotkeys.ToggleRequested += OnToggleRequested; // backslash
            _hotkeys.ModeToggleRequested += () => OnModeToggle(); // F1
            _hotkeys.PanicRequested += OnPanic;
            _inputHub.IdleTick += OnIdleTick;

            // Wire RawInput events for all modes
            _raw.KeyboardEvent += OnRawKey;
            _raw.MouseButton += OnRawMouseButton;
            _raw.MouseMove += OnRawMouseMove;
            _raw.MouseWheel += OnRawMouseWheel;

            SubscribeHooks();
            _uiRefresh.Tick += (_, __) => Invalidate();
            _modeUpdate.Tick += (_, __) => { if (_modeService.CurrentMode == InputMode.Native) _outputMode.Update(); else _passthroughMode.Update(); };
            _diagTimer.Tick += (_, __) => OnDiagTick();

            _modeService.InitializeFromPersistence();
            _status = ModeStatusFormatter.Format(_modeService, $"Anti-Recoil: {_antiRecoil.GetStatusInfo()}");

            // Initialize tray manager
            _trayManager = new TrayManager(this, _modeService, _antiRecoil, _pad, _xpass, _detector);
            _trayManager.ShowConfigRequested += OnShowConfigRequested;
            _trayManager.ExitRequested += OnExitRequested;

            KeyPreview = true;
            KeyDown += OnFormKeyDown;
        }

        private void OnRawKey(int vk, bool down)
        {
            try
            {
                // RawInput provides backup hotkey support that bypasses suppression
                if (down)
                {
                    // Critical hotkeys that should always work
                    if (vk == (int)Keys.F1)
                    {
                        Logger.Info("RawInput F1 detected - mode toggle");
                        OnModeToggle();
                        return;
                    }
                    else if (vk == (int)Keys.Oem5 || vk == 0xDC) // Backslash
                    {
                        Logger.Info("RawInput Backslash detected - overlay toggle");
                        OnToggleRequested();
                        return;
                    }
                    else if (vk == (int)Keys.F9)
                    {
                        _antiRecoil.Enabled = !_antiRecoil.Enabled;
                        UpdateAntiRecoilStatus();
                        RefreshControlValues();
                        return;
                    }
                    else if (vk == (int)Keys.F10)
                    {
                        _antiRecoil.Strength = Math.Max(0f, _antiRecoil.Strength - 0.1f);
                        UpdateAntiRecoilStatus();
                        RefreshControlValues();
                        return;
                    }
                    else if (vk == (int)Keys.F11)
                    {
                        _antiRecoil.Strength = Math.Min(1f, _antiRecoil.Strength + 0.1f);
                        UpdateAntiRecoilStatus();
                        RefreshControlValues();
                        return;
                    }
                    else if (vk == (int)Keys.F12)
                    {
                        _antiRecoil.Strength = 0.5f;
                        _antiRecoil.ActivationDelayMs = 50;
                        _antiRecoil.VerticalThreshold = 2.0f;
                        UpdateAntiRecoilStatus();
                        RefreshControlValues();
                        return;
                    }
                }

                // Route to current mode handler for non-hotkey processing
                if (_modeService.CurrentMode == InputMode.Native)
                {
                    _outputMode.OnKey(vk, down);
                }
                // Passthrough mode doesn't need keyboard input - physical controller handles everything
            }
            catch (Exception ex)
            {
                Logger.Error("Error in OnRawKey", ex);
            }
        }

        private void OnRawMouseButton(MouseInput btn, bool down)
        {
            try
            {
                Logger.Info("OnRawMouseButton called: btn={Button}, down={Down}", btn, down);
                // Middle mouse always toggles mode (RawInput bypasses suppression)
                if (btn == MouseInput.Middle && down)
                {
                    Logger.Info("RawInput middle mouse detected - toggling mode");
                    OnModeToggle();
                    return;
                }

                // Route to current mode handler
                if (_modeService.CurrentMode == InputMode.Native)
                {
                    _outputMode.OnMouseButton(btn, down);
                }
                // Passthrough mode doesn't need mouse input - physical controller handles everything
            }
            catch (Exception ex)
            {
                Logger.Error("Error in OnRawMouseButton", ex);
            }
        }

        private static bool ShouldPromptWarnings()
        {
            try
            {
                const string uiStateFile = "ui_state.json";

                // Path validation to prevent directory traversal attacks
                var fullPath = Path.GetFullPath(uiStateFile);
                var allowedDir = Path.GetFullPath(Directory.GetCurrentDirectory());
                if (!fullPath.StartsWith(allowedDir, StringComparison.OrdinalIgnoreCase))
                    throw new UnauthorizedAccessException("Path traversal detected");

                if (!File.Exists(uiStateFile)) return true;
                var json = File.ReadAllText(uiStateFile);
                WootMouseRemap.Core.UiStateData state;
                var options = new JsonSerializerOptions 
                { 
                    MaxDepth = 5,
                    PropertyNameCaseInsensitive = false,
                    AllowTrailingCommas = false
                };
                try 
                {
                    state = JsonSerializer.Deserialize<WootMouseRemap.Core.UiStateData>(json, options) ?? new WootMouseRemap.Core.UiStateData();
                }
                catch (JsonException) 
                {
                    state = new WootMouseRemap.Core.UiStateData(); // Safe fallback
                }
                return state?.UserPrefs?.ShowWarnings ?? true;
            }
            catch
            {
                return true;
            }
        }

        private void OnRawMouseMove(int dx, int dy)
        {
            try
            {
                try { Logger.Info("Overlay OnRawMouseMove dx={Dx} dy={Dy}", dx, dy); } catch { }
                // Route to current mode handler
                if (_modeService.CurrentMode == InputMode.Native)
                {
                    _outputMode.OnMouseMove(dx, dy);
                }
                // Passthrough mode doesn't need mouse movement - physical controller handles everything
            }
            catch (Exception ex)
            {
                Logger.Error("Error in OnRawMouseMove", ex);
            }
        }

        private void OnRawMouseWheel(int delta)
        {
            try
            {
                // Route to current mode handler
                if (_modeService.CurrentMode == InputMode.Native)
                {
                    _outputMode.OnWheel(delta);
                }
                // Passthrough mode doesn't need mouse wheel - physical controller handles everything
            }
            catch (Exception ex)
            {
                Logger.Error("Error in OnRawMouseWheel", ex);
            }
        }

        private void LoadBackgroundAndResize()
        {
            try
            {
                // Attempt runtime copy from provided absolute path if local image missing
                string localPath = Path.Combine(AppContext.BaseDirectory, BackgroundImageFile);
                if (!File.Exists(localPath) && File.Exists(SourceAbsoluteImagePath))
                {
                    try { File.Copy(SourceAbsoluteImagePath, localPath, overwrite: false); } catch (Exception copyEx) { Logger.Warn("Background copy failed: {Message}", copyEx.Message); }
                }
                if (File.Exists(localPath))
                {
                    _bgImage = Image.FromFile(localPath);
                    ClientSize = new Size(_bgImage.Width, _bgImage.Height); // exact size match
                }
            }
            catch (Exception ex)
            {
                Logger.Warn("Background load failed: {Message}", ex.Message);
            }
        }

        private void RepositionSettingsGroup()
        {
            if (_settingsGroup == null) return;
            int margin = 10;
            _settingsGroup.Location = new Point(ClientSize.Width - _settingsGroup.Width - margin, margin);
        }

        private void OnModeToggle()
        {
            try
            {
                var before = _modeService.CurrentMode;
                InputMode target;
                switch (before)
                {
                    case InputMode.Native:
                        target = InputMode.ControllerPass;
                        break;
                    case InputMode.ControllerPass:
                        target = InputMode.MnKConvert;
                        break;
                    case InputMode.MnKConvert:
                    default:
                        target = InputMode.Native;
                        break;
                }
                var success = _modeService.Switch(target);
                if (success && target == InputMode.ControllerPass)
                {
                    // Ensure passthrough uses currently detected physical controller slot
                    try
                    {
                        int idx = _detector?.Index ?? 0;
                        Logger.Info("Setting passthrough player index to detector raw index={Index} (display P{DisplaySlot})", idx, GetDisplaySlot(idx));
                        _xpass.SetPlayerIndex(idx);
                    }
                    catch (Exception ex) { Logger.Warn("Failed to set passthrough player index: {Message}", ex.Message); }
                }

                // Update status with mode-specific information
                if (success)
                {
                    string modeInfo;
                    switch (target)
                    {
                        case InputMode.ControllerPass:
                            modeInfo = $"Physical controller P{(_detector?.Index ?? 0) + 1} ? Virtual controller";
                            break;
                        case InputMode.MnKConvert:
                            modeInfo = "Pass-through (no mapping)";
                            break;
                        case InputMode.Native:
                        default:
                            modeInfo = "Mouse/Keyboard ? Virtual controller";
                            break;
                    }
                    _status = $"Mode: {target}";
                }
                else
                {
                    _status = $"Mode toggle failed (stayed {before})";
                }

                Invalidate();
                string logMessage = target switch
                {
                    InputMode.Native => "Entering Native mode",
                    InputMode.MnKConvert => "Switching to MnK Convert mode",
                    InputMode.ControllerPass => "Controller Pass mode active",
                    _ => $"Mode toggle: {before} ? {target} (success: {success})"
                };
                Logger.Info(logMessage);
            }
            catch (Exception ex)
            {
                Logger.Error("Mode toggle error", ex);
                _status = $"Mode toggle error: {ex.Message}";
                Invalidate();
            }
        }

        private void OnFormKeyDown(object? sender, KeyEventArgs e)
        {
            try
            {
                // Form-level hotkeys as backup (these work when form has focus)
                if (e.KeyCode == Keys.F1) { OnModeToggle(); e.Handled = true; }
                else if (e.KeyCode == Keys.F9) { _antiRecoil.Enabled = !_antiRecoil.Enabled; UpdateAntiRecoilStatus(); RefreshControlValues(); e.Handled = true; }
                else if (e.KeyCode == Keys.F10) { _antiRecoil.Strength = Math.Max(0f, _antiRecoil.Strength - 0.1f); UpdateAntiRecoilStatus(); RefreshControlValues(); e.Handled = true; }
                else if (e.KeyCode == Keys.F11) { _antiRecoil.Strength = Math.Min(1f, _antiRecoil.Strength + 0.1f); UpdateAntiRecoilStatus(); RefreshControlValues(); e.Handled = true; }
                else if (e.KeyCode == Keys.F12) { _antiRecoil.Strength = 0.5f; _antiRecoil.ActivationDelayMs = 50; _antiRecoil.VerticalThreshold = 2.0f; UpdateAntiRecoilStatus(); RefreshControlValues(); e.Handled = true; }
            }
            catch (Exception ex) { Logger.Error("Error handling form key input", ex); }
        }

        private void InitializeComponent()
        {
            SuspendLayout();
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(775, 414); // fallback before image load
            FormBorderStyle = FormBorderStyle.FixedToolWindow;
            MaximizeBox = false; MinimizeBox = false;
            Name = "OverlayForm"; ShowInTaskbar = false; StartPosition = FormStartPosition.Manual;
            TopMost = true; Location = new Point(100, 100); BackColor = Color.Black;
            AddAntiRecoilControls();
            ResumeLayout(false);
        }

        private void AddAntiRecoilControls()
        {
            var grp = new GroupBox { Text = "Anti-Recoil", Size = new Size(120, 270), ForeColor = Color.White };
            var chk = new CheckBox { Text = "Enable", Location = new Point(10, 20), AutoSize = true, ForeColor = Color.White, Checked = _antiRecoil.Enabled };
            chk.CheckedChanged += (_, __) => { _antiRecoil.Enabled = chk.Checked; UpdateAntiRecoilStatus(); RefreshControlValues(); };
            var lblS = new Label { Text = "Strength:", Location = new Point(10, 50), AutoSize = true, ForeColor = Color.White };
            var track = new TrackBar { Location = new Point(10, 66), Size = new Size(100, 40), Minimum = 0, Maximum = 100, Value = (int)(_antiRecoil.Strength * 100), TickFrequency = 20 };
            track.ValueChanged += (_, __) => { _antiRecoil.Strength = track.Value / 100f; UpdateAntiRecoilStatus(); };
            var lblD = new Label { Text = "Delay (ms):", Location = new Point(10, 113), AutoSize = true, ForeColor = Color.White };
            var num = new NumericUpDown { Location = new Point(10, 130), Size = new Size(100, 23), Minimum = 0, Maximum = 500, Value = _antiRecoil.ActivationDelayMs, Increment = 10 };
            num.ValueChanged += (_, __) => { _antiRecoil.ActivationDelayMs = (int)num.Value; UpdateAntiRecoilStatus(); };
            var configButton = new Button { Text = "Advanced", Location = new Point(10, 170), Size = new Size(100, 26), BackColor = Color.FromArgb(0, 122, 204), ForeColor = Color.White, FlatStyle = FlatStyle.Flat };
            configButton.Click += (_, __) => OpenAdvancedAntiRecoilConfig();
            var mouseSettingsButton = new Button { Text = "Mouse Settings", Location = new Point(10, 200), Size = new Size(100, 26), BackColor = Color.FromArgb(0, 122, 204), ForeColor = Color.White, FlatStyle = FlatStyle.Flat };
            mouseSettingsButton.Click += (_, __) => OpenMouseSettings();
            var settingsButton = new Button { Text = "Settings", Location = new Point(10, 230), Size = new Size(100, 26), BackColor = Color.FromArgb(0, 122, 204), ForeColor = Color.White, FlatStyle = FlatStyle.Flat };
            settingsButton.Click += (_, __) => OpenSettings();

            _enableCheckBox = chk; _strengthTrack = track; _delayNumeric = num; _settingsGroup = grp;
            grp.Controls.AddRange(new Control[] { chk, lblS, track, lblD, num, configButton, mouseSettingsButton, settingsButton });
            Controls.Add(grp);
        }


        private void HandleAutoHideOnModeChange(InputMode prev, InputMode current)
        {
            try
            {
                if (!_autoHidePhysicalController) return;

                // Entering MnKConvert: attempt to hide selected device automatically without prompting
                if (current == InputMode.Native)
                {
                    // If user hasn't selected a specific device, attempt an automatic match
                    if (string.IsNullOrEmpty(_selectedPhysicalControllerInstanceId))
                    {
                        var auto = DeviceHider.FindLikelyController();
                        if (auto != null)
                        {
                            // Auto-select the device silently (no prompt)
                            _selectedPhysicalControllerInstanceId = auto.InstanceId;
                            _status = $"Auto-selected device: {auto.Description}";
                            Logger.Info("Auto-selected physical controller for hide: {Description} ({InstanceId})", auto.Description, auto.InstanceId);
                            Invalidate();
                        }
                    }

                    if (!string.IsNullOrEmpty(_selectedPhysicalControllerInstanceId))
                    {
                        if (!DeviceHider.IsAdministrator())
                        {
                            // Do not show UI prompts; log and set status so user can inspect the overlay
                            Logger.Warn("Attempted to hide physical controller but application is not running as administrator");
                            _status = "Admin required to hide controller";
                            Invalidate();
                            return;
                        }

                        try
                        {
                            // Auto-hide without confirmation
                            if (DeviceHider.HideDevice(_selectedPhysicalControllerInstanceId))
                            {
                                _currentlyHiddenInstanceId = _selectedPhysicalControllerInstanceId;
                                _status = "Hidden physical controller";
                                Logger.Info("Hidden physical controller: {InstanceId}", _selectedPhysicalControllerInstanceId);
                            }
                            else
                            {
                                Logger.Warn("DeviceHider.HideDevice returned false for {InstanceId}", _selectedPhysicalControllerInstanceId);
                                _status = "Failed to hide physical controller";
                            }
                        }
                        catch (Exception ex)
                        {
                            Logger.Error("Failed to hide device", ex);
                            _status = $"Failed to hide device: {ex.Message}";
                        }

                        Invalidate();
                    }
                }

                // Leaving MnKConvert: unhide previously hidden device (automatically)
                if (prev == InputMode.Native && !string.IsNullOrEmpty(_currentlyHiddenInstanceId) && current != InputMode.Native)
                {
                    try
                    {
                        if (!DeviceHider.IsAdministrator())
                        {
                            Logger.Warn("Unhiding devices requires administrative privileges but application is not running as admin");
                            _status = "Admin required to unhide controller";
                            Invalidate();
                            return;
                        }

                        if (DeviceHider.UnhideDevice(_currentlyHiddenInstanceId))
                        {
                            _status = "Unhid physical controller";
                            Logger.Info("Unhid physical controller: {InstanceId}", _currentlyHiddenInstanceId);
                            _currentlyHiddenInstanceId = null;
                        }
                        else
                        {
                            Logger.Warn("DeviceHider.UnhideDevice returned false for {InstanceId}", _currentlyHiddenInstanceId);
                            _status = "Failed to unhide physical controller";
                        }
                    }
                    catch (Exception ex)
                    {
                        Logger.Error("Failed to unhide device", ex);
                        _status = $"Failed to unhide device: {ex.Message}";
                    }
                    Invalidate();
                }
            }
            catch (Exception ex)
            {
                Logger.Error("Error handling auto-hide on mode change", ex);
            }
        }

        private void OpenAdvancedAntiRecoilConfig()
        {
            bool wasUserHidden = _overlayHidden; bool wasVisible = Visible;
            try
            {
                if (wasVisible) Hide();
                using var configForm = new UI.AdvancedAntiRecoilOverlayCompactForm(_antiRecoil);
                if (configForm.ShowDialog(this) == DialogResult.OK)
                { RefreshControlValues(); UpdateAntiRecoilStatus(); }
            }
            catch (Exception ex)
            {
                Logger.Error("Error opening advanced anti-recoil configuration", ex);
                MessageBox.Show(this, $"Error opening configuration: {ex.Message}", "Configuration Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                if (!wasUserHidden && wasVisible && !_overlayHidden) { Show(); TopMost = true; BringToFront(); }
            }
        }

        private void OpenMouseSettings()
        {
            try
            {
                // Hide overlay for less clutter
                bool wasVisible = Visible;
                bool wasUserHidden = _overlayHidden;
                if (wasVisible && !wasUserHidden)
                {
                    Hide();
                }

                using var mouseSettingsForm = new UI.AdvancedMouseSettingsCompactForm();
                mouseSettingsForm.ShowDialog(this);
            }
            catch (Exception ex)
            {
                Logger.Error("Error opening mouse settings", ex);
                MessageBox.Show(this, $"Error opening mouse settings: {ex.Message}", "Settings Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                // Restore overlay if it was visible before
                bool wasVisible = !_overlayHidden;
                if (wasVisible)
                {
                    Show();
                    TopMost = true;
                    BringToFront();
                }
            }
        }

        private void OpenSettings()
        {
            try
            {
                // Hide overlay for less clutter
                bool wasVisible = Visible;
                bool wasUserHidden = _overlayHidden;
                if (wasVisible && !wasUserHidden)
                {
                    Hide();
                }

                UI.SettingsForm.ShowSettingsDialog(this);
            }
            catch (Exception ex)
            {
                Logger.Error("Error opening settings", ex);
                MessageBox.Show(this, $"Error opening settings: {ex.Message}", "Settings Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                // Restore overlay if it was visible before
                bool wasVisible = !_overlayHidden;
                if (wasVisible)
                {
                    Show();
                    TopMost = true;
                    BringToFront();
                }
            }
        }

        private void RefreshControlValues()
        {
            if (_enableCheckBox != null) _enableCheckBox.Checked = _antiRecoil.Enabled;
            if (_strengthTrack != null) { int v = (int)(_antiRecoil.Strength * 100); if (v >= _strengthTrack.Minimum && v <= _strengthTrack.Maximum) _strengthTrack.Value = v; }
            if (_delayNumeric != null) _delayNumeric.Value = Math.Clamp(_antiRecoil.ActivationDelayMs, (int)_delayNumeric.Minimum, (int)_delayNumeric.Maximum);
        }
        private void UpdateAntiRecoilStatus() { _status = ModeStatusFormatter.Format(_modeService, $"Anti-Recoil: {_antiRecoil.GetStatusInfo()}"); Invalidate(); }

        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);
            
            // Register this form for secure access
            SecureFormAccess.RegisterOverlayForm(this);
            try
            {
                RepositionSettingsGroup();
                _raw.Register();
                _inputHub.Start();
                _uiRefresh.Start();
                _modeUpdate.Start();
                _diagTimer.Start();

                // Start hotkeys after hooks are installed
                try
                {
                    _hotkeys.Start();
                    Logger.Info("HotkeyService started successfully");
                }
                catch (Exception hotkeyEx)
                {
                    Logger.Error("Failed to start HotkeyService", hotkeyEx);
                    _status = "Hotkey service failed to start - using RawInput backup";
                    Invalidate();
                }

                if (!_pad.IsConnected) { _pad.Connect(); if (!_pad.IsConnected) { _status = "ViGEm connect failed - install driver?"; HandleSystemErrorFallback("ViGEm driver not available"); } }
            }
            catch (Exception ex) { Logger.Error("Startup error", ex); MessageBox.Show(this, ex.Message, "Startup Error", MessageBoxButtons.OK, MessageBoxIcon.Error); }
        }

        protected override void OnResize(EventArgs e)
        { base.OnResize(e); RepositionSettingsGroup(); }

        private void OnToggleRequested()
        {
            try
            {
                // Ensure UI actions run on the UI thread
                if (InvokeRequired)
                {
                    try { BeginInvoke(new Action(OnToggleRequested)); } catch (Exception ex) { Logger.Warn("BeginInvoke OnToggleRequested failed: {Message}", ex.Message); }
                    return;
                }

                Logger.Info("OnToggleRequested invoked (overlayHidden={OverlayHidden})", _overlayHidden);

                if (_overlayHidden)
                {
                    // Show and restore overlay properly
                    ShowInTaskbar = true;
                    if (WindowState == FormWindowState.Minimized) WindowState = FormWindowState.Normal;
                    Show();
                    _overlayHidden = false;
                    // Ensure window is topmost and focused
                    TopMost = true;
                    try { Activate(); BringToFront(); Focus(); } catch { }
                    _status = "Overlay shown";
                }
                else
                {
                    // Hide overlay without stealing focus
                    _overlayHidden = true;
                    try { TopMost = false; } catch { }
                    Hide();
                    ShowInTaskbar = false;
                    _status = "Overlay hidden";
                }

                Invalidate();
            }
            catch (Exception ex)
            {
                Logger.Error("Overlay hide/show toggle error", ex);
            }
        }

        private void OnPanic() { try { LowLevelHooks.Suppress = false; _pad.ResetAll(); _status = "PANIC: suppression off, pad reset"; } catch (Exception ex) { Logger.Error("Panic error", ex); _status = "PANIC error: " + ex.Message; } finally { Invalidate(); } }
        private void OnIdleTick() { try { _pad.ZeroRightStick(); _pad.ResetSmoothing(); } catch (Exception ex) { Logger.Error("IdleTick", ex); } }

        private string PhysicalControllerStatus()
        {
            try
            {
                int rawIndex = _detector?.Index ?? -1;
                int display = rawIndex >= 0 ? rawIndex + 1 : -1;
                if (rawIndex < 0) return "Phys:None";
                return $"Phys:P{display}";
            }
            catch { return "Phys:?"; }
        }

        // Helper to format detector index for human-readable display (1-based)
        private static int GetDisplaySlot(int rawIndex) => rawIndex >= 0 ? rawIndex + 1 : -1;

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            var g = e.Graphics; g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            // Draw background image (already sized to form). If null, keep black.
            if (_bgImage != null)
            {
                g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                g.DrawImage(_bgImage, new Rectangle(0, 0, ClientSize.Width, ClientSize.Height));
            }

            // Info panel now extended with physical controller status
            int panelWidth = Math.Min(360, Math.Max(260, ClientSize.Width / 2));
            var panelRect = new Rectangle(12, 12, panelWidth, 190);
            using var panelBg = new SolidBrush(Color.FromArgb(150, 15, 15, 15));
            using var borderPen = new Pen(Color.FromArgb(190, 70, 130, 255), 1.2f);
            g.FillRectangle(panelBg, panelRect); g.DrawRectangle(borderPen, panelRect);
            using var fontSmall = new Font(Font.FontFamily, Font.Size * .85f);
            using var fontTiny = new Font(Font.FontFamily, Font.Size * .70f);
            int x = panelRect.X + 10; int y = panelRect.Y + 10; int lh = (int)fontSmall.GetHeight(g) + 3;

            // Mode and connection status
            string modeText;
            switch (_modeService.CurrentMode)
            {
                case InputMode.ControllerPass:
                    modeText = "Mode: Controller Pass";
                    break;
                case InputMode.MnKConvert:
                    modeText = "Mode: MnK Convert";
                    break;
                case InputMode.Native:
                default:
                    modeText = "Mode: Native";
                    break;
            }
            g.DrawString($"{modeText} | ViGEm:{(_pad.IsConnected ? "OK" : "NO")}", fontSmall, Brushes.White, x, y); y += lh;
            int rawIdx = _detector?.Index ?? -1;
            int disp = GetDisplaySlot(rawIdx);
            var slotText = rawIdx >= 0 ? $"{PhysicalControllerStatus()} (Idx:{rawIdx} Slot:{disp})" : PhysicalControllerStatus();
            g.DrawString(slotText, fontSmall, Brushes.White, x, y); y += lh;
            g.DrawString(_status, fontSmall, Brushes.LightGray, x, y); y += lh;

            // Anti-recoil status (only relevant in output mode)
            if (_modeService.CurrentMode == InputMode.Native)
            {
                var antiStatus = _antiRecoil.GetStatusInfo();
                g.DrawString($"Anti-Recoil: {antiStatus}", fontSmall, _antiRecoil.Enabled ? Brushes.LimeGreen : Brushes.Gray, x, y); y += lh;
                g.DrawString($"Strength {_antiRecoil.Strength:P0}  Delay {_antiRecoil.ActivationDelayMs}ms", fontSmall, Brushes.White, x, y); y += lh;
                if (_antiRecoil.Enabled && _antiRecoil.IsActive) { g.DrawString($"Active (tick:{_antiRecoil.LastAppliedCompensation:F2})", fontTiny, Brushes.Orange, x, y); y += (int)fontTiny.GetHeight(g) + 2; }
            }
            else if (_modeService.CurrentMode == InputMode.ControllerPass)
            {
                g.DrawString("Physical controller ? Virtual controller", fontSmall, Brushes.LimeGreen, x, y); y += lh;
                y += lh; // Skip anti-recoil line
            }
            else if (_modeService.CurrentMode == InputMode.MnKConvert)
            {
                g.DrawString("Pass-through mode (no controller mapping)", fontSmall, Brushes.Cyan, x, y); y += lh;
                y += lh; // Skip anti-recoil line
            }

            g.DrawString("F9 Toggle  F10-/F11+  F12 Reset  F1 Mode  \\ Show", fontTiny, Brushes.LightSteelBlue, x, y);

            // Anti-recoil strength bar (only in output mode)
            if (_modeService.CurrentMode == InputMode.Native && _antiRecoil.Enabled)
            {
                var bar = new Rectangle(panelRect.Right - 18, panelRect.Top + 18, 10, 80);
                g.DrawRectangle(Pens.White, bar);
                int h = (int)(bar.Height * _antiRecoil.Strength);
                var fill = new Rectangle(bar.X + 1, bar.Bottom - h + 1, bar.Width - 2, h - 2);
                using var fillBr = new SolidBrush(Color.LimeGreen);
                g.FillRectangle(fillBr, fill);
            }

            // Draw overlay diagnostics log bottom-left
            try
            {
                var logFont = new Font(Font.FontFamily, Font.Size * 0.75f);
                int logX = 12;
                int logY = ClientSize.Height - 12;
                int lineHeight = (int)logFont.GetHeight(g) + 2;

                lock (_overlayLogLock)
                {
                    for (int i = _overlayLog.Count - 1; i >= 0; i--)
                    {
                        var line = _overlayLog[i];
                        logY -= lineHeight;
                        g.DrawString(line, logFont, Brushes.LightGray, logX, logY);
                    }
                }
                logFont.Dispose();
            }
            catch { }
        }

        private void OnControllerConnectionChanged(bool connected, int index)
        {
            try
            {
                Logger.Info("ControllerConnectionChanged: connected={Connected}, rawIndex={RawIndex}, display=P{DisplaySlot}", connected, index, GetDisplaySlot(index));
                _xpass.SetPlayerIndex(index);
                if (connected) _modeManager.OnControllerConnected(index); else _modeManager.OnControllerDisconnected(index);

                // Smart auto-switching: switch to ControllerPass when controller connects in Native mode
                if (connected && _modeService.CurrentMode == InputMode.Native)
                {
                    Logger.Info("Switching to Controller Pass mode due to controller connection");
                    _modeService.Switch(InputMode.ControllerPass);
                    _status = $"Auto-switched to Controller Pass (controller P{GetDisplaySlot(index)} connected)";
                }
                else
                {
                    _status = connected ? $"Controller P{GetDisplaySlot(index)} connected" : "Controller disconnected";
                }

                Invalidate();
            }
            catch (Exception ex) { Logger.Error("Controller connection change handling", ex); }
        }

        private void OnShowConfigRequested(object? sender, EventArgs e)
        {
            try
            {
                // Show the anti-recoil configuration form
                using var configForm = new AntiRecoilConfigForm(_antiRecoil);
                configForm.ShowDialog(this);
            }
            catch (Exception ex)
            {
                Logger.Error("Error showing configuration form", ex);
                MessageBox.Show($"Error opening configuration: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void OnExitRequested(object? sender, EventArgs e)
        {
            try
            {
                Logger.Info("Exit requested from tray menu");

                // Ensure all resources are properly disposed
                _trayManager?.Dispose();

                // Force application exit
                Application.Exit();

                // If Application.Exit() doesn't work, use Environment.Exit as fallback
                Environment.Exit(0);
            }
            catch (Exception ex)
            {
                Logger.Error("Error during exit request", ex);
                // Force exit even if there's an error
                Environment.Exit(1);
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (_disposedFlag) return; _disposedFlag = true;
            
            // Unregister from secure access
            SecureFormAccess.UnregisterOverlayForm();
            
            if (disposing)
            {
                try { UnsubscribeHooks(); } catch { }
                try { _uiRefresh.Stop(); } catch { }
                try { _modeUpdate.Stop(); } catch { }
                try { _diagTimer.Stop(); } catch { }
                try { _raw.Unregister(); } catch { }
                _bgImage?.Dispose();
                _trayManager?.Dispose();
                _uiRefresh.Dispose(); _modeUpdate.Dispose();
                try { _diagTimer.Dispose(); } catch { }
                _raw.Dispose(); _rawWin.Dispose();
                _hotkeys.Dispose(); _inputHub.Dispose(); _modeService.Dispose(); _modeManager.Dispose(); _pad.Dispose(); _detector.Dispose(); _antiRecoil.Dispose();
            }
            base.Dispose(disposing);
        }

        private void SubscribeHooks()
        { if (_hooksSubscribed) return; try { LowLevelHooks.KeyEvent += OnLowLevelKey; LowLevelHooks.MouseButton += OnLowLevelMouseButton; _hooksSubscribed = true; } catch (Exception ex) { Logger.Error("Failed to subscribe low level hooks", ex); } }
        private void UnsubscribeHooks()
        { if (!_hooksSubscribed) return; try { LowLevelHooks.KeyEvent -= OnLowLevelKey; LowLevelHooks.MouseButton -= OnLowLevelMouseButton; _hooksSubscribed = false; } catch (Exception ex) { Logger.Error("Failed to unsubscribe low level hooks", ex); } }
        private void OnLowLevelKey(int vk, bool down) { try { if (_modeService.CurrentMode == InputMode.Native) _outputMode.OnKey(vk, down); } catch (Exception ex) { Logger.Error("LowLevel key routing error", ex); } }
        private void OnLowLevelMouseButton(MouseInput btn, bool down) {
            try {
                // Ensure middle mouse can toggle mode even via low-level hook
                if (btn == MouseInput.Middle && down)
                {
                    Logger.Info("LowLevel mouse middle detected - toggling mode");
                    // Use mode service to toggle safely on UI thread
                    if (InvokeRequired) { BeginInvoke(new Action(OnModeToggle)); }
                    else { OnModeToggle(); }
                    return;
                }

                if (_modeService.CurrentMode == InputMode.Native) _outputMode.OnMouseButton(btn, down);
            } catch (Exception ex) { Logger.Error("LowLevel mouse button routing error", ex); }
        }

        private void HandleSystemErrorFallback(string errorReason)
        {
            try
            {
                // Fallback to Native mode on system errors or virtual controller failures
                if (_modeService.CurrentMode != InputMode.Native)
                {
                    Logger.Warn("System error detected ({ErrorReason}) - falling back to Native mode", errorReason);
                    _modeService.Switch(InputMode.Native);
                    _status = $"Fallback to Native mode due to: {errorReason}";
                    Invalidate();
                }
            }
            catch (Exception ex)
            {
                Logger.Error("Error during system error fallback: {Message}", ex.Message, ex);
            }
        }

        private void OnDiagTick()
        {
            try
            {
                var ts = DateTime.Now.ToString("HH:mm:ss");
                var mode = _modeService?.CurrentMode.ToString() ?? "?";
                var vigem = _pad?.IsConnected ?? false;
                var passthrough = _xpass?.IsRunning ?? false;
                var phys = _detector?.Connected ?? false;
                var line = $"{ts} | Mode:{mode} | ViGEm:{(vigem?"OK":"NO")}" +
                           $" | Passthrough:{(passthrough?"RUN":"STOP")}" +
                           $" | Phys:{(phys?"Yes":"No")} | {_status}";

                lock (_overlayLogLock)
                {
                    _overlayLog.Add(line);
                    if (_overlayLog.Count > OverlayLogMaxEntries) _overlayLog.RemoveAt(0);
                }
                Invalidate();
            }
            catch (Exception ex)
            {
                Logger.Error("Error in OnDiagTick", ex);
            }
        }
    }
}
#pragma warning restore CS8600,CS8601,CS8602,CS8604,CS8618,CS8625