namespace AvaloniaApplication1.Models.Config
{
    public static class ConnectionTypes
    {
        public static readonly string[] All = { "ModbusTCP", "ModbusRTUOverTCP", "MQTT" };
    }

    public static class ByteOrders
    {
        public static readonly string[] All = { "ABCD", "CDAB", "BADC", "DCBA" };
    }

    public static class WidgetTypes
    {
        public static readonly string[] All = { "ValueDisplay", "PilotLight", "CommandButton", "Slider", "ContainerButton", "SetValue", "RealTimeTrend", "Pipe", "Valve", "Tank", "Pump" };
    }

    public static class DataTypes
    {
        public static readonly string[] All = { "Bool", "Int16", "UInt16", "Int32", "UInt32", "Float32", "Double" };
    }

    public static class ButtonModes
    {
        public static readonly string[] All = { "Toggle", "Momentary", "SetValue" };
    }
}
