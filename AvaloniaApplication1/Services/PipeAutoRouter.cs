using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Avalonia;

namespace AvaloniaApplication1.Services
{
    public static class PipeAutoRouter
    {
        private const double BendPenalty = 15.0; // Штраф за поворот трубы на 90 градусов
        private const int MaxIterations = 6000;

        /// <summary>
        /// Вычисляет оптимальный ортогональный маршрут между начальной и конечной точками
        /// с обходом препятствий (виджетов) и минимальным числом изгибов (90°).
        /// </summary>
        public static List<Point> FindOrthogonalRoute(Point start, Point end, IEnumerable<Rect>? obstacles = null)
        {
            // Если точки совпадают, возвращаем отрезок нулевой длины
            if (Math.Abs(start.X - end.X) < 0.01 && Math.Abs(start.Y - end.Y) < 0.01)
            {
                return new List<Point> { start, end };
            }

            // Округляем до ближайших полуцелых/целых для работы на регулярной сетке
            double sx = Math.Round(start.X);
            double sy = Math.Round(start.Y);
            double ex = Math.Round(end.X);
            double ey = Math.Round(end.Y);

            // Список прямоугольников препятствий с безопасным зазором
            var obstacleList = obstacles?.Select(r => new Rect(
                Math.Floor(r.X), 
                Math.Floor(r.Y), 
                Math.Ceiling(r.Width), 
                Math.Ceiling(r.Height))).ToList() ?? new List<Rect>();

            // Если прямолинейный путь без препятствий, возвращаем простой L- или Z-образный маршрут
            var simpleRoute = TrySimpleRoute(start, end, obstacleList);
            if (simpleRoute != null && simpleRoute.Count >= 2)
            {
                return SimplifyCollinearPoints(simpleRoute);
            }

            // Иначе запускаем ортогональный A*
            var astarRoute = RunOrthogonalAStar(sx, sy, ex, ey, obstacleList);
            if (astarRoute != null && astarRoute.Count >= 2)
            {
                astarRoute[0] = start;
                astarRoute[astarRoute.Count - 1] = end;
                return SimplifyCollinearPoints(astarRoute);
            }

            // Fallback: безопасный манхэттенский Z-маршрут через середину
            var fallback = CreateZRoute(start, end);
            return SimplifyCollinearPoints(fallback);
        }

        private static List<Point>? TrySimpleRoute(Point start, Point end, List<Rect> obstacles)
        {
            // Вариант 1: Прямая горизонтальная или вертикальная линия
            if (Math.Abs(start.X - end.X) < 0.01 || Math.Abs(start.Y - end.Y) < 0.01)
            {
                if (!IntersectsAnyObstacle(start, end, obstacles))
                {
                    return new List<Point> { start, end };
                }
            }

            // Вариант 2: L-образный маршрут (сначала по X, потом по Y)
            var corner1 = new Point(end.X, start.Y);
            if (!IntersectsAnyObstacle(start, corner1, obstacles) && !IntersectsAnyObstacle(corner1, end, obstacles))
            {
                return new List<Point> { start, corner1, end };
            }

            // Вариант 3: L-образный маршрут (сначала по Y, потом по X)
            var corner2 = new Point(start.X, end.Y);
            if (!IntersectsAnyObstacle(start, corner2, obstacles) && !IntersectsAnyObstacle(corner2, end, obstacles))
            {
                return new List<Point> { start, corner2, end };
            }

            return null;
        }

        private static List<Point> CreateZRoute(Point start, Point end)
        {
            double midX = Math.Round((start.X + end.X) / 2.0);
            return new List<Point>
            {
                start,
                new Point(midX, start.Y),
                new Point(midX, end.Y),
                end
            };
        }

        private static bool IntersectsAnyObstacle(Point p1, Point p2, List<Rect> obstacles)
        {
            double minX = Math.Min(p1.X, p2.X);
            double maxX = Math.Max(p1.X, p2.X);
            double minY = Math.Min(p1.Y, p2.Y);
            double maxY = Math.Max(p1.Y, p2.Y);

            // Сегмент с толщиной
            var segmentRect = new Rect(minX + 0.1, minY + 0.1, Math.Max(0.1, maxX - minX - 0.2), Math.Max(0.1, maxY - minY - 0.2));

            foreach (var obs in obstacles)
            {
                if (obs.Intersects(segmentRect))
                    return true;
            }
            return false;
        }

        private static List<Point>? RunOrthogonalAStar(double sx, double sy, double ex, double ey, List<Rect> obstacles)
        {
            int startX = (int)sx;
            int startY = (int)sy;
            int endX = (int)ex;
            int endY = (int)ey;

            int minBoundsX = Math.Min(startX, endX) - 10;
            int maxBoundsX = Math.Max(startX, endX) + 10;
            int minBoundsY = Math.Min(startY, endY) - 10;
            int maxBoundsY = Math.Max(startY, endY) + 10;

            // Направления: 0: None, 1: Right, 2: Left, 3: Down, 4: Up
            var openSet = new PriorityQueue<Node, double>();
            var gScore = new Dictionary<(int x, int y, int dir), double>();

            var startNode = new Node(startX, startY, 0, null);
            gScore[(startX, startY, 0)] = 0;
            openSet.Enqueue(startNode, Manhattan(startX, startY, endX, endY));

            int iterations = 0;
            Node? bestEndNode = null;

            int[] dx = { 1, -1, 0, 0 };
            int[] dy = { 0, 0, 1, -1 };
            int[] dirs = { 1, 2, 3, 4 };

            while (openSet.Count > 0 && iterations++ < MaxIterations)
            {
                var current = openSet.Dequeue();

                if (current.X == endX && current.Y == endY)
                {
                    bestEndNode = current;
                    break;
                }

                for (int i = 0; i < 4; i++)
                {
                    int nx = current.X + dx[i];
                    int ny = current.Y + dy[i];
                    int ndir = dirs[i];

                    if (nx < minBoundsX || nx > maxBoundsX || ny < minBoundsY || ny > maxBoundsY)
                        continue;

                    // Проверка на препятствия (начало и конец игнорируем)
                    if (!(nx == endX && ny == endY) && !(nx == startX && ny == startY))
                    {
                        var cellRect = new Rect(nx + 0.1, ny + 0.1, 0.8, 0.8);
                        bool blocked = false;
                        for (int o = 0; o < obstacles.Count; o++)
                        {
                            if (obstacles[o].Intersects(cellRect))
                            {
                                blocked = true;
                                break;
                            }
                        }
                        if (blocked) continue;
                    }

                    double stepCost = 1.0;
                    if (current.Dir != 0 && current.Dir != ndir)
                    {
                        stepCost += BendPenalty;
                    }

                    double tentativeG = current.Cost + stepCost;
                    var stateKey = (nx, ny, ndir);

                    if (!gScore.TryGetValue(stateKey, out double existingG) || tentativeG < existingG)
                    {
                        gScore[stateKey] = tentativeG;
                        var nextNode = new Node(nx, ny, ndir, current) { Cost = tentativeG };
                        double f = tentativeG + Manhattan(nx, ny, endX, endY);
                        openSet.Enqueue(nextNode, f);
                    }
                }
            }

            if (bestEndNode == null)
                return null;

            var rawPoints = new List<Point>();
            var curr = bestEndNode;
            while (curr != null)
            {
                rawPoints.Add(new Point(curr.X, curr.Y));
                curr = curr.Parent;
            }
            rawPoints.Reverse();
            return rawPoints;
        }

        private static double Manhattan(int x1, int y1, int x2, int y2)
        {
            return Math.Abs(x1 - x2) + Math.Abs(y1 - y2);
        }

        /// <summary>
        /// Удаляет промежуточные коллинеарные точки, лежащие на одной прямой,
        /// оставляя только узлы перегиба под 90° и концы отрезков.
        /// </summary>
        public static List<Point> SimplifyCollinearPoints(List<Point> points)
        {
            if (points.Count <= 2) return new List<Point>(points);

            var result = new List<Point> { points[0] };

            for (int i = 1; i < points.Count - 1; i++)
            {
                var prev = result.Last();
                var curr = points[i];
                var next = points[i + 1];

                bool isCollinearH = Math.Abs(prev.Y - curr.Y) < 0.01 && Math.Abs(curr.Y - next.Y) < 0.01;
                bool isCollinearV = Math.Abs(prev.X - curr.X) < 0.01 && Math.Abs(curr.X - next.X) < 0.01;

                if (!isCollinearH && !isCollinearV)
                {
                    result.Add(curr);
                }
            }

            result.Add(points.Last());
            return result;
        }

        public static string PointsToPipeString(IEnumerable<Point> points)
        {
            return string.Join(" ", points.Select(p => 
                $"{p.X.ToString("F1", CultureInfo.InvariantCulture)},{p.Y.ToString("F1", CultureInfo.InvariantCulture)}"));
        }

        private sealed class Node
        {
            public int X { get; }
            public int Y { get; }
            public int Dir { get; }
            public double Cost { get; set; }
            public Node? Parent { get; }

            public Node(int x, int y, int dir, Node? parent)
            {
                X = x;
                Y = y;
                Dir = dir;
                Parent = parent;
            }
        }
    }
}
