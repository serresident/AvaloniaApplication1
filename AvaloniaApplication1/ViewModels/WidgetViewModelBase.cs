using System;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using AvaloniaApplication1.Models.Config;
using AvaloniaApplication1.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using ReactiveUI;

namespace AvaloniaApplication1.ViewModels
{
    public partial class WidgetViewModelBase : ViewModelBase, IDisposable
    {
        public readonly IMockDataService DataService;
        protected readonly CompositeDisposable Disposables = new();

        [ObservableProperty]
        private string _type = string.Empty;

        [ObservableProperty]
        private string _title = string.Empty;

        [ObservableProperty]
        private double _row;

        [ObservableProperty]
        private double _col;

        [ObservableProperty]
        private int _sizeX;

        [ObservableProperty]
        private int _sizeY;
        
        [ObservableProperty]
        private DataSourceConfig? _source;

        [ObservableProperty]
        private bool _isSelected;

        public IProjectContextService ProjectContext { get; }

        /// <summary>
        /// Reference to the original config for syncing back changes in Design Mode.
        /// </summary>
        public WidgetConfig OriginalConfig { get; }

        public WidgetViewModelBase(WidgetConfig config, IMockDataService dataService, IProjectContextService projectContext)
        {
            DataService = dataService;
            ProjectContext = projectContext;
            OriginalConfig = config;
            Type = config.Type;
            Title = config.Title;
            Row = config.Position.Row;
            Col = config.Position.Col;
            SizeX = config.Position.SizeX;
            SizeY = config.Position.SizeY;
            Source = config.Source;

            // --- Rx.NET Data Routing (Phase 1) ---
            if (Source != null && !string.IsNullOrEmpty(Source.ConnId) && !string.IsNullOrEmpty(Source.Address))
            {
                DataService.TagUpdates
                    .Where(tag => tag.ConnId == Source.ConnId && tag.Address == Source.Address)
                    .Sample(TimeSpan.FromMilliseconds(100)) // Throttle updates to ~10Hz max per widget
                    .ObserveOn(RxApp.MainThreadScheduler) // Safely marshal to UI thread
                    .Subscribe(tag => OnTagValueUpdated(tag.Value))
                    .DisposeWith(Disposables); // Manage memory
            }
        }

        /// <summary>
        /// Called automatically on the UI thread when the specific tag for this widget is updated.
        /// Derived classes should override this to handle data changes.
        /// </summary>
        /// <param name="newValue">The newly received value from the network/mock.</param>
        protected virtual void OnTagValueUpdated(object newValue)
        {
        }

        public virtual void Dispose()
        {
            Disposables.Dispose(); // Clear all reactive subscriptions
            GC.SuppressFinalize(this);
        }
    }
}