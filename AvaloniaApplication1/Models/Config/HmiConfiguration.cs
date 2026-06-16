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
        
        // ModbusTCP, ModbusRTUOverTCP, MQTT
        public string Type { get; set; } = string.Empty; 
        
        public string Host { get; set; } = "127.0.0.1";
        public int Port { get; set; } = 502;
        public int PollIntervalMs { get; set; } = 500;
        
        // ABCD, CDAB, BADC, DCBA
        public string ByteOrder { get; set; } = "ABCD"; 
    }

    public class BridgeRuleConfig
    {
        public string Name { get; set; } = string.Empty;
        public BridgeEndpoint Source { get; set; } = new();
        public BridgeEndpoint Destination { get; set; } = new();
        
        // BiDir, SrcToDst
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

    public class WidgetConfig
    {
        // ValueDisplay, PilotLight, ContainerButton, CommandButton, Slider, RealTimeTrend, Pipe, Valve, Tank, Pump
        public string Type { get; set; } = string.Empty; 
        public string Title { get; set; } = string.Empty;
        public DataSourceConfig Source { get; set; } = new();
        public WidgetPosition Position { get; set; } = new();
        
        // Only for ContainerButton
        public List<WidgetConfig> Children { get; set; } = new(); 

        // ValueDisplay: display format string, e.g. "{0:F2} °C"
        public string Format { get; set; } = "{0}";

        // CommandButton: Toggle | Momentary | SetValue
        public string ButtonMode { get; set; } = "Toggle";

        // Slider: range limits
        public double MinValue { get; set; } = 0;
        public double MaxValue { get; set; } = 100;

        // PilotLight: custom colors
        public string TrueColor { get; set; } = "#00FF00";
        public string FalseColor { get; set; } = "#440000";

        // Pipe: custom segments points, e.g. "0,0;100,0;100,50"
        public string PipePoints { get; set; } = string.Empty;

        // Colors for pipes and valves
        public string ActiveColor { get; set; } = string.Empty;
        public string InactiveColor { get; set; } = string.Empty;

        // Valve: CutOff | Regulating
        public string ValveType { get; set; } = "CutOff";

        // Flange rendering option
        public bool ShowFlanges { get; set; } = true;

        // Pipe settings
        public double Thickness { get; set; } = 12.0;
        public string StartFitting { get; set; } = "None";
        public string EndFitting { get; set; } = "None";

        // Feedback source for regulating valves (separate from main Source)
        public DataSourceConfig? FeedbackSource { get; set; }

        // Control window dimensions (for valve/pump MDI popup)
        public double ControlWindowWidth { get; set; } = 0;
        public double ControlWindowHeight { get; set; } = 0;

        // Valve properties
        public bool IsVertical { get; set; } = false;
        public string ActuatorType { get; set; } = "Solenoid";
        public int Rotation { get; set; } = 0;
        public bool AlarmDisabled { get; set; } = false;
        public double Tolerance { get; set; } = 10.0;
        public DataSourceConfig? ModeSource { get; set; }
        
        // ValueDisplay: custom color and font size
        public string ValueColor { get; set; } = string.Empty;
        public double ValueFontSize { get; set; } = 0;
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
