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
        private static readonly IBrush WindowFillBrush = new ImmutableSolidColorBrush(Color.FromArgb(35, 0, 120, 215));
        private static readonly IPen WindowBorderPen = new ImmutablePen(new ImmutableSolidColorBrush(Color.FromRgb(0, 120, 215)), 1.5);

        private static readonly IBrush CrossingFillBrush = new ImmutableSolidColorBrush(Color.FromArgb(35, 40, 167, 69));
        private static readonly IPen CrossingBorderPen = new ImmutablePen(
            new ImmutableSolidColorBrush(Color.FromRgb(40, 167, 69)),
            1.5,
            new ImmutableDashStyle(new double[] { 4, 3 }, 0));

        private static readonly IBrush SelectionFillBrush = new ImmutableSolidColorBrush(Color.FromArgb(30, 0, 122, 255));
        private static readonly IPen SelectionBorderPen = new ImmutablePen(new ImmutableSolidColorBrush(Color.FromRgb(0, 122, 255)), 2.0);
        private static readonly IBrush ItemLightBrush = new ImmutableSolidColorBrush(Color.FromArgb(15, 0, 120, 215));
        private static readonly IPen ItemLightPen = new ImmutablePen(new ImmutableSolidColorBrush(Color.FromArgb(140, 0, 120, 215)), 1.0);

        private static readonly IPen GroupBoundingPen = new ImmutablePen(
            new ImmutableSolidColorBrush(Color.FromRgb(0, 120, 215)),
            1.5,
            new ImmutableDashStyle(new double[] { 5, 3 }, 0));

        private static readonly IBrush HandleBrush = Brushes.White;
        private static readonly IPen HandlePen = new ImmutablePen(new ImmutableSolidColorBrush(Color.FromRgb(0, 122, 255)), 1.5);

        private static readonly IPen SnapRingPen = new ImmutablePen(new ImmutableSolidColorBrush(Color.FromRgb(0, 230, 118)), 2.5);
        private static readonly IBrush SnapFillBrush = new ImmutableSolidColorBrush(Color.FromArgb(70, 0, 230, 118));

        private static readonly IPen SmartGuidePen = new ImmutablePen(
            new ImmutableSolidColorBrush(Color.FromRgb(224, 64, 251)),
            1.2,
            new ImmutableDashStyle(new double[] { 4, 3 }, 0));

        private static readonly IBrush CoordBadgeBg = new ImmutableSolidColorBrush(Color.FromArgb(230, 20, 25, 35));
        private static readonly IPen CoordBadgeBorder = new ImmutablePen(new ImmutableSolidColorBrush(Color.FromRgb(0, 122, 255)), 1.0);
        private static readonly IBrush CoordBadgeTextBrush = Brushes.White;
        private static readonly Typeface BadgeTypeface = new Typeface("Inter", FontStyle.Normal, FontWeight.SemiBold);

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

            // 1. Render CAD Rubber-band Selection Box (Window = Blue / Crossing = Green Dash)
            if (_panel.SelectionBoxRect.HasValue)
            {
                var box = _panel.SelectionBoxRect.Value;
                if (box.Width > 1 && box.Height > 1)
                {
                    if (_panel.IsCrossingSelection)
                    {
                        context.DrawRectangle(CrossingFillBrush, CrossingBorderPen, box, 2, 2);
                    }
                    else
                    {
                        context.DrawRectangle(WindowFillBrush, WindowBorderPen, box, 2, 2);
                    }
                }
            }

            // 2. Render Selection Borders and Handles
            var selectedWidgets = _panel.GetSelectedWidgets();
            if (selectedWidgets.Count == 0) return;

            bool isGroup = selectedWidgets.Count > 1;

            double groupMinX = double.MaxValue;
            double groupMinY = double.MaxValue;
            double groupMaxX = double.MinValue;
            double groupMaxY = double.MinValue;

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

                if (x < groupMinX) groupMinX = x;
                if (y < groupMinY) groupMinY = y;
                if (x + w > groupMaxX) groupMaxX = x + w;
                if (y + h > groupMaxY) groupMaxY = y + h;

                if (isGroup)
                {
                    // Light highlight for individual group members
                    context.DrawRectangle(ItemLightBrush, ItemLightPen, new Rect(x, y, w, h), 3, 3);
                }
                else
                {
                    // Full selection border & handles for single selected element
                    context.DrawRectangle(SelectionFillBrush, SelectionBorderPen, new Rect(x, y, w, h), 4, 4);

                    context.DrawEllipse(HandleBrush, HandlePen, new Point(x, y), 4, 4);
                    context.DrawEllipse(HandleBrush, HandlePen, new Point(x + w, y), 4, 4);
                    context.DrawEllipse(HandleBrush, HandlePen, new Point(x, y + h), 4, 4);
                    context.DrawEllipse(HandleBrush, HandlePen, new Point(x + w, y + h), 4, 4);
                }
            }

            // 3. Render Group Bounding Box with 8 Transformation Markers
            if (isGroup && groupMaxX > groupMinX && groupMaxY > groupMinY)
            {
                var groupRect = new Rect(groupMinX - 3, groupMinY - 3, (groupMaxX - groupMinX) + 6, (groupMaxY - groupMinY) + 6);
                context.DrawRectangle(null, GroupBoundingPen, groupRect, 3, 3);

                // 8 Group Handles (4 corners + 4 midpoints)
                Point[] groupHandles = new[]
                {
                    new Point(groupRect.Left, groupRect.Top),
                    new Point((groupRect.Left + groupRect.Right) / 2.0, groupRect.Top),
                    new Point(groupRect.Right, groupRect.Top),
                    new Point(groupRect.Right, (groupRect.Top + groupRect.Bottom) / 2.0),
                    new Point(groupRect.Right, groupRect.Bottom),
                    new Point((groupRect.Left + groupRect.Right) / 2.0, groupRect.Bottom),
                    new Point(groupRect.Left, groupRect.Bottom),
                    new Point(groupRect.Left, (groupRect.Top + groupRect.Bottom) / 2.0)
                };

                foreach (var pt in groupHandles)
                {
                    context.DrawRectangle(HandleBrush, HandlePen, new Rect(pt.X - 3.5, pt.Y - 3.5, 7, 7));
                }
            }

            // 4. Render Snap Target Marker
            if (_panel.ActiveSnapTarget.HasValue)
            {
                var snapPt = _panel.ActiveSnapTarget.Value;
                context.DrawEllipse(SnapFillBrush, SnapRingPen, snapPt, 11, 11);
                context.DrawEllipse(Brushes.White, null, snapPt, 3.5, 3.5);
            }

            // 5. Render Active Smart Alignment Guides
            if (_panel.ActiveSmartGuides.Count > 0)
            {
                foreach (var guide in _panel.ActiveSmartGuides)
                {
                    context.DrawLine(SmartGuidePen, guide.Start, guide.End);
                }
            }

            // 6. Render Coordinate Badge near Cursor
            if (_panel.DragCurrentBadge.HasValue)
            {
                var badge = _panel.DragCurrentBadge.Value;
                string text = string.Format(System.Globalization.CultureInfo.InvariantCulture, "X: {0:0.#}, Y: {1:0.#}", badge.Col, badge.Row);
                var formattedText = new FormattedText(
                    text,
                    System.Globalization.CultureInfo.InvariantCulture,
                    FlowDirection.LeftToRight,
                    BadgeTypeface,
                    11.0,
                    CoordBadgeTextBrush);

                double badgeW = formattedText.Width + 14;
                double badgeH = formattedText.Height + 8;
                double badgeX = badge.CursorPos.X + 16;
                double badgeY = badge.CursorPos.Y + 16;

                var badgeRect = new Rect(badgeX, badgeY, badgeW, badgeH);
                context.DrawRectangle(CoordBadgeBg, CoordBadgeBorder, badgeRect, 4, 4);
                context.DrawText(formattedText, new Point(badgeX + 7, badgeY + 4));
            }
        }
    }
}
