using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace AvaloniaApplication1.Views
{
    public class TrendLineControl : Control
    {
        public static readonly StyledProperty<IEnumerable?> ValuesProperty =
            AvaloniaProperty.Register<TrendLineControl, IEnumerable?>(nameof(Values));

        public static readonly StyledProperty<double> MinYProperty =
            AvaloniaProperty.Register<TrendLineControl, double>(nameof(MinY), 0.0);

        public static readonly StyledProperty<double> MaxYProperty =
            AvaloniaProperty.Register<TrendLineControl, double>(nameof(MaxY), 100.0);

        public static readonly StyledProperty<IBrush?> LineBrushProperty =
            AvaloniaProperty.Register<TrendLineControl, IBrush?>(nameof(LineBrush));

        public static readonly StyledProperty<IBrush?> AreaBrushProperty =
            AvaloniaProperty.Register<TrendLineControl, IBrush?>(nameof(AreaBrush));

        public IEnumerable? Values
        {
            get => GetValue(ValuesProperty);
            set => SetValue(ValuesProperty, value);
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
            ValuesProperty.Changed.AddClassHandler<TrendLineControl>((control, args) =>
            {
                control.OnValuesChanged(args.OldValue as IEnumerable, args.NewValue as IEnumerable);
            });
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
            var gridPen = new Pen(new SolidColorBrush(Color.FromArgb(20, 255, 255, 255)), 1,
                new DashStyle(new double[] { 4, 4 }, 0));

            // Horizontal grid lines (4 rows)
            for (int i = 1; i < 4; i++)
            {
                double y = (h / 4) * i;
                context.DrawLine(gridPen, new Point(0, y), new Point(w, y));
            }

            // Vertical grid lines (6 columns)
            for (int i = 1; i < 6; i++)
            {
                double x = (w / 6) * i;
                context.DrawLine(gridPen, new Point(x, 0), new Point(x, h));
            }

            // 2. Extract Values
            var valList = new List<double>();
            if (Values != null)
            {
                foreach (var item in Values)
                {
                    if (item is double d) valList.Add(d);
                    else if (item is float f) valList.Add(f);
                    else if (item != null && double.TryParse(item.ToString(), out double parsed)) valList.Add(parsed);
                }
            }

            if (valList.Count < 2) return;

            // 3. Compute Points
            double minY = MinY;
            double maxY = MaxY;
            if (Math.Abs(maxY - minY) < 0.001) maxY = minY + 1.0; // Avoid division by zero

            int maxCount = valList.Count;
            var points = new List<Point>();

            for (int i = 0; i < maxCount; i++)
            {
                double x = (w / (maxCount - 1)) * i;
                double val = valList[i];
                // Clamp
                if (val < minY) val = minY;
                if (val > maxY) val = maxY;

                // Y-coordinate: 0 is at top, Height is at bottom
                double y = h - ((val - minY) / (maxY - minY) * h);
                points.Add(new Point(x, y));
            }

            // 4. Draw Trend Line & Area Fill
            var linePen = new Pen(LineBrush ?? new SolidColorBrush(Color.FromRgb(0, 255, 204)), 2);

            // Area Geometry
            var areaGeometry = new StreamGeometry();
            using (var ctx = areaGeometry.Open())
            {
                ctx.BeginFigure(new Point(0, h), true); // Bottom-left corner
                ctx.LineTo(points[0]);
                for (int i = 1; i < points.Count; i++)
                {
                    ctx.LineTo(points[i]);
                }
                ctx.LineTo(new Point(w, h)); // Bottom-right corner
                ctx.EndFigure(true);
            }

            // Line Geometry
            var lineGeometry = new StreamGeometry();
            using (var ctx = lineGeometry.Open())
            {
                ctx.BeginFigure(points[0], false);
                for (int i = 1; i < points.Count; i++)
                {
                    ctx.LineTo(points[i]);
                }
                ctx.EndFigure(false);
            }

            // Fill Area
            var fillBrush = AreaBrush ?? new LinearGradientBrush
            {
                StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
                EndPoint = new RelativePoint(0, 1, RelativeUnit.Relative),
                GradientStops = new GradientStops
                {
                    new GradientStop(Color.FromArgb(60, 0, 255, 204), 0),
                    new GradientStop(Color.FromArgb(0, 0, 255, 204), 1)
                }
            };

            context.DrawGeometry(fillBrush, null, areaGeometry);
            context.DrawGeometry(null, linePen, lineGeometry);
        }
    }
}
