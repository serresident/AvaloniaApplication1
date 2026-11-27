using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace AvaloniaApplication1.Controls.Primitives
{
    public enum EquipmentOperationalState
    {
        Stopped,
        Running,
        Transit,
        Fault,
        Offline
    }

    public enum StatusSeverity
    {
        Normal,
        Info,
        Warning,
        Fault
    }

    /// <summary>
    /// Base class for custom-drawn SCADA/HMI controls in AvaloniaApplication1.Controls.
    /// Provides zero-allocation theme brush resolution, automatic cache invalidation on theme changes,
    /// and ISA-101 steady-state animation suppression.
    /// </summary>
    public abstract class ScadaControlBase : Control
    {
        protected ScadaControlBase()
        {
            ActualThemeVariantChanged += OnActualThemeVariantChanged;
        }

        private void OnActualThemeVariantChanged(object? sender, EventArgs e)
        {
            InvalidateCachedBrushes();
            OnThemeChanged();
            InvalidateVisual();
        }

        /// <summary>
        /// Override to clear any cached Pens, Brushes, or FormattedText when the active theme changes.
        /// </summary>
        protected virtual void InvalidateCachedBrushes()
        {
        }

        /// <summary>
        /// Override to react to theme changes (e.g., pausing steady-state animations in IndustrialGray).
        /// </summary>
        protected virtual void OnThemeChanged()
        {
        }

        /// <summary>
        /// Returns true if the active theme variant is ISA-101 IndustrialGray.
        /// </summary>
        public bool IsIndustrialGrayTheme
        {
            get
            {
                var variant = ActualThemeVariant;
                if (variant == null || variant == Avalonia.Styling.ThemeVariant.Default)
                    variant = Application.Current?.ActualThemeVariant;
                return string.Equals(variant?.Key?.ToString(), "IndustrialGray", StringComparison.OrdinalIgnoreCase);
            }
        }

        /// <summary>
        /// Returns true when steady-state animations should be suppressed (e.g. ISA-101 IndustrialGray theme).
        /// </summary>
        public bool IsSteadyAnimationSuppressed
        {
            get
            {
                if (this.TryFindResource("HmiSuppressSteadyAnimations", ActualThemeVariant, out var val) && val is bool b)
                    return b;
                return IsIndustrialGrayTheme;
            }
        }

        protected IBrush ResolveThemeBrush(string resourceKey, Color fallbackColor)
        {
            if (this.TryFindResource(resourceKey, ActualThemeVariant, out var res) && res is IBrush brush)
                return brush;
            return new SolidColorBrush(fallbackColor);
        }

        protected Color ResolveThemeColor(string resourceKey, Color fallbackColor)
        {
            if (this.TryFindResource(resourceKey, ActualThemeVariant, out var res))
            {
                if (res is ISolidColorBrush scb) return scb.Color;
                if (res is Color c) return c;
            }
            return fallbackColor;
        }

        /// <summary>
        /// Resolves the semantic fill brush for an equipment state.
        /// In IndustrialGray (ISA-101), normal Running/Stopped states always use muted theme brushes
        /// even if a legacy config specifies bright hex overrides.
        /// </summary>
        protected IBrush ResolveEquipmentStateBrush(
            EquipmentOperationalState state,
            string? activeColorOverride = null,
            string? inactiveColorOverride = null)
        {
            switch (state)
            {
                case EquipmentOperationalState.Fault:
                    return ResolveThemeBrush("HmiFaultBrush", Color.Parse("#DC2626"));
                case EquipmentOperationalState.Transit:
                    return ResolveThemeBrush("HmiTransitBrush", Color.Parse("#0284C7"));
                case EquipmentOperationalState.Offline:
                    return ResolveThemeBrush("HmiOfflineBrush", Color.Parse("#6B7280"));
                case EquipmentOperationalState.Running:
                    if (!IsIndustrialGrayTheme && !string.IsNullOrWhiteSpace(activeColorOverride) && Color.TryParse(activeColorOverride, out var ac))
                        return new SolidColorBrush(ac);
                    return ResolveThemeBrush("HmiRunningFillBrush", IsIndustrialGrayTheme ? Color.Parse("#E8ECEF") : Color.Parse("#22C55E"));
                case EquipmentOperationalState.Stopped:
                default:
                    if (!IsIndustrialGrayTheme && !string.IsNullOrWhiteSpace(inactiveColorOverride) && Color.TryParse(inactiveColorOverride, out var ic))
                        return new SolidColorBrush(ic);
                    return ResolveThemeBrush("HmiStoppedFillBrush", IsIndustrialGrayTheme ? Color.Parse("#AEB5BD") : Color.Parse("#3F3F46"));
            }
        }
    }
}
