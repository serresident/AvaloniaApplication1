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
        public double Deadband { get; set; } = 0.0;
        public TimeSpan StaleTimeout { get; set; } = TimeSpan.FromSeconds(5);
        public string? Description { get; set; }
        public string? Unit { get; set; }
    }
}
