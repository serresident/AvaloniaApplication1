using System;
using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace AvaloniaApplication1.Views
{
    /// <summary>
    /// Промышленный кастомный приборный контрол датчика уровня (Level Transmitter / LT)
    /// по стандарту ISA 5.1 / ГОСТ с отображением шкалы, волновода и аварийных зон Hi/Lo.
    /// </summary>
    public class LevelSensorControl : Control
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
                AlarmLowProperty);
        }

        // --- Кэшированные перья и кисти (Zero-Allocation) ---
        private static readonly IBrush BubbleBgBrush = new SolidColorBrush(Color.FromRgb(30, 32, 38));
        private static readonly Pen BubblePenNormal = new Pen(new SolidColorBrush(Color.FromRgb(0, 180, 255)), 2.0);
        private static readonly Pen BubblePenAlarmHi = new Pen(new SolidColorBrush(Color.FromRgb(255, 60, 60)), 2.5);
        private static readonly Pen BubblePenAlarmLo = new Pen(new SolidColorBrush(Color.FromRgb(255, 180, 0)), 2.5);
        private static readonly Pen WaveguidePen = new Pen(new SolidColorBrush(Color.FromRgb(180, 185, 195)), 2.0);
        private static readonly Pen RadarWavesPen = new Pen(new SolidColorBrush(Color.FromArgb(120, 0, 200, 255)), 1.5);
        private static readonly IBrush FlangeBrush = new SolidColorBrush(Color.FromRgb(140, 145, 155));
        private static readonly Pen FlangeBorderPen = new Pen(Brushes.Black, 1.0);
        private static readonly Typeface InstrumentTypeface = new Typeface(FontFamily.Default, FontStyle.Normal, FontWeight.Bold);

        public override void Render(DrawingContext context)
        {
            base.Render(context);

            double w = Bounds.Width;
            double h = Bounds.Height;
            if (w < 20 || h < 20) return;

            double cx = w / 2.0;
            double bubbleRadius = Math.Clamp(Math.Min(w, h) * 0.28, 14.0, 32.0);
            var bubbleCenter = new Point(cx, bubbleRadius + 4);

            // Определение статуса аварии
            bool isHiAlarm = Value >= AlarmHigh;
            bool isLoAlarm = Value <= AlarmLow;
            var bubblePen = isHiAlarm ? BubblePenAlarmHi : (isLoAlarm ? BubblePenAlarmLo : BubblePenNormal);

            // 1. Приборный круг стандарта ISA (Bubble)
            context.DrawEllipse(BubbleBgBrush, bubblePen, bubbleCenter, bubbleRadius, bubbleRadius);

            // Горизонтальная разделительная черта внутри круга
            context.DrawLine(bubblePen, 
                new Point(bubbleCenter.X - bubbleRadius + 2, bubbleCenter.Y), 
                new Point(bubbleCenter.X + bubbleRadius - 2, bubbleCenter.Y));

            // Буквенное обозначение "LT" сверху
            var textLt = new FormattedText("LT", CultureInfo.InvariantCulture, FlowDirection.LeftToRight,
                InstrumentTypeface, bubbleRadius * 0.5, Brushes.White);
            context.DrawText(textLt, new Point(bubbleCenter.X - textLt.Width / 2, bubbleCenter.Y - bubbleRadius * 0.75));

            // Номер позиции датчика снизу
            string tagShort = !string.IsNullOrEmpty(TagNumber) ? TagNumber : "101";
            if (tagShort.Length > 5) tagShort = tagShort.Substring(0, 5);
            var textTag = new FormattedText(tagShort, CultureInfo.InvariantCulture, FlowDirection.LeftToRight,
                InstrumentTypeface, bubbleRadius * 0.4, Brushes.LightGray);
            context.DrawText(textTag, new Point(bubbleCenter.X - textTag.Width / 2, bubbleCenter.Y + bubbleRadius * 0.15));

            // 2. Монтажный фланец датчика
            double flangeY = bubbleCenter.Y + bubbleRadius + 8;
            double flangeW = bubbleRadius * 1.2;
            context.DrawLine(WaveguidePen, new Point(cx, bubbleCenter.Y + bubbleRadius), new Point(cx, flangeY));
            context.DrawRectangle(FlangeBrush, FlangeBorderPen, new Rect(cx - flangeW / 2, flangeY, flangeW, 3.5));

            // 3. Волновод / зонд датчика (Probe) вниз
            double probeBottom = h - 6;
            if (probeBottom > flangeY + 8)
            {
                context.DrawLine(WaveguidePen, new Point(cx, flangeY + 3.5), new Point(cx, probeBottom));

                // Волны радара (для радарных уровнемеров)
                if (string.Equals(SensorType, "Radar", StringComparison.OrdinalIgnoreCase))
                {
                    double waveY1 = flangeY + (probeBottom - flangeY) * 0.35;
                    double waveY2 = flangeY + (probeBottom - flangeY) * 0.7;
                    context.DrawLine(RadarWavesPen, new Point(cx - 6, waveY1), new Point(cx + 6, waveY1));
                    context.DrawLine(RadarWavesPen, new Point(cx - 10, waveY2), new Point(cx + 10, waveY2));
                }
                else
                {
                    // Поплавок
                    context.DrawEllipse(FlangeBrush, FlangeBorderPen, new Point(cx, flangeY + (probeBottom - flangeY) * 0.5), 5, 4);
                }
            }
        }
    }
}
