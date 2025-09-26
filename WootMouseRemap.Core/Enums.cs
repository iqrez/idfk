using System;

namespace WootMouseRemap
{
    public enum MouseInput { Left, Right, Middle, XButton1, XButton2, ScrollUp, ScrollDown, Move }

    public enum Xbox360Control
    {
        A, B, X, Y,
        DpadUp, DpadDown, DpadLeft, DpadRight,
        LeftBumper, RightBumper,
        LeftTrigger, RightTrigger,
        Start, Back, LeftStick, RightStick
    }

    [Flags]
    public enum ModKeys { None = 0, Alt = 1, Ctrl = 2, Shift = 4, Win = 8 }

    public enum KeysVirtual : int
    {
        VK_W = 0x57, VK_A = 0x41, VK_S = 0x53, VK_D = 0x44,
        VK_SPACE = 0x20, VK_LSHIFT = 0xA0, VK_LCONTROL = 0xA2, VK_LMENU = 0xA4,
        VK_Q = 0x51, VK_E = 0x45
    }
}
