using System;

using System.Threading.Tasks;

namespace AvaloniaApplication1.Services
{
    public interface IDataCoreService
    {
        Task StartAsync();
        Task StopAsync();
        
        // Publish external tags to the core bus (e.g. from simulation)
        void PublishTag(Models.TagData tag);
        
        // Reactive stream for tag updates
        IObservable<Models.TagData> TagUpdates { get; }
        
        // Method for UI to "write" a command back to the PLC or MQTT
        void WriteCommand(string connId, string address, object value);
        
        object? GetCurrentValue(string connId, string address);
    }
}