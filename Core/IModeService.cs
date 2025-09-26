using System;

namespace WootMouseRemap.Core
{
    /// <summary>
    /// Unified facade for querying and changing input mode plus suppression state.
    /// Keeps UI and diagnostics decoupled from concrete managers/handlers.
    /// </summary>
    public interface IModeService
    {
        /// <summary>Current effective mode.</summary>
        InputMode CurrentMode { get; }
        /// <summary>Whether low level input suppression is currently active.</summary>
        bool SuppressionActive { get; }
        /// <summary>Raised after the mode has changed (oldMode, newMode).</summary>
        event Action<InputMode, InputMode> ModeChanged;
        /// <summary>Attempt to switch to the provided mode; returns true if state changed or already in that mode.</summary>
        bool Switch(InputMode mode);
        /// <summary>Toggle between MnKConvert and ControllerPass.</summary>
        InputMode Toggle();
    }
}
