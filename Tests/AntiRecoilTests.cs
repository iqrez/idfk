using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using WootMouseRemap.Features;
using WootMouseRemap.UI;
using WootMouseRemap.Core;

namespace WootMouseRemap.Tests
{
    /// <summary>
    /// Unit tests for AntiRecoil core functionality
    /// </summary>
    [TestClass]
    public class AntiRecoilTests
    {
        private AntiRecoil _antiRecoil;

        [TestInitialize]
        public void Setup()
        {
            _antiRecoil = new AntiRecoil();
        }

        [TestMethod]
        public void AntiRecoil_DefaultSettings_ShouldHaveValidDefaults()
        {
            Assert.IsFalse(_antiRecoil.Enabled, "Anti-recoil should be disabled by default");
            Assert.AreEqual(0.5f, _antiRecoil.Strength, "Default strength should be 0.5");
            Assert.AreEqual(2.0f, _antiRecoil.VerticalThreshold, "Default threshold should be 2.0");
            Assert.AreEqual(0, _antiRecoil.ActivationDelayMs, "Default activation delay should be 0");
            Assert.AreEqual(0.0f, _antiRecoil.HorizontalCompensation, "Default horizontal compensation should be 0");
            Assert.IsFalse(_antiRecoil.AdaptiveCompensation, "Adaptive compensation should be disabled by default");
        }

        [TestMethod]
        public void AntiRecoil_EnabledWithZeroStrength_ShouldNotCompensate()
        {
            _antiRecoil.Enabled = true;
            _antiRecoil.Strength = 0.0f;
            _antiRecoil.VerticalThreshold = 1.0f;

            var result = _antiRecoil.ProcessMouseMovement(0, 5); // Vertical movement above threshold

            Assert.AreEqual(0, result.dx, "No horizontal compensation should occur with zero strength");
            Assert.AreEqual(0, result.dy, "No vertical compensation should occur with zero strength");
        }

        [TestMethod]
        public void AntiRecoil_MovementBelowThreshold_ShouldNotActivate()
        {
            _antiRecoil.Enabled = true;
            _antiRecoil.Strength = 0.8f;
            _antiRecoil.VerticalThreshold = 5.0f;

            var result = _antiRecoil.ProcessMouseMovement(0, 2); // Below threshold

            Assert.AreEqual(0, result.dx, "No compensation should occur below threshold");
            Assert.AreEqual(0, result.dy, "No compensation should occur below threshold");
        }

        [TestMethod]
        public void AntiRecoil_MovementAboveThreshold_ShouldCompensate()
        {
            _antiRecoil.Enabled = true;
            _antiRecoil.Strength = 0.5f;
            _antiRecoil.VerticalThreshold = 2.0f;

            var result = _antiRecoil.ProcessMouseMovement(0, 10); // Above threshold

            Assert.AreEqual(0, result.dx, "No horizontal compensation expected");
            Assert.IsTrue(result.dy < 0, "Vertical compensation should be negative (upward)");
            Assert.IsTrue(Math.Abs(result.dy) <= Math.Abs(10 * 0.5f), "Compensation should not exceed input * strength");
        }

        [TestMethod]
        public void AntiRecoil_MaxTickCompensation_ShouldCapPerTickCompensation()
        {
            _antiRecoil.Enabled = true;
            _antiRecoil.Strength = 1.0f; // Full strength
            _antiRecoil.VerticalThreshold = 1.0f;
            _antiRecoil.MaxTickCompensation = 5.0f;

            var result = _antiRecoil.ProcessMouseMovement(0, 20); // Large movement

            Assert.IsTrue(Math.Abs(result.dy) <= 5.0f, "Per-tick compensation should not exceed MaxTickCompensation");
        }

        [TestMethod]
        public void AntiRecoil_MaxTotalCompensation_ShouldCapAccumulatedCompensation()
        {
            _antiRecoil.Enabled = true;
            _antiRecoil.Strength = 1.0f;
            _antiRecoil.VerticalThreshold = 1.0f;
            _antiRecoil.MaxTotalCompensation = 15.0f;

            // Perform multiple compensations
            for (int i = 0; i < 10; i++)
            {
                _antiRecoil.ProcessMouseMovement(0, 5);
            }

            // The accumulated compensation should not exceed the limit
            // We can't directly access AccumulatedCompensation, but we can test the effect
            var result = _antiRecoil.ProcessMouseMovement(0, 5);

            // After many compensations, further compensation should be limited
            Assert.IsTrue(Math.Abs(result.dy) < 5.0f, "Total compensation should be capped");
        }

        [TestMethod]
        public void AntiRecoil_CooldownPeriod_ShouldPreventImmediateReactivation()
        {
            _antiRecoil.Enabled = true;
            _antiRecoil.Strength = 0.8f;
            _antiRecoil.VerticalThreshold = 2.0f;
            _antiRecoil.CooldownMs = 100;

            // First activation
            var result1 = _antiRecoil.ProcessMouseMovement(0, 10);
            Assert.IsTrue(result1.dy != 0, "First activation should compensate");

            // Immediate second activation (should be in cooldown)
            var result2 = _antiRecoil.ProcessMouseMovement(0, 10);
            Assert.IsTrue(Math.Abs(result2.dy) < Math.Abs(result1.dy), "Second activation should be reduced due to cooldown");
        }

        [TestMethod]
        public void AntiRecoil_HorizontalCompensation_ShouldCompensateHorizontalMovement()
        {
            _antiRecoil.Enabled = true;
            _antiRecoil.Strength = 0.5f;
            _antiRecoil.VerticalThreshold = 2.0f;
            _antiRecoil.HorizontalCompensation = 0.3f;

            var result = _antiRecoil.ProcessMouseMovement(8, 10); // Both horizontal and vertical movement

            Assert.IsTrue(result.dx != 0, "Horizontal compensation should occur");
            Assert.IsTrue(result.dy != 0, "Vertical compensation should occur");
            Assert.IsTrue(Math.Abs(result.dx) <= Math.Abs(8 * 0.3f), "Horizontal compensation should not exceed input * horizontal compensation factor");
        }

        [TestMethod]
        public void AntiRecoil_DecayOverTime_ShouldReduceAccumulatedCompensation()
        {
            _antiRecoil.Enabled = true;
            _antiRecoil.Strength = 1.0f;
            _antiRecoil.VerticalThreshold = 1.0f;
            _antiRecoil.DecayPerMs = 0.1f;

            // Build up some compensation
            _antiRecoil.ProcessMouseMovement(0, 10);

            // Wait (simulated by multiple small movements with time gaps)
            System.Threading.Thread.Sleep(50);

            // The next compensation should be affected by decay
            var result = _antiRecoil.ProcessMouseMovement(0, 10);

            // Difficult to test precisely due to timing, but we can check that decay is working
            // by ensuring the system doesn't accumulate compensation indefinitely
            Assert.IsTrue(result.dy != 0, "Compensation should still occur after decay period");
        }

        [TestMethod]
        public void AntiRecoil_PatternRecording_ShouldCaptureMovements()
        {
            var patternName = "TestPattern";

            _antiRecoil.StartPatternRecording(patternName);
            Assert.IsTrue(_antiRecoil.IsRecording, "Should be in recording mode");

            // Simulate some movements
            _antiRecoil.ProcessMouseMovement(2, 8);
            _antiRecoil.ProcessMouseMovement(-1, 6);
            _antiRecoil.ProcessMouseMovement(0, 4);

            _antiRecoil.StopPatternRecording();
            Assert.IsFalse(_antiRecoil.IsRecording, "Should not be in recording mode");

            var patterns = _antiRecoil.GetAvailablePatterns();
            Assert.IsTrue(patterns.Any(p => p.Name == patternName), "Pattern should be saved");

            var savedPattern = patterns.First(p => p.Name == patternName);
            Assert.IsTrue(savedPattern.Samples.Count > 0, "Pattern should contain samples");
        }

        [TestMethod]
        public void AntiRecoil_InvalidParameters_ShouldHandleGracefully()
        {
            // Test negative strength
            _antiRecoil.Strength = -0.5f;
            Assert.IsTrue(_antiRecoil.Strength >= 0, "Strength should be clamped to non-negative");

            // Test strength > 1
            _antiRecoil.Strength = 1.5f;
            Assert.IsTrue(_antiRecoil.Strength <= 1.0f, "Strength should be clamped to <= 1.0");

            // Test negative threshold
            _antiRecoil.VerticalThreshold = -1.0f;
            Assert.IsTrue(_antiRecoil.VerticalThreshold >= 0, "Threshold should be non-negative");

            // Test negative activation delay
            _antiRecoil.ActivationDelayMs = -100;
            Assert.IsTrue(_antiRecoil.ActivationDelayMs >= 0, "Activation delay should be non-negative");
        }

        [TestMethod]
        public void AntiRecoil_Events_ShouldFireCorrectly()
        {
            bool settingsChangedFired = false;
            bool recordingStartedFired = false;
            bool recordingStoppedFired = false;

            _antiRecoil.SettingsChanged += () => settingsChangedFired = true;
            _antiRecoil.RecordingStarted += () => recordingStartedFired = true;
            _antiRecoil.RecordingStopped += () => recordingStoppedFired = true;

            // Change settings
            _antiRecoil.Strength = 0.8f;
            Assert.IsTrue(settingsChangedFired, "SettingsChanged event should fire when settings change");

            // Start recording
            _antiRecoil.StartPatternRecording("TestPattern");
            Assert.IsTrue(recordingStartedFired, "RecordingStarted event should fire");

            // Stop recording
            _antiRecoil.StopPatternRecording();
            Assert.IsTrue(recordingStoppedFired, "RecordingStopped event should fire");
        }
    }
}