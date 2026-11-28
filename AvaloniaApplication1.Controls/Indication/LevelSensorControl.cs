using System;
using System.Globalization;
using Avalonia;
using Avalonia.Media;
using AvaloniaApplication1.Controls.Primitives;

namespace AvaloniaApplication1.Controls.Indication
{
    /// <summary>
    /// Промышленный приборный контрол датчика уровня (Level Transmitter / LT)
    /// по стандарту ISA 5.1 / ISA-101 с поддержкой 3 тем (Dark, Light, IndustrialGray),
    /// типов Radar / Ultrasonic / Float / Hydrostatic и нулевыми аллокациями в Render.
    /// </summary>
    public class LevelSensorControl : ScadaControlBase
    {
        public static readonly StyledProperty<double> ValueProperty =
            AvaloniaProperty.Register<LevelSensorControl, double>(nameof(Value), 50.0);

        public static readonly StyledProperty<string> TagNumberProperty =
            AvaloniaProperty.Register<LevelSensorControl, string>(nameof(TagNumber), "LT-101");

        public static readonly StyledProperty<string> SensorTypeProperty =
            AvaloniaProperty.Register<LevelSensorControl, string>(nameof(SensorType), "Radar");

        public static readonly StyledProperty<string> UnitProperty =
            AvaloniaProperty.Register<LevelSensorControl, string>(nameof(Unit), "%");

        public static readonly StyledProperty<double> MinValueProperty =
            AvaloniaProperty.Register<LevelSensorControl, double>(nameof(MinValue), 0.0);

        public static readonly StyledProperty<double> MaxValueProperty =
            AvaloniaProperty.Register<LevelSensorControl, double>(nameof(MaxValue), 100.0);

        public static readonly StyledProperty<double> AlarmHighProperty =
            AvaloniaProperty.Register<LevelSensorControl, double>(nameof(AlarmHigh), 90.0);

        public static readonly StyledProperty<double> AlarmLowProperty =
            AvaloniaProperty.Register<LevelSensorControl, double>(nameof(AlarmLow), 10.0);

        public static readonly StyledProperty<bool> IsOfflineProperty =
            AvaloniaProperty.Register<LevelSensorControl, bool>(nameof(IsOffline), false);

        public double Value
        {
            get => GetValue(ValueProperty);
            set => SetValue(ValueProperty, value);
        }

        public string TagNumber
        {
            get => GetValue(TagNumberProperty);
            set => SetValue(TagNumberProperty, value);
        }

        public string SensorType
        {
            get => GetValue(SensorTypeProperty);
            set => SetValue(SensorTypeProperty, value);
        }

        public string Unit
        {
            get => GetValue(UnitProperty);
            set => SetValue(UnitProperty, value);
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

        public double AlarmHigh
        {
            get => GetValue(AlarmHighProperty);
            set => SetValue(AlarmHighProperty, value);
        }

        public double AlarmLow
        {
            get => GetValue(AlarmLowProperty);
            set => SetValue(AlarmLowProperty, value);
        }

        public bool IsOffline
        {
            get => GetValue(IsOfflineProperty);
            set => SetValue(IsOfflineProperty, value);
        }

        static LevelSensorControl()
        {
            AffectsRender<LevelSensorControl>(
                ValueProperty,
                TagNumberProperty,
                SensorTypeProperty,
                UnitProperty,
                MinValueProperty,
                MaxValueProperty,
                AlarmHighProperty,
                AlarmLowProperty,
                IsOfflineProperty);
        }

        // --- Статически кэшированные перья и кисти (Zero-Allocation) ---
        private static readonly IBrush BubbleBgBrushDark = new SolidColorBrush(Color.FromRgb(30, 32, 38));
        private static readonly IBrush BubbleBgBrushLight = new SolidColorBrush(Color.FromRgb(245, 247, 250));
        private static readonly IBrush BubbleBgBrushGray = new SolidColorBrush(Color.FromRgb(226, 232, 240));
        private static readonly IBrush TextLtBrushLight = Brushes.Black;
        private static readonly IBrush TextTagBrushLight = new SolidColorBrush(Color.FromRgb(80, 80, 80));
        private static readonly IBrush TextGrayBrush = new SolidColorBrush(Color.FromRgb(51, 65, 85));
        private static readonly Pen BubblePenNormal = new Pen(new SolidColorBrush(Color.FromRgb(0, 180, 255)), 2.0);
        private static readonly Pen BubblePenNormalGray = new Pen(new SolidColorBrush(Color.FromRgb(100, 116, 139)), 2.0);
        private static readonly Pen BubblePenOffline = new Pen(new SolidColorBrush(Color.FromRgb(107, 114, 128)), 2.0);
        private static readonly Pen BubblePenAlarmHi = new Pen(new SolidColorBrush(Color.FromRgb(220, 38, 38)), 2.5);
        private static readonly Pen BubblePenAlarmLo = new Pen(new SolidColorBrush(Color.FromRgb(217, 119, 6)), 2.5);
        private static readonly Pen WaveguidePen = new Pen(new SolidColorBrush(Color.FromRgb(180, 185, 195)), 2.0);
        private static readonly Pen WaveguidePenGray = new Pen(new SolidColorBrush(Color.FromRgb(100, 116, 139)), 2.0);
        private static readonly Pen RadarWavesPen = new Pen(new SolidColorBrush(Color.FromArgb(120, 0, 200, 255)), 1.5);
        private static readonly Pen RadarWavesPenGray = new Pen(new SolidColorBrush(Color.FromArgb(140, 100, 116, 139)), 1.5);
        private static readonly IBrush FlangeBrush = new SolidColorBrush(Color.FromRgb(140, 145, 155));
        private static readonly Pen FlangeBorderPen = new Pen(Brushes.Black, 1.0);
        private static readonly IBrush AlarmHiBrush = new SolidColorBrush(Color.FromRgb(220, 38, 38));
        private static readonly IBrush AlarmLoBrush = new SolidColorBrush(Color.FromRgb(217, 119, 6));
        private static readonly Typeface InstrumentTypeface = new Typeface(FontFamily.Default, FontStyle.Normal, FontWeight.Bold);

        // --- Кэшированный текст (перестраивается только при смене радиуса/темы/номера тега) ---
        private FormattedText? _cachedLtText;
        private double _cachedLtSize;
        private bool _cachedIsLightOrGray;
        private FormattedText? _cachedTagText;
        private string? _cachedTagShort;
        private double _cachedTagSize;
        private FormattedText? _cachedFaultText;

        protected override void InvalidateCachedBrushes()
        {
            _cachedLtText = null;
            _cachedTagText = null;
        }

        public override void Render(DrawingContext context)
        {
            base.Render(context);

            double w = Bounds.Width;
            double h = Bounds.Height;
            if (w < 20 || h < 20) return;

            double cx = w / 2.0;
            double bubbleRadius = Math.Clamp(Math.Min(w, h) * 0.28, 14.0, 32.0);
            var bubbleCenter = new Point(cx, bubbleRadius + 4);

            bool isHiAlarm = !IsOffline && Value >= AlarmHigh;
            bool isLoAlarm = !IsOffline && Value <= AlarmLow;

            bool isGray = IsIndustrialGrayTheme;
            bool isLight = ActualThemeVariant == Avalonia.Styling.ThemeVariant.Light ||
                           (!isGray && Application.Current?.ActualThemeVariant == Avalonia.Styling.ThemeVariant.Light);

            var bubblePen = IsOffline ? BubblePenOffline :
                            (isHiAlarm ? BubblePenAlarmHi :
                            (isLoAlarm ? BubblePenAlarmLo :
                            (isGray ? BubblePenNormalGray : BubblePenNormal)));

            var bubbleBg = isGray ? BubbleBgBrushGray : (isLight ? BubbleBgBrushLight : BubbleBgBrushDark);
            var textLtBrush = isGray ? TextGrayBrush : (isLight ? TextLtBrushLight : Brushes.White);
            var textTagBrush = isGray ? TextGrayBrush : (isLight ? TextTagBrushLight : Brushes.LightGray);
            var wgPen = isGray ? WaveguidePenGray : WaveguidePen;
            var wavesPen = isGray ? RadarWavesPenGray : RadarWavesPen;

            // 1. Приборный круг стандарта ISA (Bubble)
            context.DrawEllipse(bubbleBg, bubblePen, bubbleCenter, bubbleRadius, bubbleRadius);
            context.DrawLine(bubblePen,
                new Point(bubbleCenter.X - bubbleRadius + 2, bubbleCenter.Y),
                new Point(bubbleCenter.X + bubbleRadius - 2, bubbleCenter.Y));

            // Кэшированное обозначение "LT"
            double ltFontSize = bubbleRadius * 0.5;
            bool lightOrGray = isLight || isGray;
            if (_cachedLtText == null || Math.Abs(_cachedLtSize - ltFontSize) > 0.1 || _cachedIsLightOrGray != lightOrGray)
            {
                _cachedLtSize = ltFontSize;
                _cachedIsLightOrGray = lightOrGray;
                _cachedLtText = new FormattedText("LT", CultureInfo.InvariantCulture, FlowDirection.LeftToRight,
                    InstrumentTypeface, ltFontSize, textLtBrush);
            }
            context.DrawText(_cachedLtText, new Point(bubbleCenter.X - _cachedLtText.Width / 2, bubbleCenter.Y - bubbleRadius * 0.75));

            // Кэшированный номер позиции датчика
            string tagShort = !string.IsNullOrEmpty(TagNumber) ? TagNumber : "101";
            if (tagShort.Length > 5) tagShort = tagShort.Substring(0, 5);
            double tagFontSize = bubbleRadius * 0.4;
            if (_cachedTagText == null || _cachedTagShort != tagShort || Math.Abs(_cachedTagSize - tagFontSize) > 0.1 || _cachedIsLightOrGray != lightOrGray)
            {
                _cachedTagShort = tagShort;
                _cachedTagSize = tagFontSize;
                _cachedTagText = new FormattedText(tagShort, CultureInfo.InvariantCulture, FlowDirection.LeftToRight,
                    InstrumentTypeface, tagFontSize, textTagBrush);
            }
            context.DrawText(_cachedTagText, new Point(bubbleCenter.X - _cachedTagText.Width / 2, bubbleCenter.Y + bubbleRadius * 0.15));

            // 2. Монтажный фланец датчика
            double flangeY = bubbleCenter.Y + bubbleRadius + 8;
            double flangeW = bubbleRadius * 1.2;
            context.DrawLine(wgPen, new Point(cx, bubbleCenter.Y + bubbleRadius), new Point(cx, flangeY));
            context.DrawRectangle(FlangeBrush, FlangeBorderPen, new Rect(cx - flangeW / 2, flangeY, flangeW, 3.5));

            // 3. Волновод / зонд датчика (Probe) вниз
            double probeBottom = h - 6;
            if (probeBottom > flangeY + 8)
            {
                context.DrawLine(wgPen, new Point(cx, flangeY + 3.5), new Point(cx, probeBottom));

                if (string.Equals(SensorType, "Radar", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(SensorType, "Ultrasonic", StringComparison.OrdinalIgnoreCase))
                {
                    double waveY1 = flangeY + (probeBottom - flangeY) * 0.35;
                    double waveY2 = flangeY + (probeBottom - flangeY) * 0.7;
                    context.DrawLine(wavesPen, new Point(cx - 6, waveY1), new Point(cx + 6, waveY1));
                    context.DrawLine(wavesPen, new Point(cx - 10, waveY2), new Point(cx + 10, waveY2));
                }
                else if (string.Equals(SensorType, "Hydrostatic", StringComparison.OrdinalIgnoreCase))
                {
                    // Гидростатическая измерительная капсула на конце зонда
                    context.DrawRectangle(FlangeBrush, FlangeBorderPen, new Rect(cx - 5, probeBottom - 6, 10, 8), 2, 2);
                }
                else
                {
                    // Поплавок
                    context.DrawEllipse(FlangeBrush, FlangeBorderPen, new Point(cx, flangeY + (probeBottom - flangeY) * 0.5), 5, 4);
                }
            }

            // 4. Бейдж [!] при аварии Hi/Lo
            if (isHiAlarm || isLoAlarm)
            {
                var badgeBrush = isHiAlarm ? AlarmHiBrush : AlarmLoBrush;
                var badgeRect = new Rect(w - 18, 2, 16, 13);
                context.DrawRectangle(badgeBrush, null, badgeRect, 3, 3);
                _cachedFaultText ??= new FormattedText("!", CultureInfo.InvariantCulture, FlowDirection.LeftToRight,
                    InstrumentTypeface, 10, Brushes.White);
                context.DrawText(_cachedFaultText, new Point(badgeRect.Center.X - _cachedFaultText.Width / 2, badgeRect.Center.Y - _cachedFaultText.Height / 2));
            }
        }
    }
}
