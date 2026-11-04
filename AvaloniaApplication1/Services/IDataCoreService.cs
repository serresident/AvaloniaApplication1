using System;
using System.Threading.Tasks;
using AvaloniaApplication1.Models;

namespace AvaloniaApplication1.Services
{
    public interface IDataCoreService : IAsyncDisposable
    {
        Task StartAsync();
        Task StopAsync();
        
        ITagManagerService TagManager { get; }

        // Publish external tags to the core bus (e.g. from simulation or protocol drivers)
        void PublishTag(TagData tag);
        
        // Reactive stream for tag updates (raw TagData)
        IObservable<TagData> TagUpdates { get; }

        // Reactive stream for snapshots with Quality & Override status
        IObservable<TagSnapshot> SnapshotUpdates { get; }
        
        // Method for UI to "write" a command back to the PLC or MQTT
        void WriteCommand(string connId, string address, object value);
        
        object? GetCurrentValue(string connId, string address);
        
        TagSnapshot GetTagSnapshot(string tagId);
        TagSnapshot GetTagSnapshot(string connId, string address);

        void ForceOverride(string tagId, object value);
        void ClearOverride(string tagId);
    }
}