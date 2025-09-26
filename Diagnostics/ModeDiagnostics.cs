using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Timer = System.Threading.Timer;

namespace WootMouseRemap.Diagnostics
{
    /// <summary>
    /// Provides comprehensive diagnostics and monitoring for the mode system
    /// </summary>
    public sealed class ModeDiagnostics : IDisposable
    {
        private readonly ConcurrentQueue<ModeTransitionEvent> _transitionHistory;
        private readonly ConcurrentDictionary<string, object> _systemState;
        private readonly Timer _healthCheckTimer;
        private readonly object _lockObject = new();
        private volatile bool _disposed;

        /// <summary>
        /// Sanitizes strings for logging to prevent log injection attacks
        /// </summary>
        private static string SanitizeForLogging(object input)
        {
            if (input == null) return "[null]";
            var str = input.ToString();
            if (string.IsNullOrEmpty(str)) return "[empty]";
            
            // Remove control characters and limit length
            var sanitized = Regex.Replace(str, @"[\r\n\t\x00-\x1F\x7F-\x9F]", "_", RegexOptions.None, TimeSpan.FromMilliseconds(100));
            return sanitized.Length > 100 ? sanitized.Substring(0, 100) + "...[truncated]" : sanitized;
        }

        public event Action<ModeTransitionEvent>? TransitionLogged;
        public event Action<string, object>? StateChanged;
        public event Action<HealthCheckResult>? HealthCheckCompleted;

        public ModeDiagnostics()
        {
            _transitionHistory = new ConcurrentQueue<ModeTransitionEvent>();
            _systemState = new ConcurrentDictionary<string, object>();
            
            // Run health checks every 5 seconds
            _healthCheckTimer = new Timer(PerformHealthCheck, null, TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(5));
            
            Logger.Info("ModeDiagnostics initialized");
        }

        public void LogModeTransition(InputMode fromMode, InputMode toMode, bool success, Exception? exception = null, string? details = null)
        {
            if (_disposed) return;

            var transitionEvent = new ModeTransitionEvent
            {
                Timestamp = DateTime.UtcNow,
                FromMode = fromMode,
                ToMode = toMode,
                Success = success,
                Exception = exception,
                Details = details,
                ThreadId = Thread.CurrentThread.ManagedThreadId
            };

            _transitionHistory.Enqueue(transitionEvent);
            
            // Keep only last 100 events to prevent memory leaks
            while (_transitionHistory.Count > 100)
            {
                _transitionHistory.TryDequeue(out _);
            }

            try
            {
                TransitionLogged?.Invoke(transitionEvent);
            }
            catch (Exception ex)
            {
                Logger.Error("Error in TransitionLogged event handler", ex);
            }

            // Log to system logger
            if (success)
            {
                Logger.Info("Mode transition: {FromMode} -> {ToMode} (Thread: {ThreadId})", SanitizeForLogging(fromMode), SanitizeForLogging(toMode), transitionEvent.ThreadId);
            }
            else
            {
                Logger.Error("Mode transition failed: {FromMode} -> {ToMode} (Thread: {ThreadId})", SanitizeForLogging(fromMode), SanitizeForLogging(toMode), transitionEvent.ThreadId, exception);
            }
        }

        public void UpdateSystemState(string key, object value)
        {
            if (_disposed) return;

            _systemState.AddOrUpdate(key, value, (k, oldValue) => value);
            
            try
            {
                StateChanged?.Invoke(key, value);
            }
            catch (Exception ex)
            {
                Logger.Error("Error in StateChanged event handler for key {Key}", SanitizeForLogging(key), ex);
            }
        }

        public T GetSystemState<T>(string key, T defaultValue = default!)
        {
            if (_disposed) return defaultValue;

            if (_systemState.TryGetValue(key, out var value) && value is T typedValue)
            {
                return typedValue;
            }
            return defaultValue;
        }

        public IEnumerable<ModeTransitionEvent> GetRecentTransitions(int count = 20)
        {
            if (_disposed) yield break;

            var events = new List<ModeTransitionEvent>();
            foreach (var evt in _transitionHistory)
            {
                events.Add(evt);
                if (events.Count >= count) break;
            }

            // Return most recent first
            events.Reverse();
            foreach (var evt in events)
            {
                yield return evt;
            }
        }

        public Dictionary<string, object> GetCurrentSystemState()
        {
            if (_disposed) return new Dictionary<string, object>();

            return new Dictionary<string, object>(_systemState);
        }

        private void PerformHealthCheck(object? state)
        {
            if (_disposed) return;

            try
            {
                var result = new HealthCheckResult
                {
                    Timestamp = DateTime.UtcNow,
                    IsHealthy = true,
                    Issues = new List<string>()
                };

                lock (_lockObject)
                {
                    // Check for recent failed transitions
                    var recentFailures = 0;
                    var cutoff = DateTime.UtcNow.AddMinutes(-5);
                    
                    foreach (var evt in _transitionHistory)
                    {
                        if (evt.Timestamp >= cutoff && !evt.Success)
                        {
                            recentFailures++;
                        }
                    }

                    if (recentFailures > 3)
                    {
                        result.IsHealthy = false;
                        result.Issues.Add("Too many failed transitions in last 5 minutes: " + recentFailures.ToString());
                    }

                    // Check system state consistency
                    var suppressState = GetSystemState<bool?>("LowLevelHooks.Suppress");
                    var currentMode = GetSystemState<InputMode?>("CurrentMode");
                    
                    if (suppressState.HasValue && currentMode.HasValue)
                    {
                        // In MnKConvert mode, suppression should typically be true
                        if (currentMode.Value == InputMode.MnKConvert && !suppressState.Value)
                        {
                            result.Issues.Add("Suppression is OFF in MnKConvert mode - potential dual input");
                        }
                        
                        // In ControllerPass mode, suppression should typically be false
                        if (currentMode.Value == InputMode.ControllerPass && suppressState.Value)
                        {
                            result.Issues.Add("Suppression is ON in ControllerPass mode - may block controller input");
                        }
                    }

                    // Check for resource leaks (excessive transition history)
                    if (_transitionHistory.Count > 90)
                    {
                        result.Issues.Add("Transition history growing large: " + _transitionHistory.Count.ToString() + " events");
                    }

                    if (result.Issues.Count > 0)
                    {
                        result.IsHealthy = false;
                    }
                }

                try
                {
                    HealthCheckCompleted?.Invoke(result);
                }
                catch (Exception ex)
                {
                    Logger.Error("Error in HealthCheckCompleted event handler", ex);
                }

                if (!result.IsHealthy)
                {
                    Logger.Warn("Mode system health check failed: {Issues}", SanitizeForLogging(string.Join(", ", result.Issues)));
                }
            }
            catch (Exception ex)
            {
                Logger.Error("Error during health check", ex);
            }
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            try
            {
                _healthCheckTimer?.Dispose();
            }
            catch (Exception ex)
            {
                Logger.Error("Error disposing health check timer", ex);
            }

            Logger.Info("ModeDiagnostics disposed");
        }
    }

    public class ModeTransitionEvent
    {
        public DateTime Timestamp { get; set; }
        public InputMode FromMode { get; set; }
        public InputMode ToMode { get; set; }
        public bool Success { get; set; }
        public Exception? Exception { get; set; }
        public string? Details { get; set; }
        public int ThreadId { get; set; }
    }

    public class HealthCheckResult
    {
        public DateTime Timestamp { get; set; }
        public bool IsHealthy { get; set; }
        public List<string> Issues { get; set; } = new List<string>();
    }
}