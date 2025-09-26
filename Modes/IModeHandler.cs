using System;

namespace WootMouseRemap.Modes
{
    public interface IModeHandler
    {
        InputMode Mode { get; }

        void OnModeEntered(InputMode previousMode);
        void OnModeExited(InputMode nextMode);

        void OnKey(int vk, bool down);
        void OnMouseButton(MouseInput button, bool down);
        void OnMouseMove(int dx, int dy);
        void OnWheel(int delta);

        void OnControllerConnected(int index);
        void OnControllerDisconnected(int index);

        void Update();

        bool ShouldSuppressInput { get; }
        string GetStatusText();
    }
}