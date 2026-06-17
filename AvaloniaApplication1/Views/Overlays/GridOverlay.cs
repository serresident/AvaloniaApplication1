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
}
