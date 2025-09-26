using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Windows.Forms;
using WootMouseRemap.Features;

namespace WootMouseRemap.UI
{
    /// <summary>
    /// Custom control for visualizing anti-recoil patterns with multiple display modes
    /// </summary>
    public class PatternGraphControl : Control
    {
        private AntiRecoilPattern? _pattern;
        private AntiRecoilSimulationResult? _simulationResult;
        private GraphDisplayMode _displayMode = GraphDisplayMode.InputAndOutput;
        private int _highlightSampleIndex = -1;
        private float _zoomLevel = 1.0f;
        private PointF _panOffset = PointF.Empty;
        private bool _isPanning;
        private Point _lastMousePos;
        private bool _showGrid = true;
        private bool _showCrosshair = true;

        // Colors
        private readonly Color _backgroundColor = Color.Black;
        private readonly Color _gridColor = Color.FromArgb(40, 40, 40);
        private readonly Color _axisColor = Color.FromArgb(80, 80, 80);
        private readonly Color _inputColor = Color.Red;
        private readonly Color _outputColor = Color.Lime;
        private readonly Color _compensationColor = Color.Orange;
        private readonly Color _highlightColor = Color.Yellow;
        private readonly Color _crosshairColor = Color.Gray;

        public event EventHandler<SampleHoverEventArgs>? SampleHovered;
        public event EventHandler<SampleClickEventArgs>? SampleClicked;

        #region Properties

        public AntiRecoilPattern? Pattern
        {
            get => _pattern;
            set
            {
                _pattern = value;
                _simulationResult = null; // Clear simulation when pattern changes
                Invalidate();
            }
        }

        public AntiRecoilSimulationResult? SimulationResult
        {
            get => _simulationResult;
            set
            {
                _simulationResult = value;
                Invalidate();
            }
        }

        public GraphDisplayMode DisplayMode
        {
            get => _displayMode;
            set
            {
                _displayMode = value;
                Invalidate();
            }
        }

        public int HighlightSampleIndex
        {
            get => _highlightSampleIndex;
            set
            {
                _highlightSampleIndex = value;
                Invalidate();
            }
        }

        public float ZoomLevel
        {
            get => _zoomLevel;
            set
            {
                _zoomLevel = Math.Max(0.1f, Math.Min(10f, value));
                Invalidate();
            }
        }

        public bool ShowGrid
        {
            get => _showGrid;
            set
            {
                _showGrid = value;
                Invalidate();
            }
        }

        public bool ShowCrosshair
        {
            get => _showCrosshair;
            set
            {
                _showCrosshair = value;
                Invalidate();
            }
        }

        #endregion

        public PatternGraphControl()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint |
                     ControlStyles.DoubleBuffer | ControlStyles.ResizeRedraw, true);

            BackColor = _backgroundColor;

            // Enable mouse events
            MouseWheel += PatternGraphControl_MouseWheel;
            MouseDown += PatternGraphControl_MouseDown;
            MouseMove += PatternGraphControl_MouseMove;
            MouseUp += PatternGraphControl_MouseUp;
            MouseClick += PatternGraphControl_MouseClick;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(_backgroundColor);

            if (_pattern?.Samples == null || _pattern.Samples.Count == 0)
            {
                DrawEmptyMessage(g);
                return;
            }

            // Calculate drawing bounds
            var drawingBounds = GetDrawingBounds();
            if (drawingBounds.Width <= 0 || drawingBounds.Height <= 0) return;

            // Apply transformations
            g.TranslateTransform(_panOffset.X, _panOffset.Y);
            g.ScaleTransform(_zoomLevel, _zoomLevel);

            // Draw components
            if (_showGrid) DrawGrid(g, drawingBounds);
            DrawAxes(g, drawingBounds);
            DrawPattern(g, drawingBounds);
            if (_showCrosshair) DrawCrosshair(g, drawingBounds);
            DrawHighlight(g, drawingBounds);

            // Reset transformations for overlay drawing
            g.ResetTransform();
            DrawLegend(g);
            DrawInfo(g);
        }

        private Rectangle GetDrawingBounds()
        {
            const int margin = 40;
            return new Rectangle(margin, margin, Width - 2 * margin, Height - 2 * margin);
        }

        private void DrawEmptyMessage(Graphics g)
        {
            using var brush = new SolidBrush(Color.Gray);
            using var font = new Font(Font.FontFamily, 12f);
            var message = _pattern == null ? "No pattern loaded" : "Pattern has no samples";
            var size = g.MeasureString(message, font);
            var location = new PointF((Width - size.Width) / 2, (Height - size.Height) / 2);
            g.DrawString(message, font, brush, location);
        }

        private void DrawGrid(Graphics g, Rectangle bounds)
        {
            using var gridPen = new Pen(_gridColor);

            // Vertical grid lines
            for (int x = bounds.Left; x <= bounds.Right; x += 25)
            {
                g.DrawLine(gridPen, x, bounds.Top, x, bounds.Bottom);
            }

            // Horizontal grid lines
            for (int y = bounds.Top; y <= bounds.Bottom; y += 25)
            {
                g.DrawLine(gridPen, bounds.Left, y, bounds.Right, y);
            }
        }

        private void DrawAxes(Graphics g, Rectangle bounds)
        {
            using var axisPen = new Pen(_axisColor, 1f);

            // Center axes
            int centerX = bounds.Left + bounds.Width / 2;
            int centerY = bounds.Top + bounds.Height / 2;

            g.DrawLine(axisPen, bounds.Left, centerY, bounds.Right, centerY); // Horizontal axis
            g.DrawLine(axisPen, centerX, bounds.Top, centerX, bounds.Bottom);  // Vertical axis

            // Axis labels
            using var brush = new SolidBrush(Color.LightGray);
            using var font = new Font(Font.FontFamily, 8f);
            g.DrawString("Time →", font, brush, bounds.Right - 50, centerY + 5);
            g.DrawString("↑", font, brush, centerX - 10, bounds.Top + 5);
            g.DrawString("Movement", font, brush, centerX - 25, bounds.Top + 15);
        }

        private void DrawPattern(Graphics g, Rectangle bounds)
        {
            if (_pattern?.Samples == null || _pattern.Samples.Count < 2) return;

            var samples = _pattern.Samples;
            var simPoints = _simulationResult?.Points;

            // Calculate scaling
            var maxY = Math.Max(1f, samples.Max(s => Math.Max(Math.Abs(s.Dy), Math.Abs(s.Dx))));
            if (simPoints != null)
            {
                maxY = Math.Max(maxY, simPoints.Max(p => Math.Max(Math.Abs(p.InputDy), Math.Max(Math.Abs(p.OutputDy), Math.Abs(p.InputDx)))));
            }

            var scaleX = bounds.Width / (float)(samples.Count - 1);
            var scaleY = bounds.Height / 2 / maxY;
            var centerY = bounds.Top + bounds.Height / 2;

            // Draw based on display mode
            switch (_displayMode)
            {
                case GraphDisplayMode.InputOnly:
                    DrawInputPattern(g, bounds, samples, scaleX, scaleY, centerY);
                    break;
                case GraphDisplayMode.OutputOnly:
                    DrawOutputPattern(g, bounds, simPoints, scaleX, scaleY, centerY);
                    break;
                case GraphDisplayMode.InputAndOutput:
                    DrawInputPattern(g, bounds, samples, scaleX, scaleY, centerY);
                    DrawOutputPattern(g, bounds, simPoints, scaleX, scaleY, centerY);
                    break;
                case GraphDisplayMode.CompensationOnly:
                    DrawCompensationPattern(g, bounds, simPoints, scaleX, scaleY, centerY);
                    break;
                case GraphDisplayMode.All:
                    DrawInputPattern(g, bounds, samples, scaleX, scaleY, centerY);
                    DrawOutputPattern(g, bounds, simPoints, scaleX, scaleY, centerY);
                    DrawCompensationPattern(g, bounds, simPoints, scaleX, scaleY, centerY);
                    break;
            }
        }

        private void DrawInputPattern(Graphics g, Rectangle bounds, System.Collections.Generic.List<AntiRecoilSample> samples, float scaleX, float scaleY, int centerY)
        {
            using var inputPen = new Pen(_inputColor, 2f);

            for (int i = 1; i < samples.Count; i++)
            {
                var x1 = bounds.Left + (i - 1) * scaleX;
                var y1 = centerY - samples[i - 1].Dy * scaleY;
                var x2 = bounds.Left + i * scaleX;
                var y2 = centerY - samples[i].Dy * scaleY;

                g.DrawLine(inputPen, x1, y1, x2, y2);
            }
        }

        private void DrawOutputPattern(Graphics g, Rectangle bounds, System.Collections.Generic.List<AntiRecoilSimPoint>? simPoints, float scaleX, float scaleY, int centerY)
        {
            if (simPoints == null || simPoints.Count < 2) return;

            using var outputPen = new Pen(_outputColor, 2f);

            for (int i = 1; i < simPoints.Count; i++)
            {
                var x1 = bounds.Left + (i - 1) * scaleX;
                var y1 = centerY - simPoints[i - 1].OutputDy * scaleY;
                var x2 = bounds.Left + i * scaleX;
                var y2 = centerY - simPoints[i].OutputDy * scaleY;

                g.DrawLine(outputPen, x1, y1, x2, y2);
            }
        }

        private void DrawCompensationPattern(Graphics g, Rectangle bounds, System.Collections.Generic.List<AntiRecoilSimPoint>? simPoints, float scaleX, float scaleY, int centerY)
        {
            if (simPoints == null || simPoints.Count < 2) return;

            using var compPen = new Pen(_compensationColor, 1.5f);

            for (int i = 1; i < simPoints.Count; i++)
            {
                var x1 = bounds.Left + (i - 1) * scaleX;
                var y1 = centerY + simPoints[i - 1].CompensationY * scaleY; // Note: inverted for visual clarity
                var x2 = bounds.Left + i * scaleX;
                var y2 = centerY + simPoints[i].CompensationY * scaleY;

                g.DrawLine(compPen, x1, y1, x2, y2);
            }
        }

        private void DrawCrosshair(Graphics g, Rectangle bounds)
        {
            if (!bounds.Contains(PointToClient(Cursor.Position))) return;

            var mousePos = PointToClient(Cursor.Position);
            using var crosshairPen = new Pen(_crosshairColor, 1f) { DashStyle = DashStyle.Dot };

            g.DrawLine(crosshairPen, bounds.Left, mousePos.Y, bounds.Right, mousePos.Y);
            g.DrawLine(crosshairPen, mousePos.X, bounds.Top, mousePos.X, bounds.Bottom);
        }

        private void DrawHighlight(Graphics g, Rectangle bounds)
        {
            if (_highlightSampleIndex < 0 || _pattern?.Samples == null || _highlightSampleIndex >= _pattern.Samples.Count) return;

            var scaleX = bounds.Width / (float)(_pattern.Samples.Count - 1);
            var x = bounds.Left + _highlightSampleIndex * scaleX;

            using var highlightPen = new Pen(_highlightColor, 3f);
            g.DrawLine(highlightPen, x, bounds.Top, x, bounds.Bottom);

            // Draw sample point
            var maxY = Math.Max(1f, _pattern.Samples.Max(s => Math.Max(Math.Abs(s.Dy), Math.Abs(s.Dx))));
            var scaleY = bounds.Height / 2 / maxY;
            var centerY = bounds.Top + bounds.Height / 2;
            var sample = _pattern.Samples[_highlightSampleIndex];
            var y = centerY - sample.Dy * scaleY;

            using var highlightBrush = new SolidBrush(_highlightColor);
            g.FillEllipse(highlightBrush, x - 3, y - 3, 6, 6);
        }

        private void DrawLegend(Graphics g)
        {
            const int legendX = 10;
            const int legendY = 10;
            const int lineLength = 20;
            const int lineHeight = 18;
            int currentY = legendY;

            using var font = new Font(Font.FontFamily, 8f);

            if (_displayMode == GraphDisplayMode.InputOnly || _displayMode == GraphDisplayMode.InputAndOutput || _displayMode == GraphDisplayMode.All)
            {
                using var inputPen = new Pen(_inputColor, 2f);
                using var brush = new SolidBrush(_inputColor);
                g.DrawLine(inputPen, legendX, currentY + 6, legendX + lineLength, currentY + 6);
                g.DrawString("Input", font, brush, legendX + lineLength + 5, currentY);
                currentY += lineHeight;
            }

            if ((_displayMode == GraphDisplayMode.OutputOnly || _displayMode == GraphDisplayMode.InputAndOutput || _displayMode == GraphDisplayMode.All) && _simulationResult != null)
            {
                using var outputPen = new Pen(_outputColor, 2f);
                using var brush = new SolidBrush(_outputColor);
                g.DrawLine(outputPen, legendX, currentY + 6, legendX + lineLength, currentY + 6);
                g.DrawString("Output", font, brush, legendX + lineLength + 5, currentY);
                currentY += lineHeight;
            }

            if ((_displayMode == GraphDisplayMode.CompensationOnly || _displayMode == GraphDisplayMode.All) && _simulationResult != null)
            {
                using var compPen = new Pen(_compensationColor, 1.5f);
                using var brush = new SolidBrush(_compensationColor);
                g.DrawLine(compPen, legendX, currentY + 6, legendX + lineLength, currentY + 6);
                g.DrawString("Compensation", font, brush, legendX + lineLength + 5, currentY);
            }
        }

        private void DrawInfo(Graphics g)
        {
            if (_pattern == null) return;

            var info = $"Samples: {_pattern.Samples.Count} | Zoom: {_zoomLevel:P0}";
            if (_highlightSampleIndex >= 0 && _highlightSampleIndex < _pattern.Samples.Count)
            {
                var sample = _pattern.Samples[_highlightSampleIndex];
                info += $" | [{_highlightSampleIndex}]: ({sample.Dx:F1}, {sample.Dy:F1})";
            }

            using var brush = new SolidBrush(Color.LightGray);
            using var font = new Font(Font.FontFamily, 8f);
            var textSize = g.MeasureString(info, font);
            g.DrawString(info, font, brush, Width - textSize.Width - 10, Height - textSize.Height - 5);
        }

        #region Mouse Event Handlers

        private void PatternGraphControl_MouseWheel(object? sender, MouseEventArgs e)
        {
            var zoomFactor = e.Delta > 0 ? 1.1f : 0.9f;
            ZoomLevel *= zoomFactor;
        }

        private void PatternGraphControl_MouseDown(object? sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Middle || (e.Button == MouseButtons.Left && ModifierKeys == Keys.Control))
            {
                _isPanning = true;
                _lastMousePos = e.Location;
                Cursor = Cursors.Hand;
            }
        }

        private void PatternGraphControl_MouseMove(object? sender, MouseEventArgs e)
        {
            if (_isPanning)
            {
                var deltaX = e.X - _lastMousePos.X;
                var deltaY = e.Y - _lastMousePos.Y;
                _panOffset = new PointF(_panOffset.X + deltaX, _panOffset.Y + deltaY);
                _lastMousePos = e.Location;
                Invalidate();
            }
            else if (_pattern?.Samples != null)
            {
                // Calculate which sample is under the cursor
                var bounds = GetDrawingBounds();
                var relativeX = e.X - bounds.Left - _panOffset.X;
                var scaleX = bounds.Width / (float)(_pattern.Samples.Count - 1) * _zoomLevel;
                var sampleIndex = (int)Math.Round(relativeX / scaleX);

                if (sampleIndex >= 0 && sampleIndex < _pattern.Samples.Count)
                {
                    var sample = _pattern.Samples[sampleIndex];
                    SampleHovered?.Invoke(this, new SampleHoverEventArgs(sampleIndex, sample, e.Location));
                }

                Invalidate(); // For crosshair update
            }
        }

        private void PatternGraphControl_MouseUp(object? sender, MouseEventArgs e)
        {
            if (_isPanning)
            {
                _isPanning = false;
                Cursor = Cursors.Default;
            }
        }

        private void PatternGraphControl_MouseClick(object? sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left && !_isPanning && _pattern?.Samples != null)
            {
                var bounds = GetDrawingBounds();
                var relativeX = e.X - bounds.Left - _panOffset.X;
                var scaleX = bounds.Width / (float)(_pattern.Samples.Count - 1) * _zoomLevel;
                var sampleIndex = (int)Math.Round(relativeX / scaleX);

                if (sampleIndex >= 0 && sampleIndex < _pattern.Samples.Count)
                {
                    var sample = _pattern.Samples[sampleIndex];
                    SampleClicked?.Invoke(this, new SampleClickEventArgs(sampleIndex, sample, e.Location));
                    HighlightSampleIndex = sampleIndex;
                }
            }
        }

        #endregion

        public void ResetView()
        {
            _zoomLevel = 1.0f;
            _panOffset = PointF.Empty;
            _highlightSampleIndex = -1;
            Invalidate();
        }

        public void FitToWindow()
        {
            if (_pattern?.Samples == null || _pattern.Samples.Count == 0) return;

            _panOffset = PointF.Empty;
            _zoomLevel = 1.0f;
            Invalidate();
        }
    }

    public enum GraphDisplayMode
    {
        InputOnly,
        OutputOnly,
        InputAndOutput,
        CompensationOnly,
        All
    }

    public class SampleHoverEventArgs : EventArgs
    {
        public int SampleIndex { get; }
        public AntiRecoilSample Sample { get; }
        public Point MouseLocation { get; }

        public SampleHoverEventArgs(int sampleIndex, AntiRecoilSample sample, Point mouseLocation)
        {
            SampleIndex = sampleIndex;
            Sample = sample;
            MouseLocation = mouseLocation;
        }
    }

    public class SampleClickEventArgs : EventArgs
    {
        public int SampleIndex { get; }
        public AntiRecoilSample Sample { get; }
        public Point MouseLocation { get; }

        public SampleClickEventArgs(int sampleIndex, AntiRecoilSample sample, Point mouseLocation)
        {
            SampleIndex = sampleIndex;
            Sample = sample;
            MouseLocation = mouseLocation;
        }
    }
}