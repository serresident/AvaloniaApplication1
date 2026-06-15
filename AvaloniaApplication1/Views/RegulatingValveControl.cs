using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace AvaloniaApplication1.Views
{
    /// <summary>
    /// Custom SCADA-style regulating valve control with 3D volumetric appearance.
    /// Renders a butterfly valve body, actuator stem with hemisphere, and optional feedback bar.
    /// </summary>
    public class RegulatingValveControl : Control
    {
        // --- Dependency Properties ---

        public static readonly StyledProperty<double> SetpointProperty =
            AvaloniaProperty.Register<RegulatingValveControl, double>(nameof(Setpoint), 0.0);

        public static readonly StyledProperty<double> FeedbackProperty =
            AvaloniaProperty.Register<RegulatingValveControl, double>(nameof(Feedback), 0.0);

        public static readonly StyledProperty<bool> HasFeedbackSourceProperty =
            AvaloniaProperty.Register<RegulatingValveControl, bool>(nameof(HasFeedbackSource), true);

        public static readonly StyledProperty<double> ControlWindowWidthProperty =
            AvaloniaProperty.Register<RegulatingValveControl, double>(nameof(ControlWindowWidth), 320.0);

        public static readonly StyledProperty<double> ControlWindowHeightProperty =
            AvaloniaProperty.Register<RegulatingValveControl, double>(nameof(ControlWindowHeight), 280.0);

        public double Setpoint
        {
            get => GetValue(SetpointProperty);
            set => SetValue(SetpointProperty, value);
        }

        public double Feedback
        {
            get => GetValue(FeedbackProperty);
            set => SetValue(FeedbackProperty, value);
        }

        public bool HasFeedbackSource
        {
            get => GetValue(HasFeedbackSourceProperty);
            set => SetValue(HasFeedbackSourceProperty, value);
        }

        public double ControlWindowWidth
        {
            get => GetValue(ControlWindowWidthProperty);
            set => SetValue(ControlWindowWidthProperty, value);
        }

        public double ControlWindowHeight
        {
            get => GetValue(ControlWindowHeightProperty);
            set => SetValue(ControlWindowHeightProperty, value);
        }

        static RegulatingValveControl()
        {
            AffectsRender<RegulatingValveControl>(
                SetpointProperty,
                FeedbackProperty,
                HasFeedbackSourceProperty);
        }

        public RegulatingValveControl()
        {
            ClipToBounds = false;
        }

        public override void Render(DrawingContext context)
        {
            base.Render(context);

            double w = Bounds.Width;
            double h = Bounds.Height;
            if (w < 10 || h < 10) return;

            double setpoint = Math.Clamp(Setpoint, 0, 100);
            double feedback = Math.Clamp(Feedback, 0, 100);

            // Layout: valve takes top portion, feedback bar takes bottom 20px
            double feedbackBarHeight = HasFeedbackSource ? 18.0 : 0.0;
            double feedbackLabelHeight = HasFeedbackSource ? 14.0 : 0.0;
            double valveAreaHeight = h - feedbackBarHeight - feedbackLabelHeight - 4;
            if (valveAreaHeight < 20) valveAreaHeight = 20;

            // Scale valve to fit within valve area, centered
            double valveW = w * 0.85;
            double valveH = valveAreaHeight * 0.55;
            double valveX = (w - valveW) / 2;
            double valveY = valveAreaHeight * 0.45;

            // === 1. Draw Actuator (stem + hemisphere) ===
            DrawActuator(context, w, valveW, valveX, valveY, valveH, valveAreaHeight, setpoint);

            // === 2. Draw Valve Body (butterfly/bowtie) ===
            DrawValveBody(context, valveX, valveY, valveW, valveH, feedback);

            // === 3. Draw Flange plates at pipe connection points ===
            DrawFlanges(context, valveX, valveY, valveW, valveH);

            // === 4. Draw Feedback Bar ===
            if (HasFeedbackSource)
            {
                DrawFeedbackBar(context, w, valveAreaHeight + 4, feedbackBarHeight, feedback);
            }
        }

        private void DrawActuator(DrawingContext context, double totalW, double valveW, double valveX,
            double valveY, double valveH, double valveAreaHeight, double setpoint)
        {
            double centerX = totalW / 2;
            double stemTop = 4;
            double stemBottom = valveY + valveH * 0.5; // meets valve center
            double stemWidth = Math.Max(3, valveW * 0.06);

            // Actuator color based on setpoint (gray→green interpolation)
            var actuatorBrush = CreateCylindricalBrush(
                InterpolateColor(Color.FromRgb(140, 140, 140), Color.FromRgb(40, 180, 60), setpoint / 100.0),
                stemTop, stemBottom, true);

            // Draw stem (vertical bar)
            var stemRect = new Rect(centerX - stemWidth / 2, stemTop + 16, stemWidth, stemBottom - stemTop - 16);
            context.DrawRectangle(actuatorBrush, new Pen(Brushes.Black, 0.5), stemRect);

            // Draw actuator head (hemisphere/dome) at top
            double domeW = Math.Max(14, valveW * 0.3);
            double domeH = Math.Max(10, 16);
            double domeX = centerX;
            double domeY = stemTop + domeH / 2 + 2;

            // Draw dome as ellipse with volumetric gradient
            var domeBrush = CreateRadialDomeBrush(
                InterpolateColor(Color.FromRgb(140, 140, 140), Color.FromRgb(40, 180, 60), setpoint / 100.0),
                domeX, domeY, domeW, domeH);

            context.DrawEllipse(domeBrush, new Pen(new SolidColorBrush(Color.FromRgb(40, 40, 40)), 1.0),
                new Point(domeX, domeY), domeW / 2, domeH / 2);

            // Draw handwheel spokes
            var spokePen = new Pen(new SolidColorBrush(Color.FromRgb(80, 80, 80)), 1.5);
            double spokeRadius = domeW / 2 - 2;
            for (int i = 0; i < 4; i++)
            {
                double angle = i * Math.PI / 4;
                context.DrawLine(spokePen,
                    new Point(domeX + Math.Cos(angle) * spokeRadius * 0.3, domeY + Math.Sin(angle) * spokeRadius * 0.3),
                    new Point(domeX + Math.Cos(angle) * spokeRadius, domeY + Math.Sin(angle) * spokeRadius));
            }
        }

        private void DrawValveBody(DrawingContext context, double x, double y, double w, double h, double feedback)
        {
            // Butterfly/bowtie shape: two triangles meeting at center
            double centerX = x + w / 2;
            double centerY = y + h / 2;
            double topY = y;
            double bottomY = y + h;

            // Body color based on feedback (red→green interpolation)
            Color bodyBaseColor = InterpolateColor(
                Color.FromRgb(200, 40, 40),   // Red (closed)
                Color.FromRgb(40, 180, 60),    // Green (open)
                feedback / 100.0);

            // Left triangle
            var leftGeom = new StreamGeometry();
            using (var ctx = leftGeom.Open())
            {
                ctx.BeginFigure(new Point(x, topY), true);
                ctx.LineTo(new Point(centerX, centerY));
                ctx.LineTo(new Point(x, bottomY));
                ctx.EndFigure(true);
            }

            // Right triangle
            var rightGeom = new StreamGeometry();
            using (var ctx = rightGeom.Open())
            {
                ctx.BeginFigure(new Point(x + w, topY), true);
                ctx.LineTo(new Point(centerX, centerY));
                ctx.LineTo(new Point(x + w, bottomY));
                ctx.EndFigure(true);
            }

            var bodyBrush = CreateCylindricalBrush(bodyBaseColor, topY, bottomY, false);
            var bodyPen = new Pen(new SolidColorBrush(DarkenColor(bodyBaseColor, 0.4)), 1.2);

            context.DrawGeometry(bodyBrush, bodyPen, leftGeom);
            context.DrawGeometry(bodyBrush, bodyPen, rightGeom);

            // Draw center joint circle
            double jointRadius = Math.Max(3, w * 0.06);
            var jointBrush = CreateRadialDomeBrush(
                Color.FromRgb(180, 180, 184), centerX, centerY, jointRadius * 2, jointRadius * 2);
            context.DrawEllipse(jointBrush,
                new Pen(Brushes.Black, 0.8),
                new Point(centerX, centerY), jointRadius, jointRadius);
        }

        private void DrawFlanges(DrawingContext context, double valveX, double valveY, double valveW, double valveH)
        {
            // Left flange plate
            double flangeW = Math.Max(4, valveW * 0.07);
            double flangeH = valveH + 8;
            double flangeY = valveY - 4;

            var flangeBrush = new LinearGradientBrush
            {
                StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
                EndPoint = new RelativePoint(1, 0, RelativeUnit.Relative),
                GradientStops = new GradientStops
                {
                    new GradientStop(Color.FromRgb(120, 120, 124), 0.0),
                    new GradientStop(Color.FromRgb(200, 200, 204), 0.35),
                    new GradientStop(Color.FromRgb(180, 180, 184), 0.65),
                    new GradientStop(Color.FromRgb(100, 100, 104), 1.0)
                }
            };
            var flangePen = new Pen(Brushes.Black, 0.5);

            // Left flange
            context.DrawRectangle(flangeBrush, flangePen,
                new Rect(valveX - flangeW / 2, flangeY, flangeW, flangeH), 1, 1);

            // Right flange
            context.DrawRectangle(flangeBrush, flangePen,
                new Rect(valveX + valveW - flangeW / 2, flangeY, flangeW, flangeH), 1, 1);

            // Flange bolts (small circles)
            var boltBrush = new SolidColorBrush(Color.FromRgb(90, 90, 90));
            double boltR = Math.Max(1.5, flangeW * 0.25);

            // Left flange bolts
            context.DrawEllipse(boltBrush, null, new Point(valveX, flangeY + 4), boltR, boltR);
            context.DrawEllipse(boltBrush, null, new Point(valveX, flangeY + flangeH - 4), boltR, boltR);

            // Right flange bolts
            context.DrawEllipse(boltBrush, null, new Point(valveX + valveW, flangeY + 4), boltR, boltR);
            context.DrawEllipse(boltBrush, null, new Point(valveX + valveW, flangeY + flangeH - 4), boltR, boltR);
        }

        private void DrawFeedbackBar(DrawingContext context, double totalW, double barY, double barH, double feedback)
        {
            double barMargin = totalW * 0.08;
            double barW = totalW - barMargin * 2;
            double barX = barMargin;

            // Background
            var bgRect = new Rect(barX, barY, barW, barH);
            context.DrawRectangle(
                new SolidColorBrush(Color.FromRgb(30, 30, 30)),
                new Pen(new SolidColorBrush(Color.FromRgb(80, 80, 80)), 1),
                bgRect, 3, 3);

            // Fill
            double fillW = barW * (feedback / 100.0);
            if (fillW > 0.5)
            {
                Color fillColor = InterpolateColor(
                    Color.FromRgb(200, 40, 40),
                    Color.FromRgb(40, 180, 60),
                    feedback / 100.0);

                var fillBrush = new LinearGradientBrush
                {
                    StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
                    EndPoint = new RelativePoint(0, 1, RelativeUnit.Relative),
                    GradientStops = new GradientStops
                    {
                        new GradientStop(LightenColor(fillColor, 0.3), 0.0),
                        new GradientStop(fillColor, 0.5),
                        new GradientStop(DarkenColor(fillColor, 0.2), 1.0)
                    }
                };

                var fillRect = new Rect(barX + 1, barY + 1, fillW - 2, barH - 2);
                context.DrawRectangle(fillBrush, null, fillRect, 2, 2);
            }

            // Text label
            var formattedText = new FormattedText(
                $"{feedback:F1} %",
                System.Globalization.CultureInfo.InvariantCulture,
                FlowDirection.LeftToRight,
                new Typeface("Inter", FontStyle.Normal, FontWeight.Bold),
                11,
                Brushes.White);

            double textX = barX + (barW - formattedText.Width) / 2;
            double textY = barY + (barH - formattedText.Height) / 2;
            context.DrawText(formattedText, new Point(textX, textY));
        }

        // --- Gradient helpers ---

        /// <summary>
        /// Creates a cylindrical gradient brush simulating light hitting a rounded metallic surface.
        /// </summary>
        private LinearGradientBrush CreateCylindricalBrush(Color baseColor, double top, double bottom, bool vertical)
        {
            return new LinearGradientBrush
            {
                StartPoint = vertical
                    ? new RelativePoint(0, 0, RelativeUnit.Relative)
                    : new RelativePoint(0, 0, RelativeUnit.Relative),
                EndPoint = vertical
                    ? new RelativePoint(1, 0, RelativeUnit.Relative)
                    : new RelativePoint(0, 1, RelativeUnit.Relative),
                GradientStops = new GradientStops
                {
                    new GradientStop(DarkenColor(baseColor, 0.35), 0.0),     // Dark edge
                    new GradientStop(LightenColor(baseColor, 0.5), 0.3),     // Highlight
                    new GradientStop(baseColor, 0.7),                         // Base
                    new GradientStop(DarkenColor(baseColor, 0.45), 1.0)      // Shadow edge
                }
            };
        }

        /// <summary>
        /// Creates a radial gradient brush for dome/sphere shapes.
        /// </summary>
        private RadialGradientBrush CreateRadialDomeBrush(Color baseColor, double cx, double cy, double w, double h)
        {
            return new RadialGradientBrush
            {
                Center = new RelativePoint(0.4, 0.35, RelativeUnit.Relative),
                GradientOrigin = new RelativePoint(0.35, 0.3, RelativeUnit.Relative),
                RadiusX = new RelativeScalar(0.55, RelativeUnit.Relative),
                RadiusY = new RelativeScalar(0.55, RelativeUnit.Relative),
                GradientStops = new GradientStops
                {
                    new GradientStop(LightenColor(baseColor, 0.7), 0.0),   // Center highlight
                    new GradientStop(LightenColor(baseColor, 0.3), 0.3),   // Near center
                    new GradientStop(baseColor, 0.65),                      // Base color
                    new GradientStop(DarkenColor(baseColor, 0.5), 1.0)     // Outer shadow
                }
            };
        }

        // --- Color utilities ---

        private static Color InterpolateColor(Color from, Color to, double t)
        {
            t = Math.Clamp(t, 0, 1);
            return Color.FromRgb(
                (byte)(from.R + (to.R - from.R) * t),
                (byte)(from.G + (to.G - from.G) * t),
                (byte)(from.B + (to.B - from.B) * t));
        }

        private static Color LightenColor(Color color, double factor)
        {
            return Color.FromRgb(
                (byte)Math.Min(255, color.R + (255 - color.R) * factor),
                (byte)Math.Min(255, color.G + (255 - color.G) * factor),
                (byte)Math.Min(255, color.B + (255 - color.B) * factor));
        }

        private static Color DarkenColor(Color color, double factor)
        {
            return Color.FromRgb(
                (byte)(color.R * (1 - factor)),
                (byte)(color.G * (1 - factor)),
                (byte)(color.B * (1 - factor)));
        }
    }
}
