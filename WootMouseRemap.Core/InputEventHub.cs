using System;
using System.Windows.Forms;

namespace WootMouseRemap.Core
{
    /// <summary>
    /// Owns RawInput and LowLevelHooks lifecycles and emits normalized events,
    /// plus an idle watchdog tick when recent motion hasn't been observed.
    /// Now also surfaces raw keyboard/mouse events for mode routing.
    /// </summary>
    public sealed class InputEventHub : IDisposable
    {
        public event Action? IdleTick;
        public event Action<int, bool>? Key;                 // vk, down
        public event Action<MouseInput, bool>? MouseButton;  // button, down
        public event Action<int, int>? MouseMove;            // dx, dy
        public event Action<int>? MouseWheel;                // wheel delta

        private readonly RawInputMsgWindow _msgWin;
        private readonly RawInput _raw;
        private readonly System.Windows.Forms.Timer _idleTimer;
        private DateTime _lastMotionUtc = DateTime.UtcNow;
        private bool _disposed;

        public InputEventHub()
        {
            _msgWin = new RawInputMsgWindow();
            _raw = new RawInput(_msgWin);

            _raw.KeyboardEvent += (vk, down) => { Key?.Invoke(vk, down); Touch(); };
            _raw.MouseButton += (b, d) => { MouseButton?.Invoke(b, d); Touch(); };
            _raw.MouseMove += (dx, dy) => { if (dx != 0 || dy != 0) { MouseMove?.Invoke(dx, dy); Touch(); } };
            _raw.MouseWheel += delta => { MouseWheel?.Invoke(delta); Touch(); };

            // Also subscribe to LowLevelHooks for fallback keyboard/mouse button events
            LowLevelHooks.KeyEvent += (vk, down) => { Key?.Invoke(vk, down); Touch(); };
            LowLevelHooks.MouseButton += (b, d) => { MouseButton?.Invoke(b, d); Touch(); };

            _idleTimer = new System.Windows.Forms.Timer { Interval = 45 };
            _idleTimer.Tick += (_, __) =>
            {
                if ((DateTime.UtcNow - _lastMotionUtc).TotalMilliseconds >= 40.0)
                    IdleTick?.Invoke();
            };
        }

        public void Start()
        {
            if (_disposed) throw new ObjectDisposedException(nameof(InputEventHub));
            _raw.Register();
            LowLevelHooks.Install();
            _idleTimer.Start();
        }

        public void Stop()
        {
            if (_disposed) return;
            _idleTimer.Stop();
            LowLevelHooks.Uninstall();
            _raw.Unregister();
        }

        public void Touch() => _lastMotionUtc = DateTime.UtcNow;

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            try { Stop(); } catch { }
            _idleTimer.Dispose();
            _raw.Dispose();
            _msgWin.Dispose();
        }
    }
}