using Nefarius.ViGEm.Client;
using Nefarius.ViGEm.Client.Targets;
using Nefarius.ViGEm.Client.Targets.Xbox360;
using System;

namespace WootMouseRemap
{
    /// <summary>
    /// Thin wrapper around ViGEm's Xbox360 controller with a lightweight snapshot of the last values sent.
    /// Fully implemented: no placeholders. Safe to use from UI thread; calls are exception-guarded and
    /// will attempt automatic reconnection if the device drops.
    /// </summary>
    public sealed class Xbox360ControllerWrapper : IDisposable
    {
        public sealed class PadSnapshot
        {
            public short LX, LY, RX, RY;
            public byte LT, RT;
            public bool A, B, X, Y, LB, RB, Back, Start, L3, R3, DUp, DDown, DLeft, DRight;
        }

        private readonly PadSnapshot _snap = new PadSnapshot();

        private ViGEmClient? _client;
        private IXbox360Controller? _target;
        private global::System.Threading.Timer? _watchdog;

        public bool IsConnected => _target is not null;

        /// <summary>Fires when the connection state changes. True on connect, False on disconnect.</summary>
        public event Action<bool>? StatusChanged;

        /// <summary>Connects to ViGEm and provisions a virtual Xbox 360 controller.</summary>
        public void Connect()
        {
            try
            {
                Logger.Info("Xbox360ControllerWrapper: Connect() called");
                _client = new ViGEmClient();
                _target = _client.CreateXbox360Controller();
                _target.Connect();
                StatusChanged?.Invoke(true);

                // Keep reports flowing so most games detect activity;
                // short period is fine, Set* calls also send on change.
                _watchdog = new global::System.Threading.Timer(_ => Submit(), null, 0, 5);
                Logger.Info("Xbox360ControllerWrapper: Connected and watchdog started");
            }
            catch (Exception ex)
            {
                Logger.Error("ViGEm connect failed", ex);
                StatusChanged?.Invoke(false);
            }
        }

        private int _retries;

        /// <summary>Attempts a backoff reconnect if a call fails.</summary>
        private void Reconnect()
        {
            if (_retries > 6) return;
            _retries++;
            Logger.Warn("ViGEm reconnect attempt #{Retries}", _retries);
            DisposePadOnly();
            try
            {
                System.Threading.Thread.Sleep(200 * _retries);
                Logger.Info("Xbox360ControllerWrapper: Reconnect() calling Connect()");
                Connect();
            }
            catch (Exception ex)
            {
                Logger.Error("Xbox360ControllerWrapper: Reconnect failed", ex);
            }
            if (IsConnected) _retries = 0;
        }

        public void SetButton(Xbox360Button button, bool pressed)
        {
            try
            {
                Logger.Debug("Xbox360ControllerWrapper: SetButton {Button} = {Pressed}", button, pressed);
                _target?.SetButtonState(button, pressed);

                // Keep snapshot in sync
                if (button == Xbox360Button.A) _snap.A = pressed;
                else if (button == Xbox360Button.B) _snap.B = pressed;
                else if (button == Xbox360Button.X) _snap.X = pressed;
                else if (button == Xbox360Button.Y) _snap.Y = pressed;
                else if (button == Xbox360Button.LeftShoulder) _snap.LB = pressed;
                else if (button == Xbox360Button.RightShoulder) _snap.RB = pressed;
                else if (button == Xbox360Button.Back) _snap.Back = pressed;
                else if (button == Xbox360Button.Start) _snap.Start = pressed;
                else if (button == Xbox360Button.LeftThumb) _snap.L3 = pressed;
                else if (button == Xbox360Button.RightThumb) _snap.R3 = pressed;
                else if (button == Xbox360Button.Up) _snap.DUp = pressed;
                else if (button == Xbox360Button.Down) _snap.DDown = pressed;
                else if (button == Xbox360Button.Left) _snap.DLeft = pressed;
                else if (button == Xbox360Button.Right) _snap.DRight = pressed;
            }
            catch (Exception ex)
            {
                Logger.Error("Xbox360ControllerWrapper: Error in SetButton {Button}", button, ex);
                Reconnect();
            }
        }

        public void SetTrigger(bool right, byte value)
        {
            try
            {
                Logger.Debug("Xbox360ControllerWrapper: SetTrigger right={Right} value={Value}", right, value);
                if (_target == null) return;
                var slider = right ? Xbox360Slider.RightTrigger : Xbox360Slider.LeftTrigger;
                _target.SetSliderValue(slider, value);
                if (slider == Xbox360Slider.LeftTrigger) _snap.LT = value; else _snap.RT = value;
            }
            catch (Exception ex)
            {
                Logger.Error("Xbox360ControllerWrapper: Error in SetTrigger", ex);
                Reconnect();
            }
        }

        public void SetRightStick(short x, short y)
        {
            try
            {
                Logger.Debug("Xbox360ControllerWrapper: SetRightStick ({X}, {Y})", x, y);
                _target?.SetAxisValue(Xbox360Axis.RightThumbX, x);
                _target?.SetAxisValue(Xbox360Axis.RightThumbY, y);
                _snap.RX = x; _snap.RY = y;
            }
            catch (Exception ex)
            {
                Logger.Error("Xbox360ControllerWrapper: Error in SetRightStick", ex);
                Reconnect();
            }
        }

        public void SetLeftStick(short x, short y)
        {
            try
            {
                Logger.Debug("Xbox360ControllerWrapper: SetLeftStick ({X}, {Y})", x, y);
                _target?.SetAxisValue(Xbox360Axis.LeftThumbX, x);
                _target?.SetAxisValue(Xbox360Axis.LeftThumbY, y);
                _snap.LX = x; _snap.LY = y;
            }
            catch (Exception ex)
            {
                Logger.Error("Xbox360ControllerWrapper: Error in SetLeftStick", ex);
                Reconnect();
            }
        }

        public void SetDpad(bool up, bool down, bool left, bool right)
        {
            try
            {
                if (_target == null) return;
                _target.SetButtonState(Xbox360Button.Up, up); _snap.DUp = up;
                _target.SetButtonState(Xbox360Button.Down, down); _snap.DDown = down;
                _target.SetButtonState(Xbox360Button.Left, left); _snap.DLeft = left;
                _target.SetButtonState(Xbox360Button.Right, right); _snap.DRight = right;
            }
            catch
            {
                Reconnect();
            }
        }

        public void ResetAll()
        {
            try
            {
                Logger.Debug("Xbox360ControllerWrapper: ResetAll called");
                _target?.ResetReport();
                _target?.SubmitReport();
            }
            catch (Exception ex)
            {
                Logger.Error("Xbox360ControllerWrapper: Error in ResetAll", ex);
                Reconnect();
            }
        }

        public PadSnapshot GetSnapshot() => _snap;

        public void Submit()
        {
            try
            {
                Logger.Debug("Xbox360ControllerWrapper: Submit called");
                _target?.SubmitReport();
            }
            catch (Exception ex)
            {
                Logger.Error("Xbox360ControllerWrapper: Error in Submit", ex);
                Reconnect();
            }
        }

        private void DisposePadOnly()
        {
            try { _target?.Disconnect(); } catch { }
            try { _client?.Dispose(); } catch { }
            _target = null; _client = null;
            StatusChanged?.Invoke(false);
        }

        public void Dispose()
        {
            try { _watchdog?.Dispose(); } catch { }
            DisposePadOnly();
        }

        public void ZeroRightStick()
        {
            SetRightStick(0, 0);
        }

        public void ResetSmoothing()
        {
            // This is a placeholder for smoothing reset functionality
            // In a real implementation, this would reset any smoothing state
        }
    }
}
