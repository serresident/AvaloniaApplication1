using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using AvaloniaApplication1.ViewModels;

namespace AvaloniaApplication1.Views
{
    /// <summary>
    /// A lightweight control drawn underneath the widgets to show grid lines and drag highlight.
    /// Added as first child of DashboardPanel when in design mode.
    /// </summary>
    public class GridOverlay : Control
    {
        public double CellWidth { get; set; } = 150;
        public double CellHeight { get; set; } = 150;
        public bool ShowHighlight { get; set; }
        public int HighlightCol { get; set; }
        public int HighlightRow { get; set; }
        public int HighlightSizeX { get; set; } = 1;
        public int HighlightSizeY { get; set; } = 1;

        public override void Render(DrawingContext context)
        {
            base.Render(context);

            // Draw grid lines
            var pen = new Pen(new SolidColorBrush(Color.FromArgb(40, 255, 255, 255)), 1,
                new DashStyle(new double[] { 4, 4 }, 0));

            double width = Bounds.Width;
            double height = Bounds.Height;

            for (double x = 0; x <= width; x += CellWidth)
            {
                context.DrawLine(pen, new Point(x, 0), new Point(x, height));
            }

            for (double y = 0; y <= height; y += CellHeight)
            {
                context.DrawLine(pen, new Point(0, y), new Point(width, y));
            }

            // Draw drop target highlight while dragging
            if (ShowHighlight)
            {
                var highlightBrush = new SolidColorBrush(Color.FromArgb(60, 0, 122, 204));
                var highlightRect = new Rect(
                    HighlightCol * CellWidth, HighlightRow * CellHeight,
                    HighlightSizeX * CellWidth, HighlightSizeY * CellHeight);

                context.DrawRectangle(highlightBrush, null, highlightRect);

                var borderPen = new Pen(new SolidColorBrush(Color.FromArgb(180, 0, 122, 204)), 2);
                context.DrawRectangle(null, borderPen, highlightRect);
            }
        }
    }

    public class DashboardPanel : Panel
    {
        public static readonly StyledProperty<double> CellWidthProperty =
            AvaloniaProperty.Register<DashboardPanel, double>(nameof(CellWidth), 150.0);

        public static readonly StyledProperty<double> CellHeightProperty =
            AvaloniaProperty.Register<DashboardPanel, double>(nameof(CellHeight), 150.0);

        public double CellWidth
        {
            get => GetValue(CellWidthProperty);
            set => SetValue(CellWidthProperty, value);
        }

        public double CellHeight
        {
            get => GetValue(CellHeightProperty);
            set => SetValue(CellHeightProperty, value);
        }

        public static readonly StyledProperty<bool> IsDesignModeProperty =
            AvaloniaProperty.Register<DashboardPanel, bool>(nameof(IsDesignMode), false);

        public bool IsDesignMode
        {
            get => GetValue(IsDesignModeProperty);
            set => SetValue(IsDesignModeProperty, value);
        }

        // Drag state
        private Control? _dragChild;
        private WidgetViewModelBase? _dragVm;
        private Point _dragStartPoint;
        private int _dragOriginalRow;
        private int _dragOriginalCol;
        private bool _isDragging;
        private bool _isResizing;
        private int _dragOriginalSizeX;
        private int _dragOriginalSizeY;

        // Pipe drag state
        private PipeControl? _draggedPipeControl;
        private int _draggedPointIndex = -1;

        // Grid overlay control
        private GridOverlay? _gridOverlay;

        static DashboardPanel()
        {
            AffectsMeasure<DashboardPanel>(CellWidthProperty, CellHeightProperty);
            AffectsArrange<DashboardPanel>(CellWidthProperty, CellHeightProperty);
            IsDesignModeProperty.Changed.AddClassHandler<DashboardPanel>((panel, args) =>
            {
                panel.UpdateGridOverlay();
            });
        }

        public DashboardPanel()
        {
            foreach (var child in Children)
            {
                child.DataContextChanged += Child_DataContextChanged;
                SubscribeVm(child.DataContext as WidgetViewModelBase);
            }

            Children.CollectionChanged += (s, e) =>
            {
                if (e.OldItems != null)
                {
                    foreach (Control child in e.OldItems)
                    {
                        child.DataContextChanged -= Child_DataContextChanged;
                        UnsubscribeVm(child.DataContext as WidgetViewModelBase);
                    }
                }
                if (e.NewItems != null)
                {
                    foreach (Control child in e.NewItems)
                    {
                        child.DataContextChanged += Child_DataContextChanged;
                        SubscribeVm(child.DataContext as WidgetViewModelBase);
                    }
                }
                InvalidateMeasure();
                InvalidateArrange();
            };
        }

        private void Child_DataContextChanged(object? sender, EventArgs e)
        {
            if (sender is Control child)
            {
                SubscribeVm(child.DataContext as WidgetViewModelBase);
                InvalidateMeasure();
                InvalidateArrange();
            }
        }

        private void SubscribeVm(WidgetViewModelBase? vm)
        {
            if (vm != null)
            {
                vm.PropertyChanged -= Vm_PropertyChanged;
                vm.PropertyChanged += Vm_PropertyChanged;
            }
        }

        private void UnsubscribeVm(WidgetViewModelBase? vm)
        {
            if (vm != null)
            {
                vm.PropertyChanged -= Vm_PropertyChanged;
            }
        }

        private void Vm_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(WidgetViewModelBase.Col) ||
                e.PropertyName == nameof(WidgetViewModelBase.Row) ||
                e.PropertyName == nameof(WidgetViewModelBase.SizeX) ||
                e.PropertyName == nameof(WidgetViewModelBase.SizeY))
            {
                InvalidateMeasure();
                InvalidateArrange();
            }
        }

        private void UpdateGridOverlay()
        {
            if (IsDesignMode)
            {
                if (_gridOverlay == null)
                {
                    _gridOverlay = new GridOverlay
                    {
                        CellWidth = CellWidth,
                        CellHeight = CellHeight,
                        IsHitTestVisible = false // Don't intercept clicks
                    };
                    Children.Insert(0, _gridOverlay);
                }
            }
            else
            {
                if (_gridOverlay != null)
                {
                    Children.Remove(_gridOverlay);
                    _gridOverlay = null;
                }
            }
        }

        protected override void OnPointerPressed(PointerPressedEventArgs e)
        {
            base.OnPointerPressed(e);

            if (e.Handled || !IsDesignMode) return;

            var point = e.GetPosition(this);

            // 1. Check if we clicked on a vertex of any Pipe widget
            foreach (var child in Children)
            {
                if (child == _gridOverlay) continue;

                if (child.DataContext is PipeWidgetViewModel pipeVm)
                {
                    var pipeControl = FindPipeControlRecursive(child);
                    if (pipeControl != null)
                    {
                        var gridPoints = pipeVm.GetAbsoluteGridPoints();
                        double cellSize = CellWidth;
                        for (int i = 0; i < gridPoints.Count; i++)
                        {
                            var pxPoint = new Point(
                                gridPoints[i].X * cellSize + cellSize / 2, 
                                gridPoints[i].Y * cellSize + cellSize / 2);
                            
                            double dist = Math.Sqrt(Math.Pow(point.X - pxPoint.X, 2) + Math.Pow(point.Y - pxPoint.Y, 2));
                            if (dist <= 12.0)
                            {
                                if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
                                {
                                    _draggedPipeControl = pipeControl;
                                    _draggedPointIndex = i;
                                    e.Pointer.Capture(this);
                                    e.Handled = true;
                                    return;
                                }
                                else if (e.GetCurrentPoint(this).Properties.IsRightButtonPressed)
                                {
                                    var posInPipe = e.GetPosition(pipeControl);
                                    pipeControl.ShowVertexContextMenu(i, posInPipe);
                                    e.Handled = true;
                                    return;
                                }
                            }
                        }
                    }
                }
            }

            // 2. Check if we right-clicked on a segment of any Pipe widget
            if (e.GetCurrentPoint(this).Properties.IsRightButtonPressed)
            {
                foreach (var child in Children)
                {
                    if (child == _gridOverlay) continue;

                    if (child.DataContext is PipeWidgetViewModel pipeVm)
                    {
                        var pipeControl = FindPipeControlRecursive(child);
                        if (pipeControl != null)
                        {
                            var gridPoints = pipeVm.GetAbsoluteGridPoints();
                            double cellSize = CellWidth;
                            double thickness = pipeControl.Thickness;
                            for (int i = 0; i < gridPoints.Count - 1; i++)
                            {
                                var p1 = new Point(gridPoints[i].X * cellSize + cellSize / 2, gridPoints[i].Y * cellSize + cellSize / 2);
                                var p2 = new Point(gridPoints[i + 1].X * cellSize + cellSize / 2, gridPoints[i + 1].Y * cellSize + cellSize / 2);

                                if (IsPointNearSegment(point, p1, p2, thickness / 2 + 6))
                                {
                                    var posInPipe = e.GetPosition(pipeControl);
                                    pipeControl.ShowSegmentContextMenu(i, posInPipe);
                                    e.Handled = true;
                                    return;
                                }
                            }
                        }
                    }
                }
            }

            // Find which child was clicked
            foreach (var child in Children)
            {
                if (child == _gridOverlay) continue; // Skip overlay

                if (child.DataContext is WidgetViewModelBase vm)
                {
                    var childBounds = new Rect(
                        vm.Col * CellWidth, vm.Row * CellHeight,
                        vm.SizeX * CellWidth, vm.SizeY * CellHeight);

                    if (childBounds.Contains(point))
                    {
                        // Check if click was in the bottom-right corner for resize (24x24 pixels)
                        var resizeRect = new Rect(childBounds.Right - 24, childBounds.Bottom - 24, 24, 24);
                        if (resizeRect.Contains(point))
                        {
                            _dragChild = child;
                            _dragVm = vm;
                            _dragStartPoint = point;
                            _dragOriginalSizeX = vm.SizeX;
                            _dragOriginalSizeY = vm.SizeY;
                            _isDragging = false;
                            _isResizing = true;
                        }
                        else
                        {
                            _dragChild = child;
                            _dragVm = vm;
                            _dragStartPoint = point;
                            _dragOriginalRow = vm.Row;
                            _dragOriginalCol = vm.Col;
                            _isDragging = false; // Not dragging until threshold is met
                            _isResizing = false;
                        }
                        e.Handled = true;
                        return;
                    }
                }
            }
        }

        protected override void OnPointerMoved(PointerEventArgs e)
        {
            base.OnPointerMoved(e);

            var point = e.GetPosition(this);

            if (_draggedPipeControl != null && _draggedPointIndex != -1)
            {
                var pipeVm = _draggedPipeControl.DataContext as PipeWidgetViewModel;
                if (pipeVm != null)
                {
                    double cellSize = CellWidth;
                    var gridPoints = pipeVm.GetAbsoluteGridPoints();

                    if (_draggedPointIndex >= 0 && _draggedPointIndex < gridPoints.Count)
                    {
                        double cursorGridX = (point.X - cellSize / 2) / cellSize;
                        double cursorGridY = (point.Y - cellSize / 2) / cellSize;

                        double dragAbsX = cursorGridX;
                        double dragAbsY = cursorGridY;

                        bool isShiftPressed = e.KeyModifiers.HasFlag(KeyModifiers.Shift);
                        if (isShiftPressed)
                        {
                            Point? refPoint = null;
                            if (_draggedPointIndex > 0)
                            {
                                refPoint = gridPoints[_draggedPointIndex - 1];
                            }
                            else if (gridPoints.Count > 1)
                            {
                                refPoint = gridPoints[1];
                            }

                            if (refPoint.HasValue)
                            {
                                double dx = cursorGridX - refPoint.Value.X;
                                double dy = cursorGridY - refPoint.Value.Y;
                                double dist = Math.Round(Math.Sqrt(dx * dx + dy * dy));
                                if (dist < 1.0) dist = 1.0;

                                double angle = Math.Atan2(dy, dx);
                                double angleStep = 15.0 * Math.PI / 180.0;
                                double snappedAngle = Math.Round(angle / angleStep) * angleStep;

                                dragAbsX = Math.Round(refPoint.Value.X + dist * Math.Cos(snappedAngle), 2);
                                dragAbsY = Math.Round(refPoint.Value.Y + dist * Math.Sin(snappedAngle), 2);
                            }
                        }
                        else
                        {
                            dragAbsX = Math.Round(cursorGridX);
                            dragAbsY = Math.Round(cursorGridY);
                        }

                        // Sibling snapped ports check (10px capture radius)
                        var siblingPipes = FindAllSiblingPipesFor(_draggedPipeControl);
                        foreach (var sibling in siblingPipes)
                        {
                            var otherVm = sibling.DataContext as PipeWidgetViewModel;
                            if (otherVm == null) continue;

                            var otherPoints = otherVm.GetAbsoluteGridPoints();
                            if (otherPoints.Count < 2) continue;

                            var ports = new[] { otherPoints[0], otherPoints[otherPoints.Count - 1] };
                            bool snapped = false;
                            foreach (var port in ports)
                            {
                                double dx = (dragAbsX - port.X) * cellSize;
                                double dy = (dragAbsY - port.Y) * cellSize;
                                double dist = Math.Sqrt(dx * dx + dy * dy);

                                if (dist <= 10.0) // 10px snap radius
                                {
                                    dragAbsX = port.X;
                                    dragAbsY = port.Y;
                                    snapped = true;
                                    break;
                                }
                            }
                            if (snapped) break;
                        }

                        gridPoints[_draggedPointIndex] = new Point(dragAbsX, dragAbsY);

                        var relativePoints = gridPoints.Select(p => new Point(p.X - pipeVm.Col, p.Y - pipeVm.Row)).ToList();
                        string newPointsStr = string.Join(";", relativePoints.Select(p => string.Format(System.Globalization.CultureInfo.InvariantCulture, "{0:0.##},{1:0.##}", p.X, p.Y)));

                        if (pipeVm.PipePoints != newPointsStr)
                        {
                            pipeVm.PipePoints = newPointsStr;
                        }
                    }
                }
                e.Handled = true;
                return;
            }

            if (_dragChild == null || _dragVm == null)
            {
                // Hover cursor support for resize handle
                if (IsDesignMode)
                {
                    bool overResize = false;
                    foreach (var child in Children)
                    {
                        if (child == _gridOverlay) continue;
                        if (child.DataContext is WidgetViewModelBase vm)
                        {
                            var childBounds = new Rect(
                                vm.Col * CellWidth, vm.Row * CellHeight,
                                vm.SizeX * CellWidth, vm.SizeY * CellHeight);

                            var resizeRect = new Rect(childBounds.Right - 24, childBounds.Bottom - 24, 24, 24);
                            if (resizeRect.Contains(point))
                            {
                                overResize = true;
                                break;
                            }
                        }
                    }

                    Cursor = overResize ? new Cursor(StandardCursorType.BottomRightCorner) : null;
                }
                else
                {
                    Cursor = null;
                }
                return;
            }

            var delta = point - _dragStartPoint;

            if (_isResizing)
            {
                // Resizing logic
                double newWidth = _dragOriginalSizeX * CellWidth + delta.X;
                double newHeight = _dragOriginalSizeY * CellHeight + delta.Y;

                int newSizeX = (int)Math.Max(1, Math.Round(newWidth / CellWidth));
                int newSizeY = (int)Math.Max(1, Math.Round(newHeight / CellHeight));

                if (_gridOverlay != null)
                {
                    _gridOverlay.ShowHighlight = true;
                    _gridOverlay.HighlightCol = _dragVm.Col;
                    _gridOverlay.HighlightRow = _dragVm.Row;
                    _gridOverlay.HighlightSizeX = newSizeX;
                    _gridOverlay.HighlightSizeY = newSizeY;
                    _gridOverlay.InvalidateVisual();
                }
            }
            else
            {
                // Require minimum movement to start drag (avoid accidental drags when clicking overlay buttons)
                if (!_isDragging)
                {
                    if (Math.Abs(delta.X) > 10 || Math.Abs(delta.Y) > 10)
                    {
                        _isDragging = true;
                    }
                    else
                    {
                        return;
                    }
                }

                // Update highlight position for move
                if (_gridOverlay != null)
                {
                    _gridOverlay.ShowHighlight = true;
                    _gridOverlay.HighlightCol = (int)Math.Max(0, Math.Floor(point.X / CellWidth));
                    _gridOverlay.HighlightRow = (int)Math.Max(0, Math.Floor(point.Y / CellHeight));
                    _gridOverlay.HighlightSizeX = _dragVm.SizeX;
                    _gridOverlay.HighlightSizeY = _dragVm.SizeY;
                    _gridOverlay.InvalidateVisual();
                }
            }
        }

        protected override void OnPointerReleased(PointerReleasedEventArgs e)
        {
            base.OnPointerReleased(e);

            if (_draggedPipeControl != null)
            {
                _draggedPipeControl = null;
                _draggedPointIndex = -1;
                e.Pointer.Capture(null);
                e.Handled = true;
                return;
            }

            if (_isResizing && _dragVm != null)
            {
                var point = e.GetPosition(this);
                var delta = point - _dragStartPoint;

                double newWidth = _dragOriginalSizeX * CellWidth + delta.X;
                double newHeight = _dragOriginalSizeY * CellHeight + delta.Y;

                int newSizeX = (int)Math.Max(1, Math.Round(newWidth / CellWidth));
                int newSizeY = (int)Math.Max(1, Math.Round(newHeight / CellHeight));

                _dragVm.SizeX = newSizeX;
                _dragVm.SizeY = newSizeY;

                _dragVm.OriginalConfig.Position.SizeX = newSizeX;
                _dragVm.OriginalConfig.Position.SizeY = newSizeY;

                InvalidateMeasure();
                InvalidateArrange();
            }
            else if (_isDragging && _dragVm != null)
            {
                var point = e.GetPosition(this);

                // Calculate new grid position
                int newCol = (int)Math.Max(0, Math.Floor(point.X / CellWidth));
                int newRow = (int)Math.Max(0, Math.Floor(point.Y / CellHeight));

                // Update the VM (which updates the UI via bindings)
                _dragVm.Row = newRow;
                _dragVm.Col = newCol;

                // Sync back to the OriginalConfig for JSON persistence
                _dragVm.OriginalConfig.Position.Row = newRow;
                _dragVm.OriginalConfig.Position.Col = newCol;

                // Force re-layout
                InvalidateMeasure();
                InvalidateArrange();
            }

            // Clear drag state and highlight
            ClearDragState();
        }

        protected override void OnPointerCaptureLost(PointerCaptureLostEventArgs e)
        {
            base.OnPointerCaptureLost(e);

            if (_draggedPipeControl != null)
            {
                _draggedPipeControl = null;
                _draggedPointIndex = -1;
            }

            // Cancel drag/resize — revert to original values
            if (_isResizing && _dragVm != null)
            {
                InvalidateMeasure();
                InvalidateArrange();
            }
            else if (_isDragging && _dragVm != null)
            {
                _dragVm.Row = _dragOriginalRow;
                _dragVm.Col = _dragOriginalCol;
                InvalidateMeasure();
                InvalidateArrange();
            }

            ClearDragState();
        }

        private void ClearDragState()
        {
            _dragChild = null;
            _dragVm = null;
            _isDragging = false;
            _isResizing = false;

            if (_gridOverlay != null)
            {
                _gridOverlay.ShowHighlight = false;
                _gridOverlay.InvalidateVisual();
            }
        }

        protected override Size MeasureOverride(Size availableSize)
        {
            double maxWidth = 0;
            double maxHeight = 0;

            // Always reserve at least a 6x6 grid in design mode for drop targets
            if (IsDesignMode)
            {
                maxWidth = Math.Max(maxWidth, 6 * CellWidth);
                maxHeight = Math.Max(maxHeight, 6 * CellHeight);
            }

            foreach (var child in Children)
            {
                if (child == _gridOverlay)
                {
                    // Grid overlay fills entire panel — measured later
                    continue;
                }

                if (child.DataContext is WidgetViewModelBase vm)
                {
                    double w = vm.SizeX * CellWidth;
                    double h = vm.SizeY * CellHeight;
                    child.Measure(new Size(w, h));

                    maxWidth = Math.Max(maxWidth, (vm.Col + vm.SizeX) * CellWidth);
                    maxHeight = Math.Max(maxHeight, (vm.Row + vm.SizeY) * CellHeight);
                }
                else
                {
                    child.Measure(availableSize);
                }
            }

            // Now measure the grid overlay to fill everything
            _gridOverlay?.Measure(new Size(maxWidth, maxHeight));

            return new Size(maxWidth, maxHeight);
        }

        protected override Size ArrangeOverride(Size finalSize)
        {
            // Arrange grid overlay to fill entire panel
            _gridOverlay?.Arrange(new Rect(0, 0, finalSize.Width, finalSize.Height));

            foreach (var child in Children)
            {
                if (child == _gridOverlay) continue;

                if (child.DataContext is WidgetViewModelBase vm)
                {
                    double x = vm.Col * CellWidth;
                    double y = vm.Row * CellHeight;
                    double w = vm.SizeX * CellWidth;
                    double h = vm.SizeY * CellHeight;
                    
                    child.Arrange(new Rect(x, y, w, h));
                }
                else
                {
                    child.Arrange(new Rect(new Point(), child.DesiredSize));
                }
            }

            return finalSize;
        }

        private PipeControl? FindPipeControlRecursive(Control control)
        {
            if (control is PipeControl pipe) return pipe;
            if (control is Panel panel)
            {
                foreach (var child in panel.Children)
                {
                    var res = FindPipeControlRecursive(child);
                    if (res != null) return res;
                }
            }
            else if (control is ContentControl cc && cc.Content is Control contentControl)
            {
                return FindPipeControlRecursive(contentControl);
            }
            else if (control is ContentPresenter cp && cp.Child is Control childControl)
            {
                return FindPipeControlRecursive(childControl);
            }
            else if (control is Border border && border.Child is Control borderChild)
            {
                return FindPipeControlRecursive(borderChild);
            }
            return null;
        }

        private List<PipeControl> FindAllSiblingPipesFor(PipeControl activePipe)
        {
            var list = new List<PipeControl>();
            foreach (var child in Children)
            {
                FindPipeControlsRecursive(child, list, activePipe);
            }
            return list;
        }

        private void FindPipeControlsRecursive(Control control, List<PipeControl> result, PipeControl activePipe)
        {
            if (control is PipeControl pipe)
            {
                if (pipe != activePipe)
                {
                    result.Add(pipe);
                }
                return;
            }

            if (control is Panel panel)
            {
                foreach (var child in panel.Children)
                {
                    FindPipeControlsRecursive(child, result, activePipe);
                }
            }
            else if (control is ContentControl cc && cc.Content is Control contentControl)
            {
                FindPipeControlsRecursive(contentControl, result, activePipe);
            }
            else if (control is ContentPresenter cp && cp.Child is Control childControl)
            {
                FindPipeControlsRecursive(childControl, result, activePipe);
            }
            else if (control is Border border && border.Child is Control borderChild)
            {
                FindPipeControlsRecursive(borderChild, result, activePipe);
            }
            else if (control is Grid grid)
            {
                foreach (var child in grid.Children)
                {
                    FindPipeControlsRecursive(child, result, activePipe);
                }
            }
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
    }
}