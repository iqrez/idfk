
using System;
using System.Runtime.InteropServices;

namespace WootMouseRemap.Controllers
{
    public sealed class XInputController : PhysicalController
    {
        private readonly int _index;
        public XInputController(int index) { _index = index; }

        public override string DisplayName => $"XInput {_index}";
        public override bool IsConnected
        {
            get
            {
                return XInputGetState(_index, out var _) == 0;
            }
        }

        public override bool Poll(out PadSnapshot snap)
        {
            snap = default;
            if (XInputGetState(_index, out var state) != 0) return false;
            // Axes
            snap.LX = state.Gamepad.sThumbLX;
            snap.LY = state.Gamepad.sThumbLY;
            snap.RX = state.Gamepad.sThumbRX;
            snap.RY = state.Gamepad.sThumbRY;
            snap.LT = state.Gamepad.bLeftTrigger;
            snap.RT = state.Gamepad.bRightTrigger;
            // Buttons
            var b = state.Gamepad.wButtons;
            snap.A = (b & 0x1000) != 0;
            snap.B = (b & 0x2000) != 0;
            snap.X = (b & 0x4000) != 0;
            snap.Y = (b & 0x8000) != 0;
            snap.LB = (b & 0x0100) != 0;
            snap.RB = (b & 0x0200) != 0;
            snap.Back = (b & 0x0020) != 0;
            snap.Start = (b & 0x0010) != 0;
            snap.L3 = (b & 0x0040) != 0;
            snap.R3 = (b & 0x0080) != 0;
            snap.DUp = (b & 0x0001) != 0;
            snap.DDown = (b & 0x0002) != 0;
            snap.DLeft = (b & 0x0004) != 0;
            snap.DRight = (b & 0x0008) != 0;
            return true;
        }

        #region XInput P/Invoke
        [DllImport("xinput1_4.dll", EntryPoint="XInputGetState", SetLastError = true)]
        private static extern int XInputGetState14(int dwUserIndex, out XINPUT_STATE pState);

        [DllImport("xinput1_3.dll", EntryPoint="XInputGetState", SetLastError = true)]
        private static extern int XInputGetState13(int dwUserIndex, out XINPUT_STATE pState);

        private static int XInputGetState(int index, out XINPUT_STATE state)
        {
            try { return XInputGetState14(index, out state); }
            catch { try { return XInputGetState13(index, out state); } catch { state = default; return -1; } }
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct XINPUT_STATE
        {
            public uint dwPacketNumber;
            public XINPUT_GAMEPAD Gamepad;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct XINPUT_GAMEPAD
        {
            public ushort wButtons;
            public byte bLeftTrigger;
            public byte bRightTrigger;
            public short sThumbLX;
            public short sThumbLY;
            public short sThumbRX;
            public short sThumbRY;
        }
        #endregion
    }
}
