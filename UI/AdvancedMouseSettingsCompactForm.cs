using System;
using System.Drawing;
using System.Windows.Forms;
using WootMouseRemap.Input;
using WootMouseRemap.Security;

namespace WootMouseRemap.UI
{
    public partial class AdvancedMouseSettingsCompactForm : Form
    {
        private RawInputService _rawInputService = new();
        private int _lastDeltaX, _lastDeltaY;

        public AdvancedMouseSettingsCompactForm()
        {
            InitializeComponent();
            StartPosition = FormStartPosition.CenterParent;
            AutoScaleMode = AutoScaleMode.Dpi;
            ApplyOverlayClientSizeOrFallback();
            btnAntiRecoilSettings.Click += BtnAntiRecoilSettings_Click;

            // Initialize RawInput for testing
            _rawInputService.Attach(this, complianceMode: true, allowBackgroundCapture: false);
            _rawInputService.MouseEvent += OnRawMouseEvent;
        }

        private void OnRawMouseEvent(RawMouseEvent evt)
        {
            _lastDeltaX = evt.DeltaX;
            _lastDeltaY = evt.DeltaY;

            // Update UI on UI thread
            if (InvokeRequired)
            {
                BeginInvoke(() => UpdateTestDisplay());
            }
            else
            {
                UpdateTestDisplay();
            }
        }

        private void UpdateTestDisplay()
        {
            if (lblDeltaValue != null)
            {
                lblDeltaValue.Text = $"{_lastDeltaX} / {_lastDeltaY}";
            }
        }

        private void BtnAntiRecoilSettings_Click(object? sender, EventArgs e)
        {
            try
            {
                Hide();
                using var antiRecoilForm = new AdvancedAntiRecoilOverlayCompactForm();
                antiRecoilForm.ShowDialog(this);
                Show();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error opening anti-recoil settings: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
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
            target = new Size(Math.Min(target.Width, wa.Width), Math.Min(target.Height, wa.Height));

            if (ClientSize != target)
                ClientSize = target;
        }

        protected override void WndProc(ref Message m)
        {
            if (!_rawInputService.HandleMessage(ref m))
            {
                base.WndProc(ref m);
            }
        }
    }
}