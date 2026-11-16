using System;

namespace AvaloniaApplication1.Models.Config
{
    public class TrendPenConfig
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string Name { get; set; } = "Перо";
        public DataSourceConfig Source { get; set; } = new();
        public string ColorHex { get; set; } = "#00FFCC";
        public double Thickness { get; set; } = 2.0;
        public string StrokeStyle { get; set; } = "Solid"; // "Solid", "Dash", "Dot"
        public string InterpolationMode { get; set; } = "Linear"; // "Linear", "Step"
        public string YAxisBinding { get; set; } = "Left"; // "Left", "Right", "Individual"
        public double MinValue { get; set; } = 0.0;
        public double MaxValue { get; set; } = 100.0;
        public bool AutoScale { get; set; } = false; // Full Dynamic mode
        public string Units { get; set; } = string.Empty;
        public string Format { get; set; } = "{0:F1}";
        public bool IsVisible { get; set; } = true;

        public TrendPenConfig Clone()
        {
            return new TrendPenConfig
            {
                Id = this.Id,
                Name = this.Name,
                Source = new DataSourceConfig
                {
                    ConnId = this.Source.ConnId,
                    Address = this.Source.Address,
                    DataType = this.Source.DataType,
                    TagId = this.Source.TagId
                },
                ColorHex = this.ColorHex,
                Thickness = this.Thickness,
                StrokeStyle = this.StrokeStyle,
                InterpolationMode = this.InterpolationMode,
                YAxisBinding = this.YAxisBinding,
                MinValue = this.MinValue,
                MaxValue = this.MaxValue,
                AutoScale = this.AutoScale,
                Units = this.Units,
                Format = this.Format,
                IsVisible = this.IsVisible
            };
        }
    }
}
