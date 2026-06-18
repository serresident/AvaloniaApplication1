using System;
using System.Threading.Tasks;
using System.Collections.ObjectModel;
using Avalonia.Threading;
using AvaloniaApplication1.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using AvaloniaApplication1.Models.Config;
using Microsoft.Extensions.DependencyInjection;

namespace AvaloniaApplication1.ViewModels
{
    public partial class MainViewModel : ViewModelBase, IDisposable, IChildWindowService
    {
        #region Constants
        private const int LegacyCellSizeLarge  = 160;
        private const int LegacyCellSizeSmall  = 20;
        private const int DefaultMimicCellSize = 10;
        #endregion

        private readonly IConfigurationService _configurationService;
        private readonly IDataCoreService _dataCoreService;
        private readonly ISimulationService _simulationService;
        private readonly IDialogService _dialogService;
        private readonly IWidgetFactory _widgetFactory;
        private HmiConfiguration? _currentConfig;

        public IProjectContextService ProjectContext { get; }

        [ObservableProperty]
        private DashboardViewModel? _dashboard;

        [ObservableProperty]
        private bool _isMimicActive;

        [ObservableProperty]
        private string? _toastMessage;

        [ObservableProperty]
        private bool _isToastVisible;

        private DispatcherTimer? _toastTimer;

        public void ShowToast(string message)
        {
            Dispatcher.UIThread.Post(() =>
            {
                ToastMessage = message;
                IsToastVisible = true;

                _toastTimer?.Stop();
                _toastTimer = new DispatcherTimer
                {
                    Interval = TimeSpan.FromSeconds(4)
                };
                _toastTimer.Tick += (s, e) =>
                {
                    IsToastVisible = false;
                    _toastTimer.Stop();
                };
                _toastTimer.Start();
            });
        }

        private DashboardViewModel? _mainDashboard;
        private DashboardViewModel? _mimicDashboard;

        public ObservableCollection<ChildWindowViewModel> ActiveChildWindows { get; } = new();

        public MainViewModel(
            IConfigurationService configurationService, 
            IDataCoreService dataCoreService, 
            ISimulationService simulationService,
            IProjectContextService projectContext,
            IDialogService dialogService,
            IWidgetFactory widgetFactory)
        {
            _configurationService = configurationService;
            _dataCoreService = dataCoreService;
            _simulationService = simulationService;
            _dialogService = dialogService;
            _widgetFactory = widgetFactory;
            ProjectContext = projectContext;

            if (_widgetFactory is WidgetFactory factory)
            {
                factory.ChildWindowService = this;
                factory.DialogService = _dialogService;
            }
            
            if (_dialogService is AvaloniaApplication1.Services.DialogService ds)
            {
                ds.ChildWindowService = this;
            }

            // Load configuration
            LoadConfigAsync().FireAndForget(context: "MainViewModel.ctor");
        }

        [RelayCommand]
        private void ToggleDesignMode()
        {
            ProjectContext.IsDesignMode = !ProjectContext.IsDesignMode;
        }

        [ObservableProperty]
        private bool _isSimulationRunning = false;

        [RelayCommand]
        private async Task ToggleSimulationAsync()
        {
            if (IsSimulationRunning)
            {
                await _simulationService.StopSimulationAsync();
                IsSimulationRunning = false;
            }
            else
            {
                await _simulationService.StartSimulationAsync();
                IsSimulationRunning = true;
            }
        }

        [RelayCommand]
        private void ResetSimulation()
        {
            _simulationService.ResetSimulation();
        }

        [RelayCommand]
        private void ShowDashboard()
        {
            IsMimicActive = false;
            Dashboard = _mainDashboard;
        }

        [RelayCommand]
        private void ShowMimic()
        {
            IsMimicActive = true;
            Dashboard = _mimicDashboard;
        }

        [RelayCommand]
        private async Task SaveConfigAsync()
        {
            if (_currentConfig != null)
            {
                await _configurationService.SaveConfigurationAsync(_currentConfig);
            }
        }

        [RelayCommand]
        private async Task OpenConnectionManagerAsync()
        {
            if (_currentConfig == null) return;
            await _dialogService.ShowConnectionManagerAsync(_currentConfig);
        }

        public ChildWindowViewModel? OpenChildWindow(string title, object content)
        {
            Console.WriteLine($"[MainVM] OpenChildWindow called. title={title}, _currentConfig={_currentConfig != null}, content type={content?.GetType().Name}");
            if (_currentConfig == null) return null;

            if (content is DashboardConfig config)
            {
                content = new DashboardViewModel(
                    config, 
                    _dataCoreService, 
                    ProjectContext,
                    _currentConfig,
                    _dialogService,
                    _widgetFactory);
            }

            var childWindow = new ChildWindowViewModel(title, content);
            
            var originalClose = childWindow.CloseAction;
            childWindow.CloseAction = () =>
            {
                ActiveChildWindows.Remove(childWindow);
                originalClose?.Invoke();
            };

            // Stagger coordinates slightly
            int count = ActiveChildWindows.Count;
            childWindow.X = 40 + (count % 8) * 35;
            childWindow.Y = 40 + (count % 8) * 35;

            int maxZ = 0;
            foreach (var w in ActiveChildWindows)
            {
                if (w.ZIndex > maxZ)
                {
                    maxZ = w.ZIndex;
                }
            }
            childWindow.ZIndex = maxZ + 1;

            ActiveChildWindows.Add(childWindow);
            return childWindow;
        }

        private async Task LoadConfigAsync()
        {
            _currentConfig = await _configurationService.LoadConfigurationAsync();

            if (_currentConfig.Mimic == null)
            {
                _currentConfig.Mimic = new DashboardConfig();
            }

            if (_currentConfig.Mimic.CellSize == LegacyCellSizeLarge || _currentConfig.Mimic.CellSize == 0 || _currentConfig.Mimic.CellSize == LegacyCellSizeSmall)
            {
                _currentConfig.Mimic.CellSize = DefaultMimicCellSize;
            }

            _mainDashboard = new DashboardViewModel(
                _currentConfig.Dashboard, 
                _dataCoreService, 
                ProjectContext,
                _currentConfig,
                _dialogService,
                _widgetFactory);

            _mimicDashboard = new DashboardViewModel(
                _currentConfig.Mimic, 
                _dataCoreService, 
                ProjectContext,
                _currentConfig,
                _dialogService,
                _widgetFactory);

            // Start background polling for Modbus/MQTT drivers
            _dataCoreService.StartAsync().FireAndForget(context: "MainViewModel.LoadConfig");

            Dashboard = _mainDashboard;
            IsMimicActive = false;
        }

        public void Dispose()
        {
            _toastTimer?.Stop();
            _mainDashboard?.Dispose();
            _mimicDashboard?.Dispose();
            foreach (var cw in ActiveChildWindows)
                (cw.Content as IDisposable)?.Dispose();
            GC.SuppressFinalize(this);
        }
    }
}