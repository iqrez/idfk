using System;
using System.Windows.Forms;

namespace WootMouseRemap
{
    public sealed class RawInputMsgWindow : NativeWindow, IDisposable
    {
        public event EventHandler<Message>? RawMessage;

        public RawInputMsgWindow()
        {
            CreateParams cp = new()
            {
                Caption = "WootMouseRemapHidden",
                X = 0, Y = 0, Width = 0, Height = 0, Style = 0
            };
            CreateHandle(cp);
        }

        protected override void WndProc(ref Message m)
        {
            RawMessage?.Invoke(this, m);
            base.WndProc(ref m);
        }

        public void Dispose()
        {
            if (Handle != IntPtr.Zero) DestroyHandle();
            GC.SuppressFinalize(this);
        }
    }
}
