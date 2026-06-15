using System.Threading.Tasks;
using System.Collections.ObjectModel;
using AvaloniaApplication1.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using AvaloniaApplication1.Models.Config;
using Microsoft.Extensions.DependencyInjection;

namespace AvaloniaApplication1.ViewModels
{
    public partial class MainViewModel : ViewModelBase
    {
        private readonly IConfigurationService _configurationService;
        private readonly IMockDataService _mockDataService;
        private readonly IDialogService _dialogService;
        private HmiConfiguration? _currentConfig;

        public IProjectContextService ProjectContext { get; }

        [ObservableProperty]
        private DashboardViewModel? _dashboard;

        [ObservableProperty]
        private bool _isMimicActive;

        private DashboardViewModel? _mainDashboard;
        private DashboardViewModel? _mimicDashboard;

        public ObservableCollection<ChildWindowViewModel> ActiveChildWindows { get; } = new();

        public MainViewModel(
            IConfigurationService configurationService, 
            IMockDataService mockDataService, 
            IProjectContextService projectContext,
            IDialogService dialogService)
        {
            _configurationService = configurationService;
            _mockDataService = mockDataService;
            _dialogService = dialogService;
            ProjectContext = projectContext;

            // Start communication drivers & simulation
            _mockDataService.StartSimulation();

            // Load configuration
            _ = LoadConfigAsync();
        }

        [RelayCommand]
        private void ToggleDesignMode()
        {
            ProjectContext.IsDesignMode = !ProjectContext.IsDesignMode;
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

        public ChildWindowViewModel? OpenChildWindow(string title, DashboardConfig config)
        {
            if (_currentConfig == null) return null;

            var dashboardVm = new DashboardViewModel(
                config, 
                _mockDataService, 
                ProjectContext,
                _currentConfig,
                _dialogService);

            var childWindow = new ChildWindowViewModel(title, dashboardVm);
            
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

            _mainDashboard = new DashboardViewModel(
                _currentConfig.Dashboard, 
                _mockDataService, 
                ProjectContext,
                _currentConfig,
                _dialogService);

            if (_currentConfig.Mimic == null)
            {
                _currentConfig.Mimic = new DashboardConfig();
            }

            if (_currentConfig.Mimic.CellSize == 160 || _currentConfig.Mimic.CellSize == 0 || _currentConfig.Mimic.CellSize == 20)
            {
                _currentConfig.Mimic.CellSize = 10;
            }

            _mimicDashboard = new DashboardViewModel(
                _currentConfig.Mimic, 
                _mockDataService, 
                ProjectContext,
                _currentConfig,
                _dialogService);

            Dashboard = _mainDashboard;
            IsMimicActive = false;
        }
    }
}