using System;

namespace AvaloniaApplication1.Services
{
    public interface IDataCoreService
    {
        void Start();
        void Stop();
        
        // Reactive stream for tag updates
        IObservable<Models.TagData> TagUpdates { get; }
        
        // Method for UI to "write" a command back to the PLC or MQTT
        void WriteCommand(string connId, string address, object value);
        
        object? GetCurrentValue(string connId, string address);
    }
}