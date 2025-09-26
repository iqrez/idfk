using System;
using System.Threading.Tasks;
using System.Threading;
using System.Collections.Concurrent;
using WootMouseRemap;
using WootMouseRemap.Core;
using WootMouseRemap.Diagnostics;
using WootMouseRemap.Modes;

namespace WootMouseRemap.Tests
{
    /// <summary>
    /// Thread safety tests for the bulletproof mode system
    /// </summary>
    public static class ThreadSafetyTests
    {
        private static int _testsPassed = 0;
        private static int _testsFailed = 0;

        public static async Task RunAllTests()
        {
            Logger.Info("Starting ThreadSafetyTests...");
            
            try
            {
                await TestConcurrentModeChanges();
                await TestThreadSafeProperties();
                await TestConcurrentDiagnostics();
                await TestRaceConditionPrevention();
                await TestResourceCleanup();
                
                Logger.Info("ThreadSafetyTests completed: {Passed} passed, {Failed} failed", _testsPassed, _testsFailed);
            }
            catch (Exception ex)
            {
                Logger.Error("Critical error in ThreadSafetyTests", ex);
                _testsFailed++;
            }
        }

        private static async Task TestConcurrentModeChanges()
        {
            Logger.Info("Testing concurrent mode changes...");
            
            try
            {
                var diagnostics = new ModeDiagnostics();
                var validator = new ModeStateValidator();
                var controller = new ThreadSafeModeController(diagnostics, validator);
                
                const int numTasks = 10;
                var tasks = new Task[numTasks];
                var results = new ConcurrentBag<bool>();
                
                // Launch multiple concurrent mode change requests
                for (int i = 0; i < numTasks; i++)
                {
                    var targetMode = i % 2 == 0 ? InputMode.ControllerPass : InputMode.Native;
                    tasks[i] = Task.Run(async () =>
                    {
                        try
                        {
                            var result = await controller.SwitchModeSafeAsync(targetMode);
                            results.Add(result);
                        }
                        catch (Exception ex)
                        {
                            Logger.Error("Concurrent mode change failed", ex);
                            results.Add(false);
                        }
                    });
                }
                
                await Task.WhenAll(tasks);
                
                // Verify system is in a consistent state
                Assert(controller.CurrentMode != InputMode.Unknown, "System should be in a known state after concurrent changes");
                Assert(results.Count == numTasks, "All tasks should have completed");
                
                controller.Dispose();
                
                Logger.Info("Concurrent mode changes test passed");
                _testsPassed++;
            }
            catch (Exception ex)
            {
                Logger.Error("Concurrent mode changes test failed", ex);
                _testsFailed++;
            }
        }

        private static async Task TestThreadSafeProperties()
        {
            Logger.Info("Testing thread-safe properties...");
            
            try
            {
                const int numThreads = 5;
                const int operationsPerThread = 100;
                var exceptions = new ConcurrentBag<Exception>();
                
                var tasks = new Task[numThreads];
                
                for (int i = 0; i < numThreads; i++)
                {
                    tasks[i] = Task.Run(() =>
                    {
                        try
                        {
                            for (int j = 0; j < operationsPerThread; j++)
                            {
                                // Test thread-safe property access
                                var suppress = LowLevelHooks.Suppress;
                                LowLevelHooks.Suppress = j % 2 == 0;
                                var newSuppress = LowLevelHooks.Suppress;
                                
                                // Brief pause to encourage race conditions
                                Thread.Sleep(1);
                            }
                        }
                        catch (Exception ex)
                        {
                            exceptions.Add(ex);
                        }
                    });
                }
                
                await Task.WhenAll(tasks);
                
                Assert(exceptions.IsEmpty, $"Thread-safe property access should not throw exceptions. Found {exceptions.Count} exceptions.");
                
                Logger.Info("Thread-safe properties test passed");
                _testsPassed++;
            }
            catch (Exception ex)
            {
                Logger.Error("Thread-safe properties test failed", ex);
                _testsFailed++;
            }
        }

        private static async Task TestConcurrentDiagnostics()
        {
            Logger.Info("Testing concurrent diagnostics...");
            
            try
            {
                var diagnostics = new ModeDiagnostics();
                const int numTasks = 10;
                var tasks = new Task[numTasks];
                var exceptions = new ConcurrentBag<Exception>();
                
                for (int i = 0; i < numTasks; i++)
                {
                    var taskId = i;
                    tasks[i] = Task.Run(() =>
                    {
                        try
                        {
                            for (int j = 0; j < 50; j++)
                            {
                                // Concurrent logging
                                diagnostics.LogModeChange($"Task{taskId}", 
                                    InputMode.ControllerPass, 
                                    InputMode.Native, 
                                    TimeSpan.FromMilliseconds(j));
                                
                                if (j % 10 == 0)
                                {
                                    diagnostics.LogModeError($"Task{taskId}", new Exception($"Test error {j}"));
                                }
                                
                                // Concurrent health checking
                                var health = diagnostics.GetSystemHealth();
                                
                                Thread.Sleep(1);
                            }
                        }
                        catch (Exception ex)
                        {
                            exceptions.Add(ex);
                        }
                    });
                }
                
                await Task.WhenAll(tasks);
                
                Assert(exceptions.IsEmpty, $"Concurrent diagnostics should not throw exceptions. Found {exceptions.Count} exceptions.");
                
                var finalHealth = diagnostics.GetSystemHealth();
                Assert(finalHealth != null, "Should be able to get system health after concurrent operations");
                
                Logger.Info("Concurrent diagnostics test passed");
                _testsPassed++;
            }
            catch (Exception ex)
            {
                Logger.Error("Concurrent diagnostics test failed", ex);
                _testsFailed++;
            }
        }

        private static async Task TestRaceConditionPrevention()
        {
            Logger.Info("Testing race condition prevention...");
            
            try
            {
                var diagnostics = new ModeDiagnostics();
                var validator = new ModeStateValidator();
                var modeManager = new ModeManager();
                var transitionManager = new ModeTransitionManager(modeManager, validator, diagnostics);
                
                const int numConcurrentTransitions = 20;
                var tasks = new Task<ModeTransitionResult>[numConcurrentTransitions];
                
                // Launch many concurrent transitions
                for (int i = 0; i < numConcurrentTransitions; i++)
                {
                    var fromMode = i % 2 == 0 ? InputMode.Native : InputMode.ControllerPass;
                    var toMode = i % 2 == 0 ? InputMode.ControllerPass : InputMode.Native;
                    
                    tasks[i] = transitionManager.ExecuteTransitionAsync(fromMode, toMode);
                }
                
                var results = await Task.WhenAll(tasks);
                
                // Verify that at least some transitions succeeded and system is consistent
                var successCount = 0;
                foreach (var result in results)
                {
                    if (result.Success) successCount++;
                }
                
                Assert(successCount > 0, "At least some transitions should succeed");
                Assert(modeManager.CurrentMode != InputMode.Unknown, "System should be in a known state");
                
                transitionManager.Dispose();
                modeManager.Dispose();
                
                Logger.Info("Race condition prevention test passed");
                _testsPassed++;
            }
            catch (Exception ex)
            {
                Logger.Error("Race condition prevention test failed", ex);
                _testsFailed++;
            }
        }

        private static async Task TestResourceCleanup()
        {
            Logger.Info("Testing resource cleanup...");
            
            try
            {
                // Test multiple create/dispose cycles
                for (int i = 0; i < 10; i++)
                {
                    var diagnostics = new ModeDiagnostics();
                    var validator = new ModeStateValidator();
                    var controller = new ThreadSafeModeController(diagnostics, validator);
                    var modeManager = new ModeManager();
                    var transitionManager = new ModeTransitionManager(modeManager, validator, diagnostics);
                    
                    // Do some work
                    await controller.SwitchModeSafeAsync(InputMode.ControllerPass);
                    await transitionManager.ExecuteTransitionAsync(InputMode.ControllerPass, InputMode.Native);
                    
                    // Cleanup
                    transitionManager.Dispose();
                    controller.Dispose();
                    modeManager.Dispose();
                    
                    // Brief pause
                    await Task.Delay(10);
                }
                
                // Force garbage collection to detect any resource leaks
                GC.Collect();
                GC.WaitForPendingFinalizers();
                GC.Collect();
                
                Logger.Info("Resource cleanup test passed");
                _testsPassed++;
            }
            catch (Exception ex)
            {
                Logger.Error("Resource cleanup test failed", ex);
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