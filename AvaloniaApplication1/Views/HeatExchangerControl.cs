using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace AvaloniaApplication1.Views
{
    /// <summary>
    /// Промышленный кастомный контрол теплообменника (кожухотрубный или пластинчатый).
    /// Поддерживает 4 порта подключения контуров (горячий/холодный), индикацию температур и Zero-Allocation отрисовку.
    /// </summary>
    public class HeatExchangerControl : Control
    {
        public static readonly StyledProperty<string> ExchangerTypeProperty =
            AvaloniaProperty.Register<HeatExchangerControl, string>(nameof(ExchangerType), "ShellAndTube");

        public static readonly StyledProperty<double> PrimaryTempProperty =
            AvaloniaProperty.Register<HeatExchangerControl, double>(nameof(PrimaryTemp), 0.0);

        public static readonly StyledProperty<double> SecondaryTempProperty =
            AvaloniaProperty.Register<HeatExchangerControl, double>(nameof(SecondaryTemp), 0.0);

        public static readonly StyledProperty<string> ActiveColorProperty =
            AvaloniaProperty.Register<HeatExchangerControl, string>(nameof(ActiveColor), "#00FFCC");

        public static readonly StyledProperty<string> InactiveColorProperty =
            AvaloniaProperty.Register<HeatExchangerControl, string>(nameof(InactiveColor), "#777777");

        public static readonly StyledProperty<bool> ShowFlangesProperty =
            AvaloniaProperty.Register<HeatExchangerControl, bool>(nameof(ShowFlanges), true);

        public static readonly StyledProperty<double> ThicknessProperty =
            AvaloniaProperty.Register<HeatExchangerControl, double>(nameof(Thickness), 12.0);

        public string ExchangerType
        {
            get => GetValue(ExchangerTypeProperty);
            set => SetValue(ExchangerTypeProperty, value);
        }

        public double PrimaryTemp
        {
            get => GetValue(PrimaryTempProperty);
            set => SetValue(PrimaryTempProperty, value);
        }

        public double SecondaryTemp
        {
            get => GetValue(SecondaryTempProperty);
            set => SetValue(SecondaryTempProperty, value);
        }

        public string ActiveColor
        {
            get => GetValue(ActiveColorProperty);
            set => SetValue(ActiveColorProperty, value);
        }

        public string InactiveColor
        {
            get => GetValue(InactiveColorProperty);
            set => SetValue(InactiveColorProperty, value);
        }

        public bool ShowFlanges
        {
            get => GetValue(ShowFlangesProperty);
            set => SetValue(ShowFlangesProperty, value);
        }

        public double Thickness
        {
            get => GetValue(ThicknessProperty);
            set => SetValue(ThicknessProperty, value);
        }

        static HeatExchangerControl()
        {
            AffectsRender<HeatExchangerControl>(
                ExchangerTypeProperty, 
                PrimaryTempProperty, 
                SecondaryTempProperty, 
                ActiveColorProperty, 
                InactiveColorProperty, 
                ShowFlangesProperty, 
                ThicknessProperty);
        }

        // --- Статически кэшированные графические ресурсы (Zero-Allocation) ---
        private static readonly IBrush BodyBgBrush = new SolidColorBrush(Color.FromRgb(38, 40, 44));
        private static readonly IBrush PlateBgBrush = new SolidColorBrush(Color.FromRgb(30, 32, 36));
        private static readonly Pen OutlinePen = new Pen(new SolidColorBrush(Color.FromRgb(90, 95, 105)), 2.0);
        private static readonly Pen InnerTubesPen = new Pen(new SolidColorBrush(Color.FromRgb(0, 220, 200)), 1.5);
        private static readonly Pen SecondaryTubesPen = new Pen(new SolidColorBrush(Color.FromRgb(255, 140, 0)), 1.5);
        private static readonly Pen BafflePen = new Pen(new SolidColorBrush(Color.FromRgb(120, 125, 135)), 1.0, new DashStyle(new double[] { 3, 3 }, 0));
        private static readonly IBrush FlangeBrush = new SolidColorBrush(Color.FromRgb(160, 165, 175));
        private static readonly Pen FlangeBorderPen = new Pen(Brushes.Black, 1.0);
        private static readonly IBrush NozzleBrush = new SolidColorBrush(Color.FromRgb(60, 65, 75));

        public override void Render(DrawingContext context)
        {
            base.Render(context);

            double w = Bounds.Width;
            double h = Bounds.Height;
            if (w < 20 || h < 20) return;

            // Отступы для патрубков/фланцев сверху и снизу
            double nozzleLen = Math.Clamp(h * 0.18, 6.0, 18.0);
            double nozzleWidth = Math.Clamp(Thickness, 8.0, 20.0);

            var mainRect = new Rect(12, nozzleLen, Math.Max(10, w - 24), Math.Max(10, h - nozzleLen * 2));

            if (string.Equals(ExchangerType, "Plate", StringComparison.OrdinalIgnoreCase))
            {
                RenderPlateExchanger(context, mainRect, nozzleLen, nozzleWidth, w, h);
            }
            else
            {
                RenderShellAndTubeExchanger(context, mainRect, nozzleLen, nozzleWidth, w, h);
            }

            // 4 патрубка (nozzles) с фланцами
            RenderNozzlesAndFlanges(context, mainRect, nozzleLen, nozzleWidth);
        }

        private void RenderShellAndTubeExchanger(DrawingContext context, Rect r, double nozzleLen, double nozzleWidth, double totalW, double totalH)
        {
            // Корпус со скругленными крышками (эллиптические днища слева и справа)
            context.DrawRectangle(BodyBgBrush, OutlinePen, r, r.Height / 2.0, r.Height / 2.0);

            // Отрисовка внутренних трубок
            double cy = r.Center.Y;
            double tubeOffset = r.Height * 0.22;

            context.DrawLine(InnerTubesPen, new Point(r.Left + 8, cy - tubeOffset), new Point(r.Right - 8, cy - tubeOffset));
            context.DrawLine(InnerTubesPen, new Point(r.Left + 8, cy), new Point(r.Right - 8, cy));
            context.DrawLine(InnerTubesPen, new Point(r.Left + 8, cy + tubeOffset), new Point(r.Right - 8, cy + tubeOffset));

            // Перегородки (Baffles)
            double x1 = r.Left + r.Width * 0.3;
            double x2 = r.Left + r.Width * 0.7;
            context.DrawLine(BafflePen, new Point(x1, r.Top + 4), new Point(x1, r.Bottom - tubeOffset));
            context.DrawLine(BafflePen, new Point(x2, r.Top + tubeOffset), new Point(x2, r.Bottom - 4));
        }

        private void RenderPlateExchanger(DrawingContext context, Rect r, double nozzleLen, double nozzleWidth, double totalW, double totalH)
        {
            // Пакет пластин с шевронным рифлением
            context.DrawRectangle(PlateBgBrush, OutlinePen, r, 4, 4);

            // Шевроны пластин
            int plateCount = Math.Max(3, (int)(r.Width / 12));
            double step = r.Width / (plateCount + 1);

            for (int i = 1; i <= plateCount; i++)
            {
                double px = r.Left + i * step;
                var pen = (i % 2 == 0) ? InnerTubesPen : SecondaryTubesPen;
                context.DrawLine(pen, new Point(px - 4, r.Top + 6), new Point(px + 4, r.Center.Y));
                context.DrawLine(pen, new Point(px + 4, r.Center.Y), new Point(px - 4, r.Bottom - 6));
            }
        }

        private void RenderNozzlesAndFlanges(DrawingContext context, Rect r, double nozzleLen, double nozzleWidth)
        {
            double insetX = r.Width * 0.22;
            double xLeft = r.Left + insetX;
            double xRight = r.Right - insetX;

            // 1. Верхний левый патрубок (Hot In)
            var p1Top = new Point(xLeft, 0);
            context.DrawRectangle(NozzleBrush, OutlinePen, new Rect(xLeft - nozzleWidth / 2, 0, nozzleWidth, nozzleLen));

            // 2. Верхний правый патрубок (Cold Out)
            var p2Top = new Point(xRight, 0);
            context.DrawRectangle(NozzleBrush, OutlinePen, new Rect(xRight - nozzleWidth / 2, 0, nozzleWidth, nozzleLen));

            // 3. Нижний левый патрубок (Cold In)
            var p3Bottom = new Point(xLeft, r.Bottom);
            context.DrawRectangle(NozzleBrush, OutlinePen, new Rect(xLeft - nozzleWidth / 2, r.Bottom, nozzleWidth, nozzleLen));

            // 4. Нижний правый патрубок (Hot Out)
            var p4Bottom = new Point(xRight, r.Bottom);
            context.DrawRectangle(NozzleBrush, OutlinePen, new Rect(xRight - nozzleWidth / 2, r.Bottom, nozzleWidth, nozzleLen));

            // Фланцы на торцах патрубков
            if (ShowFlanges)
            {
                double flangeW = nozzleWidth * 1.5;
                double flangeH = 3.5;

                // Верхние фланцы
                context.DrawRectangle(FlangeBrush, FlangeBorderPen, new Rect(xLeft - flangeW / 2, 0, flangeW, flangeH));
                context.DrawRectangle(FlangeBrush, FlangeBorderPen, new Rect(xRight - flangeW / 2, 0, flangeW, flangeH));

                // Нижние фланцы
                context.DrawRectangle(FlangeBrush, FlangeBorderPen, new Rect(xLeft - flangeW / 2, Bounds.Height - flangeH, flangeW, flangeH));
                context.DrawRectangle(FlangeBrush, FlangeBorderPen, new Rect(xRight - flangeW / 2, Bounds.Height - flangeH, flangeW, flangeH));
            }
        }
    }
}
