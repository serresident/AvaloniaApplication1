using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Media;

namespace AvaloniaApplication1.Controls.Charting
{
    public class TrendLineControl : Control
    {
        private IEnumerable? _values;
        private IEnumerable<ITrendPenRenderData>? _pens;

        private static readonly Pen DarkGridPen = new Pen(
            new SolidColorBrush(Color.FromArgb(25, 255, 255, 255)), 
            1, 
            new DashStyle(new double[] { 4, 4 }, 0));
        private static readonly Pen LightGridPen = new Pen(
            new SolidColorBrush(Color.FromArgb(40, 0, 0, 0)), 
            1, 
            new DashStyle(new double[] { 4, 4 }, 0));

        private static readonly Pen DefaultLinePen = new Pen(new SolidColorBrush(Color.FromRgb(0, 255, 204)), 2);

        // Cached threshold and crosshair pens
        private static readonly Pen SetpointPen = new Pen(new SolidColorBrush(Color.FromRgb(0, 230, 118)), 1.5, new DashStyle(new double[] { 6, 3 }, 0));
        private static readonly Pen WarningPen = new Pen(new SolidColorBrush(Color.FromRgb(255, 214, 0)), 1.5, new DashStyle(new double[] { 4, 4 }, 0));
        private static readonly Pen AlarmPen = new Pen(new SolidColorBrush(Color.FromRgb(255, 23, 68)), 1.8, new DashStyle(new double[] { 4, 3 }, 0));
        private static readonly Pen DarkCrosshairPen = new Pen(new SolidColorBrush(Color.FromArgb(220, 255, 255, 255)), 1.2, new DashStyle(new double[] { 3, 3 }, 0));
        private static readonly Pen LightCrosshairPen = new Pen(new SolidColorBrush(Color.FromArgb(220, 20, 20, 20)), 1.2, new DashStyle(new double[] { 3, 3 }, 0));

        private readonly Dictionary<(string ColorHex, double Thickness, string Style), Pen> _penCache = new();
        private readonly Dictionary<string, IBrush> _brushCache = new();
        private readonly List<double> _legacyValList = new();

        #region Avalonia Properties

        public static readonly DirectProperty<TrendLineControl, IEnumerable?> ValuesProperty =
            AvaloniaProperty.RegisterDirect<TrendLineControl, IEnumerable?>(
                nameof(Values),
                o => o.Values,
                (o, v) => o.Values = v);

        public static readonly DirectProperty<TrendLineControl, IEnumerable<ITrendPenRenderData>?> PensProperty =
            AvaloniaProperty.RegisterDirect<TrendLineControl, IEnumerable<ITrendPenRenderData>?>(
                nameof(Pens),
                o => o.Pens,
                (o, v) => o.Pens = v);

        public static readonly StyledProperty<long> SampleTickProperty =
            AvaloniaProperty.Register<TrendLineControl, long>(nameof(SampleTick), 0L);

        public static readonly StyledProperty<string> ScalingModeProperty =
            AvaloniaProperty.Register<TrendLineControl, string>(nameof(ScalingMode), "DualAxis");

        public static readonly StyledProperty<double> MinYProperty =
            AvaloniaProperty.Register<TrendLineControl, double>(nameof(MinY), 0.0);

        public static readonly StyledProperty<double> MaxYProperty =
            AvaloniaProperty.Register<TrendLineControl, double>(nameof(MaxY), 100.0);

        public static readonly StyledProperty<double> RightMinYProperty =
            AvaloniaProperty.Register<TrendLineControl, double>(nameof(RightMinY), 0.0);

        public static readonly StyledProperty<double> RightMaxYProperty =
            AvaloniaProperty.Register<TrendLineControl, double>(nameof(RightMaxY), 100.0);

        public static readonly StyledProperty<IBrush?> LineBrushProperty =
            AvaloniaProperty.Register<TrendLineControl, IBrush?>(nameof(LineBrush));

        public static readonly StyledProperty<IBrush?> AreaBrushProperty =
            AvaloniaProperty.Register<TrendLineControl, IBrush?>(nameof(AreaBrush));

        public static readonly StyledProperty<bool> ShowSetpointProperty =
            AvaloniaProperty.Register<TrendLineControl, bool>(nameof(ShowSetpoint), false);

        public static readonly StyledProperty<double> SetpointValueProperty =
            AvaloniaProperty.Register<TrendLineControl, double>(nameof(SetpointValue), 50.0);

        public static readonly StyledProperty<bool> ShowWarningLimitsProperty =
            AvaloniaProperty.Register<TrendLineControl, bool>(nameof(ShowWarningLimits), false);

        public static readonly StyledProperty<double> WarningHighProperty =
            AvaloniaProperty.Register<TrendLineControl, double>(nameof(WarningHigh), 80.0);

        public static readonly StyledProperty<double> WarningLowProperty =
            AvaloniaProperty.Register<TrendLineControl, double>(nameof(WarningLow), 20.0);

        public static readonly StyledProperty<bool> ShowAlarmLimitsProperty =
            AvaloniaProperty.Register<TrendLineControl, bool>(nameof(ShowAlarmLimits), false);

        public static readonly StyledProperty<double> AlarmHighProperty =
            AvaloniaProperty.Register<TrendLineControl, double>(nameof(AlarmHigh), 90.0);

        public static readonly StyledProperty<double> AlarmLowProperty =
            AvaloniaProperty.Register<TrendLineControl, double>(nameof(AlarmLow), 10.0);

        public static readonly StyledProperty<bool> EnableCrosshairProperty =
            AvaloniaProperty.Register<TrendLineControl, bool>(nameof(EnableCrosshair), true);

        public static readonly StyledProperty<double> CrosshairXProperty =
            AvaloniaProperty.Register<TrendLineControl, double>(nameof(CrosshairX), -1.0, defaultBindingMode: BindingMode.TwoWay);

        public static readonly StyledProperty<int> CrosshairIndexProperty =
            AvaloniaProperty.Register<TrendLineControl, int>(nameof(CrosshairIndex), -1, defaultBindingMode: BindingMode.TwoWay);

        public static readonly StyledProperty<bool> IsCrosshairActiveProperty =
            AvaloniaProperty.Register<TrendLineControl, bool>(nameof(IsCrosshairActive), false, defaultBindingMode: BindingMode.TwoWay);

        #endregion

        #region Properties Getters/Setters

        public IEnumerable? Values
        {
            get => _values;
            set
            {
                var old = _values;
                if (SetAndRaise(ValuesProperty, ref _values, value))
                {
                    OnValuesChanged(old, value);
                }
            }
        }

        public IEnumerable<ITrendPenRenderData>? Pens
        {
            get => _pens;
            set
            {
                var old = _pens;
                if (SetAndRaise(PensProperty, ref _pens, value))
                {
                    OnPensChanged(old, value);
                }
            }
        }

        public double MinY
        {
            get => GetValue(MinYProperty);
            set => SetValue(MinYProperty, value);
        }

        public double MaxY
        {
            get => GetValue(MaxYProperty);
            set => SetValue(MaxYProperty, value);
        }

        public IBrush? LineBrush
        {
            get => GetValue(LineBrushProperty);
            set => SetValue(LineBrushProperty, value);
        }

        public IBrush? AreaBrush
        {
            get => GetValue(AreaBrushProperty);
            set => SetValue(AreaBrushProperty, value);
        }

        public bool ShowSetpoint
        {
            get => GetValue(ShowSetpointProperty);
            set => SetValue(ShowSetpointProperty, value);
        }

        public double SetpointValue
        {
            get => GetValue(SetpointValueProperty);
            set => SetValue(SetpointValueProperty, value);
        }

        public bool ShowWarningLimits
        {
            get => GetValue(ShowWarningLimitsProperty);
            set => SetValue(ShowWarningLimitsProperty, value);
        }

        public double WarningHigh
        {
            get => GetValue(WarningHighProperty);
            set => SetValue(WarningHighProperty, value);
        }

        public double WarningLow
        {
            get => GetValue(WarningLowProperty);
            set => SetValue(WarningLowProperty, value);
        }

        public bool ShowAlarmLimits
        {
            get => GetValue(ShowAlarmLimitsProperty);
            set => SetValue(ShowAlarmLimitsProperty, value);
        }

        public double AlarmHigh
        {
            get => GetValue(AlarmHighProperty);
            set => SetValue(AlarmHighProperty, value);
        }

        public double AlarmLow
        {
            get => GetValue(AlarmLowProperty);
            set => SetValue(AlarmLowProperty, value);
        }

        public bool EnableCrosshair
        {
            get => GetValue(EnableCrosshairProperty);
            set => SetValue(EnableCrosshairProperty, value);
        }

        public double CrosshairX
        {
            get => GetValue(CrosshairXProperty);
            set => SetValue(CrosshairXProperty, value);
        }

        public int CrosshairIndex
        {
            get => GetValue(CrosshairIndexProperty);
            set => SetValue(CrosshairIndexProperty, value);
        }

        public long SampleTick
        {
            get => GetValue(SampleTickProperty);
            set => SetValue(SampleTickProperty, value);
        }

        public string ScalingMode
        {
            get => GetValue(ScalingModeProperty);
            set => SetValue(ScalingModeProperty, value);
        }

        public double RightMinY
        {
            get => GetValue(RightMinYProperty);
            set => SetValue(RightMinYProperty, value);
        }

        public double RightMaxY
        {
            get => GetValue(RightMaxYProperty);
            set => SetValue(RightMaxYProperty, value);
        }

        public bool IsCrosshairActive
        {
            get => GetValue(IsCrosshairActiveProperty);
            set => SetValue(IsCrosshairActiveProperty, value);
        }

        #endregion

        static TrendLineControl()
        {
            AffectsRender<TrendLineControl>(
                ValuesProperty,
                PensProperty,
                SampleTickProperty,
                ScalingModeProperty,
                MinYProperty,
                MaxYProperty,
                RightMinYProperty,
                RightMaxYProperty,
                LineBrushProperty,
                AreaBrushProperty,
                ShowSetpointProperty,
                SetpointValueProperty,
                ShowWarningLimitsProperty,
                WarningHighProperty,
                WarningLowProperty,
                ShowAlarmLimitsProperty,
                AlarmHighProperty,
                AlarmLowProperty,
                EnableCrosshairProperty,
                CrosshairXProperty,
                CrosshairIndexProperty,
                IsCrosshairActiveProperty);
        }

        public TrendLineControl()
        {
            ClipToBounds = true;
        }

        private void OnValuesChanged(IEnumerable? oldValue, IEnumerable? newValue)
        {
            if (oldValue is INotifyCollectionChanged oldCol)
                oldCol.CollectionChanged -= OnCollectionChanged;
            if (newValue is INotifyCollectionChanged newCol)
                newCol.CollectionChanged += OnCollectionChanged;
        }

        private void OnPensChanged(IEnumerable<ITrendPenRenderData>? oldValue, IEnumerable<ITrendPenRenderData>? newValue)
        {
            if (oldValue is INotifyCollectionChanged oldCol)
                oldCol.CollectionChanged -= OnCollectionChanged;
            if (newValue is INotifyCollectionChanged newCol)
                newCol.CollectionChanged += OnCollectionChanged;
        }

        private void OnCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            InvalidateVisual();
        }

        protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
        {
            base.OnAttachedToVisualTree(e);
            ActualThemeVariantChanged += OnActualThemeVariantChanged;
        }

        protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
        {
            base.OnDetachedFromVisualTree(e);
            ActualThemeVariantChanged -= OnActualThemeVariantChanged;
        }

        private void OnActualThemeVariantChanged(object? sender, EventArgs e)
        {
            InvalidateVisual();
        }

        private Pen GetOrCreatePen(string colorHex, double thickness, string strokeStyle)
        {
            var key = (colorHex ?? "#00FFCC", thickness <= 0 ? 1.0 : thickness, strokeStyle ?? "Solid");
            if (!_penCache.TryGetValue(key, out var pen))
            {
                IBrush brush;
                try
                {
                    brush = new SolidColorBrush(Color.Parse(key.Item1));
                }
                catch
                {
                    brush = Brushes.Aqua;
                }

                DashStyle? dash = key.Item3?.ToUpperInvariant() switch
                {
                    "DASH" => new DashStyle(new double[] { 4, 3 }, 0),
                    "DOT" => new DashStyle(new double[] { 1.5, 2 }, 0),
                    _ => null
                };

                pen = new Pen(brush, key.Item2, dash);
                _penCache[key] = pen;
            }
            return pen;
        }

        private IBrush GetOrCreateBrush(string colorHex)
        {
            var hex = colorHex ?? "#00FFCC";
            if (!_brushCache.TryGetValue(hex, out var brush))
            {
                try
                {
                    brush = new SolidColorBrush(Color.Parse(hex));
                }
                catch
                {
                    brush = Brushes.Aqua;
                }
                _brushCache[hex] = brush;
            }
            return brush;
        }

        #region Pointer Interaction & Crosshair

        protected override void OnPointerEntered(PointerEventArgs e)
        {
            base.OnPointerEntered(e);
            if (EnableCrosshair)
            {
                UpdateCrosshair(e.GetPosition(this));
            }
        }

        protected override void OnPointerMoved(PointerEventArgs e)
        {
            base.OnPointerMoved(e);
            if (EnableCrosshair)
            {
                UpdateCrosshair(e.GetPosition(this));
            }
        }

        protected override void OnPointerExited(PointerEventArgs e)
        {
            base.OnPointerExited(e);
            if (EnableCrosshair)
            {
                IsCrosshairActive = false;
                CrosshairIndex = -1;
                CrosshairX = -1;
                InvalidateVisual();
            }
        }

        protected override void OnPointerPressed(PointerPressedEventArgs e)
        {
            base.OnPointerPressed(e);
            if (EnableCrosshair)
            {
                UpdateCrosshair(e.GetPosition(this));
            }
        }

        private void UpdateCrosshair(Point pos)
        {
            double w = Bounds.Width;
            if (w <= 0) return;

            double clampedX = Math.Clamp(pos.X, 0, w);
            CrosshairX = clampedX;
            IsCrosshairActive = true;

            int maxPoints = GetMaxPointsCount();
            if (maxPoints > 1)
            {
                double ratio = clampedX / w;
                int index = (int)Math.Round(ratio * (maxPoints - 1));
                CrosshairIndex = Math.Clamp(index, 0, maxPoints - 1);
            }
            else
            {
                CrosshairIndex = -1;
            }

            InvalidateVisual();
        }

        private int GetMaxPointsCount()
        {
            int max = 0;
            if (_pens != null)
            {
                foreach (var p in _pens)
                {
                    if (p.IsVisible && p.Values != null && p.Values.Count > max)
                        max = p.Values.Count;
                }
            }

            if (max == 0 && _values != null)
            {
                if (_values is ICollection col)
                {
                    max = col.Count;
                }
                else
                {
                    int count = 0;
                    foreach (var _ in _values) count++;
                    max = count;
                }
            }

            return max;
        }

        #endregion

        #region Render

        public override void Render(DrawingContext context)
        {
            base.Render(context);

            var bounds = Bounds;
            double w = bounds.Width;
            double h = bounds.Height;
            if (w <= 0 || h <= 0) return;

            // 1. Draw Grid Lines (Zero Allocations)
            bool isLight = Application.Current?.ActualThemeVariant == Avalonia.Styling.ThemeVariant.Light;
            var gridPen = isLight ? LightGridPen : DarkGridPen;

            // 4 Horizontal grid lines
            for (int i = 1; i < 4; i++)
            {
                double y = (h / 4.0) * i;
                context.DrawLine(gridPen, new Point(0, y), new Point(w, y));
            }

            // 6 Vertical grid lines
            for (int i = 1; i < 6; i++)
            {
                double x = (w / 6.0) * i;
                context.DrawLine(gridPen, new Point(x, 0), new Point(x, h));
            }

            // 2. Threshold Lines (Setpoint, Warning, Alarm)
            DrawThresholdLines(context, w, h);

            // 3. Multi-Pen Curves or Single Legacy Series
            bool hasDrawnPens = false;
            if (_pens != null)
            {
                foreach (var pen in _pens)
                {
                    if (!pen.IsVisible || pen.Values == null || pen.Values.Count < 2)
                        continue;

                    DrawPenSeries(context, pen, w, h);
                    hasDrawnPens = true;
                }
            }

            // Fallback for legacy single Values
            if (!hasDrawnPens && _values != null)
            {
                DrawLegacySeries(context, w, h);
            }

            // 4. Interactive Crosshair Line & Point Markers
            if (EnableCrosshair && IsCrosshairActive && CrosshairX >= 0 && CrosshairX <= w)
            {
                DrawCrosshair(context, w, h, isLight);
            }
        }

        private (double Min, double Max) GetPenRange(ITrendPenRenderData pen)
        {
            double pMin, pMax;
            if (pen.AutoScale)
            {
                pMin = double.MaxValue;
                pMax = double.MinValue;
                var vals = pen.Values;
                int count = vals?.Count ?? 0;
                for (int i = 0; i < count; i++)
                {
                    double v = vals![i];
                    if (v < pMin) pMin = v;
                    if (v > pMax) pMax = v;
                }
                if (pMin > pMax)
                {
                    pMin = pen.MinValue;
                    pMax = pen.MaxValue;
                }
                // ISA-101 / SCADA: Protect against micro-noise in Full Dynamic (Delta Y >= 1.0)
                if (pMax - pMin < 1.0)
                {
                    double mid = (pMax + pMin) * 0.5;
                    pMin = mid - 0.5;
                    pMax = mid + 0.5;
                }
            }
            else if (string.Equals(ScalingMode, "DualAxis", StringComparison.OrdinalIgnoreCase))
            {
                if (string.Equals(pen.YAxisBinding, "Right", StringComparison.OrdinalIgnoreCase))
                {
                    pMin = RightMinY;
                    pMax = RightMaxY;
                }
                else if (string.Equals(pen.YAxisBinding, "Left", StringComparison.OrdinalIgnoreCase))
                {
                    pMin = MinY;
                    pMax = MaxY;
                }
                else
                {
                    pMin = pen.MinValue;
                    pMax = pen.MaxValue;
                }
            }
            else
            {
                pMin = pen.MinValue;
                pMax = pen.MaxValue;
            }

            if (Math.Abs(pMax - pMin) < 0.001)
            {
                pMax = pMin + 1.0;
            }

            return (pMin, pMax);
        }

        private void DrawThresholdLines(DrawingContext context, double w, double h)
        {
            double globalMin = MinY;
            double globalMax = MaxY;
            if (Math.Abs(globalMax - globalMin) < 0.001) globalMax = globalMin + 1.0;
            double globalInvRange = 1.0 / (globalMax - globalMin);

            if (ShowSetpoint && SetpointValue >= globalMin && SetpointValue <= globalMax)
            {
                double spY = h - ((SetpointValue - globalMin) * globalInvRange * h);
                context.DrawLine(SetpointPen, new Point(0, spY), new Point(w, spY));
            }

            if (ShowWarningLimits)
            {
                if (WarningHigh >= globalMin && WarningHigh <= globalMax)
                {
                    double whY = h - ((WarningHigh - globalMin) * globalInvRange * h);
                    context.DrawLine(WarningPen, new Point(0, whY), new Point(w, whY));
                }
                if (WarningLow >= globalMin && WarningLow <= globalMax)
                {
                    double wlY = h - ((WarningLow - globalMin) * globalInvRange * h);
                    context.DrawLine(WarningPen, new Point(0, wlY), new Point(w, wlY));
                }
            }

            if (ShowAlarmLimits)
            {
                if (AlarmHigh >= globalMin && AlarmHigh <= globalMax)
                {
                    double ahY = h - ((AlarmHigh - globalMin) * globalInvRange * h);
                    context.DrawLine(AlarmPen, new Point(0, ahY), new Point(w, ahY));
                }
                if (AlarmLow >= globalMin && AlarmLow <= globalMax)
                {
                    double alY = h - ((AlarmLow - globalMin) * globalInvRange * h);
                    context.DrawLine(AlarmPen, new Point(0, alY), new Point(w, alY));
                }
            }
        }

        private void DrawPenSeries(DrawingContext context, ITrendPenRenderData pen, double w, double h)
        {
            var values = pen.Values;
            int count = values?.Count ?? 0;
            if (count < 2) return;

            var (pMin, pMax) = GetPenRange(pen);
            double invRange = 1.0 / (pMax - pMin);
            double stepX = w / (count - 1);
            var linePen = GetOrCreatePen(pen.ColorHex, pen.Thickness, pen.StrokeStyle);
            bool isStep = string.Equals(pen.InterpolationMode, "Step", StringComparison.OrdinalIgnoreCase);

            double prevVal = Math.Clamp(values![0], pMin, pMax);
            double prevX = 0;
            double prevY = h - ((prevVal - pMin) * invRange * h);

            for (int i = 1; i < count; i++)
            {
                double currVal = Math.Clamp(values[i], pMin, pMax);
                double currX = stepX * i;
                double currY = h - ((currVal - pMin) * invRange * h);

                if (isStep)
                {
                    // Orthogonal staircase (for discrete 0/1 signals)
                    context.DrawLine(linePen, new Point(prevX, prevY), new Point(currX, prevY));
                    context.DrawLine(linePen, new Point(currX, prevY), new Point(currX, currY));
                }
                else
                {
                    // Linear continuous line
                    context.DrawLine(linePen, new Point(prevX, prevY), new Point(currX, currY));
                }

                prevX = currX;
                prevY = currY;
            }
        }

        private void DrawLegacySeries(DrawingContext context, double w, double h)
        {
            _legacyValList.Clear();
            if (_values is IReadOnlyList<double> doubleList)
            {
                for (int i = 0; i < doubleList.Count; i++)
                {
                    _legacyValList.Add(doubleList[i]);
                }
            }
            else
            {
                foreach (var item in _values!)
                {
                    if (item is double d) _legacyValList.Add(d);
                    else if (item is float f) _legacyValList.Add(f);
                    else if (item != null && double.TryParse(item.ToString(), NumberStyles.Float, CultureInfo.InvariantCulture, out double parsed))
                        _legacyValList.Add(parsed);
                }
            }

            int count = _legacyValList.Count;
            if (count < 2) return;

            double minY = MinY;
            double maxY = MaxY;
            if (Math.Abs(maxY - minY) < 0.001) maxY = minY + 1.0;
            double invRange = 1.0 / (maxY - minY);
            double stepX = w / (count - 1);

            var pen = LineBrush != null ? new Pen(LineBrush, 2) : DefaultLinePen;

            double prevVal = Math.Clamp(_legacyValList[0], minY, maxY);
            double prevX = 0;
            double prevY = h - ((prevVal - minY) * invRange * h);

            for (int i = 1; i < count; i++)
            {
                double currVal = Math.Clamp(_legacyValList[i], minY, maxY);
                double currX = stepX * i;
                double currY = h - ((currVal - minY) * invRange * h);

                context.DrawLine(pen, new Point(prevX, prevY), new Point(currX, currY));

                prevX = currX;
                prevY = currY;
            }
        }

        private void DrawCrosshair(DrawingContext context, double w, double h, bool isLight)
        {
            int sliceIdx = CrosshairIndex;
            int maxPoints = GetMaxPointsCount();
            double drawX = (maxPoints > 1 && sliceIdx >= 0)
                ? (w / (maxPoints - 1)) * sliceIdx
                : CrosshairX;

            var chPen = isLight ? LightCrosshairPen : DarkCrosshairPen;
            context.DrawLine(chPen, new Point(drawX, 0), new Point(drawX, h));

            if (sliceIdx < 0) return;

            // Draw intersection dots on active pens
            if (_pens != null)
            {
                foreach (var pen in _pens)
                {
                    if (!pen.IsVisible || pen.Values == null || sliceIdx >= pen.Values.Count)
                        continue;

                    var (pMin, pMax) = GetPenRange(pen);
                    double invRange = 1.0 / (pMax - pMin);
                    double val = Math.Clamp(pen.Values[sliceIdx], pMin, pMax);
                    double ptY = h - ((val - pMin) * invRange * h);

                    var brush = GetOrCreateBrush(pen.ColorHex);
                    context.DrawEllipse(brush, null, new Point(drawX, ptY), 3.5, 3.5);
                }
            }
        }

        #endregion
    }
}
