using System;
using System.Threading;
using System.Threading.Tasks;
using WootMouseRemap.Diagnostics;
using WootMouseRemap.Modes;

namespace WootMouseRemap.Core
{
    /// <summary>
    /// Manages safe mode transitions with proper orchestration and error recovery
    /// </summary>
    public sealed class ModeTransitionManager : IDisposable
    {
        private readonly ModeDiagnostics _diagnostics;
        private readonly ModeStateValidator _validator;
        private readonly ModeManager _modeManager;
        private readonly object _lockObject = new();
        private volatile bool _disposed;

    public event Action<TransitionResult>? TransitionCompleted;

        public ModeTransitionManager(
            ModeManager modeManager,
            ModeDiagnostics? diagnostics = null,
            ModeStateValidator? validator = null)
        {
            _modeManager = modeManager ?? throw new ArgumentNullException(nameof(modeManager));
            _diagnostics = diagnostics ?? new ModeDiagnostics();
            _validator = validator ?? new ModeStateValidator(_diagnostics);
            
            Logger.Info("ModeTransitionManager initialized");
        }

        /// <summary>
        /// Executes a mode transition with comprehensive validation and error handling
        /// </summary>
        public async Task<TransitionResult> ExecuteTransitionAsync(
            InputMode fromMode, 
            InputMode toMode, 
            TimeSpan? timeout = null,
            CancellationToken cancellationToken = default)
        {
            if (_disposed)
            {
                return TransitionResult.Failure("ModeTransitionManager is disposed");
            }

            var actualTimeout = timeout ?? TimeSpan.FromSeconds(15);
            var startTime = DateTime.UtcNow;
            
            Logger.Info("Starting orchestrated transition: {FromMode} -> {ToMode}", fromMode, toMode);

            var result = new TransitionResult
            {
                FromMode = fromMode,
                ToMode = toMode,
                StartTime = startTime,
                Success = false
            };

            try
            {
                using var timeoutCts = new CancellationTokenSource(actualTimeout);
                using var combinedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

                // Phase 1: Pre-transition validation
                var preValidationResult = await ValidatePreTransition(fromMode, toMode, combinedCts.Token);
                result.PreValidationResult = preValidationResult;
                
                if (!preValidationResult.IsValid)
                {
                    result.ErrorMessage = "Pre-transition validation failed";
                    result.ValidationIssues = preValidationResult.Issues;
                    return result;
                }

                // Phase 2: Prepare for transition
                var prepareResult = await PrepareTransition(fromMode, toMode, combinedCts.Token);
                result.PrepareResult = prepareResult;
                
                if (!prepareResult.Success)
                {
                    var phaseMsg = prepareResult.Message ?? "Unknown preparation failure";
                    result.ErrorMessage = $"Transition preparation failed: {phaseMsg}";
                    return result;
                }

                // Phase 3: Execute the actual transition
                var executeResult = await ExecuteTransition(fromMode, toMode, combinedCts.Token);
                result.ExecuteResult = executeResult;
                
                if (!executeResult.Success)
                {
                    var phaseMsg = executeResult.Message ?? "Unknown execution failure";
                    result.ErrorMessage = $"Transition execution failed: {phaseMsg}";
                    
                    // Attempt rollback
                    await AttemptRollback(fromMode, toMode, combinedCts.Token);
                    return result;
                }

                // Phase 4: Post-transition validation
                var postValidationResult = await ValidatePostTransition(fromMode, toMode, combinedCts.Token);
                result.PostValidationResult = postValidationResult;
                
                if (!postValidationResult.IsValid)
                {
                    result.ErrorMessage = "Post-transition validation failed";
                    result.ValidationIssues = postValidationResult.Issues;
                    
                    // Attempt rollback
                    await AttemptRollback(fromMode, toMode, combinedCts.Token);
                    return result;
                }

                // Phase 5: Finalize transition
                await FinalizeTransition(fromMode, toMode, combinedCts.Token);
                
                result.Success = true;
                result.CompletionTime = DateTime.UtcNow;
                
                Logger.Info("Orchestrated transition completed successfully: {FromMode} -> {ToMode} in {Duration:F0}ms", fromMode, toMode, (result.CompletionTime - result.StartTime).TotalMilliseconds);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                result.ErrorMessage = "Transition was cancelled";
                Logger.Warn("Transition cancelled: {FromMode} -> {ToMode}", fromMode, toMode);
            }
            catch (OperationCanceledException)
            {
                result.ErrorMessage = "Transition timed out";
                Logger.Error("Transition timed out: {FromMode} -> {ToMode}", fromMode, toMode);
            }
            catch (Exception ex)
            {
                result.ErrorMessage = $"Unexpected error: {ex.Message}";
                result.Exception = ex;
                Logger.Error("Unexpected error during transition: {FromMode} -> {ToMode}", fromMode, toMode, ex);
            }
            finally
            {
                result.CompletionTime = DateTime.UtcNow;
                
                // Log diagnostic information
                _diagnostics?.LogModeTransition(fromMode, toMode, result.Success, result.Exception, result.ErrorMessage);
                
                // Notify completion
                try
                {
                    TransitionCompleted?.Invoke(result);
                }
                catch (Exception ex)
                {
                    Logger.Error("Error in TransitionCompleted event handler", ex);
                }
            }

            return result;
        }

        private async Task<ValidationResult> ValidatePreTransition(InputMode fromMode, InputMode toMode, CancellationToken cancellationToken)
        {
            await Task.Yield(); // Make async
            
            var context = await BuildSystemContext(cancellationToken);
            return _validator.ValidateTransition(fromMode, toMode, context);
        }

        private async Task<ValidationResult> ValidatePostTransition(InputMode fromMode, InputMode toMode, CancellationToken cancellationToken)
        {
            await Task.Yield(); // Make async
            
            // Wait a bit for the system to settle
            await Task.Delay(100, cancellationToken);
            
            var context = await BuildSystemContext(cancellationToken);
            return _validator.ValidateSystemState(context);
        }

        private async Task<TransitionPhaseResult> PrepareTransition(InputMode fromMode, InputMode toMode, CancellationToken cancellationToken)
        {
            try
            {
                Logger.Info("Preparing transition: {FromMode} -> {ToMode}", fromMode, toMode);
                
                // Prepare mode manager for transition
                // This might involve stopping certain services, cleaning up resources, etc.
                await Task.Delay(10, cancellationToken); // Simulate preparation work
                
                return TransitionPhaseResult.CreateSuccess("Preparation completed");
            }
            catch (Exception ex)
            {
                Logger.Error("Error during transition preparation", ex);
                return TransitionPhaseResult.CreateFailure($"Preparation failed: {ex.Message}", ex);
            }
        }

        private async Task<TransitionPhaseResult> ExecuteTransition(InputMode fromMode, InputMode toMode, CancellationToken cancellationToken)
        {
            try
            {
                Logger.Info("Executing transition: {FromMode} -> {ToMode}", fromMode, toMode);
                
                // Use the mode manager to perform the actual transition
                lock (_lockObject)
                {
                    _modeManager.SwitchMode(toMode);
                }
                
                // Give the system time to process the mode change
                await Task.Delay(50, cancellationToken);
                
                return TransitionPhaseResult.CreateSuccess("Execution completed");
            }
            catch (Exception ex)
            {
                Logger.Error("Error during transition execution", ex);
                return TransitionPhaseResult.CreateFailure($"Execution failed: {ex.Message}", ex);
            }
        }

        private async Task FinalizeTransition(InputMode fromMode, InputMode toMode, CancellationToken cancellationToken)
        {
            try
            {
                Logger.Info("Finalizing transition: {FromMode} -> {ToMode}", fromMode, toMode);
                
                // Update diagnostic state
                _diagnostics?.UpdateSystemState("CurrentMode", toMode);
                _diagnostics?.UpdateSystemState("LastTransition", DateTime.UtcNow);
                
                await Task.Delay(10, cancellationToken); // Simulate finalization work
                
                Logger.Info("Transition finalization completed");
            }
            catch (Exception ex)
            {
                Logger.Error("Error during transition finalization", ex);
                // Don't fail the overall transition for finalization errors
            }
        }

        private async Task AttemptRollback(InputMode fromMode, InputMode toMode, CancellationToken cancellationToken)
        {
            try
            {
                Logger.Warn("Attempting rollback: {ToMode} -> {FromMode}", toMode, fromMode);
                
                lock (_lockObject)
                {
                    _modeManager.SwitchMode(fromMode);
                }
                
                await Task.Delay(50, cancellationToken);
                
                Logger.Info("Rollback completed: {ToMode} -> {FromMode}", toMode, fromMode);
            }
            catch (Exception ex)
            {
                Logger.Error("Rollback failed", ex);
                // Can't do much more if rollback fails
            }
        }

        private async Task<ModeSystemContext> BuildSystemContext(CancellationToken cancellationToken)
        {
            await Task.Yield(); // Make async
            
            var context = new ModeSystemContext();
            
            try
            {
                // Gather current system state from various components
                context.CurrentMode = _modeManager.CurrentMode;
                context.ModeManagerCurrentMode = _modeManager.CurrentMode;
                
                // Get state from diagnostics if available
                if (_diagnostics != null)
                {
                    context.SuppressionEnabled = _diagnostics.GetSystemState<bool?>("LowLevelHooks.Suppress");
                    context.ViGEmConnected = _diagnostics.GetSystemState<bool?>("ViGEm.Connected");
                    context.PhysicalControllerConnected = _diagnostics.GetSystemState<bool?>("PhysicalController.Connected");
                    context.XInputPassthroughRunning = _diagnostics.GetSystemState<bool?>("XInputPassthrough.Running");
                }
            }
            catch (Exception ex)
            {
                Logger.Error("Error building system context", ex);
            }
            
            return context;
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            
            Logger.Info("ModeTransitionManager disposed");
        }
    }

    public class TransitionResult
    {
        public InputMode FromMode { get; set; } = default;
        public InputMode ToMode { get; set; } = default;
        public DateTime StartTime { get; set; } = DateTime.MinValue;
        public DateTime CompletionTime { get; set; } = DateTime.MinValue;
        public bool Success { get; set; } = false;
        public string? ErrorMessage { get; set; }
        public Exception? Exception { get; set; }
        
        public ValidationResult? PreValidationResult { get; set; }
        public ValidationResult? PostValidationResult { get; set; }
        public TransitionPhaseResult? PrepareResult { get; set; }
        public TransitionPhaseResult? ExecuteResult { get; set; }
        
        public System.Collections.Generic.List<ValidationIssue> ValidationIssues { get; set; } = new System.Collections.Generic.List<ValidationIssue>();
        
        public TimeSpan Duration => CompletionTime - StartTime;

        public static TransitionResult Failure(string errorMessage, Exception? exception = null)
        {
            return new TransitionResult
            {
                Success = false,
                ErrorMessage = errorMessage,
                Exception = exception,
                CompletionTime = DateTime.UtcNow
            };
        }
    }

    public class TransitionPhaseResult
    {
        public bool Success { get; set; }
        public string? Message { get; set; }
        public Exception? Exception { get; set; }

        public static TransitionPhaseResult CreateSuccess(string message) => new TransitionPhaseResult { Success = true, Message = message };
        public static TransitionPhaseResult CreateFailure(string message, Exception? exception = null) => new TransitionPhaseResult { Success = false, Message = message, Exception = exception };
    }
}