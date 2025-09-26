using System;
using System.Drawing;
using System.Text;
using System.Windows.Forms;
using WootMouseRemap.Features;
using WootMouseRemap.UI;

namespace WootMouseRemap.UI
{
    /// <summary>
    /// Simplified tray manager for basic mouse/keyboard to controller functionality
    /// </summary>
    public sealed class TrayManager : IDisposable
    {
        private readonly NotifyIcon _notifyIcon;
        private readonly ContextMenuStrip _contextMenu;
        private readonly Form _mainForm;
        private readonly AntiRecoil _antiRecoil;
        private readonly Xbox360ControllerWrapper? _pad;

        private ToolStripMenuItem? _antiRecoilMenuItem;
        private ToolStripMenuItem? _strengthMenuItem;
        private ToolStripMenuItem? _statusMenuItem;
        private bool _disposed;

        public event EventHandler? ShowConfigRequested;
        public event EventHandler? ExitRequested;

        public TrayManager(Form mainForm, AntiRecoil antiRecoil, Xbox360ControllerWrapper? pad = null)
        {
            _mainForm = mainForm ?? throw new ArgumentNullException(nameof(mainForm));
            _antiRecoil = antiRecoil ?? throw new ArgumentNullException(nameof(antiRecoil));
            _pad = pad;

            _contextMenu = CreateContextMenu();
            _notifyIcon = new NotifyIcon
            {
                Icon = CreateTrayIcon(),
                Text = "WootMouseRemap - Mouse/Keyboard to Controller",
                ContextMenuStrip = _contextMenu,
                Visible = true
            };

            _notifyIcon.DoubleClick += OnTrayDoubleClick;
            _notifyIcon.MouseClick += OnTrayClick;

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
                return SystemIcons.Application;
            }
        }

        private ContextMenuStrip CreateContextMenu()
        {
            var menu = new ContextMenuStrip();

            // Status item (non-clickable)
            _statusMenuItem = new ToolStripMenuItem("Status: Ready")
            {
                Enabled = false
            };
            menu.Items.Add(_statusMenuItem);
            menu.Items.Add(new ToolStripSeparator());

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

            // Reconnect virtual pad
            var reconnectPadItem = new ToolStripMenuItem("Reconnect Virtual Pad");
            reconnectPadItem.Click += OnReconnectVirtualPad;
            menu.Items.Add(reconnectPadItem);

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

        private void OnToggleAntiRecoil(object? sender, EventArgs e)
        {
            try
            {
                var currentEnabled = _antiRecoil.Enabled;
                _antiRecoil.Enabled = !currentEnabled;

                var newState = currentEnabled ? "disabled" : "enabled";
                Logger.Info("Anti-recoil {NewState} via tray menu", newState);
                ShowTrayNotification("Anti-Recoil", $"Anti-recoil {newState}", ToolTipIcon.Info);

                UpdateMenuItems();
            }
            catch (Exception ex)
            {
                                Logger.Error("Error showing diagnostics: {Message}", ex.Message, ex);
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
            try
            {
                using var antiRecoilForm = new AdvancedAntiRecoilOverlayCompactForm();
                antiRecoilForm.ShowDialog(_mainForm);
            }
            catch (Exception ex)
            {
                Logger.Error("Error showing anti-recoil config: {Message}", ex.Message, ex);
                MessageBox.Show($"Error opening anti-recoil config: {ex.Message}", "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void OnShowMouseSettings(object? sender, EventArgs e)
        {
            try
            {
                using var mouseSettingsForm = new AdvancedMouseSettingsCompactForm();
                mouseSettingsForm.ShowDialog(_mainForm);
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
                var diagnostics = new StringBuilder();
                diagnostics.AppendLine("=== WootMouseRemap Diagnostics ===");
                diagnostics.AppendLine($"Anti-Recoil Status: {_antiRecoil.GetStatusInfo()}");
                diagnostics.AppendLine($"Virtual Controller: {(_pad?.IsConnected ?? false ? "Connected" : "Disconnected")}");
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
                    ShowTrayNotification("Reconnect", "Virtual pad is not available", ToolTipIcon.Warning);
                    return;
                }

                Logger.Info("Tray: user requested virtual pad reconnect");
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

                UpdateMenuItems();
            }
            catch (Exception ex)
            {
                Logger.Error("Error reconnecting virtual pad via tray: {Message}", ex.Message, ex);
                ShowTrayNotification("Error", $"Reconnect failed: {ex.Message}", ToolTipIcon.Error);
            }
        }

        private void OnExit(object? sender, EventArgs e)
        {
            ExitRequested?.Invoke(this, EventArgs.Empty);
        }

        private void UpdateMenuItems()
        {
            try
            {
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

                // Update status
                if (_statusMenuItem != null)
                {
                    _statusMenuItem.Text = $"Status: Mouse/KB→Controller | AR: {(antiRecoilEnabled ? "ON" : "OFF")}";
                }

                // Update tray icon tooltip
                string tooltip = $"WootMouseRemap - Mouse/KB→Controller | AR: {(antiRecoilEnabled ? "ON" : "OFF")}";
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