using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Rendering;
using AvaloniaApplication1.ViewModels;

namespace AvaloniaApplication1.Views
{
    public class PipeControl : Control, ICustomHitTest
    {
        public static readonly StyledProperty<string> PointsProperty =
            AvaloniaProperty.Register<PipeControl, string>(nameof(Points), string.Empty);

        public static readonly StyledProperty<IBrush?> PipeColorProperty =
            AvaloniaProperty.Register<PipeControl, IBrush?>(nameof(PipeColor));

        public static readonly StyledProperty<bool> ShowFlangesProperty =
            AvaloniaProperty.Register<PipeControl, bool>(nameof(ShowFlanges), true);

        public static readonly StyledProperty<double> CellSizeProperty =
            AvaloniaProperty.Register<PipeControl, double>(nameof(CellSize), 10.0);

        public static readonly StyledProperty<string> StartFittingProperty =
            AvaloniaProperty.Register<PipeControl, string>(nameof(StartFitting), "None");

        public static readonly StyledProperty<string> EndFittingProperty =
            AvaloniaProperty.Register<PipeControl, string>(nameof(EndFitting), "None");

        public static readonly StyledProperty<bool> IsDesignModeProperty =
            AvaloniaProperty.Register<PipeControl, bool>(nameof(IsDesignMode), false);

        public static readonly StyledProperty<bool> IsEditingVerticesProperty =
            AvaloniaProperty.Register<PipeControl, bool>(nameof(IsEditingVertices), false);

        public static readonly StyledProperty<Color> ActiveColorProperty =
            AvaloniaProperty.Register<PipeControl, Color>(nameof(ActiveColor), Colors.DodgerBlue);

        public static readonly StyledProperty<Color> InactiveColorProperty =
            AvaloniaProperty.Register<PipeControl, Color>(nameof(InactiveColor), Colors.Gray);

        private bool _isFilled = false;
        public static readonly DirectProperty<PipeControl, bool> IsFilledProperty =
            AvaloniaProperty.RegisterDirect<PipeControl, bool>(
                nameof(IsFilled),
                o => o.IsFilled,
                (o, v) => o.IsFilled = v);

        public static readonly StyledProperty<double> ThicknessProperty =
            AvaloniaProperty.Register<PipeControl, double>(nameof(Thickness), 12.0, coerce: CoerceThickness);

        private static double CoerceThickness(AvaloniaObject inst, double val) => Math.Clamp(val, 2.0, 50.0);

        public string Points
        {
            get => GetValue(PointsProperty);
            set => SetValue(PointsProperty, value);
        }

        public IBrush? PipeColor
        {
            get => GetValue(PipeColorProperty);
            set => SetValue(PipeColorProperty, value);
        }

        public bool ShowFlanges
        {
            get => GetValue(ShowFlangesProperty);
            set => SetValue(ShowFlangesProperty, value);
        }

        public double CellSize
        {
            get => GetValue(CellSizeProperty);
            set => SetValue(CellSizeProperty, value);
        }

        public string StartFitting
        {
            get => GetValue(StartFittingProperty);
            set => SetValue(StartFittingProperty, value);
        }

        public string EndFitting
        {
            get => GetValue(EndFittingProperty);
            set => SetValue(EndFittingProperty, value);
        }

        public bool IsDesignMode
        {
            get => GetValue(IsDesignModeProperty);
            set => SetValue(IsDesignModeProperty, value);
        }

        public bool IsEditingVertices
        {
            get => GetValue(IsEditingVerticesProperty);
            set => SetValue(IsEditingVerticesProperty, value);
        }

        public Color ActiveColor
        {
            get => GetValue(ActiveColorProperty);
            set => SetValue(ActiveColorProperty, value);
        }

        public Color InactiveColor
        {
            get => GetValue(InactiveColorProperty);
            set => SetValue(InactiveColorProperty, value);
        }

        public bool IsFilled
        {
            get => _isFilled;
            set => SetAndRaise(IsFilledProperty, ref _isFilled, value);
        }

        public double Thickness
        {
            get => GetValue(ThicknessProperty);
            set => SetValue(ThicknessProperty, value);
        }

        static PipeControl()
        {
            AffectsRender<PipeControl>(
                PointsProperty, 
                PipeColorProperty, 
                ShowFlangesProperty, 
                CellSizeProperty,
                StartFittingProperty,
                EndFittingProperty,
                IsDesignModeProperty,
                IsEditingVerticesProperty,
                ActiveColorProperty,
                InactiveColorProperty,
                IsFilledProperty,
                ThicknessProperty);
        }

        private static ContextMenu? _activeMenu;

        public static void CloseActiveMenu()
        {
            if (_activeMenu != null)
            {
                _activeMenu.Close();
                _activeMenu = null;
            }
        }

        public PipeControl()
        {
            ClipToBounds = false;
        }

        public override void Render(DrawingContext context)
        {
            base.Render(context);

            if (string.IsNullOrEmpty(Points)) return;

            var gridPoints = ParsePoints(Points);
            if (gridPoints.Count < 2) return;

            double cellSize = CellSize;
            var pixelPoints = new List<Point>();
            foreach (var gp in gridPoints)
            {
                pixelPoints.Add(new Point(gp.X * cellSize + cellSize / 2, gp.Y * cellSize + cellSize / 2));
            }

            double thickness = Thickness;
            
            // Determine base color
            Color baseColor = InactiveColor;
            if (IsFilled)
            {
                baseColor = ActiveColor;
            }
            else if (PipeColor is SolidColorBrush scb)
            {
                baseColor = scb.Color;
            }

            // Calculations for start and end fittings
            Point startPoint = pixelPoints[0];
            Point nextPoint = pixelPoints[1];
            double startAngle = GetSegmentAngle(startPoint, nextPoint);

            double startElbowDir = 0;
            bool hasStartElbow = false;
            if (StartFitting == "Elbow90") { startElbowDir = startAngle - Math.PI / 2; hasStartElbow = true; }
            else if (StartFitting == "ElbowMinus90") { startElbowDir = startAngle + Math.PI / 2; hasStartElbow = true; }
            else if (StartFitting == "Elbow45") { startElbowDir = startAngle - 3 * Math.PI / 4; hasStartElbow = true; }
            else if (StartFitting == "ElbowMinus45") { startElbowDir = startAngle + 3 * Math.PI / 4; hasStartElbow = true; }

            Point endPoint = pixelPoints[pixelPoints.Count - 1];
            Point prevPoint = pixelPoints[pixelPoints.Count - 2];
            double endAngle = GetSegmentAngle(prevPoint, endPoint);

            double endElbowDir = 0;
            bool hasEndElbow = false;
            if (EndFitting == "Elbow90") { endElbowDir = endAngle + Math.PI / 2; hasEndElbow = true; }
            else if (EndFitting == "ElbowMinus90") { endElbowDir = endAngle - Math.PI / 2; hasEndElbow = true; }
            else if (EndFitting == "Elbow45") { endElbowDir = endAngle + Math.PI / 4; hasEndElbow = true; }
            else if (EndFitting == "ElbowMinus45") { endElbowDir = endAngle - Math.PI / 4; hasEndElbow = true; }

            // 1. Draw 3D segments
            for (int i = 0; i < pixelPoints.Count - 1; i++)
            {
                Draw3DSegment(context, pixelPoints[i], pixelPoints[i + 1], baseColor, thickness);
            }

            // 2. Draw Elbow extensions
            if (hasStartElbow)
            {
                double elbowLength = thickness * 1.2;
                Point pElbow = startPoint + new Point(Math.Cos(startElbowDir) * elbowLength, Math.Sin(startElbowDir) * elbowLength);
                Draw3DSegment(context, startPoint, pElbow, baseColor, thickness);
                context.DrawEllipse(new SolidColorBrush(baseColor), null, startPoint, thickness / 2, thickness / 2);
            }
            if (hasEndElbow)
            {
                double elbowLength = thickness * 1.2;
                Point pElbow = endPoint + new Point(Math.Cos(endElbowDir) * elbowLength, Math.Sin(endElbowDir) * elbowLength);
                Draw3DSegment(context, endPoint, pElbow, baseColor, thickness);
                context.DrawEllipse(new SolidColorBrush(baseColor), null, endPoint, thickness / 2, thickness / 2);
            }

            // 3. Draw joint plates, elbows, and flanges
            var flangeBrush = new SolidColorBrush(Color.FromRgb(160, 160, 164));
            var flangeBorderPen = new Pen(Brushes.Black, 1);

            for (int i = 0; i < pixelPoints.Count; i++)
            {
                Point p = pixelPoints[i];

                if (i == 0) // Start
                {
                    if (StartFitting == "Flange" && ShowFlanges)
                    {
                        double angle = startAngle + Math.PI / 2;
                        DrawFlangePlate(context, p, angle, flangeBrush, flangeBorderPen, thickness);
                    }
                }
                else if (i == pixelPoints.Count - 1) // End
                {
                    if (EndFitting == "Flange" && ShowFlanges)
                    {
                        double angle = endAngle + Math.PI / 2;
                        DrawFlangePlate(context, p, angle, flangeBrush, flangeBorderPen, thickness);
                    }
                }
                else // Mid corner
                {
                    context.DrawEllipse(new SolidColorBrush(baseColor), null, p, thickness / 2, thickness / 2);
                }
            }

            // 4. Markers in Design Mode
            if (IsDesignMode && IsEditingVertices)
            {
                var markerBrush = Brushes.White;
                var markerPen = new Pen(Brushes.DodgerBlue, 2);
                for (int i = 0; i < pixelPoints.Count; i++)
                {
                    context.DrawEllipse(markerBrush, markerPen, pixelPoints[i], 6, 6);
                }
            }
        }

        private void Draw3DSegment(DrawingContext context, Point p1, Point p2, Color baseColor, double thickness)
        {
            double dx = p2.X - p1.X;
            double dy = p2.Y - p1.Y;
            double dist = Math.Sqrt(dx * dx + dy * dy);
            if (dist < 0.1) return;

            double nx = -dy / dist;
            double ny = dx / dist;

            var a1 = new Point(p1.X + nx * thickness / 2, p1.Y + ny * thickness / 2);
            var a2 = new Point(p2.X + nx * thickness / 2, p2.Y + ny * thickness / 2);
            var b2 = new Point(p2.X - nx * thickness / 2, p2.Y - ny * thickness / 2);
            var b1 = new Point(p1.X - nx * thickness / 2, p1.Y - ny * thickness / 2);

            var geometry = new StreamGeometry();
            using (var ctx = geometry.Open())
            {
                ctx.BeginFigure(a1, true);
                ctx.LineTo(a2);
                ctx.LineTo(b2);
                ctx.LineTo(b1);
                ctx.EndFigure(true);
            }

            var brush = new LinearGradientBrush
            {
                StartPoint = new RelativePoint(p1.X + nx * thickness / 2, p1.Y + ny * thickness / 2, RelativeUnit.Absolute),
                EndPoint = new RelativePoint(p1.X - nx * thickness / 2, p1.Y - ny * thickness / 2, RelativeUnit.Absolute),
                GradientStops = new GradientStops
                {
                    new GradientStop(DarkenColor(baseColor, 0.65), 0.0),   // Тень
                    new GradientStop(LightenColor(baseColor, 0.75), 0.25), // Блик
                    new GradientStop(baseColor, 0.55),                    // Базовый цвет
                    new GradientStop(DarkenColor(baseColor, 0.35), 1.0)   // Полутень
                }
            };

            context.DrawGeometry(brush, null, geometry);
        }

        private void DrawFlangePlate(DrawingContext context, Point center, double angleRad, IBrush brush, Pen borderPen, double thickness)
        {
            double w = Math.Clamp(thickness * 0.4, 2.0, 8.0);
            double h = thickness * 2.2;

            var cos = Math.Cos(angleRad);
            var sin = Math.Sin(angleRad);

            var p1 = new Point(center.X - w/2 * cos - h/2 * sin, center.Y - w/2 * sin + h/2 * cos);
            var p2 = new Point(center.X + w/2 * cos - h/2 * sin, center.Y + w/2 * sin + h/2 * cos);
            var p3 = new Point(center.X + w/2 * cos + h/2 * sin, center.Y + w/2 * sin - h/2 * cos);
            var p4 = new Point(center.X - w/2 * cos + h/2 * sin, center.Y - w/2 * sin - h/2 * cos);

            var geometry = new StreamGeometry();
            using (var ctx = geometry.Open())
            {
                ctx.BeginFigure(p1, true);
                ctx.LineTo(p2);
                ctx.LineTo(p3);
                ctx.LineTo(p4);
                ctx.EndFigure(true);
            }

            context.DrawGeometry(brush, borderPen, geometry);
        }

        private double GetSegmentAngle(Point p1, Point p2)
        {
            return Math.Atan2(p2.Y - p1.Y, p2.X - p1.X);
        }

        private Color LightenColor(Color color, double factor)
        {
            return Color.FromRgb(
                (byte)Math.Min(255, color.R + (255 - color.R) * factor),
                (byte)Math.Min(255, color.G + (255 - color.G) * factor),
                (byte)Math.Min(255, color.B + (255 - color.B) * factor)
            );
        }

        private Color DarkenColor(Color color, double factor)
        {
            return Color.FromRgb(
                (byte)(color.R * (1 - factor)),
                (byte)(color.G * (1 - factor)),
                (byte)(color.B * (1 - factor))
            );
        }

        public bool HitTest(Point point)
        {
            if (string.IsNullOrEmpty(Points)) return false;

            var gridPoints = ParsePoints(Points);
            double cellSize = CellSize;
            double thickness = Thickness;
            double tolerance = IsDesignMode ? 10.0 : (thickness / 2.0 + 2.0);

            for (int i = 0; i < gridPoints.Count; i++)
            {
                var p = new Point(gridPoints[i].X * cellSize + cellSize / 2, gridPoints[i].Y * cellSize + cellSize / 2);
                if (Math.Sqrt(Math.Pow(point.X - p.X, 2) + Math.Pow(point.Y - p.Y, 2)) <= tolerance)
                    return true;
            }

            for (int i = 0; i < gridPoints.Count - 1; i++)
            {
                var p1 = new Point(gridPoints[i].X * cellSize + cellSize / 2, gridPoints[i].Y * cellSize + cellSize / 2);
                var p2 = new Point(gridPoints[i + 1].X * cellSize + cellSize / 2, gridPoints[i + 1].Y * cellSize + cellSize / 2);

                if (IsPointNearSegment(point, p1, p2, tolerance))
                    return true;
            }

            return false;
        }

        private bool IsPointNearSegment(Point p, Point s1, Point s2, double maxDistance)
        {
            double l2 = Math.Pow(s1.X - s2.X, 2) + Math.Pow(s1.Y - s2.Y, 2);
            if (l2 == 0) return Math.Sqrt(Math.Pow(p.X - s1.X, 2) + Math.Pow(p.Y - s1.Y, 2)) <= maxDistance;

            double t = ((p.X - s1.X) * (s2.X - s1.X) + (p.Y - s1.Y) * (s2.Y - s1.Y)) / l2;
            t = Math.Clamp(t, 0.0, 1.0);

            var projection = new Point(s1.X + t * (s2.X - s1.X), s1.Y + t * (s2.Y - s1.Y));
            double dist = Math.Sqrt(Math.Pow(p.X - projection.X, 2) + Math.Pow(p.Y - projection.Y, 2));

            return dist <= maxDistance;
        }

        private DashboardPanel? FindDashboardPanel()
        {
            StyledElement? parent = this.Parent;
            while (parent != null && !(parent is DashboardPanel))
            {
                parent = parent.Parent;
            }
            return parent as DashboardPanel;
        }

        private List<PipeControl> FindAllSiblingPipes()
        {
            var list = new List<PipeControl>();
            StyledElement? parent = this.Parent;
            while (parent != null && !(parent is DashboardPanel))
            {
                parent = parent.Parent;
            }

            if (parent is DashboardPanel panel)
            {
                foreach (var child in panel.Children)
                {
                    FindPipeControlsRecursive(child, list);
                }
            }
            return list;
        }

        private void FindPipeControlsRecursive(Control control, List<PipeControl> result)
        {
            if (control is PipeControl pipe && pipe != this)
            {
                result.Add(pipe);
                return;
            }

            if (control is Panel panel)
            {
                foreach (var child in panel.Children)
                {
                    FindPipeControlsRecursive(child, result);
                }
            }
            else if (control is ContentControl cc && cc.Content is Control contentControl)
            {
                FindPipeControlsRecursive(contentControl, result);
            }
            else if (control is ContentPresenter cp && cp.Child is Control childControl)
            {
                FindPipeControlsRecursive(childControl, result);
            }
            else if (control is Border border && border.Child is Control borderChild)
            {
                FindPipeControlsRecursive(borderChild, result);
            }
            else if (control is Grid grid)
            {
                foreach (var child in grid.Children)
                {
                    FindPipeControlsRecursive(child, result);
                }
            }
        }

        public void ShowVertexContextMenu(int pointIndex, Point screenPos)
        {
            CloseActiveMenu();

            var menu = new ContextMenu();
            _activeMenu = menu;
            menu.Closed += (s, ev) => {
                if (_activeMenu == menu)
                {
                    _activeMenu = null;
                }
            };

            var deleteItem = new MenuItem { Header = "Удалить точку" };
            deleteItem.Click += (s, ev) => RemovePointAt(pointIndex);
            var gridPoints = ParsePoints(Points);
            deleteItem.IsEnabled = gridPoints.Count > 2;
            menu.Items.Add(deleteItem);

            if (pointIndex == 0)
            {
                var addStartItem = new MenuItem { Header = "Добавить точку перед стартом" };
                addStartItem.Click += (s, ev) => AddPointAtStart();
                menu.Items.Add(addStartItem);

                var fittingsMenu = new MenuItem { Header = "Установить фитинг на старте" };
                
                var noneItem = new MenuItem { Header = "Нет", IsChecked = StartFitting == "None" };
                noneItem.Click += (s, ev) => StartFitting = "None";
                fittingsMenu.Items.Add(noneItem);

                var flangeItem = new MenuItem { Header = "Фланец", IsChecked = StartFitting == "Flange" };
                flangeItem.Click += (s, ev) => StartFitting = "Flange";
                fittingsMenu.Items.Add(flangeItem);

                var elbow90Item = new MenuItem { Header = "Отвод 90°", IsChecked = StartFitting == "Elbow90" };
                elbow90Item.Click += (s, ev) => StartFitting = "Elbow90";
                fittingsMenu.Items.Add(elbow90Item);

                var elbowM90Item = new MenuItem { Header = "Отвод -90°", IsChecked = StartFitting == "ElbowMinus90" };
                elbowM90Item.Click += (s, ev) => StartFitting = "ElbowMinus90";
                fittingsMenu.Items.Add(elbowM90Item);

                var elbow45Item = new MenuItem { Header = "Отвод 45°", IsChecked = StartFitting == "Elbow45" };
                elbow45Item.Click += (s, ev) => StartFitting = "Elbow45";
                fittingsMenu.Items.Add(elbow45Item);

                var elbowM45Item = new MenuItem { Header = "Отвод -45°", IsChecked = StartFitting == "ElbowMinus45" };
                elbowM45Item.Click += (s, ev) => StartFitting = "ElbowMinus45";
                fittingsMenu.Items.Add(elbowM45Item);

                menu.Items.Add(fittingsMenu);
            }
            else if (pointIndex == gridPoints.Count - 1)
            {
                var addEndItem = new MenuItem { Header = "Добавить точку после конца" };
                addEndItem.Click += (s, ev) => AddPointAtEnd();
                menu.Items.Add(addEndItem);

                var fittingsMenu = new MenuItem { Header = "Установить фитинг на конце" };
                
                var noneItem = new MenuItem { Header = "Нет", IsChecked = EndFitting == "None" };
                noneItem.Click += (s, ev) => EndFitting = "None";
                fittingsMenu.Items.Add(noneItem);

                var flangeItem = new MenuItem { Header = "Фланец", IsChecked = EndFitting == "Flange" };
                flangeItem.Click += (s, ev) => EndFitting = "Flange";
                fittingsMenu.Items.Add(flangeItem);

                var elbow90Item = new MenuItem { Header = "Отвод 90°", IsChecked = EndFitting == "Elbow90" };
                elbow90Item.Click += (s, ev) => EndFitting = "Elbow90";
                fittingsMenu.Items.Add(elbow90Item);

                var elbowM90Item = new MenuItem { Header = "Отвод -90°", IsChecked = EndFitting == "ElbowMinus90" };
                elbowM90Item.Click += (s, ev) => EndFitting = "ElbowMinus90";
                fittingsMenu.Items.Add(elbowM90Item);

                var elbow45Item = new MenuItem { Header = "Отвод 45°", IsChecked = EndFitting == "Elbow45" };
                elbow45Item.Click += (s, ev) => EndFitting = "Elbow45";
                fittingsMenu.Items.Add(elbow45Item);

                var elbowM45Item = new MenuItem { Header = "Отвод -45°", IsChecked = EndFitting == "ElbowMinus45" };
                elbowM45Item.Click += (s, ev) => EndFitting = "ElbowMinus45";
                fittingsMenu.Items.Add(elbowM45Item);

                menu.Items.Add(fittingsMenu);
            }

            menu.Open(this);
        }

        public void ShowSegmentContextMenu(int segmentIndex, Point screenPos)
        {
            CloseActiveMenu();

            var menu = new ContextMenu();
            _activeMenu = menu;
            menu.Closed += (s, ev) => {
                if (_activeMenu == menu)
                {
                    _activeMenu = null;
                }
            };

            var addItem = new MenuItem { Header = "Добавить точку перегиба" };
            addItem.Click += (s, ev) => AddPointOnSegment(segmentIndex, screenPos);
            menu.Items.Add(addItem);

            menu.Open(this);
        }

        private void AddPointOnSegment(int segmentIndex, Point clickPos)
        {
            double cellSize = CellSize;
            double gridX = Math.Round((clickPos.X - cellSize / 2) / cellSize);
            double gridY = Math.Round((clickPos.Y - cellSize / 2) / cellSize);

            var gridPoints = ParsePoints(Points);
            if (segmentIndex >= 0 && segmentIndex < gridPoints.Count - 1)
            {
                gridPoints.Insert(segmentIndex + 1, new Point(gridX, gridY));
                Points = string.Join(";", gridPoints.Select(p => string.Format(CultureInfo.InvariantCulture, "{0:0.##},{1:0.##}", p.X, p.Y)));
            }
        }

        private void AddPointAtStart()
        {
            var gridPoints = ParsePoints(Points);
            if (gridPoints.Count > 0)
            {
                var first = gridPoints[0];
                var dir = gridPoints.Count > 1 ? (first - gridPoints[1]) : new Point(-2, 0);
                double len = Math.Sqrt(dir.X * dir.X + dir.Y * dir.Y);
                if (len > 0)
                {
                    dir = new Point(Math.Round(dir.X / len * 2), Math.Round(dir.Y / len * 2));
                }
                if (dir.X == 0 && dir.Y == 0) dir = new Point(-2, 0);

                var newPoint = new Point(first.X + dir.X, first.Y + dir.Y);
                gridPoints.Insert(0, newPoint);
                Points = string.Join(";", gridPoints.Select(p => string.Format(CultureInfo.InvariantCulture, "{0:0.##},{1:0.##}", p.X, p.Y)));
            }
        }

        private void AddPointAtEnd()
        {
            var gridPoints = ParsePoints(Points);
            if (gridPoints.Count > 0)
            {
                var last = gridPoints[gridPoints.Count - 1];
                var dir = gridPoints.Count > 1 ? (last - gridPoints[gridPoints.Count - 2]) : new Point(2, 0);
                double len = Math.Sqrt(dir.X * dir.X + dir.Y * dir.Y);
                if (len > 0)
                {
                    dir = new Point(Math.Round(dir.X / len * 2), Math.Round(dir.Y / len * 2));
                }
                if (dir.X == 0 && dir.Y == 0) dir = new Point(2, 0);

                var newPoint = new Point(last.X + dir.X, last.Y + dir.Y);
                gridPoints.Add(newPoint);
                Points = string.Join(";", gridPoints.Select(p => string.Format(CultureInfo.InvariantCulture, "{0:0.##},{1:0.##}", p.X, p.Y)));
            }
        }

        private void RemovePointAt(int index)
        {
            var gridPoints = ParsePoints(Points);
            if (gridPoints.Count > 2 && index >= 0 && index < gridPoints.Count)
            {
                gridPoints.RemoveAt(index);
                Points = string.Join(";", gridPoints.Select(p => string.Format(CultureInfo.InvariantCulture, "{0:0.##},{1:0.##}", p.X, p.Y)));
            }
        }

        private List<Point> ParsePoints(string pointsStr)
        {
            var list = new List<Point>();
            var segments = pointsStr.Split(';', StringSplitOptions.RemoveEmptyEntries);
            foreach (var seg in segments)
            {
                var parts = seg.Split(',', StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length == 2 && double.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out double x) && double.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out double y))
                {
                    list.Add(new Point(x, y));
                }
            }
            return list;
        }
    }
}
