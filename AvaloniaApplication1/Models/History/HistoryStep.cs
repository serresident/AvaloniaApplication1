using System;
using AvaloniaApplication1.Models.Config;
using CommunityToolkit.Mvvm.ComponentModel;

namespace AvaloniaApplication1.Models.History
{
    public sealed partial class HistoryStep : ObservableObject
    {
        public Guid Id { get; } = Guid.NewGuid();
        public DateTime Timestamp { get; } = DateTime.Now;
        public string Title { get; }
        public string IconKey { get; }
        public DashboardConfig Snapshot { get; }
        internal string? CachedJson { get; }

        [ObservableProperty]
        private bool _isCurrent;

        public string FormattedTime => Timestamp.ToString("HH:mm:ss");

        public string DisplayIcon => IconKey switch
        {
            "Move" => "✥",
            "Resize" => "⤢",
            "Pipe" => "🚰",
            "Delete" => "🗑️",
            "Add" => "➕",
            "Settings" => "⚙️",
            "Layout" => "📐",
            "Order" => "📑",
            "Home" => "🏠",
            _ => "📝"
        };

        public HistoryStep(string title, string iconKey, DashboardConfig snapshot, DateTime? timestamp = null, Guid? id = null, string? cachedJson = null)
        {
            Title = title ?? "Изменение";
            IconKey = iconKey ?? "Edit";
            Snapshot = snapshot;
            CachedJson = cachedJson;
            if (timestamp.HasValue) Timestamp = timestamp.Value;
            if (id.HasValue) Id = id.Value;
        }
    }
}
