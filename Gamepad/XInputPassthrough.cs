using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Nefarius.ViGEm.Client.Targets.Xbox360;

namespace WootMouseRemap
{
    /// <summary>
    /// Thread-safe XInput passthrough with proper resource management and error handling.
    /// Polls a physical XInput controller and mirrors it to the ViGEm virtual pad.
    /// Automatically tracks connection changes and exposes ConnectionChanged event.
    /// </summary>
    public sealed class XInputPassthrough : IDisposable
    {
        private readonly Xbox360ControllerWrapper _pad;
        private readonly object _lockObject = new();
        private readonly object _disposeLock = new();
        
        private Thread? _thread;
        private CancellationTokenSource? _cts;
        private volatile bool _running;
        private volatile bool _connected;
        private volatile bool _disposed;
        private int _playerIndex;
        private bool _autoIndex = true;
        private DateTime _lastErrorLog = DateTime.MinValue;

        public event Action<bool>? ConnectionChanged; // true when a controller becomes available

        public XInputPassthrough(Xbox360ControllerWrapper pad)
        {
            _pad = pad ?? throw new ArgumentNullException(nameof(pad));
            _playerIndex = 0;
            Logger.Info("XInputPassthrough initialized");
        }

        public void SetPlayerIndex(int index) 
        { 
            lock (_lockObject)
            {
                _playerIndex = Math.Clamp(index, 0, 3);
                Logger.Info("XInputPassthrough player index set to {PlayerIndex}", _playerIndex);
            }
        }

        public void SetAutoIndex(bool enabled) 
        { 
            lock (_lockObject)
            {
                _autoIndex = enabled;
                Logger.Info("XInputPassthrough auto index set to {Enabled}", enabled);
            }
        }

        public bool IsRunning 
        { 
            get 
            { 
                lock (_lockObject)
                {
                    return _running && !_disposed;
                }
            } 
        }
        
        public bool IsConnected 
        { 
            get 
            { 
                lock (_lockObject)
                {
                    return _connected && !_disposed;
                }
            } 
        }

        public void Start()
        {
            if (_disposed)
            {
                Logger.Warn("Attempted to start disposed XInputPassthrough");
                return;
            }

            lock (_lockObject)
            {
                if (_running)
                {
                    Logger.Info("XInputPassthrough already running");
                    return;
                }

                try
                {
                    // Ensure virtual pad is connected before starting the loop
                    if (!_pad.IsConnected)
                    {
                        Logger.Warn("Virtual pad not connected - attempting to connect before starting passthrough");
                        try { _pad.Connect(); } catch (Exception ex) { Logger.Error("Error calling _pad.Connect()", ex); }

                        // Give the pad a short moment to establish
                        try { Thread.Sleep(200); } catch { }

                        if (!_pad.IsConnected)
                        {
                            Logger.Error("Virtual pad failed to connect - aborting XInputPassthrough.Start()");
                            return; // don't start loop if virtual pad isn't available
                        }

                        Logger.Info("Virtual pad connected successfully, proceeding to start passthrough");
                    }

                    _cts = new CancellationTokenSource();
                    _thread = new Thread(Loop) 
                    { 
                        IsBackground = true, 
                        Name = $"XInputPassthrough-P{_playerIndex}" 
                    };
                    _running = true;
                    _thread.Start(_cts.Token);
                    
                    Logger.Info("XInputPassthrough started for player {PlayerIndex}", _playerIndex);
                }
                catch (Exception ex)
                {
                    Logger.Error("Error starting XInputPassthrough", ex);
                    CleanupResources();
                    throw;
                }
            }
        }

        public void Stop()
        {
            lock (_lockObject)
            {
                if (!_running)
                {
                    return;
                }

                Logger.Info("Stopping XInputPassthrough");
                
                try
                {
                    CleanupResources();
                    Logger.Info("XInputPassthrough stopped");
                }
                catch (Exception ex)
                {
                    Logger.Error("Error stopping XInputPassthrough", ex);
                }
            }
        }

        private void CleanupResources()
        {
            _running = false;
            
            // Signal cancellation
            var cts = _cts;
            if (cts != null)
            {
                try
                {
                    cts.Cancel();
                }
                catch (Exception ex)
                {
                    Logger.Error("Error cancelling XInputPassthrough", ex);
                }
            }

            // Wait for thread to complete
            var thread = _thread;
            if (thread != null)
            {
                try
                {
                    if (!thread.Join(1000)) // Wait up to 1 second
                    {
                        Logger.Warn("XInputPassthrough thread did not stop gracefully");
                    }
                }
                catch (Exception ex)
                {
                    Logger.Error("Error joining XInputPassthrough thread", ex);
                }
            }

            // Dispose cancellation token
            if (cts != null)
            {
                try
                {
                    cts.Dispose();
                }
                catch (Exception ex)
                {
                    Logger.Error("Error disposing CancellationTokenSource", ex);
                }
            }

            _cts = null;
            _thread = null;
            
            // Reset connection state
            SetConnected(false);
        }

        private void SetConnected(bool state)
        {
            bool stateChanged = false;
            
            lock (_lockObject)
            {
                if (_connected != state)
                {
                    _connected = state;
                    stateChanged = true;
                }
            }
            
            if (stateChanged)
            {
                try
                {
                    ConnectionChanged?.Invoke(state);
                    Logger.Info("XInputPassthrough connection state changed: {State}", state);
                }
                catch (Exception ex)
                {
                    Logger.Error("Error in ConnectionChanged event handler", ex);
                }
            }
        }

        private void Loop(object? arg)
        {
            var token = (CancellationToken)arg!;
            int lastPacket = -1;
            var errorCount = 0;
            const int maxErrors = 10;

            Logger.Info("XInputPassthrough loop started");

            try
            {
                while (!token.IsCancellationRequested && !_disposed)
                {
                    try
                    {
                        int idx;
                        lock (_lockObject)
                        {
                            idx = _playerIndex;
                            if (_autoIndex)
                            {
                                int first = XInputHelper.FirstConnectedIndex();
                                if (first >= 0) idx = first;
                            }
                        }

                        if (!XInputHelper.TryGetState(idx, out var state))
                        {
                            SetConnected(false);

                            // Log controller detection issues (throttled)
                            var now = DateTime.UtcNow;
                            if (now - _lastErrorLog > TimeSpan.FromSeconds(5))
                            {
                                int firstConnected = XInputHelper.FirstConnectedIndex();
                                Logger.Warn("No controller state from slot {Idx}. FirstConnected: {FirstConnected}, AutoIndex: {AutoIndex}", idx, firstConnected, _autoIndex);

                                // Check all slots for debug
                                for (int i = 0; i < 4; i++)
                                {
                                    bool connected = XInputHelper.IsConnected(i);
                                    Logger.Info("XInput slot {Slot}: Connected={Connected}", i, connected);
                                }
                                _lastErrorLog = now;
                            }

                            Thread.Sleep(150);
                            continue;
                        }

                        SetConnected(true);
                        errorCount = 0; // Reset error count on successful read

                        if (unchecked((int)state.dwPacketNumber) != lastPacket)
                        {
                            lastPacket = unchecked((int)state.dwPacketNumber);
                            
                            if (!_disposed)
                            {
                                ProcessControllerState(state);
                            }
                        }

                        Thread.Sleep(3);
                    }
                    catch (Exception ex)
                    {
                        errorCount++;
                        
                        // Throttle error logging to prevent spam
                        var now = DateTime.UtcNow;
                        if (now - _lastErrorLog > TimeSpan.FromSeconds(5))
                        {
                            Logger.Error("Error in XInputPassthrough loop (count: {ErrorCount})", errorCount, ex);
                            _lastErrorLog = now;
                        }

                        if (errorCount >= maxErrors)
                        {
                            Logger.Error("Too many errors in XInputPassthrough loop ({ErrorCount}), stopping", errorCount);
                            break;
                        }

                        Thread.Sleep(100); // Back off on errors
                    }
                }
            }
            finally
            {
                Logger.Info("XInputPassthrough loop ended");
                SetConnected(false);
            }
        }

        private void ProcessControllerState(XInputHelper.XINPUT_STATE state)
        {
            try
            {
                var gp = state.Gamepad;

                // Enhanced logging for debugging - show activity when input is detected
                var now = DateTime.UtcNow;
                bool hasInput = gp.wButtons != 0 ||
                               Math.Abs(gp.sThumbLX) > 1000 || Math.Abs(gp.sThumbLY) > 1000 ||
                               Math.Abs(gp.sThumbRX) > 1000 || Math.Abs(gp.sThumbRY) > 1000 ||
                               gp.bLeftTrigger > 10 || gp.bRightTrigger > 10;

                if (hasInput)
                {
                    // Log immediately when there's actual input
                    Logger.Info("🎮 CONTROLLER INPUT - Buttons: 0x{Buttons:X4}, LStick: ({LStickX}, {LStickY}), RStick: ({RStickX}, {RStickY}), Triggers: L={LeftTrigger}, R={RightTrigger}", gp.wButtons, gp.sThumbLX, gp.sThumbLY, gp.sThumbRX, gp.sThumbRY, gp.bLeftTrigger, gp.bRightTrigger);
                }
                else if (now - _lastErrorLog > TimeSpan.FromSeconds(10)) // Log zero state less frequently
                {
                    Logger.Info("Controller connected but no input detected (all zeros)");
                    _lastErrorLog = now;
                }

                // Buttons
                SetBtn(gp.wButtons, XINPUT_GAMEPAD_A, Xbox360Button.A);
                SetBtn(gp.wButtons, XINPUT_GAMEPAD_B, Xbox360Button.B);
                SetBtn(gp.wButtons, XINPUT_GAMEPAD_X, Xbox360Button.X);
                SetBtn(gp.wButtons, XINPUT_GAMEPAD_Y, Xbox360Button.Y);
                SetBtn(gp.wButtons, XINPUT_GAMEPAD_DPAD_UP, Xbox360Button.Up);
                SetBtn(gp.wButtons, XINPUT_GAMEPAD_DPAD_DOWN, Xbox360Button.Down);
                SetBtn(gp.wButtons, XINPUT_GAMEPAD_DPAD_LEFT, Xbox360Button.Left);
                SetBtn(gp.wButtons, XINPUT_GAMEPAD_DPAD_RIGHT, Xbox360Button.Right);
                SetBtn(gp.wButtons, XINPUT_GAMEPAD_LEFT_SHOULDER, Xbox360Button.LeftShoulder);
                SetBtn(gp.wButtons, XINPUT_GAMEPAD_RIGHT_SHOULDER, Xbox360Button.RightShoulder);
                SetBtn(gp.wButtons, XINPUT_GAMEPAD_BACK, Xbox360Button.Back);
                SetBtn(gp.wButtons, XINPUT_GAMEPAD_START, Xbox360Button.Start);
                SetBtn(gp.wButtons, XINPUT_GAMEPAD_LEFT_THUMB, Xbox360Button.LeftThumb);
                SetBtn(gp.wButtons, XINPUT_GAMEPAD_RIGHT_THUMB, Xbox360Button.RightThumb);

                // Triggers
                _pad.SetTrigger(false, gp.bLeftTrigger);
                _pad.SetTrigger(true, gp.bRightTrigger);

                // Sticks
                _pad.SetLeftStick(gp.sThumbLX, gp.sThumbLY);
                _pad.SetRightStick(gp.sThumbRX, gp.sThumbRY);

                _pad.Submit();
            }
            catch (Exception ex)
            {
                // Throttle error logging
                var now = DateTime.UtcNow;
                if (now - _lastErrorLog > TimeSpan.FromSeconds(5))
                {
                    Logger.Error("Error processing controller state", ex);
                    _lastErrorLog = now;
                }
            }
        }

        private void SetBtn(ushort wButtons, ushort mask, Xbox360Button btn)
        {
            try
            {
                bool down = (wButtons & mask) != 0;
                _pad.SetButton(btn, down);
            }
            catch (Exception ex)
            {
                // Throttle error logging
                var now = DateTime.UtcNow;
                if (now - _lastErrorLog > TimeSpan.FromSeconds(5))
                {
                    Logger.Error("Error setting button {Btn}", btn, ex);
                    _lastErrorLog = now;
                }
            }
        }

        // XInput constants for button masks (local copy)
        private const ushort XINPUT_GAMEPAD_DPAD_UP        = 0x0001;
        private const ushort XINPUT_GAMEPAD_DPAD_DOWN      = 0x0002;
        private const ushort XINPUT_GAMEPAD_DPAD_LEFT      = 0x0004;
        private const ushort XINPUT_GAMEPAD_DPAD_RIGHT     = 0x0008;
        private const ushort XINPUT_GAMEPAD_START          = 0x0010;
        private const ushort XINPUT_GAMEPAD_BACK           = 0x0020;
        private const ushort XINPUT_GAMEPAD_LEFT_THUMB     = 0x0040;
        private const ushort XINPUT_GAMEPAD_RIGHT_THUMB    = 0x0080;
        private const ushort XINPUT_GAMEPAD_LEFT_SHOULDER  = 0x0100;
        private const ushort XINPUT_GAMEPAD_RIGHT_SHOULDER = 0x0200;
        private const ushort XINPUT_GAMEPAD_A              = 0x1000;
        private const ushort XINPUT_GAMEPAD_B              = 0x2000;
        private const ushort XINPUT_GAMEPAD_X              = 0x4000;
        private const ushort XINPUT_GAMEPAD_Y              = 0x8000;

        public void Dispose()
        {
            if (_disposed) return;
            
            lock (_disposeLock)
            {
                if (_disposed) return;
                _disposed = true;
            }

            Logger.Info("Disposing XInputPassthrough");
            
            try
            {
                Stop();
            }
            catch (Exception ex)
            {
                Logger.Error("Error during XInputPassthrough disposal", ex);
            }
            
            Logger.Info("XInputPassthrough disposed");
        }
    }
}
