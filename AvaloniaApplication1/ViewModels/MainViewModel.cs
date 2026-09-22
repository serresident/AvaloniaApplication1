using System;
using System.IO;
using System.Threading;
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
        private string _currentProjectName = "config.json";

        [ObservableProperty]
        private string _windowTitle = "Avalonia HMI Dashboard - [config.json]";

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

        [ObservableProperty]
        private string _currentTimeText = DateTime.Now.ToString("HH:mm:ss");

        [ObservableProperty]
        private string _currentDateText = DateTime.Now.ToString("dd.MM.yyyy");

        [ObservableProperty]
        private string _connectionStatusText = "🟢 Онлайн";

        [ObservableProperty]
        private string _activeAlarmsText = "🔔 0 Аварий";

        [ObservableProperty]
        private bool _hasActiveAlarms = false;

        private DispatcherTimer? _clockTimer;
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

            // Start 1-second system clock timer for informative header & title bar
            _clockTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(1)
            };
            _clockTimer.Tick += (s, e) =>
            {
                CurrentTimeText = DateTime.Now.ToString("HH:mm:ss");
                CurrentDateText = DateTime.Now.ToString("dd.MM.yyyy");
                UpdateWindowTitle();
            };
            _clockTimer.Start();

            // Load configuration
            LoadConfigAsync().FireAndForget(context: "MainViewModel.ctor");
        }

        public void UpdateWindowTitle()
        {
            var modeText = ProjectContext.IsDesignMode ? "✏️ Наладка" : "🔒 Исполнение";
            var simText = IsSimulationRunning ? "🟡 Симуляция" : "🟢 Онлайн";
            WindowTitle = $"[{CurrentProjectName}] — HMI SCADA | {simText} | {modeText} | {CurrentTimeText}";
        }

        [RelayCommand]
        private async Task ToggleDesignModeAsync()
        {
            try
            {
                if (!ProjectContext.IsDesignMode)
                {
                    var reqPassword = _currentConfig?.Security?.RequirePasswordForDesignMode ?? false;
                    if (reqPassword)
                    {
                        var expected = !string.IsNullOrEmpty(_currentConfig?.Security?.DesignModePassword) 
                            ? _currentConfig.Security.DesignModePassword 
                            : "1234";

                        var isSuccess = await _dialogService.PromptPasswordAsync(expected, "Вход в режим редактирования");
                        if (!isSuccess)
                        {
                            ShowToast("Вход отменен или неверный пароль.");
                            return;
                        }
                    }

                    ProjectContext.IsDesignMode = true;
                    ShowToast("Режим редактирования (Design Mode).");
                }
                else
                {
                    ProjectContext.IsDesignMode = false;
                    ShowToast("Режим исполнения (Runtime).");
                }
            }
            finally
            {
                UpdateWindowTitle();
                ToggleDesignModeCommand.NotifyCanExecuteChanged();
            }
        }

        [RelayCommand]
        private async Task OpenSettingsAsync()
        {
            if (_currentConfig == null) return;
            await _dialogService.ShowSettingsAsync(_currentConfig, _configurationService);
            OnPropertyChanged(nameof(CurrentProjectName));
            OnPropertyChanged(nameof(WindowTitle));
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
        private async Task OpenProjectAsync()
        {
            var filePath = await _dialogService.ShowOpenProjectDialogAsync();
            if (string.IsNullOrWhiteSpace(filePath)) return;

            try
            {
                if (IsSimulationRunning)
                {
                    await ToggleSimulationAsync();
                }

                await LoadConfigAsync(filePath);
                ShowToast($"Загружен проект: {Path.GetFileName(filePath)}");
            }
            catch (Exception ex)
            {
                ShowToast($"Ошибка загрузки проекта: {ex.Message}");
            }
        }

        [RelayCommand]
        private async Task SaveConfigAsync()
        {
            if (_currentConfig != null)
            {
                try
                {
                    await _configurationService.SaveConfigurationAsync(_currentConfig);
                    ShowToast($"Проект сохранен: {CurrentProjectName}");
                }
                catch (Exception ex)
                {
                    ShowToast($"Ошибка сохранения: {ex.Message}");
                }
            }
        }

        [RelayCommand]
        private async Task SaveConfigAsAsync()
        {
            if (_currentConfig == null) return;

            var currentFileName = Path.GetFileName(_configurationService.CurrentFilePath);
            var filePath = await _dialogService.ShowSaveProjectAsDialogAsync(currentFileName);
            if (string.IsNullOrWhiteSpace(filePath)) return;

            try
            {
                await _configurationService.SaveConfigurationAsync(_currentConfig, filePath);
                CurrentProjectName = Path.GetFileName(filePath);
                WindowTitle = $"Avalonia HMI Dashboard - [{CurrentProjectName}]";
                ShowToast($"Проект сохранен как: {CurrentProjectName}");
            }
            catch (Exception ex)
            {
                ShowToast($"Ошибка сохранения: {ex.Message}");
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
            if (content == null) return null;
            Console.WriteLine($"[MainVM] OpenChildWindow called. title={title}, _currentConfig={_currentConfig != null}, content type={content.GetType().Name}");
            if (_currentConfig == null) return null;

            object windowContent = content;
            if (content is DashboardConfig config)
            {
                windowContent = new DashboardViewModel(
                    config, 
                    _dataCoreService, 
                    ProjectContext,
                    _currentConfig,
                    _dialogService,
                    _widgetFactory);
            }

            var childWindow = new ChildWindowViewModel(title, windowContent);
            
            var originalClose = childWindow.CloseAction;
            childWindow.CloseAction = () =>
            {
                ActiveChildWindows.Remove(childWindow);
                childWindow.Dispose();
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

        private int _loadVersion = 0;

        public async Task LoadConfigAsync(string? filePath = null)
        {
            int version = Interlocked.Increment(ref _loadVersion);

            // Stop current telemetry drivers
            await _dataCoreService.StopAsync();

            var loadedConfig = await _configurationService.LoadConfigurationAsync(filePath);

            if (version != _loadVersion)
            {
                return;
            }

            _mainDashboard?.Dispose();
            _mimicDashboard?.Dispose();
            foreach (var cw in ActiveChildWindows)
                cw.Dispose();
            ActiveChildWindows.Clear();

            _currentConfig = loadedConfig;

            CurrentProjectName = Path.GetFileName(_configurationService.CurrentFilePath);
            WindowTitle = $"Avalonia HMI Dashboard - [{CurrentProjectName}]";

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

            Dashboard = IsMimicActive ? _mimicDashboard : _mainDashboard;
        }

        public void Dispose()
        {
            _toastTimer?.Stop();
            _clockTimer?.Stop();
            _mainDashboard?.Dispose();
            _mimicDashboard?.Dispose();
            foreach (var cw in ActiveChildWindows)
                cw.Dispose();
            ActiveChildWindows.Clear();
            GC.SuppressFinalize(this);
        }
    }
}