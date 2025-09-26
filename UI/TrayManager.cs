using System;
using System.Drawing;
using System.Text;
using System.IO;
using System.Text.Json;
using System.Windows.Forms;
using WootMouseRemap.Core;
using WootMouseRemap.Features;
using WootMouseRemap.UI;

namespace WootMouseRemap.UI
{
    /// <summary>
    /// Manages the system tray icon and context menu for WootMouseRemap
    /// </summary>
    public sealed class TrayManager : IDisposable
    {
        private readonly NotifyIcon _notifyIcon;
        private readonly ContextMenuStrip _contextMenu;
        private readonly Form _mainForm;
        private readonly ModeService _modeService;
        private readonly AntiRecoil _antiRecoil;
        private readonly Xbox360ControllerWrapper? _pad;
        private readonly XInputPassthrough? _xpass;
        private readonly ControllerDetector? _detector;

        private ToolStripMenuItem? _modeMenuItem;
        private ToolStripMenuItem? _antiRecoilMenuItem;
        private ToolStripMenuItem? _strengthMenuItem;
    private ToolStripMenuItem? _showWarningsItem;
        private ToolStripMenuItem? _statusMenuItem;
        private ToolStripMenuItem? _passthroughMenuItem;
        private ToolStripMenuItem? _passthroughAutoItem;
        private ToolStripMenuItem? _passthroughMapP0;
        private ToolStripMenuItem? _passthroughMapP1;
        private ToolStripMenuItem? _passthroughMapP2;
        private ToolStripMenuItem? _passthroughMapP3;
        private System.Windows.Forms.Timer? _refreshTimer;
        private bool _disposed;

        public event EventHandler? ShowConfigRequested;
        public event EventHandler? ExitRequested;

        public TrayManager(Form mainForm, ModeService modeService, AntiRecoil antiRecoil,
            Xbox360ControllerWrapper? pad = null, XInputPassthrough? xpass = null, ControllerDetector? detector = null)
        {
            _mainForm = mainForm ?? throw new ArgumentNullException(nameof(mainForm));
            _modeService = modeService ?? throw new ArgumentNullException(nameof(modeService));
            _antiRecoil = antiRecoil ?? throw new ArgumentNullException(nameof(antiRecoil));
            _pad = pad;
            _xpass = xpass;
            _detector = detector;

            _contextMenu = CreateContextMenu();
            _notifyIcon = new NotifyIcon
            {
                Icon = CreateTrayIcon(),
                Text = "WootMouseRemap - Anti-Recoil",
                ContextMenuStrip = _contextMenu,
                Visible = true
            };

            _notifyIcon.DoubleClick += OnTrayDoubleClick;
            _notifyIcon.MouseClick += OnTrayClick;

            _modeService.ModeChanged += (oldMode, newMode) => OnModeChanged();
            if (_detector != null) _detector.ConnectionChanged += (c, i) => UpdateMenuItems();

            // Periodic refresh so tooltip shows recent active controller info
            _refreshTimer = new System.Windows.Forms.Timer { Interval = 1000 };
            _refreshTimer.Tick += (_, __) => UpdateMenuItems();
            _refreshTimer.Start();

            UpdateMenuItems();
            Logger.Info("TrayManager initialized");
        }

        private Icon CreateTrayIcon()
        {
            try
            {
                using var bitmap = new Bitmap(16, 16);
                using var graphics = Graphics.FromImage(bitmap);

                // Draw a simple gamepad-like icon
                graphics.Clear(Color.Transparent);
                graphics.FillRectangle(Brushes.DarkBlue, 2, 6, 12, 6);
                graphics.FillEllipse(Brushes.Blue, 1, 5, 4, 4);
                graphics.FillEllipse(Brushes.Blue, 11, 5, 4, 4);
                graphics.FillEllipse(Brushes.White, 4, 7, 2, 2);
                graphics.FillEllipse(Brushes.White, 10, 7, 2, 2);

                var icon = Icon.FromHandle(bitmap.GetHicon());
                return icon;
            }
            catch
            {
                // Fallback to default application icon
                return SystemIcons.Application;
            }
        }

        private ContextMenuStrip CreateContextMenu()
        {
            var menu = new ContextMenuStrip();

            // Status item (non-clickable)
            _statusMenuItem = new ToolStripMenuItem("Status: Initializing...")
            {
                Enabled = false
            };
            menu.Items.Add(_statusMenuItem);
            menu.Items.Add(new ToolStripSeparator());

            // Mode selection
            _modeMenuItem = new ToolStripMenuItem("Current Mode: Loading...");
            var mnkConvertModeItem = new ToolStripMenuItem("MnK Convert")
            {
                Tag = InputMode.MnKConvert,
                ToolTipText = "Convert mouse & keyboard to virtual controller"
            };
            mnkConvertModeItem.Click += OnModeMenuClick;

            var nativeModeItem = new ToolStripMenuItem("Native")
            {
                Tag = InputMode.Native,
                ToolTipText = "Direct mouse and keyboard input - no conversion"
            };
            nativeModeItem.Click += OnModeMenuClick;

            var controllerPassModeItem = new ToolStripMenuItem("Controller Pass")
            {
                Tag = InputMode.ControllerPass,
                ToolTipText = "Physical controller passthrough to games"
            };
            controllerPassModeItem.Click += OnModeMenuClick;

            _modeMenuItem.DropDownItems.AddRange(new ToolStripItem[] { mnkConvertModeItem, nativeModeItem, controllerPassModeItem });
            menu.Items.Add(_modeMenuItem);

            // Passthrough control (manual mapping / auto-select)
            if (_detector != null)
            {
                _passthroughMenuItem = new ToolStripMenuItem("Passthrough Mapping");

                _passthroughAutoItem = new ToolStripMenuItem("Auto-Select Active Controller") { CheckOnClick = true };
                _passthroughAutoItem.Click += OnPassthroughAutoToggle;
                _passthroughMenuItem.DropDownItems.Add(_passthroughAutoItem);

                _passthroughMenuItem.DropDownItems.Add(new ToolStripSeparator());

                _passthroughMapP0 = new ToolStripMenuItem("Map to P1") { Tag = 0 };
                _passthroughMapP1 = new ToolStripMenuItem("Map to P2") { Tag = 1 };
                _passthroughMapP2 = new ToolStripMenuItem("Map to P3") { Tag = 2 };
                _passthroughMapP3 = new ToolStripMenuItem("Map to P4") { Tag = 3 };

                _passthroughMapP0.Click += OnPassthroughMapClick;
                _passthroughMapP1.Click += OnPassthroughMapClick;
                _passthroughMapP2.Click += OnPassthroughMapClick;
                _passthroughMapP3.Click += OnPassthroughMapClick;

                _passthroughMenuItem.DropDownItems.AddRange(new ToolStripItem[] { _passthroughMapP0, _passthroughMapP1, _passthroughMapP2, _passthroughMapP3 });

                menu.Items.Add(_passthroughMenuItem);
            }

            // Anti-recoil controls
            _antiRecoilMenuItem = new ToolStripMenuItem("Anti-Recoil");

            var enableAntiRecoilItem = new ToolStripMenuItem("Enable Anti-Recoil")
            {
                CheckOnClick = true
            };
            enableAntiRecoilItem.Click += OnToggleAntiRecoil;

            _strengthMenuItem = new ToolStripMenuItem("Strength: 0%");
            var strength25Item = new ToolStripMenuItem("25%") { Tag = 0.25f };
            var strength50Item = new ToolStripMenuItem("50%") { Tag = 0.50f };
            var strength75Item = new ToolStripMenuItem("75%") { Tag = 0.75f };
            var strength100Item = new ToolStripMenuItem("100%") { Tag = 1.0f };

            strength25Item.Click += OnStrengthMenuClick;
            strength50Item.Click += OnStrengthMenuClick;
            strength75Item.Click += OnStrengthMenuClick;
            strength100Item.Click += OnStrengthMenuClick;

            _strengthMenuItem.DropDownItems.AddRange(new ToolStripItem[]
            {
                strength25Item, strength50Item, strength75Item, strength100Item
            });

            _antiRecoilMenuItem.DropDownItems.AddRange(new ToolStripItem[]
            {
                enableAntiRecoilItem,
                _strengthMenuItem
            });
            menu.Items.Add(_antiRecoilMenuItem);

            menu.Items.Add(new ToolStripSeparator());

            // Configuration
            var configItem = new ToolStripMenuItem("Open Configuration...")
            {
                Font = new Font(menu.Font, FontStyle.Bold)
            };
            configItem.Click += OnShowConfig;
            menu.Items.Add(configItem);

            // Mouse Settings
            var mouseSettingsItem = new ToolStripMenuItem("Mouse Settings...");
            mouseSettingsItem.Click += OnShowMouseSettings;
            menu.Items.Add(mouseSettingsItem);

            // Diagnostics
            var diagnosticsItem = new ToolStripMenuItem("Show Diagnostics...");
            diagnosticsItem.Click += OnShowDiagnostics;
            menu.Items.Add(diagnosticsItem);

            // Reconnect virtual pad (force ViGEm reconnect)
            var reconnectPadItem = new ToolStripMenuItem("Reconnect Virtual Pad");
            reconnectPadItem.Click += OnReconnectVirtualPad;
            menu.Items.Add(reconnectPadItem);

            // Settings dialog
            var settingsItem = new ToolStripMenuItem("Settings...");
            settingsItem.Click += OnShowSettings;
            menu.Items.Add(settingsItem);

            // Show Warnings preference (persisted in ui_state.json)
            _showWarningsItem = new ToolStripMenuItem("Show Warnings")
            {
                CheckOnClick = true,
                Checked = GetShowWarningsFromUiState()
            };
            _showWarningsItem.Click += OnShowWarningsToggle;
            menu.Items.Add(_showWarningsItem);

            // Mode Diagnostics (if components available)
            if (_pad != null && _xpass != null && _detector != null)
            {
                var modeDiagnosticsItem = new ToolStripMenuItem("Mode Diagnostics...");
                modeDiagnosticsItem.Click += OnShowModeDiagnostics;
                menu.Items.Add(modeDiagnosticsItem);
            }

            menu.Items.Add(new ToolStripSeparator());

            // Exit
            var exitItem = new ToolStripMenuItem("Exit");
            exitItem.Click += OnExit;
            menu.Items.Add(exitItem);

            return menu;
        }

        private void OnTrayDoubleClick(object? sender, EventArgs e)
        {
            ShowConfigRequested?.Invoke(this, EventArgs.Empty);
        }

        private void OnTrayClick(object? sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                // Show/hide main form on left click
                if (_mainForm.Visible)
                {
                    _mainForm.Hide();
                }
                else
                {
                    _mainForm.Show();
                    _mainForm.BringToFront();
                    _mainForm.Activate();
                }
            }
        }

        private void OnModeMenuClick(object? sender, EventArgs e)
        {
            if (sender is ToolStripMenuItem item && item.Tag is InputMode mode)
            {
                try
                {
                    _modeService.Switch(mode);
                    string logMessage = mode switch
                    {
                        InputMode.Native => "Entering Native mode",
                        InputMode.MnKConvert => "Switching to MnK Convert mode",
                        InputMode.ControllerPass => "Controller Pass mode active",
                        _ => $"Mode switched to {mode}"
                    };
                    Logger.Info("{Message} via tray menu", logMessage);
                }
                catch (Exception ex)
                {
                    Logger.Error("Error switching mode via tray: {Message}", ex.Message, ex);
                    ShowTrayNotification("Error", $"Failed to switch mode: {ex.Message}", ToolTipIcon.Error);
                }
            }
        }

        private void OnPassthroughAutoToggle(object? sender, EventArgs e)
        {
            try
            {
                if (_detector == null) return;
                if (sender is ToolStripMenuItem it)
                {
                    bool enable = it.Checked;
                    _detector.AutoIndex = enable;
                    ShowTrayNotification("Passthrough", enable ? "Auto-select enabled" : "Auto-select disabled", ToolTipIcon.Info);
                    Logger.Info("Passthrough auto-select {State} via tray", enable ? "enabled" : "disabled");
                    UpdateMenuItems();
                }
            }
            catch (Exception ex)
            {
                Logger.Error("Error toggling passthrough auto-select: {Message}", ex.Message, ex);
                ShowTrayNotification("Error", $"Failed to toggle auto-select: {ex.Message}", ToolTipIcon.Error);
            }
        }

        private void OnPassthroughMapClick(object? sender, EventArgs e)
        {
            try
            {
                if (_detector == null) return;
                if (sender is ToolStripMenuItem it && it.Tag is int idx)
                {
                    _detector.SetIndex(idx);
                    _xpass?.SetPlayerIndex(idx);
                    ShowTrayNotification("Passthrough", $"Mapped passthrough to P{idx + 1}", ToolTipIcon.Info);
                    Logger.Info("Passthrough manually mapped to P{Index} via tray", idx + 1);
                    UpdateMenuItems();
                }
            }
            catch (Exception ex)
            {
                Logger.Error("Error mapping passthrough via tray: {Message}", ex.Message, ex);
                ShowTrayNotification("Error", $"Failed to map passthrough: {ex.Message}", ToolTipIcon.Error);
            }
        }

        private void OnToggleAntiRecoil(object? sender, EventArgs e)
        {
            try
            {
                var currentEnabled = _antiRecoil.Enabled;
                _antiRecoil.Enabled = !currentEnabled;

                var newState = currentEnabled ? "disabled" : "enabled";
                Logger.Info("Anti-recoil {State} via tray menu", newState);
                ShowTrayNotification("Anti-Recoil", $"Anti-recoil {newState}", ToolTipIcon.Info);

                UpdateMenuItems();
            }
            catch (Exception ex)
            {
                Logger.Error("Error toggling anti-recoil via tray: {Message}", ex.Message, ex);
                ShowTrayNotification("Error", $"Failed to toggle anti-recoil: {ex.Message}", ToolTipIcon.Error);
            }
        }

        private void OnStrengthMenuClick(object? sender, EventArgs e)
        {
            if (sender is ToolStripMenuItem item && item.Tag is float strength)
            {
                try
                {
                    _antiRecoil.Strength = strength;
                    Logger.Info("Anti-recoil strength set to {Strength:P0} via tray menu", strength);
                    ShowTrayNotification("Anti-Recoil", $"Strength set to {strength:P0}", ToolTipIcon.Info);

                    UpdateMenuItems();
                }
                catch (Exception ex)
                {
                    Logger.Error("Error setting anti-recoil strength via tray: {Message}", ex.Message, ex);
                    ShowTrayNotification("Error", $"Failed to set strength: {ex.Message}", ToolTipIcon.Error);
                }
            }
        }

        private void OnShowConfig(object? sender, EventArgs e)
        {
            ShowConfigRequested?.Invoke(this, EventArgs.Empty);
        }

        private void OnShowMouseSettings(object? sender, EventArgs e)
        {
            try
            {
                if (_mainForm is OverlayForm overlayForm)
                {
                    // Access the ProfileManager and StickMapper from OverlayForm
                    // We'll need to expose these properties in OverlayForm
                    using var mouseSettingsForm = new AdvancedMouseSettingsForm(overlayForm.ProfileManager, overlayForm.StickMapper);
                    mouseSettingsForm.ShowDialog(_mainForm);
                }
                else
                {
                    MessageBox.Show("Mouse settings are not available in this context.", "Error",
                        MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            }
            catch (Exception ex)
            {
                Logger.Error("Error showing mouse settings: {Message}", ex.Message, ex);
                MessageBox.Show($"Error opening mouse settings: {ex.Message}", "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void OnShowDiagnostics(object? sender, EventArgs e)
        {
            try
            {
                // Show a simple diagnostics window
                var diagnostics = new StringBuilder();
                diagnostics.AppendLine("=== WootMouseRemap Diagnostics ===");
                diagnostics.AppendLine($"Current Mode: {GetModeDisplayName(_modeService.CurrentMode)}");
                diagnostics.AppendLine($"Anti-Recoil Status: {_antiRecoil.GetStatusInfo()}");
                diagnostics.AppendLine($"Virtual Controller: {(_pad?.IsConnected ?? false ? "Connected" : "Disconnected")}");
                diagnostics.AppendLine($"Physical Controller: {(_detector?.Connected ?? false ? "Detected" : "Not Detected")}");
                diagnostics.AppendLine($"XInput Passthrough: {(_xpass?.IsRunning ?? false ? "Running" : "Stopped")}");
                diagnostics.AppendLine($"Timestamp: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");

                MessageBox.Show(diagnostics.ToString(), "WootMouseRemap Diagnostics",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                Logger.Error("Error showing diagnostics: {Message}", ex.Message, ex);
            }
        }

        private void OnReconnectVirtualPad(object? sender, EventArgs e)
        {
            try
            {
                if (_pad == null)
                {
                    ShowTrayNotification("Reconnect", "Virtual pad is not available in this build", ToolTipIcon.Warning);
                    return;
                }

                Logger.Info("Tray: user requested virtual pad reconnect");
                // Attempt connect and give it a short moment
                try { _pad.Connect(); } catch (Exception ex) { Logger.Error("Error calling _pad.Connect() from tray", ex); }
                System.Threading.Thread.Sleep(250);

                if (_pad.IsConnected)
                {
                    ShowTrayNotification("Reconnect", "Virtual pad reconnected", ToolTipIcon.Info);
                    Logger.Info("Virtual pad reconnected via tray");
                }
                else
                {
                    ShowTrayNotification("Reconnect", "Virtual pad reconnect failed", ToolTipIcon.Warning);
                    Logger.Warn("Virtual pad reconnect failed via tray");
                }

                // Refresh menu items to reflect new state
                UpdateMenuItems();
            }
            catch (Exception ex)
            {
                Logger.Error("Error reconnecting virtual pad via tray: {Message}", ex.Message, ex);
                ShowTrayNotification("Error", $"Reconnect failed: {ex.Message}", ToolTipIcon.Error);
            }
        }

        private void OnShowModeDiagnostics(object? sender, EventArgs e)
        {
            try
            {
                if (_pad != null && _xpass != null && _detector != null)
                {
                    var diagnosticForm = new ModeDiagnosticForm(_modeService, _pad, _xpass, _detector, _antiRecoil);
                    diagnosticForm.ShowDialog(_mainForm);
                }
            }
            catch (Exception ex)
            {
                Logger.Error("Error showing mode diagnostics: {Message}", ex.Message, ex);
                MessageBox.Show($"Error opening mode diagnostics: {ex.Message}", "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void OnExit(object? sender, EventArgs e)
        {
            ExitRequested?.Invoke(this, EventArgs.Empty);
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

        // Read the ShowWarnings preference from ui_state.json (safe fallback to true)
        private bool GetShowWarningsFromUiState()
        {
            try
            {
                const string uiFile = "ui_state.json";
                if (!File.Exists(uiFile)) return true;
                var json = File.ReadAllText(uiFile);
                var state = JsonSerializer.Deserialize<WootMouseRemap.Core.UiStateData>(json);
                return state?.UserPrefs?.ShowWarnings ?? true;
            }
            catch (Exception ex)
            {
                Logger.Error("Failed to read ui_state.json for ShowWarnings: {Message}", ex.Message, ex);
                return true;
            }
        }

        // Persist the ShowWarnings preference to ui_state.json (creates or preserves other fields)
        private void SetShowWarningsInUiState(bool enable)
        {
            try
            {
                const string uiFile = "ui_state.json";
                WootMouseRemap.Core.UiStateData state = new WootMouseRemap.Core.UiStateData();
                if (File.Exists(uiFile))
                {
                    try
                    {
                        var json = File.ReadAllText(uiFile);
                        var des = JsonSerializer.Deserialize<WootMouseRemap.Core.UiStateData>(json);
                        if (des != null) state = des;
                    }
                    catch (Exception readEx)
                    {
                        Logger.Warn("Failed to parse existing ui_state.json, overwriting: {Message}", readEx.Message);
                    }
                }

                if (state.UserPrefs == null) state.UserPrefs = new WootMouseRemap.Core.UserPreferences();
                state.UserPrefs.ShowWarnings = enable;
                state.LastSaved = DateTime.UtcNow;

                var outJson = JsonSerializer.Serialize(state, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(uiFile, outJson);
                ShowTrayNotification("Settings", enable ? "Warnings enabled" : "Warnings disabled", ToolTipIcon.Info);
            }
            catch (Exception ex)
            {
                Logger.Error("Failed to write ui_state.json for ShowWarnings: {Message}", ex.Message, ex);
            }
        }

        private void OnShowSettings(object? sender, EventArgs e)
        {
            try
            {
                SettingsForm.ShowSettingsDialog(_mainForm);
                // Refresh the Show Warnings checkbox state after settings dialog closes
                if (_showWarningsItem != null)
                {
                    _showWarningsItem.Checked = GetShowWarningsFromUiState();
                }
            }
            catch (Exception ex)
            {
                Logger.Error("Error showing settings dialog: {Message}", ex.Message, ex);
            }
        }

        private void OnShowWarningsToggle(object? sender, EventArgs e)
        {
            if (sender is ToolStripMenuItem item)
            {
                var newVal = item.Checked;
                SetShowWarningsInUiState(newVal);
                Logger.Info("ShowWarnings set to {Value} via tray menu", newVal);
            }
        }

        private void UpdateMenuItems()
        {
            try
            {
                // Update mode display
                if (_modeMenuItem != null)
                {
                    _modeMenuItem.Text = $"Current Mode: {GetModeDisplayName(_modeService.CurrentMode)}";

                    // Update checkmarks for mode items
                    foreach (ToolStripMenuItem item in _modeMenuItem.DropDownItems)
                    {
                        if (item.Tag is InputMode mode)
                        {
                            item.Checked = mode == _modeService.CurrentMode;
                        }
                    }
                }

                // Update anti-recoil status
                var antiRecoilEnabled = _antiRecoil.Enabled;
                var antiRecoilStrength = _antiRecoil.Strength;
                if (_antiRecoilMenuItem != null && _antiRecoilMenuItem.DropDownItems.Count > 0)
                {
                    var enableItem = (ToolStripMenuItem)_antiRecoilMenuItem.DropDownItems[0];
                    enableItem.Checked = antiRecoilEnabled;
                }

                if (_strengthMenuItem != null)
                {
                    _strengthMenuItem.Text = $"Strength: {antiRecoilStrength:P0}";
                }

                // Update passthrough menu state
                if (_detector != null && _passthroughAutoItem != null)
                {
                    _passthroughAutoItem.Checked = _detector.AutoIndex;
                }

                // Update status
                if (_statusMenuItem != null)
                {
                    string modeText = GetModeDisplayName(_modeService.CurrentMode);
                    _statusMenuItem.Text = $"Mode: {modeText} | AR: {(antiRecoilEnabled ? "ON" : "OFF")}";
                }

                // Update tray icon tooltip
                string active = "";
                try
                {
                    if (_detector != null)
                    {
                        int most = _detector.GetMostRecentActiveIndex();
                        if (most >= 0) active = $" | Active:P{most + 1}";
                    }
                }
                catch { }

                // NotifyIcon.Text is limited to 63 characters on Windows; ensure we stay short
                string tooltip = $"WootMouseRemap - Mode: {GetModeDisplayName(_modeService.CurrentMode)} | AR: {(antiRecoilEnabled ? "ON" : "OFF")}{active}";
                if (tooltip.Length > 63) tooltip = tooltip.Substring(0, 63);
                _notifyIcon.Text = tooltip;
            }
            catch (Exception ex)
            {
                Logger.Error("Error updating tray menu items: {Message}", ex.Message, ex);
            }
        }

        public void ShowTrayNotification(string title, string text, ToolTipIcon icon = ToolTipIcon.Info)
        {
            try
            {
                _notifyIcon.ShowBalloonTip(3000, title, text, icon);
            }
            catch (Exception ex)
            {
                Logger.Error("Error showing tray notification: {Message}", ex.Message, ex);
            }
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            try
            {
                _modeService.ModeChanged -= (oldMode, newMode) => OnModeChanged();
                if (_detector != null) _detector.ConnectionChanged -= (c, i) => UpdateMenuItems();
                try { _refreshTimer?.Stop(); } catch { };
                try { _refreshTimer?.Dispose(); } catch { };

                _notifyIcon.Visible = false;
                _notifyIcon.Dispose();
                _contextMenu.Dispose();

                Logger.Info("TrayManager disposed");
            }
            catch (Exception ex)
            {
                Logger.Error("Error disposing TrayManager: {Message}", ex.Message, ex);
            }
        }
    }
}