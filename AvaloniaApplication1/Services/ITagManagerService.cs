using System;
using System.Collections.Generic;
using AvaloniaApplication1.Models;

namespace AvaloniaApplication1.Services
{
    public interface ITagManagerService : IDisposable
    {
        void Initialize(IEnumerable<TagDefinition> tags);
        void RegisterTag(TagDefinition tag);
        TagDefinition? GetTagDefinition(string tagId);
        TagDefinition? FindTagByAddress(string connId, string address);
        
        TagSnapshot GetSnapshot(string tagId);
        TagSnapshot GetSnapshotByAddress(string connId, string address);
        
        IObservable<TagSnapshot> SnapshotUpdates { get; }
        
        void UpdateTagValue(string connId, string address, object value, TagQuality quality = TagQuality.Good, string? errorMessage = null);
        void UpdateTagValue(string tagId, object value, TagQuality quality = TagQuality.Good, string? errorMessage = null);
        
        // Manual override / force
        void ForceOverride(string tagId, object value);
        void ClearOverride(string tagId);
        
        // Watchdog stale checking
        void CheckStaleTags();
        
        IReadOnlyCollection<TagDefinition> AllTags { get; }
    }
}
