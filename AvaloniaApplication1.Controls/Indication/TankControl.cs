using System;
using Avalonia;
using Avalonia.Media;
using AvaloniaApplication1.Controls.Primitives;

namespace AvaloniaApplication1.Controls.Indication
{
    /// <summary>
    /// Industrial Tank / Vessel level indication control with zero-allocation Render.
    /// </summary>
    public class TankControl : ScadaControlBase
    {
        public static readonly StyledProperty<double> LevelProperty =
            AvaloniaProperty.Register<TankControl, double>(nameof(Level), 0.0);

        public static readonly StyledProperty<double> MinValueProperty =
            AvaloniaProperty.Register<TankControl, double>(nameof(MinValue), 0.0);

        public static readonly StyledProperty<double> MaxValueProperty =
            AvaloniaProperty.Register<TankControl, double>(nameof(MaxValue), 100.0);

        public static readonly StyledProperty<string> FillColorProperty =
            AvaloniaProperty.Register<TankControl, string>(nameof(FillColor), string.Empty);

        static TankControl()
        {
            AffectsRender<TankControl>(LevelProperty, MinValueProperty, MaxValueProperty, FillColorProperty);
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

        public string FillColor
        {
            get => GetValue(FillColorProperty);
            set => SetValue(FillColorProperty, value);
        }

        private Pen? _shellPen;
        private IBrush? _shellBgBrush;
        private IBrush? _defaultLiquidBrush;
        private Pen? _tickPen;

        protected override void InvalidateCachedBrushes()
        {
            _shellPen = null;
            _shellBgBrush = null;
            _defaultLiquidBrush = null;
            _tickPen = null;
        }

        public override void Render(DrawingContext context)
        {
            var bounds = Bounds;
            if (bounds.Width <= 6 || bounds.Height <= 6) return;

            var rect = new Rect(2, 2, bounds.Width - 4, bounds.Height - 4);

            _shellBgBrush ??= ResolveThemeBrush("ThemeBackgroundBrush", Color.Parse("#1E1E1E"));
            _shellPen ??= new Pen(ResolveThemeBrush("HmiNormalBorderBrush", Color.Parse("#475569")), 1.5);
            _tickPen ??= new Pen(ResolveThemeBrush("ThemeMutedTextBrush", Color.Parse("#64748B")), 1.0);

            IBrush liquidBrush;
            if (!IsIndustrialGrayTheme && !string.IsNullOrWhiteSpace(FillColor) && Color.TryParse(FillColor, out var customColor))
            {
                liquidBrush = new SolidColorBrush(customColor);
            }
            else
            {
                _defaultLiquidBrush ??= IsIndustrialGrayTheme
                    ? new SolidColorBrush(Color.Parse("#64748B"))
                    : ResolveThemeBrush("ThemeAccentBrush", Color.Parse("#007ACC"));
                liquidBrush = _defaultLiquidBrush;
            }

            // Tank body
            context.DrawRectangle(_shellBgBrush, _shellPen, rect, 3, 3);

            // Liquid fill
            var range = MaxValue - MinValue;
            var pct = range > 1e-6 ? Math.Clamp((Level - MinValue) / range, 0.0, 1.0) : 0.0;
            if (pct > 0.0)
            {
                var innerHeight = Math.Max(0, rect.Height - 2);
                var fillHeight = innerHeight * pct;
                var fillRect = new Rect(rect.X + 1, rect.Bottom - 1 - fillHeight, Math.Max(0, rect.Width - 2), fillHeight);
                context.DrawRectangle(liquidBrush, null, fillRect, 2, 2);
            }

            // Graduation ticks (25%, 50%, 75%)
            var tickLen = Math.Min(6.0, rect.Width * 0.2);
            for (int i = 1; i <= 3; i++)
            {
                var y = rect.Y + rect.Height * (i * 0.25);
                context.DrawLine(_tickPen, new Point(rect.X + 1, y), new Point(rect.X + 1 + tickLen, y));
            }
        }
    }
}
