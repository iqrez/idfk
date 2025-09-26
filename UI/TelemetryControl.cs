using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Windows.Forms;
using WootMouseRemap.Features;

namespace WootMouseRemap.UI
{
    /// <summary>
    /// Real-time telemetry display control for anti-recoil monitoring
    /// </summary>
    public class TelemetryControl : Control
    {
        private readonly Queue<TelemetryDataPoint> _dataPoints = new();
        private readonly object _dataLock = new();
        private int _maxDataPoints = 300; // 30 seconds at 10Hz
        private float _maxDisplayRange = 50.0f;
        private bool _autoScale = true;
        private TelemetryDisplayMode _displayMode = TelemetryDisplayMode.VerticalMovements;

        // Colors
        private readonly Color _backgroundColor = Color.Black;
        private readonly Color _gridColor = Color.FromArgb(30, 30, 30);
        private readonly Color _axisColor = Color.FromArgb(60, 60, 60);
        private readonly Color _verticalMovementColor = Color.Cyan;
        private readonly Color _compensationColor = Color.Orange;
        private readonly Color _accumulatedColor = Color.Yellow;
        private readonly Color _thresholdColor = Color.Red;

        public event EventHandler<TelemetryDataPoint>? DataPointAdded;

        #region Properties

        public int MaxDataPoints
        {
            get => _maxDataPoints;
            set
            {
                _maxDataPoints = Math.Max(10, value);
                TrimDataPoints();
                Invalidate();
            }
        }

        public float MaxDisplayRange
        {
            get => _maxDisplayRange;
            set
            {
                _maxDisplayRange = Math.Max(1f, value);
                Invalidate();
            }
        }

        public bool AutoScale
        {
            get => _autoScale;
            set
            {
                _autoScale = value;
                Invalidate();
            }
        }

        public TelemetryDisplayMode DisplayMode
        {
            get => _displayMode;
            set
            {
                _displayMode = value;
                Invalidate();
            }
        }

        #endregion

        public TelemetryControl()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint |
                     ControlStyles.DoubleBuffer | ControlStyles.ResizeRedraw, true);

            BackColor = _backgroundColor;
        }

        public void AddDataPoint(float verticalMovement, float compensation, float accumulated, bool isActive, float threshold)
        {
            var dataPoint = new TelemetryDataPoint
            {
                Timestamp = DateTime.UtcNow,
                VerticalMovement = verticalMovement,
                Compensation = compensation,
                AccumulatedCompensation = accumulated,
                IsActive = isActive,
                Threshold = threshold
            };

            lock (_dataLock)
            {
                _dataPoints.Enqueue(dataPoint);
                TrimDataPoints();
            }

            DataPointAdded?.Invoke(this, dataPoint);

            if (InvokeRequired)
                BeginInvoke(new Action(Invalidate));
            else
                Invalidate();
        }

        public void Clear()
        {
            lock (_dataLock)
            {
                _dataPoints.Clear();
            }
            Invalidate();
        }

        public TelemetryStatistics GetStatistics()
        {
            lock (_dataLock)
            {
                if (_dataPoints.Count == 0)
                    return new TelemetryStatistics();

                var points = _dataPoints.ToArray();
                var activePoints = points.Where(p => p.IsActive).ToArray();

                return new TelemetryStatistics
                {
                    TotalPoints = points.Length,
                    ActivePoints = activePoints.Length,
                    TimeSpan = points.Length > 1 ? points.Last().Timestamp - points.First().Timestamp : TimeSpan.Zero,

                    MaxVerticalMovement = points.Max(p => Math.Abs(p.VerticalMovement)),
                    AvgVerticalMovement = points.Average(p => Math.Abs(p.VerticalMovement)),

                    MaxCompensation = points.Max(p => p.Compensation),
                    AvgCompensation = activePoints.Length > 0 ? activePoints.Average(p => p.Compensation) : 0f,
                    TotalCompensation = points.Sum(p => p.Compensation),

                    MaxAccumulated = points.Max(p => p.AccumulatedCompensation),
                    CurrentAccumulated = points.LastOrDefault()?.AccumulatedCompensation ?? 0f
                };
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(_backgroundColor);

            TelemetryDataPoint[] dataPoints;
            lock (_dataLock)
            {
                if (_dataPoints.Count == 0)
                {
                    DrawEmptyMessage(g);
                    return;
                }
                dataPoints = _dataPoints.ToArray();
            }

            var drawBounds = GetDrawBounds();
            if (drawBounds.Width <= 0 || drawBounds.Height <= 0) return;

            DrawGrid(g, drawBounds);
            DrawAxes(g, drawBounds);

            switch (_displayMode)
            {
                case TelemetryDisplayMode.VerticalMovements:
                    DrawVerticalMovements(g, drawBounds, dataPoints);
                    break;
                case TelemetryDisplayMode.Compensation:
                    DrawCompensation(g, drawBounds, dataPoints);
                    break;
                case TelemetryDisplayMode.AccumulatedCompensation:
                    DrawAccumulatedCompensation(g, drawBounds, dataPoints);
                    break;
                case TelemetryDisplayMode.All:
                    DrawVerticalMovements(g, drawBounds, dataPoints);
                    DrawCompensation(g, drawBounds, dataPoints);
                    DrawAccumulatedCompensation(g, drawBounds, dataPoints);
                    break;
            }

            DrawThreshold(g, drawBounds, dataPoints);
            DrawActiveRegions(g, drawBounds, dataPoints);
            DrawLegend(g);
            DrawStatistics(g);
        }

        private Rectangle GetDrawBounds()
        {
            const int margin = 40;
            return new Rectangle(margin, margin, Width - 2 * margin, Height - 2 * margin);
        }

        private void DrawEmptyMessage(Graphics g)
        {
            using var brush = new SolidBrush(Color.Gray);
            using var font = new Font(Font.FontFamily, 12f);
            var message = "No telemetry data";
            var size = g.MeasureString(message, font);
            var location = new PointF((Width - size.Width) / 2, (Height - size.Height) / 2);
            g.DrawString(message, font, brush, location);
        }

        private void DrawGrid(Graphics g, Rectangle bounds)
        {
            using var gridPen = new Pen(_gridColor);

            // Vertical grid lines (time)
            var timeStep = bounds.Width / 10f;
            for (int i = 0; i <= 10; i++)
            {
                var x = bounds.Left + i * timeStep;
                g.DrawLine(gridPen, x, bounds.Top, x, bounds.Bottom);
            }

            // Horizontal grid lines (value)
            var valueStep = bounds.Height / 8f;
            for (int i = 0; i <= 8; i++)
            {
                var y = bounds.Top + i * valueStep;
                g.DrawLine(gridPen, bounds.Left, y, bounds.Right, y);
            }
        }

        private void DrawAxes(Graphics g, Rectangle bounds)
        {
            using var axisPen = new Pen(_axisColor, 1f);

            // Zero line (center)
            var centerY = bounds.Top + bounds.Height / 2;
            g.DrawLine(axisPen, bounds.Left, centerY, bounds.Right, centerY);

            // Labels
            using var brush = new SolidBrush(Color.LightGray);
            using var font = new Font(Font.FontFamily, 8f);
            g.DrawString("Time →", font, brush, bounds.Right - 50, centerY + 5);
            g.DrawString("Value ↑", font, brush, bounds.Left - 35, bounds.Top + 5);
        }

        private void DrawVerticalMovements(Graphics g, Rectangle bounds, TelemetryDataPoint[] dataPoints)
        {
            if (dataPoints.Length < 2) return;

            using var pen = new Pen(_verticalMovementColor, 1.5f);
            var points = CreateScaledPoints(bounds, dataPoints, p => p.VerticalMovement);

            for (int i = 1; i < points.Length; i++)
            {
                g.DrawLine(pen, points[i - 1], points[i]);
            }
        }

        private void DrawCompensation(Graphics g, Rectangle bounds, TelemetryDataPoint[] dataPoints)
        {
            if (dataPoints.Length < 2) return;

            using var pen = new Pen(_compensationColor, 2f);
            var points = CreateScaledPoints(bounds, dataPoints, p => p.Compensation);

            for (int i = 1; i < points.Length; i++)
            {
                g.DrawLine(pen, points[i - 1], points[i]);
            }
        }

        private void DrawAccumulatedCompensation(Graphics g, Rectangle bounds, TelemetryDataPoint[] dataPoints)
        {
            if (dataPoints.Length < 2) return;

            using var pen = new Pen(_accumulatedColor, 1.5f);
            var points = CreateScaledPoints(bounds, dataPoints, p => p.AccumulatedCompensation);

            for (int i = 1; i < points.Length; i++)
            {
                g.DrawLine(pen, points[i - 1], points[i]);
            }
        }

        private void DrawThreshold(Graphics g, Rectangle bounds, TelemetryDataPoint[] dataPoints)
        {
            if (dataPoints.Length == 0) return;

            var threshold = dataPoints.Last().Threshold;
            if (threshold <= 0) return;

            var maxRange = GetMaxRange(dataPoints);
            var scaleY = bounds.Height / 2 / maxRange;
            var centerY = bounds.Top + bounds.Height / 2;
            var y = centerY - threshold * scaleY;

            using var pen = new Pen(_thresholdColor, 1f) { DashStyle = DashStyle.Dash };
            g.DrawLine(pen, bounds.Left, y, bounds.Right, y);

            // Label
            using var brush = new SolidBrush(_thresholdColor);
            using var font = new Font(Font.FontFamily, 8f);
            g.DrawString($"Threshold: {threshold:F1}", font, brush, bounds.Left + 5, y - 15);
        }

        private void DrawActiveRegions(Graphics g, Rectangle bounds, TelemetryDataPoint[] dataPoints)
        {
            if (dataPoints.Length < 2) return;

            using var brush = new SolidBrush(Color.FromArgb(30, Color.Green));
            var scaleX = bounds.Width / (float)(dataPoints.Length - 1);

            for (int i = 0; i < dataPoints.Length - 1; i++)
            {
                if (dataPoints[i].IsActive)
                {
                    var x1 = bounds.Left + i * scaleX;
                    var x2 = bounds.Left + (i + 1) * scaleX;
                    var rect = new RectangleF(x1, bounds.Top, x2 - x1, bounds.Height);
                    g.FillRectangle(brush, rect);
                }
            }
        }

        private PointF[] CreateScaledPoints(Rectangle bounds, TelemetryDataPoint[] dataPoints, Func<TelemetryDataPoint, float> valueSelector)
        {
            var points = new PointF[dataPoints.Length];
            var maxRange = GetMaxRange(dataPoints);
            var scaleX = bounds.Width / (float)(dataPoints.Length - 1);
            var scaleY = bounds.Height / 2 / maxRange;
            var centerY = bounds.Top + bounds.Height / 2;

            for (int i = 0; i < dataPoints.Length; i++)
            {
                var x = bounds.Left + i * scaleX;
                var value = valueSelector(dataPoints[i]);
                var y = centerY - value * scaleY;
                points[i] = new PointF(x, y);
            }

            return points;
        }

        private float GetMaxRange(TelemetryDataPoint[] dataPoints)
        {
            if (!_autoScale) return _maxDisplayRange;

            var maxValue = 1f;
            switch (_displayMode)
            {
                case TelemetryDisplayMode.VerticalMovements:
                    maxValue = dataPoints.Max(p => Math.Abs(p.VerticalMovement));
                    break;
                case TelemetryDisplayMode.Compensation:
                    maxValue = dataPoints.Max(p => p.Compensation);
                    break;
                case TelemetryDisplayMode.AccumulatedCompensation:
                    maxValue = dataPoints.Max(p => p.AccumulatedCompensation);
                    break;
                case TelemetryDisplayMode.All:
                    maxValue = Math.Max(
                        dataPoints.Max(p => Math.Abs(p.VerticalMovement)),
                        Math.Max(
                            dataPoints.Max(p => p.Compensation),
                            dataPoints.Max(p => p.AccumulatedCompensation)
                        )
                    );
                    break;
            }

            return Math.Max(1f, maxValue * 1.1f); // Add 10% padding
        }

        private void DrawLegend(Graphics g)
        {
            const int legendX = 10;
            const int legendY = 10;
            const int lineLength = 20;
            const int lineHeight = 16;
            int currentY = legendY;

            using var font = new Font(Font.FontFamily, 8f);

            if (_displayMode == TelemetryDisplayMode.VerticalMovements || _displayMode == TelemetryDisplayMode.All)
            {
                using var pen = new Pen(_verticalMovementColor, 1.5f);
                using var brush = new SolidBrush(_verticalMovementColor);
                g.DrawLine(pen, legendX, currentY + 6, legendX + lineLength, currentY + 6);
                g.DrawString("Vertical Movement", font, brush, legendX + lineLength + 5, currentY);
                currentY += lineHeight;
            }

            if (_displayMode == TelemetryDisplayMode.Compensation || _displayMode == TelemetryDisplayMode.All)
            {
                using var pen = new Pen(_compensationColor, 2f);
                using var brush = new SolidBrush(_compensationColor);
                g.DrawLine(pen, legendX, currentY + 6, legendX + lineLength, currentY + 6);
                g.DrawString("Compensation", font, brush, legendX + lineLength + 5, currentY);
                currentY += lineHeight;
            }

            if (_displayMode == TelemetryDisplayMode.AccumulatedCompensation || _displayMode == TelemetryDisplayMode.All)
            {
                using var pen = new Pen(_accumulatedColor, 1.5f);
                using var brush = new SolidBrush(_accumulatedColor);
                g.DrawLine(pen, legendX, currentY + 6, legendX + lineLength, currentY + 6);
                g.DrawString("Accumulated", font, brush, legendX + lineLength + 5, currentY);
            }
        }

        private void DrawStatistics(Graphics g)
        {
            var stats = GetStatistics();
            if (stats.TotalPoints == 0) return;

            var info = $"Points: {stats.TotalPoints} | Active: {stats.ActivePoints} | " +
                      $"Max V: {stats.MaxVerticalMovement:F1} | " +
                      $"Avg C: {stats.AvgCompensation:F1} | " +
                      $"Current A: {stats.CurrentAccumulated:F1}";

            using var brush = new SolidBrush(Color.LightGray);
            using var font = new Font(Font.FontFamily, 8f);
            var textSize = g.MeasureString(info, font);
            g.DrawString(info, font, brush, Width - textSize.Width - 10, Height - textSize.Height - 5);
        }

        private void TrimDataPoints()
        {
            while (_dataPoints.Count > _maxDataPoints)
            {
                _dataPoints.Dequeue();
            }
        }
    }

    public enum TelemetryDisplayMode
    {
        VerticalMovements,
        Compensation,
        AccumulatedCompensation,
        All
    }

    public class TelemetryDataPoint
    {
        public DateTime Timestamp { get; set; }
        public float VerticalMovement { get; set; }
        public float Compensation { get; set; }
        public float AccumulatedCompensation { get; set; }
        public bool IsActive { get; set; }
        public float Threshold { get; set; }
    }

    public class TelemetryStatistics
    {
        public int TotalPoints { get; set; }
        public int ActivePoints { get; set; }
        public TimeSpan TimeSpan { get; set; }
        public float MaxVerticalMovement { get; set; }
        public float AvgVerticalMovement { get; set; }
        public float MaxCompensation { get; set; }
        public float AvgCompensation { get; set; }
        public float TotalCompensation { get; set; }
        public float MaxAccumulated { get; set; }
        public float CurrentAccumulated { get; set; }
    }
}