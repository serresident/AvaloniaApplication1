using System;

namespace AvaloniaApplication1.Services
{
    public interface IMockDataService
    {
        void StartSimulation();
        void StopSimulation();
        void ResetSimulation();
        
        // Reactive stream for tag updates
        IObservable<Models.TagData> TagUpdates { get; }
        
        // Method for UI to "write" a command back (will just loop back in mock)
        void WriteCommand(string connId, string address, object value);
        
        object? GetCurrentValue(string connId, string address);
    }
}