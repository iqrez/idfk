using System;
using System.Collections.Generic;
using System.Linq;
// Logger is in WootMouseRemap namespace

namespace WootMouseRemap.Features
{
    /// <summary>
    /// Static helper class providing pattern transformation and editing utilities
    /// </summary>
    public static class PatternTransforms
    {
        /// <summary>
        /// Normalize a pattern to have a target peak vertical movement
        /// </summary>
        /// <param name="pattern">The pattern to normalize</param>
        /// <param name="targetPeakDy">Target peak vertical movement value</param>
        /// <returns>New normalized pattern</returns>
        public static AntiRecoilPattern Normalize(AntiRecoilPattern pattern, float targetPeakDy)
        {
            if (pattern?.Samples == null || pattern.Samples.Count == 0)
            {
                // Return a copy of the empty pattern
                return new AntiRecoilPattern
                {
                    Name = pattern?.Name ?? "EmptyNormalized",
                    CreatedUtc = DateTime.UtcNow,
                    Notes = "Normalized from empty pattern",
                    Tags = new List<string>(pattern?.Tags ?? new List<string>()),
                    Version = pattern?.Version ?? 1,
                    Samples = new List<AntiRecoilSample>()
                };
            }

            if (targetPeakDy <= 0)
                throw new ArgumentException("Target peak must be positive", nameof(targetPeakDy));

            var currentPeakDy = pattern.Samples.Max(s => Math.Abs(s.Dy));
            if (currentPeakDy == 0) return ClonePattern(pattern, "Normalized");

            var scaleFactor = targetPeakDy / currentPeakDy;

            var normalizedPattern = new AntiRecoilPattern
            {
                Name = $"{pattern.Name}_Normalized",
                CreatedUtc = DateTime.UtcNow,
                Notes = $"Normalized from '{pattern.Name}' with scale factor {scaleFactor:F3}",
                Tags = new List<string>(pattern.Tags) { "normalized" },
                Version = Math.Max(1, pattern.Version),
                Samples = pattern.Samples.Select(s => new AntiRecoilSample
                {
                    Dx = s.Dx * scaleFactor,
                    Dy = s.Dy * scaleFactor
                }).ToList()
            };

            Logger.Info("Normalized pattern '{PatternName}' with scale factor {ScaleFactor:F3} (peak: {CurrentPeak:F2} -> {TargetPeak:F2})", pattern.Name, scaleFactor, currentPeakDy, targetPeakDy);
            return normalizedPattern;
        }

        /// <summary>
        /// Apply moving average smoothing to a pattern
        /// </summary>
        /// <param name="pattern">The pattern to smooth</param>
        /// <param name="windowSize">Size of the moving average window</param>
        /// <returns>New smoothed pattern</returns>
        public static AntiRecoilPattern SmoothMovingAverage(AntiRecoilPattern pattern, int windowSize)
        {
            if (pattern?.Samples == null || pattern.Samples.Count == 0)
            {
                // Return a copy of the empty pattern
                return new AntiRecoilPattern
                {
                    Name = pattern?.Name ?? "EmptySmoothed",
                    CreatedUtc = DateTime.UtcNow,
                    Notes = "Smoothed from empty pattern",
                    Tags = new List<string>(pattern?.Tags ?? new List<string>()),
                    Version = pattern?.Version ?? 1,
                    Samples = new List<AntiRecoilSample>()
                };
            }

            if (windowSize < 1)
                throw new ArgumentException("Window size must be at least 1", nameof(windowSize));

            if (windowSize >= pattern.Samples.Count)
                return ClonePattern(pattern, "Smoothed");

            var smoothedSamples = new List<AntiRecoilSample>();

            for (int i = 0; i < pattern.Samples.Count; i++)
            {
                var startIdx = Math.Max(0, i - windowSize / 2);
                var endIdx = Math.Min(pattern.Samples.Count - 1, i + windowSize / 2);

                var avgDx = 0f;
                var avgDy = 0f;
                var count = 0;

                for (int j = startIdx; j <= endIdx; j++)
                {
                    avgDx += pattern.Samples[j].Dx;
                    avgDy += pattern.Samples[j].Dy;
                    count++;
                }

                smoothedSamples.Add(new AntiRecoilSample
                {
                    Dx = avgDx / count,
                    Dy = avgDy / count
                });
            }

            var smoothedPattern = new AntiRecoilPattern
            {
                Name = $"{pattern.Name}_Smoothed",
                CreatedUtc = DateTime.UtcNow,
                Notes = $"Smoothed from '{pattern.Name}' with {windowSize}-sample moving average",
                Tags = new List<string>(pattern.Tags) { "smoothed" },
                Version = Math.Max(1, pattern.Version),
                Samples = smoothedSamples
            };

            Logger.Info("Smoothed pattern '{PatternName}' with window size {WindowSize}", pattern.Name, windowSize);
            return smoothedPattern;
        }

        /// <summary>
        /// Remove leading samples with minimal vertical movement
        /// </summary>
        /// <param name="pattern">The pattern to trim</param>
        /// <param name="threshold">Threshold for "zero" movement (default: 0.1)</param>
        /// <returns>New trimmed pattern</returns>
        public static AntiRecoilPattern TrimLeadingZeroDy(AntiRecoilPattern pattern, float threshold = 0.1f)
        {
            if (pattern?.Samples == null || pattern.Samples.Count == 0)
            {
                // Return a copy of the empty pattern
                return new AntiRecoilPattern
                {
                    Name = pattern?.Name ?? "EmptyTrimmed",
                    CreatedUtc = DateTime.UtcNow,
                    Notes = "Trimmed from empty pattern",
                    Tags = new List<string>(pattern?.Tags ?? new List<string>()),
                    Version = pattern?.Version ?? 1,
                    Samples = new List<AntiRecoilSample>()
                };
            }

            var startIdx = 0;
            for (int i = 0; i < pattern.Samples.Count; i++)
            {
                if (Math.Abs(pattern.Samples[i].Dy) > threshold)
                {
                    startIdx = i;
                    break;
                }
            }

            if (startIdx == 0)
                return ClonePattern(pattern, "Trimmed");

            var trimmedPattern = new AntiRecoilPattern
            {
                Name = $"{pattern.Name}_Trimmed",
                CreatedUtc = DateTime.UtcNow,
                Notes = $"Trimmed from '{pattern.Name}', removed {startIdx} leading samples",
                Tags = new List<string>(pattern.Tags) { "trimmed" },
                Version = Math.Max(1, pattern.Version),
                Samples = pattern.Samples.Skip(startIdx).ToList()
            };

            Logger.Info("Trimmed pattern '{PatternName}', removed {RemovedCount} leading samples", pattern.Name, startIdx);
            return trimmedPattern;
        }

        /// <summary>
        /// Downsample a pattern by keeping every Nth sample
        /// </summary>
        /// <param name="pattern">The pattern to downsample</param>
        /// <param name="factor">Downsampling factor (e.g., 2 = keep every 2nd sample)</param>
        /// <returns>New downsampled pattern</returns>
        public static AntiRecoilPattern Downsample(AntiRecoilPattern pattern, int factor)
        {
            if (pattern?.Samples == null || pattern.Samples.Count == 0)
                throw new ArgumentException("Pattern must have samples", nameof(pattern));

            if (factor < 2)
                throw new ArgumentException("Downsampling factor must be at least 2", nameof(factor));

            var downsampledSamples = new List<AntiRecoilSample>();
            for (int i = 0; i < pattern.Samples.Count; i += factor)
            {
                downsampledSamples.Add(pattern.Samples[i]);
            }

            var downsampledPattern = new AntiRecoilPattern
            {
                Name = $"{pattern.Name}_Downsampled",
                CreatedUtc = DateTime.UtcNow,
                Notes = $"Downsampled from '{pattern.Name}' by factor {factor} ({pattern.Samples.Count} -> {downsampledSamples.Count} samples)",
                Tags = new List<string>(pattern.Tags) { "downsampled" },
                Version = Math.Max(1, pattern.Version),
                Samples = downsampledSamples
            };

            Logger.Info("Downsampled pattern '{PatternName}' by factor {Factor} ({OriginalCount} -> {NewCount} samples)", pattern.Name, factor, pattern.Samples.Count, downsampledSamples.Count);
            return downsampledPattern;
        }

        /// <summary>
        /// Apply Gaussian smoothing to a pattern
        /// </summary>
        /// <param name="pattern">The pattern to smooth</param>
        /// <param name="sigma">Standard deviation for Gaussian kernel</param>
        /// <param name="kernelSize">Size of the Gaussian kernel (odd number)</param>
        /// <returns>New smoothed pattern</returns>
        public static AntiRecoilPattern SmoothGaussian(AntiRecoilPattern pattern, float sigma = 1.0f, int kernelSize = 5)
        {
            if (pattern?.Samples == null || pattern.Samples.Count == 0)
                throw new ArgumentException("Pattern must have samples", nameof(pattern));

            if (sigma <= 0)
                throw new ArgumentException("Sigma must be positive", nameof(sigma));

            if (kernelSize < 3 || kernelSize % 2 == 0)
                throw new ArgumentException("Kernel size must be odd and >= 3", nameof(kernelSize));

            // Generate Gaussian kernel
            var kernel = GenerateGaussianKernel(sigma, kernelSize);

            var smoothedSamples = new List<AntiRecoilSample>();
            var halfKernel = kernelSize / 2;

            for (int i = 0; i < pattern.Samples.Count; i++)
            {
                float weightedDx = 0f, weightedDy = 0f, totalWeight = 0f;

                for (int j = -halfKernel; j <= halfKernel; j++)
                {
                    var sampleIdx = i + j;
                    if (sampleIdx >= 0 && sampleIdx < pattern.Samples.Count)
                    {
                        var weight = kernel[j + halfKernel];
                        weightedDx += pattern.Samples[sampleIdx].Dx * weight;
                        weightedDy += pattern.Samples[sampleIdx].Dy * weight;
                        totalWeight += weight;
                    }
                }

                smoothedSamples.Add(new AntiRecoilSample
                {
                    Dx = weightedDx / totalWeight,
                    Dy = weightedDy / totalWeight
                });
            }

            var smoothedPattern = new AntiRecoilPattern
            {
                Name = $"{pattern.Name}_GaussianSmooth",
                CreatedUtc = DateTime.UtcNow,
                Notes = $"Gaussian smoothed from '{pattern.Name}' (σ={sigma}, kernel={kernelSize})",
                Tags = new List<string>(pattern.Tags) { "gaussian-smoothed" },
                Version = Math.Max(1, pattern.Version),
                Samples = smoothedSamples
            };

            Logger.Info("Applied Gaussian smoothing to pattern '{PatternName}' (σ={Sigma}, kernel={KernelSize})", pattern.Name, sigma, kernelSize);
            return smoothedPattern;
        }

        /// <summary>
        /// Scale pattern by separate X and Y factors
        /// </summary>
        /// <param name="pattern">The pattern to scale</param>
        /// <param name="scaleX">X-axis scaling factor</param>
        /// <param name="scaleY">Y-axis scaling factor</param>
        /// <returns>New scaled pattern</returns>
        public static AntiRecoilPattern Scale(AntiRecoilPattern pattern, float scaleX, float scaleY)
        {
            if (pattern?.Samples == null || pattern.Samples.Count == 0)
                throw new ArgumentException("Pattern must have samples", nameof(pattern));

            var scaledPattern = new AntiRecoilPattern
            {
                Name = $"{pattern.Name}_Scaled",
                CreatedUtc = DateTime.UtcNow,
                Notes = $"Scaled from '{pattern.Name}' (X={scaleX:F2}, Y={scaleY:F2})",
                Tags = new List<string>(pattern.Tags) { "scaled" },
                Version = Math.Max(1, pattern.Version),
                Samples = pattern.Samples.Select(s => new AntiRecoilSample
                {
                    Dx = s.Dx * scaleX,
                    Dy = s.Dy * scaleY
                }).ToList()
            };

            Logger.Info("Scaled pattern '{PatternName}' (X={ScaleX:F2}, Y={ScaleY:F2})", pattern.Name, scaleX, scaleY);
            return scaledPattern;
        }

        /// <summary>
        /// Invert a pattern (flip Y values)
        /// </summary>
        /// <param name="pattern">The pattern to invert</param>
        /// <returns>New inverted pattern</returns>
        public static AntiRecoilPattern Invert(AntiRecoilPattern pattern)
        {
            if (pattern?.Samples == null || pattern.Samples.Count == 0)
                throw new ArgumentException("Pattern must have samples", nameof(pattern));

            var invertedPattern = new AntiRecoilPattern
            {
                Name = $"{pattern.Name}_Inverted",
                CreatedUtc = DateTime.UtcNow,
                Notes = $"Inverted from '{pattern.Name}'",
                Tags = new List<string>(pattern.Tags) { "inverted" },
                Version = Math.Max(1, pattern.Version),
                Samples = pattern.Samples.Select(s => new AntiRecoilSample
                {
                    Dx = s.Dx,
                    Dy = -s.Dy
                }).ToList()
            };

            Logger.Info("Inverted pattern '{PatternName}'", pattern.Name);
            return invertedPattern;
        }

        /// <summary>
        /// Remove outlier samples that exceed threshold
        /// </summary>
        /// <param name="pattern">The pattern to filter</param>
        /// <param name="threshold">Maximum allowed movement magnitude</param>
        /// <returns>New filtered pattern</returns>
        public static AntiRecoilPattern RemoveOutliers(AntiRecoilPattern pattern, float threshold)
        {
            if (pattern?.Samples == null || pattern.Samples.Count == 0)
                throw new ArgumentException("Pattern must have samples", nameof(pattern));

            if (threshold <= 0)
                throw new ArgumentException("Threshold must be positive", nameof(threshold));

            var originalCount = pattern.Samples.Count;
            var filteredSamples = pattern.Samples
                .Where(s => Math.Sqrt(s.Dx * s.Dx + s.Dy * s.Dy) <= threshold)
                .ToList();

            var filteredPattern = new AntiRecoilPattern
            {
                Name = $"{pattern.Name}_Filtered",
                CreatedUtc = DateTime.UtcNow,
                Notes = $"Outliers removed from '{pattern.Name}' (threshold={threshold:F1}, {originalCount - filteredSamples.Count} removed)",
                Tags = new List<string>(pattern.Tags) { "filtered" },
                Version = Math.Max(1, pattern.Version),
                Samples = filteredSamples
            };

            Logger.Info("Removed outliers from pattern '{PatternName}' (threshold={Threshold:F1}, {RemovedCount} removed)", pattern.Name, threshold, originalCount - filteredSamples.Count);
            return filteredPattern;
        }

        /// <summary>
        /// Get pattern statistics
        /// </summary>
        /// <param name="pattern">The pattern to analyze</param>
        /// <returns>Statistics about the pattern</returns>
        public static PatternStatistics GetStatistics(AntiRecoilPattern pattern)
        {
            if (pattern?.Samples == null || pattern.Samples.Count == 0)
                throw new ArgumentException("Pattern must have samples", nameof(pattern));

            var dxValues = pattern.Samples.Select(s => s.Dx).ToArray();
            var dyValues = pattern.Samples.Select(s => s.Dy).ToArray();
            var magnitudes = pattern.Samples.Select(s => (float)Math.Sqrt(s.Dx * s.Dx + s.Dy * s.Dy)).ToArray();

            return new PatternStatistics
            {
                SampleCount = pattern.Samples.Count,

                DxMin = dxValues.Min(),
                DxMax = dxValues.Max(),
                DxMean = dxValues.Average(),
                DxStdDev = (float)CalculateStandardDeviation(dxValues),

                DyMin = dyValues.Min(),
                DyMax = dyValues.Max(),
                DyMean = dyValues.Average(),
                DyStdDev = (float)CalculateStandardDeviation(dyValues),

                MagnitudeMin = magnitudes.Min(),
                MagnitudeMax = magnitudes.Max(),
                MagnitudeMean = magnitudes.Average(),
                MagnitudeStdDev = (float)CalculateStandardDeviation(magnitudes),

                TotalDx = dxValues.Sum(),
                TotalDy = dyValues.Sum(),
                TotalMagnitude = magnitudes.Sum()
            };
        }

        #region Private Helper Methods

        private static AntiRecoilPattern ClonePattern(AntiRecoilPattern pattern, string suffix)
        {
            return new AntiRecoilPattern
            {
                Name = $"{pattern.Name}_{suffix}",
                CreatedUtc = DateTime.UtcNow,
                Notes = $"Cloned from '{pattern.Name}'",
                Tags = new List<string>(pattern.Tags),
                Version = Math.Max(1, pattern.Version),
                Samples = pattern.Samples.Select(s => new AntiRecoilSample { Dx = s.Dx, Dy = s.Dy }).ToList()
            };
        }

        private static float[] GenerateGaussianKernel(float sigma, int size)
        {
            var kernel = new float[size];
            var halfSize = size / 2;
            var twoSigmaSquared = 2 * sigma * sigma;

            for (int i = 0; i < size; i++)
            {
                var x = i - halfSize;
                kernel[i] = (float)(Math.Exp(-(x * x) / twoSigmaSquared) / (Math.Sqrt(2 * Math.PI) * sigma));
            }

            // Normalize kernel
            var sum = kernel.Sum();
            for (int i = 0; i < size; i++)
            {
                kernel[i] /= sum;
            }

            return kernel;
        }

        private static double CalculateStandardDeviation(IEnumerable<float> values)
        {
            var valuesArray = values.ToArray();
            if (valuesArray.Length < 2) return 0;

            var mean = valuesArray.Average();
            var sumOfSquaredDifferences = valuesArray.Sum(v => Math.Pow(v - mean, 2));
            return Math.Sqrt(sumOfSquaredDifferences / (valuesArray.Length - 1));
        }

        #endregion
    }

    /// <summary>
    /// Statistical information about a pattern
    /// </summary>
    public class PatternStatistics
    {
        public int SampleCount { get; set; }

        public float DxMin { get; set; }
        public float DxMax { get; set; }
        public double DxMean { get; set; }
        public double DxStdDev { get; set; }

        public float DyMin { get; set; }
        public float DyMax { get; set; }
        public double DyMean { get; set; }
        public double DyStdDev { get; set; }

        public double MagnitudeMin { get; set; }
        public double MagnitudeMax { get; set; }
        public double MagnitudeMean { get; set; }
        public double MagnitudeStdDev { get; set; }

        public float TotalDx { get; set; }
        public float TotalDy { get; set; }
        public double TotalMagnitude { get; set; }

        public override string ToString()
        {
            return $"Samples: {SampleCount}, " +
                   $"DY: {DyMin:F1}→{DyMax:F1} (μ={DyMean:F1}, σ={DyStdDev:F1}), " +
                   $"DX: {DxMin:F1}→{DxMax:F1} (μ={DxMean:F1}, σ={DxStdDev:F1}), " +
                   $"Total: ({TotalDx:F1}, {TotalDy:F1})";
        }
    }
}