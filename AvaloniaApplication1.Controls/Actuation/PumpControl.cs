using System;
using System.Globalization;
using Avalonia;
using Avalonia.Media;
using Avalonia.Threading;
using AvaloniaApplication1.Controls.Primitives;

namespace AvaloniaApplication1.Controls.Actuation
{
    /// <summary>
    /// Production-grade SCADA centrifugal pump control.
    /// Supports ISA-101 IndustrialGray theme (muted grayscale for normal states, steady-state animation suppression),
    /// Manual/Auto [M] badge, Fault/Warning [!] badge, VFD frequency overlay, and zero-allocation Render.
    /// </summary>
    public class PumpControl : ScadaControlBase
    {
        public static readonly StyledProperty<EquipmentOperationalState> OperationalStateProperty =
            AvaloniaProperty.Register<PumpControl, EquipmentOperationalState>(nameof(OperationalState), EquipmentOperationalState.Stopped);

        public static readonly StyledProperty<bool> IsRunningProperty =
            AvaloniaProperty.Register<PumpControl, bool>(nameof(IsRunning), false);

        public static readonly StyledProperty<bool> IsManualModeProperty =
            AvaloniaProperty.Register<PumpControl, bool>(nameof(IsManualMode), false);

        public static readonly StyledProperty<bool> IsFaultProperty =
            AvaloniaProperty.Register<PumpControl, bool>(nameof(IsFault), false);

        public static readonly StyledProperty<bool> HasWarningProperty =
            AvaloniaProperty.Register<PumpControl, bool>(nameof(HasWarning), false);

        public static readonly StyledProperty<bool> IsVfdModeProperty =
            AvaloniaProperty.Register<PumpControl, bool>(nameof(IsVfdMode), false);

        public static readonly StyledProperty<string> FrequencyTextProperty =
            AvaloniaProperty.Register<PumpControl, string>(nameof(FrequencyText), string.Empty);

        public static readonly StyledProperty<int> RotationProperty =
            AvaloniaProperty.Register<PumpControl, int>(nameof(Rotation), 0);

        public static readonly StyledProperty<string> ActiveColorProperty =
            AvaloniaProperty.Register<PumpControl, string>(nameof(ActiveColor), string.Empty);

        public static readonly StyledProperty<string> InactiveColorProperty =
            AvaloniaProperty.Register<PumpControl, string>(nameof(InactiveColor), string.Empty);

        static PumpControl()
        {
            AffectsRender<PumpControl>(
                OperationalStateProperty,
                IsRunningProperty,
                IsManualModeProperty,
                IsFaultProperty,
                HasWarningProperty,
                IsVfdModeProperty,
                FrequencyTextProperty,
                RotationProperty,
                ActiveColorProperty,
                InactiveColorProperty);
        }

        public EquipmentOperationalState OperationalState
        {
            get => GetValue(OperationalStateProperty);
            set => SetValue(OperationalStateProperty, value);
        }

        public bool IsRunning
        {
            get => GetValue(IsRunningProperty);
            set => SetValue(IsRunningProperty, value);
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

        public bool HasWarning
        {
            get => GetValue(HasWarningProperty);
            set => SetValue(HasWarningProperty, value);
        }

        public bool IsVfdMode
        {
            get => GetValue(IsVfdModeProperty);
            set => SetValue(IsVfdModeProperty, value);
        }

        public string FrequencyText
        {
            get => GetValue(FrequencyTextProperty);
            set => SetValue(FrequencyTextProperty, value);
        }

        public int Rotation
        {
            get => GetValue(RotationProperty);
            set => SetValue(RotationProperty, value);
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

        private DispatcherTimer? _animTimer;
        private double _impellerAngle;

        private Pen? _casingPen;
        private Pen? _volutePen;
        private Pen? _impellerPen;
        private IBrush? _lastStateBrush;
        private IBrush? _casingBgBrush;
        private IBrush? _hubBrush;
        private IBrush? _overrideBrush;
        private IBrush? _faultBrush;
        private IBrush? _warningBrush;
        private IBrush? _badgeTextBrush;
        private static readonly Typeface BadgeTypeface = new("Segoe UI", FontStyle.Normal, FontWeight.Bold);

        protected override void InvalidateCachedBrushes()
        {
            _casingPen = null;
            _volutePen = null;
            _impellerPen = null;
            _lastStateBrush = null;
            _casingBgBrush = null;
            _hubBrush = null;
            _overrideBrush = null;
            _faultBrush = null;
            _warningBrush = null;
            _badgeTextBrush = null;
        }

        protected override void OnThemeChanged()
        {
            UpdateAnimationTimer();
        }

        protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
        {
            base.OnPropertyChanged(change);
            if (change.Property == OperationalStateProperty || change.Property == IsRunningProperty)
            {
                UpdateAnimationTimer();
            }
        }

        protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
        {
            base.OnAttachedToVisualTree(e);
            UpdateAnimationTimer();
        }

        protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
        {
            base.OnDetachedFromVisualTree(e);
            StopAnimationTimer();
        }

        private void UpdateAnimationTimer()
        {
            var effectiveRunning = OperationalState == EquipmentOperationalState.Running || (IsRunning && OperationalState != EquipmentOperationalState.Fault && OperationalState != EquipmentOperationalState.Offline);
            if (effectiveRunning && !IsSteadyAnimationSuppressed && VisualRoot != null)
            {
                if (_animTimer == null)
                {
                    _animTimer = new DispatcherTimer(TimeSpan.FromMilliseconds(80), DispatcherPriority.Background, (_, _) =>
                    {
                        _impellerAngle = (_impellerAngle + 15.0) % 360.0;
                        InvalidateVisual();
                    });
                }
                if (!_animTimer.IsEnabled)
                    _animTimer.Start();
            }
            else
            {
                StopAnimationTimer();
            }
        }

        private void StopAnimationTimer()
        {
            if (_animTimer != null && _animTimer.IsEnabled)
                _animTimer.Stop();
        }

        public override void Render(DrawingContext context)
        {
            var bounds = Bounds;
            if (bounds.Width <= 8 || bounds.Height <= 8) return;

            var effectiveState = IsFault ? EquipmentOperationalState.Fault : OperationalState;
            if (effectiveState == EquipmentOperationalState.Stopped && IsRunning)
                effectiveState = EquipmentOperationalState.Running;

            var stateBrush = ResolveEquipmentStateBrush(effectiveState, ActiveColor, InactiveColor);
            _casingBgBrush ??= ResolveThemeBrush("ThemeBackgroundBrush", Color.Parse("#1E1E1E"));
            _casingPen ??= new Pen(ResolveThemeBrush("HmiNormalBorderBrush", Color.Parse("#64748B")), 2.0);
            if (!ReferenceEquals(_lastStateBrush, stateBrush) || _volutePen == null || _impellerPen == null)
            {
                _lastStateBrush = stateBrush;
                _volutePen = new Pen(stateBrush, 2.5);
                _impellerPen = new Pen(stateBrush, 2.0, lineCap: PenLineCap.Round);
            }
            _hubBrush = stateBrush;

            var minDim = Math.Min(bounds.Width, bounds.Height);
            var radius = Math.Max(10.0, minDim * 0.36);
            var cx = bounds.Width / 2.0;
            var cy = bounds.Height / 2.0;
            var center = new Point(cx, cy);

            // Draw discharge nozzle rotated by Rotation
            var radRot = Rotation * Math.PI / 180.0;
            using (context.PushTransform(Matrix.CreateTranslation(-cx, -cy) * Matrix.CreateRotation(radRot) * Matrix.CreateTranslation(cx, cy)))
            {
                var nozzleW = radius * 0.72;
                var nozzleH = radius * 0.55;
                var nozzleRect = new Rect(cx, cy - radius, nozzleW + radius * 0.35, nozzleH);
                context.DrawRectangle(_casingBgBrush, _casingPen, nozzleRect, 2, 2);

                // Base stand
                var baseRect = new Rect(cx - radius * 0.7, cy + radius * 0.75, radius * 1.4, radius * 0.25);
                context.DrawRectangle(stateBrush, _casingPen, baseRect, 1, 1);
            }

            // Outer volute casing
            context.DrawEllipse(_casingBgBrush, _volutePen, center, radius, radius);

            // Impeller blades
            var bladeRadius = radius * 0.68;
            var angleOffset = IsSteadyAnimationSuppressed ? 0.0 : _impellerAngle * Math.PI / 180.0;
            for (int i = 0; i < 4; i++)
            {
                var a = angleOffset + i * (Math.PI / 2.0);
                var p1 = new Point(cx + Math.Cos(a) * bladeRadius, cy + Math.Sin(a) * bladeRadius);
                var p2 = new Point(cx - Math.Cos(a) * bladeRadius, cy - Math.Sin(a) * bladeRadius);
                context.DrawLine(_impellerPen, p1, p2);
            }

            // Center hub
            var hubR = Math.Max(3.5, radius * 0.22);
            context.DrawEllipse(_hubBrush, null, center, hubR, hubR);

            // Manual Override [M] badge (top-left)
            if (IsManualMode)
            {
                _overrideBrush ??= ResolveThemeBrush("HmiOverrideBrush", Color.Parse("#0284C7"));
                _badgeTextBrush ??= Brushes.White;
                var badgeRect = new Rect(2, 2, 16, 14);
                context.DrawRectangle(_overrideBrush, null, badgeRect, 3, 3);
                var ft = new FormattedText("M", CultureInfo.InvariantCulture, FlowDirection.LeftToRight, BadgeTypeface, 9.5, _badgeTextBrush);
                context.DrawText(ft, new Point(badgeRect.X + (badgeRect.Width - ft.Width) / 2, badgeRect.Y + (badgeRect.Height - ft.Height) / 2));
            }

            // Fault / Warning [!] badge (top-right)
            if (IsFault || HasWarning)
            {
                _faultBrush ??= ResolveThemeBrush("HmiFaultBrush", Color.Parse("#DC2626"));
                _warningBrush ??= ResolveThemeBrush("HmiWarningBrush", Color.Parse("#D97706"));
                _badgeTextBrush ??= Brushes.White;
                var badgeBrush = IsFault ? _faultBrush : _warningBrush;
                var badgeRect = new Rect(Math.Max(2, bounds.Width - 18), 2, 16, 14);
                context.DrawRectangle(badgeBrush, null, badgeRect, 3, 3);
                var ft = new FormattedText("!", CultureInfo.InvariantCulture, FlowDirection.LeftToRight, BadgeTypeface, 10, _badgeTextBrush);
                context.DrawText(ft, new Point(badgeRect.X + (badgeRect.Width - ft.Width) / 2, badgeRect.Y + (badgeRect.Height - ft.Height) / 2));
            }
        }
    }
}
