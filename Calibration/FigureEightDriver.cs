using System;

namespace WootMouseRemap
{
    public sealed class FigureEightDriver
    {
        // Deterministic Lissajous style
        public (int dx, int dy) At(double t)
        {
            // Base amplitude in "mouse delta" units
            double ax = Math.Sin(t * 2.0) * Math.Cos(t * 0.5);
            double ay = Math.Sin(t) * Math.Cos(t * 2.0);
            return ((int)(ax * 150), (int)(ay * 150));
        }
    }
}
