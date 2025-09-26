using System;
using System.Threading;
using System.Threading.Tasks;
using WootMouseRemap.Diagnostics;

namespace WootMouseRemap.Core
{
    /// <summary>
    /// Thread-safe mode controller with proper synchronization and error handling
    /// </summary>
    public sealed class ThreadSafeModeController : IDisposable
    {
        private readonly object _lockObject = new();
        private readonly ModeDiagnostics _diagnostics;
        private readonly SemaphoreSlim _transitionSemaphore;
        private volatile InputMode _currentMode;
        private volatile bool _disposed;
    private string? _configFilePath;

    public event Action<InputMode>? ModeChanged;

        public InputMode Current 
        { 
            get 
            { 
                lock (_lockObject)
                {
                    return _currentMode;
                }
            } 
        }

        public ThreadSafeModeController(string configFilePath, ModeDiagnostics? diagnostics = null)
        {
            _configFilePath = configFilePath ?? "mode.json";
            _diagnostics = diagnostics ?? new ModeDiagnostics();
            _transitionSemaphore = new SemaphoreSlim(1, 1);
            _currentMode = InputMode.Native; // Safe default
            
            LoadMode();
            
            Logger.Info("ThreadSafeModeController initialized with mode: {Mode}", _currentMode);
        }

        /// <summary>
        /// Safely switches to the specified mode with proper error handling and rollback
        /// </summary>
        public async Task<bool> SwitchModeSafeAsync(InputMode newMode, TimeSpan? timeout = null)
        {
            if (_disposed)
            {
                Logger.Warn("Attempted to switch mode on disposed controller");
                return false;
            }

            var actualTimeout = timeout ?? TimeSpan.FromSeconds(10);
            var oldMode = _currentMode;

            // Prevent concurrent mode switches
            if (!await _transitionSemaphore.WaitAsync(actualTimeout))
            {
                Logger.Error("Mode switch timeout: {OldMode} -> {NewMode}", oldMode, newMode);
                _diagnostics?.LogModeTransition(oldMode, newMode, false, new TimeoutException("Mode switch timeout"));
                return false;
            }

            try
            {
                lock (_lockObject)
                {
                    // Check if already in target mode
                    if (_currentMode == newMode)
                    {
                        Logger.Info("Already in mode {Mode}, no switch needed", newMode);
                        return true;
                    }
                }

                Logger.Info("Starting mode transition: {OldMode} -> {NewMode}", oldMode, newMode);

                // Validate the new mode
                if (!IsValidMode(newMode))
                {
                    var ex = new ArgumentException($"Invalid mode: {newMode}");
                    Logger.Error("Mode validation failed", ex);
                    _diagnostics?.LogModeTransition(oldMode, newMode, false, ex);
                    return false;
                }

                // Perform the transition
                bool success = await PerformModeTransition(oldMode, newMode);
                
                if (success)
                {
                    lock (_lockObject)
                    {
                        _currentMode = newMode;
                    }
                    
                    // Update diagnostics
                    _diagnostics?.UpdateSystemState("CurrentMode", newMode);
                    _diagnostics?.LogModeTransition(oldMode, newMode, true);
                    
                    // Save to file
                    SaveMode();
                    
                    // Notify listeners
                    NotifyModeChanged(newMode);
                    
                    Logger.Info("Mode transition completed: {OldMode} -> {NewMode}", oldMode, newMode);
                }
                else
                {
                    Logger.Error("Mode transition failed: {OldMode} -> {NewMode}", oldMode, newMode);
                    _diagnostics?.LogModeTransition(oldMode, newMode, false, new InvalidOperationException("Mode transition failed"));
                }

                return success;
            }
            catch (Exception ex)
            {
                Logger.Error("Error during mode transition: {OldMode} -> {NewMode}", oldMode, newMode, ex);
                _diagnostics?.LogModeTransition(oldMode, newMode, false, ex);
                
                // Attempt rollback
                try
                {
                    await AttemptRollback(oldMode);
                }
                catch (Exception rollbackEx)
                {
                    Logger.Error("Rollback failed", rollbackEx);
                }
                
                return false;
            }
            finally
            {
                _transitionSemaphore.Release();
            }
        }

        /// <summary>
        /// Synchronous version for backward compatibility
        /// </summary>
        public bool Apply(InputMode mode)
        {
            try
            {
                var task = SwitchModeSafeAsync(mode);
                return task.GetAwaiter().GetResult();
            }
            catch (Exception ex)
            {
                Logger.Error("Error in synchronous Apply({Mode})", mode, ex);
                return false;
            }
        }

        /// <summary>
        /// Toggles between available modes safely
        /// </summary>
        public async Task<bool> ToggleNextAsync(bool includePassthrough = true)
        {
            if (_disposed) return false;

            var current = Current;
            InputMode next;

            switch (current)
            {
                case InputMode.Native:
                    next = InputMode.MnKConvert;
                    break;
                case InputMode.MnKConvert:
                    next = includePassthrough ? InputMode.ControllerPass : InputMode.Native;
                    break;
                case InputMode.ControllerPass:
                    next = InputMode.Native;
                    break;
                default:
                    next = InputMode.Native; // Safe fallback
                    break;
            }

            return await SwitchModeSafeAsync(next);
        }

        /// <summary>
        /// Gets diagnostic information about the current mode system state
        /// </summary>
        public ModeSystemState GetSystemState()
        {
            lock (_lockObject)
            {
                return new ModeSystemState
                {
                    CurrentMode = _currentMode,
                    IsTransitioning = _transitionSemaphore.CurrentCount == 0,
                    ConfigFilePath = _configFilePath ?? string.Empty,
                    IsDisposed = _disposed
                };
            }
        }

        private async Task<bool> PerformModeTransition(InputMode fromMode, InputMode toMode)
        {
            // This is where the actual mode transition logic would go
            // For now, we'll simulate a transition that could fail
            await Task.Delay(50); // Simulate some work
            
            // In a real implementation, this would coordinate with other components
            // like ModeManager, XInputPassthrough, etc.
            return true;
        }

        private async Task AttemptRollback(InputMode originalMode)
        {
            Logger.Warn("Attempting rollback to mode: {OriginalMode}", originalMode);
            
            try
            {
                // Simulate async rollback operation 
                await Task.Yield();
                
                // Simple rollback - just restore the original mode
                lock (_lockObject)
                {
                    _currentMode = originalMode;
                }
                
                _diagnostics?.UpdateSystemState("CurrentMode", originalMode);
                NotifyModeChanged(originalMode);
                
                Logger.Info("Rollback successful to mode: {Mode}", originalMode);
            }
            catch (Exception ex)
            {
                Logger.Error("Rollback failed", ex);
                throw;
            }
        }

        private bool IsValidMode(InputMode mode)
        {
            return Enum.IsDefined(typeof(InputMode), mode);
        }

        private void LoadMode()
        {
            try
            {
                if (System.IO.File.Exists(_configFilePath))
                {
                    var content = System.IO.File.ReadAllText(_configFilePath);
                    if (int.TryParse(content.Trim(), out int modeValue) && 
                        Enum.IsDefined(typeof(InputMode), modeValue))
                    {
                        lock (_lockObject)
                        {
                            _currentMode = (InputMode)modeValue;
                        }
                        Logger.Info("Loaded mode from file: {Mode}", _currentMode);
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Error("Error loading mode from file", ex);
                // Keep default mode
            }
        }

        private void SaveMode()
        {
            try
            {
                var modeToSave = Current;
                if (!string.IsNullOrEmpty(_configFilePath))
                    System.IO.File.WriteAllText(_configFilePath, ((int)modeToSave).ToString());
            }
            catch (Exception ex)
            {
                Logger.Error("Error saving mode to file", ex);
            }
        }

        private void NotifyModeChanged(InputMode newMode)
        {
            try
            {
                ModeChanged?.Invoke(newMode);
            }
            catch (Exception ex)
            {
                Logger.Error("Error in ModeChanged event notification", ex);
            }
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            try
            {
                _transitionSemaphore?.Dispose();
            }
            catch (Exception ex)
            {
                Logger.Error("Error disposing transition semaphore", ex);
            }

            Logger.Info("ThreadSafeModeController disposed");
        }
    }

    public class ModeSystemState
    {
        public InputMode CurrentMode { get; set; }
        public bool IsTransitioning { get; set; }
        public string ConfigFilePath { get; set; } = string.Empty;
        public bool IsDisposed { get; set; }
    }
}