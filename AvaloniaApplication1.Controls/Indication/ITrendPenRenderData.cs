using System.Collections.Generic;

namespace AvaloniaApplication1.Controls.Indication
{
    public interface ITrendPenRenderData
    {
        string Name { get; }
        string ColorHex { get; }
        double Thickness { get; }
        string StrokeStyle { get; } // "Solid", "Dash", "Dot"
        string InterpolationMode { get; } // "Linear", "Step"
        string YAxisBinding { get; } // "Left", "Right", "Individual"
        double MinValue { get; }
        double MaxValue { get; }
        bool AutoScale { get; }
        bool IsVisible { get; }
        IReadOnlyList<double> Values { get; }
    }
}
