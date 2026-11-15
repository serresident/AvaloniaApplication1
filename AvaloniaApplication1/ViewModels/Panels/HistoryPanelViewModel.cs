using AvaloniaApplication1.Models.History;
using AvaloniaApplication1.Services.History;
using AvaloniaApplication1.ViewModels.Core;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;

namespace AvaloniaApplication1.ViewModels.Panels
{
    public partial class HistoryPanelViewModel : ViewModelBase
    {
        public IHistoryService History { get; }
        private readonly DashboardViewModel _dashboard;

        public ObservableCollection<HistoryStep> Steps => History.Steps;

        public bool CanUndo => History.CanUndo;
        public bool CanRedo => History.CanRedo;

        public HistoryStep? CurrentStep => History.CurrentStep;

        public string SummaryText => $"{History.Steps.Count} / {History.Capacity}";
        public bool IsOpen => _dashboard.IsHistoryPanelOpen;

        public HistoryPanelViewModel(IHistoryService history, DashboardViewModel dashboard)
        {
            History = history;
            _dashboard = dashboard;

            History.HistoryChanged += (s, e) =>
            {
                OnPropertyChanged(nameof(CanUndo));
                OnPropertyChanged(nameof(CanRedo));
                OnPropertyChanged(nameof(CurrentStep));
                OnPropertyChanged(nameof(SummaryText));
            };

            _dashboard.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(DashboardViewModel.IsHistoryPanelOpen))
                {
                    OnPropertyChanged(nameof(IsOpen));
                }
            };
        }

        [RelayCommand]
        public void Undo() => _dashboard.Undo();

        [RelayCommand]
        public void Redo() => _dashboard.Redo();

        [RelayCommand]
        public void JumpToStep(HistoryStep step) => _dashboard.JumpToStep(step);

        [RelayCommand]
        public void Close() => _dashboard.CloseHistoryPanel();

        [RelayCommand]
        public void Clear() => _dashboard.ClearHistory();
    }
}
