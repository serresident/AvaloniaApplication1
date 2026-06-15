using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace AvaloniaApplication1.Views
{
    /// <summary>
    /// Custom SCADA-style valve control.
    /// Renders a bowtie valve body, optional flanges, and multiple actuator types (Solenoid, Diaphragm, Manual, None).
    /// Supports vertical and horizontal orientation, rotation, mismatch alarms, and bottom progress bars.
    /// </summary>
    public class ValveControl : Control
    {
        // --- Dependency Properties ---

        public static readonly StyledProperty<string> ValveTypeProperty =
            AvaloniaProperty.Register<ValveControl, string>(nameof(ValveType), "CutOff");

        public static readonly StyledProperty<bool> IsOpenProperty =
            AvaloniaProperty.Register<ValveControl, bool>(nameof(IsOpen), false);

        public static readonly StyledProperty<double> SetpointProperty =
            AvaloniaProperty.Register<ValveControl, double>(nameof(Setpoint), 0.0);

        public static readonly StyledProperty<double> FeedbackProperty =
            AvaloniaProperty.Register<ValveControl, double>(nameof(Feedback), 0.0);

        public static readonly StyledProperty<bool> HasFeedbackSourceProperty =
            AvaloniaProperty.Register<ValveControl, bool>(nameof(HasFeedbackSource), false);

        public static readonly StyledProperty<bool> IsVerticalProperty =
            AvaloniaProperty.Register<ValveControl, bool>(nameof(IsVertical), false);

        public static readonly StyledProperty<string> ActuatorTypeProperty =
            AvaloniaProperty.Register<ValveControl, string>(nameof(ActuatorType), "Solenoid");

        public static readonly StyledProperty<string> ActiveColorProperty =
            AvaloniaProperty.Register<ValveControl, string>(nameof(ActiveColor), "#00FF00");

        public static readonly StyledProperty<string> InactiveColorProperty =
            AvaloniaProperty.Register<ValveControl, string>(nameof(InactiveColor), "#FF0000");

        public static readonly StyledProperty<bool> ShowFlangesProperty =
            AvaloniaProperty.Register<ValveControl, bool>(nameof(ShowFlanges), true);

        public static readonly StyledProperty<bool> ShowFeedbackBarProperty =
            AvaloniaProperty.Register<ValveControl, bool>(nameof(ShowFeedbackBar), false);

        public static readonly StyledProperty<int> RotationProperty =
            AvaloniaProperty.Register<ValveControl, int>(nameof(Rotation), 0);

        public static readonly StyledProperty<bool> IsAlarmFlashingProperty =
            AvaloniaProperty.Register<ValveControl, bool>(nameof(IsAlarmFlashing), false);

        public static readonly StyledProperty<bool> IsAlarmFlashStateProperty =
            AvaloniaProperty.Register<ValveControl, bool>(nameof(IsAlarmFlashState), false);

        public static readonly StyledProperty<bool> ShowStaticAlarmIconProperty =
            AvaloniaProperty.Register<ValveControl, bool>(nameof(ShowStaticAlarmIcon), false);

        public static readonly StyledProperty<double> ThicknessProperty =
            AvaloniaProperty.Register<ValveControl, double>(nameof(Thickness), 12.0);

        // --- Properties wrappers ---

        public string ValveType
        {
            get => GetValue(ValveTypeProperty);
            set => SetValue(ValveTypeProperty, value);
        }

        public bool IsOpen
        {
            get => GetValue(IsOpenProperty);
            set => SetValue(IsOpenProperty, value);
        }

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

        public bool IsVertical
        {
            get => GetValue(IsVerticalProperty);
            set => SetValue(IsVerticalProperty, value);
        }

        public string ActuatorType
        {
            get => GetValue(ActuatorTypeProperty);
            set => SetValue(ActuatorTypeProperty, value);
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

        public bool ShowFlanges
        {
            get => GetValue(ShowFlangesProperty);
            set => SetValue(ShowFlangesProperty, value);
        }

        public bool ShowFeedbackBar
        {
            get => GetValue(ShowFeedbackBarProperty);
            set => SetValue(ShowFeedbackBarProperty, value);
        }

        public int Rotation
        {
            get => GetValue(RotationProperty);
            set => SetValue(RotationProperty, value);
        }

        public bool IsAlarmFlashing
        {
            get => GetValue(IsAlarmFlashingProperty);
            set => SetValue(IsAlarmFlashingProperty, value);
        }

        public bool IsAlarmFlashState
        {
            get => GetValue(IsAlarmFlashStateProperty);
            set => SetValue(IsAlarmFlashStateProperty, value);
        }

        public bool ShowStaticAlarmIcon
        {
            get => GetValue(ShowStaticAlarmIconProperty);
            set => SetValue(ShowStaticAlarmIconProperty, value);
        }

        public double Thickness
        {
            get => GetValue(ThicknessProperty);
            set => SetValue(ThicknessProperty, value);
        }

        static ValveControl()
        {
            AffectsRender<ValveControl>(
                ValveTypeProperty,
                IsOpenProperty,
                SetpointProperty,
                FeedbackProperty,
                HasFeedbackSourceProperty,
                IsVerticalProperty,
                ActuatorTypeProperty,
                ActiveColorProperty,
                InactiveColorProperty,
                ShowFlangesProperty,
                ShowFeedbackBarProperty,
                RotationProperty,
                IsAlarmFlashingProperty,
                IsAlarmFlashStateProperty,
                ShowStaticAlarmIconProperty,
                ThicknessProperty);
        }

        public ValveControl()
        {
            ClipToBounds = false;
        }

        public override void Render(DrawingContext context)
        {
            base.Render(context);

            double w = Bounds.Width;
            double h = Bounds.Height;
            if (w < 6 || h < 6) return;

            // Center of the control
            var center = new Point(w / 2, h / 2);

            // Compute angle from Rotation (0, 90, 180, 270)
            int finalRotation = Rotation;
            if (finalRotation == 0 && IsVertical)
            {
                finalRotation = 90;
            }
            double angle = finalRotation * Math.PI / 180.0;

            var rotationMatrix = Matrix.CreateTranslation(-center.X, -center.Y) 
                                 * Matrix.CreateRotation(angle) 
                                 * Matrix.CreateTranslation(center.X, center.Y);
            var rotationTransform = context.PushTransform(rotationMatrix);

            // Inside rotated coordinate space, we draw as if it's horizontal.
            // We scale relative to the minimum dimension to ensure it fits beautifully.
            double minSize = Math.Min(w, h);
            double flowSize = minSize * 0.75;  // width of valve bowtie
            double crossSize = minSize * 0.45; // height of valve bowtie

            double cx = w / 2;
            double cy = h / 2;

            // Parse configured colors
            Color activeCol = ParseHexColor(ActiveColor);
            Color inactiveCol = ParseHexColor(InactiveColor);
            
            // Standard HMI dark/light grey for butterflies
            Color greyCol = Color.FromRgb(112, 112, 112); 

            Color bodyColor;
            Color actuatorColor;

            // Determine body and actuator colors based on type and feedback/setpoint
            if (string.Equals(ValveType, "Regulating", StringComparison.OrdinalIgnoreCase))
            {
                // Butterflies (body) depend on Feedback: Grey -> Active Color
                double fbRatio = Math.Clamp(Feedback, 0, 100) / 100.0;
                bodyColor = InterpolateColor(greyCol, activeCol, fbRatio);

                // Actuator (mushroom top) depends on Setpoint: Inactive -> Active Color
                double spRatio = Math.Clamp(Setpoint, 0, 100) / 100.0;
                actuatorColor = InterpolateColor(inactiveCol, activeCol, spRatio);
            }
            else // CutOff / onoff
            {
                // Butterflies depend on feedback (IsOpen): Open = Active, Closed = Grey
                bodyColor = IsOpen ? activeCol : greyCol;

                // Actuator rectangle depends on Setpoint (command): Open (Setpoint > 0) = Active, Closed = Inactive
                actuatorColor = (Setpoint > 0) ? activeCol : inactiveCol;
            }

            // If alarm is flashing and we are in the "on" state, override actuator with red
            if (IsAlarmFlashing && IsAlarmFlashState)
            {
                actuatorColor = Color.FromRgb(255, 30, 30);
            }

            // 1. Draw horizontal pipe connection stubs
            double pipeThickness = Thickness;
            double leftEnd = cx - flowSize / 2 - (ShowFlanges ? (flowSize * 0.06) : 0);
            double rightStart = cx + flowSize / 2 + (ShowFlanges ? (flowSize * 0.06) : 0);
            
            var pipeBrush = new SolidColorBrush(Color.FromRgb(100, 100, 104));
            var pipePen = new Pen(new SolidColorBrush(Color.FromRgb(40, 40, 40)), 1.0);
            
            context.DrawRectangle(pipeBrush, pipePen, new Rect(0, cy - pipeThickness / 2, Math.Max(0, leftEnd), pipeThickness));
            context.DrawRectangle(pipeBrush, pipePen, new Rect(rightStart, cy - pipeThickness / 2, Math.Max(0, w - rightStart), pipeThickness));

            // 2. Draw Actuator (stem + head)
            DrawActuator(context, cx, cy, flowSize, crossSize, actuatorColor);

            // 3. Draw Valve Body (bowtie)
            DrawValveBody(context, cx, cy, flowSize, crossSize, bodyColor);

            // 4. Draw Flanges
            if (ShowFlanges)
            {
                DrawFlanges(context, cx, cy, flowSize, crossSize);
            }

            // 5. Draw Red Triangle in center if cutoff valve is CLOSED
            if (!string.Equals(ValveType, "Regulating", StringComparison.OrdinalIgnoreCase) && !IsOpen)
            {
                double triSize = Math.Max(6.0, flowSize * 0.2);
                var triGeom = new StreamGeometry();
                using (var ctx = triGeom.Open())
                {
                    ctx.BeginFigure(new Point(cx - triSize / 2, cy - triSize / 3), true);
                    ctx.LineTo(new Point(cx + triSize / 2, cy - triSize / 3));
                    ctx.LineTo(new Point(cx, cy + triSize * 2 / 3));
                    ctx.EndFigure(true);
                }
                var triBrush = Brushes.Red;
                var triPen = new Pen(new SolidColorBrush(Color.FromRgb(40, 40, 40)), 1.0);
                context.DrawGeometry(triBrush, triPen, triGeom);
            }

            // Pop rotation
            rotationTransform.Dispose();

            // 6. Draw Feedback Bar outside of the rotation (at the bottom) if enabled
            if (string.Equals(ValveType, "Regulating", StringComparison.OrdinalIgnoreCase) && HasFeedbackSource && ShowFeedbackBar)
            {
                double feedback = Math.Clamp(Feedback, 0, 100);
                DrawFeedbackBar(context, w, h - 16, 14, feedback, activeCol, greyCol);
            }

            // 7. Draw Flashing Alarm Border around final bounds (not rotated)
            if (IsAlarmFlashing && IsAlarmFlashState)
            {
                var borderPen = new Pen(Brushes.Red, 2.5);
                context.DrawRectangle(null, borderPen, new Rect(1, 1, w - 2, h - 2), 4, 4);
            }

            // 8. Draw Static Alarm Icon (🔴) in top-right corner if disabled alarm notifications is check and mismatch is present
            if (ShowStaticAlarmIcon)
            {
                var alarmPen = new Pen(Brushes.Black, 0.8);
                context.DrawEllipse(Brushes.Red, alarmPen, new Point(w - 8, 8), 5, 5);
            }
        }

        private void DrawValveBody(DrawingContext context, double cx, double cy, double w, double h, Color bodyColor)
        {
            double left = cx - w / 2;
            double right = cx + w / 2;
            double top = cy - h / 2;
            double bottom = cy + h / 2;

            // Left triangle
            var leftGeom = new StreamGeometry();
            using (var ctx = leftGeom.Open())
            {
                ctx.BeginFigure(new Point(left, top), true);
                ctx.LineTo(new Point(cx, cy));
                ctx.LineTo(new Point(left, bottom));
                ctx.EndFigure(true);
            }

            // Right triangle
            var rightGeom = new StreamGeometry();
            using (var ctx = rightGeom.Open())
            {
                ctx.BeginFigure(new Point(right, top), true);
                ctx.LineTo(new Point(cx, cy));
                ctx.LineTo(new Point(right, bottom));
                ctx.EndFigure(true);
            }

            // Brush & Pen
            var fillBrush = new SolidColorBrush(bodyColor);
            var borderPen = new Pen(new SolidColorBrush(Color.FromRgb(40, 40, 40)), 1.2);

            context.DrawGeometry(fillBrush, borderPen, leftGeom);
            context.DrawGeometry(fillBrush, borderPen, rightGeom);

            // Center joint circle
            double jointRadius = Math.Max(2.5, w * 0.08);
            context.DrawEllipse(Brushes.LightGray, borderPen, new Point(cx, cy), jointRadius, jointRadius);
        }

        private void DrawActuator(DrawingContext context, double cx, double cy, double flowSize, double crossSize, Color actuatorColor)
        {
            if (string.Equals(ActuatorType, "None", StringComparison.OrdinalIgnoreCase))
                return;

            double stemThickness = Math.Max(1.5, flowSize * 0.05);
            var stemPen = new Pen(new SolidColorBrush(Color.FromRgb(60, 60, 60)), stemThickness);

            // Stem goes up (towards top border)
            double stemTop = cy - flowSize * 0.5; // end of stem
            
            // Draw stem line
            context.DrawLine(stemPen, new Point(cx, cy), new Point(cx, stemTop));

            // Draw actuator head at stemTop
            double headSize = flowSize * 0.35;
            if (headSize < 10) headSize = 10;

            if (string.Equals(ActuatorType, "Solenoid", StringComparison.OrdinalIgnoreCase))
            {
                // Box (Solenoid/Electric)
                double rectW = headSize;
                double rectH = headSize;
                var rect = new Rect(cx - rectW / 2, stemTop - rectH, rectW, rectH);
                
                var boxBrush = new SolidColorBrush(actuatorColor); 
                var boxPen = new Pen(new SolidColorBrush(Color.FromRgb(40, 40, 40)), 1.0);
                context.DrawRectangle(boxBrush, boxPen, rect, 1, 1);

                // Draw letter inside (P for Regulating, S for CutOff)
                string letter = string.Equals(ValveType, "Regulating", StringComparison.OrdinalIgnoreCase) ? "P" : "S";
                var text = new FormattedText(
                    letter,
                    System.Globalization.CultureInfo.InvariantCulture,
                    FlowDirection.LeftToRight,
                    new Typeface("Inter", FontStyle.Normal, FontWeight.Bold),
                    headSize * 0.75,
                    Brushes.Black);

                context.DrawText(text, new Point(cx - text.Width / 2, stemTop - rectH + (rectH - text.Height) / 2));
            }
            else if (string.Equals(ActuatorType, "Diaphragm", StringComparison.OrdinalIgnoreCase))
            {
                // Dome / Semicircle (Pneumatic diaphragm)
                double domeW = headSize * 1.3;
                double domeH = headSize * 0.6;
                double plateThickness = Math.Max(1.5, flowSize * 0.04);

                // Draw flat horizontal plate at stemTop
                var platePen = new Pen(new SolidColorBrush(Color.FromRgb(60, 60, 60)), plateThickness);
                context.DrawLine(platePen, new Point(cx - domeW / 2, stemTop), new Point(cx + domeW / 2, stemTop));

                // Draw dome (semicircle) on top of the plate
                var domeGeom = new StreamGeometry();
                using (var ctx = domeGeom.Open())
                {
                    ctx.BeginFigure(new Point(cx - domeW / 2, stemTop), true);
                    ctx.ArcTo(new Point(cx + domeW / 2, stemTop), new Size(domeW / 2, domeH), 0, false, SweepDirection.Clockwise, true);
                    ctx.EndFigure(true);
                }

                var domeBrush = new SolidColorBrush(actuatorColor);
                var domePen = new Pen(new SolidColorBrush(Color.FromRgb(40, 40, 40)), 1.0);
                context.DrawGeometry(domeBrush, domePen, domeGeom);
            }
            else if (string.Equals(ActuatorType, "Manual", StringComparison.OrdinalIgnoreCase))
            {
                // Manual T-bar / Handwheel
                double wheelW = headSize * 1.1;
                double plateThickness = Math.Max(2.0, flowSize * 0.05);

                var wheelPen = new Pen(new SolidColorBrush(actuatorColor), plateThickness);
                context.DrawLine(wheelPen, new Point(cx - wheelW / 2, stemTop), new Point(cx + wheelW / 2, stemTop));
                context.DrawLine(wheelPen, new Point(cx - wheelW / 2, stemTop - 2), new Point(cx - wheelW / 2, stemTop + 2));
                context.DrawLine(wheelPen, new Point(cx + wheelW / 2, stemTop - 2), new Point(cx + wheelW / 2, stemTop + 2));
            }
        }

        private void DrawFlanges(DrawingContext context, double cx, double cy, double w, double h)
        {
            double left = cx - w / 2;
            double right = cx + w / 2;
            double flangeW = Math.Max(2.0, w * 0.06);
            double flangeH = h * 1.1;

            var flangeBrush = new SolidColorBrush(Color.FromRgb(160, 160, 164));
            var flangePen = new Pen(new SolidColorBrush(Color.FromRgb(40, 40, 40)), 0.6);

            // Left Flange
            context.DrawRectangle(flangeBrush, flangePen, new Rect(left - flangeW / 2, cy - flangeH / 2, flangeW, flangeH), 0.5, 0.5);

            // Right Flange
            context.DrawRectangle(flangeBrush, flangePen, new Rect(right - flangeW / 2, cy - flangeH / 2, flangeW, flangeH), 0.5, 0.5);
        }

        private void DrawFeedbackBar(DrawingContext context, double totalW, double barY, double barH, double feedback, Color activeCol, Color inactiveCol)
        {
            double barMargin = totalW * 0.08;
            double barW = totalW - barMargin * 2;
            double barX = barMargin;

            // Background
            var bgRect = new Rect(barX, barY, barW, barH);
            context.DrawRectangle(
                new SolidColorBrush(Color.FromRgb(30, 30, 30)),
                new Pen(new SolidColorBrush(Color.FromRgb(80, 80, 80)), 1),
                bgRect, 2, 2);

            // Fill
            double fillW = barW * (feedback / 100.0);
            if (fillW > 0.5)
            {
                Color fillColor = InterpolateColor(inactiveCol, activeCol, feedback / 100.0);
                var fillBrush = new SolidColorBrush(fillColor);
                var fillRect = new Rect(barX + 1, barY + 1, fillW - 2, barH - 2);
                context.DrawRectangle(fillBrush, null, fillRect, 1, 1);
            }

            // Text label
            var formattedText = new FormattedText(
                $"{feedback:F0}%",
                System.Globalization.CultureInfo.InvariantCulture,
                FlowDirection.LeftToRight,
                new Typeface("Inter", FontStyle.Normal, FontWeight.Bold),
                barH * 0.8,
                Brushes.White);

            double textX = barX + (barW - formattedText.Width) / 2;
            double textY = barY + (barH - formattedText.Height) / 2;
            context.DrawText(formattedText, new Point(textX, textY));
        }

        // --- Color Helper Utilities ---

        private static Color ParseHexColor(string hex)
        {
            if (string.IsNullOrEmpty(hex)) return Color.FromRgb(128, 128, 128);
            try
            {
                hex = hex.Trim().TrimStart('#');
                if (hex.Length == 6)
                {
                    return Color.FromRgb(
                        Convert.ToByte(hex.Substring(0, 2), 16),
                        Convert.ToByte(hex.Substring(2, 2), 16),
                        Convert.ToByte(hex.Substring(4, 2), 16));
                }
                if (hex.Length == 8)
                {
                    return Color.FromArgb(
                        Convert.ToByte(hex.Substring(0, 2), 16),
                        Convert.ToByte(hex.Substring(2, 2), 16),
                        Convert.ToByte(hex.Substring(4, 2), 16),
                        Convert.ToByte(hex.Substring(6, 2), 16));
                }
            }
            catch
            {
                // Fallback
            }
            return Color.FromRgb(128, 128, 128);
        }

        private static Color InterpolateColor(Color from, Color to, double t)
        {
            t = Math.Clamp(t, 0, 1);
            return Color.FromRgb(
                (byte)(from.R + (to.R - from.R) * t),
                (byte)(from.G + (to.G - from.G) * t),
                (byte)(from.B + (to.B - from.B) * t));
        }
    }
}
