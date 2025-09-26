using System;
using System.IO;
using WootMouseRemap.Modes;

namespace WootMouseRemap.Core
{
    /// <summary>
    /// Concrete implementation that composes persistence (ModeController), runtime handlers (ModeManager), and suppression flag.
    /// </summary>
    public sealed class ModeService : IModeService, IDisposable
    {
        private readonly ModeController _persistence;
        private readonly ModeManager _manager;
        private readonly string _storePath;
        private bool _disposed;

        public event Action<InputMode, InputMode> ModeChanged = delegate { };

        public ModeService(string storePath, ModeManager manager)
        {
            _storePath = storePath ?? throw new ArgumentNullException(nameof(storePath));
            _manager = manager ?? throw new ArgumentNullException(nameof(manager));
            _persistence = new ModeController(storePath);
            _manager.ModeChanged += InternalManagerChanged;
        }

        public InputMode CurrentMode => _manager.CurrentMode; // runtime authoritative
    public bool SuppressionActive => LowLevelHooks.Suppress;

    /// <summary>
    /// True when the persisted stored mode equals the currently active runtime mode.
    /// Useful for diagnostics to detect mismatches or delayed initialization.
    /// </summary>
    public bool IsPersistedMatch => !_disposed && _persistence.Current == CurrentMode;

        public bool Switch(InputMode mode)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(ModeService));
            return _manager.SwitchMode(mode); // Manager will invoke events and update suppression via handlers
        }

        public InputMode Toggle()
        {
            if (_disposed) throw new ObjectDisposedException(nameof(ModeService));
            InputMode next;
            switch (CurrentMode)
            {
                case InputMode.Native:
                    next = InputMode.ControllerPass;
                    break;
                case InputMode.ControllerPass:
                    next = InputMode.MnKConvert;
                    break;
                case InputMode.MnKConvert:
                default:
                    next = InputMode.Native;
                    break;
            }
            Switch(next);
            return next;
        }

        /// <summary>
        /// Apply persisted startup mode after all handlers have been registered.
        /// </summary>
        public void InitializeFromPersistence()
        {
            if (_disposed) throw new ObjectDisposedException(nameof(ModeService));
            var desired = _persistence.Current;
            _manager.SwitchMode(desired); // safe if not registered yet; caller should register first
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            try { _manager.ModeChanged -= InternalManagerChanged; } catch { }
        }

        private void InternalManagerChanged(InputMode oldMode, InputMode newMode)
        {
            try { _persistence.Apply(newMode); } catch { }
            try { ModeChanged?.Invoke(oldMode, newMode); } catch { }
        }
    }
}
