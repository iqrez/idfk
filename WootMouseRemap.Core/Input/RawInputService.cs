using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace WootMouseRemap.Input
{
    public sealed class RawInputService : IDisposable
    {
        private const int WM_INPUT = 0x00FF;
        private const uint RIM_TYPEMOUSE = 0;
        private const uint RIM_TYPEKEYBOARD = 1;
        private const uint RID_INPUT = 0x10000003;

        private const uint RIDEV_REMOVE = 0x00000001;
        private const uint RIDEV_EXINPUTSINK = 0x00001000;
        private const uint RIDEV_INPUTSINK = 0x00000100;
        private const uint RIDEV_NOHOTKEYS = 0x00000200;
        private const uint RIDEV_NOLEGACY = 0x00000030;

        private IntPtr _hwnd = IntPtr.Zero;
        private bool _registered;
        private bool _disposed;

        private bool _backgroundCapture;
        private bool _suppressLegacy;

        public event Action<RawMouseEvent>? MouseEvent;
        public event Action<RawKeyboardEvent>? KeyboardEvent;

        public void Attach(Control host, bool complianceMode = true, bool allowBackgroundCapture = false)
        {
            if (host is null) throw new ArgumentNullException(nameof(host));
            EnsureNotDisposed();
            host.HandleCreated += Host_HandleCreated;
            host.HandleDestroyed += Host_HandleDestroyed;
            _backgroundCapture = allowBackgroundCapture && !complianceMode;
            _suppressLegacy = complianceMode;
            if (host.IsHandleCreated) { _hwnd = host.Handle; RegisterDevices(); }
        }

        public void Detach(Control host)
        {
            if (host is null) return;
            try
            {
                host.HandleCreated -= Host_HandleCreated;
                host.HandleDestroyed -= Host_HandleDestroyed;
                UnregisterDevices();
                _hwnd = IntPtr.Zero;
            }
            catch { }
        }

        private void Host_HandleCreated(object? sender, EventArgs e)
        {
            if (sender is Control c) { _hwnd = c.Handle; RegisterDevices(); }
        }

        private void Host_HandleDestroyed(object? sender, EventArgs e)
        {
            UnregisterDevices(); _hwnd = IntPtr.Zero;
        }

        public bool HandleMessage(ref Message m)
        {
            if (_disposed) return false;
            if (m.Msg != WM_INPUT) return false;
            return HandleRawInput(m.LParam);
        }

        private bool HandleRawInput(IntPtr lParam)
        {
            uint dwSize = 0;
            if (GetRawInputData(lParam, RID_INPUT, IntPtr.Zero, ref dwSize, (uint)Marshal.SizeOf<RAWINPUTHEADER>()) == unchecked((uint)-1))
                ThrowLastWin32("GetRawInputData(size)");
            IntPtr buffer = Marshal.AllocHGlobal((int)dwSize);
            try
            {
                uint read = GetRawInputData(lParam, RID_INPUT, buffer, ref dwSize, (uint)Marshal.SizeOf<RAWINPUTHEADER>());
                if (read == unchecked((uint)-1)) ThrowLastWin32("GetRawInputData(data)");
                var raw = Marshal.PtrToStructure<RAWINPUT>(buffer);
                switch (raw.header.dwType)
                {
                    case RIM_TYPEMOUSE: 
                        Logger.Info("RAW MOUSE EVENT: flags={Flags:X4}", ((RAWMOUSE)raw.data.mouse).usButtonFlags);
                        MouseEvent?.Invoke(RawMouseEvent.From(raw.data.mouse)); 
                        break;
                    case RIM_TYPEKEYBOARD: 
                        Logger.Info("RAW KEYBOARD EVENT: vkey={VKey}", ((RAWKEYBOARD)raw.data.keyboard).VKey);
                        KeyboardEvent?.Invoke(RawKeyboardEvent.From(raw.data.keyboard)); 
                        break;
                }
            }
            finally { Marshal.FreeHGlobal(buffer); }
            return true;
        }

        private void RegisterDevices()
        {
            if (_registered || _hwnd == IntPtr.Zero) return;
            uint mouseFlags = 0, kbFlags = 0;
            if (_backgroundCapture) { mouseFlags |= RIDEV_INPUTSINK; kbFlags |= RIDEV_INPUTSINK; }
            // Always allow background capture for hotkeys to work globally
            mouseFlags |= RIDEV_INPUTSINK; kbFlags |= RIDEV_INPUTSINK;
            var devices = new RAWINPUTDEVICE[2];
            devices[0] = new RAWINPUTDEVICE { usUsagePage = 0x01, usUsage = 0x02, dwFlags = mouseFlags, hwndTarget = _hwnd };
            devices[1] = new RAWINPUTDEVICE { usUsagePage = 0x01, usUsage = 0x06, dwFlags = kbFlags, hwndTarget = _hwnd };
            if (!RegisterRawInputDevices(devices, (uint)devices.Length, (uint)Marshal.SizeOf<RAWINPUTDEVICE>())) ThrowLastWin32("RegisterRawInputDevices");
            _registered = true;
        }

        private void UnregisterDevices()
        {
            if (!_registered) return;
            try
            {
                var devices = new RAWINPUTDEVICE[2];
                devices[0] = new RAWINPUTDEVICE { usUsagePage = 0x01, usUsage = 0x02, dwFlags = RIDEV_REMOVE, hwndTarget = IntPtr.Zero };
                devices[1] = new RAWINPUTDEVICE { usUsagePage = 0x01, usUsage = 0x06, dwFlags = RIDEV_REMOVE, hwndTarget = IntPtr.Zero };
                RegisterRawInputDevices(devices, (uint)devices.Length, (uint)Marshal.SizeOf<RAWINPUTDEVICE>());
            }
            finally { _registered = false; }
        }

        private static void ThrowLastWin32(string api)
        {
            int err = Marshal.GetLastWin32Error();
            throw new Win32Exception(err, $"{api} failed with {err}");
        }

        private void EnsureNotDisposed()
        {
            if (_disposed) throw new ObjectDisposedException(nameof(RawInputService));
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            try { UnregisterDevices(); } catch { }
            _hwnd = IntPtr.Zero;
            GC.SuppressFinalize(this);
        }

        [StructLayout(LayoutKind.Sequential)]
        internal struct RAWINPUTDEVICE { public ushort usUsagePage; public ushort usUsage; public uint dwFlags; public IntPtr hwndTarget; }

        [StructLayout(LayoutKind.Sequential)]
        internal struct RAWINPUTHEADER { public uint dwType; public uint dwSize; public IntPtr hDevice; public IntPtr wParam; }

        [StructLayout(LayoutKind.Explicit)]
        internal struct RAWINPUT
        {
            [FieldOffset(0)] public RAWINPUTHEADER header;
            [FieldOffset(16)] public RAWMOUSE mouse;
            [FieldOffset(16)] public RAWKEYBOARD keyboard;
            public RAWINPUTDATA data => new RAWINPUTDATA { mouse = mouse, keyboard = keyboard };
        }

        internal struct RAWINPUTDATA { public RAWMOUSE mouse; public RAWKEYBOARD keyboard; }

        [StructLayout(LayoutKind.Sequential)]
        internal struct RAWMOUSE
        {
            public ushort usFlags;
            public uint ulButtons;        // Current button state
            public ushort usButtonFlags;  // Button transition flags
            public ushort usButtonData;   // Wheel data or X button info
            public uint ulRawButtons;
            public int lLastX;
            public int lLastY;
            public uint ulExtraInformation;
        }

        [StructLayout(LayoutKind.Sequential)]
        internal struct RAWKEYBOARD
        {
            public ushort MakeCode; public ushort Flags; public ushort Reserved; public ushort VKey; public uint Message; public uint ExtraInformation;
        }

        [DllImport("User32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern bool RegisterRawInputDevices([In] RAWINPUTDEVICE[] pRawInputDevices, uint uiNumDevices, uint cbSize);

        [DllImport("User32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern uint GetRawInputData(IntPtr hRawInput, uint uiCommand, IntPtr pData, ref uint pcbSize, uint cbSizeHeader);
    }

    public readonly struct RawMouseEvent
    {
        public readonly int DeltaX; public readonly int DeltaY; public readonly int Wheel; public readonly MouseButtons ButtonsDown;
        private RawMouseEvent(int dx, int dy, int wheel, MouseButtons buttons) { DeltaX = dx; DeltaY = dy; Wheel = wheel; ButtonsDown = buttons; }
        
        private static MouseButtons _currentButtons = MouseButtons.None;
        
        internal static RawMouseEvent From(object any)
        {
            var m = (RawInputService.RAWMOUSE)any;
            int wheel = 0;
            if ((m.usButtonFlags & 0x0400) != 0) wheel = (short)m.usButtonData / 120;
            
            // Update button state based on transition flags
            if ((m.usButtonFlags & 0x0001) != 0) { _currentButtons |= MouseButtons.Left; Logger.Info("LEFT DOWN"); }   // Left down
            if ((m.usButtonFlags & 0x0002) != 0) { _currentButtons &= ~MouseButtons.Left; Logger.Info("LEFT UP"); }  // Left up
            if ((m.usButtonFlags & 0x0004) != 0) { _currentButtons |= MouseButtons.Right; Logger.Info("RIGHT DOWN"); }  // Right down
            if ((m.usButtonFlags & 0x0008) != 0) { _currentButtons &= ~MouseButtons.Right; Logger.Info("RIGHT UP"); } // Right up
            if ((m.usButtonFlags & 0x0010) != 0) { _currentButtons |= MouseButtons.Middle; Logger.Info("MIDDLE DOWN"); } // Middle down
            if ((m.usButtonFlags & 0x0020) != 0) { _currentButtons &= ~MouseButtons.Middle; Logger.Info("MIDDLE UP"); }// Middle up
            if ((m.usButtonFlags & 0x0040) != 0) { _currentButtons |= MouseButtons.XButton1; Logger.Info("X1 DOWN"); } // X1 down
            if ((m.usButtonFlags & 0x0080) != 0) { _currentButtons &= ~MouseButtons.XButton1; Logger.Info("X1 UP"); } // X1 up
            if ((m.usButtonFlags & 0x0100) != 0) { _currentButtons |= MouseButtons.XButton2; Logger.Info("X2 DOWN"); } // X2 down
            if ((m.usButtonFlags & 0x0200) != 0) { _currentButtons &= ~MouseButtons.XButton2; Logger.Info("X2 UP"); } // X2 up
            
            return new RawMouseEvent(m.lLastX, m.lLastY, wheel, _currentButtons);
        }
    }

    public readonly struct RawKeyboardEvent
    {
        public readonly int VKey; public readonly ushort ScanCode; public readonly bool IsE0; public readonly bool IsBreak;
        private RawKeyboardEvent(int vkey, ushort scan, bool e0, bool isBreak) { VKey = vkey; ScanCode = scan; IsE0 = e0; IsBreak = isBreak; }
        internal static RawKeyboardEvent From(object any)
        {
            var k = (RawInputService.RAWKEYBOARD)any;
            bool isBreak = (k.Flags & 0x0001) != 0; bool e0 = (k.Flags & 0x0002) != 0;
            return new RawKeyboardEvent((int)k.VKey, k.MakeCode, e0, isBreak);
        }
    }
}
