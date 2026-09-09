using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace AvaloniaApplication1.Models.Config
{
    public class HmiConfiguration
    {
        public ProjectConfig Project { get; set; } = new();
        public List<ConnectionConfig> Connections { get; set; } = new();
        public List<BridgeRuleConfig> BridgeRules { get; set; } = new();
        public DashboardConfig Dashboard { get; set; } = new();
        public DashboardConfig Mimic { get; set; } = new();
    }

    public class ProjectConfig
    {
        public string Name { get; set; } = string.Empty;
        public string Version { get; set; } = "1.0.0";
    }

    public class ConnectionConfig
    {
        public string Id { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty; 
        public string Host { get; set; } = "127.0.0.1";
        public int Port { get; set; } = 502;
        public int PollIntervalMs { get; set; } = 500;
        public string ByteOrder { get; set; } = "ABCD"; 
    }

    public class BridgeRuleConfig
    {
        public string Name { get; set; } = string.Empty;
        public BridgeEndpoint Source { get; set; } = new();
        public BridgeEndpoint Destination { get; set; } = new();
        public string Direction { get; set; } = "SrcToDst"; 
    }

    public class BridgeEndpoint
    {
        public string ConnId { get; set; } = string.Empty;
        public string Address { get; set; } = string.Empty;
    }

    public class DashboardConfig
    {
        public int CellSize { get; set; } = 160;
        public List<WidgetConfig> Widgets { get; set; } = new();
    }

    [JsonConverter(typeof(WidgetJsonConverter))]
    public abstract class WidgetConfig
    {
        public string Type { get; set; } = string.Empty; 
        public string Title { get; set; } = string.Empty;
        public DataSourceConfig Source { get; set; } = new();
        public WidgetPosition Position { get; set; } = new();
    }

    // Generic fallback for unknown widgets
    public class WidgetConfigBase : WidgetConfig { }

    public class ValueDisplayConfig : WidgetConfig
    {
        public string Format { get; set; } = "{0}";
        public string ValueColor { get; set; } = string.Empty;
        public double ValueFontSize { get; set; } = 0;
    }

    public class PilotLightConfig : WidgetConfig
    {
        public string TrueColor { get; set; } = "#00FF00";
        public string FalseColor { get; set; } = "#440000";
    }

    public class ContainerButtonConfig : WidgetConfig
    {
        public List<WidgetConfig> Children { get; set; } = new();
    }

    public class CommandButtonConfig : WidgetConfig
    {
        // Toggle | Momentary | SetValue
        public string ButtonMode { get; set; } = "Toggle";
    }

    public class SliderConfig : WidgetConfig
    {
        public double MinValue { get; set; } = 0;
        public double MaxValue { get; set; } = 100;
    }

    public class SetValueConfig : WidgetConfig
    {
        public string Format { get; set; } = "{0}";
        public double MinValue { get; set; } = 0;
        public double MaxValue { get; set; } = 100;
        public string ValueColor { get; set; } = string.Empty;
        public double ValueFontSize { get; set; } = 0;
    }

    public class RealTimeTrendConfig : WidgetConfig
    {
        public string Format { get; set; } = "{0}";
        public double MinValue { get; set; } = 0;
        public double MaxValue { get; set; } = 100;
    }

    public class PipeConfig : WidgetConfig
    {
        public string PipePoints { get; set; } = string.Empty;
        public string ActiveColor { get; set; } = string.Empty;
        public string InactiveColor { get; set; } = string.Empty;
        public bool ShowFlanges { get; set; } = true;
        public double Thickness { get; set; } = 12.0;
        public string StartFitting { get; set; } = "None";
        public string EndFitting { get; set; } = "None";
    }

    public class ValveConfig : WidgetConfig
    {
        public string ValveType { get; set; } = "CutOff";
        public string ActiveColor { get; set; } = string.Empty;
        public string InactiveColor { get; set; } = string.Empty;
        public DataSourceConfig? FeedbackSource { get; set; }
        public bool IsVertical { get; set; } = false;
        public string ActuatorType { get; set; } = "Solenoid";
        public int Rotation { get; set; } = 0;
        public bool AlarmDisabled { get; set; } = false;
        public double Tolerance { get; set; } = 10.0;
        public DataSourceConfig? ModeSource { get; set; }
    }

    public class TankConfig : WidgetConfig
    {
        public string Format { get; set; } = "{0}";
        public double MinValue { get; set; } = 0;
        public double MaxValue { get; set; } = 100;
        public string ValueColor { get; set; } = string.Empty;
        public double ValueFontSize { get; set; } = 0;
    }

    public class PumpConfig : WidgetConfig
    {
        public string ActiveColor { get; set; } = string.Empty;
        public string InactiveColor { get; set; } = string.Empty;
        public double ControlWindowWidth { get; set; } = 0;
        public double ControlWindowHeight { get; set; } = 0;
    }

    public class HeatExchangerConfig : WidgetConfig
    {
        public string ExchangerType { get; set; } = "ShellAndTube"; // ShellAndTube | Plate
        public DataSourceConfig? SecondarySource { get; set; }
        public string Format { get; set; } = "{0:F1} °C";
        public string ActiveColor { get; set; } = "#00FFCC";
        public string InactiveColor { get; set; } = "#777777";
        public bool ShowFlanges { get; set; } = true;
    }

    public class ReactorConfig : WidgetConfig
    {
        public DataSourceConfig? AgitatorSource { get; set; }
        public DataSourceConfig? TempSource { get; set; }
        public double MinValue { get; set; } = 0;
        public double MaxValue { get; set; } = 100;
        public string Format { get; set; } = "{0:F1} %";
        public bool HasJacket { get; set; } = true;
        public string ActiveColor { get; set; } = "#00FF00";
        public string InactiveColor { get; set; } = "#555555";
    }

    public class LevelSensorConfig : WidgetConfig
    {
        public string SensorType { get; set; } = "Radar"; // Radar | Ultrasonic | Hydrostatic
        public string TagNumber { get; set; } = "LT-101";
        public string Unit { get; set; } = "%";
        public string Format { get; set; } = "{0:F1}";
        public double MinValue { get; set; } = 0;
        public double MaxValue { get; set; } = 100;
        public double AlarmHigh { get; set; } = 90;
        public double AlarmLow { get; set; } = 10;
        public string ValueColor { get; set; } = "#FFFFFF";
    }

    public class DataSourceConfig
    {
        public string ConnId { get; set; } = string.Empty;
        public string Address { get; set; } = string.Empty;
        public string DataType { get; set; } = string.Empty;
    }

    public class WidgetPosition
    {
        public double Row { get; set; } = 0;
        public double Col { get; set; } = 0;
        public int SizeX { get; set; } = 1;
        public int SizeY { get; set; } = 1;
    }
}
