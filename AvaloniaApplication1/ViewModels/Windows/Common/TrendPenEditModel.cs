using System;
using AvaloniaApplication1.Models.Config;
using CommunityToolkit.Mvvm.ComponentModel;

namespace AvaloniaApplication1.ViewModels.Windows.Common
{
    public partial class TrendPenEditModel : ObservableObject
    {
        public string[] AvailableStrokeStyles => new[] { "Solid", "Dash", "Dot" };
        public string[] AvailableInterpolationModes => new[] { "Linear", "Step" };
        public string[] AvailableYAxisBindings => new[] { "Left", "Right", "Individual" };

        public Guid Id { get; set; } = Guid.NewGuid();

        [ObservableProperty]
        private string _name = "Перо";

        [ObservableProperty]
        private string _selectedConnId = string.Empty;

        [ObservableProperty]
        private string _address = string.Empty;

        [ObservableProperty]
        private string _selectedTagId = string.Empty;

        [ObservableProperty]
        private string _colorHex = "#00FFCC";

        [ObservableProperty]
        private double _thickness = 2.0;

        [ObservableProperty]
        private string _strokeStyle = "Solid"; // "Solid", "Dash", "Dot"

        [ObservableProperty]
        private string _interpolationMode = "Linear"; // "Linear", "Step"

        [ObservableProperty]
        private string _yAxisBinding = "Left"; // "Left", "Right", "Individual"

        [ObservableProperty]
        private double _minValue = 0.0;

        [ObservableProperty]
        private double _maxValue = 100.0;

        [ObservableProperty]
        private bool _autoScale = false;

        [ObservableProperty]
        private string _units = string.Empty;

        [ObservableProperty]
        private string _format = "{0:F1}";

        [ObservableProperty]
        private bool _isVisible = true;

        public void ApplyTag(AvaloniaApplication1.Models.TagDefinition? tag)
        {
            if (tag == null) return;
            SelectedTagId = tag.TagId;
            if (!string.IsNullOrEmpty(tag.ConnId)) SelectedConnId = tag.ConnId;
            if (!string.IsNullOrEmpty(tag.Address)) Address = tag.Address;
            if (string.IsNullOrWhiteSpace(Name) || Name.StartsWith("Перо"))
            {
                Name = tag.Name;
            }
            if (!string.IsNullOrEmpty(tag.Unit))
            {
                Units = tag.Unit;
                if (string.IsNullOrWhiteSpace(Format) || Format == "{0:F1}")
                {
                    Format = $"{{0:F1}} {tag.Unit}";
                }
            }
        }

        public TrendPenConfig ToConfig()
        {
            return new TrendPenConfig
            {
                Id = Id,
                Name = Name,
                Source = new DataSourceConfig
                {
                    ConnId = SelectedConnId,
                    Address = Address,
                    TagId = SelectedTagId
                },
                ColorHex = ColorHex,
                Thickness = Thickness,
                StrokeStyle = StrokeStyle,
                InterpolationMode = InterpolationMode,
                YAxisBinding = YAxisBinding,
                MinValue = MinValue,
                MaxValue = MaxValue > MinValue ? MaxValue : MinValue + 1.0,
                AutoScale = AutoScale,
                Units = Units ?? string.Empty,
                Format = string.IsNullOrWhiteSpace(Format) ? "{0:F1}" : Format,
                IsVisible = IsVisible
            };
        }

        public static TrendPenEditModel FromConfig(TrendPenConfig config)
        {
            return new TrendPenEditModel
            {
                Id = config.Id,
                Name = config.Name,
                SelectedConnId = config.Source?.ConnId ?? string.Empty,
                Address = config.Source?.Address ?? string.Empty,
                SelectedTagId = config.Source?.TagId ?? string.Empty,
                ColorHex = string.IsNullOrEmpty(config.ColorHex) ? "#00FFCC" : config.ColorHex,
                Thickness = config.Thickness > 0 ? config.Thickness : 2.0,
                StrokeStyle = string.IsNullOrEmpty(config.StrokeStyle) ? "Solid" : config.StrokeStyle,
                InterpolationMode = string.IsNullOrEmpty(config.InterpolationMode) ? "Linear" : config.InterpolationMode,
                YAxisBinding = string.IsNullOrEmpty(config.YAxisBinding) ? "Left" : config.YAxisBinding,
                MinValue = config.MinValue,
                MaxValue = config.MaxValue,
                AutoScale = config.AutoScale,
                Units = config.Units ?? string.Empty,
                Format = string.IsNullOrEmpty(config.Format) ? "{0:F1}" : config.Format,
                IsVisible = config.IsVisible
            };
        }
    }
}
