using System;
using System.Drawing;
using System.Windows.Forms;
using WootMouseRemap.Features;
using WootMouseRemap.Security;

namespace WootMouseRemap.UI
{
    public partial class AdvancedAntiRecoilOverlayCompactForm : Form
    {
        private static readonly Size FallbackClientSize = new Size(775, 414);
        private readonly AntiRecoil _antiRecoil;

        public AdvancedAntiRecoilOverlayCompactForm(AntiRecoil antiRecoil)
        {
            _antiRecoil = antiRecoil ?? throw new ArgumentNullException(nameof(antiRecoil));
            InitializeComponent();
            StartPosition = FormStartPosition.CenterParent;
            AutoScaleMode = AutoScaleMode.Dpi;
            ApplyOverlayClientSizeOrFallback();
            LoadCurrentSettings();
            WireUpEventHandlers();
            btnMouseSettings.Click += BtnMouseSettings_Click;
        }

        private void LoadCurrentSettings()
        {
            try
            {
                chkEnable.Checked = _antiRecoil.Enabled;
                tbStrength.Value = (int)(_antiRecoil.Strength * 100);
                numStrength.Value = (decimal)(_antiRecoil.Strength * 100);
                numActivationDelay.Value = _antiRecoil.ActivationDelayMs;
                numVertThreshold.Value = (decimal)_antiRecoil.VerticalThreshold;
                numHorizComp.Value = (decimal)(_antiRecoil.HorizontalCompensation * 100);
                chkAdaptiveComp.Checked = _antiRecoil.AdaptiveCompensation;
                numMaxPerTick.Value = (decimal)_antiRecoil.MaxTickCompensation;
                numMaxTotal.Value = (decimal)_antiRecoil.MaxTotalCompensation;
                numCooldown.Value = _antiRecoil.CooldownMs;
                numDecay.Value = (decimal)(_antiRecoil.DecayPerMs * 1000); // Convert to % per second

                UpdateStatus("Settings loaded successfully");
            }
            catch (Exception ex)
            {
                UpdateStatus($"Error loading settings: {ex.Message}");
            }
        }

        private void WireUpEventHandlers()
        {
            chkEnable.CheckedChanged += (s, e) => _antiRecoil.Enabled = chkEnable.Checked;
            tbStrength.ValueChanged += (s, e) => {
                numStrength.Value = tbStrength.Value;
                _antiRecoil.Strength = tbStrength.Value / 100f;
            };
            numStrength.ValueChanged += (s, e) => {
                tbStrength.Value = (int)numStrength.Value;
                _antiRecoil.Strength = (float)numStrength.Value / 100f;
            };
            numActivationDelay.ValueChanged += (s, e) => _antiRecoil.ActivationDelayMs = (int)numActivationDelay.Value;
            numVertThreshold.ValueChanged += (s, e) => _antiRecoil.VerticalThreshold = (float)numVertThreshold.Value;
            numHorizComp.ValueChanged += (s, e) => _antiRecoil.HorizontalCompensation = (float)numHorizComp.Value / 100f;
            chkAdaptiveComp.CheckedChanged += (s, e) => _antiRecoil.AdaptiveCompensation = chkAdaptiveComp.Checked;
            numMaxPerTick.ValueChanged += (s, e) => _antiRecoil.MaxTickCompensation = (float)numMaxPerTick.Value;
            numMaxTotal.ValueChanged += (s, e) => _antiRecoil.MaxTotalCompensation = (float)numMaxTotal.Value;
            numCooldown.ValueChanged += (s, e) => _antiRecoil.CooldownMs = (int)numCooldown.Value;
            numDecay.ValueChanged += (s, e) => _antiRecoil.DecayPerMs = (float)numDecay.Value / 1000f; // Convert from % per second

            btnSave.Click += BtnSave_Click;
            btnApply.Click += BtnApply_Click;
            btnReset.Click += BtnReset_Click;
        }

        private void BtnSave_Click(object? sender, EventArgs e)
        {
            try
            {
                // Settings are already applied via event handlers, just save to disk
                _antiRecoil.SaveSettings();
                UpdateStatus("Settings saved successfully");
            }
            catch (Exception ex)
            {
                UpdateStatus($"Error saving settings: {ex.Message}");
                MessageBox.Show(this, $"Error saving settings: {ex.Message}", "Save Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void BtnApply_Click(object? sender, EventArgs e)
        {
            try
            {
                // All settings are already applied via event handlers
                UpdateStatus("Settings applied successfully");
            }
            catch (Exception ex)
            {
                UpdateStatus($"Error applying settings: {ex.Message}");
                MessageBox.Show(this, $"Error applying settings: {ex.Message}", "Apply Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void BtnReset_Click(object? sender, EventArgs e)
        {
            if (MessageBox.Show(this, "Reset all settings to defaults?", "Confirm Reset", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
                return;

            try
            {
                _antiRecoil.Strength = 0.5f;
                _antiRecoil.ActivationDelayMs = 50;
                _antiRecoil.VerticalThreshold = 2.0f;
                _antiRecoil.HorizontalCompensation = 0.0f;
                _antiRecoil.AdaptiveCompensation = false;
                _antiRecoil.MaxTickCompensation = 10.0f;
                _antiRecoil.MaxTotalCompensation = 100.0f;
                _antiRecoil.CooldownMs = 0;
                _antiRecoil.DecayPerMs = 0.0f;

                LoadCurrentSettings();
                UpdateStatus("Settings reset to defaults");
            }
            catch (Exception ex)
            {
                UpdateStatus($"Error resetting settings: {ex.Message}");
                MessageBox.Show(this, $"Error resetting settings: {ex.Message}", "Reset Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void UpdateStatus(string message)
        {
            if (statusLabel != null)
                statusLabel.Text = message;
        }

        private void BtnMouseSettings_Click(object? sender, EventArgs e)
        {
            try
            {
                Hide();
                using var mouseSettingsForm = new AdvancedMouseSettingsCompactForm();
                mouseSettingsForm.ShowDialog(this);
                Show();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error opening mouse settings: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
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
            target = new Size(Math.Min(target.Width, wa.Width), Math.Max(400, Math.Min(target.Height, wa.Height)));

            if (ClientSize != target)
                ClientSize = target;
        }
    }
}