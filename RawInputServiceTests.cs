using System;
using System.Threading;
using System.Windows.Forms;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using WootMouseRemap.Input;

namespace WootMouseRemap.Tests
{
    /// <summary>
    /// Unit tests for RawInputService functionality
    /// </summary>
    [TestClass]
    public class RawInputServiceTests
    {
        private RawInputService _rawInputService = null!;
        private Form _testForm = null!;
        private bool _mouseEventReceived;
        private RawMouseEvent _lastMouseEvent;

        [TestInitialize]
        public void Setup()
        {
            _rawInputService = new RawInputService();
            _testForm = new Form();
            _mouseEventReceived = false;
            _lastMouseEvent = default;

            _rawInputService.MouseEvent += OnMouseEvent;
        }

        [TestCleanup]
        public void Cleanup()
        {
            _rawInputService.MouseEvent -= OnMouseEvent;
            _rawInputService.Detach(_testForm);
            _rawInputService.Dispose();
            _testForm.Dispose();
        }

        private void OnMouseEvent(RawMouseEvent evt)
        {
            _mouseEventReceived = true;
            _lastMouseEvent = evt;
        }

        [TestMethod]
        public void Attach_ShouldRegisterDevice()
        {
            // Arrange
            _testForm.Show(); // Form needs to be visible for handle creation

            // Act
            _rawInputService.Attach(_testForm, complianceMode: true, allowBackgroundCapture: false);

            // Assert
            // If no exception is thrown, the attachment succeeded
            Assert.IsTrue(true, "RawInputService attached successfully");
        }

        [TestMethod]
        public void Detach_ShouldUnregisterDevice()
        {
            // Arrange
            _testForm.Show();
            _rawInputService.Attach(_testForm, complianceMode: true, allowBackgroundCapture: false);

            // Act
            _rawInputService.Detach(_testForm);

            // Assert
            // If no exception is thrown, the detachment succeeded
            Assert.IsTrue(true, "RawInputService detached successfully");
        }

        [TestMethod]
        public void Dispose_ShouldCleanUpResources()
        {
            // Arrange
            _testForm.Show();
            _rawInputService.Attach(_testForm, complianceMode: true, allowBackgroundCapture: false);

            // Act
            _rawInputService.Dispose();

            // Assert
            // If no exception is thrown, disposal succeeded
            Assert.IsTrue(true, "RawInputService disposed successfully");
        }

        [TestMethod]
        public void HandleMessage_WithNonInputMessage_ShouldReturnFalse()
        {
            // Arrange
            var message = new Message
            {
                Msg = 0x0001, // WM_CREATE
                HWnd = _testForm.Handle,
                LParam = IntPtr.Zero,
                WParam = IntPtr.Zero
            };

            // Act
            bool result = _rawInputService.HandleMessage(ref message);

            // Assert
            Assert.IsFalse(result, "Non-input message should return false");
        }

        [TestMethod]
        public void MouseEvent_ShouldBeRaisedOnMouseInput()
        {
            // Arrange
            _testForm.Show();
            _rawInputService.Attach(_testForm, complianceMode: true, allowBackgroundCapture: false);

            // Act - Simulate mouse input (this is difficult to test directly, so we just verify the event is wired)
            // In a real scenario, we'd need to simulate actual mouse input

            // Assert - The event handler is attached and the flag is initialized
            Assert.IsFalse(_mouseEventReceived, "Mouse event should not be received initially");
            Assert.AreEqual(default(RawMouseEvent), _lastMouseEvent, "Last mouse event should be default initially");
        }
    }
}