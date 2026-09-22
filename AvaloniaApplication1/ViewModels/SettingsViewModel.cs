using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using AvaloniaApplication1.Models.Config;
using AvaloniaApplication1.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AvaloniaApplication1.ViewModels
{
    public partial class SettingsViewModel : ViewModelBase
    {
        private readonly HmiConfiguration _config;
        private readonly IConfigurationService _configService;
        private readonly IDialogService? _dialogService;
        private readonly Action? _closeAction;

        [ObservableProperty]
        private string _projectName = string.Empty;

        [ObservableProperty]
        private string _projectVersion = "1.0.0";

        [ObservableProperty]
        private string _currentFilePath = string.Empty;

        [ObservableProperty]
        private int _mimicCellSize = 10;

        [ObservableProperty]
        private double _mimicZoomScale = 1.0;

        [ObservableProperty]
        private bool _requirePassword;

        [ObservableProperty]
        private string _designPassword = "1234";

        [ObservableProperty]
        private string _statusMessage = string.Empty;

        [ObservableProperty]
        private int _selectedTabIndex;

        public ObservableCollection<ConnectionConfig> Connections { get; } = new();

        public SettingsViewModel(
            HmiConfiguration config,
            IConfigurationService configService,
            IDialogService? dialogService = null,
            Action? closeAction = null)
        {
            _config = config;
            _configService = configService;
            _dialogService = dialogService;
            _closeAction = closeAction;

            _config.Project ??= new ProjectConfig();
            _config.Security ??= new SecurityConfig();
            _config.Mimic ??= new DashboardConfig { CellSize = 10, ZoomScale = 1.0 };

            ProjectName = _config.Project.Name;
            ProjectVersion = _config.Project.Version;
            CurrentFilePath = _configService.CurrentFilePath;

            MimicCellSize = _config.Mimic.CellSize > 0 ? _config.Mimic.CellSize : 10;
            MimicZoomScale = _config.Mimic.ZoomScale > 0 ? _config.Mimic.ZoomScale : 1.0;

            RequirePassword = _config.Security.RequirePasswordForDesignMode;
            DesignPassword = !string.IsNullOrEmpty(_config.Security.DesignModePassword) 
                ? _config.Security.DesignModePassword 
                : "1234";

            if (_config.Connections != null)
            {
                foreach (var conn in _config.Connections)
                {
                    Connections.Add(conn);
                }
            }
        }

        [RelayCommand]
        private async Task SaveSettingsAsync()
        {
            _config.Project.Name = ProjectName;
            _config.Project.Version = ProjectVersion;
            _config.Mimic.CellSize = MimicCellSize;
            _config.Mimic.ZoomScale = MimicZoomScale;
            _config.Security.RequirePasswordForDesignMode = RequirePassword;
            _config.Security.DesignModePassword = DesignPassword;

            await _configService.SaveConfigurationAsync(_config);
            StatusMessage = "Настройки успешно сохранены!";
            await Task.Delay(1500);
            _closeAction?.Invoke();
        }

        [RelayCommand]
        private void Cancel()
        {
            _closeAction?.Invoke();
        }

        [RelayCommand]
        private async Task OpenConnectionsManagerAsync()
        {
            if (_dialogService != null)
            {
                await _dialogService.ShowConnectionManagerAsync(_config);
                Connections.Clear();
                if (_config.Connections != null)
                {
                    foreach (var conn in _config.Connections)
                    {
                        Connections.Add(conn);
                    }
                }
            }
        }
    }
}
