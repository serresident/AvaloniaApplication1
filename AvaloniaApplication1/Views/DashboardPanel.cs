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
    public class DashboardPanel : Panel
    {
        public static readonly StyledProperty<double> CellWidthProperty =
            AvaloniaProperty.Register<DashboardPanel, double>(nameof(CellWidth), 150.0, coerce: CoerceCellSize);

        public static readonly StyledProperty<double> CellHeightProperty =
            AvaloniaProperty.Register<DashboardPanel, double>(nameof(CellHeight), 150.0, coerce: CoerceCellSize);

        public static readonly AttachedProperty<double> ColProperty =
            AvaloniaProperty.RegisterAttached<DashboardPanel, Control, double>("Col", 0.0);

        public static readonly AttachedProperty<double> RowProperty =
            AvaloniaProperty.RegisterAttached<DashboardPanel, Control, double>("Row", 0.0);

        public static readonly AttachedProperty<int> SizeXProperty =
            AvaloniaProperty.RegisterAttached<DashboardPanel, Control, int>("SizeX", 1);

        public static readonly AttachedProperty<int> SizeYProperty =
            AvaloniaProperty.RegisterAttached<DashboardPanel, Control, int>("SizeY", 1);

        public static double GetCol(Control element) => element.GetValue(ColProperty);
        public static void SetCol(Control element, double value) => element.SetValue(ColProperty, value);

        public static double GetRow(Control element) => element.GetValue(RowProperty);
        public static void SetRow(Control element, double value) => element.SetValue(RowProperty, value);

        public static int GetSizeX(Control element) => element.GetValue(SizeXProperty);
        public static void SetSizeX(Control element, int value) => element.SetValue(SizeXProperty, value);

        public static int GetSizeY(Control element) => element.GetValue(SizeYProperty);
        public static void SetSizeY(Control element, int value) => element.SetValue(SizeYProperty, value);

        private static double CoerceCellSize(AvaloniaObject inst, double val) => AvaloniaApplication1.Views.DashboardPanelHelpers.GridMathHelper.CoerceCellSize(inst, val);

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
                    if (_selectedVm != null)
                    {
                        _selectedVm.IsSelected = false;
                        if (_selectedVm is PipeWidgetViewModel oldPipe)
                        {
                            oldPipe.IsEditingVertices = false;
                        }
                    }
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

        private struct ConnectedPipePoint
        {
            public PipeWidgetViewModel PipeVm;
            public int PointIndex;
            public bool IsPort1;
        }
        private List<ConnectedPipePoint> _connectedPipePoints = new();

        // Grid overlay control
        private GridOverlay? _gridOverlay;
        private SelectionOverlay? _selectionOverlay;
        private bool _isManagingOverlays;

        static DashboardPanel()
        {
            AffectsMeasure<DashboardPanel>(CellWidthProperty, CellHeightProperty, ColProperty, RowProperty, SizeXProperty, SizeYProperty);
            AffectsArrange<DashboardPanel>(CellWidthProperty, CellHeightProperty, ColProperty, RowProperty, SizeXProperty, SizeYProperty);
            IsDesignModeProperty.Changed.AddClassHandler<DashboardPanel>((panel, args) =>
            {
                panel.UpdateGridOverlay();
            });
            ColProperty.Changed.AddClassHandler<Control>((control, args) =>
            {
                if (control.GetVisualParent() is DashboardPanel panel)
                {
                    panel._selectionOverlay?.InvalidateVisual();
                }
            });
            RowProperty.Changed.AddClassHandler<Control>((control, args) =>
            {
                if (control.GetVisualParent() is DashboardPanel panel)
                {
                    panel._selectionOverlay?.InvalidateVisual();
                }
            });
            SizeXProperty.Changed.AddClassHandler<Control>((control, args) =>
            {
                if (control.GetVisualParent() is DashboardPanel panel)
                {
                    panel._selectionOverlay?.InvalidateVisual();
                }
            });
            SizeYProperty.Changed.AddClassHandler<Control>((control, args) =>
            {
                if (control.GetVisualParent() is DashboardPanel panel)
                {
                    panel._selectionOverlay?.InvalidateVisual();
                }
            });
        }

        public DashboardPanel()
        {
            Focusable = true;
            foreach (var child in Children)
            {
                child.DataContextChanged += Child_DataContextChanged;
            }

            Children.CollectionChanged += (s, e) =>
            {
                if (_isManagingOverlays) return;
                if (e.OldItems != null)
                {
                    foreach (Control child in e.OldItems)
                    {
                        child.DataContextChanged -= Child_DataContextChanged;
                    }
                }
                if (e.NewItems != null)
                {
                    foreach (Control child in e.NewItems)
                    {
                        child.DataContextChanged += Child_DataContextChanged;
                        var vm = child.DataContext as WidgetViewModelBase;
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
                Avalonia.Threading.Dispatcher.UIThread.Post(() => EnsureOverlaysState());
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

        private void UpdateGridOverlay()
        {
            if (IsDesignMode)
            {
                EnsureOverlaysState();
            }
            else
            {
                if (_isManagingOverlays) return;
                _isManagingOverlays = true;
                try
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
                finally
                {
                    _isManagingOverlays = false;
                }
            }
        }

        protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
        {
            base.OnAttachedToVisualTree(e);
            UpdateGridOverlay();
        }

        private void EnsureOverlaysState()
        {
            if (!IsDesignMode) return;
            if (_isManagingOverlays) return;
            
            _isManagingOverlays = true;
            try
            {
                // Ensure grid overlay is at index 0
                if (_gridOverlay != null)
                {
                    int gridIdx = Children.IndexOf(_gridOverlay);
                    if (gridIdx != 0)
                    {
                        if (gridIdx > 0)
                        {
                            Children.RemoveAt(gridIdx);
                        }
                        Children.Insert(0, _gridOverlay);
                    }
                }
                else
                {
                    _gridOverlay = new GridOverlay
                    {
                        CellWidth = CellWidth,
                        CellHeight = CellHeight,
                        IsHitTestVisible = false
                    };
                    Children.Insert(0, _gridOverlay);
                }

                // Ensure selection overlay is at the end
                if (_selectionOverlay != null)
                {
                    int selIdx = Children.IndexOf(_selectionOverlay);
                    if (selIdx != Children.Count - 1)
                    {
                        if (selIdx >= 0)
                        {
                            Children.RemoveAt(selIdx);
                        }
                        Children.Add(_selectionOverlay);
                    }
                }
                else
                {
                    _selectionOverlay = new SelectionOverlay(this)
                    {
                        IsHitTestVisible = false
                    };
                    Children.Add(_selectionOverlay);
                }
            }
            finally
            {
                _isManagingOverlays = false;
            }
        }

        protected override void OnPointerPressed(PointerPressedEventArgs e)
        {
            PipeControl.CloseActiveMenu();

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
                    if (!pipeVm.IsEditingVertices) continue;

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
                                    pipeVm.IsSuppressingNormalization = true;
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
                        if (!pipeVm.IsEditingVertices) continue;

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
                    double col = GetCol(child);
                    double row = GetRow(child);
                    int sizeX = GetSizeX(child);
                    int sizeY = GetSizeY(child);

                    var childBounds = new Rect(
                        col * CellWidth, row * CellHeight,
                        sizeX * CellWidth, sizeY * CellHeight);

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

                        // Check if click was in the bottom-right corner for resize (20x20 pixels)
                        // ONLY allow resize if the widget was ALREADY selected!
                        var resizeRect = new Rect(childBounds.Right - 20, childBounds.Bottom - 20, 20, 20);
                        if (wasSelected && resizeRect.Contains(point))
                        {
                            _dragChild = child;
                            _dragVm = vm;
                            _dragStartPoint = point;
                            _dragOriginalSizeX = sizeX;
                            _dragOriginalSizeY = sizeY;
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
                            _dragOriginalRow = row;
                            _dragOriginalCol = col;
                            _isDragging = false; // Not dragging until threshold is met
                            _isResizing = false;
                            
                            // Collect connected pipes for rubber-banding
                            _connectedPipePoints.Clear();
                            bool isValve = string.Equals(vm.Type, "Valve", StringComparison.OrdinalIgnoreCase);
                            bool isPump = string.Equals(vm.Type, "Pump", StringComparison.OrdinalIgnoreCase);
                            if (isValve || isPump)
                            {
                                var (p1, p2) = GetVisualPortsInGrid(child, vm);

                                foreach (var otherChild in Children)
                                {
                                    if (otherChild == _gridOverlay || otherChild == _selectionOverlay) continue;
                                    if (otherChild.DataContext is PipeWidgetViewModel pipeVm)
                                    {
                                        var points = pipeVm.GetAbsoluteGridPoints();
                                        for (int i = 0; i < points.Count; i++)
                                        {
                                            var pt = points[i];
                                            if (Math.Abs(pt.X - p1.X) < 0.01 && Math.Abs(pt.Y - p1.Y) < 0.01)
                                            {
                                                _connectedPipePoints.Add(new ConnectedPipePoint
                                                {
                                                    PipeVm = pipeVm,
                                                    PointIndex = i,
                                                    IsPort1 = true
                                                });
                                            }
                                            else if (Math.Abs(pt.X - p2.X) < 0.01 && Math.Abs(pt.Y - p2.Y) < 0.01)
                                            {
                                                _connectedPipePoints.Add(new ConnectedPipePoint
                                                {
                                                    PipeVm = pipeVm,
                                                    PointIndex = i,
                                                    IsPort1 = false
                                                });
                                            }
                                        }
                                    }
                                }
                            }

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
                                    var (p1, p2) = GetVisualPortsInGrid(child, widgetVm);

                                    // Check Port 1
                                    double dx1 = (dragAbsX - p1.X) * cellSize;
                                    double dy1 = (dragAbsY - p1.Y) * cellSize;
                                    double dist1 = Math.Sqrt(dx1 * dx1 + dy1 * dy1);
                                    if (dist1 <= 20.0) // 20px snap radius
                                    {
                                        dragAbsX = p1.X;
                                        dragAbsY = p1.Y;
                                        snapped = true;
                                        break;
                                    }

                                    // Check Port 2
                                    double dx2 = (dragAbsX - p2.X) * cellSize;
                                    double dy2 = (dragAbsY - p2.Y) * cellSize;
                                    double dist2 = Math.Sqrt(dx2 * dx2 + dy2 * dy2);
                                    if (dist2 <= 20.0) // 20px snap radius
                                    {
                                        dragAbsX = p2.X;
                                        dragAbsY = p2.Y;
                                        snapped = true;
                                        break;
                                    }
                                }
                            }
                        }

                        gridPoints[_draggedPointIndex] = new Point(dragAbsX, dragAbsY);

                        double pipeCol = pipeVm.Col;
                        double pipeRow = pipeVm.Row;

                        var relativePoints = gridPoints.Select(p => new Point(p.X - pipeCol, p.Y - pipeRow)).ToList();
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
                                double col = GetCol(child);
                                double row = GetRow(child);
                                int sizeX = GetSizeX(child);
                                int sizeY = GetSizeY(child);
                                var childBounds = new Rect(
                                    col * CellWidth, row * CellHeight,
                                    sizeX * CellWidth, sizeY * CellHeight);

                                var resizeRect = new Rect(childBounds.Right - 20, childBounds.Bottom - 20, 20, 20);
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
                    _gridOverlay.HighlightCol = (int)Math.Round(GetCol(_dragChild));
                    _gridOverlay.HighlightRow = (int)Math.Round(GetRow(_dragChild));
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
                    GetSnappedPosition(_dragChild, point, out double snappedCol, out double snappedRow);
                    _gridOverlay.HighlightCol = snappedCol;
                    _gridOverlay.HighlightRow = snappedRow;
                    _gridOverlay.HighlightSizeX = GetSizeX(_dragChild);
                    _gridOverlay.HighlightSizeY = GetSizeY(_dragChild);
                    _gridOverlay.InvalidateVisual();
                }
            }
        }

        protected override void OnPointerReleased(PointerReleasedEventArgs e)
        {
            base.OnPointerReleased(e);

            if (_draggedPipeControl != null)
            {
                var pipeVm = _draggedPipeControl.DataContext as PipeWidgetViewModel;
                if (pipeVm != null)
                {
                    pipeVm.IsSuppressingNormalization = false;
                    pipeVm.NormalizePointsAndSize();
                }
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
            else if (_isDragging && _dragVm != null && _dragChild != null)
            {
                var point = e.GetPosition(this);

                GetSnappedPosition(_dragChild, point, out double newCol, out double newRow);

                // Update connected pipes for rubber-banding
                if (_connectedPipePoints.Count > 0)
                {
                    bool isValve = string.Equals(_dragVm.Type, "Valve", StringComparison.OrdinalIgnoreCase);
                    bool isPump = string.Equals(_dragVm.Type, "Pump", StringComparison.OrdinalIgnoreCase);
                    if (isValve || isPump)
                    {
                        double deltaCol = newCol - _dragOriginalCol;
                        double deltaRow = newRow - _dragOriginalRow;

                        foreach (var conn in _connectedPipePoints)
                        {
                            var pipePoints = conn.PipeVm.GetAbsoluteGridPoints();
                            if (conn.PointIndex >= 0 && conn.PointIndex < pipePoints.Count)
                            {
                                var oldPt = pipePoints[conn.PointIndex];
                                double targetX = oldPt.X + deltaCol;
                                double targetY = oldPt.Y + deltaRow;
                                
                                pipePoints[conn.PointIndex] = new Point(targetX, targetY);

                                double pipeCol = conn.PipeVm.Col;
                                double pipeRow = conn.PipeVm.Row;

                                var relativePoints = pipePoints.Select(p => new Point(p.X - pipeCol, p.Y - pipeRow)).ToList();
                                string newPointsStr = string.Join(";", relativePoints.Select(p => string.Format(System.Globalization.CultureInfo.InvariantCulture, "{0:0.##},{1:0.##}", p.X, p.Y)));
                                
                                if (conn.PipeVm.PipePoints != newPointsStr)
                                {
                                    conn.PipeVm.PipePoints = newPointsStr;
                                }
                            }
                        }
                    }
                }

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
                var pipeVm = _draggedPipeControl.DataContext as PipeWidgetViewModel;
                if (pipeVm != null)
                {
                    pipeVm.IsSuppressingNormalization = false;
                    pipeVm.NormalizePointsAndSize();
                }
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
            _connectedPipePoints.Clear();

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

                double col = GetCol(child);
                double row = GetRow(child);
                int sizeX = GetSizeX(child);
                int sizeY = GetSizeY(child);

                double w = sizeX * CellWidth;
                double h = sizeY * CellHeight;
                child.Measure(new Size(w, h));

                maxWidth = Math.Max(maxWidth, (col + sizeX) * CellWidth);
                maxHeight = Math.Max(maxHeight, (row + sizeY) * CellHeight);
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

                double col = GetCol(child);
                double row = GetRow(child);
                int sizeX = GetSizeX(child);
                int sizeY = GetSizeY(child);

                double x = col * CellWidth;
                double y = row * CellHeight;
                double w = sizeX * CellWidth;
                double h = sizeY * CellHeight;
                
                child.Arrange(new Rect(x, y, w, h));
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
            return AvaloniaApplication1.Views.DashboardPanelHelpers.GridMathHelper.IsPointNearSegment(p, s1, s2, maxDistance);
        }

        private void GetSnappedPosition(Control child, Point pointer, out double snappedCol, out double snappedRow)
        {
            AvaloniaApplication1.Views.DashboardPanelHelpers.GridMathHelper.GetSnappedPosition(pointer, CellWidth, CellHeight, out snappedCol, out snappedRow);
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

        private Point GetGraphicsCenter(Control child, WidgetViewModelBase vm)
        {
            double col = GetCol(child);
            double row = GetRow(child);
            int sizeX = GetSizeX(child);
            int sizeY = GetSizeY(child);

            // Default center based on layout
            double defaultCx = col * CellWidth + sizeX * CellWidth / 2;
            double defaultCy = row * CellHeight + sizeY * CellHeight / 2;

            if (string.Equals(vm.Type, "Valve", StringComparison.OrdinalIgnoreCase))
            {
                var valveControl = FindValveControlRecursive(child);
                if (valveControl != null)
                {
                    var pt = valveControl.TranslatePoint(new Point(valveControl.Bounds.Width / 2, valveControl.Bounds.Height / 2), this);
                    if (pt.HasValue)
                    {
                        return pt.Value;
                    }
                }
            }
            else if (string.Equals(vm.Type, "Pump", StringComparison.OrdinalIgnoreCase))
            {
                var viewbox = FindViewboxRecursive(child);
                if (viewbox != null)
                {
                    var pt = viewbox.TranslatePoint(new Point(viewbox.Bounds.Width / 2, viewbox.Bounds.Height / 2), this);
                    if (pt.HasValue)
                    {
                        return pt.Value;
                    }
                }
            }

            return new Point(defaultCx, defaultCy);
        }

        private (Point p1, Point p2) GetVisualPortsInGrid(Control child, WidgetViewModelBase vm)
        {
            double col = GetCol(child);
            double row = GetRow(child);
            int sizeX = GetSizeX(child);
            int sizeY = GetSizeY(child);

            int rotation = 0;
            bool isVertical = false;
            var rotProp = vm.GetType().GetProperty("Rotation");
            if (rotProp != null) rotation = (int)(rotProp.GetValue(vm) ?? 0);
            var vertProp = vm.GetType().GetProperty("IsVertical");
            if (vertProp != null) isVertical = (bool)(vertProp.GetValue(vm) ?? false);

            int finalRotation = rotation;
            if (finalRotation == 0 && isVertical) finalRotation = 90;
            bool isFlowVertical = (finalRotation == 90 || finalRotation == 270);

            double fallbackRelX1 = isFlowVertical ? (sizeX / 2.0) : 0.0;
            double fallbackRelY1 = isFlowVertical ? 0.0 : (sizeY / 2.0);
            double fallbackRelX2 = isFlowVertical ? (sizeX / 2.0) : sizeX;
            double fallbackRelY2 = isFlowVertical ? sizeY : (sizeY / 2.0);

            var fallbackP1 = new Point(col + fallbackRelX1, row + fallbackRelY1);
            var fallbackP2 = new Point(col + fallbackRelX2, row + fallbackRelY2);

            var centerPt = GetGraphicsCenter(child, vm);
            double localOffsetX = centerPt.X - col * CellWidth;
            double localOffsetY = centerPt.Y - row * CellHeight;
            double gridCx = col + localOffsetX / CellWidth;
            double gridCy = row + localOffsetY / CellHeight;

            if (string.Equals(vm.Type, "Valve", StringComparison.OrdinalIgnoreCase))
            {
                var valveControl = FindValveControlRecursive(child);
                if (valveControl != null && valveControl.Bounds.Width > 0 && valveControl.Bounds.Height > 0)
                {
                    double w = valveControl.Bounds.Width;
                    double h = valveControl.Bounds.Height;
                    double flowSize = isFlowVertical ? h : w;
                    
                    double angle = finalRotation * Math.PI / 180.0;
                    var lp1 = new Point(w / 2.0 - flowSize / 2.0, h / 2.0);
                    var lp2 = new Point(w / 2.0 + flowSize / 2.0, h / 2.0);
                    
                    var lp1Rot = RotatePoint(lp1, new Point(w / 2.0, h / 2.0), angle);
                    var lp2Rot = RotatePoint(lp2, new Point(w / 2.0, h / 2.0), angle);
                    
                    var p1PixelOpt = valveControl.TranslatePoint(lp1Rot, this);
                    var p2PixelOpt = valveControl.TranslatePoint(lp2Rot, this);
                    
                    if (p1PixelOpt.HasValue && p2PixelOpt.HasValue)
                    {
                        var p1Grid = new Point(
                            (p1PixelOpt.Value.X - CellWidth / 2.0) / CellWidth,
                            (p1PixelOpt.Value.Y - CellHeight / 2.0) / CellHeight);
                        var p2Grid = new Point(
                            (p2PixelOpt.Value.X - CellWidth / 2.0) / CellWidth,
                            (p2PixelOpt.Value.Y - CellHeight / 2.0) / CellHeight);
                        return (p1Grid, p2Grid);
                    }
                }
            }
            else if (string.Equals(vm.Type, "Pump", StringComparison.OrdinalIgnoreCase))
            {
                var viewbox = FindViewboxRecursive(child);
                if (viewbox != null && viewbox.Bounds.Width > 0 && viewbox.Bounds.Height > 0)
                {
                    double w = viewbox.Bounds.Width;
                    double h = viewbox.Bounds.Height;
                    
                    double flowSize = isFlowVertical ? h : w;
                    double halfFlowGrid = (flowSize / 2.0) / (isFlowVertical ? CellHeight : CellWidth);

                    var p1Grid = isFlowVertical ? new Point(gridCx, gridCy - halfFlowGrid) : new Point(gridCx - halfFlowGrid, gridCy);
                    var p2Grid = isFlowVertical ? new Point(gridCx, gridCy + halfFlowGrid) : new Point(gridCx + halfFlowGrid, gridCy);
                    return (p1Grid, p2Grid);
                }
            }

            return (fallbackP1, fallbackP2);
        }

        private static Point RotatePoint(Point p, Point origin, double angleRad)
        {
            return AvaloniaApplication1.Views.DashboardPanelHelpers.GridMathHelper.RotatePoint(p, origin, angleRad);
        }

        private ValveControl? FindValveControlRecursive(Control control)
        {
            if (control is ValveControl valve) return valve;
            if (control is Panel panel)
            {
                foreach (var child in panel.Children)
                {
                    var res = FindValveControlRecursive(child);
                    if (res != null) return res;
                }
            }
            else if (control is ContentControl cc && cc.Content is Control contentControl)
            {
                return FindValveControlRecursive(contentControl);
            }
            else if (control is ContentPresenter cp && cp.Child is Control childControl)
            {
                return FindValveControlRecursive(childControl);
            }
            return null;
        }

        private Viewbox? FindViewboxRecursive(Control control)
        {
            if (control is Viewbox viewbox) return viewbox;
            if (control is Panel panel)
            {
                foreach (var child in panel.Children)
                {
                    var res = FindViewboxRecursive(child);
                    if (res != null) return res;
                }
            }
            else if (control is ContentControl cc && cc.Content is Control contentControl)
            {
                return FindViewboxRecursive(contentControl);
            }
            else if (control is ContentPresenter cp && cp.Child is Control childControl)
            {
                return FindViewboxRecursive(childControl);
            }
            return null;
        }
    }
}