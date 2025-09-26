using System;
using WootMouseRemap.Diagnostics;

namespace WootMouseRemap.Modes
{
    /// <summary>
    /// Native mode - pass-through mode that does not suppress input
    /// and does not map to virtual controller. Allows normal mouse and keyboard operation.
    /// </summary>
    public class NativeMode : IModeHandler
    {
        public InputMode Mode => InputMode.Native;
        public bool ShouldSuppressInput => false; // Never suppress input in Native mode

        public void OnModeEntered(InputMode previousMode)
        {
            Logger.Info("Entering Native mode (from {PreviousMode})", previousMode);
            // No input suppression needed - allow normal mouse/keyboard operation
        }

        public void OnModeExited(InputMode nextMode)
        {
            Logger.Info("Exiting Native mode (to {NextMode})", nextMode);
            // No cleanup needed for pass-through mode
        }

        public void OnKey(int vk, bool down)
        {
            // Pass-through mode - keyboard input flows normally
            // No controller mapping or processing needed
        }

        public void OnMouseButton(MouseInput button, bool down)
        {
            // Pass-through mode - mouse button input flows normally
            // No controller mapping or processing needed
        }

        public void OnMouseMove(int dx, int dy)
        {
            // Pass-through mode - mouse movement flows normally
            // No controller mapping or processing needed
        }

        public void OnWheel(int delta)
        {
            // Pass-through mode - mouse wheel input flows normally
            // No controller mapping or processing needed
        }

        public void OnControllerConnected(int index)
        {
            // Pass-through mode - controller connections don't affect mouse/keyboard operation
            Logger.Info("Controller connected at index {Index} in Native mode", index);
        }

        public void OnControllerDisconnected(int index)
        {
            // Pass-through mode - controller disconnections don't affect mouse/keyboard operation
            Logger.Info("Controller disconnected at index {Index} in Native mode", index);
        }

        public void Update()
        {
            // No periodic updates needed for pass-through mode
        }

        public string GetStatusText()
        {
            return "Mode: Native (Pass-through)";
        }
    }
}