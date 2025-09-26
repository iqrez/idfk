using System;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using WootMouseRemap.Core;
using WootMouseRemap.Features;

namespace WootMouseRemap.Tests
{
    /// <summary>
    /// Unit tests for PatternTransforms utility functions
    /// </summary>
    [TestClass]
    public class PatternTransformTests
    {
        private AntiRecoilPattern _testPattern = null!;

        [TestInitialize]
        public void Setup()
        {
            _testPattern = new AntiRecoilPattern
            {
                Name = "TestPattern",
                Samples = new List<AntiRecoilSample>
                {
                    new AntiRecoilSample { Dx = 0, Dy = 10 },
                    new AntiRecoilSample { Dx = 2, Dy = 8 },
                    new AntiRecoilSample { Dx = -1, Dy = 12 },
                    new AntiRecoilSample { Dx = 1, Dy = 6 },
                    new AntiRecoilSample { Dx = 0, Dy = 4 },
                    new AntiRecoilSample { Dx = -2, Dy = 2 }
                }
            };
        }

        [TestMethod]
        public void PatternTransforms_Normalize_ShouldScaleToTargetPeak()
        {
            var targetPeak = 20.0f;
            var originalPeak = _testPattern.Samples.Max(s => Math.Abs(s.Dy));

            var normalized = PatternTransforms.Normalize(_testPattern, targetPeak);

            var newPeak = normalized.Samples.Max(s => Math.Abs(s.Dy));
            Assert.AreEqual(targetPeak, newPeak, 0.01f, "Peak should match target");

            // Verify scaling is proportional
            var expectedScale = targetPeak / originalPeak;
            var firstSample = normalized.Samples.First();
            var expectedDy = _testPattern.Samples.First().Dy * expectedScale;
            Assert.AreEqual(expectedDy, firstSample.Dy, 0.01f, "Scaling should be proportional");
        }

        [TestMethod]
        public void PatternTransforms_Normalize_WithZeroPeak_ShouldReturnOriginal()
        {
            var zeroPattern = new AntiRecoilPattern
            {
                Name = "ZeroPattern",
                Samples = new List<AntiRecoilSample>
                {
                    new AntiRecoilSample { Dx = 0, Dy = 0 }
                }
            };

            var normalized = PatternTransforms.Normalize(zeroPattern, 10.0f);

            Assert.AreEqual(0, normalized.Samples.First().Dy, "Zero pattern should remain zero");
        }

        [TestMethod]
        public void PatternTransforms_SmoothMovingAverage_ShouldReduceNoise()
        {
            var smoothed = PatternTransforms.SmoothMovingAverage(_testPattern, 3);

            Assert.AreEqual(_testPattern.Samples.Count, smoothed.Samples.Count, "Sample count should be preserved");

            // Check that the smoothed pattern has less variation
            var originalVariation = CalculateVariation(_testPattern.Samples.Select(s => s.Dy));
            var smoothedVariation = CalculateVariation(smoothed.Samples.Select(s => s.Dy));

            Assert.IsTrue(smoothedVariation <= originalVariation, "Smoothed pattern should have less variation");
        }

        [TestMethod]
        public void PatternTransforms_SmoothGaussian_ShouldPreserveSampleCount()
        {
            var smoothed = PatternTransforms.SmoothGaussian(_testPattern, 1.0f, 3);

            Assert.AreEqual(_testPattern.Samples.Count, smoothed.Samples.Count, "Sample count should be preserved");
            Assert.AreEqual(_testPattern.Name + "_GaussianSmooth", smoothed.Name, "Name should be updated");
        }

        [TestMethod]
        public void PatternTransforms_Trim_ShouldRemoveLeadingAndTrailingZeros()
        {
            var patternWithZeros = new AntiRecoilPattern
            {
                Name = "PatternWithZeros",
                Samples = new List<AntiRecoilSample>
                {
                    new AntiRecoilSample { Dx = 0, Dy = 0 },
                    new AntiRecoilSample { Dx = 0, Dy = 0 },
                    new AntiRecoilSample { Dx = 2, Dy = 8 },
                    new AntiRecoilSample { Dx = 1, Dy = 6 },
                    new AntiRecoilSample { Dx = 0, Dy = 0 },
                    new AntiRecoilSample { Dx = 0, Dy = 0 }
                }
            };

            var trimmed = PatternTransforms.TrimLeadingZeroDy(patternWithZeros, 1.0f);

            Assert.AreEqual(4, trimmed.Samples.Count, "Should keep samples after first non-zero");
            Assert.AreEqual(8, trimmed.Samples.First().Dy, "First sample should be first non-zero");
            Assert.AreEqual(0, trimmed.Samples.Last().Dy, "Last sample should be last in pattern");
        }

        [TestMethod]
        public void PatternTransforms_Downsample_ShouldReduceSampleCount()
        {
            var downsampled = PatternTransforms.Downsample(_testPattern, 2);

            Assert.AreEqual(3, downsampled.Samples.Count, "Should have half the samples (rounded up)");
            Assert.AreEqual(_testPattern.Samples[0].Dy, downsampled.Samples[0].Dy, "First sample should be preserved");
            Assert.AreEqual(_testPattern.Samples[2].Dy, downsampled.Samples[1].Dy, "Should skip samples according to factor");
        }

        [TestMethod]
        public void PatternTransforms_Scale_ShouldScaleXAndYIndependently()
        {
            var scaleX = 2.0f;
            var scaleY = 0.5f;

            var scaled = PatternTransforms.Scale(_testPattern, scaleX, scaleY);

            var firstOriginal = _testPattern.Samples.First();
            var firstScaled = scaled.Samples.First();

            Assert.AreEqual(firstOriginal.Dx * scaleX, firstScaled.Dx, 0.01f, "X should be scaled correctly");
            Assert.AreEqual(firstOriginal.Dy * scaleY, firstScaled.Dy, 0.01f, "Y should be scaled correctly");
        }

        [TestMethod]
        public void PatternTransforms_Invert_ShouldNegateYValues()
        {
            var inverted = PatternTransforms.Invert(_testPattern);

            for (int i = 0; i < _testPattern.Samples.Count; i++)
            {
                var original = _testPattern.Samples[i];
                var invertedSample = inverted.Samples[i];

                Assert.AreEqual(original.Dx, invertedSample.Dx, "X values should not change");
                Assert.AreEqual(-original.Dy, invertedSample.Dy, "Y values should be negated");
            }
        }

        [TestMethod]
        public void PatternTransforms_RemoveOutliers_ShouldFilterExtremeValues()
        {
            var patternWithOutliers = new AntiRecoilPattern
            {
                Name = "PatternWithOutliers",
                Samples = new List<AntiRecoilSample>
                {
                    new AntiRecoilSample { Dx = 0, Dy = 5 },
                    new AntiRecoilSample { Dx = 1, Dy = 6 },
                    new AntiRecoilSample { Dx = 2, Dy = 50 }, // Outlier
                    new AntiRecoilSample { Dx = 0, Dy = 4 },
                    new AntiRecoilSample { Dx = -1, Dy = 5 }
                }
            };

            var filtered = PatternTransforms.RemoveOutliers(patternWithOutliers, 2.0f);

            Assert.IsTrue(filtered.Samples.Count < patternWithOutliers.Samples.Count, "Should remove outlier samples");
            Assert.IsFalse(filtered.Samples.Any(s => Math.Abs(s.Dy) > 20), "Should not contain extreme outliers");
        }

        [TestMethod]
        public void PatternTransforms_Statistics_ShouldCalculateCorrectMetrics()
        {
            var stats = PatternTransforms.GetStatistics(_testPattern);

            Assert.AreEqual(_testPattern.Samples.Count, stats.SampleCount, "Sample count should match");
            Assert.AreEqual(12, stats.DyMax, "Max Y should be correct");
            Assert.AreEqual(2, stats.DyMin, "Min Y should be correct");

            var expectedAvgDy = _testPattern.Samples.Average(s => s.Dy);
            Assert.AreEqual(expectedAvgDy, stats.DyMean, 0.01f, "Average Y should be correct");
        }

        [TestMethod]
        public void PatternTransforms_WithEmptyPattern_ShouldHandleGracefully()
        {
            var emptyPattern = new AntiRecoilPattern
            {
                Name = "EmptyPattern",
                Samples = new List<AntiRecoilSample>()
            };

            var normalized = PatternTransforms.Normalize(emptyPattern, 10.0f);
            Assert.AreEqual(0, normalized.Samples.Count, "Empty pattern should remain empty");

            var smoothed = PatternTransforms.SmoothMovingAverage(emptyPattern, 3);
            Assert.AreEqual(0, smoothed.Samples.Count, "Empty pattern should remain empty");

            var trimmed = PatternTransforms.TrimLeadingZeroDy(emptyPattern, 1.0f);
            Assert.AreEqual(0, trimmed.Samples.Count, "Empty pattern should remain empty");
        }

        [TestMethod]
        public void PatternTransforms_WithSingleSample_ShouldHandleGracefully()
        {
            var singleSamplePattern = new AntiRecoilPattern
            {
                Name = "SingleSample",
                Samples = new List<AntiRecoilSample>
                {
                    new AntiRecoilSample { Dx = 5, Dy = 10 }
                }
            };

            var normalized = PatternTransforms.Normalize(singleSamplePattern, 20.0f);
            Assert.AreEqual(1, normalized.Samples.Count, "Single sample should be preserved");
            Assert.AreEqual(20.0f, normalized.Samples.First().Dy, "Single sample should be scaled correctly");

            var smoothed = PatternTransforms.SmoothMovingAverage(singleSamplePattern, 3);
            Assert.AreEqual(1, smoothed.Samples.Count, "Single sample should be preserved");

            var downsampled = PatternTransforms.Downsample(singleSamplePattern, 2);
            Assert.AreEqual(1, downsampled.Samples.Count, "Single sample should be preserved");
        }

        private static float CalculateVariation(IEnumerable<float> values)
        {
            var array = values.ToArray();
            if (array.Length < 2) return 0;

            var mean = array.Average();
            return array.Sum(v => (v - mean) * (v - mean)) / array.Length;
        }
    }
}
