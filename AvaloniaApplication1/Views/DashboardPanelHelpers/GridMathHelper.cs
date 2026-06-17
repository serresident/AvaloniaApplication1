using System;
using Avalonia;
using Avalonia.Controls;

namespace AvaloniaApplication1.Views.DashboardPanelHelpers
{
    public static class GridMathHelper
    {
        public static double CoerceCellSize(AvaloniaObject inst, double val) => Math.Clamp(val, 10.0, 500.0);

        public static void GetSnappedPosition(Point pointer, double cellWidth, double cellHeight, out double snappedCol, out double snappedRow)
        {
            snappedCol = Math.Floor(pointer.X / cellWidth);
            snappedRow = Math.Floor(pointer.Y / cellHeight);
        }
        
        public static Point RotatePoint(Point p, Point origin, double angleRad)
        {
            double s = Math.Sin(angleRad);
            double c = Math.Cos(angleRad);

            p = new Point(p.X - origin.X, p.Y - origin.Y);

            double xNew = p.X * c - p.Y * s;
            double yNew = p.X * s + p.Y * c;

            return new Point(xNew + origin.X, yNew + origin.Y);
        }

        public static bool IsPointNearSegment(Point p, Point s1, Point s2, double maxDistance)
        {
            double l2 = (s1.X - s2.X) * (s1.X - s2.X) + (s1.Y - s2.Y) * (s1.Y - s2.Y);
            if (l2 == 0) return PointDistance(p, s1) <= maxDistance;

            double t = Math.Max(0, Math.Min(1, ((p.X - s1.X) * (s2.X - s1.X) + (p.Y - s1.Y) * (s2.Y - s1.Y)) / l2));
            Point projection = new Point(s1.X + t * (s2.X - s1.X), s1.Y + t * (s2.Y - s1.Y));
            return PointDistance(p, projection) <= maxDistance;
        }

        public static double PointDistance(Point p1, Point p2)
        {
            return Math.Sqrt(Math.Pow(p1.X - p2.X, 2) + Math.Pow(p1.Y - p2.Y, 2));
        }
    }
}
