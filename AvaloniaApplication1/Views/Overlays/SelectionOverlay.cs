using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.VisualTree;
using AvaloniaApplication1.ViewModels;

namespace AvaloniaApplication1.Views
{
    public class SelectionOverlay : Control
    {
        private DashboardPanel? _panel;

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

            if (_panel != null && _panel.IsDesignMode && _panel.SelectedVm != null)
            {
                Control? selectedControl = null;
                foreach (var child in _panel.Children)
                {
                    if (child.DataContext == _panel.SelectedVm)
                    {
                        selectedControl = child;
                        break;
                    }
                }

                double col, row;
                double sizeX, sizeY;

                if (_panel.SelectedVm is PipeWidgetViewModel pipeVm)
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
                    col = _panel.SelectedVm.Col;
                    row = _panel.SelectedVm.Row;
                    sizeX = _panel.SelectedVm.SizeX;
                    sizeY = _panel.SelectedVm.SizeY;
                }

                double x = col * _panel.CellWidth;
                double y = row * _panel.CellHeight;
                double w = sizeX * _panel.CellWidth;
                double h = sizeY * _panel.CellHeight;

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

            if (_panel != null && _panel.IsDesignMode && _panel.ActiveSnapTarget.HasValue)
            {
                var snapPt = _panel.ActiveSnapTarget.Value;
                var snapRingPen = new Pen(new SolidColorBrush(Color.FromRgb(0, 230, 118)), 2.5);
                var snapFill = new SolidColorBrush(Color.FromArgb(70, 0, 230, 118));
                context.DrawEllipse(snapFill, snapRingPen, snapPt, 11, 11);
                context.DrawEllipse(Brushes.White, null, snapPt, 3.5, 3.5);
            }
        }
    }
}
