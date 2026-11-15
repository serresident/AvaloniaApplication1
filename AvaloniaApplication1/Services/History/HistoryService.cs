using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Reactive.Linq;
using System.Text.Json;
using Avalonia.Threading;
using AvaloniaApplication1.Models.Config;
using AvaloniaApplication1.Models.History;
using CommunityToolkit.Mvvm.ComponentModel;

namespace AvaloniaApplication1.Services.History
{
    public sealed partial class HistoryService : ObservableObject, IHistoryService
    {
        public const int DefaultCapacity = 30;

        public int Capacity { get; } = DefaultCapacity;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(CanUndo))]
        [NotifyPropertyChangedFor(nameof(CanRedo))]
        [NotifyPropertyChangedFor(nameof(CurrentStep))]
        private int _currentIndex = -1;

        public bool CanUndo => CurrentIndex > 0;

        public bool CanRedo => CurrentIndex >= 0 && CurrentIndex < _steps.Count - 1;

        public HistoryStep? CurrentStep => (CurrentIndex >= 0 && CurrentIndex < _steps.Count) ? _steps[CurrentIndex] : null;

        private readonly ObservableCollection<HistoryStep> _steps = new();
        public ObservableCollection<HistoryStep> Steps => _steps;

        public TimeSpan ExpirationTimeout { get; set; } = TimeSpan.FromMinutes(20);

        private Func<DashboardConfig>? _snapshotProvider;
        private IDisposable? _expirationTimerSubscription;
        private readonly IProjectContextService? _projectContext;

        public event EventHandler? HistoryChanged;
        public event EventHandler<HistoryStep>? StepRestored;

        public HistoryService(IProjectContextService? projectContext = null, int capacity = DefaultCapacity)
        {
            Capacity = capacity > 0 ? capacity : DefaultCapacity;
            _projectContext = projectContext;

            if (_projectContext != null)
            {
                _projectContext.PropertyChanged += OnProjectContextPropertyChanged;
                SetDesignMode(_projectContext.IsDesignMode);
            }
        }

        private void OnProjectContextPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(IProjectContextService.IsDesignMode) && _projectContext != null)
            {
                SetDesignMode(_projectContext.IsDesignMode);
            }
        }

        public void SetSnapshotProvider(Func<DashboardConfig> provider)
        {
            _snapshotProvider = provider;
        }

        public void Initialize(DashboardConfig initialSnapshot, string title = "Исходное состояние", string iconKey = "Home")
        {
            _steps.Clear();
            var clone = CloneDashboardConfig(initialSnapshot);
            var initialStep = new HistoryStep(title, iconKey, clone) { IsCurrent = true };
            _steps.Add(initialStep);
            CurrentIndex = 0;
            NotifyStateChanged();
        }

        public void RecordStep(string title, string iconKey, DashboardConfig? snapshot = null)
        {
            snapshot ??= _snapshotProvider?.Invoke();
            if (snapshot == null) return;

            var newJson = JsonSerializer.Serialize(snapshot);

            // Duplicate guard: don't record if identical to current snapshot
            if (CurrentStep != null)
            {
                var currentJson = JsonSerializer.Serialize(CurrentStep.Snapshot);
                if (string.Equals(currentJson, newJson, StringComparison.Ordinal))
                {
                    return;
                }
            }

            var clonedSnapshot = JsonSerializer.Deserialize<DashboardConfig>(newJson) ?? new DashboardConfig();

            // Timeline branching: truncate all future steps
            if (CurrentIndex >= 0 && CurrentIndex < _steps.Count - 1)
            {
                while (_steps.Count > CurrentIndex + 1)
                {
                    _steps.RemoveAt(_steps.Count - 1);
                }
            }

            // Ring buffer capacity enforcement (FIFO)
            while (_steps.Count >= Capacity)
            {
                _steps.RemoveAt(0);
            }

            var step = new HistoryStep(title, iconKey, clonedSnapshot);
            _steps.Add(step);
            CurrentIndex = _steps.Count - 1;
            UpdateStepSelection();
            NotifyStateChanged();
        }

        public HistoryStep? Undo()
        {
            if (!CanUndo) return null;
            CurrentIndex--;
            UpdateStepSelection();
            var step = _steps[CurrentIndex];
            StepRestored?.Invoke(this, step);
            NotifyStateChanged();
            return step;
        }

        public HistoryStep? Redo()
        {
            if (!CanRedo) return null;
            CurrentIndex++;
            UpdateStepSelection();
            var step = _steps[CurrentIndex];
            StepRestored?.Invoke(this, step);
            NotifyStateChanged();
            return step;
        }

        public HistoryStep? JumpToStep(HistoryStep step)
        {
            if (step == null) return null;
            int index = _steps.IndexOf(step);
            return JumpToIndex(index);
        }

        public HistoryStep? JumpToIndex(int index)
        {
            if (index < 0 || index >= _steps.Count || index == CurrentIndex) return null;
            CurrentIndex = index;
            UpdateStepSelection();
            var step = _steps[CurrentIndex];
            StepRestored?.Invoke(this, step);
            NotifyStateChanged();
            return step;
        }

        public void Clear()
        {
            _steps.Clear();
            CurrentIndex = -1;
            NotifyStateChanged();
        }

        public void SetDesignMode(bool isDesignMode)
        {
            if (isDesignMode)
            {
                _expirationTimerSubscription?.Dispose();
                _expirationTimerSubscription = null;

                // If history buffer was cleared during Runtime inactivity, re-establish baseline snapshot
                if (_steps.Count == 0 && _snapshotProvider != null)
                {
                    var baseline = _snapshotProvider.Invoke();
                    if (baseline != null)
                    {
                        Initialize(baseline, "Исходное состояние", "Home");
                    }
                }
            }
            else
            {
                _expirationTimerSubscription?.Dispose();
                _expirationTimerSubscription = Observable.Timer(ExpirationTimeout)
                    .Subscribe(_ =>
                    {
                        if (Dispatcher.UIThread.CheckAccess())
                        {
                            Clear();
                        }
                        else
                        {
                            Dispatcher.UIThread.Post(() => Clear());
                        }
                    });
            }
        }

        private void UpdateStepSelection()
        {
            for (int i = 0; i < _steps.Count; i++)
            {
                _steps[i].IsCurrent = (i == CurrentIndex);
            }
        }

        private void NotifyStateChanged()
        {
            OnPropertyChanged(nameof(CanUndo));
            OnPropertyChanged(nameof(CanRedo));
            OnPropertyChanged(nameof(CurrentStep));
            OnPropertyChanged(nameof(CurrentIndex));
            HistoryChanged?.Invoke(this, EventArgs.Empty);
        }

        public static DashboardConfig CloneDashboardConfig(DashboardConfig config)
        {
            if (config == null) return new DashboardConfig();
            var json = JsonSerializer.Serialize(config);
            return JsonSerializer.Deserialize<DashboardConfig>(json) ?? new DashboardConfig();
        }

        public void Dispose()
        {
            _expirationTimerSubscription?.Dispose();
            _expirationTimerSubscription = null;
            if (_projectContext != null)
            {
                _projectContext.PropertyChanged -= OnProjectContextPropertyChanged;
            }
        }
    }
}
