using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace WootMouseRemap
{
    public sealed class InputVisualizerControl : Control
    {
        // Backing state
        private short _lx, _ly, _rx, _ry; private byte _lt, _rt;
        private bool _a, _b, _x, _y, _lb, _rb, _back, _start, _l3, _r3, _dup, _ddown, _dleft, _dright;

        public InputVisualizerControl()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint, true);
            DoubleBuffered = true;
            ResizeRedraw = true;
            MinimumSize = new Size(260, 140);
            BackColor = Color.FromArgb(18, 18, 18);
        }

        /// <summary>Batch update of all pad state with a single repaint.</summary>
        public void SetState(
            short lx, short ly, short rx, short ry, byte lt, byte rt,
            bool a, bool b, bool x, bool y, bool lb, bool rb, bool back, bool start, bool l3, bool r3,
            bool dup, bool ddown, bool dleft, bool dright)
        {
            _lx = lx; _ly = ly; _rx = rx; _ry = ry; _lt = lt; _rt = rt;
            _a = a; _b = b; _x = x; _y = y; _lb = lb; _rb = rb; _back = back; _start = start; _l3 = l3; _r3 = r3;
            _dup = dup; _ddown = ddown; _dleft = dleft; _dright = dright;
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(BackColor);

            var r = ClientRectangle;
            if (r.Width < 80 || r.Height < 60) return;

            float pad = Math.Max(6f, Math.Min(r.Width, r.Height) * 0.02f);
            var inner = RectangleF.Inflate(r, -pad, -pad);

            float hTrig = inner.Height * 0.18f;
            float hStick = inner.Height * 0.48f;
            float hButtons = inner.Height - hTrig - hStick;

            var trigRow = new RectangleF(inner.Left, inner.Top, inner.Width, hTrig);
            var stickRow = new RectangleF(inner.Left, inner.Top + hTrig, inner.Width, hStick);
            var btnRow = new RectangleF(inner.Left, inner.Bottom - hButtons, inner.Width, hButtons);

            DrawTriggers(g, trigRow);
            DrawSticks(g, stickRow);
            DrawButtons(g, btnRow);
        }

        private static readonly Color AxisGrid = Color.FromArgb(70, 70, 70);
        private static readonly Color AxisDot = Color.DodgerBlue;
        private static readonly Color AxisRing = Color.FromArgb(110, 110, 110);

        private void DrawTriggers(Graphics g, RectangleF area)
        {
            float gap = area.Width * 0.04f;
            float w = (area.Width - gap) / 2f;
            var ltRect = new RectangleF(area.Left, area.Top, w, area.Height);
            var rtRect = new RectangleF(area.Left + w + gap, area.Top, w, area.Height);
            DrawTrigger(g, ltRect, _lt / 255f, "LT");
            DrawTrigger(g, rtRect, _rt / 255f, "RT");
        }

        private void DrawSticks(Graphics g, RectangleF area)
        {
            float gap = area.Width * 0.04f;
            float w = (area.Width - gap) / 2f;
            var left = new RectangleF(area.Left, area.Top, w, area.Height);
            var right = new RectangleF(area.Left + w + gap, area.Top, w, area.Height);
            DrawStick(g, left, _lx, _ly, "L");
            DrawStick(g, right, _rx, _ry, "R");
        }

        private void DrawStick(Graphics g, RectangleF rect, short x, short y, string label)
        {
            float rad = Math.Min(rect.Width, rect.Height) * 0.42f;
            var center = new PointF(rect.Left + rect.Width / 2f, rect.Top + rect.Height / 2f);

            using var ring = new Pen(AxisRing, Math.Max(1f, rect.Width * 0.01f));
            using var grid = new Pen(AxisGrid, Math.Max(1f, rect.Width * 0.005f));
            using var dotB = new SolidBrush(AxisDot);
            using var textB = new SolidBrush(Color.White);

            g.DrawEllipse(ring, center.X - rad, center.Y - rad, rad * 2, rad * 2);
            g.DrawLine(grid, center.X - rad, center.Y, center.X + rad, center.Y);
            g.DrawLine(grid, center.X, center.Y - rad, center.X, center.Y + rad);

            float nx = x / 32767f;
            float ny = y / 32767f;
            var dot = new PointF(center.X + nx * rad, center.Y - ny * rad);
            float dotR = Math.Max(3f, rad * 0.08f);
            g.FillEllipse(dotB, dot.X - dotR, dot.Y - dotR, dotR * 2, dotR * 2);

            var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Near };
            g.DrawString(label, this.Font, textB, new RectangleF(rect.Left, rect.Top, rect.Width, rect.Height), sf);
        }

        private void DrawTrigger(Graphics g, RectangleF rect, float v01, string label)
        {
            v01 = Math.Clamp(v01, 0f, 1f);
            using var frame = new Pen(AxisRing, Math.Max(1f, rect.Height * 0.06f));
            using var fill = new SolidBrush(Color.DodgerBlue);
            using var textB = new SolidBrush(Color.White);
            g.DrawRectangle(frame, rect.X, rect.Y, rect.Width, rect.Height);
            var fillRect = new RectangleF(rect.X, rect.Y, rect.Width * v01, rect.Height);
            g.FillRectangle(fill, fillRect);
            g.DrawString(label, this.Font, textB, rect.Right + 6, rect.Top - 2);
        }

        private void DrawButtons(Graphics g, RectangleF area)
        {
            using var textB = new SolidBrush(Color.White);
            float pad = Math.Max(6f, Math.Min(area.Width, area.Height) * 0.03f);

            var leftCol = new RectangleF(area.Left, area.Top, area.Width * 0.33f - pad, area.Height);
            var midCol  = new RectangleF(area.Left + area.Width * 0.33f + pad, area.Top, area.Width * 0.34f - pad * 2, area.Height);
            var rightCol= new RectangleF(area.Left + area.Width * 0.67f + pad, area.Top, area.Width * 0.33f - pad, area.Height);

            float d = Math.Min(leftCol.Width, leftCol.Height) * 0.28f;
            var center = new PointF(leftCol.Left + leftCol.Width * 0.45f, leftCol.Top + leftCol.Height * 0.52f);
            DrawDpadDirection(g, new PointF(center.X, center.Y - d), _dup);
            DrawDpadDirection(g, new PointF(center.X + d, center.Y), _dright);
            DrawDpadDirection(g, new PointF(center.X, center.Y + d), _ddown);
            DrawDpadDirection(g, new PointF(center.X - d, center.Y), _dleft);

            float btnW = leftCol.Width * 0.38f;
            float btnH = Math.Max(18f, leftCol.Height * 0.22f);
            DrawKey(g, new RectangleF(leftCol.Left + pad, leftCol.Top + pad, btnW, btnH), "LB", _lb);
            DrawKey(g, new RectangleF(leftCol.Left + pad, leftCol.Top + pad + btnH + pad, btnW, btnH), "L3", _l3);

            float midW = midCol.Width * 0.36f;
            float midH = Math.Max(18f, midCol.Height * 0.22f);
            DrawKey(g, new RectangleF(midCol.Left + pad, midCol.Top + pad, midW, midH), "Back", _back);
            DrawKey(g, new RectangleF(midCol.Left + pad + midW + pad, midCol.Top + pad, midW, midH), "Start", _start);

            float circ = Math.Min(rightCol.Width, rightCol.Height) * 0.22f;
            var aC = new PointF(rightCol.Left + rightCol.Width * 0.60f, rightCol.Top + rightCol.Height * 0.66f);
            var bC = new PointF(aC.X + circ + pad, aC.Y - circ - pad);
            var xC = new PointF(aC.X - circ - pad, aC.Y - circ - pad);
            var yC = new PointF(aC.X, aC.Y - (circ * 2 + pad * 2));
            DrawRound(g, xC, circ, "X", _x);
            DrawRound(g, yC, circ, "Y", _y);
            DrawRound(g, aC, circ, "A", _a);
            DrawRound(g, bC, circ, "B", _b);

            float rbW = rightCol.Width * 0.38f;
            float rbH = Math.Max(18f, rightCol.Height * 0.22f);
            DrawKey(g, new RectangleF(rightCol.Left + pad, rightCol.Top + pad, rbW, rbH), "RB", _rb);
            DrawKey(g, new RectangleF(rightCol.Left + pad, rightCol.Top + pad + rbH + pad, rbW, rbH), "R3", _r3);
        }

        private void DrawDpadDirection(Graphics g, PointF tip, bool on)
        {
            float size = Math.Max(10f, Math.Min(Width, Height) * 0.035f);
            using var b = new SolidBrush(on ? Color.DodgerBlue : Color.DimGray);
            using var p = new Pen(Color.White, 1f);
            var path = new GraphicsPath();
            path.AddPolygon(new[] {
                new PointF(tip.X, tip.Y),
                new PointF(tip.X - size, tip.Y + size),
                new PointF(tip.X, tip.Y + size * 0.6f),
                new PointF(tip.X + size, tip.Y + size)
            });
            g.FillPath(b, path);
            g.DrawPath(p, path);
        }

        private void DrawKey(Graphics g, RectangleF rect, string label, bool down)
        {
            using var fill = new SolidBrush(down ? Color.DodgerBlue : Color.DimGray);
            using var pen  = new Pen(Color.White, 1f);
            using var text = new SolidBrush(Color.White);
            g.FillRectangle(fill, rect);
            g.DrawRectangle(pen, rect.X, rect.Y, rect.Width, rect.Height);
            var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
            g.DrawString(label, this.Font, text, rect, sf);
        }

        private void DrawRound(Graphics g, PointF center, float radius, string label, bool down)
        {
            using var fill = new SolidBrush(down ? Color.DodgerBlue : Color.DimGray);
            using var pen  = new Pen(Color.White, 1f);
            using var text = new SolidBrush(Color.White);
            g.FillEllipse(fill, center.X - radius, center.Y - radius, radius * 2, radius * 2);
            g.DrawEllipse(pen, center.X - radius, center.Y - radius, radius * 2, radius * 2);
            var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
            g.DrawString(label, this.Font, text, new RectangleF(center.X - radius, center.Y - radius, radius * 2, radius * 2), sf);
        }
    }
}
