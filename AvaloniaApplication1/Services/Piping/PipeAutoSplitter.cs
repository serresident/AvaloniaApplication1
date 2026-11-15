using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Avalonia;
using AvaloniaApplication1.Models.Config;
using AvaloniaApplication1.ViewModels.Widgets;

namespace AvaloniaApplication1.Services.Piping
{
    public class PipeSplitCandidate
    {
        public PipeWidgetViewModel PipeVm { get; set; } = null!;
        public int SegmentIndex { get; set; }
        public Point SegmentStart { get; set; }
        public Point SegmentEnd { get; set; }
        public bool IsHorizontal { get; set; }
        public Point ValveCenterOnAxis { get; set; }
        public Point ValveInputFlange { get; set; }
        public Point ValveOutputFlange { get; set; }
        public Point ValveSnappedColRow { get; set; }
        public int ValveRotation { get; set; }
        public bool ValveIsVertical { get; set; }
        public double DistanceToCenter { get; set; }
    }

    public class PipeSplitResult
    {
        public bool Success { get; set; }
        public PipeConfig PipeA { get; set; } = null!;
        public PipeConfig PipeB { get; set; } = null!;
        public Point ValveSnappedColRow { get; set; }
        public int ValveRotation { get; set; }
        public bool ValveIsVertical { get; set; }
        public PipeWidgetViewModel OriginalPipeVm { get; set; } = null!;
    }

    public static class PipeAutoSplitter
    {
        private const double Epsilon = 0.001;

        /// <summary>
        /// Проверяет, можно ли врезать клапан в данный сегмент трубы, и вычисляет координаты фланцев и положение клапана.
        /// </summary>
        public static PipeSplitCandidate? EvaluateSegmentCandidate(
            PipeWidgetViewModel pipeVm,
            int segmentIndex,
            Point pStart,
            Point pEnd,
            ValveWidgetViewModel valveVm,
            Point dropGridCenter,
            double snapTolerance = 1.5)
        {
            bool isHorizontal = Math.Abs(pStart.Y - pEnd.Y) < Epsilon;
            bool isVertical = Math.Abs(pStart.X - pEnd.X) < Epsilon;

            // Врезка допускается только в строго ортогональные прямолинейные сегменты
            if (!isHorizontal && !isVertical) return null;

            double valveLength = Math.Max(2.0, isHorizontal ? valveVm.SizeX : valveVm.SizeY);

            if (isHorizontal)
            {
                double segY = pStart.Y;
                double minX = Math.Min(pStart.X, pEnd.X);
                double maxX = Math.Max(pStart.X, pEnd.X);
                double segLen = maxX - minX;

                // Длина сегмента должна быть не меньше монтажной длины клапана
                if (segLen < valveLength) return null;

                // Проверка расстояния от центра клапана до оси сегмента
                double distY = Math.Abs(dropGridCenter.Y - segY);
                if (distY > snapTolerance) return null;

                // Проверка, что центр клапана проецируется на сегмент с учетом допуска
                if (dropGridCenter.X < minX - snapTolerance || dropGridCenter.X > maxX + snapTolerance) return null;

                // Ограничиваем центр клапана, чтобы фланцы не вылезали за пределы сегмента
                double minCenterX = minX + valveLength / 2.0;
                double maxCenterX = maxX - valveLength / 2.0;
                double clampedCenterX = Math.Clamp(dropGridCenter.X, minCenterX, maxCenterX);

                // Округление до сетки (с шагом 0.5 для соосности портов)
                clampedCenterX = Math.Round(clampedCenterX * 2.0) / 2.0;
                clampedCenterX = Math.Clamp(clampedCenterX, minCenterX, maxCenterX);

                double flangeInX = clampedCenterX - valveLength / 2.0;
                double flangeOutX = clampedCenterX + valveLength / 2.0;

                // Позиция клапана (Col, Row)
                // Для горизонтального клапана левый порт в локальных координатах = -0.5
                // port.X = Col - 0.5 => Col = port.X + 0.5
                double valveCol = flangeInX + 0.5;
                // CenterY = Row + (SizeY - 1) / 2.0 => Row = CenterY - (SizeY - 1) / 2.0
                double valveRow = segY - (valveVm.SizeY - 1) / 2.0;

                Point valveCenter = new Point(clampedCenterX, segY);
                Point flangeIn = new Point(flangeInX, segY);
                Point flangeOut = new Point(flangeOutX, segY);

                double dist = Math.Sqrt(Math.Pow(dropGridCenter.X - clampedCenterX, 2) + Math.Pow(dropGridCenter.Y - segY, 2));

                return new PipeSplitCandidate
                {
                    PipeVm = pipeVm,
                    SegmentIndex = segmentIndex,
                    SegmentStart = pStart,
                    SegmentEnd = pEnd,
                    IsHorizontal = true,
                    ValveCenterOnAxis = valveCenter,
                    ValveInputFlange = flangeIn,
                    ValveOutputFlange = flangeOut,
                    ValveSnappedColRow = new Point(valveCol, valveRow),
                    ValveRotation = 0,
                    ValveIsVertical = false,
                    DistanceToCenter = dist
                };
            }
            else // isVertical
            {
                double segX = pStart.X;
                double minY = Math.Min(pStart.Y, pEnd.Y);
                double maxY = Math.Max(pStart.Y, pEnd.Y);
                double segLen = maxY - minY;

                if (segLen < valveLength) return null;

                double distX = Math.Abs(dropGridCenter.X - segX);
                if (distX > snapTolerance) return null;

                if (dropGridCenter.Y < minY - snapTolerance || dropGridCenter.Y > maxY + snapTolerance) return null;

                double minCenterY = minY + valveLength / 2.0;
                double maxCenterY = maxY - valveLength / 2.0;
                double clampedCenterY = Math.Clamp(dropGridCenter.Y, minCenterY, maxCenterY);

                clampedCenterY = Math.Round(clampedCenterY * 2.0) / 2.0;
                clampedCenterY = Math.Clamp(clampedCenterY, minCenterY, maxCenterY);

                double flangeTopY = clampedCenterY - valveLength / 2.0;
                double flangeBottomY = clampedCenterY + valveLength / 2.0;

                // Для вертикального клапана верхний порт = -0.5
                // port.Y = Row - 0.5 => Row = port.Y + 0.5
                double valveRow = flangeTopY + 0.5;
                // CenterX = Col + (SizeX - 1) / 2.0 => Col = CenterX - (SizeX - 1) / 2.0
                double valveCol = segX - (valveVm.SizeX - 1) / 2.0;

                Point valveCenter = new Point(segX, clampedCenterY);
                Point flangeTop = new Point(segX, flangeTopY);
                Point flangeBottom = new Point(segX, flangeBottomY);

                double dist = Math.Sqrt(Math.Pow(dropGridCenter.X - segX, 2) + Math.Pow(dropGridCenter.Y - clampedCenterY, 2));

                return new PipeSplitCandidate
                {
                    PipeVm = pipeVm,
                    SegmentIndex = segmentIndex,
                    SegmentStart = pStart,
                    SegmentEnd = pEnd,
                    IsHorizontal = false,
                    ValveCenterOnAxis = valveCenter,
                    ValveInputFlange = flangeTop,
                    ValveOutputFlange = flangeBottom,
                    ValveSnappedColRow = new Point(valveCol, valveRow),
                    ValveRotation = 90,
                    ValveIsVertical = true,
                    DistanceToCenter = dist
                };
            }
        }

        /// <summary>
        /// Находит наилучшего кандидата для врезки среди всех сегментов всех переданных труб.
        /// </summary>
        public static PipeSplitCandidate? FindSplitCandidate(
            IEnumerable<PipeWidgetViewModel> pipes,
            ValveWidgetViewModel valveVm,
            Point dropGridCenter,
            double snapTolerance = 1.5)
        {
            PipeSplitCandidate? bestCandidate = null;
            double bestDist = double.MaxValue;

            foreach (var pipe in pipes)
            {
                var absPoints = pipe.GetAbsoluteGridPoints();
                if (absPoints.Count < 2) continue;

                for (int i = 0; i < absPoints.Count - 1; i++)
                {
                    var cand = EvaluateSegmentCandidate(
                        pipe, i, absPoints[i], absPoints[i + 1], valveVm, dropGridCenter, snapTolerance);

                    if (cand != null && cand.DistanceToCenter < bestDist)
                    {
                        bestDist = cand.DistanceToCenter;
                        bestCandidate = cand;
                    }
                }
            }

            return bestCandidate;
        }

        /// <summary>
        /// Выполняет авто-разрезание трубы на основе найденного кандидата.
        /// Возвращает две новые конфигурации труб с фланцами и точным клонированием параметров.
        /// </summary>
        public static PipeSplitResult SplitPipe(PipeSplitCandidate candidate)
        {
            var originalPipe = candidate.PipeVm;
            var absPoints = originalPipe.GetAbsoluteGridPoints();

            int segIdx = candidate.SegmentIndex;
            Point pStart = absPoints[segIdx];
            Point pEnd = absPoints[segIdx + 1];

            // Определяем, какой фланец ближе к pStart, а какой к pEnd
            Point cutPortA, cutPortB;
            double dStartToFlange1 = DistanceSquared(pStart, candidate.ValveInputFlange);
            double dStartToFlange2 = DistanceSquared(pStart, candidate.ValveOutputFlange);

            if (dStartToFlange1 <= dStartToFlange2)
            {
                cutPortA = candidate.ValveInputFlange;
                cutPortB = candidate.ValveOutputFlange;
            }
            else
            {
                cutPortA = candidate.ValveOutputFlange;
                cutPortB = candidate.ValveInputFlange;
            }

            // Формируем точки для Pipe A: от начала до cutPortA
            var pointsA = new List<Point>();
            for (int i = 0; i <= segIdx; i++)
            {
                pointsA.Add(absPoints[i]);
            }
            if (DistanceSquared(pointsA.Last(), cutPortA) > Epsilon)
            {
                pointsA.Add(cutPortA);
            }

            // Формируем точки для Pipe B: от cutPortB до конца
            var pointsB = new List<Point>();
            pointsB.Add(cutPortB);
            for (int i = segIdx + 1; i < absPoints.Count; i++)
            {
                if (i == segIdx + 1 && DistanceSquared(cutPortB, absPoints[i]) < Epsilon)
                {
                    continue;
                }
                pointsB.Add(absPoints[i]);
            }

            // Создаем PipeConfig A
            var pipeAConfig = ClonePipeConfig(originalPipe.TypedConfig);
            pipeAConfig.Title = string.IsNullOrEmpty(originalPipe.Title) ? "Трубопровод A" : $"{originalPipe.Title} (1)";
            pipeAConfig.EndFitting = "Flange";
            pipeAConfig.ShowFlanges = true;
            SetupPipePointsAndBounds(pipeAConfig, pointsA);

            // Создаем PipeConfig B
            var pipeBConfig = ClonePipeConfig(originalPipe.TypedConfig);
            pipeBConfig.Title = string.IsNullOrEmpty(originalPipe.Title) ? "Трубопровод B" : $"{originalPipe.Title} (2)";
            pipeBConfig.StartFitting = "Flange";
            pipeBConfig.ShowFlanges = true;
            SetupPipePointsAndBounds(pipeBConfig, pointsB);

            return new PipeSplitResult
            {
                Success = true,
                PipeA = pipeAConfig,
                PipeB = pipeBConfig,
                ValveSnappedColRow = candidate.ValveSnappedColRow,
                ValveRotation = candidate.ValveRotation,
                ValveIsVertical = candidate.ValveIsVertical,
                OriginalPipeVm = originalPipe
            };
        }

        /// <summary>
        /// Попытка найти подходящую трубу и сразу рассечь её.
        /// </summary>
        public static PipeSplitResult? TryFindAndSplitPipe(
            IEnumerable<PipeWidgetViewModel> pipes,
            ValveWidgetViewModel valveVm,
            Point dropGridCenter,
            double snapTolerance = 1.5)
        {
            var candidate = FindSplitCandidate(pipes, valveVm, dropGridCenter, snapTolerance);
            if (candidate == null) return null;

            return SplitPipe(candidate);
        }

        /// <summary>
        /// Настраивает Col, Row, SizeX, SizeY и относительные PipePoints по набору абсолютных точек.
        /// </summary>
        public static void SetupPipePointsAndBounds(PipeConfig pipeCfg, List<Point> absPoints)
        {
            if (absPoints.Count < 2) return;

            double minX = absPoints.Min(p => p.X);
            double maxX = absPoints.Max(p => p.X);
            double minY = absPoints.Min(p => p.Y);
            double maxY = absPoints.Max(p => p.Y);

            int col = (int)Math.Floor(minX);
            int row = (int)Math.Floor(minY);
            int sizeX = Math.Max(1, (int)Math.Ceiling(maxX - col));
            int sizeY = Math.Max(1, (int)Math.Ceiling(maxY - row));

            var relPoints = absPoints.Select(p => new Point(p.X - col, p.Y - row)).ToList();
            string pointsStr = string.Join(";", relPoints.Select(p => 
                string.Format(CultureInfo.InvariantCulture, "{0:0.##},{1:0.##}", p.X, p.Y)));

            pipeCfg.PipePoints = pointsStr;
            pipeCfg.Position = new WidgetPosition
            {
                Col = col,
                Row = row,
                SizeX = sizeX,
                SizeY = sizeY
            };
        }

        private static PipeConfig ClonePipeConfig(PipeConfig source)
        {
            return new PipeConfig
            {
                Type = "Pipe",
                Title = source.Title,
                Thickness = source.Thickness,
                ActiveColor = source.ActiveColor,
                InactiveColor = source.InactiveColor,
                ShowFlanges = source.ShowFlanges,
                StartFitting = source.StartFitting,
                EndFitting = source.EndFitting,
                Source = new DataSourceConfig
                {
                    TagId = source.Source?.TagId,
                    ConnId = source.Source?.ConnId ?? string.Empty,
                    Address = source.Source?.Address ?? string.Empty,
                    DataType = source.Source?.DataType ?? string.Empty
                },
                Position = new WidgetPosition
                {
                    Col = source.Position.Col,
                    Row = source.Position.Row,
                    SizeX = source.Position.SizeX,
                    SizeY = source.Position.SizeY
                }
            };
        }

        private static double DistanceSquared(Point p1, Point p2)
        {
            double dx = p1.X - p2.X;
            double dy = p1.Y - p2.Y;
            return dx * dx + dy * dy;
        }
    }
}
