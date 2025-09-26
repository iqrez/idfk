#if DIRECTINPUT

using System;
using SharpDX.DirectInput;

namespace WootMouseRemap.Controllers
{
    public sealed class DirectInputController : PhysicalController
    {
        private readonly DirectInput _di;
        private readonly Joystick _js;
        private readonly string _name;

        public DirectInputController(Guid instanceGuid)
        {
            _di = new DirectInput();
            _js = new Joystick(_di, instanceGuid);
            _name = _js.Information.ProductName;
            try { _js.Acquire(); } catch { }
        }

        public override string DisplayName => $"DirectInput: {_name}";
        public override bool IsConnected
        {
            get
            {
                try { _js.Poll(); return true; } catch { return false; }
            }
        }

        public override bool Poll(out PadSnapshot snap)
        {
            snap = default;
            try
            {
                _js.Poll();
                var s = _js.GetCurrentState();
                // Basic heuristic mapping (works for many pads; can be customized later)
                snap.LX = (short)(s.X - 32767);
                snap.LY = (short)(s.Y - 32767);
                snap.RX = (short)(s.RotationX - 32767);
                snap.RY = (short)(s.RotationY - 32767);
                // Triggers
                snap.LT = (byte)(Math.Clamp(s.Z, 0, 65535) / 257);
                snap.RT = (byte)(Math.Clamp(s.RotationZ, 0, 65535) / 257);

                var btns = s.Buttons;
                bool Bn(int i) => i >= 0 && i < btns.Length && btns[i];
                snap.A = Bn(0);
                snap.B = Bn(1);
                snap.X = Bn(2);
                snap.Y = Bn(3);
                snap.LB = Bn(4);
                snap.RB = Bn(5);
                snap.Back = Bn(8);
                snap.Start = Bn(9);
                snap.L3 = Bn(10);
                snap.R3 = Bn(11);

                // POV/DPad
                var povs = s.PointOfViewControllers;
                if (povs != null && povs.Length > 0)
                {
                    int pov = povs[0];
                    if (pov == 0 || pov == 4500 || pov == 31500) snap.DUp = true;
                    if (pov == 9000 || pov == 4500 || pov == 13500) snap.DRight = true;
                    if (pov == 18000 || pov == 13500 || pov == 22500) snap.DDown = true;
                    if (pov == 27000 || pov == 22500 || pov == 31500) snap.DLeft = true;
                }
                return true;
            }
            catch
            {
                return false;
            }
        }

        public override void Dispose()
        {
            try { _js.Unacquire(); } catch { }
            try { _js.Dispose(); } catch { }
            try { _di.Dispose(); } catch { }
            base.Dispose();
        }
    }
}

#endif
