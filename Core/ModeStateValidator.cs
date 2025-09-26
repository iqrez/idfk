using System;
using System.Collections.Generic;
using System.Linq;
using WootMouseRemap.Diagnostics;

namespace WootMouseRemap.Core
{
    /// <summary>
    /// Centralized state validation and consistency checking for the mode system
    /// </summary>
    public sealed class ModeStateValidator
    {
        private readonly ModeDiagnostics? _diagnostics;
        
        public ModeStateValidator(ModeDiagnostics? diagnostics = null)
        {
            _diagnostics = diagnostics;
        }

        /// <summary>
        /// Validates the entire system state for consistency
        /// </summary>
        public ValidationResult ValidateSystemState(ModeSystemContext context)
        {
            var result = new ValidationResult { IsValid = true, Issues = new List<ValidationIssue>() };

            try
            {
                // Validate mode consistency
                ValidateModeConsistency(context, result);
                
                // Validate suppression state
                ValidateSuppressionState(context, result);
                
                // Validate controller state
                ValidateControllerState(context, result);
                
                // Validate resource state
                ValidateResourceState(context, result);
                
                // Update overall validity
                result.IsValid = !result.Issues.Any(i => i.Severity == ValidationSeverity.Error);
                
                // Log validation results
                if (!result.IsValid)
                {
                    var errors = result.Issues.Where(i => i.Severity == ValidationSeverity.Error).ToList();
                    Logger.Error("System state validation failed with {ErrorCount} errors", errors.Count);
                }
                
                var warnings = result.Issues.Where(i => i.Severity == ValidationSeverity.Warning).ToList();
                if (warnings.Any())
                {
                    Logger.Warn("System state validation found {WarningCount} warnings", warnings.Count);
                }
            }
            catch (Exception ex)
            {
                Logger.Error("Error during system state validation", ex);
                result.IsValid = false;
                result.Issues.Add(new ValidationIssue
                {
                    Severity = ValidationSeverity.Error,
                    Component = "ModeStateValidator",
                    Message = $"Validation error: {ex.Message}",
                    Exception = ex
                });
            }

            return result;
        }

        /// <summary>
        /// Validates that a mode transition is safe and valid
        /// </summary>
        public ValidationResult ValidateTransition(InputMode fromMode, InputMode toMode, ModeSystemContext context)
        {
            var result = new ValidationResult { IsValid = true, Issues = new List<ValidationIssue>() };

            try
            {
                // Check if modes are valid
                if (!Enum.IsDefined(typeof(InputMode), fromMode))
                {
                    result.Issues.Add(new ValidationIssue
                    {
                        Severity = ValidationSeverity.Error,
                        Component = "ModeStateValidator",
                        Message = $"Invalid source mode: {fromMode}"
                    });
                }

                if (!Enum.IsDefined(typeof(InputMode), toMode))
                {
                    result.Issues.Add(new ValidationIssue
                    {
                        Severity = ValidationSeverity.Error,
                        Component = "ModeStateValidator",
                        Message = $"Invalid target mode: {toMode}"
                    });
                }

                // Check if transition is allowed
                if (!IsTransitionAllowed(fromMode, toMode, context))
                {
                    result.Issues.Add(new ValidationIssue
                    {
                        Severity = ValidationSeverity.Error,
                        Component = "ModeStateValidator",
                        Message = $"Transition not allowed: {fromMode} -> {toMode}"
                    });
                }

                // Check preconditions for target mode
                ValidateModePreconditions(toMode, context, result);

                result.IsValid = !result.Issues.Any(i => i.Severity == ValidationSeverity.Error);
            }
            catch (Exception ex)
            {
                Logger.Error("Error validating transition {FromMode} -> {ToMode}", fromMode, toMode, ex);
                result.IsValid = false;
                result.Issues.Add(new ValidationIssue
                {
                    Severity = ValidationSeverity.Error,
                    Component = "ModeStateValidator",
                    Message = $"Transition validation error: {ex.Message}",
                    Exception = ex
                });
            }

            return result;
        }

        private void ValidateModeConsistency(ModeSystemContext context, ValidationResult result)
        {
            // Check if current mode matches what components think it should be
            if (context.ModeManagerCurrentMode.HasValue && 
                context.ThreadSafeControllerMode.HasValue &&
                context.ModeManagerCurrentMode.Value != context.ThreadSafeControllerMode.Value)
            {
                result.Issues.Add(new ValidationIssue
                {
                    Severity = ValidationSeverity.Error,
                    Component = "ModeConsistency",
                    Message = $"Mode mismatch: ModeManager={context.ModeManagerCurrentMode}, ThreadSafeController={context.ThreadSafeControllerMode}"
                });
            }
        }

        private void ValidateSuppressionState(ModeSystemContext context, ValidationResult result)
        {
            if (!context.SuppressionEnabled.HasValue || !context.CurrentMode.HasValue)
            {
                return; // Can't validate without this info
            }

            var mode = context.CurrentMode.Value;
            var suppressed = context.SuppressionEnabled.Value;

            // In Native mode, suppression should typically be enabled
            if (mode == InputMode.Native && !suppressed)
            {
                result.Issues.Add(new ValidationIssue
                {
                    Severity = ValidationSeverity.Warning,
                    Component = "SuppressionState",
                    Message = "Suppression is disabled in Native mode - may cause dual input"
                });
            }

            // In ControllerPass mode, suppression should typically be disabled
            if (mode == InputMode.ControllerPass && suppressed)
            {
                result.Issues.Add(new ValidationIssue
                {
                    Severity = ValidationSeverity.Warning,
                    Component = "SuppressionState",
                    Message = "Suppression is enabled in ControllerPass mode - may block controller input"
                });
            }
        }

        private void ValidateControllerState(ModeSystemContext context, ValidationResult result)
        {
            // Check ViGEm connection
            if (context.ViGEmConnected.HasValue && !context.ViGEmConnected.Value)
            {
                result.Issues.Add(new ValidationIssue
                {
                    Severity = ValidationSeverity.Error,
                    Component = "ViGEm",
                    Message = "ViGEm virtual controller is not connected"
                });
            }

            // Check physical controller for passthrough mode
            if (context.CurrentMode == InputMode.ControllerPass)
            {
                if (context.PhysicalControllerConnected.HasValue && !context.PhysicalControllerConnected.Value)
                {
                    result.Issues.Add(new ValidationIssue
                    {
                        Severity = ValidationSeverity.Warning,
                        Component = "PhysicalController",
                        Message = "No physical controller detected for passthrough mode"
                    });
                }

                if (context.XInputPassthroughRunning.HasValue && !context.XInputPassthroughRunning.Value)
                {
                    result.Issues.Add(new ValidationIssue
                    {
                        Severity = ValidationSeverity.Warning,
                        Component = "XInputPassthrough",
                        Message = "XInput passthrough is not running in passthrough mode"
                    });
                }
            }
        }

        private void ValidateResourceState(ModeSystemContext context, ValidationResult result)
        {
            // Check for resource leaks or invalid states
            if (context.CurrentMode != InputMode.ControllerPass && 
                context.XInputPassthroughRunning.HasValue && 
                context.XInputPassthroughRunning.Value)
            {
                result.Issues.Add(new ValidationIssue
                {
                    Severity = ValidationSeverity.Warning,
                    Component = "ResourceLeak",
                    Message = "XInput passthrough is running outside of passthrough mode"
                });
            }
        }

        private bool IsTransitionAllowed(InputMode fromMode, InputMode toMode, ModeSystemContext context)
        {
            // All transitions are generally allowed, but we can add specific rules here
            
            // Don't allow transition to the same mode
            if (fromMode == toMode)
            {
                return false;
            }

            // Add any other business rules here
            return true;
        }

        private void ValidateModePreconditions(InputMode mode, ModeSystemContext context, ValidationResult result)
        {
            switch (mode)
            {
                case InputMode.Native:
                    // ViGEm should be available
                    if (context.ViGEmConnected.HasValue && !context.ViGEmConnected.Value)
                    {
                        result.Issues.Add(new ValidationIssue
                        {
                            Severity = ValidationSeverity.Warning,
                            Component = "Native",
                            Message = "ViGEm not connected for Native mode"
                        });
                    }
                    break;

                case InputMode.ControllerPass:
                    // Physical controller should be available
                    if (context.PhysicalControllerConnected.HasValue && !context.PhysicalControllerConnected.Value)
                    {
                        result.Issues.Add(new ValidationIssue
                        {
                            Severity = ValidationSeverity.Warning,
                            Component = "ControllerPass",
                            Message = "No physical controller for passthrough mode"
                        });
                    }
                    break;
            }
        }
    }

    /// <summary>
    /// Contains the current state of various system components for validation
    /// </summary>
    public class ModeSystemContext
    {
        public InputMode? CurrentMode { get; set; }
        public InputMode? ModeManagerCurrentMode { get; set; }
        public InputMode? ThreadSafeControllerMode { get; set; }
        public bool? SuppressionEnabled { get; set; }
        public bool? ViGEmConnected { get; set; }
        public bool? PhysicalControllerConnected { get; set; }
        public bool? XInputPassthroughRunning { get; set; }
        public DateTime? LastTransition { get; set; }
        public Dictionary<string, object> AdditionalState { get; set; } = new Dictionary<string, object>();
    }

    public class ValidationResult
    {
        public bool IsValid { get; set; }
        public List<ValidationIssue> Issues { get; set; } = new List<ValidationIssue>();
    }

    public class ValidationIssue
    {
        public ValidationSeverity Severity { get; set; }
        public required string Component { get; set; }
        public required string Message { get; set; }
        public Exception? Exception { get; set; }
    }

    public enum ValidationSeverity
    {
        Info,
        Warning,
        Error
    }
}