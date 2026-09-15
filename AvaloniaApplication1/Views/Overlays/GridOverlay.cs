using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.VisualTree;

namespace AvaloniaApplication1.Views
{
    public class GridOverlay : Control
    {
        private DashboardPanel? _panel;

        public GridOverlay()
        {
            IsHitTestVisible = false;
        }

        public GridOverlay(DashboardPanel panel) : this()
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

        public double CellWidth { get; set; } = 150;
        public double CellHeight { get; set; } = 150;
        public bool ShowHighlight { get; set; }
        public double HighlightCol { get; set; }
        public double HighlightRow { get; set; }
        public int HighlightSizeX { get; set; } = 1;
        public int HighlightSizeY { get; set; } = 1;

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
                    panel.GridOverlay = this;
                    InvalidateVisual();
                }
            }
        }

        public override void Render(DrawingContext context)
        {
            base.Render(context);

            bool isDesignMode = _panel?.IsDesignMode ?? false;
            if (!isDesignMode) return;

            double cellW = _panel != null ? _panel.CellWidth : CellWidth;
            double cellH = _panel != null ? _panel.CellHeight : CellHeight;

            // Draw grid lines
            var pen = new Pen(new SolidColorBrush(Color.FromArgb(40, 255, 255, 255)), 1,
                new DashStyle(new double[] { 4, 4 }, 0));

            double width = Bounds.Width;
            double height = Bounds.Height;

            for (double x = 0; x <= width; x += cellW)
            {
                context.DrawLine(pen, new Point(x, 0), new Point(x, height));
            }

            for (double y = 0; y <= height; y += cellH)
            {
                context.DrawLine(pen, new Point(0, y), new Point(width, y));
            }

            // Draw drop target highlight while dragging
            if (ShowHighlight)
            {
                var highlightBrush = new SolidColorBrush(Color.FromArgb(60, 0, 122, 204));
                var highlightRect = new Rect(
                    HighlightCol * cellW, HighlightRow * cellH,
                    HighlightSizeX * cellW, HighlightSizeY * cellH);

                context.DrawRectangle(highlightBrush, null, highlightRect);

                var borderPen = new Pen(new SolidColorBrush(Color.FromArgb(180, 0, 122, 204)), 2);
                context.DrawRectangle(null, borderPen, highlightRect);
            }
        }
    }
}
