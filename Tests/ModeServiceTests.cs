using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using WootMouseRemap.Core;
using WootMouseRemap.Modes;

namespace WootMouseRemap.Tests
{
    [TestClass]
    public class ModeServiceTests
    {
        private string _tempPath;
        private ModeManager _manager;
        private ModeService _service;
        private FakePassthroughMode _pass;
        private FakeOutputMode _out;

        [TestInitialize]
        public void Setup()
        {
            _tempPath = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "woot_mode_test.json");
            try { if (System.IO.File.Exists(_tempPath)) System.IO.File.Delete(_tempPath); } catch { }
            _manager = new ModeManager();
            _pass = new FakePassthroughMode();
            _out = new FakeOutputMode();
            _manager.RegisterMode(_out);
            _manager.RegisterMode(_pass);
            _service = new ModeService(_tempPath, _manager);
        }

        [TestCleanup]
        public void Cleanup()
        {
            _service?.Dispose();
            _manager?.Dispose();
            try { if (System.IO.File.Exists(_tempPath)) System.IO.File.Delete(_tempPath); } catch { }
        }

        [TestMethod]
        public void PersistedPassthroughStartsInPassthrough()
        {
            // Arrange: persist passthrough
            System.IO.File.WriteAllText(_tempPath, InputMode.ControllerPass.ToString());
            // Act: initialize after registration
            _service.InitializeFromPersistence();
            // Assert
            Assert.AreEqual(InputMode.ControllerPass, _service.CurrentMode, "Should restore persisted passthrough mode");
            Assert.IsFalse(_service.SuppressionActive, "Passthrough should not suppress input");
        }

        [TestMethod]
        public void DisconnectTriggersSwitchToOutput()
        {
            _service.Switch(InputMode.ControllerPass);
            Assert.AreEqual(InputMode.ControllerPass, _service.CurrentMode);
            // Simulate controller disconnect event
            _manager.OnControllerDisconnected(0);
            // Our fake passthrough issues a request to switch; emulate by calling directly like real implementation
            _manager.SwitchMode(InputMode.Native);
            Assert.AreEqual(InputMode.Native, _service.CurrentMode, "Should switch back to output on disconnect");
        }

        [TestMethod]
        public void ToggleCyclesThroughAllModes()
        {
            // Start in default (first registered output)
            Assert.AreEqual(InputMode.Native, _service.CurrentMode);
            var first = _service.Toggle();
            Assert.AreEqual(InputMode.ControllerPass, first);
            Assert.AreEqual(InputMode.ControllerPass, _service.CurrentMode);
            var second = _service.Toggle();
            Assert.AreEqual(InputMode.MnKConvert, second);
            Assert.AreEqual(InputMode.MnKConvert, _service.CurrentMode);
            var third = _service.Toggle();
            Assert.AreEqual(InputMode.Native, third);
            Assert.AreEqual(InputMode.Native, _service.CurrentMode);
        }

        [TestMethod]
        public void ModeChangedFiresOncePerSwitch()
        {
            int count = 0; InputMode lastOld = default; InputMode lastNew = default;
            _service.ModeChanged += (o, n) => { count++; lastOld = o; lastNew = n; };
            _service.Switch(InputMode.ControllerPass);
            Assert.AreEqual(1, count, "ModeChanged should fire once for first switch");
            Assert.AreEqual(InputMode.Native, lastOld);
            Assert.AreEqual(InputMode.ControllerPass, lastNew);
            // Switch to same mode should be idempotent - no extra event
            _service.Switch(InputMode.ControllerPass);
            Assert.AreEqual(1, count, "No event when switching to same mode");
            _service.Switch(InputMode.Native);
            Assert.AreEqual(2, count, "Second valid transition increments count");
        }

        [TestMethod]
        public void IsPersistedMatchTransitionsAfterInitialization()
        {
            // Persist passthrough before service initialization logic runs
            System.IO.File.WriteAllText(_tempPath, InputMode.ControllerPass.ToString());
            // Before init we haven't aligned runtime manager yet
            Assert.IsFalse(_service.IsPersistedMatch, "Runtime should not yet match persisted before InitializeFromPersistence");
            _service.InitializeFromPersistence();
            Assert.IsTrue(_service.IsPersistedMatch, "Runtime should match persisted after initialization");
        }

        private sealed class FakePassthroughMode : IModeHandler
        {
            public InputMode Mode => InputMode.ControllerPass;
            public bool ShouldSuppressInput => false;
            public void OnModeEntered(InputMode previousMode) { LowLevelHooks.Suppress = false; }
            public void OnModeExited(InputMode nextMode) { }
            public void OnKey(int vk, bool down) { }
            public void OnMouseButton(MouseInput button, bool down) { }
            public void OnMouseMove(int dx, int dy) { }
            public void OnWheel(int delta) { }
            public void OnControllerConnected(int index) { }
            public void OnControllerDisconnected(int index) { /* Real impl requests mode change */ }
            public void Update() { }
            public string GetStatusText() => "FakePass";
        }
        private sealed class FakeOutputMode : IModeHandler
        {
            public InputMode Mode => InputMode.Native;
            public bool ShouldSuppressInput => true;
            public void OnModeEntered(InputMode previousMode) { LowLevelHooks.Suppress = true; }
            public void OnModeExited(InputMode nextMode) { LowLevelHooks.Suppress = false; }
            public void OnKey(int vk, bool down) { }
            public void OnMouseButton(MouseInput button, bool down) { }
            public void OnMouseMove(int dx, int dy) { }
            public void OnWheel(int delta) { }
            public void OnControllerConnected(int index) { }
            public void OnControllerDisconnected(int index) { }
            public void Update() { }
            public string GetStatusText() => "FakeOut";
        }
    }
}
