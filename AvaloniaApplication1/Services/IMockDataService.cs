using System;

namespace AvaloniaApplication1.Services
{
    public interface IMockDataService
    {
        void StartSimulation();
        void StopSimulation();
        
        // Event fired when a simulated tag value changes
        event EventHandler<(string ConnId, string Address, object Value)>? TagValueChanged;
        
        // Method for UI to "write" a command back (will just loop back in mock)
        void WriteCommand(string connId, string address, object value);
        
        object? GetCurrentValue(string connId, string address);
    }
}