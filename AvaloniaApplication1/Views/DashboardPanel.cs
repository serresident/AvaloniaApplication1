using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.VisualTree;
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
        public double HighlightCol { get; set; }
        public double HighlightRow { get; set; }
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

    /// <summary>
    /// A lightweight control drawn on top of the widgets to show selection highlight.
    /// Added as last child of DashboardPanel when in design mode.
    /// </summary>
    public class SelectionOverlay : Control
    {
        private readonly DashboardPanel _panel;

        public SelectionOverlay(DashboardPanel panel)
        {
            _panel = panel;
            IsHitTestVisible = false;
        }

        public override void Render(DrawingContext context)
        {
            base.Render(context);

            if (_panel.IsDesignMode && _panel.SelectedVm != null)
            {
                double x = _panel.SelectedVm.Col * _panel.CellWidth;
                double y = _panel.SelectedVm.Row * _panel.CellHeight;
                double w = _panel.SelectedVm.SizeX * _panel.CellWidth;
                double h = _panel.SelectedVm.SizeY * _panel.CellHeight;

                var fillBrush = new SolidColorBrush(Color.FromArgb(30, 0, 122, 255));
                var borderPen = new Pen(new SolidColorBrush(Color.FromRgb(0, 122, 255)), 2.0);
                
                context.DrawRectangle(fillBrush, borderPen, new Rect(x, y, w, h), 4, 4);

                var handleBrush = Brushes.White;
                var handlePen = new Pen(new SolidColorBrush(Color.FromRgb(0, 122, 255)), 1.5);
                context.DrawEllipse(handleBrush, handlePen, new Point(x, y), 4, 4);
                context.DrawEllipse(handleBrush, handlePen, new Point(x + w, y), 4, 4);
                context.DrawEllipse(handleBrush, handlePen, new Point(x, y + h), 4, 4);
                context.DrawEllipse(handleBrush, handlePen, new Point(x + w, y + h), 4, 4);
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
        private double _dragOriginalRow;
        private double _dragOriginalCol;
        private WidgetViewModelBase? _selectedVm;
        public WidgetViewModelBase? SelectedVm
        {
            get => _selectedVm;
            set
            {
                if (_selectedVm != value)
                {
                    if (_selectedVm != null) _selectedVm.IsSelected = false;
                    _selectedVm = value;
                    if (_selectedVm != null) _selectedVm.IsSelected = true;
                    InvalidateVisual();
                    _selectionOverlay?.InvalidateVisual();
                }
            }
        }
        private bool _isDragging;
        private bool _isResizing;
        private int _dragOriginalSizeX;
        private int _dragOriginalSizeY;

        // Pipe drag state
        private PipeControl? _draggedPipeControl;
        private int _draggedPointIndex = -1;

        // Grid overlay control
        private GridOverlay? _gridOverlay;
        private SelectionOverlay? _selectionOverlay;
        private bool _isManagingOverlays;

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
            Focusable = true;
            foreach (var child in Children)
            {
                child.DataContextChanged += Child_DataContextChanged;
                SubscribeVm(child.DataContext as WidgetViewModelBase);
            }

            Children.CollectionChanged += (s, e) =>
            {
                if (_isManagingOverlays) return;
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
                        var vm = child.DataContext as WidgetViewModelBase;
                        SubscribeVm(vm);
                        if (vm != null && vm.IsSelected)
                        {
                            SelectedVm = vm;
                        }
                        child.InvalidateMeasure();
                        child.InvalidateVisual();
                    }
                }

                // Clean orphaned selection reference
                if (SelectedVm != null)
                {
                    bool found = false;
                    foreach (var child in Children)
                    {
                        if (child.DataContext == SelectedVm)
                        {
                            found = true;
                            break;
                        }
                    }
                    if (!found)
                    {
                        SelectedVm = null;
                    }
                }
                Avalonia.Threading.Dispatcher.UIThread.Post(() => EnsureSelectionOverlayOnTop());
                InvalidateMeasure();
                InvalidateArrange();
                InvalidateVisual();
            };
        }

        private void Child_DataContextChanged(object? sender, EventArgs e)
        {
            if (sender is Control child)
            {
                var vm = child.DataContext as WidgetViewModelBase;
                SubscribeVm(vm);
                if (vm != null && vm.IsSelected)
                {
                    SelectedVm = vm;
                }

                // Clean orphaned selection reference
                if (SelectedVm != null)
                {
                    bool found = false;
                    foreach (var c in Children)
                    {
                        if (c.DataContext == SelectedVm)
                        {
                            found = true;
                            break;
                        }
                    }
                    if (!found)
                    {
                        SelectedVm = null;
                    }
                }

                child.InvalidateMeasure();
                child.InvalidateVisual();
                InvalidateMeasure();
                InvalidateArrange();
                InvalidateVisual();
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
                InvalidateVisual();
                _selectionOverlay?.InvalidateVisual();
            }
        }

        private void UpdateGridOverlay()
        {
            if (_isManagingOverlays) return;
            _isManagingOverlays = true;
            try
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
                    if (_selectionOverlay == null)
                    {
                        _selectionOverlay = new SelectionOverlay(this)
                        {
                            IsHitTestVisible = false
                        };
                        Children.Add(_selectionOverlay);
                    }
                }
                else
                {
                    if (_gridOverlay != null)
                    {
                        Children.Remove(_gridOverlay);
                        _gridOverlay = null;
                    }
                    if (_selectionOverlay != null)
                    {
                        Children.Remove(_selectionOverlay);
                        _selectionOverlay = null;
                    }
                }
            }
            finally
            {
                _isManagingOverlays = false;
            }
        }

        private void EnsureSelectionOverlayOnTop()
        {
            if (IsDesignMode && _selectionOverlay != null)
            {
                int index = Children.IndexOf(_selectionOverlay);
                if (index >= 0 && index < Children.Count - 1)
                {
                    if (_isManagingOverlays) return;
                    _isManagingOverlays = true;
                    try
                    {
                        Children.Remove(_selectionOverlay);
                        Children.Add(_selectionOverlay);
                    }
                    finally
                    {
                        _isManagingOverlays = false;
                    }
                }
            }
        }

        protected override void OnPointerPressed(PointerPressedEventArgs e)
        {
            base.OnPointerPressed(e);

            if (e.Handled || !IsDesignMode) return;

            Focus();

            var point = e.GetPosition(this);
            bool clickedWidget = false;

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
                if (child == _gridOverlay || child == _selectionOverlay) continue; // Skip overlays

                if (child.DataContext is WidgetViewModelBase vm)
                {
                    var childBounds = new Rect(
                        vm.Col * CellWidth, vm.Row * CellHeight,
                        vm.SizeX * CellWidth, vm.SizeY * CellHeight);

                    if (childBounds.Contains(point))
                    {
                        bool wasSelected = SelectedVm == vm;
                        SelectedVm = vm;
                        clickedWidget = true;

                        // Правый клик (ПКМ) — выделяем виджет и программно открываем контекстное меню
                        if (e.GetCurrentPoint(this).Properties.IsRightButtonPressed)
                        {
                            var dragHandle = FindDragHandleRecursive(child);
                            if (dragHandle != null && dragHandle.ContextMenu != null)
                            {
                                dragHandle.ContextMenu.PlacementTarget = dragHandle;
                                dragHandle.ContextMenu.Open(dragHandle);
                            }
                            e.Handled = true;
                            return;
                        }

                        // Check if click was in the bottom-right corner for resize (60x60 pixels)
                        // ONLY allow resize if the widget was ALREADY selected!
                        var resizeRect = new Rect(childBounds.Right - 60, childBounds.Bottom - 60, 60, 60);
                        if (wasSelected && resizeRect.Contains(point))
                        {
                            _dragChild = child;
                            _dragVm = vm;
                            _dragStartPoint = point;
                            _dragOriginalSizeX = vm.SizeX;
                            _dragOriginalSizeY = vm.SizeY;
                            _isDragging = false;
                            _isResizing = true;
                            
                            // Capture pointer for robust drag/resize tracking
                            e.Pointer.Capture(this);
                            e.Handled = true;
                            return;
                        }
                        else if (IsDragHandle(e.Source))
                        {
                            _dragChild = child;
                            _dragVm = vm;
                            _dragStartPoint = point;
                            _dragOriginalRow = vm.Row;
                            _dragOriginalCol = vm.Col;
                            _isDragging = false; // Not dragging until threshold is met
                            _isResizing = false;
                            
                            // Capture pointer for robust drag/resize tracking
                            e.Pointer.Capture(this);
                            e.Handled = true;
                            return;
                        }
                        else
                        {
                            e.Handled = true;
                            return;
                        }
                    }
                }
            }

            if (!clickedWidget)
            {
                SelectedVm = null;
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
                        bool snapped = false;
                        foreach (var sibling in siblingPipes)
                        {
                            var otherVm = sibling.DataContext as PipeWidgetViewModel;
                            if (otherVm == null) continue;

                            var otherPoints = otherVm.GetAbsoluteGridPoints();
                            if (otherPoints.Count < 2) continue;

                            var ports = new[] { otherPoints[0], otherPoints[otherPoints.Count - 1] };
                            foreach (var port in ports)
                            {
                                double dx = (dragAbsX - port.X) * cellSize;
                                double dy = (dragAbsY - port.Y) * cellSize;
                                double dist = Math.Sqrt(dx * dx + dy * dy);

                                if (dist <= 20.0) // 20px snap radius (was 10px)
                                {
                                    dragAbsX = port.X;
                                    dragAbsY = port.Y;
                                    snapped = true;
                                    break;
                                }
                            }
                            if (snapped) break;
                        }

                        // Valve/Pump snapped ports check if not snapped to sibling pipe (20px snap radius)
                        if (!snapped)
                        {
                            foreach (var child in Children)
                            {
                                if (child == _gridOverlay || child == _selectionOverlay) continue;
                                if (child.DataContext is WidgetViewModelBase widgetVm && 
                                    (string.Equals(widgetVm.Type, "Valve", StringComparison.OrdinalIgnoreCase) ||
                                     string.Equals(widgetVm.Type, "Pump", StringComparison.OrdinalIgnoreCase)))
                                {
                                    int rotation = 0;
                                    bool isVertical = false;
                                    var rotProp = widgetVm.GetType().GetProperty("Rotation");
                                    if (rotProp != null) rotation = (int)(rotProp.GetValue(widgetVm) ?? 0);
                                    var vertProp = widgetVm.GetType().GetProperty("IsVertical");
                                    if (vertProp != null) isVertical = (bool)(vertProp.GetValue(widgetVm) ?? false);

                                    int finalRotation = rotation;
                                    if (finalRotation == 0 && isVertical) finalRotation = 90;
                                    bool isFlowVertical = (finalRotation == 90 || finalRotation == 270);

                                    double sizeX = widgetVm.SizeX;
                                    double sizeY = widgetVm.SizeY;

                                    // Flanges in grid coordinate units (flange boundaries are aligned to cell edges)
                                    // Pipe endpoints are centered in cells, so we subtract 0.5 to align cell center to cell edge
                                    double pX1 = isFlowVertical ? (widgetVm.Col + sizeX / 2.0 - 0.5) : (widgetVm.Col - 0.5);
                                    double pY1 = isFlowVertical ? (widgetVm.Row - 0.5) : (widgetVm.Row + sizeY / 2.0 - 0.5);

                                    double pX2 = isFlowVertical ? (widgetVm.Col + sizeX / 2.0 - 0.5) : (widgetVm.Col + sizeX - 0.5);
                                    double pY2 = isFlowVertical ? (widgetVm.Row + sizeY - 0.5) : (widgetVm.Row + sizeY / 2.0 - 0.5);

                                    // Check Port 1
                                    double dx1 = (dragAbsX - pX1) * cellSize;
                                    double dy1 = (dragAbsY - pY1) * cellSize;
                                    double dist1 = Math.Sqrt(dx1 * dx1 + dy1 * dy1);
                                    if (dist1 <= 20.0) // 20px snap radius (was 10px)
                                    {
                                        dragAbsX = pX1;
                                        dragAbsY = pY1;
                                        break;
                                    }

                                    // Check Port 2
                                    double dx2 = (dragAbsX - pX2) * cellSize;
                                    double dy2 = (dragAbsY - pY2) * cellSize;
                                    double dist2 = Math.Sqrt(dx2 * dx2 + dy2 * dy2);
                                    if (dist2 <= 20.0) // 20px snap radius (was 10px)
                                    {
                                        dragAbsX = pX2;
                                        dragAbsY = pY2;
                                        break;
                                    }
                                }
                            }
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
                        if (child == _gridOverlay || child == _selectionOverlay) continue;
                        if (child.DataContext is WidgetViewModelBase vm)
                        {
                            // ONLY show resize cursor if this widget is the currently selected one!
                            if (SelectedVm == vm)
                            {
                                var childBounds = new Rect(
                                    vm.Col * CellWidth, vm.Row * CellHeight,
                                    vm.SizeX * CellWidth, vm.SizeY * CellHeight);

                                var resizeRect = new Rect(childBounds.Right - 60, childBounds.Bottom - 60, 60, 60);
                                if (resizeRect.Contains(point))
                                {
                                    overResize = true;
                                    break;
                                }
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
                    _gridOverlay.HighlightCol = (int)Math.Round(_dragVm.Col);
                    _gridOverlay.HighlightRow = (int)Math.Round(_dragVm.Row);
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
                    GetSnappedPosition(_dragVm, point, out double snappedCol, out double snappedRow);
                    _gridOverlay.HighlightCol = snappedCol;
                    _gridOverlay.HighlightRow = snappedRow;
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

                GetSnappedPosition(_dragVm, point, out double newCol, out double newRow);

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

            // Clear drag state FIRST so OnPointerCaptureLost doesn't revert anything
            ClearDragState();

            // Release pointer capture
            if (e.Pointer.Captured == this)
            {
                e.Pointer.Capture(null);
            }
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
                if (child == _gridOverlay || child == _selectionOverlay)
                {
                    // Overlay fills entire panel — measured later
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

            // Now measure the overlays to fill everything
            _gridOverlay?.Measure(new Size(maxWidth, maxHeight));
            _selectionOverlay?.Measure(new Size(maxWidth, maxHeight));

            return new Size(maxWidth, maxHeight);
        }

        protected override Size ArrangeOverride(Size finalSize)
        {
            // Arrange overlays to fill entire panel
            _gridOverlay?.Arrange(new Rect(0, 0, finalSize.Width, finalSize.Height));
            _selectionOverlay?.Arrange(new Rect(0, 0, finalSize.Width, finalSize.Height));

            foreach (var child in Children)
            {
                if (child == _gridOverlay || child == _selectionOverlay) continue;

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

        protected override void OnKeyDown(Avalonia.Input.KeyEventArgs e)
        {
            base.OnKeyDown(e);

            if (!IsDesignMode || SelectedVm == null) return;

            bool isShiftPressed = e.KeyModifiers.HasFlag(Avalonia.Input.KeyModifiers.Shift);
            double stepX = isShiftPressed ? 1.0 : (1.0 / CellWidth);
            double stepY = isShiftPressed ? 1.0 : (1.0 / CellHeight);

            bool moved = false;
            if (e.Key == Avalonia.Input.Key.Left)
            {
                SelectedVm.Col -= stepX;
                SelectedVm.OriginalConfig.Position.Col = SelectedVm.Col;
                moved = true;
            }
            else if (e.Key == Avalonia.Input.Key.Right)
            {
                SelectedVm.Col += stepX;
                SelectedVm.OriginalConfig.Position.Col = SelectedVm.Col;
                moved = true;
            }
            else if (e.Key == Avalonia.Input.Key.Up)
            {
                SelectedVm.Row -= stepY;
                SelectedVm.OriginalConfig.Position.Row = SelectedVm.Row;
                moved = true;
            }
            else if (e.Key == Avalonia.Input.Key.Down)
            {
                SelectedVm.Row += stepY;
                SelectedVm.OriginalConfig.Position.Row = SelectedVm.Row;
                moved = true;
            }

            if (moved)
            {
                e.Handled = true;
                InvalidateMeasure();
                InvalidateArrange();
                _selectionOverlay?.InvalidateVisual();
            }
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

        private void GetSnappedPosition(WidgetViewModelBase vm, Point pointer, out double snappedCol, out double snappedRow)
        {
            // Initial candidate column and row based on mouse pointer
            double dragX = pointer.X - _dragStartPoint.X + _dragOriginalCol * CellWidth;
            double dragY = pointer.Y - _dragStartPoint.Y + _dragOriginalRow * CellHeight;

            double candCol = Math.Max(0, dragX / CellWidth);
            double candRow = Math.Max(0, dragY / CellHeight);

            snappedCol = Math.Round(candCol);
            snappedRow = Math.Round(candRow);

            // Snapping is active for Valve and Pump type widgets
            bool isValve = string.Equals(vm.Type, "Valve", StringComparison.OrdinalIgnoreCase);
            bool isPump = string.Equals(vm.Type, "Pump", StringComparison.OrdinalIgnoreCase);
            if (!isValve && !isPump) return;

            // Get Valve specific properties
            int rotation = 0;
            bool isVertical = false;
            var rotProp = vm.GetType().GetProperty("Rotation");
            if (rotProp != null) rotation = (int)(rotProp.GetValue(vm) ?? 0);
            var vertProp = vm.GetType().GetProperty("IsVertical");
            if (vertProp != null) isVertical = (bool)(vertProp.GetValue(vm) ?? false);

            int finalRotation = rotation;
            if (finalRotation == 0 && isVertical) finalRotation = 90;
            bool isFlowVertical = (finalRotation == 90 || finalRotation == 270);

            double sizeX = vm.SizeX;
            double sizeY = vm.SizeY;

            // Define candidate ports relative to the candidate top-left corner
            // In cells:
            double relX1 = isFlowVertical ? (sizeX / 2.0) : 0.0;
            double relY1 = isFlowVertical ? 0.0 : (sizeY / 2.0);

            double relX2 = isFlowVertical ? (sizeX / 2.0) : sizeX;
            double relY2 = isFlowVertical ? sizeY : (sizeY / 2.0);

            double snapRadius = 20.0; // pixels (was 15.0)
            bool snapped = false;

            // 1. Try to snap ports to any pipe vertex (2D snapping)
            foreach (var child in Children)
            {
                if (child == _gridOverlay || child == _selectionOverlay) continue;
                if (child.DataContext is PipeWidgetViewModel pipeVm)
                {
                    var points = pipeVm.GetAbsoluteGridPoints();
                    foreach (var ep in points)
                    {
                        // Check Port 1
                        double p1X = (candCol + relX1) * CellWidth;
                        double p1Y = (candRow + relY1) * CellHeight;
                        double epX = ep.X * CellWidth + CellWidth / 2.0;
                        double epY = ep.Y * CellHeight + CellHeight / 2.0;

                        double dist1 = Math.Sqrt(Math.Pow(p1X - epX, 2) + Math.Pow(p1Y - epY, 2));
                        if (dist1 <= snapRadius)
                        {
                            // Snap Port 1 to ep
                            snappedCol = ep.X + 0.5 - relX1;
                            snappedRow = ep.Y + 0.5 - relY1;
                            snapped = true;
                            break;
                        }

                        // Check Port 2
                        double p2X = (candCol + relX2) * CellWidth;
                        double p2Y = (candRow + relY2) * CellHeight;

                        double dist2 = Math.Sqrt(Math.Pow(p2X - epX, 2) + Math.Pow(p2Y - epY, 2));
                        if (dist2 <= snapRadius)
                        {
                            // Snap Port 2 to ep
                            snappedCol = ep.X + 0.5 - relX2;
                            snappedRow = ep.Y + 0.5 - relY2;
                            snapped = true;
                            break;
                        }
                    }
                }
                if (snapped) break;
            }

            // 2. If not snapped to a vertex, try to snap to horizontal/vertical segments (1D snapping perpendicular to flow)
            if (!snapped)
            {
                foreach (var child in Children)
                {
                    if (child == _gridOverlay || child == _selectionOverlay) continue;
                    if (child.DataContext is PipeWidgetViewModel pipeVm)
                    {
                        var points = pipeVm.GetAbsoluteGridPoints();
                        for (int i = 0; i < points.Count - 1; i++)
                        {
                            var pt1 = points[i];
                            var pt2 = points[i + 1];

                            // Horizontal segment (align Y of horizontal valve flow to horizontal pipe)
                            if (Math.Abs(pt1.Y - pt2.Y) < 0.01 && !isFlowVertical)
                            {
                                double pY = (candRow + relY1) * CellHeight;
                                double segY = pt1.Y * CellHeight + CellHeight / 2.0;
                                double distY = Math.Abs(pY - segY);

                                if (distY <= snapRadius)
                                {
                                    // Check if valve overlaps or is close horizontally to the segment
                                    double minX = Math.Min(pt1.X, pt2.X) - 0.5;
                                    double maxX = Math.Max(pt1.X, pt2.X) + 0.5;
                                    double valveCenterCol = candCol + sizeX / 2.0;

                                    if (valveCenterCol >= minX && valveCenterCol <= maxX)
                                    {
                                        snappedRow = pt1.Y + 0.5 - relY1;
                                        snapped = true;
                                        break;
                                    }
                                }
                            }
                            // Vertical segment (align X of vertical valve flow to vertical pipe)
                            else if (Math.Abs(pt1.X - pt2.X) < 0.01 && isFlowVertical)
                            {
                                double pX = (candCol + relX1) * CellWidth;
                                double segX = pt1.X * CellWidth + CellWidth / 2.0;
                                double distX = Math.Abs(pX - segX);

                                if (distX <= snapRadius)
                                {
                                    // Check if valve overlaps or is close vertically to the segment
                                    double minY = Math.Min(pt1.Y, pt2.Y) - 0.5;
                                    double maxY = Math.Max(pt1.Y, pt2.Y) + 0.5;
                                    double valveCenterRow = candRow + sizeY / 2.0;

                                    if (valveCenterRow >= minY && valveCenterRow <= maxY)
                                    {
                                        snappedCol = pt1.X + 0.5 - relX1;
                                        snapped = true;
                                        break;
                                    }
                                }
                            }
                        }
                    }
                    if (snapped) break;
                }
            }

            // Keep in bounds
            snappedCol = Math.Max(0.0, snappedCol);
            snappedRow = Math.Max(0.0, snappedRow);
        }

        private bool IsDragHandle(object? source)
        {
            if (source is Avalonia.Visual visual)
            {
                var current = visual;
                while (current != null)
                {
                    if (current is Control ctrl && ctrl.Name == "DragHandle")
                        return true;
                    current = current.GetVisualParent();
                }
            }
            return false;
        }

        private Border? FindDragHandleRecursive(Avalonia.Visual? visual)
        {
            if (visual == null) return null;
            if (visual is Border border && border.Name == "DragHandle") return border;
            foreach (var child in visual.GetVisualChildren())
            {
                var result = FindDragHandleRecursive(child);
                if (result != null) return result;
            }
            return null;
        }
    }
}