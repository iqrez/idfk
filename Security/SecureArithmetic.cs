using System;

namespace WootMouseRemap.Security
{
    public static class SecureArithmetic
    {
        public static bool TryAdd(int a, int b, out int result)
        {
            try
            {
                checked
                {
                    result = a + b;
                    return true;
                }
            }
            catch (OverflowException)
            {
                result = 0;
                return false;
            }
        }

        public static bool TryAdd(float a, float b, out float result)
        {
            result = a + b;
            return !float.IsInfinity(result) && !float.IsNaN(result);
        }

        public static bool TrySubtract(int a, int b, out int result)
        {
            try
            {
                checked
                {
                    result = a - b;
                    return true;
                }
            }
            catch (OverflowException)
            {
                result = 0;
                return false;
            }
        }

        public static bool TryMultiply(float a, float b, out float result)
        {
            result = a * b;
            return !float.IsInfinity(result) && !float.IsNaN(result);
        }

        public static bool TryConvertToInt(double value, out int result)
        {
            if (value >= int.MinValue && value <= int.MaxValue)
            {
                result = (int)value;
                return true;
            }
            result = 0;
            return false;
        }

        public static bool IsValidRange(int value, int min, int max)
        {
            return value >= min && value <= max;
        }

        public static bool IsWithinBounds(float value, float min = -1000000f, float max = 1000000f)
        {
            return !float.IsInfinity(value) && !float.IsNaN(value) && value >= min && value <= max;
        }

        public static bool IsWithinBounds(float value)
        {
            return !float.IsInfinity(value) && !float.IsNaN(value);
        }

        public static int ClampToRange(int value, int min, int max)
        {
            return Math.Max(min, Math.Min(max, value));
        }

        public static float SafeAdd(float a, float b)
        {
            var result = a + b;
            if (float.IsInfinity(result) || float.IsNaN(result))
            {
                return a > 0 ? float.MaxValue : float.MinValue;
            }
            return result;
        }

        public static float SafeMultiply(float a, float b)
        {
            var result = a * b;
            if (float.IsInfinity(result) || float.IsNaN(result))
            {
                return (a > 0) == (b > 0) ? float.MaxValue : float.MinValue;
            }
            return result;
        }

        public static float SafeDivide(float a, float b)
        {
            if (Math.Abs(b) < float.Epsilon)
            {
                return 0f; // Safe default for division by zero
            }
            var result = a / b;
            if (float.IsInfinity(result) || float.IsNaN(result))
            {
                return 0f;
            }
            return result;
        }
    }
}