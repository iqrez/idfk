using System;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using System.Threading;

namespace WootMouseRemap.Core
{
    /// <summary>
    /// Hotkey handling.
    /// Overlay Show/Hide: Backslash (VK_OEM_5 / '\\')
    /// Mode Toggle: F1
    /// Panic: Ctrl + Alt + Pause
    /// </summary>
    public sealed class HotkeyService : IDisposable
    {
        public event Action? ToggleRequested;          // Overlay visibility
        public event Action? ModeToggleRequested;      // Mode change
        public event Action? PanicRequested;           // Panic

        private bool _disposed;
        private bool _started;
        private SynchronizationContext? _syncContext;

        public HotkeyService()
        {
            Logger.Info("HotkeyService constructor called");
            // Don't subscribe to hooks in constructor - wait for explicit Start() call
        }

        public void Start()
        {
            if (_disposed) throw new ObjectDisposedException(nameof(HotkeyService));
            if (_started) return;

            // Capture the current SynchronizationContext so we can marshal event callbacks
            // to the UI thread (OverlayForm calls Start() from its OnLoad on the UI thread).
            _syncContext = SynchronizationContext.Current;

            Logger.Info("HotkeyService starting - subscribing to LowLevelHooks");
            LowLevelHooks.KeyEvent += OnKey;
            LowLevelHooks.MouseButton += OnMouseBtn; // reserved
            _started = true;
            Logger.Info("HotkeyService subscribed to LowLevelHooks.KeyEvent and LowLevelHooks.MouseButton");
        }

        public void Stop()
        {
            if (_disposed || !_started) return;

            Logger.Info("HotkeyService stopping - unsubscribing from LowLevelHooks");
            LowLevelHooks.KeyEvent -= OnKey;
            LowLevelHooks.MouseButton -= OnMouseBtn;
            _started = false;
            Logger.Info("HotkeyService unsubscribed from LowLevelHooks");
        }

        private void Post(Action action)
        {
            try
            {
                if (_syncContext != null)
                {
                    _syncContext.Post(_ =>
                    {
                        try { action(); } catch (Exception ex) { Logger.Error("Error in hotkey event handler", ex); }
                    }, null);
                }
                else
                {
                    // No synchronization context available - invoke inline but protect with try/catch
                    try { action(); } catch (Exception ex) { Logger.Error("Error in hotkey event handler (no sync)", ex); }
                }
            }
            catch (Exception ex)
            {
                Logger.Error("Failed to post hotkey event to synchronization context", ex);
                // Fall back to direct invocation if posting fails
                try { action(); } catch (Exception inner) { Logger.Error("Error invoking hotkey event after post failure", inner); }
            }
        }

        private void OnKey(int vk, bool down)
        {
            if (_disposed || !_started || !down) return;

            // Always log key detection for debugging
            Logger.Info("HotkeyService key detected: vk={VK} (0x{VKHex:X}) down={Down}", vk, vk, down);

            // Panic
            if (vk == (int)Keys.Pause && IsCtrlDown() && IsAltDown())
            {
                Logger.Info("Hotkey: Panic (Ctrl+Alt+Pause)");
                Post(() => PanicRequested?.Invoke());
                return;
            }

            // Mode toggle (F1)
            if (vk == (int)Keys.F1)
            {
                Logger.Info("Hotkey: Mode toggle (F1)");
                Post(() => ModeToggleRequested?.Invoke());
                return;
            }

            // Overlay toggle (Backslash / VK_OEM_5 = 0xDC)
            if (vk == (int)Keys.Oem5 || vk == 0xDC)
            {
                Logger.Info("Hotkey: Overlay toggle (VK=0x{VK:X})", vk);
                Post(() => ToggleRequested?.Invoke());
                return;
            }
        }

        private void OnMouseBtn(MouseInput b, bool down)
        {
            if (_disposed || !_started) return;
            // (Unused now) Add future mouse hotkeys here.
        }

        public static bool IsAltDown() => (GetKeyState(VK_MENU) & 0x8000) != 0;
        public static bool IsCtrlDown() => (GetKeyState(VK_CONTROL) & 0x8000) != 0;

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            Stop();
        }

        private const int VK_MENU = 0x12;     // Alt
        private const int VK_CONTROL = 0x11;  // Ctrl

        [DllImport("user32.dll", SetLastError = true)] private static extern short GetKeyState(int nVirtKey);
    }
}