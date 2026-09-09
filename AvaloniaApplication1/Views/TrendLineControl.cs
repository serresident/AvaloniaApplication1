using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace AvaloniaApplication1.Views
{
    public class TrendLineControl : Control
    {
        private IEnumerable? _values;
        private static readonly Pen GridPen = new Pen(
            new SolidColorBrush(Color.FromArgb(20, 255, 255, 255)), 
            1, 
            new DashStyle(new double[] { 4, 4 }, 0));
        private static readonly Pen DefaultLinePen = new Pen(new SolidColorBrush(Color.FromRgb(0, 255, 204)), 2);
        private static readonly LinearGradientBrush DefaultAreaBrush = new LinearGradientBrush
        {
            StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
            EndPoint = new RelativePoint(0, 1, RelativeUnit.Relative),
            GradientStops = new GradientStops
            {
                new GradientStop(Color.FromArgb(60, 0, 255, 204), 0),
                new GradientStop(Color.FromArgb(0, 0, 255, 204), 1)
            }
        };

        private readonly List<double> _valList = new();
        private readonly List<Point> _points = new();

        public static readonly DirectProperty<TrendLineControl, IEnumerable?> ValuesProperty =
            AvaloniaProperty.RegisterDirect<TrendLineControl, IEnumerable?>(
                nameof(Values),
                o => o.Values,
                (o, v) => o.Values = v);

        public static readonly StyledProperty<double> MinYProperty =
            AvaloniaProperty.Register<TrendLineControl, double>(nameof(MinY), 0.0, coerce: CoerceMinY);

        public static readonly StyledProperty<double> MaxYProperty =
            AvaloniaProperty.Register<TrendLineControl, double>(nameof(MaxY), 100.0, coerce: CoerceMaxY);

        public static readonly StyledProperty<IBrush?> LineBrushProperty =
            AvaloniaProperty.Register<TrendLineControl, IBrush?>(nameof(LineBrush));

        public static readonly StyledProperty<IBrush?> AreaBrushProperty =
            AvaloniaProperty.Register<TrendLineControl, IBrush?>(nameof(AreaBrush));

        private static double CoerceMinY(AvaloniaObject inst, double val) => Math.Clamp(val, -10000.0, 10000.0);
        private static double CoerceMaxY(AvaloniaObject inst, double val) => Math.Clamp(val, -10000.0, 10000.0);

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

        static TrendLineControl()
        {
            AffectsRender<TrendLineControl>(ValuesProperty, MinYProperty, MaxYProperty, LineBrushProperty, AreaBrushProperty);
        }

        private void OnValuesChanged(IEnumerable? oldValue, IEnumerable? newValue)
        {
            if (oldValue is INotifyCollectionChanged oldCol)
            {
                oldCol.CollectionChanged -= OnCollectionChanged;
            }
            if (newValue is INotifyCollectionChanged newCol)
            {
                newCol.CollectionChanged += OnCollectionChanged;
            }
        }

        private void OnCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            InvalidateVisual();
        }

        public override void Render(DrawingContext context)
        {
            base.Render(context);

            var bounds = Bounds;
            double w = bounds.Width;
            double h = bounds.Height;

            if (w <= 0 || h <= 0) return;

            // 1. Draw Grid Lines
            // Horizontal grid lines (4 rows)
            for (int i = 1; i < 4; i++)
            {
                double y = (h / 4) * i;
                context.DrawLine(GridPen, new Point(0, y), new Point(w, y));
            }

            // Vertical grid lines (6 columns)
            for (int i = 1; i < 6; i++)
            {
                double x = (w / 6) * i;
                context.DrawLine(GridPen, new Point(x, 0), new Point(x, h));
            }

            // 2. Extract Values
            _valList.Clear();
            if (Values != null)
            {
                foreach (var item in Values)
                {
                    if (item is double d) _valList.Add(d);
                    else if (item is float f) _valList.Add(f);
                    else if (item != null && double.TryParse(item.ToString(), NumberStyles.Float, CultureInfo.InvariantCulture, out double parsed)) _valList.Add(parsed);
                }
            }

            if (_valList.Count < 2) return;

            // 3. Compute Points
            double minY = MinY;
            double maxY = MaxY;
            if (Math.Abs(maxY - minY) < 0.001) maxY = minY + 1.0; // Avoid division by zero

            int maxCount = _valList.Count;
            _points.Clear();

            for (int i = 0; i < maxCount; i++)
            {
                double x = (w / (maxCount - 1)) * i;
                double val = _valList[i];
                // Clamp
                if (val < minY) val = minY;
                if (val > maxY) val = maxY;

                // Y-coordinate: 0 is at top, Height is at bottom
                double y = h - ((val - minY) / (maxY - minY) * h);
                _points.Add(new Point(x, y));
            }

            // 4. Draw Trend Line & Area Fill
            var linePen = LineBrush != null ? new Pen(LineBrush, 2) : DefaultLinePen;

            // Area Geometry
            var areaGeometry = new StreamGeometry();
            using (var ctx = areaGeometry.Open())
            {
                ctx.BeginFigure(new Point(0, h), true); // Bottom-left corner
                ctx.LineTo(_points[0]);
                for (int i = 1; i < _points.Count; i++)
                {
                    ctx.LineTo(_points[i]);
                }
                ctx.LineTo(new Point(w, h)); // Bottom-right corner
                ctx.EndFigure(true);
            }

            // Line Geometry
            var lineGeometry = new StreamGeometry();
            using (var ctx = lineGeometry.Open())
            {
                ctx.BeginFigure(_points[0], false);
                for (int i = 1; i < _points.Count; i++)
                {
                    ctx.LineTo(_points[i]);
                }
                ctx.EndFigure(false);
            }

            // Fill Area
            var fillBrush = AreaBrush ?? DefaultAreaBrush;

            context.DrawGeometry(fillBrush, null, areaGeometry);
            context.DrawGeometry(null, linePen, lineGeometry);
        }
    }
}
