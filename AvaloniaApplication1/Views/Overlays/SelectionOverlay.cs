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
                Control? selectedControl = null;
                foreach (var child in _panel.Children)
                {
                    if (child.DataContext == _panel.SelectedVm)
                    {
                        selectedControl = child;
                        break;
                    }
                }

                if (selectedControl != null)
                {
                    double x = DashboardPanel.GetCol(selectedControl) * _panel.CellWidth;
                    double y = DashboardPanel.GetRow(selectedControl) * _panel.CellHeight;
                    double w = DashboardPanel.GetSizeX(selectedControl) * _panel.CellWidth;
                    double h = DashboardPanel.GetSizeY(selectedControl) * _panel.CellHeight;

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
    }

}
