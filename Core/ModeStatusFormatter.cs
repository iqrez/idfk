using System;

namespace WootMouseRemap.Core
{
    /// <summary>
    /// Helper for producing consistent UI/diagnostic strings for current mode + suppression state + optional extra info.
    /// </summary>
    public static class ModeStatusFormatter
    {
        public static string Format(IModeService service, string? extra = null)
        {
            if (service == null) return "Mode: (service null)";
            var sup = service.SuppressionActive ? "Suppression: ON" : "Suppression: OFF";
            var core = $"Mode: {service.CurrentMode} | {sup}";
            if (!string.IsNullOrWhiteSpace(extra)) core += " | " + extra;
            return core;
        }
    }
}
