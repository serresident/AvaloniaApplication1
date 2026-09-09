using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.VisualTree;
using AvaloniaApplication1.ViewModels;

namespace AvaloniaApplication1.Views.DashboardPanelHelpers
{
    public static class VisualPortHelper
    {
        public static bool IsDragHandle(object? source)
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

        public static Border? FindDragHandleRecursive(Avalonia.Visual? visual)
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

        public static ValveControl? FindValveControlRecursive(Control control)
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

        public static HeatExchangerControl? FindHeatExchangerControlRecursive(Control control)
        {
            if (control is HeatExchangerControl he) return he;
            if (control is Panel panel)
            {
                foreach (var child in panel.Children)
                {
                    var res = FindHeatExchangerControlRecursive(child);
                    if (res != null) return res;
                }
            }
            else if (control is ContentControl cc && cc.Content is Control contentControl)
            {
                return FindHeatExchangerControlRecursive(contentControl);
            }
            else if (control is ContentPresenter cp && cp.Child is Control childControl)
            {
                return FindHeatExchangerControlRecursive(childControl);
            }
            return null;
        }

        public static ReactorControl? FindReactorControlRecursive(Control control)
        {
            if (control is ReactorControl reactor) return reactor;
            if (control is Panel panel)
            {
                foreach (var child in panel.Children)
                {
                    var res = FindReactorControlRecursive(child);
                    if (res != null) return res;
                }
            }
            else if (control is ContentControl cc && cc.Content is Control contentControl)
            {
                return FindReactorControlRecursive(contentControl);
            }
            else if (control is ContentPresenter cp && cp.Child is Control childControl)
            {
                return FindReactorControlRecursive(childControl);
            }
            return null;
        }

        public static Viewbox? FindViewboxRecursive(Control control)
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

        public static PipeControl? FindPipeControlRecursive(Control control)
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

        public static void FindPipeControlsRecursive(Control control, List<PipeControl> result, PipeControl activePipe)
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

        public static List<PipeControl> FindAllSiblingPipesFor(Panel panel, PipeControl activePipe)
        {
            var list = new List<PipeControl>();
            foreach (var child in panel.Children)
            {
                FindPipeControlsRecursive(child, list, activePipe);
            }
            return list;
        }

        public static Point GetGraphicsCenter(Control child, WidgetViewModelBase vm, DashboardPanel panel)
        {
            double col = DashboardPanel.GetCol(child);
            double row = DashboardPanel.GetRow(child);
            int sizeX = DashboardPanel.GetSizeX(child);
            int sizeY = DashboardPanel.GetSizeY(child);

            // Default center based on layout
            double defaultCx = col * panel.CellWidth + sizeX * panel.CellWidth / 2;
            double defaultCy = row * panel.CellHeight + sizeY * panel.CellHeight / 2;

            if (string.Equals(vm.Type, "Valve", StringComparison.OrdinalIgnoreCase))
            {
                var valveControl = FindValveControlRecursive(child);
                if (valveControl != null)
                {
                    var pt = valveControl.TranslatePoint(new Point(valveControl.Bounds.Width / 2, valveControl.Bounds.Height / 2), panel);
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
                    var pt = viewbox.TranslatePoint(new Point(viewbox.Bounds.Width / 2, viewbox.Bounds.Height / 2), panel);
                    if (pt.HasValue)
                    {
                        return pt.Value;
                    }
                }
            }

            return new Point(defaultCx, defaultCy);
        }

        public static (Point p1, Point p2) GetVisualPortsInGrid(Control child, WidgetViewModelBase vm, DashboardPanel panel)
        {
            double col = DashboardPanel.GetCol(child);
            double row = DashboardPanel.GetRow(child);
            int sizeX = DashboardPanel.GetSizeX(child);
            int sizeY = DashboardPanel.GetSizeY(child);

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

            var centerPt = GetGraphicsCenter(child, vm, panel);
            double localOffsetX = centerPt.X - col * panel.CellWidth;
            double localOffsetY = centerPt.Y - row * panel.CellHeight;
            double gridCx = col + localOffsetX / panel.CellWidth;
            double gridCy = row + localOffsetY / panel.CellHeight;

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
                    
                    var lp1Rot = GridMathHelper.RotatePoint(lp1, new Point(w / 2.0, h / 2.0), angle);
                    var lp2Rot = GridMathHelper.RotatePoint(lp2, new Point(w / 2.0, h / 2.0), angle);
                    
                    var p1PixelOpt = valveControl.TranslatePoint(lp1Rot, panel);
                    var p2PixelOpt = valveControl.TranslatePoint(lp2Rot, panel);
                    
                    if (p1PixelOpt.HasValue && p2PixelOpt.HasValue)
                    {
                        var p1Grid = new Point(
                            (p1PixelOpt.Value.X - panel.CellWidth / 2.0) / panel.CellWidth,
                            (p1PixelOpt.Value.Y - panel.CellHeight / 2.0) / panel.CellHeight);
                        var p2Grid = new Point(
                            (p2PixelOpt.Value.X - panel.CellWidth / 2.0) / panel.CellWidth,
                            (p2PixelOpt.Value.Y - panel.CellHeight / 2.0) / panel.CellHeight);
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
                    double halfFlowGrid = (flowSize / 2.0) / (isFlowVertical ? panel.CellHeight : panel.CellWidth);

                    var p1Grid = isFlowVertical ? new Point(gridCx, gridCy - halfFlowGrid) : new Point(gridCx - halfFlowGrid, gridCy);
                    var p2Grid = isFlowVertical ? new Point(gridCx, gridCy + halfFlowGrid) : new Point(gridCx + halfFlowGrid, gridCy);
                    return (p1Grid, p2Grid);
                }
            }
            else if (string.Equals(vm.Type, "HeatExchanger", StringComparison.OrdinalIgnoreCase))
            {
                // Port 1: Верхний левый патрубок (Hot In)
                var p1Grid = new Point(col + sizeX * 0.22, row);
                // Port 2: Нижний правый патрубок (Hot Out)
                var p2Grid = new Point(col + sizeX * 0.78, row + sizeY);
                return (p1Grid, p2Grid);
            }
            else if (string.Equals(vm.Type, "Reactor", StringComparison.OrdinalIgnoreCase))
            {
                // Port 1: Верхний загрузочный штуцер
                var p1Grid = new Point(col + sizeX * 0.2, row);
                // Port 2: Нижний сливной штуцер
                var p2Grid = new Point(col + sizeX * 0.5, row + sizeY);
                return (p1Grid, p2Grid);
            }

            return (fallbackP1, fallbackP2);
        }
    }
}
