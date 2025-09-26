using System;
using System.Collections.Generic;
using System.Windows.Forms;
using WootMouseRemap.Security;

namespace WootMouseRemap
{
    public sealed class StickMapper
    {
        private readonly CurveProcessor _curve = new();
        private readonly HashSet<Keys> _pressed = new();

        public CurveProcessor Curve => _curve;

        public (short X, short Y) MouseToRightStick(int dx, int dy) => _curve.ToStick(dx, dy);

        public (short X, short Y) WasdToLeftStick()
        {
            int x = 0, y = 0;
            if (_pressed.Contains(Keys.A)) x -= 1;
            if (_pressed.Contains(Keys.D)) x += 1;
            if (_pressed.Contains(Keys.W)) y -= 1;
            if (_pressed.Contains(Keys.S)) y += 1;

            float fx = x; float fy = y;
            if (!SecureArithmetic.TryMultiply(fx, fx, out float fxSq) || 
                !SecureArithmetic.TryMultiply(fy, fy, out float fySq))
                return (0, 0);
                
            float len = MathF.Sqrt(fxSq + fySq);
            if (len > 1e-5) { fx /= len; fy /= len; }
            
            if (!SecureArithmetic.TryMultiply(fx, 32767, out float scaledX) ||
                !SecureArithmetic.TryMultiply(-fy, 32767, out float scaledY))
                return (0, 0);
                
            short sx = (short)Math.Clamp(scaledX, short.MinValue, short.MaxValue);
            short sy = (short)Math.Clamp(scaledY, short.MinValue, short.MaxValue);
            return (sx, sy);
        }

        public void UpdateKey(int vk, bool down)
        {
            Keys k = (Keys)vk;
            if (down) _pressed.Add(k); else _pressed.Remove(k);
        }
    }
}
