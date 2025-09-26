
using System;

namespace WootMouseRemap.Controllers
{
    public struct PadSnapshot
    {
        public short LX, LY, RX, RY;
        public byte LT, RT;
        public bool A, B, X, Y, LB, RB, Back, Start, L3, R3, DUp, DDown, DLeft, DRight;
        public void Clear()
        {
            LX = LY = RX = RY = 0;
            LT = RT = 0;
            A = B = X = Y = LB = RB = Back = Start = L3 = R3 = DUp = DDown = DLeft = DRight = false;
        }
    }

    public abstract class PhysicalController : IDisposable
    {
        public abstract string DisplayName { get; }
        public abstract bool IsConnected { get; }
        public abstract bool Poll(out PadSnapshot snap);
        public virtual void Dispose() { }
    }
}
