using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using AvaloniaApplication1.Models.Config;
using AvaloniaApplication1.ViewModels.Core;
using AvaloniaApplication1.ViewModels.Widgets;

namespace AvaloniaApplication1.Services.Piping
{
    public static class CollinearAssemblyService
    {
        private const double Epsilon = 0.001;

        /// <summary>
        /// Проверяет, применимо ли действие "Собрать соосный узел" к текущему набору выделенных элементов.
        /// Требуется ровно 2 трубы и 1 клапан.
        /// </summary>
        public static bool CanAssemble(IEnumerable<WidgetViewModelBase> selectedWidgets)
        {
            if (selectedWidgets == null) return false;
            var list = selectedWidgets.ToList();
            return list.OfType<PipeWidgetViewModel>().Count() == 2 && list.OfType<ValveWidgetViewModel>().Count() == 1;
        }

        /// <summary>
        /// Выполняет соосную сборку двух труб и клапана:
        /// выравнивает элементы по оси опорной трубы, ликвидирует зазоры и устанавливает фланцевые фитинги.
        /// </summary>
        public static bool AssembleCollinear(IEnumerable<WidgetViewModelBase> selectedWidgets)
        {
            if (!CanAssemble(selectedWidgets)) return false;

            var list = selectedWidgets.ToList();
            var pipes = list.OfType<PipeWidgetViewModel>().ToList();
            var valve = list.OfType<ValveWidgetViewModel>().Single();

            var pipe1 = pipes[0];
            var pipe2 = pipes[1];

            var pts1 = pipe1.GetAbsoluteGridPoints();
            var pts2 = pipe2.GetAbsoluteGridPoints();

            if (pts1.Count < 2 || pts2.Count < 2) return false;

            // Вычисляем длины труб, чтобы опорной выбрать более длинную трубу
            double len1 = ComputeTotalLength(pts1);
            double len2 = ComputeTotalLength(pts2);

            var refPipe = len1 >= len2 ? pipe1 : pipe2;
            var otherPipe = len1 >= len2 ? pipe2 : pipe1;
            var refPoints = refPipe.GetAbsoluteGridPoints();

            // Определяем ориентацию опорной оси: горизонтальная или вертикальная
            double dxRef = Math.Abs(refPoints.Last().X - refPoints.First().X);
            double dyRef = Math.Abs(refPoints.Last().Y - refPoints.First().Y);
            bool isHorizontalAxis = dxRef >= dyRef;

            if (isHorizontalAxis)
            {
                AssembleHorizontal(pipe1, pipe2, valve, refPoints[0].Y);
            }
            else
            {
                AssembleVertical(pipe1, pipe2, valve, refPoints[0].X);
            }

            return true;
        }

        private static void AssembleHorizontal(
            PipeWidgetViewModel pipe1, 
            PipeWidgetViewModel pipe2, 
            ValveWidgetViewModel valve, 
            double yAxis)
        {
            // Настраиваем ориентацию и вертикальное положение клапана
            valve.IsVertical = false;
            valve.Rotation = 0;
            valve.Row = yAxis - (valve.SizeY - 1) / 2.0;
            valve.OriginalConfig.Position.Row = valve.Row;
            valve.TypedConfig.IsVertical = false;
            valve.TypedConfig.Rotation = 0;

            // Упорядочиваем трубы слева направо по среднему X
            var pts1 = pipe1.GetAbsoluteGridPoints();
            var pts2 = pipe2.GetAbsoluteGridPoints();

            double avgX1 = pts1.Average(p => p.X);
            double avgX2 = pts2.Average(p => p.X);

            var leftPipe = avgX1 <= avgX2 ? pipe1 : pipe2;
            var rightPipe = avgX1 <= avgX2 ? pipe2 : pipe1;

            var leftPts = leftPipe.GetAbsoluteGridPoints();
            if (leftPts.First().X > leftPts.Last().X) leftPts.Reverse();

            // Выравниваем левую трубу по оси yAxis
            double leftLen = Math.Max(2.0, leftPts.Last().X - leftPts.First().X);
            double leftStartX = leftPts.First().X;
            double leftEndX = leftStartX + leftLen;

            var newLeftPoints = new List<Point>
            {
                new Point(leftStartX, yAxis),
                new Point(leftEndX, yAxis)
            };
            leftPipe.EndFitting = "Flange";
            leftPipe.ShowFlanges = true;
            UpdatePipePoints(leftPipe, newLeftPoints);

            // Позиционируем клапан встык к левой трубе
            // Левый порт клапана находится в Col - 0.5. Мы хотим, чтобы он совпадал с leftEndX.
            // leftEndX = valve.Col - 0.5 => valve.Col = leftEndX + 0.5
            valve.Col = leftEndX + 0.5;
            valve.OriginalConfig.Position.Col = valve.Col;

            // Правый порт клапана находится в: Col + SizeX - 0.5 = leftEndX + valve.SizeX
            double rightStartX = leftEndX + valve.SizeX;

            // Позиционируем правую трубу встык к правому порту клапана
            var rightPts = rightPipe.GetAbsoluteGridPoints();
            double rightLen = Math.Max(2.0, Math.Abs(rightPts.Last().X - rightPts.First().X));
            double rightEndX = rightStartX + rightLen;

            var newRightPoints = new List<Point>
            {
                new Point(rightStartX, yAxis),
                new Point(rightEndX, yAxis)
            };
            rightPipe.StartFitting = "Flange";
            rightPipe.ShowFlanges = true;
            UpdatePipePoints(rightPipe, newRightPoints);
        }

        private static void AssembleVertical(
            PipeWidgetViewModel pipe1, 
            PipeWidgetViewModel pipe2, 
            ValveWidgetViewModel valve, 
            double xAxis)
        {
            // Настраиваем ориентацию и горизонтальное положение клапана
            valve.IsVertical = true;
            valve.Rotation = 90;
            valve.Col = xAxis - (valve.SizeX - 1) / 2.0;
            valve.OriginalConfig.Position.Col = valve.Col;
            valve.TypedConfig.IsVertical = true;
            valve.TypedConfig.Rotation = 90;

            // Упорядочиваем трубы сверху вниз по среднему Y
            var pts1 = pipe1.GetAbsoluteGridPoints();
            var pts2 = pipe2.GetAbsoluteGridPoints();

            double avgY1 = pts1.Average(p => p.Y);
            double avgY2 = pts2.Average(p => p.Y);

            var topPipe = avgY1 <= avgY2 ? pipe1 : pipe2;
            var bottomPipe = avgY1 <= avgY2 ? pipe2 : pipe1;

            var topPts = topPipe.GetAbsoluteGridPoints();
            if (topPts.First().Y > topPts.Last().Y) topPts.Reverse();

            // Выравниваем верхнюю трубу по оси xAxis
            double topLen = Math.Max(2.0, topPts.Last().Y - topPts.First().Y);
            double topStartY = topPts.First().Y;
            double topEndY = topStartY + topLen;

            var newTopPoints = new List<Point>
            {
                new Point(xAxis, topStartY),
                new Point(xAxis, topEndY)
            };
            topPipe.EndFitting = "Flange";
            topPipe.ShowFlanges = true;
            UpdatePipePoints(topPipe, newTopPoints);

            // Позиционируем клапан встык к верхней трубе
            // Верхний порт клапана находится в Row - 0.5. Совмещаем с topEndY.
            // topEndY = valve.Row - 0.5 => valve.Row = topEndY + 0.5
            valve.Row = topEndY + 0.5;
            valve.OriginalConfig.Position.Row = valve.Row;

            // Нижний порт клапана находится в: Row + SizeY - 0.5 = topEndY + valve.SizeY
            double bottomStartY = topEndY + valve.SizeY;

            // Позиционируем нижнюю трубу встык к нижнему порту клапана
            var bottomPts = bottomPipe.GetAbsoluteGridPoints();
            double bottomLen = Math.Max(2.0, Math.Abs(bottomPts.Last().Y - bottomPts.First().Y));
            double bottomEndY = bottomStartY + bottomLen;

            var newBottomPoints = new List<Point>
            {
                new Point(xAxis, bottomStartY),
                new Point(xAxis, bottomEndY)
            };
            bottomPipe.StartFitting = "Flange";
            bottomPipe.ShowFlanges = true;
            UpdatePipePoints(bottomPipe, newBottomPoints);
        }

        /// <summary>
        /// Создает 2 новые трубы, пристыкованные к фланцам клапана.
        /// </summary>
        public static (PipeConfig Pipe1, PipeConfig Pipe2) CreatePipesForValveFlanges(
            ValveWidgetViewModel valveVm, 
            double pipeLength = 8.0, 
            double pipeThickness = 12.0)
        {
            bool isVert = valveVm.IsVertical || valveVm.Rotation == 90 || valveVm.Rotation == 270;

            if (!isVert)
            {
                // Горизонтальный клапан
                double y = valveVm.Row + (valveVm.SizeY - 1) / 2.0;
                double xLeftFlange = valveVm.Col - 0.5;
                double xRightFlange = valveVm.Col + valveVm.SizeX - 0.5;

                var leftPoints = new List<Point>
                {
                    new Point(Math.Max(0, xLeftFlange - pipeLength), y),
                    new Point(xLeftFlange, y)
                };

                var rightPoints = new List<Point>
                {
                    new Point(xRightFlange, y),
                    new Point(xRightFlange + pipeLength, y)
                };

                var pipe1 = new PipeConfig
                {
                    Type = "Pipe",
                    Title = $"{valveVm.Title} (Вход)",
                    Thickness = pipeThickness,
                    StartFitting = "None",
                    EndFitting = "Flange",
                    ShowFlanges = true,
                    ActiveColor = "#00FFCC",
                    InactiveColor = "#777777"
                };
                PipeAutoSplitter.SetupPipePointsAndBounds(pipe1, leftPoints);

                var pipe2 = new PipeConfig
                {
                    Type = "Pipe",
                    Title = $"{valveVm.Title} (Выход)",
                    Thickness = pipeThickness,
                    StartFitting = "Flange",
                    EndFitting = "None",
                    ShowFlanges = true,
                    ActiveColor = "#00FFCC",
                    InactiveColor = "#777777"
                };
                PipeAutoSplitter.SetupPipePointsAndBounds(pipe2, rightPoints);

                return (pipe1, pipe2);
            }
            else
            {
                // Вертикальный клапан
                double x = valveVm.Col + (valveVm.SizeX - 1) / 2.0;
                double yTopFlange = valveVm.Row - 0.5;
                double yBottomFlange = valveVm.Row + valveVm.SizeY - 0.5;

                var topPoints = new List<Point>
                {
                    new Point(x, Math.Max(0, yTopFlange - pipeLength)),
                    new Point(x, yTopFlange)
                };

                var bottomPoints = new List<Point>
                {
                    new Point(x, yBottomFlange),
                    new Point(x, yBottomFlange + pipeLength)
                };

                var pipe1 = new PipeConfig
                {
                    Type = "Pipe",
                    Title = $"{valveVm.Title} (Верх)",
                    Thickness = pipeThickness,
                    StartFitting = "None",
                    EndFitting = "Flange",
                    ShowFlanges = true,
                    ActiveColor = "#00FFCC",
                    InactiveColor = "#777777"
                };
                PipeAutoSplitter.SetupPipePointsAndBounds(pipe1, topPoints);

                var pipe2 = new PipeConfig
                {
                    Type = "Pipe",
                    Title = $"{valveVm.Title} (Низ)",
                    Thickness = pipeThickness,
                    StartFitting = "Flange",
                    EndFitting = "None",
                    ShowFlanges = true,
                    ActiveColor = "#00FFCC",
                    InactiveColor = "#777777"
                };
                PipeAutoSplitter.SetupPipePointsAndBounds(pipe2, bottomPoints);

                return (pipe1, pipe2);
            }
        }

        private static void UpdatePipePoints(PipeWidgetViewModel pipeVm, List<Point> absPoints)
        {
            var dummyConfig = new PipeConfig();
            PipeAutoSplitter.SetupPipePointsAndBounds(dummyConfig, absPoints);

            pipeVm.Col = dummyConfig.Position.Col;
            pipeVm.Row = dummyConfig.Position.Row;
            pipeVm.SizeX = dummyConfig.Position.SizeX;
            pipeVm.SizeY = dummyConfig.Position.SizeY;
            pipeVm.PipePoints = dummyConfig.PipePoints;

            pipeVm.OriginalConfig.Position.Col = pipeVm.Col;
            pipeVm.OriginalConfig.Position.Row = pipeVm.Row;
            pipeVm.OriginalConfig.Position.SizeX = pipeVm.SizeX;
            pipeVm.OriginalConfig.Position.SizeY = pipeVm.SizeY;
            pipeVm.TypedConfig.PipePoints = pipeVm.PipePoints;
        }

        private static double ComputeTotalLength(List<Point> points)
        {
            double total = 0;
            for (int i = 0; i < points.Count - 1; i++)
            {
                double dx = points[i + 1].X - points[i].X;
                double dy = points[i + 1].Y - points[i].Y;
                total += Math.Sqrt(dx * dx + dy * dy);
            }
            return total;
        }
    }
}
