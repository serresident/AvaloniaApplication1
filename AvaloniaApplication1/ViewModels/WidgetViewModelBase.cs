using AvaloniaApplication1.Models.Config;
using AvaloniaApplication1.Services;
using CommunityToolkit.Mvvm.ComponentModel;

namespace AvaloniaApplication1.ViewModels
{
    public partial class WidgetViewModelBase : ViewModelBase
    {
        public readonly IMockDataService DataService;

        [ObservableProperty]
        private string _type = string.Empty;

        [ObservableProperty]
        private string _title = string.Empty;

        [ObservableProperty]
        private int _row;

        [ObservableProperty]
        private int _col;

        [ObservableProperty]
        private int _sizeX;

        [ObservableProperty]
        private int _sizeY;
        
        [ObservableProperty]
        private DataSourceConfig? _source;

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
        }

        public virtual void Dispose()
        {
            // Clean up event subscriptions in derived classes
        }
    }
}