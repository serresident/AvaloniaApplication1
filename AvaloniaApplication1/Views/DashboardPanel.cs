using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.VisualTree;
using AvaloniaApplication1.Services;
using AvaloniaApplication1.ViewModels;
using AvaloniaApplication1.Views.DashboardPanelHelpers;

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
                    if (value == null)
                    {
                        ClearAllSelection();
                    }
                    else
                    {
                        SelectWidget(value, addToSelection: false);
                    }
                }
            }
        }

        public Rect? SelectionBoxRect { get; set; }

        public List<WidgetViewModelBase> GetSelectedWidgets()
        {
            var result = new List<WidgetViewModelBase>();
            foreach (var child in Children)
            {
                if (child.DataContext is WidgetViewModelBase vm && vm.IsSelected && !result.Contains(vm))
                {
                    result.Add(vm);
                }
            }
            return result;
        }

        public void ClearAllSelection()
        {
            foreach (var child in Children)
            {
                if (child.DataContext is WidgetViewModelBase vm)
                {
                    vm.IsSelected = false;
                    if (vm is PipeWidgetViewModel pipeVm)
                    {
                        pipeVm.IsEditingVertices = false;
                    }
                }
            }
            _selectedVm = null;
            InvalidateVisual();
            _selectionOverlay?.InvalidateVisual();
        }

        public void SelectWidget(WidgetViewModelBase vm, bool addToSelection = false)
        {
            if (!addToSelection)
            {
                foreach (var child in Children)
                {
                    if (child.DataContext is WidgetViewModelBase other && other != vm)
                    {
                        other.IsSelected = false;
                        if (other is PipeWidgetViewModel pipeVm)
                        {
                            pipeVm.IsEditingVertices = false;
                        }
                    }
                }
            }

            vm.IsSelected = true;
            _selectedVm = vm;
            InvalidateVisual();
            _selectionOverlay?.InvalidateVisual();
        }

        public void DeselectWidget(WidgetViewModelBase vm)
        {
            vm.IsSelected = false;
            if (vm is PipeWidgetViewModel pipeVm)
            {
                pipeVm.IsEditingVertices = false;
            }

            if (_selectedVm == vm)
            {
                _selectedVm = GetSelectedWidgets().LastOrDefault();
            }

            InvalidateVisual();
            _selectionOverlay?.InvalidateVisual();
        }

        private bool _isDragging;
        private bool _isResizing;
        private int _dragOriginalSizeX;
        private int _dragOriginalSizeY;

        private bool _isBoxSelecting;
        private Point _boxSelectStartPoint;
        private bool _wasAlreadySelectedOnPressed;
        private readonly Dictionary<WidgetViewModelBase, (double Col, double Row)> _dragOriginalPositions = new();
        private readonly Dictionary<PipeWidgetViewModel, List<Point>> _dragOriginalPipePoints = new();

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

        // Overlay references (hosted outside Children to keep Children 1:1 with Widgets)
        private GridOverlay? _gridOverlay;
        public GridOverlay? GridOverlay
        {
            get => _gridOverlay;
            set
            {
                if (_gridOverlay != value)
                {
                    _gridOverlay = value;
                    _gridOverlay?.InvalidateVisual();
                }
            }
        }

        private SelectionOverlay? _selectionOverlay;
        public SelectionOverlay? SelectionOverlay
        {
            get => _selectionOverlay;
            set
            {
                if (_selectionOverlay != value)
                {
                    _selectionOverlay = value;
                    _selectionOverlay?.InvalidateVisual();
                }
            }
        }

        private ContextMenu? _activeWidgetMenu;

        public Point? ActiveSnapTarget { get; private set; }
        private bool _isSnappedToEquipmentPort = false;

        public void CloseActiveWidgetMenu()
        {
            if (_activeWidgetMenu != null)
            {
                _activeWidgetMenu.Close();
                _activeWidgetMenu = null;
            }
        }

        private DashboardViewModel? GetDashboardViewModel()
        {
            if (DataContext is DashboardViewModel vm) return vm;
            var view = this.FindAncestorOfType<DashboardView>();
            if (view?.DataContext is DashboardViewModel vm2) return vm2;
            return null;
        }

        static DashboardPanel()
        {
            AffectsMeasure<DashboardPanel>(CellWidthProperty, CellHeightProperty, ColProperty, RowProperty, SizeXProperty, SizeYProperty);
            AffectsArrange<DashboardPanel>(CellWidthProperty, CellHeightProperty, ColProperty, RowProperty, SizeXProperty, SizeYProperty);
            CellWidthProperty.Changed.AddClassHandler<DashboardPanel>((panel, args) =>
            {
                if (panel._gridOverlay != null)
                {
                    panel._gridOverlay.CellWidth = panel.CellWidth;
                    panel._gridOverlay.InvalidateVisual();
                }
                panel._selectionOverlay?.InvalidateVisual();
                panel.InvalidateMeasure();
                panel.InvalidateArrange();
                panel.InvalidateVisual();
            });
            CellHeightProperty.Changed.AddClassHandler<DashboardPanel>((panel, args) =>
            {
                if (panel._gridOverlay != null)
                {
                    panel._gridOverlay.CellHeight = panel.CellHeight;
                    panel._gridOverlay.InvalidateVisual();
                }
                panel._selectionOverlay?.InvalidateVisual();
                panel.InvalidateMeasure();
                panel.InvalidateArrange();
                panel.InvalidateVisual();
            });
            IsDesignModeProperty.Changed.AddClassHandler<DashboardPanel>((panel, args) =>
            {
                panel._gridOverlay?.InvalidateVisual();
                panel._selectionOverlay?.InvalidateVisual();
                panel.InvalidateVisual();
                if (!panel.IsDesignMode)
                {
                    panel.SelectedVm = null;
                    panel.CloseActiveWidgetMenu();
                }
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

        private readonly Dictionary<Control, WidgetViewModelBase> _boundChildVms = new();

        public Control? FindChildForVm(WidgetViewModelBase vm)
        {
            foreach (var kvp in _boundChildVms)
            {
                if (kvp.Value == vm)
                    return kvp.Key;
            }
            foreach (var child in Children)
            {
                if (child.DataContext == vm)
                    return child;
            }
            return null;
        }

        private void SubscribeChildVm(Control child)
        {
            UnsubscribeChildVm(child);
            if (child.DataContext is WidgetViewModelBase vm)
            {
                _boundChildVms[child] = vm;
                vm.PropertyChanged += OnWidgetVmPropertyChanged;

                // Sync initial attached properties from VM
                SetCol(child, vm.Col);
                SetRow(child, vm.Row);
                SetSizeX(child, vm.SizeX);
                SetSizeY(child, vm.SizeY);
            }
        }

        private void UnsubscribeChildVm(Control child)
        {
            if (_boundChildVms.TryGetValue(child, out var vm))
            {
                vm.PropertyChanged -= OnWidgetVmPropertyChanged;
                _boundChildVms.Remove(child);
            }
        }

        private void OnWidgetVmPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (sender is WidgetViewModelBase vm)
            {
                Control? targetChild = FindChildForVm(vm);

                if (e.PropertyName == nameof(WidgetViewModelBase.Col) ||
                    e.PropertyName == nameof(WidgetViewModelBase.Row) ||
                    e.PropertyName == nameof(WidgetViewModelBase.SizeX) ||
                    e.PropertyName == nameof(WidgetViewModelBase.SizeY))
                {
                    if (targetChild != null)
                    {
                        SetCol(targetChild, vm.Col);
                        SetRow(targetChild, vm.Row);
                        SetSizeX(targetChild, vm.SizeX);
                        SetSizeY(targetChild, vm.SizeY);
                        targetChild.InvalidateMeasure();
                        targetChild.InvalidateArrange();
                        targetChild.InvalidateVisual();
                    }

                    InvalidateMeasure();
                    InvalidateArrange();
                    InvalidateVisual();
                    _selectionOverlay?.InvalidateVisual();
                }
                else if (e.PropertyName == nameof(PipeWidgetViewModel.PipePoints))
                {
                    if (targetChild != null)
                    {
                        targetChild.InvalidateMeasure();
                        targetChild.InvalidateArrange();
                        targetChild.InvalidateVisual();
                    }

                    InvalidateMeasure();
                    InvalidateArrange();
                    InvalidateVisual();
                    _selectionOverlay?.InvalidateVisual();
                }
                else if (e.PropertyName == nameof(WidgetViewModelBase.IsSelected))
                {
                    if (vm.IsSelected)
                    {
                        _selectedVm = vm;
                    }
                    else if (_selectedVm == vm)
                    {
                        _selectedVm = GetSelectedWidgets().LastOrDefault();
                    }
                    InvalidateVisual();
                    _selectionOverlay?.InvalidateVisual();
                }
            }
        }

        public DashboardPanel()
        {
            Focusable = true;
            AddHandler(PointerPressedEvent, OnPreviewPointerPressed, RoutingStrategies.Tunnel);
            foreach (var child in Children)
            {
                child.DataContextChanged += Child_DataContextChanged;
                SubscribeChildVm(child);
            }

            Children.CollectionChanged += (s, e) =>
            {
                if (e.OldItems != null)
                {
                    foreach (Control child in e.OldItems)
                    {
                        child.DataContextChanged -= Child_DataContextChanged;
                        UnsubscribeChildVm(child);
                    }
                }
                if (e.NewItems != null)
                {
                    foreach (Control child in e.NewItems)
                    {
                        child.DataContextChanged += Child_DataContextChanged;
                        SubscribeChildVm(child);
                        var vm = child.DataContext as WidgetViewModelBase;
                        if (vm != null && vm.IsSelected)
                        {
                            _selectedVm = vm;
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
                InvalidateMeasure();
                InvalidateArrange();
                InvalidateVisual();
                _selectionOverlay?.InvalidateVisual();
            };
        }

        private void Child_DataContextChanged(object? sender, EventArgs e)
        {
            if (sender is Control child)
            {
                SubscribeChildVm(child);

                var vm = child.DataContext as WidgetViewModelBase;
                if (vm != null && vm.IsSelected)
                {
                    _selectedVm = vm;
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
                _selectionOverlay?.InvalidateVisual();
            }
        }

        protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
        {
            base.OnAttachedToVisualTree(e);
            ResolveOverlays();
        }

        public void ResolveOverlays()
        {
            var view = this.FindAncestorOfType<DashboardView>();
            if (view != null)
            {
                if (_gridOverlay == null)
                {
                    var gridOverlay = view.FindControl<GridOverlay>("DashboardGridOverlay");
                    if (gridOverlay != null)
                    {
                        _gridOverlay = gridOverlay;
                        gridOverlay.Panel = this;
                        _gridOverlay.InvalidateVisual();
                    }
                }
                if (_selectionOverlay == null)
                {
                    var selectionOverlay = view.FindControl<SelectionOverlay>("DashboardSelectionOverlay");
                    if (selectionOverlay != null)
                    {
                        _selectionOverlay = selectionOverlay;
                        selectionOverlay.Panel = this;
                        _selectionOverlay.InvalidateVisual();
                    }
                }
            }
        }

        private void OnPreviewPointerPressed(object? sender, PointerPressedEventArgs e)
        {
            if (!IsDesignMode) return;
            HandleDesignPointerPressed(e);
        }

        protected override void OnPointerPressed(PointerPressedEventArgs e)
        {
            base.OnPointerPressed(e);
            if (e.Handled || !IsDesignMode) return;
            HandleDesignPointerPressed(e);
        }

        private void HandleDesignPointerPressed(PointerPressedEventArgs e)
        {
            PipeControl.CloseActiveMenu();
            CloseActiveWidgetMenu();

            Focus();

            var point = e.GetPosition(this);
            bool clickedWidget = false;

            // 1. Check if we clicked on a vertex of any Pipe widget
            foreach (var child in Children)
            {
                if (child.DataContext is PipeWidgetViewModel pipeVm)
                {
                    if (!pipeVm.IsEditingVertices) continue;

                    var pipeControl = VisualPortHelper.FindPipeControlRecursive(child);
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
                    if (child.DataContext is PipeWidgetViewModel pipeVm)
                    {
                        if (!pipeVm.IsEditingVertices) continue;

                        var pipeControl = VisualPortHelper.FindPipeControlRecursive(child);
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
            Control? clickedChild = null;
            WidgetViewModelBase? clickedVm = null;

            // 1. Try to find the clicked child via Avalonia's visual tree from e.Source
            if (e.Source is Visual sourceVisual)
            {
                var curr = sourceVisual;
                while (curr != null && curr != this)
                {
                    if (curr.GetVisualParent() == this && curr is Control c)
                    {
                        if (c.DataContext is WidgetViewModelBase vm)
                        {
                            // If the source visual is a Pipe, only accept if the click actually hit the pipe line
                            if (vm is PipeWidgetViewModel pipeVm)
                            {
                                var pipeControl = VisualPortHelper.FindPipeControlRecursive(c);
                                double thickness = pipeControl != null ? pipeControl.Thickness : pipeVm.Thickness;
                                var gridPoints = pipeVm.GetAbsoluteGridPoints();
                                double cellSize = CellWidth;
                                bool nearSegment = false;
                                for (int segIdx = 0; segIdx < gridPoints.Count - 1; segIdx++)
                                {
                                    var p1 = new Point(gridPoints[segIdx].X * cellSize + cellSize / 2, gridPoints[segIdx].Y * cellSize + cellSize / 2);
                                    var p2 = new Point(gridPoints[segIdx + 1].X * cellSize + cellSize / 2, gridPoints[segIdx + 1].Y * cellSize + cellSize / 2);

                                    if (IsPointNearSegment(point, p1, p2, thickness / 2 + 8))
                                    {
                                        nearSegment = true;
                                        break;
                                    }
                                }

                                if (nearSegment)
                                {
                                    clickedChild = c;
                                    clickedVm = vm;
                                    break;
                                }
                            }
                            else
                            {
                                clickedChild = c;
                                clickedVm = vm;
                                break;
                            }
                        }
                    }
                    curr = curr.GetVisualParent();
                }
            }

            // 2. Fallback: Search Children in REVERSE Z-order (top-most elements first)
            if (clickedChild == null)
            {
                for (int i = Children.Count - 1; i >= 0; i--)
                {
                    var child = Children[i];

                    if (child.DataContext is WidgetViewModelBase vm)
                    {
                        if (vm is PipeWidgetViewModel pipeVm)
                        {
                            var pipeControl = VisualPortHelper.FindPipeControlRecursive(child);
                            double thickness = pipeControl != null ? pipeControl.Thickness : pipeVm.Thickness;
                            var gridPoints = pipeVm.GetAbsoluteGridPoints();
                            double cellSize = CellWidth;
                            for (int segIdx = 0; segIdx < gridPoints.Count - 1; segIdx++)
                            {
                                var p1 = new Point(gridPoints[segIdx].X * cellSize + cellSize / 2, gridPoints[segIdx].Y * cellSize + cellSize / 2);
                                var p2 = new Point(gridPoints[segIdx + 1].X * cellSize + cellSize / 2, gridPoints[segIdx + 1].Y * cellSize + cellSize / 2);

                                if (IsPointNearSegment(point, p1, p2, thickness / 2 + 8))
                                {
                                    clickedChild = child;
                                    clickedVm = vm;
                                    break;
                                }
                            }
                            if (clickedChild != null) break;
                        }
                        else
                        {
                            double col = vm.Col;
                            double row = vm.Row;
                            int sizeX = vm.SizeX;
                            int sizeY = vm.SizeY;

                            var childBounds = new Rect(
                                col * CellWidth, row * CellHeight,
                                sizeX * CellWidth, sizeY * CellHeight);

                            if (childBounds.Contains(point))
                            {
                                clickedChild = child;
                                clickedVm = vm;
                                break;
                            }
                        }
                    }
                }
            }

            if (clickedChild != null && clickedVm != null)
            {
                var dashboardVm = GetDashboardViewModel();
                bool isMultiSelectModifier = e.KeyModifiers.HasFlag(KeyModifiers.Control) || 
                                             e.KeyModifiers.HasFlag(KeyModifiers.Shift) || 
                                             (dashboardVm != null && dashboardVm.IsMultiSelectMode);

                bool wasSelected = clickedVm.IsSelected;
                _wasAlreadySelectedOnPressed = wasSelected;

                if (isMultiSelectModifier)
                {
                    if (!wasSelected)
                    {
                        SelectWidget(clickedVm, addToSelection: true);
                    }
                    else
                    {
                        _selectedVm = clickedVm;
                    }
                }
                else
                {
                    // Single select mode: if clicked item was not selected, select it exclusively
                    if (!wasSelected)
                    {
                        SelectWidget(clickedVm, addToSelection: false);
                    }
                    else
                    {
                        // Already selected: retain group selection during drag
                        _selectedVm = clickedVm;
                    }
                }

                clickedWidget = true;

                double col = clickedVm.Col;
                double row = clickedVm.Row;
                int sizeX = clickedVm.SizeX;
                int sizeY = clickedVm.SizeY;
                var childBounds = new Rect(col * CellWidth, row * CellHeight, sizeX * CellWidth, sizeY * CellHeight);

                // Правый клик (ПКМ) — открываем надежное контекстное меню точно для выбранного виджета
                if (e.GetCurrentPoint(this).Properties.IsRightButtonPressed)
                {
                    ShowWidgetContextMenu(clickedVm, clickedChild);
                    e.Handled = true;
                    return;
                }

                // Check if click was in the bottom-right corner for resize (20x20 pixels)
                // ONLY allow resize if ONLY this single widget is selected and not in pipe vertex edit mode!
                var allSelectedNow = GetSelectedWidgets();
                bool isEditingPipe = clickedVm is PipeWidgetViewModel pvm && pvm.IsEditingVertices;
                var resizeRect = new Rect(childBounds.Right - 20, childBounds.Bottom - 20, 20, 20);
                if (allSelectedNow.Count == 1 && wasSelected && !isEditingPipe && resizeRect.Contains(point))
                {
                    _dragChild = clickedChild;
                    _dragVm = clickedVm;
                    _dragStartPoint = point;
                    _dragOriginalSizeX = sizeX;
                    _dragOriginalSizeY = sizeY;
                    _isDragging = false;
                    _isResizing = true;
                    
                    e.Pointer.Capture(this);
                    e.Handled = true;
                    return;
                }
                else
                {
                    _dragChild = clickedChild;
                    _dragVm = clickedVm;
                    _dragStartPoint = point;
                    _dragOriginalRow = row;
                    _dragOriginalCol = col;
                    _isDragging = false;
                    _isResizing = false;
                    
                    // Group drag setup
                    _dragOriginalPositions.Clear();
                    _dragOriginalPipePoints.Clear();

                    if (!allSelectedNow.Contains(clickedVm))
                    {
                        allSelectedNow.Add(clickedVm);
                    }

                    foreach (var sel in allSelectedNow)
                    {
                        _dragOriginalPositions[sel] = (sel.Col, sel.Row);
                        if (sel is PipeWidgetViewModel pipeVm)
                        {
                            _dragOriginalPipePoints[pipeVm] = pipeVm.GetAbsoluteGridPoints().ToList();
                        }
                    }

                    // Collect connected pipes for rubber-banding (only for pipes NOT moving with group)
                    _connectedPipePoints.Clear();
                    foreach (var movingVm in allSelectedNow)
                    {
                        bool isValve = string.Equals(movingVm.Type, "Valve", StringComparison.OrdinalIgnoreCase);
                        bool isPump = string.Equals(movingVm.Type, "Pump", StringComparison.OrdinalIgnoreCase);
                        bool isExchanger = string.Equals(movingVm.Type, "HeatExchanger", StringComparison.OrdinalIgnoreCase);
                        bool isReactor = string.Equals(movingVm.Type, "Reactor", StringComparison.OrdinalIgnoreCase);
                        if (isValve || isPump || isExchanger || isReactor)
                        {
                            var movingChild = FindChildForVm(movingVm);
                            if (movingChild != null)
                            {
                                var (p1, p2) = VisualPortHelper.GetVisualPortsInGrid(movingChild, movingVm, this);

                                foreach (var otherChild in Children)
                                {
                                    if (otherChild.DataContext is PipeWidgetViewModel pipeVm && !_dragOriginalPositions.ContainsKey(pipeVm))
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
                        }
                    }

                    e.Pointer.Capture(this);
                    e.Handled = true;
                    return;
                }
            }

            if (!clickedWidget)
            {
                if (IsDesignMode && e.GetCurrentPoint(this).Properties.IsRightButtonPressed)
                {
                    ShowCanvasContextMenu(point);
                    e.Handled = true;
                    return;
                }

                if (IsDesignMode && e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
                {
                    var dashboardVm = GetDashboardViewModel();
                    bool isMultiSelectModifier = e.KeyModifiers.HasFlag(KeyModifiers.Control) || 
                                                 e.KeyModifiers.HasFlag(KeyModifiers.Shift) || 
                                                 (dashboardVm != null && dashboardVm.IsMultiSelectMode);

                    if (!isMultiSelectModifier)
                    {
                        ClearAllSelection();
                    }

                    _isBoxSelecting = true;
                    _boxSelectStartPoint = point;
                    SelectionBoxRect = new Rect(point.X, point.Y, 0, 0);
                    _selectionOverlay?.InvalidateVisual();

                    e.Pointer.Capture(this);
                    e.Handled = true;
                    return;
                }
                else
                {
                    ClearAllSelection();
                }
            }
        }

        public void ShowCanvasContextMenu(Point clickPoint)
        {
            var dashboardVm = GetDashboardViewModel();
            if (dashboardVm == null) return;

            CloseActiveWidgetMenu();

            var menu = new ContextMenu();
            _activeWidgetMenu = menu;
            menu.Closed += (s, ev) =>
            {
                if (_activeWidgetMenu == menu) _activeWidgetMenu = null;
            };

            double col = Math.Max(0, Math.Floor(clickPoint.X / CellWidth));
            double row = Math.Max(0, Math.Floor(clickPoint.Y / CellHeight));

            // 1. Добавить элемент...
            var addEditorItem = new MenuItem 
            { 
                Header = "➕  Добавить элемент..." 
            };
            addEditorItem.Click += async (s, ev) =>
            {
                await dashboardVm.AddWidgetAtAsync(col, row);
            };
            menu.Items.Add(addEditorItem);

            // 2. Быстро добавить >
            var quickMenu = new MenuItem 
            { 
                Header = "⚡  Быстро добавить" 
            };

            void AddQuickItem(string header, string type)
            {
                var item = new MenuItem { Header = header };
                item.Click += (s, ev) => dashboardVm.AddQuickWidgetAt(type, col, row);
                quickMenu.Items.Add(item);
            }

            AddQuickItem("🚰  Задвижка / Клапан", "Valve");
            AddQuickItem("⚙️  Насос", "Pump");
            AddQuickItem("═  Трубопровод", "Pipe");
            AddQuickItem("🛢️  Бак / Емкость", "Tank");
            AddQuickItem("🔄  Теплообменник", "HeatExchanger");
            AddQuickItem("🧪  Реактор с мешалкой", "Reactor");
            AddQuickItem("📊  Датчик уровня", "LevelSensor");
            quickMenu.Items.Add(new Separator());
            AddQuickItem("📁  Окно-контейнер", "ContainerButton");
            AddQuickItem("🔢  Индикатор значения", "ValueDisplay");
            AddQuickItem("🎛️  Задатчик уставки", "SetValue");
            AddQuickItem("📈  График тренда", "RealTimeTrend");
            AddQuickItem("🔘  Кнопка управления", "CommandButton");
            AddQuickItem("💡  Сигнальная лампа", "PilotLight");
            AddQuickItem("🎚️  Ползунок", "Slider");

            menu.Items.Add(quickMenu);

            // 3. Вставить
            var pasteItem = new MenuItem 
            { 
                Header = "📋  Вставить", 
                IsEnabled = WidgetClipboard.HasWidget 
            };
            pasteItem.Click += (s, ev) =>
            {
                dashboardVm.PasteWidgetAt(col, row);
            };
            menu.Items.Add(pasteItem);

            menu.Items.Add(new Separator());

            // 4. Свойства...
            var propertiesItem = new MenuItem 
            { 
                Header = "⚙️  Свойства..." 
            };
            propertiesItem.Click += async (s, ev) =>
            {
                await dashboardVm.OpenPropertiesCommand.ExecuteAsync(null);
            };
            menu.Items.Add(propertiesItem);

            menu.Placement = PlacementMode.Pointer;
            menu.Open(this);
        }

        private void ShowWidgetContextMenu(WidgetViewModelBase targetVm, Control? targetChild)
        {
            var dashboardVm = GetDashboardViewModel();
            if (dashboardVm == null) return;

            CloseActiveWidgetMenu();

            var menu = new ContextMenu();
            _activeWidgetMenu = menu;
            menu.Closed += (s, ev) =>
            {
                if (_activeWidgetMenu == menu) _activeWidgetMenu = null;
            };

            var bringToFrontItem = new MenuItem { Header = "На передний план" };
            bringToFrontItem.Click += (s, ev) => dashboardVm.BringToFrontCommand.Execute(targetVm);
            menu.Items.Add(bringToFrontItem);

            var bringForwardItem = new MenuItem { Header = "Переместить вперед" };
            bringForwardItem.Click += (s, ev) => dashboardVm.BringForwardCommand.Execute(targetVm);
            menu.Items.Add(bringForwardItem);

            var sendBackwardItem = new MenuItem { Header = "Переместить назад" };
            sendBackwardItem.Click += (s, ev) => dashboardVm.SendBackwardCommand.Execute(targetVm);
            menu.Items.Add(sendBackwardItem);

            var sendToBackItem = new MenuItem { Header = "На задний план" };
            sendToBackItem.Click += (s, ev) => dashboardVm.SendToBackCommand.Execute(targetVm);
            menu.Items.Add(sendToBackItem);

            menu.Items.Add(new Separator());

            if (targetVm is ValveWidgetViewModel valveVm)
            {
                var rotateItem = new MenuItem { Header = "Повернуть" };
                rotateItem.Click += (s, ev) => valveVm.RotateCommand.Execute(null);
                menu.Items.Add(rotateItem);
                menu.Items.Add(new Separator());
            }

            if (targetVm is ContainerButtonViewModel containerVm)
            {
                var openContainerItem = new MenuItem { Header = "Открыть контейнер" };
                openContainerItem.Click += async (s, ev) => await containerVm.OpenContainerCommand.ExecuteAsync(null);
                menu.Items.Add(openContainerItem);

                var pasteIntoContainerItem = new MenuItem 
                { 
                    Header = "Вставить внутрь контейнера",
                    IsEnabled = WidgetClipboard.HasWidget
                };
                pasteIntoContainerItem.Click += (s, ev) => containerVm.PasteWidgetIntoContainer();
                menu.Items.Add(pasteIntoContainerItem);

                menu.Items.Add(new Separator());
            }

            var editItem = new MenuItem { Header = "Свойства" };
            editItem.Click += (s, ev) => dashboardVm.EditWidgetCommand.Execute(targetVm);
            menu.Items.Add(editItem);

            menu.Items.Add(new Separator());

            var copyItem = new MenuItem { Header = "Копировать" };
            copyItem.Click += (s, ev) => dashboardVm.CopyWidgetCommand.Execute(targetVm);
            menu.Items.Add(copyItem);

            var pasteItem = new MenuItem 
            { 
                Header = "Вставить", 
                IsEnabled = WidgetClipboard.HasWidget 
            };
            pasteItem.Click += (s, ev) => dashboardVm.PasteWidgetCommand.Execute(null);
            menu.Items.Add(pasteItem);

            var dupItem = new MenuItem { Header = "Дублировать" };
            dupItem.Click += (s, ev) => dashboardVm.DuplicateWidgetCommand.Execute(targetVm);
            menu.Items.Add(dupItem);

            var removeItem = new MenuItem { Header = "Удалить" };
            removeItem.Click += (s, ev) =>
            {
                var toRemove = targetVm;
                dashboardVm.RemoveWidgetCommand.Execute(toRemove);
                SelectedVm = null;
                InvalidateVisual();
                _selectionOverlay?.InvalidateVisual();
            };
            menu.Items.Add(removeItem);

            if (targetVm is PipeWidgetViewModel pipeVm)
            {
                menu.Items.Add(new Separator());

                var editVerticesItem = new MenuItem
                {
                    Header = pipeVm.IsEditingVertices ? "Завершить редактирование" : "Редактировать вершины"
                };
                editVerticesItem.Click += (s, ev) => pipeVm.ToggleEditingVerticesCommand.Execute(null);
                menu.Items.Add(editVerticesItem);

                var autoRouteItem = new MenuItem { Header = "Автотрассировка (под 90°)" };
                autoRouteItem.Click += (s, ev) => dashboardVm.AutoRoutePipeCommand.Execute(pipeVm);
                menu.Items.Add(autoRouteItem);
            }

            menu.Placement = PlacementMode.Pointer;
            menu.Open(this);
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
                        var siblingPipes = VisualPortHelper.FindAllSiblingPipesFor(this, _draggedPipeControl);
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

                                if (dist <= 25.0) // 25px snap radius
                                {
                                    dragAbsX = port.X;
                                    dragAbsY = port.Y;
                                    snapped = true;
                                    break;
                                }
                            }
                            if (snapped) break;
                        }

                        // Valve/Pump snapped ports check if not snapped to sibling pipe (25px snap radius)
                        bool snappedToEquipment = false;
                        if (!snapped)
                        {
                            foreach (var child in Children)
                            {
                                if (child.DataContext is WidgetViewModelBase widgetVm && 
                                    (string.Equals(widgetVm.Type, "Valve", StringComparison.OrdinalIgnoreCase) ||
                                     string.Equals(widgetVm.Type, "Pump", StringComparison.OrdinalIgnoreCase) ||
                                     string.Equals(widgetVm.Type, "HeatExchanger", StringComparison.OrdinalIgnoreCase) ||
                                     string.Equals(widgetVm.Type, "Reactor", StringComparison.OrdinalIgnoreCase)))
                                {
                                    var (p1, p2) = VisualPortHelper.GetVisualPortsInGrid(child, widgetVm, this);

                                    // Check Port 1
                                    double dx1 = (dragAbsX - p1.X) * cellSize;
                                    double dy1 = (dragAbsY - p1.Y) * cellSize;
                                    double dist1 = Math.Sqrt(dx1 * dx1 + dy1 * dy1);
                                    if (dist1 <= 25.0) // 25px snap radius
                                    {
                                        dragAbsX = p1.X;
                                        dragAbsY = p1.Y;
                                        snapped = true;
                                        snappedToEquipment = true;
                                        break;
                                    }

                                    // Check Port 2
                                    double dx2 = (dragAbsX - p2.X) * cellSize;
                                    double dy2 = (dragAbsY - p2.Y) * cellSize;
                                    double dist2 = Math.Sqrt(dx2 * dx2 + dy2 * dy2);
                                    if (dist2 <= 25.0) // 25px snap radius
                                    {
                                        dragAbsX = p2.X;
                                        dragAbsY = p2.Y;
                                        snapped = true;
                                        snappedToEquipment = true;
                                        break;
                                    }
                                }
                            }
                        }

                        _isSnappedToEquipmentPort = snappedToEquipment;
                        Point? newSnapTarget = snapped ? new Point(dragAbsX * cellSize + cellSize / 2, dragAbsY * cellSize + cellSize / 2) : null;
                        if (ActiveSnapTarget != newSnapTarget)
                        {
                            ActiveSnapTarget = newSnapTarget;
                            _selectionOverlay?.InvalidateVisual();
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

                        _selectionOverlay?.InvalidateVisual();
                    }
                }
                e.Handled = true;
                return;
            }

            if (_isBoxSelecting)
            {
                double minX = Math.Min(_boxSelectStartPoint.X, point.X);
                double minY = Math.Min(_boxSelectStartPoint.Y, point.Y);
                double maxX = Math.Max(_boxSelectStartPoint.X, point.X);
                double maxY = Math.Max(_boxSelectStartPoint.Y, point.Y);

                var boxRect = new Rect(minX, minY, Math.Max(1, maxX - minX), Math.Max(1, maxY - minY));
                SelectionBoxRect = boxRect;

                var dashboardVm = GetDashboardViewModel();
                bool isMultiSelectModifier = e.KeyModifiers.HasFlag(KeyModifiers.Control) || 
                                             e.KeyModifiers.HasFlag(KeyModifiers.Shift) || 
                                             (dashboardVm != null && dashboardVm.IsMultiSelectMode);

                foreach (var child in Children)
                {
                    if (child.DataContext is WidgetViewModelBase vm)
                    {
                        var widgetRect = new Rect(
                            vm.Col * CellWidth,
                            vm.Row * CellHeight,
                            Math.Max(1, vm.SizeX) * CellWidth,
                            Math.Max(1, vm.SizeY) * CellHeight);

                        bool intersects = boxRect.Intersects(widgetRect);
                        if (intersects)
                        {
                            vm.IsSelected = true;
                        }
                        else if (!isMultiSelectModifier)
                        {
                            vm.IsSelected = false;
                        }
                    }
                }

                _selectionOverlay?.InvalidateVisual();
                e.Handled = true;
                return;
            }

            if (_dragChild == null || _dragVm == null)
            {
                // Hover cursor support for resize handle
                if (IsDesignMode)
                {
                    bool overResize = false;
                    if (SelectedVm != null)
                    {
                        double col = SelectedVm.Col;
                        double row = SelectedVm.Row;
                        int sizeX = SelectedVm.SizeX;
                        int sizeY = SelectedVm.SizeY;
                        var childBounds = new Rect(
                            col * CellWidth, row * CellHeight,
                            sizeX * CellWidth, sizeY * CellHeight);

                        var resizeRect = new Rect(childBounds.Right - 20, childBounds.Bottom - 20, 20, 20);
                        if (resizeRect.Contains(point))
                        {
                            overResize = true;
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

            if (_isBoxSelecting)
            {
                _isBoxSelecting = false;
                SelectionBoxRect = null;
                _selectionOverlay?.InvalidateVisual();
                var selectedWidgets = GetSelectedWidgets();
                _selectedVm = selectedWidgets.LastOrDefault();
                if (e.Pointer.Captured == this)
                {
                    e.Pointer.Capture(null);
                }
                e.Handled = true;
                return;
            }

            if (_draggedPipeControl != null)
            {
                var pipeVm = _draggedPipeControl.DataContext as PipeWidgetViewModel;
                if (pipeVm != null)
                {
                    if (_isSnappedToEquipmentPort)
                    {
                        var gridPoints = pipeVm.GetAbsoluteGridPoints();
                        if (_draggedPointIndex == 0)
                        {
                            pipeVm.StartFitting = "Flange";
                        }
                        else if (_draggedPointIndex == gridPoints.Count - 1)
                        {
                            pipeVm.EndFitting = "Flange";
                        }
                    }
                    pipeVm.IsSuppressingNormalization = false;
                    pipeVm.NormalizePointsAndSize();

                    var child = FindChildForVm(pipeVm);
                    if (child != null)
                    {
                        SetCol(child, pipeVm.Col);
                        SetRow(child, pipeVm.Row);
                        SetSizeX(child, pipeVm.SizeX);
                        SetSizeY(child, pipeVm.SizeY);
                        child.InvalidateMeasure();
                        child.InvalidateArrange();
                        child.InvalidateVisual();
                    }

                    InvalidateMeasure();
                    InvalidateArrange();
                    InvalidateVisual();
                    _selectionOverlay?.InvalidateVisual();
                }
                _isSnappedToEquipmentPort = false;
                if (ActiveSnapTarget != null)
                {
                    ActiveSnapTarget = null;
                    _selectionOverlay?.InvalidateVisual();
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

                if (_dragChild != null)
                {
                    SetSizeX(_dragChild, newSizeX);
                    SetSizeY(_dragChild, newSizeY);
                }

                _dragVm.OriginalConfig.Position.SizeX = newSizeX;
                _dragVm.OriginalConfig.Position.SizeY = newSizeY;

                InvalidateMeasure();
                InvalidateArrange();
            }
            else if (_isDragging && _dragVm != null && _dragChild != null)
            {
                var point = e.GetPosition(this);

                GetSnappedPosition(_dragChild, point, out double newCol, out double newRow, out var snappedPipeConn);

                if (snappedPipeConn.HasValue)
                {
                    if (snappedPipeConn.Value.isStart)
                    {
                        snappedPipeConn.Value.pipeVm.StartFitting = "Flange";
                    }
                    else
                    {
                        snappedPipeConn.Value.pipeVm.EndFitting = "Flange";
                    }
                }

                if (ActiveSnapTarget != null)
                {
                    ActiveSnapTarget = null;
                    _selectionOverlay?.InvalidateVisual();
                }

                double deltaCol = newCol - _dragOriginalCol;
                double deltaRow = newRow - _dragOriginalRow;

                // Ensure none of the moving widgets go out of bounds (< 0)
                if (_dragOriginalPositions.Count > 0)
                {
                    double minOrigCol = _dragOriginalPositions.Values.Min(p => p.Col);
                    double minOrigRow = _dragOriginalPositions.Values.Min(p => p.Row);
                    if (minOrigCol + deltaCol < 0) deltaCol = -minOrigCol;
                    if (minOrigRow + deltaRow < 0) deltaRow = -minOrigRow;
                }

                // 1. Update connected external pipes for rubber-banding
                if (_connectedPipePoints.Count > 0)
                {
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

                            var pipeChild = FindChildForVm(conn.PipeVm);
                            if (pipeChild != null)
                            {
                                SetCol(pipeChild, conn.PipeVm.Col);
                                SetRow(pipeChild, conn.PipeVm.Row);
                                SetSizeX(pipeChild, conn.PipeVm.SizeX);
                                SetSizeY(pipeChild, conn.PipeVm.SizeY);
                                pipeChild.InvalidateMeasure();
                                pipeChild.InvalidateArrange();
                                pipeChild.InvalidateVisual();
                            }
                        }
                    }
                }

                // 2. Move any pipes that were part of the selected group
                foreach (var kvp in _dragOriginalPipePoints)
                {
                    var pvm = kvp.Key;
                    var origPoints = kvp.Value;
                    var shiftedPoints = origPoints.Select(p => new Point(p.X + deltaCol, p.Y + deltaRow)).ToList();

                    if (_dragOriginalPositions.TryGetValue(pvm, out var origPos))
                    {
                        double pipeCol = origPos.Col + deltaCol;
                        double pipeRow = origPos.Row + deltaRow;

                        var relativePoints = shiftedPoints.Select(p => new Point(p.X - pipeCol, p.Y - pipeRow)).ToList();
                        string newPointsStr = string.Join(";", relativePoints.Select(p => string.Format(System.Globalization.CultureInfo.InvariantCulture, "{0:0.##},{1:0.##}", p.X, p.Y)));
                        pvm.PipePoints = newPointsStr;
                    }
                }

                // 3. Move all selected widgets
                foreach (var kvp in _dragOriginalPositions)
                {
                    var vm = kvp.Key;
                    double targetCol = kvp.Value.Col + deltaCol;
                    double targetRow = kvp.Value.Row + deltaRow;

                    vm.Col = targetCol;
                    vm.Row = targetRow;
                    vm.OriginalConfig.Position.Col = targetCol;
                    vm.OriginalConfig.Position.Row = targetRow;

                    var child = FindChildForVm(vm);
                    if (child != null)
                    {
                        SetCol(child, targetCol);
                        SetRow(child, targetRow);
                        child.InvalidateMeasure();
                        child.InvalidateArrange();
                        child.InvalidateVisual();
                    }
                }

                InvalidateMeasure();
                InvalidateArrange();
            }
            else if (!_isDragging && _dragVm != null)
            {
                // Simple click without dragging
                var dashboardVm = GetDashboardViewModel();
                bool isMultiSelectModifier = e.KeyModifiers.HasFlag(KeyModifiers.Control) || 
                                             e.KeyModifiers.HasFlag(KeyModifiers.Shift) || 
                                             (dashboardVm != null && dashboardVm.IsMultiSelectMode);

                if (!isMultiSelectModifier)
                {
                    // Regular click on a selected item when multiple were selected: collapse selection to this single item
                    SelectWidget(_dragVm, addToSelection: false);
                }
                else if (_wasAlreadySelectedOnPressed)
                {
                    // Click with modifier on already selected item: toggle selection off
                    DeselectWidget(_dragVm);
                }
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

            if (_isBoxSelecting)
            {
                _isBoxSelecting = false;
                SelectionBoxRect = null;
                _selectionOverlay?.InvalidateVisual();
            }

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
            else if (_isDragging && _dragOriginalPositions.Count > 0)
            {
                foreach (var kvp in _dragOriginalPositions)
                {
                    kvp.Key.Row = kvp.Value.Row;
                    kvp.Key.Col = kvp.Value.Col;
                    var child = FindChildForVm(kvp.Key);
                    if (child != null)
                    {
                        SetCol(child, kvp.Value.Col);
                        SetRow(child, kvp.Value.Row);
                    }
                }
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
            _isSnappedToEquipmentPort = false;
            _dragOriginalPositions.Clear();
            _dragOriginalPipePoints.Clear();
            _wasAlreadySelectedOnPressed = false;

            if (ActiveSnapTarget != null)
            {
                ActiveSnapTarget = null;
                _selectionOverlay?.InvalidateVisual();
            }

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

            // Always reserve a comfortable grid in design mode for drop targets and canvas interactions
            if (IsDesignMode)
            {
                maxWidth = Math.Max(maxWidth, 50 * CellWidth);
                maxHeight = Math.Max(maxHeight, 40 * CellHeight);
            }

            foreach (var child in Children)
            {
                var vm = child.DataContext as WidgetViewModelBase;
                double col = vm != null ? vm.Col : GetCol(child);
                double row = vm != null ? vm.Row : GetRow(child);
                int sizeX = vm != null ? vm.SizeX : GetSizeX(child);
                int sizeY = vm != null ? vm.SizeY : GetSizeY(child);

                double w = sizeX * CellWidth;
                double h = sizeY * CellHeight;
                child.Measure(new Size(w, h));

                maxWidth = Math.Max(maxWidth, (col + sizeX) * CellWidth);
                maxHeight = Math.Max(maxHeight, (row + sizeY) * CellHeight);
            }

            return new Size(maxWidth, maxHeight);
        }

        protected override Size ArrangeOverride(Size finalSize)
        {
            foreach (var child in Children)
            {
                var vm = child.DataContext as WidgetViewModelBase;
                double col = vm != null ? vm.Col : GetCol(child);
                double row = vm != null ? vm.Row : GetRow(child);
                int sizeX = vm != null ? vm.SizeX : GetSizeX(child);
                int sizeY = vm != null ? vm.SizeY : GetSizeY(child);

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

            if (!IsDesignMode) return;

            if (e.KeyModifiers.HasFlag(Avalonia.Input.KeyModifiers.Control))
            {
                var dashboardVm = GetDashboardViewModel();
                if (dashboardVm != null)
                {
                    if (e.Key == Avalonia.Input.Key.C && SelectedVm != null)
                    {
                        dashboardVm.CopyWidgetCommand.Execute(SelectedVm);
                        e.Handled = true;
                        return;
                    }
                    if (e.Key == Avalonia.Input.Key.V && WidgetClipboard.HasWidget)
                    {
                        dashboardVm.PasteWidgetCommand.Execute(null);
                        e.Handled = true;
                        return;
                    }
                }
            }

            if (SelectedVm == null) return;

            if (e.Key == Avalonia.Input.Key.Delete)
            {
                var dashboardVm = GetDashboardViewModel();
                if (dashboardVm != null)
                {
                    var selectedWidgets = GetSelectedWidgets();
                    if (selectedWidgets.Count > 1)
                    {
                        dashboardVm.RemoveSelectedWidgetsCommand.Execute(null);
                        ClearAllSelection();
                        e.Handled = true;
                        return;
                    }
                    else if (selectedWidgets.Count == 1)
                    {
                        dashboardVm.RemoveWidgetCommand.Execute(selectedWidgets[0]);
                        ClearAllSelection();
                        e.Handled = true;
                        return;
                    }
                }
            }

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

        private bool IsPointNearSegment(Point p, Point s1, Point s2, double maxDistance)
        {
            return GridMathHelper.IsPointNearSegment(p, s1, s2, maxDistance);
        }

        private void GetSnappedPosition(Control child, Point pointer, out double snappedCol, out double snappedRow, out (PipeWidgetViewModel pipeVm, bool isStart)? connectedPipe)
        {
            connectedPipe = null;
            double deltaX = pointer.X - _dragStartPoint.X;
            double deltaY = pointer.Y - _dragStartPoint.Y;

            double tentativeCol = Math.Max(0, _dragOriginalCol + deltaX / CellWidth);
            double tentativeRow = Math.Max(0, _dragOriginalRow + deltaY / CellHeight);

            var vm = child.DataContext as WidgetViewModelBase;
            bool isSnapCandidate = vm != null && (
                string.Equals(vm.Type, "Valve", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(vm.Type, "Pump", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(vm.Type, "HeatExchanger", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(vm.Type, "Reactor", StringComparison.OrdinalIgnoreCase));

            if (isSnapCandidate && vm != null)
            {
                var (p1Grid, p2Grid) = VisualPortHelper.GetVisualPortsInGrid(child, vm, this);
                double relX1 = p1Grid.X - vm.Col;
                double relY1 = p1Grid.Y - vm.Row;
                double relX2 = p2Grid.X - vm.Col;
                double relY2 = p2Grid.Y - vm.Row;

                var candidatePort1 = new Point(tentativeCol + relX1, tentativeRow + relY1);
                var candidatePort2 = new Point(tentativeCol + relX2, tentativeRow + relY2);

                double bestDist = 25.0; // 25px snap capture radius
                double bestCol = Math.Round(tentativeCol);
                double bestRow = Math.Round(tentativeRow);
                Point? bestSnapTarget = null;
                PipeWidgetViewModel? bestPipe = null;
                bool bestIsStart = false;

                foreach (var otherChild in Children)
                {
                    if (otherChild.DataContext is PipeWidgetViewModel pipeVm)
                    {
                        // Skip pipes that are already connected and moving with this widget
                        if (_connectedPipePoints.Any(c => c.PipeVm == pipeVm)) continue;

                        var pts = pipeVm.GetAbsoluteGridPoints();
                        if (pts.Count < 2) continue;

                        var pipeEndpoints = new[] { (pts[0], true), (pts[pts.Count - 1], false) };
                        foreach (var (pipePt, isStart) in pipeEndpoints)
                        {
                            // Check distance to candidatePort1
                            double dx1 = (candidatePort1.X - pipePt.X) * CellWidth;
                            double dy1 = (candidatePort1.Y - pipePt.Y) * CellHeight;
                            double dist1 = Math.Sqrt(dx1 * dx1 + dy1 * dy1);
                            if (dist1 <= bestDist)
                            {
                                bestDist = dist1;
                                bestCol = pipePt.X - relX1;
                                bestRow = pipePt.Y - relY1;
                                bestSnapTarget = new Point(pipePt.X * CellWidth + CellWidth / 2.0, pipePt.Y * CellHeight + CellHeight / 2.0);
                                bestPipe = pipeVm;
                                bestIsStart = isStart;
                            }

                            // Check distance to candidatePort2
                            double dx2 = (candidatePort2.X - pipePt.X) * CellWidth;
                            double dy2 = (candidatePort2.Y - pipePt.Y) * CellHeight;
                            double dist2 = Math.Sqrt(dx2 * dx2 + dy2 * dy2);
                            if (dist2 <= bestDist)
                            {
                                bestDist = dist2;
                                bestCol = pipePt.X - relX2;
                                bestRow = pipePt.Y - relY2;
                                bestSnapTarget = new Point(pipePt.X * CellWidth + CellWidth / 2.0, pipePt.Y * CellHeight + CellHeight / 2.0);
                                bestPipe = pipeVm;
                                bestIsStart = isStart;
                            }
                        }
                    }
                }

                if (bestSnapTarget.HasValue)
                {
                    snappedCol = bestCol;
                    snappedRow = bestRow;
                    ActiveSnapTarget = bestSnapTarget;
                    _selectionOverlay?.InvalidateVisual();
                    if (bestPipe != null)
                    {
                        connectedPipe = (bestPipe, bestIsStart);
                    }
                    return;
                }
            }

            if (ActiveSnapTarget != null)
            {
                ActiveSnapTarget = null;
                _selectionOverlay?.InvalidateVisual();
            }

            snappedCol = Math.Max(0, Math.Round(tentativeCol));
            snappedRow = Math.Max(0, Math.Round(tentativeRow));
        }

        private void GetSnappedPosition(Control child, Point pointer, out double snappedCol, out double snappedRow)
        {
            GetSnappedPosition(child, pointer, out snappedCol, out snappedRow, out _);
        }
    }
}