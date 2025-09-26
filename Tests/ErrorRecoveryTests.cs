using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using WootMouseRemap.Core;
using WootMouseRemap.Diagnostics;
using WootMouseRemap.Modes;

namespace WootMouseRemap.Tests
{
    [TestClass]
    public class ErrorRecoveryTests
    {
        private ThreadSafeModeController _modeController;
        private ModeTransitionManager _transitionManager;
        private ModeDiagnostics _diagnostics;
        private ModeStateValidator _validator;

        [TestInitialize]
        public void Setup()
        {
            _diagnostics = new ModeDiagnostics();
            _validator = new ModeStateValidator(_diagnostics);
            _transitionManager = new ModeTransitionManager(_diagnostics, _validator);
            _modeController = new ThreadSafeModeController(_transitionManager, _diagnostics);
        }

        [TestCleanup]
        public void Cleanup()
        {
            _modeController?.Dispose();
            _transitionManager?.Dispose();
            _diagnostics?.Dispose();
        }

        [TestMethod]
        public async Task RecoverFromFailedModeTransition()
        {
            // Start in a known good state
            await _modeController.SwitchModeSafeAsync(InputMode.ControllerPass);
            Assert.AreEqual(InputMode.ControllerPass, _modeController.CurrentMode);

            // Simulate a transition failure by forcing an exception
            var originalMode = _modeController.CurrentMode;
            
            // This should fail gracefully and not leave us in an inconsistent state
            try
            {
                await _modeController.SwitchModeSafeAsync((InputMode)999); // Invalid mode
                Assert.Fail("Expected exception for invalid mode");
            }
            catch
            {
                // Expected to fail
            }

            // Should still be in original mode
            Assert.AreEqual(originalMode, _modeController.CurrentMode);
            
            // Should be able to transition to valid mode after failure
            await _modeController.SwitchModeSafeAsync(InputMode.Native);
            Assert.AreEqual(InputMode.Native, _modeController.CurrentMode);
        }

        [TestMethod]
        public async Task RecoverFromResourceExhaustion()
        {
            // Simulate resource exhaustion by creating many mode switches rapidly
            var tasks = new Task[100];
            var random = new Random();

            for (int i = 0; i < tasks.Length; i++)
            {
                tasks[i] = Task.Run(async () =>
                {
                    try
                    {
                        var mode = random.Next(2) == 0 ? InputMode.Native : InputMode.ControllerPass;
                        await _modeController.SwitchModeSafeAsync(mode);
                        await Task.Delay(10); // Small delay to create contention
                    }
                    catch
                    {
                        // Expected to have some failures under extreme load
                    }
                });
            }

            await Task.WhenAll(tasks);

            // System should still be responsive and in a valid state
            var finalMode = _modeController.CurrentMode;
            Assert.IsTrue(finalMode == InputMode.Native || finalMode == InputMode.ControllerPass);

            // Should be able to switch modes normally after stress test
            await _modeController.SwitchModeSafeAsync(InputMode.Native);
            Assert.AreEqual(InputMode.Native, _modeController.CurrentMode);
        }

        [TestMethod]
        public async Task HandleExceptionInModeApply()
        {
            // Create a mode that will throw during Apply
            var faultyMode = new FaultyMode();
            
            var originalMode = _modeController.CurrentMode;

            try
            {
                // This should fail but not crash the system
                await _modeController.SwitchToModeAsync(faultyMode);
                Assert.Fail("Expected exception from faulty mode");
            }
            catch
            {
                // Expected to fail
            }

            // Should have rolled back to original mode
            Assert.AreEqual(originalMode, _modeController.CurrentMode);
        }

        [TestMethod]
        public void RecoverFromHealthCheckFailures()
        {
            // Force some failed transitions to trigger health issues
            _diagnostics.LogModeTransition(InputMode.Native, InputMode.ControllerPass, false, 
                new Exception("Test failure 1"));
            _diagnostics.LogModeTransition(InputMode.ControllerPass, InputMode.Native, false, 
                new Exception("Test failure 2"));
            _diagnostics.LogModeTransition(InputMode.Native, InputMode.ControllerPass, false, 
                new Exception("Test failure 3"));
            _diagnostics.LogModeTransition(InputMode.ControllerPass, InputMode.Native, false, 
                new Exception("Test failure 4"));

            bool healthCheckFailed = false;
            _diagnostics.HealthCheckCompleted += result =>
            {
                if (!result.IsHealthy)
                {
                    healthCheckFailed = true;
                    Assert.IsTrue(result.Issues.Count > 0);
                }
            };

            // Wait for health check to run
            Thread.Sleep(6000); // Health check runs every 5 seconds
            
            Assert.IsTrue(healthCheckFailed, "Health check should have detected failures");

            // System should still be functional despite health issues
            var currentState = _diagnostics.GetCurrentSystemState();
            Assert.IsNotNull(currentState);
        }

        [TestMethod]
        public async Task HandleDisposalDuringOperation()
        {
            // Start a long-running operation
            var operationTask = Task.Run(async () =>
            {
                for (int i = 0; i < 10; i++)
                {
                    try
                    {
                        await _modeController.SwitchModeSafeAsync(
                            i % 2 == 0 ? InputMode.Native : InputMode.ControllerPass);
                        await Task.Delay(100);
                    }
                    catch (ObjectDisposedException)
                    {
                        // Expected when disposed during operation
                        return;
                    }
                }
            });

            // Dispose while operation is running
            await Task.Delay(200);
            _modeController.Dispose();

            // Operation should complete gracefully
            await operationTask;
            Assert.IsTrue(operationTask.IsCompleted);
        }

        [TestMethod]
        public void ValidateStateConsistency()
        {
            // Test various state combinations
            _diagnostics.UpdateSystemState("CurrentMode", InputMode.Native);
            _diagnostics.UpdateSystemState("LowLevelHooks.Suppress", true);

            var issues = _validator.ValidateState();
            Assert.AreEqual(0, issues.Count, "Native with suppression ON should be valid");

            // Test invalid combination
            _diagnostics.UpdateSystemState("CurrentMode", InputMode.ControllerPass);
            _diagnostics.UpdateSystemState("LowLevelHooks.Suppress", true);

            issues = _validator.ValidateState();
            Assert.IsTrue(issues.Count > 0, "ControllerPass with suppression ON should be invalid");
        }

        // Helper class for testing error handling
        private class FaultyMode : IModeHandler
        {
            public InputMode Mode => (InputMode)999;
            public bool ShouldSuppressInput => false;

            public Task<bool> ApplyAsync()
            {
                throw new InvalidOperationException("Simulated mode failure");
            }

            public Task<bool> UnapplyAsync()
            {
                return Task.FromResult(true);
            }

            public void OnModeEntered(InputMode previousMode) { }
            public void OnModeExited(InputMode nextMode) { }
            public void OnKey(int vk, bool down) { }
            public void OnMouseButton(MouseInput button, bool down) { }
            public void OnMouseMove(int dx, int dy) { }
            public void OnWheel(int delta) { }
            public void OnControllerConnected(int index) { }
            public void OnControllerDisconnected(int index) { }
            public void Update() { }
            public string GetStatusText() => "Faulty Mode";

            public void Dispose()
            {
                // Nothing to dispose
            }
        }
    }
}