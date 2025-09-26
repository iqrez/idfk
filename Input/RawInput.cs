using System;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace WootMouseRemap
{
    public sealed class RawInput : IDisposable
    {
        private readonly RawInputMsgWindow _msgWin;
        public event Action<int, bool>? KeyboardEvent;         // vk, isDown
        public event Action<MouseInput, bool>? MouseButton;    // button, isDown
        public event Action<int, int>? MouseMove;              // dx, dy
        public event Action<int>? MouseWheel;                  // delta
        private bool _registered;

        public RawInput(RawInputMsgWindow msgWindow)
        {
            _msgWin = msgWindow;
            _msgWin.RawMessage += OnWndMessage;
        }

        public void Register()
        {
            if (_registered) return;

            RAWINPUTDEVICE[] rid = new RAWINPUTDEVICE[2];
            rid[0] = new RAWINPUTDEVICE { usUsagePage = 0x01, usUsage = 0x06, dwFlags = RIDEV_INPUTSINK, hwndTarget = _msgWin.Handle }; // keyboard
            rid[1] = new RAWINPUTDEVICE { usUsagePage = 0x01, usUsage = 0x02, dwFlags = RIDEV_INPUTSINK, hwndTarget = _msgWin.Handle }; // mouse

            if (!RegisterRawInputDevices(rid, (uint)rid.Length, (uint)Marshal.SizeOf<RAWINPUTDEVICE>()))
                throw new InvalidOperationException("RegisterRawInputDevices failed: " + Marshal.GetLastPInvokeError());

            _registered = true;
        }

        public void Unregister()
        {
            if (!_registered) return;

            try
            {
                RAWINPUTDEVICE[] rid = new RAWINPUTDEVICE[2];
                rid[0] = new RAWINPUTDEVICE { usUsagePage = 0x01, usUsage = 0x06, dwFlags = RIDEV_REMOVE, hwndTarget = IntPtr.Zero };
                rid[1] = new RAWINPUTDEVICE { usUsagePage = 0x01, usUsage = 0x02, dwFlags = RIDEV_REMOVE, hwndTarget = IntPtr.Zero };

                RegisterRawInputDevices(rid, (uint)rid.Length, (uint)Marshal.SizeOf<RAWINPUTDEVICE>());
                _registered = false;
            }
            catch (Exception ex)
            {
                Logger.Error("Error unregistering raw input devices", ex);
            }
        }

        private void OnWndMessage(object? sender, Message m)
        {
            if (m.Msg == WM_INPUT) ProcessRawInput(m.LParam);
        }

        private unsafe void ProcessRawInput(IntPtr hRawInput)
        {
            uint size = 0;
            GetRawInputData(hRawInput, RID_INPUT, IntPtr.Zero, ref size, (uint)Marshal.SizeOf<RAWINPUTHEADER>());
            if (size == 0) return;

            byte[] buffer = new byte[size];
            fixed (byte* p = buffer)
            {
                if (GetRawInputData(hRawInput, RID_INPUT, (IntPtr)p, ref size, (uint)Marshal.SizeOf<RAWINPUTHEADER>()) != size) return;

                IntPtr ptr = (IntPtr)p;
                var header = Marshal.PtrToStructure<RAWINPUTHEADER>(ptr);
                IntPtr dataPtr = ptr + Marshal.SizeOf<RAWINPUTHEADER>();

                if (header.dwType == RIM_TYPEKEYBOARD)
                {
                    var kb = Marshal.PtrToStructure<RAWKEYBOARD>(dataPtr);
                    bool isDown = kb.Message == WM_KEYDOWN || kb.Message == WM_SYSKEYDOWN;
                    bool isUp = kb.Message == WM_KEYUP || kb.Message == WM_SYSKEYUP;
                    if (isDown) KeyboardEvent?.Invoke(kb.VKey, true);
                    else if (isUp) KeyboardEvent?.Invoke(kb.VKey, false);
                }
                else if (header.dwType == RIM_TYPEMOUSE)
                {
                    var mouse = Marshal.PtrToStructure<RAWMOUSE>(dataPtr);

                    if ((mouse.usButtonFlags & RI_MOUSE_BUTTON_1_DOWN) != 0) { Logger.Info("RawInput: Left mouse down"); MouseButton?.Invoke(MouseInput.Left, true); }
                    if ((mouse.usButtonFlags & RI_MOUSE_BUTTON_1_UP) != 0) { Logger.Info("RawInput: Left mouse up"); MouseButton?.Invoke(MouseInput.Left, false); }
                    if ((mouse.usButtonFlags & RI_MOUSE_BUTTON_2_DOWN) != 0) { Logger.Info("RawInput: Right mouse down"); MouseButton?.Invoke(MouseInput.Right, true); }
                    if ((mouse.usButtonFlags & RI_MOUSE_BUTTON_2_UP) != 0) { Logger.Info("RawInput: Right mouse up"); MouseButton?.Invoke(MouseInput.Right, false); }
                    if ((mouse.usButtonFlags & RI_MOUSE_BUTTON_3_DOWN) != 0) { Logger.Info("RawInput: Middle mouse down"); MouseButton?.Invoke(MouseInput.Middle, true); }
                    if ((mouse.usButtonFlags & RI_MOUSE_BUTTON_3_UP) != 0) { Logger.Info("RawInput: Middle mouse up"); MouseButton?.Invoke(MouseInput.Middle, false); }
                    if ((mouse.usButtonFlags & RI_MOUSE_BUTTON_4_DOWN) != 0) { Logger.Info("RawInput: XButton1 down"); MouseButton?.Invoke(MouseInput.XButton1, true); }
                    if ((mouse.usButtonFlags & RI_MOUSE_BUTTON_4_UP) != 0) { Logger.Info("RawInput: XButton1 up"); MouseButton?.Invoke(MouseInput.XButton1, false); }
                    if ((mouse.usButtonFlags & RI_MOUSE_BUTTON_5_DOWN) != 0) { Logger.Info("RawInput: XButton2 down"); MouseButton?.Invoke(MouseInput.XButton2, true); }
                    if ((mouse.usButtonFlags & RI_MOUSE_BUTTON_5_UP) != 0) { Logger.Info("RawInput: XButton2 up"); MouseButton?.Invoke(MouseInput.XButton2, false); }

                    if ((mouse.usButtonFlags & RI_MOUSE_WHEEL) != 0)
                        MouseWheel?.Invoke((short)mouse.usButtonData);

                    int dx = mouse.lLastX, dy = mouse.lLastY;
                    if (dx != 0 || dy != 0)
                    {
                        try { Logger.Info("RawInput dx={Dx} dy={Dy}", dx, dy); } catch { }
                        MouseMove?.Invoke(dx, dy);
                    }
                }
            }
        }

        public void Dispose() 
        { 
            Unregister();
            _msgWin.RawMessage -= OnWndMessage; 
        }

        private const int WM_INPUT = 0x00FF;
        private const uint RID_INPUT = 0x10000003;
        private const int RIM_TYPEMOUSE = 0;
        private const int RIM_TYPEKEYBOARD = 1;

        private const int WM_KEYDOWN = 0x0100;
        private const int WM_KEYUP = 0x0101;
        private const int WM_SYSKEYDOWN = 0x0104;
        private const int WM_SYSKEYUP = 0x0105;

        private const int RIDEV_INPUTSINK = 0x00000100;
        private const int RIDEV_REMOVE = 0x00000001;

        private const int RI_MOUSE_WHEEL = 0x0400;
        private const int RI_MOUSE_BUTTON_1_DOWN = 0x0001;
        private const int RI_MOUSE_BUTTON_1_UP = 0x0002;
        private const int RI_MOUSE_BUTTON_2_DOWN = 0x0004;
        private const int RI_MOUSE_BUTTON_2_UP = 0x0008;
        private const int RI_MOUSE_BUTTON_3_DOWN = 0x0010;
        private const int RI_MOUSE_BUTTON_3_UP = 0x0020;
        private const int RI_MOUSE_BUTTON_4_DOWN = 0x0040;
        private const int RI_MOUSE_BUTTON_4_UP = 0x0080;
        private const int RI_MOUSE_BUTTON_5_DOWN = 0x0100;
        private const int RI_MOUSE_BUTTON_5_UP = 0x0200;

        [StructLayout(LayoutKind.Sequential)]
        private struct RAWINPUTDEVICE { public ushort usUsagePage, usUsage; public int dwFlags; public IntPtr hwndTarget; }
        [StructLayout(LayoutKind.Sequential)]
        private struct RAWINPUTHEADER { public uint dwType, dwSize; public IntPtr hDevice, wParam; }
        [StructLayout(LayoutKind.Sequential)]
        private struct RAWMOUSE
        {
            public ushort usFlags;
            public uint ulButtons;
            public ushort usButtonFlags;
            public ushort usButtonData;
            public uint ulRawButtons; // missing in original layout - required for correct offsets on x86/x64
            public int lLastX;
            public int lLastY;
            public uint ulExtraInformation;
        }
        [StructLayout(LayoutKind.Sequential)]
        private struct RAWKEYBOARD
        {
            public ushort MakeCode, Flags, Reserved, VKey;
            public uint Message, ExtraInformation;
        }

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool RegisterRawInputDevices([In] RAWINPUTDEVICE[] pRawInputDevices, uint uiNumDevices, uint cbSize);
        [DllImport("user32.dll", SetLastError = true)]
        private static extern uint GetRawInputData(IntPtr hRawInput, uint uiCommand, IntPtr pData, ref uint pcbSize, uint cbSizeHeader);
    }
}
