using System;
using Avalonia;
using Avalonia.Media;
using AvaloniaApplication1.Controls.Primitives;

namespace AvaloniaApplication1.Controls.Indication
{
    /// <summary>
    /// Industrial pilot light / status indicator control.
    /// Respects ISA-101 IndustrialGray theme by muting normal states while preserving saturated fault/override colors.
    /// </summary>
    public class PilotLightControl : ScadaControlBase
    {
        public static readonly StyledProperty<bool> IsOnProperty =
            AvaloniaProperty.Register<PilotLightControl, bool>(nameof(IsOn), false);

        public static readonly StyledProperty<string> ActiveColorProperty =
            AvaloniaProperty.Register<PilotLightControl, string>(nameof(ActiveColor), string.Empty);

        public static readonly StyledProperty<string> InactiveColorProperty =
            AvaloniaProperty.Register<PilotLightControl, string>(nameof(InactiveColor), string.Empty);

        public static readonly StyledProperty<bool> IsFaultProperty =
            AvaloniaProperty.Register<PilotLightControl, bool>(nameof(IsFault), false);

        static PilotLightControl()
        {
            AffectsRender<PilotLightControl>(IsOnProperty, ActiveColorProperty, InactiveColorProperty, IsFaultProperty);
        }

        public bool IsOn
        {
            get => GetValue(IsOnProperty);
            set => SetValue(IsOnProperty, value);
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

        public bool IsFault
        {
            get => GetValue(IsFaultProperty);
            set => SetValue(IsFaultProperty, value);
        }

        private Pen? _bezelPen;
        private Pen? _innerHighlightPen;

        protected override void InvalidateCachedBrushes()
        {
            _bezelPen = null;
            _innerHighlightPen = null;
        }

        public override void Render(DrawingContext context)
        {
            var bounds = Bounds;
            if (bounds.Width <= 2 || bounds.Height <= 2) return;

            var diameter = Math.Max(8.0, Math.Min(bounds.Width, bounds.Height) - 4.0);
            var radius = diameter / 2.0;
            var center = new Point(bounds.Width / 2.0, bounds.Height / 2.0);

            var state = IsFault
                ? EquipmentOperationalState.Fault
                : (IsOn ? EquipmentOperationalState.Running : EquipmentOperationalState.Stopped);

            var fillBrush = ResolveEquipmentStateBrush(state, ActiveColor, InactiveColor);

            _bezelPen ??= new Pen(ResolveThemeBrush("HmiNormalBorderBrush", Color.Parse("#475569")), 2.0);
            _innerHighlightPen ??= new Pen(new SolidColorBrush(Color.FromArgb(90, 255, 255, 255)), 1.5);

            context.DrawEllipse(fillBrush, _bezelPen, center, radius, radius);

            if (IsOn && radius > 6)
            {
                var highlightCenter = new Point(center.X - radius * 0.28, center.Y - radius * 0.28);
                context.DrawEllipse(null, _innerHighlightPen, highlightCenter, radius * 0.25, radius * 0.18);
            }
        }
    }
}
