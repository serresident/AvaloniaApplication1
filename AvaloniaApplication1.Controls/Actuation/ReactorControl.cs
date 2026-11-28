using System;
using System.Globalization;
using Avalonia;
using Avalonia.Media;
using Avalonia.Threading;
using AvaloniaApplication1.Controls.Primitives;

namespace AvaloniaApplication1.Controls.Actuation
{
    /// <summary>
    /// Промышленный кастомный контрол реактора / сосуда с мешалкой.
    /// Поддерживает 3 темы (Dark, Light, IndustrialGray ISA-101), рубашку обогрева/охлаждения,
    /// нормализацию уровня (MinValue..MaxValue), подавление установившихся анимаций в ISA-101,
    /// а также бейджи [M] (ручной режим) и [!] (авария/отказ).
    /// </summary>
    public class ReactorControl : ScadaControlBase
    {
        public static readonly StyledProperty<EquipmentOperationalState> OperationalStateProperty =
            AvaloniaProperty.Register<ReactorControl, EquipmentOperationalState>(nameof(OperationalState), EquipmentOperationalState.Stopped);

        public static readonly StyledProperty<double> LevelProperty =
            AvaloniaProperty.Register<ReactorControl, double>(nameof(Level), 50.0);

        public static readonly StyledProperty<double> MinValueProperty =
            AvaloniaProperty.Register<ReactorControl, double>(nameof(MinValue), 0.0);

        public static readonly StyledProperty<double> MaxValueProperty =
            AvaloniaProperty.Register<ReactorControl, double>(nameof(MaxValue), 100.0);

        public static readonly StyledProperty<double> TemperatureProperty =
            AvaloniaProperty.Register<ReactorControl, double>(nameof(Temperature), 20.0);

        public static readonly StyledProperty<bool> IsAgitatorRunningProperty =
            AvaloniaProperty.Register<ReactorControl, bool>(nameof(IsAgitatorRunning), false);

        public static readonly StyledProperty<bool> HasJacketProperty =
            AvaloniaProperty.Register<ReactorControl, bool>(nameof(HasJacket), true);

        public static readonly StyledProperty<bool> IsManualModeProperty =
            AvaloniaProperty.Register<ReactorControl, bool>(nameof(IsManualMode), false);

        public static readonly StyledProperty<bool> IsFaultProperty =
            AvaloniaProperty.Register<ReactorControl, bool>(nameof(IsFault), false);

        public static readonly StyledProperty<bool> HasTempAlarmProperty =
            AvaloniaProperty.Register<ReactorControl, bool>(nameof(HasTempAlarm), false);

        public static readonly StyledProperty<bool> HasLevelAlarmProperty =
            AvaloniaProperty.Register<ReactorControl, bool>(nameof(HasLevelAlarm), false);

        public static readonly StyledProperty<string> ActiveColorProperty =
            AvaloniaProperty.Register<ReactorControl, string>(nameof(ActiveColor), string.Empty);

        public static readonly StyledProperty<string> InactiveColorProperty =
            AvaloniaProperty.Register<ReactorControl, string>(nameof(InactiveColor), string.Empty);

        public EquipmentOperationalState OperationalState
        {
            get => GetValue(OperationalStateProperty);
            set => SetValue(OperationalStateProperty, value);
        }

        public double Level
        {
            get => GetValue(LevelProperty);
            set => SetValue(LevelProperty, value);
        }

        public double MinValue
        {
            get => GetValue(MinValueProperty);
            set => SetValue(MinValueProperty, value);
        }

        public double MaxValue
        {
            get => GetValue(MaxValueProperty);
            set => SetValue(MaxValueProperty, value);
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

        public bool IsManualMode
        {
            get => GetValue(IsManualModeProperty);
            set => SetValue(IsManualModeProperty, value);
        }

        public bool IsFault
        {
            get => GetValue(IsFaultProperty);
            set => SetValue(IsFaultProperty, value);
        }

        public bool HasTempAlarm
        {
            get => GetValue(HasTempAlarmProperty);
            set => SetValue(HasTempAlarmProperty, value);
        }

        public bool HasLevelAlarm
        {
            get => GetValue(HasLevelAlarmProperty);
            set => SetValue(HasLevelAlarmProperty, value);
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
                OperationalStateProperty,
                LevelProperty,
                MinValueProperty,
                MaxValueProperty,
                TemperatureProperty,
                HasJacketProperty,
                IsManualModeProperty,
                IsFaultProperty,
                HasTempAlarmProperty,
                HasLevelAlarmProperty,
                ActiveColorProperty,
                InactiveColorProperty);

            IsAgitatorRunningProperty.Changed.AddClassHandler<ReactorControl>((x, _) => x.SyncAnimationTimer());
        }

        // --- Анимационный таймер вращения лопастей ---
        private readonly DispatcherTimer _animationTimer;
        private double _rotationAngle = 0.0;

        // --- Кэш перьев/кистей (сбрасывается при смене темы/цвета) ---
        private IBrush? _cachedImpellerBrush;
        private Pen? _cachedImpellerPen;
        private FormattedText? _cachedManualText;
        private FormattedText? _cachedFaultText;

        public ReactorControl()
        {
            _animationTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(33) // ~30 FPS
            };
            _animationTimer.Tick += (_, _) =>
            {
                _rotationAngle = (_rotationAngle + 12.0) % 360.0;
                InvalidateVisual();
            };
        }

        private void SyncAnimationTimer()
        {
            if (IsAgitatorRunning && !IsSteadyAnimationSuppressed)
            {
                if (!_animationTimer.IsEnabled) _animationTimer.Start();
            }
            else
            {
                if (_animationTimer.IsEnabled) _animationTimer.Stop();
                if (IsSteadyAnimationSuppressed) _rotationAngle = 0.0;
                InvalidateVisual();
            }
        }

        protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
        {
            base.OnAttachedToVisualTree(e);
            SyncAnimationTimer();
        }

        protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
        {
            base.OnDetachedFromVisualTree(e);
            _animationTimer.Stop();
        }

        protected override void InvalidateCachedBrushes()
        {
            _cachedImpellerBrush = null;
            _cachedImpellerPen = null;
        }

        protected override void OnThemeChanged()
        {
            SyncAnimationTimer();
        }

        // --- Статически кэшированные перья и кисти (Zero-Allocation) ---
        private static readonly IBrush VesselBgBrushDark = new SolidColorBrush(Color.FromRgb(25, 27, 30));
        private static readonly IBrush VesselBgBrushLight = new SolidColorBrush(Color.FromRgb(235, 238, 242));
        private static readonly IBrush VesselBgBrushGray = new SolidColorBrush(Color.FromRgb(203, 213, 225));
        private static readonly IBrush LiquidBrushColor = new SolidColorBrush(Color.FromArgb(170, 0, 122, 204));
        private static readonly IBrush LiquidBrushGray = new SolidColorBrush(Color.FromArgb(180, 100, 116, 139));
        private static readonly IBrush LiquidBrushAlarm = new SolidColorBrush(Color.FromArgb(190, 220, 38, 38));
        private static readonly IBrush JacketBrushColor = new SolidColorBrush(Color.FromArgb(90, 255, 140, 0));
        private static readonly IBrush JacketBrushGray = new SolidColorBrush(Color.FromArgb(80, 148, 163, 184));
        private static readonly IBrush JacketBrushAlarm = new SolidColorBrush(Color.FromArgb(130, 220, 38, 38));
        private static readonly Pen VesselOutlinePenDark = new Pen(new SolidColorBrush(Color.FromRgb(100, 105, 115)), 2.0);
        private static readonly Pen VesselOutlinePenLight = new Pen(new SolidColorBrush(Color.FromRgb(50, 55, 65)), 2.0);
        private static readonly Pen VesselOutlinePenGray = new Pen(new SolidColorBrush(Color.FromRgb(71, 85, 105)), 2.0);
        private static readonly Pen JacketOutlinePenColor = new Pen(new SolidColorBrush(Color.FromRgb(200, 110, 0)), 1.5);
        private static readonly Pen JacketOutlinePenGray = new Pen(new SolidColorBrush(Color.FromRgb(100, 116, 139)), 1.5);
        private static readonly Pen JacketOutlinePenAlarm = new Pen(new SolidColorBrush(Color.FromRgb(220, 38, 38)), 2.0);
        private static readonly IBrush MotorBrush = new SolidColorBrush(Color.FromRgb(65, 70, 80));
        private static readonly IBrush MotorBrushGray = new SolidColorBrush(Color.FromRgb(148, 163, 184));
        private static readonly Pen MotorPen = new Pen(new SolidColorBrush(Color.FromRgb(130, 135, 145)), 1.5);
        private static readonly Pen ShaftPen = new Pen(new SolidColorBrush(Color.FromRgb(200, 205, 215)), 2.5);
        private static readonly Pen ShaftPenGray = new Pen(new SolidColorBrush(Color.FromRgb(71, 85, 105)), 2.5);
        private static readonly IBrush FlangeBrush = new SolidColorBrush(Color.FromRgb(160, 165, 175));
        private static readonly Pen FlangeBorderPen = new Pen(Brushes.Black, 1.0);
        private static readonly IBrush ManualBadgeBrush = new SolidColorBrush(Color.FromRgb(217, 70, 239));
        private static readonly IBrush FaultBadgeBrush = new SolidColorBrush(Color.FromRgb(220, 38, 38));

        private Pen GetImpellerPen(EquipmentOperationalState state)
        {
            var brush = ResolveEquipmentStateBrush(state, ActiveColor, InactiveColor);
            if (!ReferenceEquals(_cachedImpellerBrush, brush) || _cachedImpellerPen == null)
            {
                _cachedImpellerBrush = brush;
                _cachedImpellerPen = new Pen(brush, 3.0);
            }
            return _cachedImpellerPen;
        }

        public override void Render(DrawingContext context)
        {
            base.Render(context);

            double w = Bounds.Width;
            double h = Bounds.Height;
            if (w < 30 || h < 40) return;

            double motorH = Math.Clamp(h * 0.15, 14.0, 28.0);
            double motorW = Math.Clamp(w * 0.28, 16.0, 36.0);
            double bottomNozzleH = Math.Clamp(h * 0.08, 6.0, 14.0);

            bool isGray = IsIndustrialGrayTheme;
            bool isLight = ActualThemeVariant == Avalonia.Styling.ThemeVariant.Light ||
                           (!isGray && Application.Current?.ActualThemeVariant == Avalonia.Styling.ThemeVariant.Light);

            var vesselBg = isGray ? VesselBgBrushGray : (isLight ? VesselBgBrushLight : VesselBgBrushDark);
            var vesselOutline = isGray ? VesselOutlinePenGray : (isLight ? VesselOutlinePenLight : VesselOutlinePenDark);
            var motorFill = isGray ? MotorBrushGray : MotorBrush;
            var shaftPen = isGray ? ShaftPenGray : ShaftPen;

            var bodyRect = new Rect(10, motorH, Math.Max(10, w - 20), Math.Max(10, h - motorH - bottomNozzleH));

            // 1. Рубашка обогрева/охлаждения (Jacket)
            if (HasJacket)
            {
                double jacketTop = bodyRect.Top + bodyRect.Height * 0.25;
                double jacketH = bodyRect.Height * 0.7;
                var jacketRect = new Rect(bodyRect.Left - 5, jacketTop, bodyRect.Width + 10, jacketH);
                var jBrush = HasTempAlarm ? JacketBrushAlarm : (isGray ? JacketBrushGray : JacketBrushColor);
                var jPen = HasTempAlarm ? JacketOutlinePenAlarm : (isGray ? JacketOutlinePenGray : JacketOutlinePenColor);
                context.DrawRectangle(jBrush, jPen, jacketRect, 8, 8);

                // Патрубки рубашки
                context.DrawRectangle(motorFill, vesselOutline, new Rect(bodyRect.Left - 10, jacketTop + 4, 6, 8));
                context.DrawRectangle(motorFill, vesselOutline, new Rect(bodyRect.Right + 4, jacketTop + jacketH - 12, 6, 8));
            }

            // 2. Корпус реактора (Vessel) со скругленными днищами
            context.DrawRectangle(vesselBg, vesselOutline, bodyRect, 14, 14);

            // 3. Заполнение жидкостью (Level, нормализовано MinValue..MaxValue)
            double range = Math.Max(0.001, MaxValue - MinValue);
            double levelRatio = Math.Clamp((Level - MinValue) / range, 0.0, 1.0);
            if (levelRatio > 0.02)
            {
                double liquidH = (bodyRect.Height - 10) * levelRatio;
                var liquidRect = new Rect(bodyRect.Left + 2, bodyRect.Bottom - liquidH - 2, bodyRect.Width - 4, liquidH);
                var liqBrush = HasLevelAlarm ? LiquidBrushAlarm : (isGray ? LiquidBrushGray : LiquidBrushColor);
                context.DrawRectangle(liqBrush, null, liquidRect, 10, 10);
            }

            // 4. Электропривод мешалки (Motor & Gearbox сверху)
            double cx = bodyRect.Center.X;
            var motorRect = new Rect(cx - motorW / 2, 0, motorW, motorH);
            context.DrawRectangle(motorFill, MotorPen, motorRect, 3, 3);

            // Шток / Вал мешалки (Shaft)
            double shaftBottom = bodyRect.Bottom - 12;
            context.DrawLine(shaftPen, new Point(cx, motorRect.Bottom), new Point(cx, shaftBottom));

            // 5. Лопасти импеллера мешалки
            var effectiveState = IsFault ? EquipmentOperationalState.Fault :
                                 (OperationalState != EquipmentOperationalState.Stopped ? OperationalState :
                                 (IsAgitatorRunning ? EquipmentOperationalState.Running : EquipmentOperationalState.Stopped));
            var impellerPen = GetImpellerPen(effectiveState);
            double bladeSpan = bodyRect.Width * 0.35;

            double cos = IsSteadyAnimationSuppressed ? 1.0 : Math.Cos(_rotationAngle * Math.PI / 180.0);
            double currentSpan = bladeSpan * cos;

            // Нижний ярус лопастей
            context.DrawLine(impellerPen, new Point(cx - currentSpan, shaftBottom - 4), new Point(cx + currentSpan, shaftBottom - 4));
            context.DrawLine(impellerPen, new Point(cx - currentSpan, shaftBottom - 10), new Point(cx - currentSpan, shaftBottom + 2));
            context.DrawLine(impellerPen, new Point(cx + currentSpan, shaftBottom - 10), new Point(cx + currentSpan, shaftBottom + 2));

            // Средний ярус лопастей
            double midShaftY = bodyRect.Top + bodyRect.Height * 0.55;
            double midSpan = currentSpan * 0.85;
            context.DrawLine(impellerPen, new Point(cx - midSpan, midShaftY), new Point(cx + midSpan, midShaftY));

            // 6. Верхний загрузочный и нижний сливной штуцеры с фланцами
            double nozzleW = 12.0;
            double topNozzleX = bodyRect.Left + bodyRect.Width * 0.2;
            context.DrawRectangle(motorFill, vesselOutline, new Rect(topNozzleX - nozzleW / 2, bodyRect.Top - 8, nozzleW, 8));
            context.DrawRectangle(FlangeBrush, FlangeBorderPen, new Rect(topNozzleX - 9, bodyRect.Top - 10, 18, 3));

            context.DrawRectangle(motorFill, vesselOutline, new Rect(cx - nozzleW / 2, bodyRect.Bottom, nozzleW, bottomNozzleH));
            context.DrawRectangle(FlangeBrush, FlangeBorderPen, new Rect(cx - 9, Bounds.Height - 3, 18, 3));

            // 7. Компактные бейджи [M] (ручной режим) и [!] (авария/тревога)
            if (IsManualMode)
            {
                var badgeRect = new Rect(2, 2, 18, 14);
                context.DrawRectangle(ManualBadgeBrush, null, badgeRect, 3, 3);
                _cachedManualText ??= new FormattedText("M", CultureInfo.InvariantCulture, FlowDirection.LeftToRight,
                    new Typeface(FontFamily.Default, FontStyle.Normal, FontWeight.Bold), 10, Brushes.White);
                context.DrawText(_cachedManualText, new Point(badgeRect.Center.X - _cachedManualText.Width / 2, badgeRect.Center.Y - _cachedManualText.Height / 2));
            }

            if (IsFault || HasTempAlarm || HasLevelAlarm)
            {
                var badgeRect = new Rect(w - 20, 2, 18, 14);
                context.DrawRectangle(FaultBadgeBrush, null, badgeRect, 3, 3);
                _cachedFaultText ??= new FormattedText("!", CultureInfo.InvariantCulture, FlowDirection.LeftToRight,
                    new Typeface(FontFamily.Default, FontStyle.Normal, FontWeight.Bold), 10, Brushes.White);
                context.DrawText(_cachedFaultText, new Point(badgeRect.Center.X - _cachedFaultText.Width / 2, badgeRect.Center.Y - _cachedFaultText.Height / 2));
            }
        }
    }
}
