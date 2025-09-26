
using System;
using System.Collections.Generic;

namespace WootMouseRemap.Controllers
{
    public static class ControllerManager
    {
        public static IEnumerable<(int Index, string Name)> EnumerateXInput()
        {
            for (int i = 0; i < 4; i++) yield return (i, $"XInput {i}");
        }

#if DIRECTINPUT
        public static IEnumerable<(Guid Instance, string Name)> EnumerateDirectInput()
        {
            var di = new SharpDX.DirectInput.DirectInput();
            foreach (var dev in di.GetDevices(SharpDX.DirectInput.DeviceClass.GameControl, SharpDX.DirectInput.DeviceEnumerationFlags.AttachedOnly))
                yield return (dev.InstanceGuid, $"{dev.ProductName} ({dev.InstanceGuid})");
        }
#else
        public static IEnumerable<(Guid Instance, string Name)> EnumerateDirectInput()
        {
            yield break;
        }
#endif
    }
}
