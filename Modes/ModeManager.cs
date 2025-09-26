using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using WootMouseRemap.Diagnostics;
using WootMouseRemap.Core;

namespace WootMouseRemap.Modes
{
    public class ModeManager : IDisposable
    {
        private readonly Dictionary<InputMode, IModeHandler> _modes;
        private readonly object _lockObject = new();
        private readonly ModeDiagnostics _diagnostics;
        private readonly ModeStateValidator _validator;
        private volatile IModeHandler? _currentMode;
        private volatile bool _disposed;

    public event Action<InputMode, InputMode>? ModeChanged;

        public InputMode CurrentMode 
        { 
            get 
            { 
                lock (_lockObject)
                {
                    return _currentMode?.Mode ?? InputMode.Native;
                }
            } 
        }

        public ModeManager(ModeDiagnostics? diagnostics = null)
        {
            _modes = new Dictionary<InputMode, IModeHandler>();
            _diagnostics = diagnostics ?? new ModeDiagnostics();
            _validator = new ModeStateValidator(_diagnostics);
            
            Logger.Info("ModeManager initialized with thread safety");
        }

        public void RegisterMode(IModeHandler modeHandler)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(ModeManager));
            if (modeHandler == null) throw new ArgumentNullException(nameof(modeHandler));

            lock (_lockObject)
            {
                _modes[modeHandler.Mode] = modeHandler;

                if (_currentMode == null)
                {
                    _currentMode = modeHandler;
                    _diagnostics?.UpdateSystemState("CurrentMode", modeHandler.Mode);
                }
            }
            
            Logger.Info("Registered mode handler: {Mode}", modeHandler.Mode);
        }

        public bool SwitchMode(InputMode newMode)
        {
            if (_disposed)
            {
                Logger.Warn("Attempted to switch mode on disposed ModeManager");
                return false;
            }

            try
            {
                return SwitchModeInternal(newMode);
            }
            catch (Exception ex)
            {
                Logger.Error("Error switching to mode {NewMode}", newMode, ex);
                _diagnostics?.LogModeTransition(CurrentMode, newMode, false, ex);
                return false;
            }
        }

        private bool SwitchModeInternal(InputMode newMode)
        {
            IModeHandler? newModeHandler;
            IModeHandler? oldModeHandler;
            InputMode oldMode;

            // Validate and prepare under lock
            lock (_lockObject)
            {
                if (!_modes.TryGetValue(newMode, out newModeHandler))
                {
                    var ex = new ArgumentException($"Mode {newMode} is not registered");
                    Logger.Error("Mode not registered", ex);
                    return false;
                }

                oldModeHandler = _currentMode;
                oldMode = oldModeHandler?.Mode ?? InputMode.Native;

                if (oldMode == newMode)
                {
                    Logger.Info("Already in mode {NewMode}, no switch needed", newMode);
                    return true;
                }

                // Validate the transition
                var context = BuildValidationContext();
                var validationResult = _validator.ValidateTransition(oldMode, newMode, context);
                
                if (!validationResult.IsValid)
                {
                    var errors = string.Join(", ", validationResult.Issues.ConvertAll(i => i.Message));
                    Logger.Error("Mode transition validation failed: {Errors}", errors);
                    _diagnostics?.LogModeTransition(oldMode, newMode, false, 
                        new InvalidOperationException($"Validation failed: {errors}"));
                    return false;
                }
            }

            // Perform transition outside of lock to avoid deadlocks
            try
            {
                Logger.Info("Switching mode: {OldMode} -> {NewMode}", oldMode, newMode);

                // Exit old mode
                if (oldModeHandler != null)
                {
                    try
                    {
                        oldModeHandler.OnModeExited(newMode);
                    }
                    catch (Exception ex)
                    {
                        Logger.Error("Error exiting mode {OldMode}", oldMode, ex);
                        // Continue with transition despite exit error
                    }
                }

                // Update current mode under lock
                lock (_lockObject)
                {
                    _currentMode = newModeHandler;
                }

                // Enter new mode
                try
                {
                    newModeHandler.OnModeEntered(oldMode);
                }
                catch (Exception ex)
                {
                    Logger.Error("Error entering mode {NewMode}", newMode, ex);
                    
                    // Attempt rollback
                    try
                    {
                        lock (_lockObject)
                        {
                            _currentMode = oldModeHandler;
                        }
                        oldModeHandler?.OnModeEntered(newMode);
                    }
                    catch (Exception rollbackEx)
                    {
                        Logger.Error("Mode rollback failed", rollbackEx);
                    }
                    
                    _diagnostics?.LogModeTransition(oldMode, newMode, false, ex);
                    return false;
                }

                // Update diagnostics
                _diagnostics?.UpdateSystemState("CurrentMode", newMode);
                _diagnostics?.LogModeTransition(oldMode, newMode, true);

                // Notify listeners
                try
                {
                    ModeChanged?.Invoke(oldMode, newMode);
                }
                catch (Exception ex)
                {
                    Logger.Error("Error in ModeChanged event handlers", ex);
                    // Don't fail the transition for event handler errors
                }

                Logger.Info("Mode switch completed: {OldMode} -> {NewMode}", oldMode, newMode);
                return true;
            }
            catch (Exception ex)
            {
                Logger.Error("Unexpected error during mode switch: {OldMode} -> {NewMode}", oldMode, newMode, ex);
                _diagnostics?.LogModeTransition(oldMode, newMode, false, ex);
                return false;
            }
        }

        public void OnKey(int vk, bool down)
        {
            if (_disposed) return;
            
            try
            {
                IModeHandler? currentHandler;
                lock (_lockObject)
                {
                    currentHandler = _currentMode;
                }
                
                currentHandler?.OnKey(vk, down);
            }
            catch (Exception ex)
            {
                Logger.Error("Error in OnKey({Vk}, {Down})", vk, down, ex);
            }
        }

        public void OnMouseButton(MouseInput button, bool down)
        {
            if (_disposed) return;
            
            try
            {
                IModeHandler? currentHandler;
                lock (_lockObject)
                {
                    currentHandler = _currentMode;
                }
                
                currentHandler?.OnMouseButton(button, down);
            }
            catch (Exception ex)
            {
                Logger.Error("Error in OnMouseButton({Button}, {Down})", button, down, ex);
            }
        }

        public void OnMouseMove(int dx, int dy)
        {
            if (_disposed) return;
            
            try
            {
                IModeHandler? currentHandler;
                lock (_lockObject)
                {
                    currentHandler = _currentMode;
                }
                
                currentHandler?.OnMouseMove(dx, dy);
            }
            catch (Exception ex)
            {
                Logger.Error("Error in OnMouseMove({Dx}, {Dy})", dx, dy, ex);
            }
        }

        public void OnWheel(int delta)
        {
            if (_disposed) return;
            
            try
            {
                IModeHandler? currentHandler;
                lock (_lockObject)
                {
                    currentHandler = _currentMode;
                }
                
                currentHandler?.OnWheel(delta);
            }
            catch (Exception ex)
            {
                Logger.Error("Error in OnWheel({Delta})", delta, ex);
            }
        }

        public void OnControllerConnected(int index)
        {
            if (_disposed) return;
            
            try
            {
                IModeHandler? currentHandler;
                lock (_lockObject)
                {
                    currentHandler = _currentMode;
                }
                
                currentHandler?.OnControllerConnected(index);
                _diagnostics?.UpdateSystemState("PhysicalController.Connected", true);
                _diagnostics?.UpdateSystemState("PhysicalController.Index", index);
            }
            catch (Exception ex)
            {
                Logger.Error("Error in OnControllerConnected({Index})", index, ex);
            }
        }

        public void OnControllerDisconnected(int index)
        {
            if (_disposed) return;
            
            try
            {
                IModeHandler? currentHandler;
                lock (_lockObject)
                {
                    currentHandler = _currentMode;
                }
                
                currentHandler?.OnControllerDisconnected(index);
                _diagnostics?.UpdateSystemState("PhysicalController.Connected", false);
            }
            catch (Exception ex)
            {
                Logger.Error("Error in OnControllerDisconnected({Index})", index, ex);
            }
        }

        public void Update()
        {
            if (_disposed) return;
            
            try
            {
                IModeHandler? currentHandler;
                lock (_lockObject)
                {
                    currentHandler = _currentMode;
                }
                
                currentHandler?.Update();
            }
            catch (Exception ex)
            {
                Logger.Error("Error in Update()", ex);
            }
        }

        public bool ShouldSuppressInput 
        { 
            get 
            { 
                if (_disposed) return false;
                
                try
                {
                    IModeHandler? currentHandler;
                    lock (_lockObject)
                    {
                        currentHandler = _currentMode;
                    }
                    
                    var shouldSuppress = currentHandler?.ShouldSuppressInput ?? false;
                    _diagnostics?.UpdateSystemState("LowLevelHooks.Suppress", shouldSuppress);
                    return shouldSuppress;
                }
                catch (Exception ex)
                {
                    Logger.Error("Error getting ShouldSuppressInput", ex);
                    return false;
                }
            } 
        }

        public string GetStatusText()
        {
            if (_disposed) return "Mode: Disposed";
            
            try
            {
                IModeHandler? currentHandler;
                lock (_lockObject)
                {
                    currentHandler = _currentMode;
                }
                
                return currentHandler?.GetStatusText() ?? "Mode: Unknown";
            }
            catch (Exception ex)
            {
                Logger.Error("Error getting status text", ex);
                return "Mode: Error";
            }
        }

        private ModeSystemContext BuildValidationContext()
        {
            var context = new ModeSystemContext
            {
                CurrentMode = CurrentMode,
                ModeManagerCurrentMode = CurrentMode
            };

            if (_diagnostics != null)
            {
                context.SuppressionEnabled = _diagnostics.GetSystemState<bool?>("LowLevelHooks.Suppress");
                context.ViGEmConnected = _diagnostics.GetSystemState<bool?>("ViGEm.Connected");
                context.PhysicalControllerConnected = _diagnostics.GetSystemState<bool?>("PhysicalController.Connected");
                context.XInputPassthroughRunning = _diagnostics.GetSystemState<bool?>("XInputPassthrough.Running");
            }

            return context;
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            try
            {
                // Dispose of any mode handlers that implement IDisposable
                lock (_lockObject)
                {
                    foreach (var mode in _modes.Values)
                    {
                        if (mode is IDisposable disposable)
                        {
                            try
                            {
                                disposable.Dispose();
                            }
                            catch (Exception ex)
                            {
                                Logger.Error("Error disposing mode handler {Mode}", mode.Mode, ex);
                            }
                        }
                    }
                    _modes.Clear();
                    _currentMode = null;
                }

                _diagnostics?.Dispose();
            }
            catch (Exception ex)
            {
                Logger.Error("Error disposing ModeManager", ex);
            }

            Logger.Info("ModeManager disposed");
        }
    }
}