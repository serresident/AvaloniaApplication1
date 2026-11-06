using System;

namespace AvaloniaApplication1.Models
{
    /// <summary>
    /// Metadata definition for a centralized SCADA tag in the TagManager engine.
    /// </summary>
    public class TagDefinition
    {
        public string TagId { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string ConnId { get; set; } = string.Empty;
        public string Address { get; set; } = string.Empty;
        public string DataType { get; set; } = "Float32";
        public double ScaleA { get; set; } = 1.0;
        public double ScaleB { get; set; } = 0.0;
        public double Deadband { get; set; } = 0.0;
        public TimeSpan StaleTimeout { get; set; } = TimeSpan.FromSeconds(5);
        public string? Description { get; set; }
        public string? Unit { get; set; }

        [System.Text.Json.Serialization.JsonIgnore]
        public string ScalingDisplay => (Math.Abs(ScaleA - 1.0) > 0.0001 || Math.Abs(ScaleB) > 0.0001)
            ? $"{ScaleA.ToString(System.Globalization.CultureInfo.InvariantCulture)}*X + {ScaleB.ToString(System.Globalization.CultureInfo.InvariantCulture)}"
            : "1.0*X";

        public TagDefinition Clone()
        {
            return new TagDefinition
            {
                TagId = TagId,
                Name = Name,
                ConnId = ConnId,
                Address = Address,
                DataType = DataType,
                ScaleA = ScaleA,
                ScaleB = ScaleB,
                Deadband = Deadband,
                StaleTimeout = StaleTimeout,
                Description = Description,
                Unit = Unit
            };
        }
    }
}
