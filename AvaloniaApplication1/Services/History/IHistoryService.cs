using System;
using System.Collections.ObjectModel;
using AvaloniaApplication1.Models.Config;
using AvaloniaApplication1.Models.History;

namespace AvaloniaApplication1.Services.History
{
    public interface IHistoryService : IDisposable
    {
        int Capacity { get; }
        int CurrentIndex { get; }
        bool CanUndo { get; }
        bool CanRedo { get; }
        ObservableCollection<HistoryStep> Steps { get; }
        HistoryStep? CurrentStep { get; }
        TimeSpan ExpirationTimeout { get; set; }

        event EventHandler? HistoryChanged;
        event EventHandler<HistoryStep>? StepRestored;

        void Initialize(DashboardConfig initialSnapshot, string title = "Исходное состояние", string iconKey = "Home");
        void SetSnapshotProvider(Func<DashboardConfig> provider);
        void RecordStep(string title, string iconKey, DashboardConfig? snapshot = null);
        HistoryStep? Undo();
        HistoryStep? Redo();
        HistoryStep? JumpToStep(HistoryStep step);
        HistoryStep? JumpToIndex(int index);
        void Clear();
        void SetDesignMode(bool isDesignMode);
    }
}
