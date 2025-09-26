using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Text;
using System.Linq;

namespace WootMouseRemap.Interop
{
    // Simple device information record
    public sealed class DeviceInstance
    {
        public string InstanceId { get; init; } = string.Empty;
        public string Description { get; init; } = string.Empty;
        public Guid ClassGuid { get; init; }
    }

    public static class DeviceHider
    {
        // Public helpers
        public static bool IsAdministrator()
        {
            try
            {
                using var id = WindowsIdentity.GetCurrent();
                var p = new WindowsPrincipal(id);
                return p.IsInRole(WindowsBuiltInRole.Administrator);
            }
            catch { return false; }
        }

        public static IEnumerable<DeviceInstance> EnumerateDevices()
        {
            var list = new List<DeviceInstance>();
            var devInfo = SetupDiGetClassDevs(IntPtr.Zero, null, IntPtr.Zero, DIGCF_ALLCLASSES | DIGCF_PRESENT);
            if (devInfo == INVALID_HANDLE_VALUE) yield break;

            try
            {
                SP_DEVINFO_DATA data = default;
                data.cbSize = Marshal.SizeOf<SP_DEVINFO_DATA>();

                for (uint i = 0; ; i++)
                {
                    if (!SetupDiEnumDeviceInfo(devInfo, i, ref data)) break;

                    // Instance ID
                    var sb = new StringBuilder(512);
                    if (SetupDiGetDeviceInstanceId(devInfo, ref data, sb, sb.Capacity, out _))
                    {
                        string id = sb.ToString();

                        // Try to get friendly name or device description
                        string desc = GetDeviceProperty(devInfo, ref data, SPDRP_FRIENDLYNAME) ??
                                      GetDeviceProperty(devInfo, ref data, SPDRP_DEVICEDESC) ?? string.Empty;

                        list.Add(new DeviceInstance { InstanceId = id, Description = desc, ClassGuid = data.ClassGuid });
                    }
                }
            }
            finally
            {
                SetupDiDestroyDeviceInfoList(devInfo);
            }

            foreach (var d in list) yield return d;
        }

        public static bool SetDeviceEnabled(string instanceId, bool enable)
        {
            if (!IsAdministrator()) throw new UnauthorizedAccessException("Administrator privileges are required to enable/disable devices.");
            if (string.IsNullOrEmpty(instanceId)) throw new ArgumentNullException(nameof(instanceId));

            var devInfo = SetupDiGetClassDevs(IntPtr.Zero, null, IntPtr.Zero, DIGCF_ALLCLASSES | DIGCF_PRESENT);
            if (devInfo == INVALID_HANDLE_VALUE) throw new Win32Exception(Marshal.GetLastWin32Error());

            try
            {
                SP_DEVINFO_DATA data = default;
                data.cbSize = Marshal.SizeOf<SP_DEVINFO_DATA>();

                for (uint i = 0; ; i++)
                {
                    if (!SetupDiEnumDeviceInfo(devInfo, i, ref data)) break;

                    var sb = new StringBuilder(512);
                    if (!SetupDiGetDeviceInstanceId(devInfo, ref data, sb, sb.Capacity, out _)) continue;
                    if (!string.Equals(sb.ToString(), instanceId, StringComparison.OrdinalIgnoreCase)) continue;

                    // Prepare property change params
                    SP_CLASSINSTALL_HEADER header = new SP_CLASSINSTALL_HEADER();
                    header.cbSize = Marshal.SizeOf<SP_CLASSINSTALL_HEADER>();
                    header.InstallFunction = DIF_PROPERTYCHANGE;

                    SP_PROPCHANGE_PARAMS pcp = new SP_PROPCHANGE_PARAMS();
                    pcp.ClassInstallHeader = header;
                    pcp.StateChange = enable ? DICS_ENABLE : DICS_DISABLE;
                    pcp.Scope = DICS_FLAG_GLOBAL;
                    pcp.HwProfile = 0;

                    if (!SetupDiSetClassInstallParams(devInfo, ref data, ref pcp, Marshal.SizeOf<SP_PROPCHANGE_PARAMS>()))
                    {
                        throw new Win32Exception(Marshal.GetLastWin32Error());
                    }

                    if (!SetupDiCallClassInstaller(DIF_PROPERTYCHANGE, devInfo, ref data))
                    {
                        throw new Win32Exception(Marshal.GetLastWin32Error());
                    }

                    return true;
                }
            }
            finally
            {
                SetupDiDestroyDeviceInfoList(devInfo);
            }

            return false;
        }

        // Convenience methods
        public static bool HideDevice(string instanceId) => SetDeviceEnabled(instanceId, false);
        public static bool UnhideDevice(string instanceId) => SetDeviceEnabled(instanceId, true);

        private static string? GetDeviceProperty(IntPtr devInfo, ref SP_DEVINFO_DATA data, uint prop)
        {
            var sb = new StringBuilder(512);
            if (SetupDiGetDeviceRegistryProperty(devInfo, ref data, prop, out _, sb, sb.Capacity, out _))
                return sb.ToString();
            return null;
        }

        /// <summary>
        /// Attempt to find the most likely physical controller device instance by searching
        /// for common controller keywords and the presence of VID/PID in the instance id.
        /// This is a heuristic used for auto-selection when the user hasn't explicitly picked
        /// a device in the UI.
        /// </summary>
        public static DeviceInstance? FindLikelyController()
        {
            try
            {
                var list = EnumerateDevices().ToList();
                // Preferred keywords
                string[] keywords = new[] { "xbox", "controller", "gamepad", "wireless", "joystick", "360", "ps4", "ps5", "dualshock" };

                // 1) Find device with keyword in description and VID_ in instance id
                foreach (var d in list)
                {
                    if (string.IsNullOrEmpty(d.InstanceId) || string.IsNullOrEmpty(d.Description)) continue;
                    var idu = d.InstanceId.ToUpperInvariant();
                    var desc = d.Description.ToLowerInvariant();
                    if (!idu.Contains("VID_")) continue;
                    if (keywords.Any(k => desc.Contains(k))) return d;
                }

                // 2) Fallback: first device with VID_ and a non-empty description
                var fallback = list.FirstOrDefault(d => !string.IsNullOrEmpty(d.InstanceId) && d.InstanceId.ToUpperInvariant().Contains("VID_") && !string.IsNullOrEmpty(d.Description));
                if (fallback != null) return fallback;

                // 3) Last resort: any device with controller-related keyword
                var any = list.FirstOrDefault(d => !string.IsNullOrEmpty(d.Description) && keywords.Any(k => d.Description.ToLowerInvariant().Contains(k)));
                return any;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Find device by explicit VID/PID strings (e.g. "VID_045E","PID_028E").
        /// Returns first match.
        /// </summary>
        public static DeviceInstance? FindDeviceByVidPid(string vid, string pid)
        {
            if (string.IsNullOrEmpty(vid) || string.IsNullOrEmpty(pid)) return null;
            vid = vid.ToUpperInvariant(); pid = pid.ToUpperInvariant();
            try
            {
                foreach (var d in EnumerateDevices())
                {
                    var idu = d.InstanceId.ToUpperInvariant();
                    if (idu.Contains(vid) && idu.Contains(pid)) return d;
                }
            }
            catch { }
            return null;
        }

        // P/Invoke and constants
        private static readonly IntPtr INVALID_HANDLE_VALUE = new IntPtr(-1);

        private const uint DIGCF_PRESENT = 0x00000002;
        private const uint DIGCF_ALLCLASSES = 0x00000004;

        private const uint SPDRP_FRIENDLYNAME = 0x0000000C;
        private const uint SPDRP_DEVICEDESC = 0x00000000;

        private const int DIF_PROPERTYCHANGE = 0x12;
        private const int DICS_ENABLE = 0x00000001;
        private const int DICS_DISABLE = 0x00000002;
        private const int DICS_FLAG_GLOBAL = 0x00000001;

        [StructLayout(LayoutKind.Sequential)]
        private struct SP_DEVINFO_DATA
        {
            public int cbSize;
            public Guid ClassGuid;
            public uint DevInst;
            public IntPtr Reserved;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct SP_CLASSINSTALL_HEADER
        {
            public int cbSize;
            public int InstallFunction;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct SP_PROPCHANGE_PARAMS
        {
            public SP_CLASSINSTALL_HEADER ClassInstallHeader;
            public int StateChange;
            public int Scope;
            public int HwProfile;
        }

        [DllImport("setupapi.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr SetupDiGetClassDevs(IntPtr ClassGuid, string? Enumerator, IntPtr hwndParent, uint Flags);

        [DllImport("setupapi.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern bool SetupDiEnumDeviceInfo(IntPtr DeviceInfoSet, uint MemberIndex, ref SP_DEVINFO_DATA DeviceInfoData);

        [DllImport("setupapi.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern bool SetupDiGetDeviceInstanceId(IntPtr DeviceInfoSet, ref SP_DEVINFO_DATA DeviceInfoData, StringBuilder DeviceInstanceId, int DeviceInstanceIdSize, out int RequiredSize);

        [DllImport("setupapi.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern bool SetupDiGetDeviceRegistryProperty(IntPtr DeviceInfoSet, ref SP_DEVINFO_DATA DeviceInfoData, uint Property, out uint PropertyRegDataType, StringBuilder PropertyBuffer, int PropertyBufferSize, out int RequiredSize);

        [DllImport("setupapi.dll", SetLastError = true)]
        private static extern bool SetupDiSetClassInstallParams(IntPtr DeviceInfoSet, ref SP_DEVINFO_DATA DeviceInfoData, ref SP_PROPCHANGE_PARAMS ClassInstallParams, int ClassInstallParamsSize);

        [DllImport("setupapi.dll", SetLastError = true)]
        private static extern bool SetupDiCallClassInstaller(int InstallFunction, IntPtr DeviceInfoSet, ref SP_DEVINFO_DATA DeviceInfoData);

        [DllImport("setupapi.dll", SetLastError = true)]
        private static extern bool SetupDiDestroyDeviceInfoList(IntPtr DeviceInfoSet);
    }
}
