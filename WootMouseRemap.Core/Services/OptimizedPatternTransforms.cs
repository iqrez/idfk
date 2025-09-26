using System;
using WootMouseRemap.Features;

namespace WootMouseRemap.Core.Services;

/// <summary>
/// Optimized pattern transformations using Span&lt;T&gt; and Memory&lt;T&gt; for better performance
/// </summary>
public static class OptimizedPatternTransforms
{
    /// <summary>
    /// Apply moving average smoothing using Span for better performance
    /// </summary>
    public static AntiRecoilPattern SmoothMovingAverageOptimized(AntiRecoilPattern pattern, int windowSize)
    {
        if (pattern?.Samples == null || pattern.Samples.Count == 0)
            throw new ArgumentException("Pattern must have samples", nameof(pattern));

        if (windowSize < 1)
            throw new ArgumentException("Window size must be at least 1", nameof(windowSize));

        var samples = pattern.Samples;
        var smoothedSamples = new AntiRecoilSample[samples.Count];
        
        // Use stackalloc for small windows, heap allocation for larger ones
        Span<float> dxWindow = windowSize <= 64 ? stackalloc float[windowSize] : new float[windowSize];
        Span<float> dyWindow = windowSize <= 64 ? stackalloc float[windowSize] : new float[windowSize];
        
        var halfWindow = windowSize / 2;
        
        for (int i = 0; i < samples.Count; i++)
        {
            var startIdx = Math.Max(0, i - halfWindow);
            var endIdx = Math.Min(samples.Count - 1, i + halfWindow);
            var actualWindowSize = endIdx - startIdx + 1;
            
            // Fill windows
            for (int j = 0; j < actualWindowSize; j++)
            {
                var sampleIdx = startIdx + j;
                dxWindow[j] = samples[sampleIdx].Dx;
                dyWindow[j] = samples[sampleIdx].Dy;
            }
            
            // Calculate averages
            var dxSum = 0f;
            var dySum = 0f;
            var windowSpan = dxWindow[..actualWindowSize];
            var dyWindowSpan = dyWindow[..actualWindowSize];
            
            foreach (var dx in windowSpan) dxSum += dx;
            foreach (var dy in dyWindowSpan) dySum += dy;
            
            smoothedSamples[i] = new AntiRecoilSample
            {
                Dx = dxSum / actualWindowSize,
                Dy = dySum / actualWindowSize
            };
        }

        return new AntiRecoilPattern
        {
            Name = $"{pattern.Name}_OptSmoothed",
            CreatedUtc = DateTime.UtcNow,
            Notes = $"Optimized smoothed from '{pattern.Name}' with {windowSize}-sample moving average",
            Tags = new List<string>(pattern.Tags) { "opt-smoothed" },
            Version = Math.Max(1, pattern.Version),
            Samples = smoothedSamples.ToList()
        };
    }
    
    /// <summary>
    /// Downsample pattern using optimized memory access patterns
    /// </summary>
    public static AntiRecoilPattern DownsampleOptimized(AntiRecoilPattern pattern, int factor)
    {
        if (pattern?.Samples == null || pattern.Samples.Count == 0)
            throw new ArgumentException("Pattern must have samples", nameof(pattern));

        if (factor < 2)
            throw new ArgumentException("Downsampling factor must be at least 2", nameof(factor));

        var samples = pattern.Samples;
        var outputSize = (samples.Count + factor - 1) / factor; // Ceiling division
        var downsampledSamples = new AntiRecoilSample[outputSize];
        
        var outputIndex = 0;
        for (int i = 0; i < samples.Count && outputIndex < outputSize; i += factor)
        {
            downsampledSamples[outputIndex++] = samples[i];
        }

        return new AntiRecoilPattern
        {
            Name = $"{pattern.Name}_OptDownsampled",
            CreatedUtc = DateTime.UtcNow,
            Notes = $"Optimized downsampled from '{pattern.Name}' by factor {factor} ({samples.Count} -> {outputIndex} samples)",
            Tags = new List<string>(pattern.Tags) { "opt-downsampled" },
            Version = Math.Max(1, pattern.Version),
            Samples = downsampledSamples[..outputIndex].ToArray().ToList()
        };
    }
    
    /// <summary>
    /// Calculate pattern statistics using optimized memory access
    /// </summary>
    public static PatternStatistics GetStatisticsOptimized(AntiRecoilPattern pattern)
    {
        if (pattern?.Samples == null || pattern.Samples.Count == 0)
            throw new ArgumentException("Pattern must have samples", nameof(pattern));

        var samples = pattern.Samples;
        var count = samples.Count;
        
        // Single pass calculation for better cache performance
        var dxMin = float.MaxValue;
        var dxMax = float.MinValue;
        var dyMin = float.MaxValue;
        var dyMax = float.MinValue;
        var magnitudeMin = float.MaxValue;
        var magnitudeMax = float.MinValue;
        
        var dxSum = 0.0;
        var dySum = 0.0;
        var magnitudeSum = 0.0;
        var dxSumSquares = 0.0;
        var dySumSquares = 0.0;
        var magnitudeSumSquares = 0.0;
        
        foreach (var sample in samples)
        {
            var dx = sample.Dx;
            var dy = sample.Dy;
            var magnitude = MathF.Sqrt(dx * dx + dy * dy);
            
            // Min/Max
            if (dx < dxMin) dxMin = dx;
            if (dx > dxMax) dxMax = dx;
            if (dy < dyMin) dyMin = dy;
            if (dy > dyMax) dyMax = dy;
            if (magnitude < magnitudeMin) magnitudeMin = magnitude;
            if (magnitude > magnitudeMax) magnitudeMax = magnitude;
            
            // Sums for mean and variance
            dxSum += dx;
            dySum += dy;
            magnitudeSum += magnitude;
            dxSumSquares += dx * dx;
            dySumSquares += dy * dy;
            magnitudeSumSquares += magnitude * magnitude;
        }
        
        var dxMean = dxSum / count;
        var dyMean = dySum / count;
        var magnitudeMean = magnitudeSum / count;
        
        // Calculate standard deviations using the computational formula
        var dxVariance = (dxSumSquares / count) - (dxMean * dxMean);
        var dyVariance = (dySumSquares / count) - (dyMean * dyMean);
        var magnitudeVariance = (magnitudeSumSquares / count) - (magnitudeMean * magnitudeMean);
        
        return new PatternStatistics
        {
            SampleCount = count,
            DxMin = dxMin,
            DxMax = dxMax,
            DxMean = dxMean,
            DxStdDev = Math.Sqrt(Math.Max(0, dxVariance)),
            DyMin = dyMin,
            DyMax = dyMax,
            DyMean = dyMean,
            DyStdDev = Math.Sqrt(Math.Max(0, dyVariance)),
            MagnitudeMin = magnitudeMin,
            MagnitudeMax = magnitudeMax,
            MagnitudeMean = magnitudeMean,
            MagnitudeStdDev = Math.Sqrt(Math.Max(0, magnitudeVariance)),
            TotalDx = (float)dxSum,
            TotalDy = (float)dySum,
            TotalMagnitude = magnitudeSum
        };
    }
}