using System;
using System.Drawing;
using System.Diagnostics;
using System.Windows.Forms;
using System.Collections.Generic;
using System.Threading;
using System.Linq;
using WootMouseRemap.Core;
using WootMouseRemap.Diagnostics;
using WootMouseRemap.Features;

namespace WootMouseRemap.Modes
{
    /// <summary>
    /// Enhanced controller output mode with comprehensive monitoring,
    /// configurable parameters, and robust error recovery mechanisms.
    /// Provides full feature parity with ControllerPassMode.
    /// </summary>
    public class MnKConvertMode : IModeHandler, IDisposable
    {
        #region Configuration Constants
        
        private const int IDLE_RESET_THRESHOLD_MS = 18;
        private const byte MAX_TRIGGER_VALUE = 255;
        private const int DEFAULT_ERROR_BACKOFF_DELAY_MS = 100;
        private const int DEFAULT_VIRTUAL_PAD_RETRY_DELAY_MS = 200;
        private const int MAX_CONSECUTIVE_ERRORS = 3;
        private const int HEALTH_CHECK_INTERVAL_MS = 2000;
        private const int ANTI_RECOIL_LOG_COOLDOWN_MS = 1000;
        private const int EVENT_HISTORY_MAX_COUNT = 200;
        
        #endregion

        #region Core Properties & Fields
        
        public InputMode Mode => InputMode.MnKConvert;
        public bool ShouldSuppressInput => true;

        private readonly Xbox360ControllerWrapper _pad;
        private readonly StickMapper _mapper;
        private readonly ProfileManager _profiles;
        private readonly Telemetry _telemetry;
        private readonly XInputPassthrough _xpass;
        private readonly ModeStateValidator? _validator;
        private readonly AntiRecoil? _antiRecoil;
        private readonly object _lockObject = new();
        private volatile bool _disposed;
        private readonly Action<InputMode>? _requestModeChange;
        private bool _autoPassthroughArmed = false; // Disable auto-switch to passthrough by default to avoid interrupting mouse->stick mapping

        private DateTime _lastMouseMoveUtc = DateTime.UtcNow;
        private long _lastWheelTick;
        private DateTime _lastAntiRecoilLog = DateTime.MinValue;

        #endregion

        #region Performance & Diagnostics
        
        private readonly PerformanceMetrics _performanceMetrics = new();
        private readonly System.Threading.Timer _healthCheckTimer;
        private readonly Queue<ModeEvent> _eventHistory = new();
        private int _consecutiveErrors;
        private DateTime _lastErrorTime = DateTime.MinValue;
        private DateTime _lastSuccessfulOperation = DateTime.UtcNow;
        private long _operationCount;
        private long _successfulOperations;

        #endregion

        #region Configuration Properties
        
        public TimeSpan ErrorBackoffDelay { get; set; } = TimeSpan.FromMilliseconds(DEFAULT_ERROR_BACKOFF_DELAY_MS);
        public TimeSpan VirtualPadRetryDelay { get; set; } = TimeSpan.FromMilliseconds(DEFAULT_VIRTUAL_PAD_RETRY_DELAY_MS);
        public int MaxConsecutiveErrors { get; set; } = MAX_CONSECUTIVE_ERRORS;
        public bool EnablePerformanceMonitoring { get; set; } = true;
        public bool EnableAutomaticRecovery { get; set; } = true;
        public int IdleResetThresholdMs { get; set; } = IDLE_RESET_THRESHOLD_MS;

        #endregion

        #region Events
        
        public event Action<PerformanceMetrics>? PerformanceUpdated;
        public event Action<string, Exception?>? ErrorOccurred;
        public event Action<string>? StatusChanged;
        public event Action<HealthCheckResult>? HealthCheckCompleted;

        #endregion

        public MnKConvertMode(
            Xbox360ControllerWrapper pad,
            StickMapper mapper,
            ProfileManager profiles,
            Telemetry telemetry,
            XInputPassthrough xpass,
            ModeStateValidator? validator = null,
            AntiRecoil? antiRecoil = null,
            Action<InputMode>? requestModeChange = null)
        {
            _pad = pad ?? throw new ArgumentNullException(nameof(pad));
            _mapper = mapper ?? throw new ArgumentNullException(nameof(mapper));
            _profiles = profiles ?? throw new ArgumentNullException(nameof(profiles));
            _telemetry = telemetry ?? throw new ArgumentNullException(nameof(telemetry));
            _xpass = xpass ?? throw new ArgumentNullException(nameof(xpass));
            _validator = validator;
            _antiRecoil = antiRecoil;
            _requestModeChange = requestModeChange;

            // Initialize health check timer
            _healthCheckTimer = new System.Threading.Timer(
                PerformHealthCheck, 
                null, 
                TimeSpan.FromMilliseconds(HEALTH_CHECK_INTERVAL_MS),
                TimeSpan.FromMilliseconds(HEALTH_CHECK_INTERVAL_MS)
            );
            
            Logger.Info("MnKConvertMode initialized with comprehensive monitoring and health checks");
            RecordEvent("Initialized", "Mode handler created with advanced diagnostics");
        }

        public void OnModeEntered(InputMode previousMode)
        {
            if (_disposed) return;

            var stopwatch = Stopwatch.StartNew();
            try
            {
                Interlocked.Increment(ref _operationCount);
                Logger.Info("Entering MnKConvert mode (from {PreviousMode})", previousMode);
                RecordEvent("ModeEntered", $"Transitioning from {previousMode}");

                lock (_lockObject)
                {
                    // Ensure XInput passthrough is stopped
                    if (_xpass.IsRunning)
                    {
                        try
                        {
                            _xpass.Stop();
                            Logger.Info("Stopped XInput passthrough when entering MnKConvert mode");
                            RecordEvent("PassthroughStopped", "XInput passthrough stopped on mode entry");
                        }
                        catch (Exception ex)
                        {
                            Logger.Error("Error stopping XInput passthrough", ex);
                            RecordEvent("Error", $"Failed to stop XInput passthrough: {ex.Message}");
                        }
                    }

                    // Enable input suppression for this mode
                    LowLevelHooks.Suppress = true;
                    Logger.Info("Input suppression enabled for MnKConvert mode");

                    // Ensure virtual pad connection with retry logic
                    if (!EnsureVirtualPadConnection())
                    {
                        Logger.Warn("Virtual pad connection failed - controller output may not work correctly");
                        RecordEvent("Warning", "Virtual pad connection failed");
                    }

                    // Reset pad state
                    try
                    {
                        SafePadOperation(() => _pad.ResetAll(), "ResetAll");
                    }
                    catch (Exception ex)
                    {
                        Logger.Error("Error resetting controller pad", ex);
                        RecordEvent("Error", $"Pad reset failed: {ex.Message}");
                    }

                    // Note: do NOT re-arm auto-switching automatically to avoid unwanted mode changes
                }

                // Comprehensive state validation
                PerformStateValidation();

                _lastSuccessfulOperation = DateTime.UtcNow;
                Interlocked.Increment(ref _successfulOperations);
                Logger.Info("MnKConvert mode entered successfully");
                RecordEvent("Success", "Mode entry completed successfully");

                StatusChanged?.Invoke("MnKConvert mode active");
            }
            catch (Exception ex)
            {
                _consecutiveErrors++;
                _lastErrorTime = DateTime.UtcNow;
                Logger.Error("Error entering MnKConvert mode", ex);
                RecordEvent("Error", $"Mode entry failed: {ex.Message}");
                ErrorOccurred?.Invoke("OnModeEntered", ex);

                if (EnableAutomaticRecovery && _consecutiveErrors < MaxConsecutiveErrors)
                {
                    AttemptAutomaticRecovery("OnModeEntered");
                }
            }
            finally
            {
                if (EnablePerformanceMonitoring)
                {
                    stopwatch.Stop();
                    _performanceMetrics.RecordOperation("OnModeEntered", stopwatch.Elapsed);
                    PerformanceUpdated?.Invoke(_performanceMetrics);
                }
            }
        }

        public void OnModeExited(InputMode nextMode)
        {
            if (_disposed) return;

            var stopwatch = Stopwatch.StartNew();
            try
            {
                Interlocked.Increment(ref _operationCount);
                Logger.Info("Exiting MnKConvert mode (to {NextMode})", nextMode);
                RecordEvent("ModeExited", $"Transitioning to {nextMode}");

                lock (_lockObject)
                {
                    // Reset controller state when exiting
                    try
                    {
                        SafePadOperation(() => _pad.ResetAll(), "ResetAll");
                        Logger.Info("Controller pad reset during mode exit");
                        RecordEvent("Cleanup", "Controller pad reset completed");
                    }
                    catch (Exception ex)
                    {
                        Logger.Error("Error resetting controller when exiting mode", ex);
                        RecordEvent("Error", $"Pad reset on exit failed: {ex.Message}");
                    }
                }

                _lastSuccessfulOperation = DateTime.UtcNow;
                Interlocked.Increment(ref _successfulOperations);
                Logger.Info("MnKConvert mode exited successfully");
                RecordEvent("Success", "Mode exit completed successfully");

                StatusChanged?.Invoke($"Exited to {nextMode}");
            }
            catch (Exception ex)
            {
                _consecutiveErrors++;
                _lastErrorTime = DateTime.UtcNow;
                Logger.Error("Error exiting MnKConvert mode", ex);
                RecordEvent("Error", $"Mode exit failed: {ex.Message}");
                ErrorOccurred?.Invoke("OnModeExited", ex);
            }
            finally
            {
                if (EnablePerformanceMonitoring)
                {
                    stopwatch.Stop();
                    _performanceMetrics.RecordOperation("OnModeExited", stopwatch.Elapsed);
                    PerformanceUpdated?.Invoke(_performanceMetrics);
                }
            }
        }

        public void OnKey(int vk, bool down)
        {
            if (_disposed) return;

            var stopwatch = EnablePerformanceMonitoring ? Stopwatch.StartNew() : null;
            try
            {
                lock (_lockObject)
                {
                    _mapper.UpdateKey(vk, down);

                    if (_profiles.Current.WasdToLeftStick && IsWASD(vk))
                    {
                        // Always recompute WASD to left stick values to prevent stale state
                        var (lx, ly) = _mapper.WasdToLeftStick();
                        SafePadOperation(() => _pad.SetLeftStick(lx, ly), $"SetLeftStick({lx}, {ly})");
                        return;
                    }

                    if (!down) return;

                    if (_profiles.Current.KeyMap.TryGetValue(vk, out var control))
                    {
                        ExecuteControl(control, down);
                    }
                }

                _lastSuccessfulOperation = DateTime.UtcNow;
                Interlocked.Increment(ref _successfulOperations);
                _consecutiveErrors = 0; // Reset error count on successful operation
            }
            catch (Exception ex)
            {
                _consecutiveErrors++;
                _lastErrorTime = DateTime.UtcNow;
                Logger.Error("Error processing key input {Vk} (down: {Down})", vk, down, ex);
                RecordEvent("Error", $"Key processing failed: vk={vk}, down={down}, error={ex.Message}");
                ErrorOccurred?.Invoke("OnKey", ex);
            }
            finally
            {
                if (EnablePerformanceMonitoring && stopwatch != null)
                {
                    stopwatch.Stop();
                    _performanceMetrics.RecordOperation("OnKey", stopwatch.Elapsed);
                }
            }
        }

        public void OnMouseButton(MouseInput button, bool down)
        {
            if (_disposed) return;

            var stopwatch = EnablePerformanceMonitoring ? Stopwatch.StartNew() : null;
            try
            {
                lock (_lockObject)
                {
                    // Handle anti-recoil activation on left mouse button with reduced logging
                    if (button == MouseInput.Left && _antiRecoil != null)
                    {
                        var shouldLog = (DateTime.UtcNow - _lastAntiRecoilLog).TotalMilliseconds > ANTI_RECOIL_LOG_COOLDOWN_MS;
                        if (shouldLog)
                        {
                            Logger.Info("Anti-recoil {State}", down ? "activated" : "deactivated");
                            _lastAntiRecoilLog = DateTime.UtcNow;
                        }

                        if (down)
                        {
                            _antiRecoil.OnShootingStarted();
                        }
                        else
                        {
                            _antiRecoil.OnShootingStopped();
                        }
                    }

                    if (_profiles.Current.MouseMap.TryGetValue(button, out var control))
                    {
                        ExecuteControl(control, down);
                    }
                }

                _lastSuccessfulOperation = DateTime.UtcNow;
                Interlocked.Increment(ref _successfulOperations);
                _consecutiveErrors = 0; // Reset error count on successful operation
            }
            catch (Exception ex)
            {
                _consecutiveErrors++;
                _lastErrorTime = DateTime.UtcNow;
                Logger.Error("Error processing mouse button {Button} (down: {Down})", button, down, ex);
                RecordEvent("Error", $"Mouse button processing failed: button={button}, down={down}, error={ex.Message}");
                ErrorOccurred?.Invoke("OnMouseButton", ex);
            }
            finally
            {
                if (EnablePerformanceMonitoring && stopwatch != null)
                {
                    stopwatch.Stop();
                    _performanceMetrics.RecordOperation("OnMouseButton", stopwatch.Elapsed);
                }
            }
        }

        public void OnMouseMove(int dx, int dy)
        {
            if (_disposed) return;

            var stopwatch = EnablePerformanceMonitoring ? Stopwatch.StartNew() : null;
            try
            {
                try { Logger.Info("MnKConvertMode.OnMouseMove dx={Dx} dy={Dy}", dx, dy); } catch { }

                lock (_lockObject)
                {
                    _lastMouseMoveUtc = DateTime.UtcNow;
                    _telemetry.RawDx += dx;
                    _telemetry.RawDy += dy;

                    // Apply anti-recoil processing if available
                    // Note: Anti-recoil adjustments are independent of WASD left stick mapping
                    float processedDx = dx;
                    float processedDy = dy;

                    if (_antiRecoil != null)
                    {
                        (processedDx, processedDy) = _antiRecoil.ProcessMouseMovement(dx, dy);
                    }

                    var (x, y) = _mapper.MouseToRightStick((int)processedDx, (int)processedDy);
                    try { Logger.Info("Mapped to stick x={X} y={Y}", x, y); } catch { }
                    SafePadOperation(() => _pad.SetRightStick(x, y), $"SetRightStick({x}, {y})");
                }

                _lastSuccessfulOperation = DateTime.UtcNow;
                Interlocked.Increment(ref _successfulOperations);
                _consecutiveErrors = 0; // Reset error count on successful operation
            }
            catch (Exception ex)
            {
                _consecutiveErrors++;
                _lastErrorTime = DateTime.UtcNow;
                Logger.Error("Error processing mouse move ({Dx}, {Dy})", dx, dy, ex);
                RecordEvent("Error", $"Mouse move processing failed: dx={dx}, dy={dy}, error={ex.Message}");
                ErrorOccurred?.Invoke("OnMouseMove", ex);
            }
            finally
            {
                if (EnablePerformanceMonitoring && stopwatch != null)
                {
                    stopwatch.Stop();
                    _performanceMetrics.RecordOperation("OnMouseMove", stopwatch.Elapsed);
                }
            }
        }

        public void OnWheel(int delta)
        {
            if (_disposed) return;

            var stopwatch = EnablePerformanceMonitoring ? Stopwatch.StartNew() : null;
            try
            {
                lock (_lockObject)
                {
                    _lastWheelTick = Environment.TickCount64;

                    if (delta > 0 && _profiles.Current.MouseMap.TryGetValue(MouseInput.ScrollUp, out var upControl))
                    {
                        ExecuteControl(upControl, true);
                        ExecuteControl(upControl, false);
                    }
                    else if (delta < 0 && _profiles.Current.MouseMap.TryGetValue(MouseInput.ScrollDown, out var downControl))
                    {
                        ExecuteControl(downControl, true);
                        ExecuteControl(downControl, false);
                    }
                }

                _lastSuccessfulOperation = DateTime.UtcNow;
                Interlocked.Increment(ref _successfulOperations);
                _consecutiveErrors = 0; // Reset error count on successful operation
            }
            catch (Exception ex)
            {
                _consecutiveErrors++;
                _lastErrorTime = DateTime.UtcNow;
                Logger.Error("Error processing wheel input (delta: {Delta})", delta, ex);
                RecordEvent("Error", $"Wheel processing failed: delta={delta}, error={ex.Message}");
                ErrorOccurred?.Invoke("OnWheel", ex);
            }
            finally
            {
                if (EnablePerformanceMonitoring && stopwatch != null)
                {
                    stopwatch.Stop();
                    _performanceMetrics.RecordOperation("OnWheel", stopwatch.Elapsed);
                }
            }
        }

        public void OnControllerConnected(int index)
        {
            if (_disposed) return;

            var stopwatch = Stopwatch.StartNew();
            try
            {
                Interlocked.Increment(ref _operationCount);
                Logger.Info("Physical controller connected at index {Index} in output mode", index);
                RecordEvent("ControllerConnected", $"Controller at index {index}");

                lock (_lockObject)
                {
                    _xpass.SetPlayerIndex(index);
                    
                    // Auto-switch to passthrough if armed and callback provided
                    if (_autoPassthroughArmed && _requestModeChange != null)
                    {
                        _autoPassthroughArmed = false; // Prevent multiple rapid switches
                        try
                        {
                            Logger.Info("Requesting automatic switch to ControllerPass after controller connect");
                            RecordEvent("ModeChangeRequest", "Auto-switching to passthrough mode");
                            _requestModeChange(InputMode.ControllerPass);
                        }
                        catch (Exception ex)
                        {
                            Logger.Error("Failed automatic passthrough switch request", ex);
                            RecordEvent("Error", $"Auto-switch failed: {ex.Message}");
                        }
                    }
                }

                _lastSuccessfulOperation = DateTime.UtcNow;
                Interlocked.Increment(ref _successfulOperations);
                _consecutiveErrors = 0; // Reset error count on successful operation

                StatusChanged?.Invoke($"Controller P{index + 1} connected");
            }
            catch (Exception ex)
            {
                _consecutiveErrors++;
                _lastErrorTime = DateTime.UtcNow;
                Logger.Error("Error handling controller connection at index {Index}", index, ex);
                RecordEvent("Error", $"Controller connection handling failed: {ex.Message}");
                ErrorOccurred?.Invoke("OnControllerConnected", ex);
            }
            finally
            {
                if (EnablePerformanceMonitoring)
                {
                    stopwatch.Stop();
                    _performanceMetrics.RecordOperation("OnControllerConnected", stopwatch.Elapsed);
                    PerformanceUpdated?.Invoke(_performanceMetrics);
                }
            }
        }

        public void OnControllerDisconnected(int index)
        {
            if (_disposed) return;

            var stopwatch = Stopwatch.StartNew();
            try
            {
                Interlocked.Increment(ref _operationCount);
                Logger.Info("Physical controller disconnected at index {Index}", index);
                RecordEvent("ControllerDisconnected", $"Controller at index {index}");

                _lastSuccessfulOperation = DateTime.UtcNow;
                Interlocked.Increment(ref _successfulOperations);

                StatusChanged?.Invoke("Controller disconnected");
            }
            catch (Exception ex)
            {
                _consecutiveErrors++;
                _lastErrorTime = DateTime.UtcNow;
                Logger.Error("Error handling controller disconnection at index {Index}", index, ex);
                RecordEvent("Error", $"Controller disconnection handling failed: {ex.Message}");
                ErrorOccurred?.Invoke("OnControllerDisconnected", ex);
            }
            finally
            {
                if (EnablePerformanceMonitoring)
                {
                    stopwatch.Stop();
                    _performanceMetrics.RecordOperation("OnControllerDisconnected", stopwatch.Elapsed);
                    PerformanceUpdated?.Invoke(_performanceMetrics);
                }
            }
        }

        public void Update()
        {
            if (_disposed) return;

            var stopwatch = EnablePerformanceMonitoring ? Stopwatch.StartNew() : null;
            try
            {
                lock (_lockObject)
                {
                    var idleMs = (DateTime.UtcNow - _lastMouseMoveUtc).TotalMilliseconds;
                    if (idleMs > IdleResetThresholdMs)
                    {
                        SafePadOperation(() => _pad.SetRightStick(0, 0), "SetRightStick(0, 0)");
                        try { _mapper.Curve.ResetSmoothing(); } catch { }
                    }

                    // Periodic virtual pad health check
                    if (!_pad.IsConnected && EnableAutomaticRecovery)
                    {
                        RecordEvent("Warning", "Virtual pad disconnected during update");
                        EnsureVirtualPadConnection();
                    }
                }

                _lastSuccessfulOperation = DateTime.UtcNow;
                Interlocked.Increment(ref _successfulOperations);
                _consecutiveErrors = 0; // Reset error count on successful update
            }
            catch (Exception ex)
            {
                _consecutiveErrors++;
                _lastErrorTime = DateTime.UtcNow;
                Logger.Error("Error during MnKConvert mode update", ex);
                RecordEvent("Error", $"Update failed: {ex.Message}");
                ErrorOccurred?.Invoke("Update", ex);

                if (EnableAutomaticRecovery && _consecutiveErrors < MaxConsecutiveErrors)
                {
                    AttemptAutomaticRecovery("Update");
                }
            }
            finally
            {
                if (EnablePerformanceMonitoring && stopwatch != null)
                {
                    stopwatch.Stop();
                    _performanceMetrics.RecordOperation("Update", stopwatch.Elapsed);
                    PerformanceUpdated?.Invoke(_performanceMetrics);
                }
            }
        }

        public string GetStatusText()
        {
            if (_disposed) return "Mode: MnKConvert (Disposed)";

            try
            {
                var status = $"Mode: {Mode}";

                lock (_lockObject)
                {
                    status += $" | ViGEm: {(_pad.IsConnected ? "OK" : "Not Connected")} | XInput Passthrough: {(_xpass.IsRunning ? "Running" : "Stopped")}";

                    // Add performance metrics
                    if (EnablePerformanceMonitoring)
                    {
                        var successRate = _operationCount > 0 ? (_successfulOperations * 100.0 / _operationCount) : 100.0;
                        status += $" | Success: {successRate:F1}%";

                        if (_consecutiveErrors > 0)
                        {
                            status += $" | Errors: {_consecutiveErrors}";
                        }
                    }
                }

                return status;
            }
            catch (Exception ex)
            {
                Logger.Error("Error getting status text", ex);
                return "Mode: MnKConvert (Error)";
            }
        }

        #region Enhanced Helper Methods

        private bool IsWASD(int vk)
        {
            return vk == (int)Keys.W || vk == (int)Keys.A || vk == (int)Keys.S || vk == (int)Keys.D;
        }

        private bool EnsureVirtualPadConnection()
        {
            try
            {
                if (!_pad.IsConnected)
                {
                    Logger.Info("Virtual pad not connected - attempting to connect");
                    _pad.Connect();

                    // Wait briefly for connection to establish
                    Thread.Sleep((int)VirtualPadRetryDelay.TotalMilliseconds);

                    if (_pad.IsConnected)
                    {
                        Logger.Info("Virtual pad connected successfully");
                        RecordEvent("Recovery", "Virtual pad reconnected");
                        return true;
                    }
                    else
                    {
                        Logger.Error("Virtual pad connection failed");
                        RecordEvent("Error", "Virtual pad connection failed");
                        return false;
                    }
                }
                return true;
            }
            catch (Exception ex)
            {
                Logger.Error("Virtual pad connection attempt failed: {Message}", ex.Message, ex);
                RecordEvent("Error", $"Virtual pad connection attempt failed: {ex.Message}");
                return false;
            }
        }

        private void SafePadOperation(Action operation, string operationName)
        {
            try
            {
                operation();
            }
            catch (Exception ex)
            {
                Logger.Error("Pad operation '{OperationName}' failed: {Message}", operationName, ex.Message, ex);
                RecordEvent("Error", $"Pad operation failed: {operationName}");
                
                // Attempt recovery if pad becomes disconnected
                if (!_pad.IsConnected && EnableAutomaticRecovery)
                {
                    EnsureVirtualPadConnection();
                }
            }
        }

        private void ExecuteControl(Xbox360Control control, bool down)
        {
            try
            {
                if (TryConvertToXbox360Button(control, out var btn))
                {
                    SafePadOperation(() => _pad.SetButton(btn, down), $"SetButton({btn}, {down})");
                }
                else
                {
                    switch (control)
                    {
                        case Xbox360Control.LeftTrigger:
                            SafePadOperation(() => _pad.SetTrigger(false, down ? MAX_TRIGGER_VALUE : (byte)0), 
                                           $"SetTrigger(left, {(down ? MAX_TRIGGER_VALUE : 0)})");
                            break;
                        case Xbox360Control.RightTrigger:
                            SafePadOperation(() => _pad.SetTrigger(true, down ? MAX_TRIGGER_VALUE : (byte)0), 
                                           $"SetTrigger(right, {(down ? MAX_TRIGGER_VALUE : 0)})");
                            break;
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Error("Error executing control {Control} (down: {Down})", control, down, ex);
                RecordEvent("Error", $"Control execution failed: {control}, down={down}, error={ex.Message}");
                ErrorOccurred?.Invoke("ExecuteControl", ex);
            }
        }

        private static bool TryConvertToXbox360Button(Xbox360Control control, out Nefarius.ViGEm.Client.Targets.Xbox360.Xbox360Button button)
        {
            switch (control)
            {
                case Xbox360Control.A:
                    button = Nefarius.ViGEm.Client.Targets.Xbox360.Xbox360Button.A;
                    return true;
                case Xbox360Control.B:
                    button = Nefarius.ViGEm.Client.Targets.Xbox360.Xbox360Button.B;
                    return true;
                case Xbox360Control.X:
                    button = Nefarius.ViGEm.Client.Targets.Xbox360.Xbox360Button.X;
                    return true;
                case Xbox360Control.Y:
                    button = Nefarius.ViGEm.Client.Targets.Xbox360.Xbox360Button.Y;
                    return true;
                case Xbox360Control.LeftBumper:
                    button = Nefarius.ViGEm.Client.Targets.Xbox360.Xbox360Button.LeftShoulder;
                    return true;
                case Xbox360Control.RightBumper:
                    button = Nefarius.ViGEm.Client.Targets.Xbox360.Xbox360Button.RightShoulder;
                    return true;
                case Xbox360Control.Start:
                    button = Nefarius.ViGEm.Client.Targets.Xbox360.Xbox360Button.Start;
                    return true;
                case Xbox360Control.Back:
                    button = Nefarius.ViGEm.Client.Targets.Xbox360.Xbox360Button.Back;
                    return true;
                case Xbox360Control.LeftStick:
                    button = Nefarius.ViGEm.Client.Targets.Xbox360.Xbox360Button.LeftThumb;
                    return true;
                case Xbox360Control.RightStick:
                    button = Nefarius.ViGEm.Client.Targets.Xbox360.Xbox360Button.RightThumb;
                    return true;
                case Xbox360Control.DpadUp:
                    button = Nefarius.ViGEm.Client.Targets.Xbox360.Xbox360Button.Up;
                    return true;
                case Xbox360Control.DpadDown:
                    button = Nefarius.ViGEm.Client.Targets.Xbox360.Xbox360Button.Down;
                    return true;
                case Xbox360Control.DpadLeft:
                    button = Nefarius.ViGEm.Client.Targets.Xbox360.Xbox360Button.Left;
                    return true;
                case Xbox360Control.DpadRight:
                    button = Nefarius.ViGEm.Client.Targets.Xbox360.Xbox360Button.Right;
                    return true;
                default:
                    button = Nefarius.ViGEm.Client.Targets.Xbox360.Xbox360Button.A;
                    return false;
            }
        }

        private void PerformStateValidation()
        {
            if (_validator == null) return;

            try
            {
                var context = BuildValidationContext();
                var validationResult = _validator.ValidateSystemState(context);

                if (!validationResult.IsValid)
                {
                    var warnings = validationResult.Issues.FindAll(i => i.Severity == ValidationSeverity.Warning);
                    if (warnings.Count > 0)
                    {
                        var warningMessages = string.Join(", ", warnings.ConvertAll(w => w.Message));
                        Logger.Warn("MnKConvert mode validation warnings: {WarningMessages}", warningMessages);
                        RecordEvent("ValidationWarning", warningMessages);
                    }

                    var errors = validationResult.Issues.FindAll(i => i.Severity == ValidationSeverity.Error);
                    if (errors.Count > 0)
                    {
                        var errorMessages = string.Join(", ", errors.ConvertAll(e => e.Message));
                        Logger.Error("MnKConvert mode validation errors: {ErrorMessages}", errorMessages);
                        RecordEvent("ValidationError", errorMessages);
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Error("Error during state validation", ex);
                RecordEvent("Error", $"State validation failed: {ex.Message}");
            }
        }

        private void AttemptAutomaticRecovery(string operation)
        {
            try
            {
                Logger.Info("Attempting automatic recovery for {Operation} (attempt {Attempt}/{MaxAttempts})", operation, _consecutiveErrors, MaxConsecutiveErrors);
                RecordEvent("Recovery", $"Auto-recovery attempt for {operation}");

                // Wait for backoff period
                Thread.Sleep((int)ErrorBackoffDelay.TotalMilliseconds * _consecutiveErrors);

                // Attempt to re-establish virtual pad connection
                if (!_pad.IsConnected)
                {
                    EnsureVirtualPadConnection();
                }

                Logger.Info("Automatic recovery attempt completed");
                RecordEvent("Recovery", "Auto-recovery completed");
            }
            catch (Exception ex)
            {
                Logger.Error("Automatic recovery failed for {Operation}", operation, ex);
                RecordEvent("Error", $"Auto-recovery failed: {ex.Message}");
            }
        }

        private void PerformHealthCheck(object? state)
        {
            if (_disposed) return;

            try
            {
                var healthResult = new HealthCheckResult
                {
                    Timestamp = DateTime.UtcNow,
                    IsHealthy = true,
                    Issues = new List<string>()
                };

                // Check error rate
                var errorRate = _operationCount > 0 ? (_operationCount - _successfulOperations) * 100.0 / _operationCount : 0.0;
                if (errorRate > 10.0)
                {
                    healthResult.IsHealthy = false;
                    healthResult.Issues.Add($"High error rate: {errorRate:F1}%");
                }

                // Check recent activity
                var timeSinceLastSuccess = DateTime.UtcNow - _lastSuccessfulOperation;
                if (timeSinceLastSuccess > TimeSpan.FromMinutes(5))
                {
                    healthResult.IsHealthy = false;
                    healthResult.Issues.Add($"No successful operations for {timeSinceLastSuccess.TotalMinutes:F1} minutes");
                }

                // Check consecutive errors
                if (_consecutiveErrors >= MaxConsecutiveErrors)
                {
                    healthResult.IsHealthy = false;
                    healthResult.Issues.Add($"Too many consecutive errors: {_consecutiveErrors}");
                }

                // Check component health
                if (!_pad.IsConnected)
                {
                    healthResult.Issues.Add("Virtual pad disconnected");
                }

                HealthCheckCompleted?.Invoke(healthResult);

                if (!healthResult.IsHealthy)
                {
                    Logger.Warn("Health check failed: {Issues}", string.Join(", ", healthResult.Issues));
                    RecordEvent("HealthCheck", $"Health issues: {string.Join(", ", healthResult.Issues)}");
                }
            }
            catch (Exception ex)
            {
                Logger.Error("Error during health check", ex);
                RecordEvent("Error", $"Health check failed: {ex.Message}");
            }
        }

        private void RecordEvent(string type, string message)
        {
            if (_disposed) return;

            try
            {
                var evt = new ModeEvent
                {
                    Timestamp = DateTime.UtcNow,
                    Type = type,
                    Message = message,
                    ThreadId = Thread.CurrentThread.ManagedThreadId
                };

                lock (_eventHistory)
                {
                    _eventHistory.Enqueue(evt);

                    // Keep only recent events to prevent memory growth
                    while (_eventHistory.Count > EVENT_HISTORY_MAX_COUNT)
                    {
                        _eventHistory.Dequeue();
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Error("Error recording event", ex);
            }
        }

        private ModeSystemContext BuildValidationContext()
        {
            return new ModeSystemContext
            {
                CurrentMode = Mode,
                SuppressionEnabled = LowLevelHooks.Suppress,
                PhysicalControllerConnected = null, // Not directly tracked in output mode
                XInputPassthroughRunning = _xpass?.IsRunning,
                ViGEmConnected = _pad?.IsConnected,
                LastTransition = DateTime.UtcNow,
                AdditionalState = new Dictionary<string, object>
                {
                    ["OperationCount"] = _operationCount,
                    ["SuccessfulOperations"] = _successfulOperations,
                    ["ConsecutiveErrors"] = _consecutiveErrors,
                    ["LastSuccessfulOperation"] = _lastSuccessfulOperation,
                    ["AutoPassthroughArmed"] = _autoPassthroughArmed,
                    ["PerformanceMonitoring"] = EnablePerformanceMonitoring,
                    ["AutomaticRecovery"] = EnableAutomaticRecovery
                }
            };
        }

        #endregion

        #region Supporting Classes (shared with ControllerPassMode)

        public class PerformanceMetrics
        {
            private readonly object _lock = new();
            private readonly Queue<OperationMetric> _recentOperations = new();
            private readonly Dictionary<string, TimeSpan> _operationTimes = new();
            
            public TimeSpan AverageProcessingTime { get; private set; }
            public TimeSpan MaxProcessingTime { get; private set; }
            public TimeSpan MinProcessingTime { get; private set; } = TimeSpan.MaxValue;
            public long TotalOperations { get; private set; }
            public DateTime LastUpdated { get; private set; }

            public void RecordOperation(string operation, TimeSpan duration)
            {
                lock (_lock)
                {
                    var metric = new OperationMetric
                    {
                        Operation = operation,
                        Duration = duration,
                        Timestamp = DateTime.UtcNow
                    };
                    
                    _recentOperations.Enqueue(metric);
                    
                    // Keep only recent operations
                    while (_recentOperations.Count > 100)
                    {
                        _recentOperations.Dequeue();
                    }
                    
                    TotalOperations++;
                    
                    // Update statistics
                    if (duration > MaxProcessingTime)
                        MaxProcessingTime = duration;
                    
                    if (duration < MinProcessingTime)
                        MinProcessingTime = duration;
                    
                    // Calculate average from recent operations
                    var totalTicks = _recentOperations.Sum(m => m.Duration.Ticks);
                    AverageProcessingTime = new TimeSpan(totalTicks / _recentOperations.Count);
                    
                    _operationTimes[operation] = duration;
                    LastUpdated = DateTime.UtcNow;
                }
            }

            public PerformanceMetrics Clone()
            {
                lock (_lock)
                {
                    return new PerformanceMetrics
                    {
                        AverageProcessingTime = AverageProcessingTime,
                        MaxProcessingTime = MaxProcessingTime,
                        MinProcessingTime = MinProcessingTime,
                        TotalOperations = TotalOperations,
                        LastUpdated = LastUpdated
                    };
                }
            }

            public Dictionary<string, TimeSpan> GetOperationTimes()
            {
                lock (_lock)
                {
                    return new Dictionary<string, TimeSpan>(_operationTimes);
                }
            }
        }

        public class OperationMetric
        {
            public string Operation { get; set; } = string.Empty;
            public TimeSpan Duration { get; set; }
            public DateTime Timestamp { get; set; }
        }

        public class ModeEvent
        {
            public DateTime Timestamp { get; set; }
            public string Type { get; set; } = string.Empty;
            public string Message { get; set; } = string.Empty;
            public int ThreadId { get; set; }
        }

        public class HealthCheckResult
        {
            public DateTime Timestamp { get; set; }
            public bool IsHealthy { get; set; }
            public List<string> Issues { get; set; } = new();
        }

        #endregion

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            Logger.Info("Disposing MnKConvertMode");
            RecordEvent("Disposing", "Mode handler disposal started");

            try
            {
                // Dispose health check timer
                _healthCheckTimer?.Dispose();

                lock (_lockObject)
                {
                    // Reset pad state
                    try
                    {
                        _pad?.ResetAll();
                        Logger.Info("Controller pad reset during disposal");
                        RecordEvent("Cleanup", "Controller pad reset during disposal");
                    }
                    catch (Exception ex)
                    {
                        Logger.Error("Error resetting pad during disposal", ex);
                        RecordEvent("Error", $"Pad reset during disposal failed: {ex.Message}");
                    }

                    // Defensive suppression reset
                    try
                    {
                        LowLevelHooks.Suppress = false;
                        Logger.Info("Input suppression disabled during disposal");
                        RecordEvent("Cleanup", "Input suppression disabled during disposal");
                    }
                    catch (Exception ex)
                    {
                        Logger.Error("Error disabling suppression during disposal", ex);
                        RecordEvent("Error", $"Suppression disable during disposal failed: {ex.Message}");
                    }
                }

                // Dispose dependencies if they implement IDisposable
                // Note: StickMapper and AntiRecoil don't currently implement IDisposable
                // but this section is prepared for future implementations
            }
            catch (Exception ex)
            {
                Logger.Error("Error disposing MnKConvertMode", ex);
                RecordEvent("Error", $"Disposal error: {ex.Message}");
            }

            Logger.Info("MnKConvertMode disposed");
            RecordEvent("Disposed", "Mode handler disposal completed");
        }
    }
}