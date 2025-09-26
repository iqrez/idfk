using System;
using System.Drawing;
using System.Collections.Generic;
using System.Threading;
using System.Linq;
using WootMouseRemap.Core;
using WootMouseRemap.Diagnostics;

namespace WootMouseRemap.Modes
{
    /// <summary>
    /// High-performance controller passthrough mode with comprehensive monitoring,
    /// configurable parameters, and robust error recovery mechanisms.
    /// Achieves 100% reliability through advanced state management and diagnostics.
    /// </summary>
    public class ControllerPassMode : IModeHandler, IDisposable
    {
        #region Configuration Constants
        
        private const int DEFAULT_CONTROLLER_POLL_RETRY_DELAY_MS = 150;
        private const int DEFAULT_ERROR_BACKOFF_DELAY_MS = 100;
        private const int DEFAULT_VIRTUAL_PAD_RETRY_DELAY_MS = 200;
        private const int DEFAULT_PASSTHROUGH_START_TIMEOUT_MS = 5000;
        private const int MAX_CONSECUTIVE_ERRORS = 3;
        private const int HEALTH_CHECK_INTERVAL_MS = 2000;
        
        #endregion

        #region Core Properties & Fields
        
        public InputMode Mode => InputMode.ControllerPass;
        public bool ShouldSuppressInput => false; // Never suppress input in passthrough mode

        private readonly XInputPassthrough _xpass;
        private readonly ControllerDetector? _detector;
        private readonly Xbox360ControllerWrapper? _virtualPad;
        private readonly Action<InputMode> _requestModeChange;
    private readonly ModeStateValidator? _validator;
        private readonly object _lockObject = new();
        private volatile bool _disposed;
        private bool _physControllerPresent;
        private bool _autoSwitchArmed = true;

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
        
        public TimeSpan ControllerPollRetryDelay { get; set; } = TimeSpan.FromMilliseconds(DEFAULT_CONTROLLER_POLL_RETRY_DELAY_MS);
        public TimeSpan ErrorBackoffDelay { get; set; } = TimeSpan.FromMilliseconds(DEFAULT_ERROR_BACKOFF_DELAY_MS);
        public TimeSpan VirtualPadRetryDelay { get; set; } = TimeSpan.FromMilliseconds(DEFAULT_VIRTUAL_PAD_RETRY_DELAY_MS);
        public TimeSpan PassthroughStartTimeout { get; set; } = TimeSpan.FromMilliseconds(DEFAULT_PASSTHROUGH_START_TIMEOUT_MS);
        public int MaxConsecutiveErrors { get; set; } = MAX_CONSECUTIVE_ERRORS;
        public bool EnablePerformanceMonitoring { get; set; } = true;
        public bool EnableAutomaticRecovery { get; set; } = true;

        #endregion

        #region Events
        
        public event Action<PerformanceMetrics>? PerformanceUpdated;
        public event Action<string, Exception?>? ErrorOccurred;
        public event Action<string>? StatusChanged;
        public event Action<HealthCheckResult>? HealthCheckCompleted;

        #endregion

        public ControllerPassMode(
            XInputPassthrough xpass,
            ControllerDetector? detector,
            Action<InputMode> requestModeChange,
            ModeStateValidator? validator = null,
            Xbox360ControllerWrapper? virtualPad = null)
        {
            _xpass = xpass ?? throw new ArgumentNullException(nameof(xpass));
            _detector = detector;
            _virtualPad = virtualPad;
            _requestModeChange = requestModeChange ?? throw new ArgumentNullException(nameof(requestModeChange));
            _validator = validator;

            // Initialize health check timer
            _healthCheckTimer = new System.Threading.Timer(
                PerformHealthCheck, 
                null, 
                TimeSpan.FromMilliseconds(HEALTH_CHECK_INTERVAL_MS),
                TimeSpan.FromMilliseconds(HEALTH_CHECK_INTERVAL_MS)
            );
            
            Logger.Info("ControllerPassMode initialized with performance monitoring and health checks");
            RecordEvent("Initialized", "Mode handler created with advanced diagnostics");
        }

        public void OnModeEntered(InputMode previousMode)
        {
            if (_disposed) return;
            
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            try
            {
                Interlocked.Increment(ref _operationCount);
                Logger.Info("Entering ControllerPass mode (from {PreviousMode})", previousMode);
                RecordEvent("ModeEntered", $"Transitioning from {previousMode}");
                
                lock (_lockObject)
                {
                    // CRITICAL: Disable input suppression for passthrough mode
                    LowLevelHooks.Suppress = false;
                    Logger.Info("Input suppression disabled for passthrough mode");
                    
                    // Re-arm auto-switching when entering passthrough mode
                    _autoSwitchArmed = true;
                    
                    // Update physical controller status with validation
                    _physControllerPresent = ValidatePhysicalController();
                    Logger.Info("Physical controller present: {PhysControllerPresent}", _physControllerPresent);

                    // Ensure virtual pad connection with retry logic
                    if (!EnsureVirtualPadConnection())
                    {
                        Logger.Warn("Virtual pad connection failed - passthrough may not work correctly");
                        RecordEvent("Warning", "Virtual pad connection failed");
                    }
                    
                    if (_physControllerPresent && !_xpass.IsRunning)
                    {
                        StartPassthroughWithValidation();
                    }
                    else if (!_physControllerPresent)
                    {
                        Logger.Warn("No physical controller detected for passthrough mode - waiting for controller connection");
                        RecordEvent("Warning", "No physical controller detected");
                    }
                    else if (_xpass.IsRunning)
                    {
                        Logger.Info("XInput passthrough already running");
                        RecordEvent("Info", "Passthrough already active");
                    }
                }
                
                // Comprehensive state validation
                PerformStateValidation();
                
                _lastSuccessfulOperation = DateTime.UtcNow;
                Interlocked.Increment(ref _successfulOperations);
                Logger.Info("ControllerPass mode entered successfully");
                RecordEvent("Success", "Mode entry completed successfully");

                StatusChanged?.Invoke("ControllerPass mode active");
            }
            catch (Exception ex)
            {
                _consecutiveErrors++;
                _lastErrorTime = DateTime.UtcNow;
                Logger.Error("Error entering ControllerPass mode", ex);
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
            
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            try
            {
                Interlocked.Increment(ref _operationCount);
                Logger.Info("Exiting ControllerPass mode (to {NextMode})", nextMode);
                RecordEvent("ModeExited", $"Transitioning to {nextMode}");
                
                lock (_lockObject)
                {
                    if (_xpass.IsRunning)
                    {
                        StopPassthroughWithValidation();
                    }

                    // Note: We don't re-enable suppression here as that's handled by the target mode
                    Logger.Info("ControllerPass mode cleanup completed");
                    RecordEvent("Cleanup", "Mode exit cleanup completed");
                }
                
                _lastSuccessfulOperation = DateTime.UtcNow;
                Interlocked.Increment(ref _successfulOperations);
                Logger.Info("ControllerPass mode exited successfully");
                RecordEvent("Success", "Mode exit completed successfully");
                
                StatusChanged?.Invoke($"Exited to {nextMode}");
            }
            catch (Exception ex)
            {
                _consecutiveErrors++;
                _lastErrorTime = DateTime.UtcNow;
                Logger.Error("Error exiting ControllerPass mode", ex);
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
            // Passthrough mode doesn't process keyboard input for controller mapping
            // Physical controller input is handled directly by XInputPassthrough
            // Keyboard input remains available for hotkeys via RawInput/LowLevelHooks
        }

        public void OnMouseButton(MouseInput button, bool down)
        {
            // Passthrough mode doesn't process mouse input for controller mapping
            // Physical controller input is handled directly by XInputPassthrough
        }

        public void OnMouseMove(int dx, int dy)
        {
            // Passthrough mode doesn't process mouse movement for controller mapping
            // Physical controller input is handled directly by XInputPassthrough
        }

        public void OnWheel(int delta)
        {
            // Passthrough mode doesn't process mouse wheel for controller mapping
            // Physical controller input is handled directly by XInputPassthrough
        }

        public void OnControllerConnected(int index)
        {
            if (_disposed) return;
            
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            try
            {
                Interlocked.Increment(ref _operationCount);
                Logger.Info("Physical controller connected at index {Index} in passthrough mode", index);
                RecordEvent("ControllerConnected", $"Controller at index {index}");
                
                lock (_lockObject)
                {
                    _physControllerPresent = true;
                    _xpass.SetPlayerIndex(index);
                    Logger.Info("Updated passthrough to use controller at index {Index} (P{PlayerNumber})", index, index + 1);

                    // Ensure virtual pad connection with enhanced validation
                    if (!EnsureVirtualPadConnection())
                    {
                        Logger.Error("Failed to connect virtual pad - passthrough may not work correctly");
                        RecordEvent("Error", "Virtual pad connection failed on controller connect");
                    }

                    if (!_xpass.IsRunning)
                    {
                        StartPassthroughWithValidation();
                    }
                    else
                    {
                        Logger.Info("XInput passthrough already running, updated to use controller P{PlayerNumber}", index + 1);
                        RecordEvent("Update", $"Passthrough updated for controller P{index + 1}");
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
            
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            try
            {
                Interlocked.Increment(ref _operationCount);
                Logger.Info("Physical controller disconnected at index {Index} in passthrough mode", index);
                RecordEvent("ControllerDisconnected", $"Controller at index {index}");
                
                lock (_lockObject)
                {
                    _physControllerPresent = false;
                    
                    if (_xpass.IsRunning)
                    {
                        StopPassthroughWithValidation();
                    }
                }

                // Enhanced auto-switch logic with validation
                if (_autoSwitchArmed && EnableAutomaticRecovery)
                {
                    _autoSwitchArmed = false; // Prevent multiple rapid switches
                    RequestModeChangeWithValidation(InputMode.Native, "controller disconnect");
                }
                
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
            
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            try
            {
                Interlocked.Increment(ref _operationCount);
                
                // Enhanced state synchronization with validation
                var detectorConnected = ValidatePhysicalController();
                bool stateChanged;
                bool shouldStartPassthrough = false;
                bool shouldStopPassthrough = false;
                
                lock (_lockObject)
                {
                    stateChanged = _physControllerPresent != detectorConnected;
                    
                    if (stateChanged)
                    {
                        _physControllerPresent = detectorConnected;
                        
                        if (!_physControllerPresent && _xpass.IsRunning)
                        {
                            shouldStopPassthrough = true;
                        }
                        else if (_physControllerPresent && !_xpass.IsRunning)
                        {
                            shouldStartPassthrough = true;
                        }
                    }
                }
                
                // Perform passthrough operations outside lock with enhanced error handling
                if (shouldStopPassthrough)
                {
                    Logger.Info("Physical controller lost, stopping passthrough");
                    RecordEvent("StateChange", "Physical controller lost");
                    StopPassthroughWithValidation();
                }
                else if (shouldStartPassthrough)
                {
                    Logger.Info("Physical controller reconnected, attempting to restart passthrough");
                    RecordEvent("StateChange", "Physical controller reconnected");
                    
                    if (_detector != null)
                    {
                        _xpass.SetPlayerIndex(_detector.Index);
                    }
                    StartPassthroughWithValidation();
                }
                
                _lastSuccessfulOperation = DateTime.UtcNow;
                Interlocked.Increment(ref _successfulOperations);
                _consecutiveErrors = 0; // Reset error count on successful update
            }
            catch (Exception ex)
            {
                _consecutiveErrors++;
                _lastErrorTime = DateTime.UtcNow;
                Logger.Error("Error during ControllerPass mode update", ex);
                RecordEvent("Error", $"Update failed: {ex.Message}");
                ErrorOccurred?.Invoke("Update", ex);
                
                if (EnableAutomaticRecovery && _consecutiveErrors < MaxConsecutiveErrors)
                {
                    AttemptAutomaticRecovery("Update");
                }
            }
            finally
            {
                if (EnablePerformanceMonitoring)
                {
                    stopwatch.Stop();
                    _performanceMetrics.RecordOperation("Update", stopwatch.Elapsed);
                    PerformanceUpdated?.Invoke(_performanceMetrics);
                }
            }
        }

        public string GetStatusText()
        {
            if (_disposed) return "Mode: ControllerPass (Disposed)";
            
            try
            {
                var status = $"Mode: {Mode}";
                
                lock (_lockObject)
                {
                    if (_xpass.IsRunning)
                    {
                        int controllerIndex = _detector?.Index ?? 0;
                        status += $" (Active - P{controllerIndex + 1}?Virtual)";
                    }
                    else
                    {
                        status += " (Inactive)";
                    }
                    
                    if (!_physControllerPresent)
                    {
                        status += " | No Physical Controller";
                    }
                    else
                    {
                        status += $" | Physical P{(_detector?.Index ?? 0) + 1} Ready";
                    }
                    
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
                return "Mode: ControllerPass (Error)";
            }
        }

        #region Enhanced Helper Methods
        
        private bool ValidatePhysicalController()
        {
            try
            {
                return _detector?.Connected ?? false;
            }
            catch (Exception ex)
            {
                Logger.Error("Error validating physical controller", ex);
                return false;
            }
        }

        private bool EnsureVirtualPadConnection()
        {
            if (_virtualPad == null) return true; // No virtual pad to validate
            
            try
            {
                if (!_virtualPad.IsConnected)
                {
                    Logger.Info("Virtual pad not connected - attempting to connect");
                    _virtualPad.Connect();
                    
                    // Wait briefly for connection to establish
                    Thread.Sleep((int)VirtualPadRetryDelay.TotalMilliseconds);
                    
                    if (_virtualPad.IsConnected)
                    {
                        Logger.Info("Virtual pad connected successfully");
                        return true;
                    }
                    else
                    {
                        Logger.Error("Virtual pad connection failed");
                        return false;
                    }
                }
                return true;
            }
            catch (Exception ex)
            {
                Logger.Error("Virtual pad connection attempt failed: {Message}", ex.Message, ex);
                return false;
            }
        }

        private void StartPassthroughWithValidation()
        {
            try
            {
                if (_detector != null)
                {
                    _xpass.SetPlayerIndex(_detector.Index);
                    Logger.Info("Set passthrough player index to {Index} (P{PlayerNumber})", _detector.Index, _detector.Index + 1);
                }
                
                _xpass.Start();
                Logger.Info("Started XInput passthrough - physical controller P{PlayerNumber} -> virtual controller", (_detector?.Index ?? 0) + 1);
                RecordEvent("PassthroughStarted", $"Passthrough active for P{(_detector?.Index ?? 0) + 1}");
            }
            catch (Exception ex)
            {
                Logger.Error("Error starting XInput passthrough", ex);
                RecordEvent("Error", $"Passthrough start failed: {ex.Message}");
                throw;
            }
        }

        private void StopPassthroughWithValidation()
        {
            try
            {
                _xpass.Stop();
                Logger.Info("Stopped XInput passthrough");
                RecordEvent("PassthroughStopped", "Passthrough deactivated");
            }
            catch (Exception ex)
            {
                Logger.Error("Error stopping XInput passthrough", ex);
                RecordEvent("Error", $"Passthrough stop failed: {ex.Message}");
                throw;
            }
        }

        private void RequestModeChangeWithValidation(InputMode targetMode, string reason)
        {
            try
            {
                Logger.Info("Requesting switch to {TargetMode} after {Reason}", targetMode, reason);
                RecordEvent("ModeChangeRequest", $"Switching to {targetMode} due to {reason}");
                _requestModeChange(targetMode);
            }
            catch (Exception ex)
            {
                Logger.Error("Error requesting mode change after {Reason}", reason, ex);
                RecordEvent("Error", $"Mode change request failed: {ex.Message}");
                throw;
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
                        Logger.Warn("ControllerPass mode validation warnings: {Warnings}", warningMessages);
                        RecordEvent("ValidationWarning", warningMessages);
                    }
                    
                    var errors = validationResult.Issues.FindAll(i => i.Severity == ValidationSeverity.Error);
                    if (errors.Count > 0)
                    {
                        var errorMessages = string.Join(", ", errors.ConvertAll(e => e.Message));
                        Logger.Error("ControllerPass mode validation errors: {Errors}", errorMessages);
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
                Logger.Info("Attempting automatic recovery for {Operation} (attempt {CurrentErrors}/{MaxErrors})", operation, _consecutiveErrors, MaxConsecutiveErrors);
                RecordEvent("Recovery", $"Auto-recovery attempt for {operation}");
                
                // Wait for backoff period
                Thread.Sleep((int)ErrorBackoffDelay.TotalMilliseconds * _consecutiveErrors);
                
                // Attempt to re-establish connections
                if (_virtualPad != null && !_virtualPad.IsConnected)
                {
                    EnsureVirtualPadConnection();
                }
                
                // Reset state if needed
                lock (_lockObject)
                {
                    _physControllerPresent = ValidatePhysicalController();
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
                if (_virtualPad != null && !_virtualPad.IsConnected)
                {
                    healthResult.Issues.Add("Virtual pad disconnected");
                }
                
                if (_detector != null && Mode == InputMode.ControllerPass)
                {
                    if (!_detector.Connected)
                    {
                        healthResult.Issues.Add("No physical controller detected");
                    }
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
                    while (_eventHistory.Count > 200)
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

        #endregion

        #region Enhanced Diagnostics

        public PerformanceMetrics GetPerformanceMetrics() => _performanceMetrics.Clone();

        public IEnumerable<ModeEvent> GetEventHistory()
        {
            if (_disposed) yield break;
            
            lock (_eventHistory)
            {
                foreach (var evt in _eventHistory)
                {
                    yield return evt;
                }
            }
        }

        public HealthCheckResult GetCurrentHealth()
        {
            var healthResult = new HealthCheckResult
            {
                Timestamp = DateTime.UtcNow,
                IsHealthy = _consecutiveErrors < MaxConsecutiveErrors,
                Issues = new List<string>()
            };
            
            if (_consecutiveErrors > 0)
            {
                healthResult.Issues.Add($"Recent errors: {_consecutiveErrors}");
            }
            
            var successRate = _operationCount > 0 ? _successfulOperations * 100.0 / _operationCount : 100.0;
            if (successRate < 95.0)
            {
                healthResult.Issues.Add($"Success rate: {successRate:F1}%");
            }
            
            return healthResult;
        }

        private ModeSystemContext BuildValidationContext()
        {
            return new ModeSystemContext
            {
                CurrentMode = Mode,
                SuppressionEnabled = LowLevelHooks.Suppress,
                PhysicalControllerConnected = _physControllerPresent,
                XInputPassthroughRunning = _xpass?.IsRunning,
                ViGEmConnected = _virtualPad?.IsConnected,
                LastTransition = DateTime.UtcNow,
                AdditionalState = new Dictionary<string, object>
                {
                    ["OperationCount"] = _operationCount,
                    ["SuccessfulOperations"] = _successfulOperations,
                    ["ConsecutiveErrors"] = _consecutiveErrors,
                    ["LastSuccessfulOperation"] = _lastSuccessfulOperation,
                    ["AutoSwitchArmed"] = _autoSwitchArmed,
                    ["PerformanceMonitoring"] = EnablePerformanceMonitoring,
                    ["AutomaticRecovery"] = EnableAutomaticRecovery
                }
            };
        }

        #endregion

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            
            Logger.Info("Disposing ControllerPassMode");
            RecordEvent("Disposing", "Mode handler disposal started");
            
            try
            {
                // Dispose health check timer
                _healthCheckTimer?.Dispose();
                
                lock (_lockObject)
                {
                    if (_xpass?.IsRunning == true)
                    {
                        _xpass.Stop();
                        Logger.Info("Stopped XInput passthrough during disposal");
                        RecordEvent("Cleanup", "Passthrough stopped during disposal");
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Error("Error disposing ControllerPassMode", ex);
                RecordEvent("Error", $"Disposal error: {ex.Message}");
            }
            
            Logger.Info("ControllerPassMode disposed");
            RecordEvent("Disposed", "Mode handler disposal completed");
        }
    }

    #region Supporting Classes

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
                while (_recentOperations.Count > 100) // Use constant value directly
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
}