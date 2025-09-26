using System;
using System.Runtime.CompilerServices;

namespace WootMouseRemap
{
    public readonly struct MouseSettings : IEquatable<MouseSettings>
    {
        public readonly float SensitivityX;
        public readonly float SensitivityY;
        public readonly float Deadzone;
        public readonly float Gamma;
        public readonly float Smoothing;
        public readonly float AccelGain;
        public readonly float AccelCap;
        public readonly bool InvertX;
        public readonly bool InvertY;

        public static MouseSettings Defaults => new MouseSettings(
            sensitivityX: 1.00f,
            sensitivityY: 1.00f,
            deadzone:     0.05f,
            gamma:        1.40f,
            smoothing:    0.25f,
            accelGain:    0.00f,
            accelCap:     1.50f,
            invertX:      false,
            invertY:      false
        );

        public MouseSettings(
            float sensitivityX,
            float sensitivityY,
            float deadzone,
            float gamma,
            float smoothing,
            float accelGain,
            float accelCap,
            bool invertX,
            bool invertY)
        {
            SensitivityX = Clamp(sensitivityX, 0.01f, 10.0f);
            SensitivityY = Clamp(sensitivityY, 0.01f, 10.0f);
            Deadzone     = Clamp(deadzone,     0.00f, 0.50f);
            Gamma        = Clamp(gamma,        0.10f, 3.00f);
            Smoothing    = Clamp(smoothing,    0.00f, 1.00f);
            AccelGain    = Clamp(accelGain,    0.00f, 3.00f);
            AccelCap     = Clamp(accelCap,     0.00f, 5.00f);
            InvertX      = invertX;
            InvertY      = invertY;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float Clamp(float v, float min, float max) => v < min ? min : (v > max ? max : v);

        public MouseSettings With(
            float? sensitivityX = null,
            float? sensitivityY = null,
            float? deadzone = null,
            float? gamma = null,
            float? smoothing = null,
            float? accelGain = null,
            float? accelCap = null,
            bool? invertX = null,
            bool? invertY = null)
        {
            return new MouseSettings(
                sensitivityX ?? SensitivityX,
                sensitivityY ?? SensitivityY,
                deadzone     ?? Deadzone,
                gamma        ?? Gamma,
                smoothing    ?? Smoothing,
                accelGain    ?? AccelGain,
                accelCap     ?? AccelCap,
                invertX      ?? InvertX,
                invertY      ?? InvertY
            );
        }

        public bool Equals(MouseSettings other) =>
            SensitivityX.Equals(other.SensitivityX) &&
            SensitivityY.Equals(other.SensitivityY) &&
            Deadzone.Equals(other.Deadzone) &&
            Gamma.Equals(other.Gamma) &&
            Smoothing.Equals(other.Smoothing) &&
            AccelGain.Equals(other.AccelGain) &&
            AccelCap.Equals(other.AccelCap) &&
            InvertX == other.InvertX &&
            InvertY == other.InvertY;

        public override bool Equals(object? obj) => obj is MouseSettings ms && Equals(ms);
        public override int GetHashCode()
        {
            var hc = new HashCode();
            hc.Add(SensitivityX);
            hc.Add(SensitivityY);
            hc.Add(Deadzone);
            hc.Add(Gamma);
            hc.Add(Smoothing);
            hc.Add(AccelGain);
            hc.Add(AccelCap);
            hc.Add(InvertX);
            hc.Add(InvertY);
            return hc.ToHashCode();
        }
        public static bool operator ==(MouseSettings a, MouseSettings b) => a.Equals(b);
        public static bool operator !=(MouseSettings a, MouseSettings b) => !a.Equals(b);

    }
}