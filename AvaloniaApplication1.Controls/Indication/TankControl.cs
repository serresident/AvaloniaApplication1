using System;
using System.Globalization;
using Avalonia;
using Avalonia.Media;
using AvaloniaApplication1.Controls.Primitives;

namespace AvaloniaApplication1.Controls.Indication
{
    /// <summary>
    /// Industrial Tank / Vessel level indication control with ISA-101 3-theme support,
    /// Hi/Lo alarm markers, Cylindrical/Rectangular styles, and zero-allocation Render.
    /// </summary>
    public class TankControl : ScadaControlBase
    {
        public static readonly StyledProperty<double> LevelProperty =
            AvaloniaProperty.Register<TankControl, double>(nameof(Level), 0.0);

        public static readonly StyledProperty<double> MinValueProperty =
            AvaloniaProperty.Register<TankControl, double>(nameof(MinValue), 0.0);

        public static readonly StyledProperty<double> MaxValueProperty =
            AvaloniaProperty.Register<TankControl, double>(nameof(MaxValue), 100.0);

        public static readonly StyledProperty<double> AlarmHighProperty =
            AvaloniaProperty.Register<TankControl, double>(nameof(AlarmHigh), 90.0);

        public static readonly StyledProperty<double> AlarmLowProperty =
            AvaloniaProperty.Register<TankControl, double>(nameof(AlarmLow), 10.0);

        public static readonly StyledProperty<bool> ShowAlarmMarkersProperty =
            AvaloniaProperty.Register<TankControl, bool>(nameof(ShowAlarmMarkers), true);

        public static readonly StyledProperty<bool> IsHighAlarmProperty =
            AvaloniaProperty.Register<TankControl, bool>(nameof(IsHighAlarm), false);

        public static readonly StyledProperty<bool> IsLowAlarmProperty =
            AvaloniaProperty.Register<TankControl, bool>(nameof(IsLowAlarm), false);

        public static readonly StyledProperty<bool> IsOfflineProperty =
            AvaloniaProperty.Register<TankControl, bool>(nameof(IsOffline), false);

        public static readonly StyledProperty<string> VesselStyleProperty =
            AvaloniaProperty.Register<TankControl, string>(nameof(VesselStyle), "Cylindrical");

        public static readonly StyledProperty<string> FillColorProperty =
            AvaloniaProperty.Register<TankControl, string>(nameof(FillColor), string.Empty);

        static TankControl()
        {
            AffectsRender<TankControl>(
                LevelProperty,
                MinValueProperty,
                MaxValueProperty,
                AlarmHighProperty,
                AlarmLowProperty,
                ShowAlarmMarkersProperty,
                IsHighAlarmProperty,
                IsLowAlarmProperty,
                IsOfflineProperty,
                VesselStyleProperty,
                FillColorProperty);
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

        public bool ShowAlarmMarkers
        {
            get => GetValue(ShowAlarmMarkersProperty);
            set => SetValue(ShowAlarmMarkersProperty, value);
        }

        public bool IsHighAlarm
        {
            get => GetValue(IsHighAlarmProperty);
            set => SetValue(IsHighAlarmProperty, value);
        }

        public bool IsLowAlarm
        {
            get => GetValue(IsLowAlarmProperty);
            set => SetValue(IsLowAlarmProperty, value);
        }

        public bool IsOffline
        {
            get => GetValue(IsOfflineProperty);
            set => SetValue(IsOfflineProperty, value);
        }

        public string VesselStyle
        {
            get => GetValue(VesselStyleProperty);
            set => SetValue(VesselStyleProperty, value);
        }

        public string FillColor
        {
            get => GetValue(FillColorProperty);
            set => SetValue(FillColorProperty, value);
        }

        private Pen? _shellPen;
        private Pen? _alarmHiPen;
        private Pen? _alarmLoPen;
        private IBrush? _shellBgBrush;
        private IBrush? _defaultLiquidBrush;
        private IBrush? _highAlarmBrush;
        private IBrush? _lowAlarmBrush;
        private IBrush? _offlineBrush;
        private Pen? _tickPen;
        private string? _cachedCustomColorHex;
        private IBrush? _cachedCustomBrush;
        private FormattedText? _cachedFaultText;

        protected override void InvalidateCachedBrushes()
        {
            _shellPen = null;
            _alarmHiPen = null;
            _alarmLoPen = null;
            _shellBgBrush = null;
            _defaultLiquidBrush = null;
            _highAlarmBrush = null;
            _lowAlarmBrush = null;
            _offlineBrush = null;
            _tickPen = null;
            _cachedCustomColorHex = null;
            _cachedCustomBrush = null;
        }

        private IBrush? GetCustomFillBrush()
        {
            if (IsIndustrialGrayTheme || string.IsNullOrWhiteSpace(FillColor)) return null;
            if (!string.Equals(_cachedCustomColorHex, FillColor, StringComparison.Ordinal))
            {
                _cachedCustomColorHex = FillColor;
                _cachedCustomBrush = Color.TryParse(FillColor, out var c) ? new SolidColorBrush(c) : null;
            }
            return _cachedCustomBrush;
        }

        public override void Render(DrawingContext context)
        {
            var bounds = Bounds;
            if (bounds.Width <= 8 || bounds.Height <= 8) return;

            var rect = new Rect(2, 2, bounds.Width - 4, bounds.Height - 4);
            bool isCylindrical = !string.Equals(VesselStyle, "Rectangular", StringComparison.OrdinalIgnoreCase);
            double cornerRadius = isCylindrical ? Math.Min(16.0, rect.Width * 0.22) : 3.0;

            _shellBgBrush ??= ResolveThemeBrush("ThemeBackgroundBrush", Color.Parse("#1E1E1E"));
            _shellPen ??= new Pen(ResolveThemeBrush("HmiNormalBorderBrush", Color.Parse("#475569")), 1.5);
            _tickPen ??= new Pen(ResolveThemeBrush("ThemeMutedTextBrush", Color.Parse("#64748B")), 1.0);
            _highAlarmBrush ??= ResolveThemeBrush("HmiFaultBrush", Color.Parse("#DC2626"));
            _lowAlarmBrush ??= ResolveThemeBrush("HmiWarningBrush", Color.Parse("#D97706"));
            _offlineBrush ??= ResolveThemeBrush("HmiOfflineBrush", Color.Parse("#6B7280"));
            _alarmHiPen ??= new Pen(_highAlarmBrush, 2.0);
            _alarmLoPen ??= new Pen(_lowAlarmBrush, 2.0);

            IBrush liquidBrush;
            Pen borderPen = _shellPen;
            if (IsOffline)
            {
                liquidBrush = _offlineBrush;
            }
            else if (IsHighAlarm)
            {
                liquidBrush = _highAlarmBrush;
                borderPen = _alarmHiPen;
            }
            else if (IsLowAlarm)
            {
                liquidBrush = _lowAlarmBrush;
                borderPen = _alarmLoPen;
            }
            else
            {
                liquidBrush = GetCustomFillBrush()
                    ?? (_defaultLiquidBrush ??= IsIndustrialGrayTheme
                        ? new SolidColorBrush(Color.Parse("#64748B"))
                        : ResolveThemeBrush("ThemeAccentBrush", Color.Parse("#007ACC")));
            }

            // 1. Tank body
            context.DrawRectangle(_shellBgBrush, borderPen, rect, cornerRadius, cornerRadius);

            // 2. Liquid fill
            var range = MaxValue - MinValue;
            var pct = range > 1e-6 ? Math.Clamp((Level - MinValue) / range, 0.0, 1.0) : 0.0;
            if (pct > 0.0)
            {
                var innerHeight = Math.Max(0, rect.Height - 4);
                var fillHeight = innerHeight * pct;
                var fillRadius = Math.Max(1.0, Math.Min(cornerRadius - 2, fillHeight * 0.45));
                var fillRect = new Rect(rect.X + 2, rect.Bottom - 2 - fillHeight, Math.Max(0, rect.Width - 4), fillHeight);
                context.DrawRectangle(liquidBrush, null, fillRect, fillRadius, fillRadius);
            }

            // 3. Graduation ticks (25%, 50%, 75%) on the left
            var tickLen = Math.Min(6.0, rect.Width * 0.18);
            for (int i = 1; i <= 3; i++)
            {
                var y = rect.Y + rect.Height * (i * 0.25);
                context.DrawLine(_tickPen, new Point(rect.X + 2, y), new Point(rect.X + 2 + tickLen, y));
            }

            // 4. Hi / Lo alarm markers on the right
            if (ShowAlarmMarkers && range > 1e-6)
            {
                double hiPct = Math.Clamp((AlarmHigh - MinValue) / range, 0.0, 1.0);
                double hiY = rect.Bottom - 2 - (rect.Height - 4) * hiPct;
                context.DrawLine(_alarmHiPen, new Point(rect.Right - tickLen - 2, hiY), new Point(rect.Right - 2, hiY));

                double loPct = Math.Clamp((AlarmLow - MinValue) / range, 0.0, 1.0);
                double loY = rect.Bottom - 2 - (rect.Height - 4) * loPct;
                context.DrawLine(_alarmLoPen, new Point(rect.Right - tickLen - 2, loY), new Point(rect.Right - 2, loY));
            }

            // 5. Compact [!] badge on Hi/Lo alarm
            if (IsHighAlarm || IsLowAlarm)
            {
                var badgeBrush = IsHighAlarm ? _highAlarmBrush : _lowAlarmBrush;
                var badgeRect = new Rect(rect.Right - 18, rect.Top + 2, 16, 13);
                context.DrawRectangle(badgeBrush, null, badgeRect, 3, 3);
                _cachedFaultText ??= new FormattedText("!", CultureInfo.InvariantCulture, FlowDirection.LeftToRight,
                    new Typeface(FontFamily.Default, FontStyle.Normal, FontWeight.Bold), 10, Brushes.White);
                context.DrawText(_cachedFaultText, new Point(badgeRect.Center.X - _cachedFaultText.Width / 2, badgeRect.Center.Y - _cachedFaultText.Height / 2));
            }
        }
    }
}
