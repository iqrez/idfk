using System;

namespace WootMouseRemap
{
    public sealed class CurveProcessor
    {
        public float Sensitivity { get; set; } = 0.35f;   // global multiplier
        public float Expo { get; set; } = 0.6f;           // 0 linear .. 1 cubic
        public float AntiDeadzone { get; set; } = 0.05f;  // push off center
        public float MaxSpeed { get; set; } = 1.0f;       // clamp in [-1..1]
        public float EmaAlpha { get; set; } = 0.35f;      // smoothing 0..1
        public float VelocityGain { get; set; } = 0.0f;   // extra scale by |v|
        public float VelocityCap { get; set; } = 1.5f; // cap for |v| contribution
        public float JitterFloor { get; set; } = 0.0f;    // ignore tiny deltas
        public float ScaleX { get; set; } = 1.0f;
        public float ScaleY { get; set; } = 1.0f;

        private float _emaX, _emaY;
        private float _lastR;

        public (short X, short Y) ToStick(float dx, float dy)
        {
            // Convert mouse deltas to floating-point stick deltas using sensitivity and anisotropic scaling
            float scale = Sensitivity / 50f;
            float x = dx * scale * ScaleX;
            float y = dy * scale * ScaleY;

            // Compute radial magnitude
            float r = MathF.Sqrt(x * x + y * y);
            // Apply jitter floor radially rather than per-axis.  When the total movement is tiny, ignore it.
            if (r < JitterFloor)
            {
                x = 0f;
                y = 0f;
                r = 0f;
            }

            if (r > 0f)
            {
                // Unit vector in direction of movement
                float ux = x / r;
                float uy = y / r;

                // Velocity-based gain computed from change in radial magnitude instead of per-axis
                float v = MathF.Abs(r - _lastR);
                float velGain = 1f + VelocityGain * Math.Clamp(v, 0f, VelocityCap);
                r *= velGain;
                _lastR = r;

                // Normalize to 0..1 relative to maximum speed
                float maxR = MaxSpeed > 0f ? MaxSpeed : 1f;
                float rn = Math.Clamp(r / maxR, 0f, 1f);
                // Apply exponential curve to radial magnitude
                float expo = Expo;
                if (expo > 0f)
                {
                    // Ease-out style: preserves fine aim and accelerates towards edges
                    rn = MathF.Pow(rn, 1f - expo);
                }

                // Apply anti-deadzone radially
                float adz = AntiDeadzone;
                rn = rn <= 0f ? 0f : (adz + (1f - adz) * rn);

                // Convert back to stick space (0..maxR)
                float targetR = rn * maxR;
                x = ux * targetR;
                y = uy * targetR;
            }
            else
            {
                _lastR = 0f;
            }

            // Vector EMA smoothing: apply the same alpha to both axes based on the same radial magnitude
            float alpha = EmaAlpha;
            if (alpha > 0f)
            {
                _emaX += alpha * (x - _emaX);
                _emaY += alpha * (y - _emaY);
                x = _emaX;
                y = _emaY;
            }

            // Final circular clamp.  Ensure the vector length does not exceed MaxSpeed.
            float rr = MathF.Sqrt(x * x + y * y);
            float m = MaxSpeed > 0f ? MaxSpeed : 1f;
            if (rr > m)
            {
                float s = m / rr;
                x *= s;
                y *= s;
            }

            // Convert from [-1,1] to [-32768,32767] stick units.  Note Y is inverted for Xbox semantics.
            short sx = (short)Math.Clamp(x * 32767f, -32768f, 32767f);
            short sy = (short)Math.Clamp(-y * 32767f, -32768f, 32767f);
            return (sx, sy);
        }

        // Removed unused ApplyExpo and PushAntiDeadzone methods - functionality integrated into ToStick

        public void ResetSmoothing()
        {
            _emaX = 0f;
            _emaY = 0f;
            _lastR = 0f;
        }
    }
}
