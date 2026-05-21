using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;
using CANDebugTool.Models;

namespace CANDebugTool.Views
{
    /// <summary>
    /// 曲线图表控件 — 使用 DrawingContext 渲染曲线、网格和悬停提示
    /// </summary>
    public class CurveChart : FrameworkElement
    {
        public static readonly DependencyProperty CurvesProperty =
            DependencyProperty.Register(nameof(Curves), typeof(IEnumerable<CurveConfig>), typeof(CurveChart),
                new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

        public IEnumerable<CurveConfig>? Curves
        {
            get => (IEnumerable<CurveConfig>?)GetValue(CurvesProperty);
            set => SetValue(CurvesProperty, value);
        }

        private const double MarginLeft = 50;
        private const double MarginRight = 15;
        private const double MarginTop = 10;
        private const double MarginBottom = 25;
        private const int GridRows = 6;
        private const int GridCols = 8;

        // Hover state
        private string? _hoverText;
        private Point _hoverPos;

        private readonly DispatcherTimer _renderTimer;

        public CurveChart()
        {
            ClipToBounds = true;
            RenderOptions.SetEdgeMode(this, EdgeMode.Aliased);
            _renderTimer = new DispatcherTimer(TimeSpan.FromMilliseconds(33), DispatcherPriority.Render, OnTimerTick, Dispatcher);
        }

        private void OnTimerTick(object? sender, EventArgs e) => Refresh();

        public void Refresh()
        {
            InvalidateVisual();
        }

        protected override void OnMouseMove(System.Windows.Input.MouseEventArgs e)
        {
            base.OnMouseMove(e);
            var pos = e.GetPosition(this);
            var curves = Curves;
            if (curves == null) return;

            double chartW = ActualWidth - MarginLeft - MarginRight;
            double chartH = ActualHeight - MarginTop - MarginBottom;
            if (chartW < 1 || chartH < 1) return;

            var enabledCurves = curves.Where(c => c.Enabled).ToList();
            if (enabledCurves.Count == 0) { _hoverText = null; return; }

            (double yMin, double yMax) = CalcYRange(enabledCurves);

            double bestDist = double.MaxValue;
            string? bestText = null;
            Point bestScreenPt = default;

            foreach (var curve in enabledCurves)
            {
                var pts = curve.DataPoints.ToArray();
                if (pts.Length == 0) continue;

                long tMin = pts[0].Tick;
                long tMax = pts[^1].Tick;
                double tRange = tMax - tMin;
                if (tRange == 0) tRange = 1;

                double yRange = yMax - yMin;
                if (yRange <= 0) yRange = 1;

                for (int i = 0; i < pts.Length; i++)
                {
                    var (tick, val) = pts[i];
                    if (val < curve.LowerLimit || val > curve.UpperLimit) continue;

                    double sx = MarginLeft + ((tick - tMin) / tRange) * chartW;
                    double sy = MarginTop + ((yMax - val) / yRange) * chartH;

                    double dx = sx - pos.X;
                    double dy = sy - pos.Y;
                    double dist = dx * dx + dy * dy;
                    if (dist < bestDist)
                    {
                        bestDist = dist;
                        long timeMs = tick / 10;
                        bestText = $"{curve.Name}\n值: {val:F3}\n时间: {timeMs / 1000}.{timeMs % 1000:D3}s";
                        bestScreenPt = new Point(sx, sy);
                    }
                }
            }

            // Only show tooltip if mouse is reasonably close (within 30px)
            if (bestDist < 900 && bestText != null)
            {
                _hoverText = bestText;
                _hoverPos = bestScreenPt;
            }
            else
            {
                _hoverText = null;
            }
            InvalidateVisual();
        }

        protected override void OnMouseLeave(System.Windows.Input.MouseEventArgs e)
        {
            base.OnMouseLeave(e);
            _hoverText = null;
            InvalidateVisual();
        }

        protected override void OnRender(DrawingContext dc)
        {
            double w = ActualWidth;
            double h = ActualHeight;
            if (w < 1 || h < 1) return;

            // ── 1. Background ──
            dc.DrawRectangle(Brushes.White, null, new Rect(0, 0, w, h));

            double chartW = w - MarginLeft - MarginRight;
            double chartH = h - MarginTop - MarginBottom;
            if (chartW < 1 || chartH < 1) return;

            var curves = Curves;
            var enabledCurves = curves?.Where(c => c.Enabled).ToList() ?? new List<CurveConfig>();

            (double yMin, double yMax) = CalcYRange(enabledCurves);
            double yRange = yMax - yMin;
            if (yRange <= 0) yRange = 1;

            // ── 2. Semi-transparent grid ──
            var gridBrush = new SolidColorBrush(Color.FromArgb(30, 128, 128, 128));
            gridBrush.Freeze();
            var gridPen = new Pen(gridBrush, 0.5);
            gridPen.Freeze();

            var gridLabelBrush = new SolidColorBrush(Color.FromArgb(140, 100, 100, 100));
            gridLabelBrush.Freeze();
            var gridLabelTypeface = new Typeface("Segoe UI");
            const double gridLabelSize = 9;

            // Horizontal grid lines + Y-axis labels
            for (int i = 0; i <= GridRows; i++)
            {
                double y = MarginTop + (chartH * i / GridRows);
                dc.DrawLine(gridPen, new Point(MarginLeft, y), new Point(w - MarginRight, y));

                double val = yMax - (yRange * i / GridRows);
                string label = val.ToString("F1");
                var ft = new FormattedText(label, System.Globalization.CultureInfo.CurrentCulture,
                    FlowDirection.LeftToRight, gridLabelTypeface, gridLabelSize, gridLabelBrush, 1.0);
                dc.DrawText(ft, new Point(MarginLeft - ft.Width - 4, y - ft.Height / 2));
            }

            // Vertical grid lines
            for (int i = 0; i <= GridCols; i++)
            {
                double x = MarginLeft + (chartW * i / GridCols);
                dc.DrawLine(gridPen, new Point(x, MarginTop), new Point(x, h - MarginBottom));
            }

            // ── 3. Draw curves ──
            foreach (var curve in enabledCurves)
            {
                var pts = curve.DataPoints.ToArray();
                if (pts.Length < 2) continue;

                long tMin = pts[0].Tick;
                long tMax = pts[^1].Tick;
                double tRange = tMax - tMin;
                if (tRange == 0) tRange = 1;

                var geom = new StreamGeometry();
                using (var ctx = geom.Open())
                {
                    bool first = true;
                    foreach (var (tick, val) in pts)
                    {
                        if (val < curve.LowerLimit || val > curve.UpperLimit) continue;

                        double x = MarginLeft + ((tick - tMin) / tRange) * chartW;
                        double y = MarginTop + ((yMax - val) / yRange) * chartH;

                        if (first)
                        {
                            ctx.BeginFigure(new Point(x, y), false, false);
                            first = false;
                        }
                        else
                        {
                            ctx.LineTo(new Point(x, y), true, false);
                        }
                    }
                }
                geom.Freeze();

                var curvePen = new Pen(curve.Brush, 1.2);
                curvePen.Freeze();
                dc.DrawGeometry(null, curvePen, geom);
            }

            // ── 4. Axis border ──
            var axisPen = new Pen(Brushes.Gray, 1);
            axisPen.Freeze();
            dc.DrawRectangle(null, axisPen, new Rect(MarginLeft, MarginTop, chartW, chartH));

            // ── 5. Legend (top-left, inside chart) ──
            if (enabledCurves.Count > 0)
            {
                var legendTypeface = new Typeface("Segoe UI");
                const double legendSize = 9;
                double legendY = MarginTop + 4;
                foreach (var curve in enabledCurves)
                {
                    var legendBrush = new SolidColorBrush(curve.Brush.Color);
                    legendBrush.Freeze();
                    var ft = new FormattedText($"─ {curve.Name}", System.Globalization.CultureInfo.CurrentCulture,
                        FlowDirection.LeftToRight, legendTypeface, legendSize, legendBrush, 1.0);
                    dc.DrawText(ft, new Point(MarginLeft + 6, legendY));
                    legendY += ft.Height + 2;
                }
            }

            // ── 6. Hover tooltip ──
            if (_hoverText != null)
            {
                var typeface = new Typeface("Segoe UI");
                var textBrush = new SolidColorBrush(Color.FromRgb(30, 30, 30));
                textBrush.Freeze();

                // Split lines
                var lines = _hoverText.Split('\n');
                double maxTw = 0;
                var formattedLines = new List<FormattedText>();
                foreach (var line in lines)
                {
                    var ft = new FormattedText(line, System.Globalization.CultureInfo.CurrentCulture,
                        FlowDirection.LeftToRight, typeface, 10, textBrush, 1.0);
                    formattedLines.Add(ft);
                    if (ft.Width > maxTw) maxTw = ft.Width;
                }
                double th = formattedLines.Sum(fl => fl.Height);

                double tipX = _hoverPos.X + 10;
                double tipY = _hoverPos.Y - th - 6;
                if (tipX + maxTw + 10 > w) tipX = _hoverPos.X - maxTw - 14;
                if (tipY < 0) tipY = _hoverPos.Y + 10;

                var tipBg = new SolidColorBrush(Color.FromArgb(220, 255, 255, 220));
                tipBg.Freeze();
                var tipBorder = new Pen(new SolidColorBrush(Color.FromRgb(180, 180, 180)), 0.5);
                tipBorder.Freeze();

                var tipRect = new Rect(tipX, tipY, maxTw + 8, th + 6);
                dc.DrawRoundedRectangle(tipBg, tipBorder, tipRect, 3, 3);

                double lineY = tipY + 3;
                foreach (var fl in formattedLines)
                {
                    dc.DrawText(fl, new Point(tipX + 4, lineY));
                    lineY += fl.Height;
                }

                // Crosshair dot
                dc.DrawEllipse(Brushes.Red, null, _hoverPos, 3, 3);
            }
        }

        private static (double yMin, double yMax) CalcYRange(List<CurveConfig> enabledCurves)
        {
            if (enabledCurves.Count == 0) return (0, 100);
            double yMin = enabledCurves.Min(c => c.LowerLimit);
            double yMax = enabledCurves.Max(c => c.UpperLimit);
            if (yMin >= yMax) yMax = yMin + 1;
            // Add 5% padding
            double pad = (yMax - yMin) * 0.05;
            return (yMin - pad, yMax + pad);
        }
    }
}
