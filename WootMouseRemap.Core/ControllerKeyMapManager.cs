// ControllerKeyMapManager — enum-agnostic, bi-directional, with overloads
using System.Collections.Generic;
using System.Windows.Forms;
using Nefarius.ViGEm.Client.Targets.Xbox360;

namespace WootMouseRemap
{
    /// <summary>
    /// Stores mappings between keyboard Keys and your project's Xbox360Control enum.
    /// Also provides helpers to translate the logical control to ViGEm buttons.
    /// This version avoids hardcoding specific enum member names in signatures used by the UI.
    /// </summary>
    public static class ControllerKeyMapManager
    {
        private static readonly Dictionary<Keys, Xbox360Control> _keyToControl = new();
        private static readonly Dictionary<Xbox360Control, Keys> _controlToKey = new();

        // ---- Bindings CRUD ----
        public static void SetMapping(Xbox360Control control, Keys key)
        {
            if (_controlToKey.TryGetValue(control, out var oldKey)) _keyToControl.Remove(oldKey);
            if (_keyToControl.TryGetValue(key, out var oldCtl)) _controlToKey.Remove(oldCtl);

            _controlToKey[control] = key;
            _keyToControl[key] = control;
        }

        public static bool RemoveMapping(Xbox360Control control)
        {
            if (_controlToKey.TryGetValue(control, out var key))
            {
                _controlToKey.Remove(control);
                _keyToControl.Remove(key);
                return true;
            }
            return false;
        }

        public static bool RemoveMapping(Keys key)
        {
            if (_keyToControl.TryGetValue(key, out var ctl))
            {
                _keyToControl.Remove(key);
                _controlToKey.Remove(ctl);
                return true;
            }
            return false;
        }

        public static bool TryGetKey(Xbox360Control control, out Keys key) =>
            _controlToKey.TryGetValue(control, out key);

        public static bool TryGetControl(Keys key, out Xbox360Control control) =>
            _keyToControl.TryGetValue(key, out control);

        public static void Clear()
        {
            _keyToControl.Clear();
            _controlToKey.Clear();
        }

        // ---- ViGEm helpers ----
        /// <summary>
        /// Try translate a keyboard key to a ViGEm Xbox360Button via the current control binding.
        /// </summary>
        public static bool TryGetButton(Keys key, out Xbox360Button button)
        {
            button = Xbox360Button.A;
            if (!_keyToControl.TryGetValue(key, out var control))
                return false;
            return TryMapControlToButton(control, out button);
        }

        /// <summary>
        /// OverlayForm frequently wants to fire a button directly from a control; support that too.
        /// </summary>
        public static bool TryGetButton(Xbox360Control control, out Xbox360Button button) =>
            TryMapControlToButton(control, out button);

        /// <summary>
        /// Map your enum value to ViGEm's Xbox360Button by control name.
        /// Supports common aliases used in different codebases.
        /// </summary>
        private static bool TryMapControlToButton(Xbox360Control control, out Xbox360Button button)
        {
            button = Xbox360Button.A;
            var name = control.ToString(); // use enum's label

            switch (name)
            {
                // Face
                case "A": button = Xbox360Button.A; return true;
                case "B": button = Xbox360Button.B; return true;
                case "X": button = Xbox360Button.X; return true;
                case "Y": button = Xbox360Button.Y; return true;

                // Menu
                case "Start": button = Xbox360Button.Start; return true;
                case "Back": button = Xbox360Button.Back; return true;

                // Shoulders / Bumpers
                case "LeftShoulder":
                case "LB":
                case "LeftBumper":
                case "ShoulderLeft":
                case "BumperLeft":
                    button = Xbox360Button.LeftShoulder; return true;

                case "RightShoulder":
                case "RB":
                case "RightBumper":
                case "ShoulderRight":
                case "BumperRight":
                    button = Xbox360Button.RightShoulder; return true;

                // Stick clicks
                case "LeftThumb":
                case "LeftStick":
                case "ThumbLeft":
                case "L3":
                    button = Xbox360Button.LeftThumb; return true;

                case "RightThumb":
                case "RightStick":
                case "ThumbRight":
                case "R3":
                    button = Xbox360Button.RightThumb; return true;

                // D-Pad (support common spellings)
                case "Up":
                case "DPadUp":
                case "DpadUp":
                case "DPad_Up":
                    button = Xbox360Button.Up; return true;

                case "Down":
                case "DPadDown":
                case "DpadDown":
                case "DPad_Down":
                    button = Xbox360Button.Down; return true;

                case "Left":
                case "DPadLeft":
                case "DpadLeft":
                case "DPad_Left":
                    button = Xbox360Button.Left; return true;

                case "Right":
                case "DPadRight":
                case "DpadRight":
                case "DPad_Right":
                    button = Xbox360Button.Right; return true;

                // Guide
                case "Guide":
                case "Xbox":
                case "Home":
                    button = Xbox360Button.Guide; return true;

                // Triggers are analog (LT/RT) — not mapped to buttons here.

                default:
                    return false;
            }
        }
    }
}
