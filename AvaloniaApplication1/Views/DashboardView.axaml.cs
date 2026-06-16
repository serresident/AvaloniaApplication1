using System;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;

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
            
            LostFocus += (s, e) => {
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

        private void OnKeyDown(object? sender, KeyEventArgs e)
        {
            if (e.Key == Key.Space)
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
            if (_isSpacePressed && e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
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

        private void OnPointerWheelChanged(object? sender, PointerWheelEventArgs e)
        {
            if (e.KeyModifiers.HasFlag(KeyModifiers.Control) && e.KeyModifiers.HasFlag(KeyModifiers.Shift))
            {
                if (DataContext is ViewModels.DashboardViewModel viewModel && viewModel.ProjectContext.IsDesignMode)
                {
                    double delta = e.Delta.Y > 0 ? 0.1 : -0.1;
                    viewModel.ZoomScale = Math.Clamp(viewModel.ZoomScale + delta, 0.5, 3.0);
                    e.Handled = true;
                }
            }
        }

        private void ContextMenu_Opened(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            if (sender is ContextMenu menu)
            {
                var target = menu.PlacementTarget;
                var tag = target?.Tag;
                var parent = menu.Parent;
                var parentTag = (parent is Control c) ? c.Tag : null;
                var dc = menu.DataContext;
                
                var logLines = new System.Collections.Generic.List<string>();
                logLines.Add("--- ContextMenu Opened Diagnostic ---");
                logLines.Add($"Timestamp: {DateTime.Now}");
                logLines.Add($"Menu: {menu.GetType().FullName}");
                logLines.Add($"Menu.DataContext: {(dc == null ? "null" : dc.GetType().FullName)}");
                logLines.Add($"PlacementTarget: {(target == null ? "null" : target.GetType().FullName)}");
                logLines.Add($"PlacementTarget.Tag: {(tag == null ? "null" : tag.GetType().FullName)}");
                logLines.Add($"Parent: {(parent == null ? "null" : parent.GetType().FullName)}");
                logLines.Add($"Parent.Tag: {(parentTag == null ? "null" : parentTag.GetType().FullName)}");
                
                if (menu.ItemsSource != null)
                {
                    logLines.Add($"ItemsSource: {menu.ItemsSource.GetType().FullName}");
                }
                else
                {
                    logLines.Add("ItemsSource is null");
                }
                
                var items = menu.Items;
                if (items != null)
                {
                    logLines.Add($"Items count: {items.Count}");
                    for (int i = 0; i < items.Count; i++)
                    {
                        var item = items[i];
                        if (item is MenuItem mi)
                        {
                            logLines.Add($"  Item {i}: Header='{mi.Header}', Command={(mi.Command == null ? "null" : mi.Command.GetType().FullName)}, CommandParameter={(mi.CommandParameter == null ? "null" : mi.CommandParameter.GetType().FullName)}, IsEnabled={mi.IsEnabled}");
                        }
                        else
                        {
                            logLines.Add($"  Item {i}: {item.GetType().FullName}");
                        }
                    }
                }
                
                System.IO.File.WriteAllLines(@"c:\Users\ess2\source\repos\AvaloniaApplication1\debug_log.txt", logLines);
            }
        }
    }
}