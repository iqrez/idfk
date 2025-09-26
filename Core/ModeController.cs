using System;
using System.IO;

namespace WootMouseRemap
{
    /// <summary>
    /// Centralizes input mode selection with persistence and an idempotent state machine.
    /// Produces a single ModeChanged event; side effects (suppression, passthrough, UI) are handled by subscribers.
    /// </summary>
    public sealed class ModeController
    {
        private readonly object _lock = new();
        private readonly string _path;
        public InputMode Current { get; private set; }

        public event Action<InputMode>? ModeChanged;

        public ModeController(string storePath)
        {
            if (storePath == null) throw new ArgumentNullException(nameof(storePath));
            
            // Validate path to prevent path traversal
            var fullPath = Path.GetFullPath(storePath);
            var currentDir = Path.GetFullPath(Directory.GetCurrentDirectory());
            if (!fullPath.StartsWith(currentDir, StringComparison.OrdinalIgnoreCase))
            {
                throw new ArgumentException("Invalid store path", nameof(storePath));
            }
            
            _path = fullPath;
            Current = InputMode.Native;
            try
            {
                if (File.Exists(_path))
                {
                    var txt = File.ReadAllText(_path).Trim();
                    if (Enum.TryParse<InputMode>(txt, ignoreCase: true, out var parsed))
                        Current = parsed;
                    else
                        Current = MigrateOldMode(txt);
                }
            }
            catch { /* ignore persistence problems; start in default */ }
        }

        private InputMode MigrateOldMode(string oldModeString)
        {
            return oldModeString switch
            {
                "MouseKeyboard" => InputMode.Native,
                "ControllerOutput" => InputMode.MnKConvert,
                "ControllerPassthrough" => InputMode.ControllerPass,
                _ => InputMode.Native // Default fallback
            };
        }

        public void Apply(InputMode mode)
        {
            lock (_lock)
            {
                if (Current == mode) return;
                Current = mode;
                try
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
                    File.WriteAllText(_path, Current.ToString());
                }
                catch { /* ignore persistence errors */ }
            }
            try { ModeChanged?.Invoke(Current); } catch { /* subscriber threw; ignore */ }
        }

        /// <summary>
        /// Cycle through three modes: Native → MnKConvert → ControllerPass → Native
        /// </summary>
        public void ToggleNext(bool includePassthrough)
        {
            InputMode next;
            lock (_lock)
            {
                next = Current switch
                {
                    InputMode.Native => InputMode.MnKConvert,
                    InputMode.MnKConvert => includePassthrough ? InputMode.ControllerPass : InputMode.Native,
                    InputMode.ControllerPass => InputMode.Native,
                    _ => InputMode.Native
                };
            }
            // Call Apply outside the lock to avoid nested lock re-entry
            Apply(next);
        }

        /// <summary>
        /// Explicit two-way toggle, now only Native <-> ControllerPass.
        /// </summary>
        public void ToggleDesktopPlay()
        {
            InputMode next;
            lock (_lock)
            {
                next = Current == InputMode.Native ? InputMode.ControllerPass : InputMode.Native;
            }
            Apply(next);
        }
    }
}
