using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using WootMouseRemap.Features;
// Logger is in WootMouseRemap namespace
using WootMouseRemap.Core;

namespace WootMouseRemap.UI
{
    /// <summary>
    /// Advanced features and enhancements for the anti-recoil system
    /// </summary>
    public static class AdvancedFeatures
    {
        /// <summary>
        /// Performance monitoring and optimization suggestions
        /// </summary>
        public static class PerformanceMonitor
        {
            private static readonly Queue<float> _recentCpuUsage = new();
            private static readonly Queue<TimeSpan> _recentProcessingTimes = new();
            private static DateTime _lastOptimizationCheck = DateTime.MinValue;

            public static void RecordProcessingTime(TimeSpan processingTime)
            {
                _recentProcessingTimes.Enqueue(processingTime);
                if (_recentProcessingTimes.Count > 100)
                    _recentProcessingTimes.Dequeue();
            }

            public static PerformanceMetrics GetMetrics()
            {
                if (_recentProcessingTimes.Count == 0)
                    return new PerformanceMetrics();

                var times = _recentProcessingTimes.ToArray();
                return new PerformanceMetrics
                {
                    AverageProcessingTime = TimeSpan.FromTicks((long)times.Average(t => t.Ticks)),
                    MaxProcessingTime = new TimeSpan(times.Max(t => t.Ticks)),
                    MinProcessingTime = new TimeSpan(times.Min(t => t.Ticks)),
                    SampleCount = times.Length
                };
            }

            public static List<string> GetOptimizationSuggestions(AntiRecoilViewModel viewModel)
            {
                var suggestions = new List<string>();
                var metrics = GetMetrics();

                // Check if performance monitoring is needed
                if (DateTime.Now.Subtract(_lastOptimizationCheck).TotalMinutes < 5)
                    return suggestions;

                _lastOptimizationCheck = DateTime.Now;

                if (metrics.AverageProcessingTime.TotalMilliseconds > 2.0)
                {
                    suggestions.Add("Consider reducing telemetry update frequency for better performance");
                }

                if (viewModel.DecayPerMs > 0 && viewModel.CooldownMs == 0)
                {
                    suggestions.Add("Add cooldown period to reduce computational overhead");
                }

                if (viewModel.MaxTickCompensation == 0 && viewModel.MaxTotalCompensation == 0)
                {
                    suggestions.Add("Enable compensation limits to prevent excessive calculations");
                }

                return suggestions;
            }
        }

        /// <summary>
        /// Accessibility enhancements
        /// </summary>
        public static class AccessibilityHelper
        {
            public static void ApplyAccessibilityEnhancements(Form form)
            {
                // High contrast support
                if (SystemInformation.HighContrast)
                {
                    ApplyHighContrastTheme(form);
                }

                // Screen reader support
                AddScreenReaderSupport(form);

                // Keyboard navigation improvements
                ImproveKeyboardNavigation(form);
            }

            private static void ApplyHighContrastTheme(Control control)
            {
                control.BackColor = SystemColors.Control;
                control.ForeColor = SystemColors.ControlText;

                foreach (Control child in control.Controls)
                {
                    ApplyHighContrastTheme(child);

                    // Special handling for specific control types
                    switch (child)
                    {
                        case Button button:
                            button.BackColor = SystemColors.ButtonFace;
                            button.ForeColor = SystemColors.ControlText; // replaced ButtonText
                            button.FlatStyle = FlatStyle.System;
                            break;
                        case TextBox textBox:
                            textBox.BackColor = SystemColors.Window;
                            textBox.ForeColor = SystemColors.WindowText;
                            break;
                        case Label label:
                            label.BackColor = SystemColors.Control;
                            label.ForeColor = SystemColors.ControlText;
                            break;
                    }
                }
            }

            private static void AddScreenReaderSupport(Control control)
            {
                foreach (Control child in control.Controls)
                {
                    // Add accessible descriptions
                    switch (child)
                    {
                        case NumericUpDown numeric when child.Name.Contains("Strength"):
                            child.AccessibleDescription = "Anti-recoil compensation strength as percentage";
                            child.AccessibleRole = AccessibleRole.SpinButton;
                            break;
                        case NumericUpDown numeric when child.Name.Contains("Threshold"):
                            child.AccessibleDescription = "Minimum vertical movement to trigger compensation";
                            child.AccessibleRole = AccessibleRole.SpinButton;
                            break;
                        case CheckBox checkbox:
                            checkbox.AccessibleRole = AccessibleRole.CheckButton;
                            break;
                        case Button button:
                            button.AccessibleRole = AccessibleRole.PushButton;
                            break;
                    }

                    AddScreenReaderSupport(child);
                }
            }

            private static void ImproveKeyboardNavigation(Control control)
            {
                // Ensure all interactive controls can receive focus
                foreach (Control child in control.Controls)
                {
                    if (child is Button || child is NumericUpDown || child is CheckBox || child is ComboBox)
                    {
                        child.TabStop = true;
                    }

                    ImproveKeyboardNavigation(child);
                }
            }
        }

        /// <summary>
        /// Advanced pattern analysis and recommendations
        /// </summary>
        public static class PatternAnalyzer
        {
            public static PatternAnalysisResult AnalyzePattern(AntiRecoilPattern pattern)
            {
                if (pattern?.Samples == null || pattern.Samples.Count == 0)
                    return new PatternAnalysisResult { IsValid = false };

                var analysis = new PatternAnalysisResult { IsValid = true };

                // Analyze pattern characteristics
                AnalyzeStability(pattern, analysis);
                AnalyzePredictability(pattern, analysis);
                AnalyzeOptimalSettings(pattern, analysis);

                return analysis;
            }

            private static void AnalyzeStability(AntiRecoilPattern pattern, PatternAnalysisResult result)
            {
                var dyValues = pattern.Samples.Select(s => s.Dy).ToArray();
                var variance = CalculateVariance(dyValues);

                result.StabilityScore = Math.Max(0, Math.Min(100, 100 - (variance / 10))); // Normalize to 0-100

                if (result.StabilityScore < 30)
                {
                    result.Recommendations.Add("Pattern shows high variance - consider using smoothing transforms");
                }
                else if (result.StabilityScore > 80)
                {
                    result.Recommendations.Add("Pattern is very stable - good for consistent compensation");
                }
            }

            private static void AnalyzePredictability(AntiRecoilPattern pattern, PatternAnalysisResult result)
            {
                var samples = pattern.Samples.ToArray();
                var predictable = 0;

                // Simple predictability check: how often does the direction change
                for (int i = 2; i < samples.Length; i++)
                {
                    var dir1 = Math.Sign(samples[i - 1].Dy - samples[i - 2].Dy);
                    var dir2 = Math.Sign(samples[i].Dy - samples[i - 1].Dy);

                    if (dir1 == dir2)
                        predictable++;
                }

                result.PredictabilityScore = samples.Length > 2 ? (predictable * 100f) / (samples.Length - 2) : 0;

                if (result.PredictabilityScore > 70)
                {
                    result.Recommendations.Add("Pattern is highly predictable - consider enabling adaptive compensation");
                }
                else if (result.PredictabilityScore < 30)
                {
                    result.Recommendations.Add("Pattern is chaotic - use higher threshold to avoid over-compensation");
                }
            }

            private static void AnalyzeOptimalSettings(AntiRecoilPattern pattern, PatternAnalysisResult result)
            {
                var dyValues = pattern.Samples.Select(s => Math.Abs(s.Dy)).ToArray();
                var maxMovement = dyValues.Max();
                var avgMovement = dyValues.Average();

                // Suggest optimal threshold (slightly below average significant movement)
                result.SuggestedThreshold = avgMovement * 0.8f;

                // Suggest strength based on pattern intensity
                if (maxMovement > 20)
                {
                    result.SuggestedStrength = 0.6f; // High strength for large movements
                    result.Recommendations.Add("Large recoil detected - suggest strength around 60%");
                }
                else if (maxMovement > 10)
                {
                    result.SuggestedStrength = 0.4f; // Medium strength
                    result.Recommendations.Add("Moderate recoil detected - suggest strength around 40%");
                }
                else
                {
                    result.SuggestedStrength = 0.2f; // Low strength for small movements
                    result.Recommendations.Add("Small recoil detected - suggest strength around 20%");
                }

                // Suggest max tick compensation based on peak movement
                result.SuggestedMaxTick = maxMovement * 0.7f;
            }

            private static float CalculateVariance(float[] values)
            {
                if (values.Length < 2) return 0;

                var mean = values.Average();
                var sumSquaredDiff = values.Sum(v => (v - mean) * (v - mean));
                return sumSquaredDiff / (values.Length - 1);
            }
        }

        /// <summary>
        /// Export and import utilities
        /// </summary>
        public static class DataExporter
        {
            public static string ExportTelemetryToCsv(List<TelemetryDataPoint> dataPoints)
            {
                var csv = new System.Text.StringBuilder();
                csv.AppendLine("Timestamp,VerticalMovement,Compensation,AccumulatedCompensation,IsActive,Threshold");

                foreach (var point in dataPoints)
                {
                    csv.AppendLine($"{point.Timestamp:yyyy-MM-dd HH:mm:ss.fff}," +
                                  $"{point.VerticalMovement}," +
                                  $"{point.Compensation}," +
                                  $"{point.AccumulatedCompensation}," +
                                  $"{point.IsActive}," +
                                  $"{point.Threshold}");
                }

                return csv.ToString();
            }

            public static string ExportPatternToJson(AntiRecoilPattern pattern)
            {
                return System.Text.Json.JsonSerializer.Serialize(pattern, new System.Text.Json.JsonSerializerOptions
                {
                    WriteIndented = true,
                    PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase
                });
            }

            public static AntiRecoilPattern ImportPatternFromJson(string json)
            {
                var options = new System.Text.Json.JsonSerializerOptions
                {
                    PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase,
                    MaxDepth = 5,
                    PropertyNameCaseInsensitive = false,
                    AllowTrailingCommas = false
                };
                try 
                {
                    return System.Text.Json.JsonSerializer.Deserialize<AntiRecoilPattern>(json, options) ?? new AntiRecoilPattern();
                }
                catch (System.Text.Json.JsonException) 
                {
                    return new AntiRecoilPattern(); // Safe fallback
                }
            }
        }
    }

    public class PerformanceMetrics
    {
        public TimeSpan AverageProcessingTime { get; set; }
        public TimeSpan MaxProcessingTime { get; set; }
        public TimeSpan MinProcessingTime { get; set; }
        public int SampleCount { get; set; }
    }

    public class PatternAnalysisResult
    {
        public bool IsValid { get; set; }
        public float StabilityScore { get; set; }
        public float PredictabilityScore { get; set; }
        public float SuggestedThreshold { get; set; }
        public float SuggestedStrength { get; set; }
        public float SuggestedMaxTick { get; set; }
        public List<string> Recommendations { get; set; } = new();
    }
}