using System;
using System.Threading;

namespace WootMouseRemap
{
    public sealed class MouseSettingsBus : IDisposable
    {
        private MouseSettings _current = MouseSettings.Defaults;
        private readonly object _gate = new object();
        private readonly System.Windows.Forms.Timer _debounce;
        private volatile bool _dirty;
        private MouseSettings _pending;

        public event Action<MouseSettings>? Changed;

        public MouseSettingsBus(int debounceMs = 20)
        {
            _pending = _current;
            _debounce = new System.Windows.Forms.Timer { Interval = Math.Max(1, debounceMs) };
            _debounce.Tick += (_, __) =>
            {
                if (!_dirty) return;
                _dirty = false;
                ApplyNow(_pending);
            };
            _debounce.Start();
        }

        public MouseSettings Current => _current;

        public void Queue(MouseSettings settings)
        {
            lock (_gate)
            {
                _pending = settings;
                _dirty = true;
            }
        }

        public void ApplyNow(MouseSettings settings)
        {
            _current = settings;
            Changed?.Invoke(settings);
        }

        public void Dispose()
        {
            _debounce?.Stop();
            _debounce?.Dispose();
        }
    }
}