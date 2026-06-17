using System;
using System.Threading;
using System.Threading.Tasks;
using AvaloniaApplication1.Models;
using AvaloniaApplication1.Models.Config;

namespace AvaloniaApplication1.Services.Protocols
{
    public interface IProtocolDriver : IDisposable
    {
        string ConnectionId { get; }
        
        // Starts the polling/subscription loop
        Task StartAsync(CancellationToken cancellationToken);
        
        // Stops the driver gracefully
        Task StopAsync();
        
        // Write command back to PLC (Reverse channel)
        Task WriteAsync(string address, object value, CancellationToken cancellationToken);
        
        // Reactive stream of incoming data
        IObservable<TagData> TagUpdates { get; }
    }
}
