using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using Avalonia.VisualTree;
using AvaloniaApplication1.ViewModels;

namespace AvaloniaApplication1.Views
{
    public class SelectionOverlay : Control
    {
        private DashboardPanel? _panel;

        // Cached brushes and pens for zero-allocation rendering in Render()
        private static readonly IBrush SelectionFillBrush = new ImmutableSolidColorBrush(Color.FromArgb(30, 0, 122, 255));
        private static readonly IPen SelectionBorderPen = new ImmutablePen(new ImmutableSolidColorBrush(Color.FromRgb(0, 122, 255)), 2.0);
        private static readonly IBrush HandleBrush = Brushes.White;
        private static readonly IPen HandlePen = new ImmutablePen(new ImmutableSolidColorBrush(Color.FromRgb(0, 122, 255)), 1.5);

        private static readonly IPen SnapRingPen = new ImmutablePen(new ImmutableSolidColorBrush(Color.FromRgb(0, 230, 118)), 2.5);
        private static readonly IBrush SnapFillBrush = new ImmutableSolidColorBrush(Color.FromArgb(70, 0, 230, 118));

        private static readonly IBrush RubberBandFillBrush = new ImmutableSolidColorBrush(Color.FromArgb(35, 0, 122, 255));
        private static readonly IPen RubberBandBorderPen = new ImmutablePen(
            new ImmutableSolidColorBrush(Color.FromRgb(0, 122, 255)),
            1.5,
            new ImmutableDashStyle(new double[] { 4, 3 }, 0));

        public SelectionOverlay()
        {
            IsHitTestVisible = false;
        }

        public SelectionOverlay(DashboardPanel panel) : this()
        {
            _panel = panel;
        }

        public DashboardPanel? Panel
        {
            get => _panel;
            set
            {
                if (_panel != value)
                {
                    _panel = value;
                    InvalidateVisual();
                }
            }
        }

        protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
        {
            base.OnAttachedToVisualTree(e);
            if (_panel == null)
            {
                var view = this.FindAncestorOfType<DashboardView>();
                var panel = view?.FindDescendantOfType<DashboardPanel>();
                if (panel != null)
                {
                    _panel = panel;
                    panel.SelectionOverlay = this;
                    InvalidateVisual();
                }
            }
        }

        public override void Render(DrawingContext context)
        {
            base.Render(context);

            if (_panel == null || !_panel.IsDesignMode) return;

            // 1. Render Rubber-band Selection Box if currently dragging
            if (_panel.SelectionBoxRect.HasValue)
            {
                var box = _panel.SelectionBoxRect.Value;
                if (box.Width > 1 && box.Height > 1)
                {
                    context.DrawRectangle(RubberBandFillBrush, RubberBandBorderPen, box, 2, 2);
                }
            }

            // 2. Render Selection Borders and Handles for ALL Selected Widgets
            var selectedWidgets = _panel.GetSelectedWidgets();
            foreach (var widgetVm in selectedWidgets)
            {
                double col, row;
                double sizeX, sizeY;

                if (widgetVm is PipeWidgetViewModel pipeVm)
                {
                    var points = pipeVm.GetAbsoluteGridPoints();
                    if (points.Count >= 2)
                    {
                        double minX = points.Min(p => p.X);
                        double maxX = points.Max(p => p.X);
                        double minY = points.Min(p => p.Y);
                        double maxY = points.Max(p => p.Y);

                        col = Math.Floor(minX);
                        row = Math.Floor(minY);
                        sizeX = Math.Max(1.0, Math.Ceiling(maxX - col));
                        sizeY = Math.Max(1.0, Math.Ceiling(maxY - row));
                    }
                    else
                    {
                        col = pipeVm.Col;
                        row = pipeVm.Row;
                        sizeX = pipeVm.SizeX;
                        sizeY = pipeVm.SizeY;
                    }
                }
                else
                {
                    col = widgetVm.Col;
                    row = widgetVm.Row;
                    sizeX = widgetVm.SizeX;
                    sizeY = widgetVm.SizeY;
                }

                double x = col * _panel.CellWidth;
                double y = row * _panel.CellHeight;
                double w = sizeX * _panel.CellWidth;
                double h = sizeY * _panel.CellHeight;

                context.DrawRectangle(SelectionFillBrush, SelectionBorderPen, new Rect(x, y, w, h), 4, 4);

                // Draw corner handles
                context.DrawEllipse(HandleBrush, HandlePen, new Point(x, y), 4, 4);
                context.DrawEllipse(HandleBrush, HandlePen, new Point(x + w, y), 4, 4);
                context.DrawEllipse(HandleBrush, HandlePen, new Point(x, y + h), 4, 4);
                context.DrawEllipse(HandleBrush, HandlePen, new Point(x + w, y + h), 4, 4);
            }

            // 3. Render Snap Target Marker
            if (_panel.ActiveSnapTarget.HasValue)
            {
                var snapPt = _panel.ActiveSnapTarget.Value;
                context.DrawEllipse(SnapFillBrush, SnapRingPen, snapPt, 11, 11);
                context.DrawEllipse(Brushes.White, null, snapPt, 3.5, 3.5);
            }
        }
    }
}
