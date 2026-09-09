using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Threading;

namespace AvaloniaApplication1.Views
{
    /// <summary>
    /// Промышленный кастомный контрол реактора / сосуда с мешалкой.
    /// Поддерживает рубашку обогрева/охлаждения, индикацию уровня жидкости,
    /// и плавную анимацию вращения импеллера мешалки в реальном времени.
    /// </summary>
    public class ReactorControl : Control
    {
        public static readonly StyledProperty<double> LevelProperty =
            AvaloniaProperty.Register<ReactorControl, double>(nameof(Level), 50.0);

        public static readonly StyledProperty<double> TemperatureProperty =
            AvaloniaProperty.Register<ReactorControl, double>(nameof(Temperature), 20.0);

        public static readonly StyledProperty<bool> IsAgitatorRunningProperty =
            AvaloniaProperty.Register<ReactorControl, bool>(nameof(IsAgitatorRunning), false);

        public static readonly StyledProperty<bool> HasJacketProperty =
            AvaloniaProperty.Register<ReactorControl, bool>(nameof(HasJacket), true);

        public static readonly StyledProperty<string> ActiveColorProperty =
            AvaloniaProperty.Register<ReactorControl, string>(nameof(ActiveColor), "#00FF00");

        public static readonly StyledProperty<string> InactiveColorProperty =
            AvaloniaProperty.Register<ReactorControl, string>(nameof(InactiveColor), "#555555");

        public double Level
        {
            get => GetValue(LevelProperty);
            set => SetValue(LevelProperty, value);
        }

        public double Temperature
        {
            get => GetValue(TemperatureProperty);
            set => SetValue(TemperatureProperty, value);
        }

        public bool IsAgitatorRunning
        {
            get => GetValue(IsAgitatorRunningProperty);
            set => SetValue(IsAgitatorRunningProperty, value);
        }

        public bool HasJacket
        {
            get => GetValue(HasJacketProperty);
            set => SetValue(HasJacketProperty, value);
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

        static ReactorControl()
        {
            AffectsRender<ReactorControl>(
                LevelProperty,
                TemperatureProperty,
                HasJacketProperty,
                ActiveColorProperty,
                InactiveColorProperty);
        }

        // --- Анимационный таймер вращения лопастей ---
        private readonly DispatcherTimer _animationTimer;
        private double _rotationAngle = 0.0;

        public ReactorControl()
        {
            _animationTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(33) // ~30 FPS
            };
            _animationTimer.Tick += (s, e) =>
            {
                _rotationAngle = (_rotationAngle + 12.0) % 360.0;
                InvalidateVisual();
            };

            this.GetObservable(IsAgitatorRunningProperty).Subscribe(isRunning =>
            {
                if (isRunning)
                {
                    if (!_animationTimer.IsEnabled) _animationTimer.Start();
                }
                else
                {
                    _animationTimer.Stop();
                    InvalidateVisual();
                }
            });
        }

        // --- Статически кэшированные перья и кисти (Zero-Allocation) ---
        private static readonly IBrush VesselBgBrush = new SolidColorBrush(Color.FromRgb(25, 27, 30));
        private static readonly IBrush LiquidBrush = new SolidColorBrush(Color.FromArgb(170, 0, 122, 204));
        private static readonly IBrush JacketBrush = new SolidColorBrush(Color.FromArgb(90, 255, 140, 0));
        private static readonly Pen VesselOutlinePen = new Pen(new SolidColorBrush(Color.FromRgb(100, 105, 115)), 2.0);
        private static readonly Pen JacketOutlinePen = new Pen(new SolidColorBrush(Color.FromRgb(200, 110, 0)), 1.5);
        private static readonly IBrush MotorBrush = new SolidColorBrush(Color.FromRgb(65, 70, 80));
        private static readonly Pen MotorPen = new Pen(new SolidColorBrush(Color.FromRgb(130, 135, 145)), 1.5);
        private static readonly Pen ShaftPen = new Pen(new SolidColorBrush(Color.FromRgb(200, 205, 215)), 2.5);
        private static readonly Pen ImpellerRunningPen = new Pen(new SolidColorBrush(Color.FromRgb(0, 255, 100)), 3.0);
        private static readonly Pen ImpellerStoppedPen = new Pen(new SolidColorBrush(Color.FromRgb(120, 125, 135)), 3.0);
        private static readonly IBrush FlangeBrush = new SolidColorBrush(Color.FromRgb(160, 165, 175));
        private static readonly Pen FlangeBorderPen = new Pen(Brushes.Black, 1.0);

        public override void Render(DrawingContext context)
        {
            base.Render(context);

            double w = Bounds.Width;
            double h = Bounds.Height;
            if (w < 30 || h < 40) return;

            double motorH = Math.Clamp(h * 0.15, 14.0, 28.0);
            double motorW = Math.Clamp(w * 0.28, 16.0, 36.0);
            double bottomNozzleH = Math.Clamp(h * 0.08, 6.0, 14.0);

            var bodyRect = new Rect(10, motorH, Math.Max(10, w - 20), Math.Max(10, h - motorH - bottomNozzleH));

            // 1. Рубашка обогрева/охлаждения (Jacket)
            if (HasJacket)
            {
                double jacketTop = bodyRect.Top + bodyRect.Height * 0.25;
                double jacketH = bodyRect.Height * 0.7;
                var jacketRect = new Rect(bodyRect.Left - 5, jacketTop, bodyRect.Width + 10, jacketH);
                context.DrawRectangle(JacketBrush, JacketOutlinePen, jacketRect, 8, 8);

                // Патрубки рубашки (слева внизу и справа вверху)
                context.DrawRectangle(MotorBrush, VesselOutlinePen, new Rect(bodyRect.Left - 10, jacketTop + 4, 6, 8));
                context.DrawRectangle(MotorBrush, VesselOutlinePen, new Rect(bodyRect.Right + 4, jacketTop + jacketH - 12, 6, 8));
            }

            // 2. Корпус реактора (Vessel) со скругленными днищами
            context.DrawRectangle(VesselBgBrush, VesselOutlinePen, bodyRect, 14, 14);

            // 3. Заполнение жидкостью (Level)
            double levelRatio = Math.Clamp(Level / 100.0, 0.0, 1.0);
            if (levelRatio > 0.02)
            {
                double liquidH = (bodyRect.Height - 10) * levelRatio;
                var liquidRect = new Rect(bodyRect.Left + 2, bodyRect.Bottom - liquidH - 2, bodyRect.Width - 4, liquidH);
                context.DrawRectangle(LiquidBrush, null, liquidRect, 10, 10);
            }

            // 4. Электропривод мешалки (Motor & Gearbox сверху)
            double cx = bodyRect.Center.X;
            var motorRect = new Rect(cx - motorW / 2, 0, motorW, motorH);
            context.DrawRectangle(MotorBrush, MotorPen, motorRect, 3, 3);

            // Шток / Вал мешалки (Shaft)
            double shaftBottom = bodyRect.Bottom - 12;
            context.DrawLine(ShaftPen, new Point(cx, motorRect.Bottom), new Point(cx, shaftBottom));

            // 5. Лопасти импеллера мешалки
            var impellerPen = IsAgitatorRunning ? ImpellerRunningPen : ImpellerStoppedPen;
            double bladeSpan = bodyRect.Width * 0.35;

            // Перспективное вращение импеллера
            double cos = Math.Cos(_rotationAngle * Math.PI / 180.0);
            double currentSpan = bladeSpan * cos;

            // Нижний ярус лопастей (турбинный)
            context.DrawLine(impellerPen, new Point(cx - currentSpan, shaftBottom - 4), new Point(cx + currentSpan, shaftBottom - 4));
            // Лопатки
            context.DrawLine(impellerPen, new Point(cx - currentSpan, shaftBottom - 10), new Point(cx - currentSpan, shaftBottom + 2));
            context.DrawLine(impellerPen, new Point(cx + currentSpan, shaftBottom - 10), new Point(cx + currentSpan, shaftBottom + 2));

            // Средний ярус лопастей
            double midShaftY = bodyRect.Top + bodyRect.Height * 0.55;
            double midSpan = currentSpan * 0.85;
            context.DrawLine(impellerPen, new Point(cx - midSpan, midShaftY), new Point(cx + midSpan, midShaftY));

            // 6. Верхний загрузочный и нижний сливной штуцеры с фланцами
            double nozzleW = 12.0;
            // Верхний штуцер (сбоку от мотора)
            double topNozzleX = bodyRect.Left + bodyRect.Width * 0.2;
            context.DrawRectangle(MotorBrush, VesselOutlinePen, new Rect(topNozzleX - nozzleW / 2, bodyRect.Top - 8, nozzleW, 8));
            context.DrawRectangle(FlangeBrush, FlangeBorderPen, new Rect(topNozzleX - 9, bodyRect.Top - 10, 18, 3));

            // Нижний сливной штуцер
            context.DrawRectangle(MotorBrush, VesselOutlinePen, new Rect(cx - nozzleW / 2, bodyRect.Bottom, nozzleW, bottomNozzleH));
            context.DrawRectangle(FlangeBrush, FlangeBorderPen, new Rect(cx - 9, Bounds.Height - 3, 18, 3));
        }
    }
}
