using System;
using WootMouseRemap.Core;

namespace WootMouseRemap.Core.Mapping
{
    public static class ProfileAdapters
    {
        // Map legacy ConfigurationProfile to new InputMappingProfile (minimal mapping)
        public static InputMappingProfile MapFromConfigurationProfile(ConfigurationProfile src)
        {
            if (src == null) throw new ArgumentNullException(nameof(src));

            var dest = new InputMappingProfile
            {
                Name = src.Name ?? "",
                Description = src.Description ?? string.Empty,
                MouseDpi = src.MouseDpi <= 0 ? 1600 : src.MouseDpi,
                CurveSettings = new CurveSettings
                {
                    Sensitivity = src.Curves?.Sensitivity ?? 1.0f,
                    Expo = src.Curves?.Expo ?? 0.0f,
                    AntiDeadzone = src.Curves?.Deadzone ?? 0.0f,
                    MaxSpeed = src.Curves?.MaxOutput ?? 1.0f,
                    EmaAlpha = src.Curves?.EmaAlpha ?? 0.5f,
                    ScaleX = src.Curves?.ScaleX ?? 1.0f,
                    ScaleY = src.Curves?.ScaleY ?? 1.0f,
                    JitterFloor = 0.0f
                }
            };

            // Basic mapping for mouse mapping config defaults
            dest.MouseConfig.EnableMouseToRightStick = true;
            dest.MouseConfig.WheelSensitivity = 1.0f;

            return dest;
        }

        // Map back if needed - keep minimal and best-effort
        public static ConfigurationProfile MapToConfigurationProfile(InputMappingProfile src)
        {
            if (src == null) throw new ArgumentNullException(nameof(src));

            var dest = new ConfigurationProfile
            {
                Name = src.Name ?? string.Empty,
                Description = src.Description ?? string.Empty,
                MouseDpi = src.MouseDpi <= 0 ? 1600 : src.MouseDpi,
                Curves = new ResponseCurves
                {
                    Sensitivity = src.CurveSettings?.Sensitivity ?? 1.0f,
                    Expo = src.CurveSettings?.Expo ?? 0.0f,
                    Deadzone = src.CurveSettings?.AntiDeadzone ?? 0.0f,
                    MaxOutput = src.CurveSettings?.MaxSpeed ?? 1.0f,
                    EmaAlpha = src.CurveSettings?.EmaAlpha ?? 0.5f,
                    ScaleX = src.CurveSettings?.ScaleX ?? 1.0f,
                    ScaleY = src.CurveSettings?.ScaleY ?? 1.0f
                }
            };

            return dest;
        }
    }
}
