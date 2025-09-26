
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace WootMouseRemap
{
    public sealed class AutoTuner
    {
        public sealed record Result(float Sens, float Expo, float ADZ, double Score);

        public async Task<Result> RunAsync(CurveProcessor baseline, int seconds, CancellationToken ct)
        {
            var grid = Sweep(
                baseline.Sensitivity, 0.10f, 3,
                baseline.Expo,        0.20f, 3,
                baseline.AntiDeadzone,0.05f, 3
            );

            var fig = new FigureEightDriver();
            Result best = new(baseline.Sensitivity, baseline.Expo, baseline.AntiDeadzone, double.MaxValue);

            foreach (var triple in grid)
            {
                ct.ThrowIfCancellationRequested();
                float s = triple.s;
                float e = triple.e;
                float a = triple.a;

                var curve = new CurveProcessor
                {
                    Sensitivity = s,
                    Expo = e,
                    AntiDeadzone = a,
                    EmaAlpha = baseline.EmaAlpha,
                    VelocityGain = baseline.VelocityGain,
                    JitterFloor = baseline.JitterFloor,
                    ScaleX = baseline.ScaleX,
                    ScaleY = baseline.ScaleY
                };

                int ticks = seconds * 200; // simulate @200Hz
                var lx = new List<short>(ticks);
                var ly = new List<short>(ticks);
                for (int i = 0; i < ticks; i++)
                {
                    double t = i / 100.0;
                    var (dx, dy) = fig.At(t);
                    var (sx, sy) = curve.ToStick(dx, dy);
                    lx.Add(sx); ly.Add(sy);
                    await Task.Delay(5, ct);
                }

                double score = Score(lx, ly);
                if (score < best.Score) best = new(s, e, a, score);
            }

            return best;
        }

        private static IEnumerable<(float s, float e, float a)> Sweep(
            float s0, float ds, int n,
            float e0, float de, int m,
            float a0, float da, int k)
        {
            for (int i = -n; i <= n; i++)
                for (int j = -m; j <= m; j++)
                    for (int r = -k; r <= k; r++)
                        yield return (
                            Math.Max(0.05f, s0 + i * ds),
                            Math.Clamp(e0 + j * de, 0f, 1f),
                            Math.Clamp(a0 + r * da, 0f, 0.3f)
                        );
        }

        // Lower is better: jerk + center dwell + radius error
        private static double Score(List<short> x, List<short> y)
        {
            double jerk = 0;
            for (int i = 3; i < x.Count; i++)
            {
                int jx = x[i] - 3 * x[i - 1] + 3 * x[i - 2] - x[i - 3];
                int jy = y[i] - 3 * y[i - 1] + 3 * y[i - 2] - y[i - 3];
                jerk += Math.Abs(jx) + Math.Abs(jy);
            }
            double dwell = 0;
            for (int i = 0; i < x.Count; i++)
                if (Math.Abs(x[i]) < 1500 && Math.Abs(y[i]) < 1500) dwell++;

            double maxR = 0, sumR = 0;
            for (int i = 0; i < x.Count; i++)
            {
                double r = Math.Sqrt((double)x[i] * x[i] + (double)y[i] * y[i]);
                sumR += r; if (r > maxR) maxR = r;
            }
            double avgR = sumR / x.Count;
            double radiusErr = Math.Abs(maxR - 28000) / 28000.0 + Math.Abs(avgR - 18000) / 18000.0;

            return jerk * 1e-3 + (dwell / x.Count) * 1.5 + radiusErr * 2.0;
        }
    }
}
