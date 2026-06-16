using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using AvaloniaApplication1.ViewModels;

namespace AvaloniaApplication1
{
    public partial class MainWindow : Window
    {
        private bool _isDragging;
        private Point _dragStartPointerPos;
        private double _dragStartWindowX;
        private double _dragStartWindowY;
        private ChildWindowViewModel? _draggedWindowVm;
        private Control? _draggedTitleBar;

        private bool _isResizing;
        private Point _resizeStartPointerPos;
        private double _resizeStartWidth;
        private double _resizeStartHeight;
        private ChildWindowViewModel? _resizedWindowVm;
        private Control? _resizeHandle;

        public MainWindow()
        {
            InitializeComponent();

            // Tunneling handler to bring clicked child window to front
            AddHandler(PointerPressedEvent, (s, e) =>
            {
                var visual = e.Source as Visual;
                while (visual != null)
                {
                    if (visual is Border border && border.DataContext is ChildWindowViewModel vm)
                    {
                        BringToFront(vm);
                        break;
                    }
                    visual = visual.GetVisualParent();
                }
            }, RoutingStrategies.Tunnel);
        }

        private void BringToFront(ChildWindowViewModel vm)
        {
            if (DataContext is MainViewModel mainVm)
            {
                int maxZ = 0;
                foreach (var w in mainVm.ActiveChildWindows)
                {
                    if (w != vm && w.ZIndex > maxZ)
                    {
                        maxZ = w.ZIndex;
                    }
                }
                vm.ZIndex = maxZ + 1;
            }
        }

        // ===== Dragging =====

        private void OnTitleBarPointerPressed(object sender, PointerPressedEventArgs e)
        {
            if (sender is Control control && control.DataContext is ChildWindowViewModel vm)
            {
                _isDragging = true;
                _draggedWindowVm = vm;
                _draggedTitleBar = control;

                _dragStartPointerPos = e.GetPosition(this);
                _dragStartWindowX = vm.X;
                _dragStartWindowY = vm.Y;

                e.Pointer.Capture(control);
                e.Handled = true;
            }
        }

        private void OnTitleBarPointerMoved(object sender, PointerEventArgs e)
        {
            if (_isDragging && _draggedWindowVm != null && _draggedTitleBar != null)
            {
                var currentPointerPos = e.GetPosition(this);
                double deltaX = currentPointerPos.X - _dragStartPointerPos.X;
                double deltaY = currentPointerPos.Y - _dragStartPointerPos.Y;

                double targetX = _dragStartWindowX + deltaX;
                double targetY = _dragStartWindowY + deltaY;

                var canvas = _draggedTitleBar.FindAncestorOfType<Canvas>();
                if (canvas != null)
                {
                    double width = _draggedWindowVm.Width;
                    targetY = Math.Clamp(targetY, 0, Math.Max(0, canvas.Bounds.Height - 36));
                    targetX = Math.Clamp(targetX, -width + 100, Math.Max(100, canvas.Bounds.Width - 100));
                }
                else
                {
                    targetY = Math.Max(0, targetY);
                }

                _draggedWindowVm.X = targetX;
                _draggedWindowVm.Y = targetY;

                e.Handled = true;
            }
        }

        private void OnTitleBarPointerReleased(object sender, PointerReleasedEventArgs e)
        {
            if (_isDragging && _draggedTitleBar != null)
            {
                e.Pointer.Capture(null);
                _isDragging = false;
                _draggedWindowVm = null;
                _draggedTitleBar = null;
                e.Handled = true;
            }
        }

        // ===== Resizing =====

        private void OnResizeHandlePointerPressed(object sender, PointerPressedEventArgs e)
        {
            if (sender is Control control && control.DataContext is ChildWindowViewModel vm)
            {
                _isResizing = true;
                _resizedWindowVm = vm;
                _resizeHandle = control;

                _resizeStartPointerPos = e.GetPosition(this);
                _resizeStartWidth = vm.Width;
                _resizeStartHeight = vm.Height;

                e.Pointer.Capture(control);
                e.Handled = true;
            }
        }

        private void OnResizeHandlePointerMoved(object sender, PointerEventArgs e)
        {
            if (_isResizing && _resizedWindowVm != null && _resizeHandle != null)
            {
                var currentPointerPos = e.GetPosition(this);
                double deltaX = currentPointerPos.X - _resizeStartPointerPos.X;
                double deltaY = currentPointerPos.Y - _resizeStartPointerPos.Y;

                double newWidth = Math.Max(200, _resizeStartWidth + deltaX);
                double newHeight = Math.Max(120, _resizeStartHeight + deltaY);

                var canvas = _resizeHandle.FindAncestorOfType<Canvas>();
                if (canvas != null)
                {
                    newWidth = Math.Min(newWidth, Math.Max(200, canvas.Bounds.Width - _resizedWindowVm.X));
                    newHeight = Math.Min(newHeight, Math.Max(120, canvas.Bounds.Height - _resizedWindowVm.Y));
                }

                _resizedWindowVm.Width = newWidth;
                _resizedWindowVm.Height = newHeight;

                e.Handled = true;
            }
        }

        private void OnResizeHandlePointerReleased(object sender, PointerReleasedEventArgs e)
        {
            if (_isResizing && _resizeHandle != null)
            {
                e.Pointer.Capture(null);
                _isResizing = false;
                _resizedWindowVm = null;
                _resizeHandle = null;
                e.Handled = true;
            }
        }
    }
}