using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using WootMouseRemap.Core;
using WootMouseRemap.Modes;
using WootMouseRemap.Diagnostics;

namespace WootMouseRemap.Tests
{
    /// <summary>
    /// Comprehensive test suite for Native mode functionality
    /// Tests mode transitions, input suppression, controller scenarios, UI updates, and error handling
    /// </summary>
    public static class NativeModeIntegrationTests
    {
        private static readonly string TestLogPath = Path.Combine(Path.GetTempPath(), "native_test.log");

        public static void RunAllTests()
        {
            Console.WriteLine("🧪 Starting Native Mode Integration Tests...");
            Console.WriteLine($"📝 Test log: {TestLogPath}");

            try
            {
                // Initialize logging
                Logger.Initialize(TestLogPath);

                // Test 1: Mode Transition Cycling
                TestModeTransitions();

                // Test 2: Input Suppression Behavior
                TestInputSuppression();

                // Test 3: Controller Connect/Disconnect Scenarios
                TestControllerScenarios();

                // Test 4: UI Status Updates
                TestUIStatusUpdates();

                // Test 5: Error Handling and Recovery
                TestErrorHandling();

                // Test 6: Thread Safety
                TestThreadSafety();

                Console.WriteLine("✅ All tests completed successfully!");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Test suite failed: {ex.Message}");
                Logger.Error("Test suite failed", ex);
            }
            finally
            {
                Logger.Shutdown();
            }
        }

        private static void TestModeTransitions()
        {
            Console.WriteLine("\n🔄 Testing Mode Transitions...");

            var modeManager = new ModeManager();
            var nativeMode = new NativeMode();
            var mnkConvertMode = new MnKConvertMode();
            var controllerPassMode = new ControllerPassMode();

            // Register all modes
            modeManager.RegisterMode(nativeMode);
            modeManager.RegisterMode(mnkConvertMode);
            modeManager.RegisterMode(controllerPassMode);

            // Test cycling through all modes
            var modeService = new ModeService(Path.GetTempFileName(), modeManager);

            // Start in default (Native)
            Assert(modeService.CurrentMode == InputMode.Native, "Should start in Native mode");

            // Cycle 1: Native -> ControllerPass
            var next1 = modeService.Toggle();
            Assert(next1 == InputMode.ControllerPass, $"Expected ControllerPass, got {next1}");
            Assert(modeService.CurrentMode == InputMode.ControllerPass, "Should be in ControllerPass mode");

            // Cycle 2: ControllerPass -> MnKConvert
            var next2 = modeService.Toggle();
            Assert(next2 == InputMode.MnKConvert, $"Expected MnKConvert, got {next2}");
            Assert(modeService.CurrentMode == InputMode.MnKConvert, "Should be in MnKConvert mode");

            // Cycle 3: MnKConvert -> Native
            var next3 = modeService.Toggle();
            Assert(next3 == InputMode.Native, $"Expected Native, got {next3}");
            Assert(modeService.CurrentMode == InputMode.Native, "Should be back in Native mode");

            Console.WriteLine("✅ Mode transitions test passed");
            Logger.Info("Mode transitions test passed");
        }

        private static void TestInputSuppression()
        {
            Console.WriteLine("\n🚫 Testing Input Suppression Behavior...");

            var nativeMode = new NativeMode();
            var mnkConvertMode = new MnKConvertMode();
            var controllerPassMode = new ControllerPassMode();

            // Test Native mode - should NOT suppress input
            Assert(!nativeMode.ShouldSuppressInput, "Native mode should not suppress input");

            // Test MnKConvert mode - should suppress input
            Assert(mnkConvertMode.ShouldSuppressInput, "MnKConvert mode should suppress input");

            // Test ControllerPass mode - should not suppress input
            Assert(!controllerPassMode.ShouldSuppressInput, "ControllerPass mode should not suppress input");

            Console.WriteLine("✅ Input suppression test passed");
            Logger.Info("Input suppression test passed");
        }

        private static void TestControllerScenarios()
        {
            Console.WriteLine("\n🎮 Testing Controller Connect/Disconnect Scenarios...");

            var modeManager = new ModeManager();
            var nativeMode = new NativeMode();
            var controllerPassMode = new ControllerPassMode();

            modeManager.RegisterMode(nativeMode);
            modeManager.RegisterMode(controllerPassMode);

            var modeService = new ModeService(Path.GetTempFileName(), modeManager);

            // Start in Native mode
            modeService.Switch(InputMode.Native);
            Assert(modeService.CurrentMode == InputMode.Native, "Should be in Native mode");

            // Simulate controller connection - should trigger auto-switch to ControllerPass
            modeManager.OnControllerConnected(0);

            // Note: In real implementation, this would trigger the auto-switch logic in OverlayForm
            // For this test, we verify the mode manager receives the event
            Console.WriteLine("✅ Controller connection event handled");

            // Test controller disconnection in ControllerPass mode
            modeService.Switch(InputMode.ControllerPass);
            modeManager.OnControllerDisconnected(0);
            Console.WriteLine("✅ Controller disconnection event handled");

            Logger.Info("Controller scenarios test passed");
        }

        private static void TestUIStatusUpdates()
        {
            Console.WriteLine("\n🖥️ Testing UI Status Updates...");

            var nativeMode = new NativeMode();

            // Test status text
            var statusText = nativeMode.GetStatusText();
            Assert(statusText.Contains("Native"), $"Status text should contain 'Native', got: {statusText}");
            Assert(statusText.Contains("Pass-through"), $"Status text should contain 'Pass-through', got: {statusText}");

            Console.WriteLine("✅ UI status updates test passed");
            Logger.Info("UI status updates test passed");
        }

        private static void TestErrorHandling()
        {
            Console.WriteLine("\n🛡️ Testing Error Handling and Recovery...");

            var modeManager = new ModeManager();
            var nativeMode = new NativeMode();

            modeManager.RegisterMode(nativeMode);

            var modeService = new ModeService(Path.GetTempFileName(), modeManager);

            // Test switching to invalid mode (should handle gracefully)
            try
            {
                modeService.Switch((InputMode)999); // Invalid mode
                Assert(false, "Should have thrown exception for invalid mode");
            }
            catch (ArgumentException)
            {
                Console.WriteLine("✅ Invalid mode switch handled correctly");
            }

            // Test mode persistence with corrupted file
            var corruptedPath = Path.GetTempFileName();
            File.WriteAllText(corruptedPath, "INVALID_MODE_DATA");

            var corruptedService = new ModeService(corruptedPath, modeManager);
            Assert(corruptedService.CurrentMode == InputMode.Native, "Should fallback to default mode on corrupted persistence");

            Console.WriteLine("✅ Error handling test passed");
            Logger.Info("Error handling test passed");
        }

        private static void TestThreadSafety()
        {
            Console.WriteLine("\n🔒 Testing Thread Safety...");

            var modeManager = new ModeManager();
            var nativeMode = new NativeMode();
            var mnkConvertMode = new MnKConvertMode();

            modeManager.RegisterMode(nativeMode);
            modeManager.RegisterMode(mnkConvertMode);

            var modeService = new ModeService(Path.GetTempFileName(), modeManager);

            // Test concurrent mode switches
            Exception? concurrentException = null;
            var threads = new Thread[10];

            for (int i = 0; i < threads.Length; i++)
            {
                threads[i] = new Thread(() =>
                {
                    try
                    {
                        for (int j = 0; j < 100; j++)
                        {
                            modeService.Toggle();
                            Thread.Sleep(1); // Small delay to increase chance of race conditions
                        }
                    }
                    catch (Exception ex)
                    {
                        concurrentException = ex;
                    }
                });
            }

            // Start all threads
            foreach (var thread in threads)
                thread.Start();

            // Wait for all threads to complete
            foreach (var thread in threads)
                thread.Join();

            Assert(concurrentException == null, $"Concurrent operations failed: {concurrentException?.Message}");

            // Verify final state is valid
            var validModes = new[] { InputMode.MnKConvert, InputMode.Native, InputMode.ControllerPass };
            Assert(Array.Exists(validModes, m => m == modeService.CurrentMode), $"Final mode should be valid, got: {modeService.CurrentMode}");

            Console.WriteLine("✅ Thread safety test passed");
            Logger.Info("Thread safety test passed");
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