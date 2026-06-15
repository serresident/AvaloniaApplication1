using System;

namespace AvaloniaApplication1.Services
{
    public interface IDataCoreService
    {
        void Start();
        void Stop();
        
        // Event fired when a tag value changes (either from Modbus, MQTT, or Mock)
        event EventHandler<(string ConnId, string Address, object Value)>? TagValueChanged;
        
        // Method for UI to "write" a command back to the PLC or MQTT
        void WriteCommand(string connId, string address, object value);
        
        object? GetCurrentValue(string connId, string address);
    }
}