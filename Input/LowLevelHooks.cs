using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using WootMouseRemap.Diagnostics;
// System.Drawing no longer needed after removing mouse move delta logic

namespace WootMouseRemap
{
    public static class LowLevelHooks
    {
        public static event Action<int, bool>? KeyEvent;           // vk, down
        public static event Action<MouseInput, bool>? MouseButton;  // button, down
        // Mouse movement is now handled exclusively via RawInput; we keep only button & key events here.
        public static event Action? PanicTriggered;

        private static volatile bool _suppress = false;
        private static readonly object _lockObject = new();
        private static DateTime _lastErrorLog = DateTime.MinValue;

        public static bool Suppress 
        { 
            get 
            { 
                lock (_lockObject)
                {
                    return _suppress;
                }
            } 
            set 
            { 
                lock (_lockObject)
                {
                    if (_suppress != value)
                    {
                        _suppress = value;
                        Logger.Info("LowLevelHooks.Suppress set to {Value}", value);
                    }
                }
            } 
        }

        private static IntPtr _hkKb = IntPtr.Zero;
        private static IntPtr _hkMs = IntPtr.Zero;
        // Removed last point tracking; mouse delta generation moved to RawInput pathway.
        
        public static bool IsInstalled 
        { 
            get 
            { 
                lock (_lockObject)
                {
                    return _hkKb != IntPtr.Zero && _hkMs != IntPtr.Zero;
                }
            } 
        }

        public static void Install(bool enableLowLevelHooks = true, bool complianceMode = false)
        {
            try
            {
                if (!enableLowLevelHooks)
                {
                    Logger.Info("LowLevelHooks disabled by configuration");
                    return;
                }
                
                if (_hkKb != IntPtr.Zero) 
                {
                    Logger.Info("LowLevelHooks already installed");
                    return;
                }
                
                _kbProc = KbProc;
                _msProc = MsProc;
                _hkKb = SetWindowsHookEx(13 /*WH_KEYBOARD_LL*/, _kbProc, GetModuleHandle(IntPtr.Zero), 0);
                _hkMs = SetWindowsHookEx(14 /*WH_MOUSE_LL*/, _msProc, GetModuleHandle(IntPtr.Zero), 0);
                
                if (_hkKb == IntPtr.Zero || _hkMs == IntPtr.Zero) 
                {
                    var error = Marshal.GetLastWin32Error();
                    Logger.Error("SetWindowsHookEx failed with error code: {Error}", error);
                    
                    // Clean up any partially installed hooks
                    if (_hkKb != IntPtr.Zero)
                    {
                        UnhookWindowsHookEx(_hkKb);
                        _hkKb = IntPtr.Zero;
                    }
                    if (_hkMs != IntPtr.Zero)
                    {
                        UnhookWindowsHookEx(_hkMs);
                        _hkMs = IntPtr.Zero;
                    }
                    
                    var errorMessage = error switch
                    {
                        1428 => "Access denied. Try running as administrator.",
                        87 => "Invalid parameter. System may not support low-level hooks.",
                        8 => "Not enough memory to install hooks.",
                        _ => $"Unknown error code: {error}"
                    };
                    
                    throw new InvalidOperationException($"SetWindowsHookEx failed: {errorMessage}");
                }
                
                Logger.Info("LowLevelHooks installed successfully");
            }
            catch (Exception ex)
            {
                Logger.Error("Error installing LowLevelHooks", ex);
                throw;
            }
        }

        public static void Uninstall()
        {
            try
            {
                lock (_lockObject)
                {
                    if (_hkKb != IntPtr.Zero) 
                    { 
                        UnhookWindowsHookEx(_hkKb); 
                        _hkKb = IntPtr.Zero; 
                        Logger.Info("Keyboard hook uninstalled");
                    }
                    if (_hkMs != IntPtr.Zero) 
                    { 
                        UnhookWindowsHookEx(_hkMs); 
                        _hkMs = IntPtr.Zero; 
                        Logger.Info("Mouse hook uninstalled");
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Error("Error uninstalling LowLevelHooks", ex);
            }
        }

        private static IntPtr KbProc(int nCode, IntPtr wParam, IntPtr lParam)
        {
            try
            {
                if (nCode >= 0 && IsInstalled)
                {
                    var msg = (int)wParam;
                    var kb = Marshal.PtrToStructure<KBDLLHOOKSTRUCT>(lParam);
                    bool down = msg == WM_KEYDOWN || msg == WM_SYSKEYDOWN;
                    bool up = msg == WM_KEYUP || msg == WM_SYSKEYUP;

                    // Panic hotkey: Ctrl+Alt+Pause -> disable suppression
                    if (down && kb.vkCode == (int)Keys.Pause && IsCtrlAltDown())
                    {
                        Suppress = false;
                        try
                        {
                            PanicTriggered?.Invoke();
                        }
                        catch (Exception ex)
                        {
                            Logger.Error("Error in PanicTriggered event handler", ex);
                        }
                        Logger.Warn("PANIC triggered: suppression disabled");
                    }

                    if (down || up) 
                    {
                        try
                        {
                            KeyEvent?.Invoke(kb.vkCode, down);
                        }
                        catch (Exception ex)
                        {
                            // Throttle error logging to prevent spam
                            var now = DateTime.UtcNow;
                            if (now - _lastErrorLog > TimeSpan.FromSeconds(5))
                            {
                                Logger.Error("Error in KeyEvent handler for key {VkCode} (down: {Down})", kb.vkCode, down, ex);
                                _lastErrorLog = now;
                            }
                        }
                    }

                    if (Suppress) return (IntPtr)1;
                }
            }
            catch (Exception ex)
            {
                // Throttle error logging to prevent spam
                var now = DateTime.UtcNow;
                if (now - _lastErrorLog > TimeSpan.FromSeconds(5))
                {
                    Logger.Error("Error in keyboard hook procedure", ex);
                    _lastErrorLog = now;
                }
            }
            
            return CallNextHookEx(_hkKb, nCode, wParam, lParam);
        }

        private static IntPtr MsProc(int nCode, IntPtr wParam, IntPtr lParam)
        {
            try
            {
                if (nCode >= 0 && IsInstalled)
                {
                    var msg = (int)wParam;
                    var ms = Marshal.PtrToStructure<MSLLHOOKSTRUCT>(lParam);

                    try
                    {
                        switch (msg)
                        {
                            case WM_LBUTTONDOWN: MouseButton?.Invoke(MouseInput.Left, true); break;
                            case WM_LBUTTONUP:   MouseButton?.Invoke(MouseInput.Left, false); break;
                            case WM_RBUTTONDOWN: MouseButton?.Invoke(MouseInput.Right, true); break;
                            case WM_RBUTTONUP:   MouseButton?.Invoke(MouseInput.Right, false); break;
                            case WM_MBUTTONDOWN: MouseButton?.Invoke(MouseInput.Middle, true); break;
                            case WM_MBUTTONUP:   MouseButton?.Invoke(MouseInput.Middle, false); break;
                            case WM_XBUTTONDOWN:
                                if (((ms.mouseData >> 16) & 0xffff) == 1) MouseButton?.Invoke(MouseInput.XButton1, true); else MouseButton?.Invoke(MouseInput.XButton2, true);
                                break;
                            case WM_XBUTTONUP:
                                if (((ms.mouseData >> 16) & 0xffff) == 1) MouseButton?.Invoke(MouseInput.XButton1, false); else MouseButton?.Invoke(MouseInput.XButton2, false);
                                break;
                            case WM_MOUSEWHEEL:
                                // Wheel handled by RawInput; ignore here.
                                break;
                            // Guard against accidental reintroduction of movement handling.
                            // Movement MUST come from RawInput for accuracy (high freq, per device) and to avoid cursor deltas under suppression.
                            case 0x0200: // WM_MOUSEMOVE (intentionally not using constant to discourage reuse)
                                // In DEBUG builds, log this occurrence but don't fail - system initialization can trigger mouse events
                                #if DEBUG
                                var now = DateTime.UtcNow;
                                if (now - _lastErrorLog > TimeSpan.FromSeconds(10)) 
                                { 
                                    Logger.Debug("WM_MOUSEMOVE in LowLevelHooks during system initialization - ignoring (movement handled by RawInput)"); 
                                    _lastErrorLog = now; 
                                }
                                #else
                                if ((DateTime.UtcNow - _lastErrorLog) > TimeSpan.FromSeconds(10)) { Logger.Warn("Ignored WM_MOUSEMOVE in LowLevelHooks (movement handled by RawInput)"); _lastErrorLog = DateTime.UtcNow; }
                                #endif
                                break;
                        }
                    }
                    catch (Exception ex)
                    {
                        // Throttle error logging to prevent spam
                        var now = DateTime.UtcNow;
                        if (now - _lastErrorLog > TimeSpan.FromSeconds(5))
                        {
                            Logger.Error("Error in mouse event handler for message {Msg}", msg, ex);
                            _lastErrorLog = now;
                        }
                    }

                    if (Suppress) return (IntPtr)1;
                }
            }
            catch (Exception ex)
            {
                // Throttle error logging to prevent spam
                var now = DateTime.UtcNow;
                if (now - _lastErrorLog > TimeSpan.FromSeconds(5))
                {
                    Logger.Error("Error in mouse hook procedure", ex);
                    _lastErrorLog = now;
                }
            }
            
            return CallNextHookEx(_hkMs, nCode, wParam, lParam);
        }

        private static bool IsCtrlAltDown()
        {
            return (GetKeyState((int)Keys.LControlKey) < 0 || GetKeyState((int)Keys.RControlKey) < 0)
                && (GetKeyState((int)Keys.LMenu) < 0 || GetKeyState((int)Keys.RMenu) < 0);
        }

        private static LowLevelProc? _kbProc, _msProc;

        private delegate IntPtr LowLevelProc(int nCode, IntPtr wParam, IntPtr lParam);

        [StructLayout(LayoutKind.Sequential)]
        private struct KBDLLHOOKSTRUCT { public int vkCode, scanCode, flags, time; public IntPtr dwExtraInfo; }

        [StructLayout(LayoutKind.Sequential)]
        private struct MSLLHOOKSTRUCT
        {
            public POINT pt;
            public int mouseData, flags, time;
            public IntPtr dwExtraInfo;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct POINT { public int x, y; }

        private const int WM_KEYDOWN = 0x0100, WM_KEYUP = 0x0101, WM_SYSKEYDOWN = 0x0104, WM_SYSKEYUP = 0x0105;
    private const int WM_LBUTTONDOWN = 0x0201, WM_LBUTTONUP = 0x0202, WM_RBUTTONDOWN = 0x0204, WM_RBUTTONUP = 0x0205,
              WM_MBUTTONDOWN = 0x0207, WM_MBUTTONUP = 0x0208, WM_XBUTTONDOWN = 0x020B, WM_XBUTTONUP = 0x020C, WM_MOUSEWHEEL = 0x020A;

        [DllImport("user32.dll")] private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelProc lpfn, IntPtr hMod, uint dwThreadId);
        [DllImport("user32.dll")] private static extern bool UnhookWindowsHookEx(IntPtr hhk);
        [DllImport("user32.dll")] private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);
        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)] private static extern IntPtr GetModuleHandle(IntPtr lpModuleName);
        [DllImport("user32.dll")] private static extern short GetKeyState(int nVirtKey);
    }
}
