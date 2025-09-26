using System;
using System.Threading;

namespace WootMouseRemap
{
    /// <summary>
    /// Detects presence of an external (physical) XInput controller while ignoring our own virtual ViGEm pad.
    /// Strategy: caller injects reference to virtual wrapper. We consider a slot physical if:
    ///  1) XInput reports it connected AND
    ///  2) Its state does not exactly mirror the current virtual snapshot (basic heuristic)
    /// We stop trying to be too clever with capability heuristics which can misclassify.
    /// </summary>
    public sealed class ControllerDetector : IDisposable
    {
        private readonly object _sync = new();
        private System.Threading.Timer? _timer;
        private readonly int _periodMs;
        private bool _autoIndex;
        private int _index;
        private bool _connected;
        private volatile bool _disposed;
        private DateTime _lastErrorLog = DateTime.MinValue;
        private readonly Xbox360ControllerWrapper? _virtual;

        // Track recent right-stick activity timestamps per slot
        private readonly DateTime[] _lastRightStickActivity = new DateTime[4];
        private int _recentActivityWindowMs = 2000; // consider controllers active if right-stick moved in last 2s

        public event Action<bool,int>? ConnectionChanged; // (connected, index)

        public ControllerDetector(int startIndex = 0, bool autoIndex = true, int periodMs = 500, Xbox360ControllerWrapper? virtualController = null)
        {
            _index = Math.Clamp(startIndex, 0, 3);
            _autoIndex = autoIndex;
            _periodMs = Math.Max(100, periodMs);
            _virtual = virtualController;

            try
            {
                _timer = new System.Threading.Timer(Poll, null, 0, _periodMs);
                Logger.Info("ControllerDetector init (startIndex={StartIndex}, auto={Auto}, period={Period})", _index, _autoIndex, _periodMs);
            }
            catch (Exception ex)
            {
                Logger.Error("ControllerDetector ctor", ex);
                throw;
            }
        }

        public bool AutoIndex { get { lock (_sync) return _autoIndex; } set { lock (_sync) _autoIndex = value; } }
        public int Index { get { lock (_sync) return _index; } }
        public bool Connected { get { lock (_sync) return _connected; } }

        public void SetIndex(int index)
        {
            if (_disposed) return;
            index = Math.Clamp(index, 0, 3);
            lock (_sync)
            {
                _index = index;
                _autoIndex = false;
            }
            FireImmediate(index);
        }

        private void FireImmediate(int idx)
        {
            bool isConn = IsPhysical(idx);
            try { ConnectionChanged?.Invoke(isConn, idx); } catch (Exception ex) { Logger.Error("ControllerDetector.FireImmediate", ex); }
        }

        private bool IsPhysical(int slot)
        {
            if (slot < 0 || slot > 3) return false;
            if (!XInputHelper.IsConnected(slot)) return false;

            if (_virtual == null || !_virtual.IsConnected)
                return true; // nothing to exclude

            // Compare snapshot vs current state; if identical on all primary axes + triggers -> assume virtual
            try
            {
                if (XInputHelper.TryGetState(slot, out var st))
                {
                    var snap = _virtual.GetSnapshot();
                    bool same = st.Gamepad.sThumbLX == snap.LX && st.Gamepad.sThumbLY == snap.LY &&
                                st.Gamepad.sThumbRX == snap.RX && st.Gamepad.sThumbRY == snap.RY &&
                                st.Gamepad.bLeftTrigger == snap.LT && st.Gamepad.bRightTrigger == snap.RT;
                    if (same)
                    {
                        // To reduce false positives, require the snapshot to be neutral too
                        bool neutral = snap.LX == 0 && snap.LY == 0 && snap.RX == 0 && snap.RY == 0 && snap.LT == 0 && snap.RT == 0;
                        if (neutral) return false; // likely our virtual at rest
                    }
                }
            }
            catch (Exception ex)
            {
                var now = DateTime.UtcNow;
                if (now - _lastErrorLog > TimeSpan.FromSeconds(5))
                {
                    Logger.Warn("IsPhysical state compare failed slot {Slot}: {Message}", slot, ex.Message);
                    _lastErrorLog = now;
                }
            }
            return true;
        }

        private int FirstPhysical()
        {
            for (int i = 0; i < 4; i++)
                if (IsPhysical(i)) return i;
            return -1;
        }

        private void Poll(object? _)
        {
            if (_disposed) return;
            try
            {
                int slot; bool auto;
                lock (_sync) { slot = _index; auto = _autoIndex; }

                // Refresh activity timestamps for all connected slots
                for (int i = 0; i < 4; i++)
                {
                    try
                    {
                        if (!XInputHelper.IsConnected(i)) continue;
                        if (!IsPhysical(i)) continue;
                        if (XInputHelper.TryGetState(i, out var st))
                        {
                            // Record recent right-stick activity
                            if (st.Gamepad.sThumbRX != 0 || st.Gamepad.sThumbRY != 0)
                            {
                                _lastRightStickActivity[i] = DateTime.UtcNow;
                            }
                        }
                    }
                    catch { /* ignore per-slot read errors */ }
                }

                bool phys = IsPhysical(slot);
                int newSlot = slot;
                if (!phys && auto)
                {
                    // Prefer the most recently active physical controller within window
                    DateTime cutoff = DateTime.UtcNow - TimeSpan.FromMilliseconds(_recentActivityWindowMs);
                    int best = -1; DateTime bestTime = DateTime.MinValue;
                    for (int i = 0; i < 4; i++)
                    {
                        if (!XInputHelper.IsConnected(i)) continue;
                        if (!IsPhysical(i)) continue;
                        var t = _lastRightStickActivity[i];
                        if (t > cutoff && t > bestTime)
                        {
                            best = i; bestTime = t;
                        }
                    }

                    if (best >= 0)
                    {
                        phys = true; newSlot = best;
                      }
                    else
                    {
                        int first = FirstPhysical();
                        if (first >= 0) { phys = true; newSlot = first; }
                    }
                }

                bool fire = false; bool conn = phys; int fireSlot = newSlot;
                lock (_sync)
                {
                    if (conn != _connected || newSlot != _index)
                    {
                        _connected = conn;
                        _index = newSlot;
                        fire = true;
                    }
                }
                if (fire)
                {
                    try
                    {
                        Logger.Info("ControllerDetector change: connected={Connected}, slot={Slot}", conn, fireSlot);
                        ConnectionChanged?.Invoke(conn, fireSlot);
                    }
                    catch (Exception ex)
                    {
                        var now = DateTime.UtcNow;
                        if (now - _lastErrorLog > TimeSpan.FromSeconds(5))
                        {
                            Logger.Error("ControllerDetector ConnectionChanged handler", ex);
                            _lastErrorLog = now;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                var now2 = DateTime.UtcNow;
                if (now2 - _lastErrorLog > TimeSpan.FromSeconds(5))
                {
                    Logger.Error("ControllerDetector Poll", ex);
                    _lastErrorLog = now2;
                }
            }
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            try { _timer?.Dispose(); } catch { }
            _timer = null;
        }

        public static bool IsXInputControllerConnected()
        {
            try { return XInputHelper.FirstConnectedIndex() >= 0; }
            catch { return false; }
        }

        /// <summary>
        /// Returns the index of the most-recently active physical controller (right-stick movement)
        /// within the detector's activity window, or -1 if none.
        /// </summary>
        public int GetMostRecentActiveIndex()
        {
            try
            {
                DateTime cutoff = DateTime.UtcNow - TimeSpan.FromMilliseconds(_recentActivityWindowMs);
                int best = -1; DateTime bestTime = DateTime.MinValue;
                lock (_sync)
                {
                    for (int i = 0; i < 4; i++)
                    {
                        var t = _lastRightStickActivity[i];
                        if (t > cutoff && t > bestTime)
                        {
                            best = i; bestTime = t;
                        }
                    }
                }
                return best;
            }
            catch { return -1; }
        }
    }
}
