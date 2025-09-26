using System;
using System.Runtime.InteropServices;

namespace WootMouseRemap
{
    /// <summary>
    /// Thin XInput utility that supports 1.4 → 1.3 → 9.1.0 with safe fallbacks.
    /// Provides connection checks and state retrieval without lambdas (no CS1628 issues).
    /// </summary>
    public static class XInputHelper
    {
        [StructLayout(LayoutKind.Sequential)]
        public struct XINPUT_GAMEPAD
        {
            public ushort wButtons;
            public byte bLeftTrigger;
            public byte bRightTrigger;
            public short sThumbLX;
            public short sThumbLY;
            public short sThumbRX;
            public short sThumbRY;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct XINPUT_STATE
        {
            public uint dwPacketNumber;
            public XINPUT_GAMEPAD Gamepad;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct XINPUT_CAPABILITIES
        {
            public byte Type;
            public byte SubType;
            public ushort Flags;
            public XINPUT_GAMEPAD Gamepad;
        }

        /// <summary>Returns bitmask of connected XInput slots (bit i set => slot i connected).</summary>
        public static int GetConnectedMask()
        {
            int mask = 0;
            for (int i = 0; i < 4; i++)
            {
                if (IsConnected(i)) mask |= (1 << i);
            }
            return mask;
        }

        /// <summary>Returns true if any XInput slot [0..3] is connected.</summary>
        public static bool AnyConnected()
        {
            return FirstConnectedIndex() >= 0;
        }

        /// <summary>Best-effort GetState across XInput DLLs. Returns Win32 code (0==OK).</summary>
        public static int GetState(int index, out XINPUT_STATE state)
        {
            // 1.4
            try { return X14.GetState((uint)index, out state); }
            catch (DllNotFoundException) { }
            catch (EntryPointNotFoundException) { }
            // 1.3
            try { return X13.GetState((uint)index, out state); }
            catch (DllNotFoundException) { }
            catch (EntryPointNotFoundException) { }
            // 9.1.0
            try { return X910.GetState((uint)index, out state); }
            catch (DllNotFoundException) { }
            catch (EntryPointNotFoundException) { }

            state = default;
            return -1;
        }

        /// <summary>Boolean wrapper around GetState.</summary>
        public static bool TryGetState(int index, out XINPUT_STATE state)
        {
            int r = GetState(index, out state);
            return r == 0;
        }

        /// <summary>True if the slot has a device. Uses GetCapabilities where available, falls back to GetState.</summary>
        public static bool IsConnected(int index)
        {
            // Capabilities is cheaper and avoids packet churn
            // 1.4
            try { if (X14.GetCapabilities((uint)index, 0, out var _caps) == 0) return true; }
            catch (DllNotFoundException) { }
            catch (EntryPointNotFoundException) { }
            // 1.3
            try { if (X13.GetCapabilities((uint)index, 0, out var _caps13) == 0) return true; }
            catch (DllNotFoundException) { }
            catch (EntryPointNotFoundException) { }
            // 9.1.0
            try { if (X910.GetCapabilities((uint)index, 0, out var _caps910) == 0) return true; }
            catch (DllNotFoundException) { }
            catch (EntryPointNotFoundException) { }

            // Fallback: state call
            return GetState(index, out var _state) == 0;
        }

        /// <summary>Returns first connected index or -1.</summary>
        public static int FirstConnectedIndex()
        {
            for (int i = 0; i < 4; i++)
                if (IsConnected(i)) return i;
            return -1;
        }

        /// <summary>
        /// Returns the first connected physical controller index, attempting to exclude virtual controllers.
        /// This is a best-effort attempt and may not be 100% accurate in all scenarios.
        /// </summary>
        public static int FirstPhysicalControllerIndex()
        {
            for (int i = 0; i < 4; i++)
            {
                if (IsConnected(i) && IsLikelyPhysicalController(i))
                    return i;
            }
            return -1;
        }

        /// <summary>
        /// Attempts to determine if a controller at the given index is likely a physical controller.
        /// This uses heuristics and may not be 100% accurate.
        /// </summary>
        public static bool IsLikelyPhysicalController(int index)
        {
            try
            {
                // Try to get capabilities - virtual controllers often have different capability patterns
                // XInput 1.4 first
                try 
                { 
                    if (X14.GetCapabilities((uint)index, 0, out var caps14) == 0)
                    {
                        return caps14.SubType != 1 || caps14.Type != 1; // heuristic
                    }
                }
                catch (DllNotFoundException) { }
                catch (EntryPointNotFoundException) { }

                // XInput 1.3 fallback
                try 
                { 
                    if (X13.GetCapabilities((uint)index, 0, out var caps13) == 0)
                    {
                        return caps13.SubType != 1 || caps13.Type != 1; // heuristic
                    }
                }
                catch (DllNotFoundException) { }
                catch (EntryPointNotFoundException) { }

                // If we can't get capabilities, assume physical
                return true;
            }
            catch
            {
                return true; // be permissive
            }
        }

        /// <summary>
        /// Gets a count of likely physical controllers (excluding virtual ones where possible)
        /// </summary>
        public static int GetPhysicalControllerCount()
        {
            int count = 0;
            for (int i = 0; i < 4; i++)
            {
                if (IsConnected(i) && IsLikelyPhysicalController(i))
                    count++;
            }
            return count;
        }

        private static class X14
        {
            [DllImport("xinput1_4.dll", EntryPoint = "XInputGetState")]
            public static extern int GetState(uint dwUserIndex, out XINPUT_STATE pState);

            [DllImport("xinput1_4.dll", EntryPoint = "XInputGetCapabilities")]
            public static extern int GetCapabilities(uint dwUserIndex, uint dwFlags, out XINPUT_CAPABILITIES caps);
        }

        private static class X13
        {
            [DllImport("xinput1_3.dll", EntryPoint = "XInputGetState")]
            public static extern int GetState(uint dwUserIndex, out XINPUT_STATE pState);

            [DllImport("xinput1_3.dll", EntryPoint = "XInputGetCapabilities")]
            public static extern int GetCapabilities(uint dwUserIndex, uint dwFlags, out XINPUT_CAPABILITIES caps);
        }

        private static class X910
        {
            [DllImport("xinput9_1_0.dll", EntryPoint = "XInputGetState")]
            public static extern int GetState(uint dwUserIndex, out XINPUT_STATE pState);

            [DllImport("xinput9_1_0.dll", EntryPoint = "XInputGetCapabilities")]
            public static extern int GetCapabilities(uint dwUserIndex, uint dwFlags, out XINPUT_CAPABILITIES caps);
        }
    }
}
