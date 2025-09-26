using System;
using System.Drawing;
using System.Windows.Forms;

namespace WootMouseRemap.UI
{
    public partial class AdvancedMouseSettingsCompactForm : Form
    {
        private static readonly Size FallbackClientSize = new Size(775, 414);

        public AdvancedMouseSettingsCompactForm()
        {
            InitializeComponent();
            StartPosition = FormStartPosition.CenterParent;
            AutoScaleMode = AutoScaleMode.Dpi;
            ApplyOverlayClientSizeOrFallback();
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
                var overlay = Application.OpenForms["OverlayForm"] as Form;
                if (overlay?.BackgroundImage is Image img2 && img2.Width > 100 && img2.Height > 100)
                    target = img2.Size;
            }

            var wa = Screen.FromControl(this).WorkingArea;
            target = new Size(Math.Min(target.Width, wa.Width), Math.Min(target.Height, wa.Height));

            if (ClientSize != target)
                ClientSize = target;
        }
    }
}
