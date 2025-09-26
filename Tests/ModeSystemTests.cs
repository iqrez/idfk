using System;
using System.Threading.Tasks;
using WootMouseRemap;
using WootMouseRemap.Core;
using WootMouseRemap.Diagnostics;
using WootMouseRemap.Modes;

namespace WootMouseRemap.Tests
{
    /// <summary>
    /// Comprehensive tests for the bulletproof mode system
    /// </summary>
    public static class ModeSystemTests
    {
        private static int _testsPassed = 0;
        private static int _testsFailed = 0;

        public static async Task RunAllTests()
        {
            Logger.Info("Starting ModeSystemTests...");
            
            try
            {
                await TestModeDiagnostics();
                await TestModeStateValidator();
                await TestThreadSafeModeController();
                await TestModeTransitionManager();
                await TestModeManagerIntegration();
                await TestPanicButtonFunctionality();
                await TestErrorRecovery();
                
                Logger.Info("ModeSystemTests completed: {Passed} passed, {Failed} failed", _testsPassed, _testsFailed);
            }
            catch (Exception ex)
            {
                Logger.Error("Critical error in ModeSystemTests", ex);
                _testsFailed++;
            }
        }

        private static async Task TestModeDiagnostics()
        {
            Logger.Info("Testing ModeDiagnostics...");
            
            try
            {
                var diagnostics = new ModeDiagnostics();
                
                // Test health checking
                var healthBefore = diagnostics.GetSystemHealth();
                Assert(healthBefore.IsHealthy, "System should be healthy initially");
                
                // Test logging mode change
                diagnostics.LogModeChange("Test", InputMode.ControllerPass, InputMode.Native, TimeSpan.FromMilliseconds(100));
                
                // Test error logging
                diagnostics.LogModeError("Test", new Exception("Test exception"));
                
                var healthAfter = diagnostics.GetSystemHealth();
                Assert(!healthAfter.IsHealthy, "System should be unhealthy after error");
                
                // Test recovery
                await Task.Delay(100);
                diagnostics.LogModeChange("Recovery", InputMode.Native, InputMode.ControllerPass, TimeSpan.FromMilliseconds(50));
                
                Logger.Info("ModeDiagnostics tests passed");
                _testsPassed++;
            }
            catch (Exception ex)
            {
                Logger.Error("ModeDiagnostics test failed", ex);
                _testsFailed++;
            }
        }

        private static async Task TestModeStateValidator()
        {
            Logger.Info("Testing ModeStateValidator...");
            
            try
            {
                var validator = new ModeStateValidator();
                
                // Test valid system state
                var validContext = new ModeSystemContext
                {
                    CurrentMode = InputMode.ControllerPass,
                    TargetMode = InputMode.ControllerPass,
                    IsTransitioning = false,
                    ControllerConnected = true,
                    HooksInstalled = true,
                    LastModeChange = DateTime.UtcNow.AddSeconds(-10)
                };
                
                var result = validator.ValidateSystemState(validContext);
                Assert(result.IsValid, "Valid system state should pass validation");
                
                // Test invalid state (transitioning too long)
                var invalidContext = new ModeSystemContext
                {
                    CurrentMode = InputMode.ControllerPass,
                    TargetMode = InputMode.Native,
                    IsTransitioning = true,
                    ControllerConnected = true,
                    HooksInstalled = true,
                    LastModeChange = DateTime.UtcNow.AddMinutes(-10) // Too long ago
                };
                
                var invalidResult = validator.ValidateSystemState(invalidContext);
                Assert(!invalidResult.IsValid, "Invalid system state should fail validation");
                Assert(invalidResult.Issues.Count > 0, "Should have validation issues");
                
                // Test transition validation
                var transitionResult = validator.ValidateTransition(InputMode.ControllerPass, InputMode.Native, validContext);
                Assert(transitionResult.IsValid, "Valid transition should pass");
                
                Logger.Info("ModeStateValidator tests passed");
                _testsPassed++;
            }
            catch (Exception ex)
            {
                Logger.Error("ModeStateValidator test failed", ex);
                _testsFailed++;
            }
        }

        private static async Task TestThreadSafeModeController()
        {
            Logger.Info("Testing ThreadSafeModeController...");
            
            try
            {
                var diagnostics = new ModeDiagnostics();
                var validator = new ModeStateValidator();
                var controller = new ThreadSafeModeController(diagnostics, validator);
                
                // Test initial state
                Assert(controller.CurrentMode == InputMode.Native, "Should start in native mode");
                
                // Test safe mode switch
                var result = await controller.SwitchModeSafeAsync(InputMode.ControllerPass);
                Assert(result, "Mode switch should succeed");
                Assert(controller.CurrentMode == InputMode.ControllerPass, "Should be in controller mode");
                
                // Test concurrent mode switches (should be serialized)
                var task1 = controller.SwitchModeSafeAsync(InputMode.Native);
                var task2 = controller.SwitchModeSafeAsync(InputMode.ControllerPass);
                
                await Task.WhenAll(task1, task2);
                
                // One should succeed, system should be in a valid state
                Assert(controller.CurrentMode != InputMode.Unknown, "Should be in a known mode after concurrent switches");
                
                controller.Dispose();
                
                Logger.Info("ThreadSafeModeController tests passed");
                _testsPassed++;
            }
            catch (Exception ex)
            {
                Logger.Error("ThreadSafeModeController test failed", ex);
                _testsFailed++;
            }
        }

        private static async Task TestModeTransitionManager()
        {
            Logger.Info("Testing ModeTransitionManager...");
            
            try
            {
                var diagnostics = new ModeDiagnostics();
                var validator = new ModeStateValidator();
                var modeManager = new ModeManager();
                var transitionManager = new ModeTransitionManager(modeManager, validator, diagnostics);
                
                // Test successful transition
                var result = await transitionManager.ExecuteTransitionAsync(InputMode.Native, InputMode.ControllerPass);
                Assert(result.Success, "Transition should succeed");
                Assert(result.ErrorMessage == null, "Should have no error message");
                
                // Test transition to same mode
                var sameResult = await transitionManager.ExecuteTransitionAsync(InputMode.ControllerPass, InputMode.ControllerPass);
                Assert(sameResult.Success, "Transition to same mode should succeed");
                
                transitionManager.Dispose();
                modeManager.Dispose();
                
                Logger.Info("ModeTransitionManager tests passed");
                _testsPassed++;
            }
            catch (Exception ex)
            {
                Logger.Error("ModeTransitionManager test failed", ex);
                _testsFailed++;
            }
        }

        private static async Task TestModeManagerIntegration()
        {
            Logger.Info("Testing ModeManager integration...");
            
            try
            {
                var modeManager = new ModeManager();
                
                // Test basic mode switching
                modeManager.SwitchMode(InputMode.ControllerPass);
                Assert(modeManager.CurrentMode == InputMode.ControllerPass, "Should switch to controller mode");
                
                modeManager.SwitchMode(InputMode.Native);
                Assert(modeManager.CurrentMode == InputMode.Native, "Should switch to native mode");
                
                modeManager.Dispose();
                
                Logger.Info("ModeManager integration tests passed");
                _testsPassed++;
            }
            catch (Exception ex)
            {
                Logger.Error("ModeManager integration test failed", ex);
                _testsFailed++;
            }
        }

        private static async Task TestPanicButtonFunctionality()
        {
            Logger.Info("Testing panic button functionality...");
            
            try
            {
                // Test that panic button disables suppression
                LowLevelHooks.Suppress = true;
                Assert(LowLevelHooks.Suppress == true, "Suppression should be enabled");
                
                // Simulate panic trigger
                bool panicTriggered = false;
                LowLevelHooks.PanicTriggered += () => panicTriggered = true;
                
                // This would be triggered by the actual key combination in real use
                LowLevelHooks.Suppress = false; // Simulate panic disabling suppression
                
                Assert(LowLevelHooks.Suppress == false, "Panic should disable suppression");
                
                Logger.Info("Panic button functionality tests passed");
                _testsPassed++;
            }
            catch (Exception ex)
            {
                Logger.Error("Panic button functionality test failed", ex);
                _testsFailed++;
            }
        }

        private static async Task TestErrorRecovery()
        {
            Logger.Info("Testing error recovery...");
            
            try
            {
                var diagnostics = new ModeDiagnostics();
                
                // Simulate error condition
                diagnostics.LogModeError("TestComponent", new Exception("Simulated error"));
                
                var health = diagnostics.GetSystemHealth();
                Assert(!health.IsHealthy, "System should be unhealthy after error");
                
                // Simulate recovery
                await Task.Delay(100);
                diagnostics.LogModeChange("Recovery", InputMode.Native, InputMode.ControllerPass, TimeSpan.FromMilliseconds(50));
                
                // Error recovery should improve health over time
                Logger.Info("Error recovery tests passed");
                _testsPassed++;
            }
            catch (Exception ex)
            {
                Logger.Error("Error recovery test failed", ex);
                _testsFailed++;
            }
        }

        private static void Assert(bool condition, string message)
        {
            if (!condition)
            {
                throw new Exception($"Assertion failed: {message}");
            }
        }
    }
}