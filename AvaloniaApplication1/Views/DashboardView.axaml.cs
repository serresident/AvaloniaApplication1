using System;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.VisualTree;

namespace AvaloniaApplication1.Views
{
    public partial class DashboardView : UserControl
    {
        private bool _isSpacePressed;
        private bool _isPanning;
        private Avalonia.Point _lastPointerPosition;

        private Avalonia.Threading.DispatcherTimer? _panTimer;
        private Avalonia.Vector _targetOffset;
        private Avalonia.Vector _currentOffset;
        private Avalonia.Vector _velocity;
        private DateTime _lastMoveTime;

        public DashboardView()
        {
            InitializeComponent();
            Focusable = true;
            AddHandler(PointerWheelChangedEvent, OnPointerWheelChanged, RoutingStrategies.Tunnel);
            AddHandler(KeyDownEvent, OnKeyDown, RoutingStrategies.Tunnel);
            AddHandler(KeyUpEvent, OnKeyUp, RoutingStrategies.Tunnel);
            AddHandler(PointerPressedEvent, OnPointerPressed, RoutingStrategies.Tunnel);
            AddHandler(PointerMovedEvent, OnPointerMoved, RoutingStrategies.Tunnel);
            AddHandler(PointerReleasedEvent, OnPointerReleased, RoutingStrategies.Tunnel);
            AddHandler(PointerCaptureLostEvent, OnPointerCaptureLost, RoutingStrategies.Tunnel);
            
            LostFocus += (s, e) => {
                var topLevel = Avalonia.Controls.TopLevel.GetTopLevel(this);
                var newFocus = topLevel?.FocusManager?.GetFocusedElement() as Avalonia.Visual;
                var current = newFocus;
                while (current != null)
                {
                    if (current == this) return;
                    current = current.GetVisualParent();
                }
                _isSpacePressed = false;
                _isPanning = false;
                _panTimer?.Stop();
                UpdateCursor();
            };

            _panTimer = new Avalonia.Threading.DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(16)
            };
            _panTimer.Tick += OnPanTimerTick;
        }

        private void UpdateCursor()
        {
            if (_isPanning)
            {
                Cursor = new Cursor(StandardCursorType.SizeAll);
            }
            else if (_isSpacePressed)
            {
                Cursor = new Cursor(StandardCursorType.Hand);
            }
            else
            {
                Cursor = null;
            }
        }

        private bool IsDesignModeActive()
        {
            return DataContext is ViewModels.DashboardViewModel viewModel && viewModel.ProjectContext.IsDesignMode;
        }

        private void OnKeyDown(object? sender, KeyEventArgs e)
        {
            if (e.Key == Key.Space && IsDesignModeActive())
            {
                _isSpacePressed = true;
                UpdateCursor();
                e.Handled = true;
            }
        }

        private void OnKeyUp(object? sender, KeyEventArgs e)
        {
            if (e.Key == Key.Space)
            {
                _isSpacePressed = false;
                _isPanning = false;
                UpdateCursor();
                e.Handled = true;
            }
        }

        private void OnPointerPressed(object? sender, PointerPressedEventArgs e)
        {
            PipeControl.CloseActiveMenu();

            if (IsDesignModeActive() && _isSpacePressed && e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            {
                _isPanning = true;
                _lastPointerPosition = e.GetPosition(this);

                var scrollViewer = this.FindControl<ScrollViewer>("DashboardScrollViewer");
                if (scrollViewer != null)
                {
                    _targetOffset = scrollViewer.Offset;
                    _currentOffset = scrollViewer.Offset;
                }

                _velocity = new Avalonia.Vector(0, 0);
                _lastMoveTime = DateTime.UtcNow;

                e.Pointer.Capture(this);
                UpdateCursor();

                _panTimer?.Start();
                e.Handled = true;
            }
        }

        private void OnPointerMoved(object? sender, PointerEventArgs e)
        {
            if (_isPanning)
            {
                var pointerPoint = e.GetCurrentPoint(this);
                if (!pointerPoint.Properties.IsLeftButtonPressed)
                {
                    _isPanning = false;
                    _velocity = new Avalonia.Vector(0, 0);
                    _panTimer?.Stop();
                    if (e.Pointer.Captured == this)
                    {
                        e.Pointer.Capture(null);
                    }
                    UpdateCursor();
                    return;
                }

                var currentPos = e.GetPosition(this);
                var delta = currentPos - _lastPointerPosition;

                _targetOffset = new Avalonia.Vector(_targetOffset.X - delta.X, _targetOffset.Y - delta.Y);

                // Calculate velocity (pixels per second)
                var now = DateTime.UtcNow;
                double dt = (now - _lastMoveTime).TotalSeconds;
                if (dt > 0.001)
                {
                    _velocity = new Avalonia.Vector(-delta.X / dt, -delta.Y / dt);
                    // Limit max velocity to prevent huge jumps from errors
                    double maxVel = 3000.0;
                    if (_velocity.Length > maxVel)
                    {
                        _velocity = _velocity / _velocity.Length * maxVel;
                    }
                }
                _lastMoveTime = now;

                _lastPointerPosition = currentPos;
                e.Handled = true;
            }
        }

        private void OnPointerReleased(object? sender, PointerReleasedEventArgs e)
        {
            if (_isPanning)
            {
                _isPanning = false;

                // Stop inertia if user paused before releasing mouse
                if ((DateTime.UtcNow - _lastMoveTime).TotalMilliseconds > 100)
                {
                    _velocity = new Avalonia.Vector(0, 0);
                }

                e.Pointer.Capture(null);
                UpdateCursor();
                e.Handled = true;
            }
        }

        private void OnPanTimerTick(object? sender, EventArgs e)
        {
            var scrollViewer = this.FindControl<ScrollViewer>("DashboardScrollViewer");
            if (scrollViewer == null)
            {
                _panTimer?.Stop();
                return;
            }

            double dt = 0.016; // 60 FPS
            bool isDragging = _isPanning;

            if (!isDragging)
            {
                // Decelerate and scroll with inertia
                if (_velocity.Length > 5.0)
                {
                    _targetOffset += _velocity * dt;
                    _velocity *= Math.Pow(0.90, dt * 60.0); // decay by 10% per 16.6ms
                }
                else
                {
                    _velocity = new Avalonia.Vector(0, 0);
                }
            }

            // Lerp current offset toward target offset
            var diff = _targetOffset - _currentOffset;
            if (diff.Length > 0.1 || _velocity.Length > 0.1)
            {
                double lerpFactor = isDragging ? 0.3 : 0.15;
                _currentOffset += diff * lerpFactor;

                // Clamp to ScrollViewer bounds
                double maxCol = scrollViewer.Extent.Width - scrollViewer.Viewport.Width;
                double maxRow = scrollViewer.Extent.Height - scrollViewer.Viewport.Height;

                double clampX = Math.Clamp(_currentOffset.X, 0, Math.Max(0, maxCol));
                double clampY = Math.Clamp(_currentOffset.Y, 0, Math.Max(0, maxRow));
                _currentOffset = new Avalonia.Vector(clampX, clampY);

                _targetOffset = new Avalonia.Vector(
                    Math.Clamp(_targetOffset.X, 0, Math.Max(0, maxCol)),
                    Math.Clamp(_targetOffset.Y, 0, Math.Max(0, maxRow))
                );

                scrollViewer.Offset = _currentOffset;
            }
            else
            {
                if (!isDragging)
                {
                    scrollViewer.Offset = _targetOffset;
                    _panTimer?.Stop();
                }
            }
        }
        protected override void OnAttachedToVisualTree(Avalonia.VisualTreeAttachmentEventArgs e)
        {
            base.OnAttachedToVisualTree(e);
            var window = Avalonia.Controls.TopLevel.GetTopLevel(this) as Window;
            if (window != null)
            {
                window.Deactivated += Window_Deactivated;
            }
        }

        protected override void OnDetachedFromVisualTree(Avalonia.VisualTreeAttachmentEventArgs e)
        {
            base.OnDetachedFromVisualTree(e);
            var window = Avalonia.Controls.TopLevel.GetTopLevel(this) as Window;
            if (window != null)
            {
                window.Deactivated -= Window_Deactivated;
            }
        }

        private void Window_Deactivated(object? sender, EventArgs e)
        {
            _isSpacePressed = false;
            _isPanning = false;
            _panTimer?.Stop();
            UpdateCursor();
        }

        private void OnPointerCaptureLost(object? sender, PointerCaptureLostEventArgs e)
        {
            if (_isPanning)
            {
                _isPanning = false;
                _velocity = new Avalonia.Vector(0, 0);
                _panTimer?.Stop();
                UpdateCursor();
            }
        }

        private void OnPointerWheelChanged(object? sender, PointerWheelEventArgs e)
        {
            if (e.KeyModifiers.HasFlag(KeyModifiers.Control) && e.KeyModifiers.HasFlag(KeyModifiers.Shift))
            {
                if (DataContext is ViewModels.DashboardViewModel viewModel && viewModel.ProjectContext.IsDesignMode)
                {
                    double delta = e.Delta.Y > 0 ? 0.1 : -0.1;
                    viewModel.AdjustZoom(delta);
                    e.Handled = true;
                }
            }
        }

        private void ContextMenu_Opened(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            // Debugging code removed to prevent crashes on invalid hardcoded paths.
        }
    }
}