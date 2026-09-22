using System.Collections.Generic;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform.Storage;
using AvaloniaApplication1.Models.Config;
using AvaloniaApplication1.ViewModels;
using AvaloniaApplication1.Views;
using Microsoft.Extensions.DependencyInjection;

namespace AvaloniaApplication1.Services
{
    public class DialogService : IDialogService
    {
        private double? _lastNumpadX;
        private double? _lastNumpadY;
        private readonly IDataCoreService _dataCoreService;
        private readonly IProjectContextService _projectContext;
        private readonly IWidgetFactory _widgetFactory;

        public DialogService(IDataCoreService dataCoreService, IProjectContextService projectContext, IWidgetFactory widgetFactory)
        {
            _dataCoreService = dataCoreService;
            _projectContext = projectContext;
            _widgetFactory = widgetFactory;
        }

        public IChildWindowService? ChildWindowService { get; set; }

        private Window? MainWindow => (Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)?.MainWindow;

        public async Task<string?> ShowNumpadAsync(string title, string initialValue, double? x = null, double? y = null)
        {
            if (ChildWindowService == null) return null;

            var tcs = new TaskCompletionSource<string?>();

            var vm = new NumpadViewModel { InputValue = initialValue };
            var childWindow = ChildWindowService.OpenChildWindow(title, vm);
            
            if (childWindow != null)
            {
                var targetWindow = childWindow;
                // Use remembered position if available, otherwise default
                if (_lastNumpadX.HasValue && _lastNumpadY.HasValue)
                {
                    targetWindow.X = _lastNumpadX.Value;
                    targetWindow.Y = _lastNumpadY.Value;
                }
                else
                {
                    targetWindow.X = 100;
                    targetWindow.Y = 100;
                }
                
                // Numpad doesn't need to be resizable, lock sizes
                targetWindow.Width = 320;
                targetWindow.Height = 400;

                vm.OnConfirm = (val) => 
                {
                    tcs.TrySetResult(val);
                    _lastNumpadX = targetWindow.X;
                    _lastNumpadY = targetWindow.Y;
                    targetWindow.CloseCommand.Execute(null);
                };
                
                vm.OnCancel = () => 
                {
                    tcs.TrySetResult(null);
                    _lastNumpadX = targetWindow.X;
                    _lastNumpadY = targetWindow.Y;
                    targetWindow.CloseCommand.Execute(null);
                };

                // Handle window close via X button
                var originalClose = targetWindow.CloseAction;
                targetWindow.CloseAction = () =>
                {
                    _lastNumpadX = targetWindow.X;
                    _lastNumpadY = targetWindow.Y;
                    tcs.TrySetResult(null);
                    originalClose?.Invoke();
                };
            }
            else
            {
                return null;
            }

            return await tcs.Task;
        }

        public async Task ShowContainerDashboardAsync(string title, DashboardConfig config)
        {
            if (MainWindow == null) return;
            
            // Create a minimal HmiConfiguration for the container context
            var containerConfig = new HmiConfiguration
            {
                Dashboard = config,
                Connections = new List<ConnectionConfig>() // Container doesn't manage connections
            };
            
            var dashboardVm = new DashboardViewModel(
                config, 
                _dataCoreService, 
                _projectContext,
                containerConfig,
                this,
                _widgetFactory);
            
            try
            {
                var window = new Window
                {
                    Title = title,
                    Width = 800,
                    Height = 600,
                    WindowStartupLocation = WindowStartupLocation.CenterOwner,
                    Background = Avalonia.Media.Brush.Parse("#1E1E1E"),
                    Content = new DashboardView { DataContext = dashboardVm }
                };

                await window.ShowDialog(MainWindow);
            }
            finally
            {
                dashboardVm.Dispose();
            }
        }

        // ===== Design Mode Dialogs =====

        public async Task<WidgetConfig?> ShowWidgetEditorAsync(WidgetConfig? existingConfig, List<ConnectionConfig> connections, WidgetPosition? initialPosition = null)
        {
            var targetWindow = MainWindow;
            if (targetWindow == null) return null;

            var vm = new WidgetEditorViewModel(existingConfig, connections, initialPosition);
            var window = new WidgetEditorWindow
            {
                DataContext = vm
            };

            vm.CloseAction = () => window.Close();

            await window.ShowDialog(targetWindow);

            return vm.IsConfirmed ? vm.ToWidgetConfig() : null;
        }

        public async Task<(double CellSize, double ZoomScale)?> ShowDashboardPropertiesAsync(double currentCellSize, double currentZoomScale)
        {
            var targetWindow = MainWindow;
            if (targetWindow == null) return null;

            var vm = new DashboardPropertiesViewModel(currentCellSize, currentZoomScale);
            var window = new DashboardPropertiesWindow
            {
                DataContext = vm
            };

            vm.CloseAction = () => window.Close();

            await window.ShowDialog(targetWindow);

            return vm.IsConfirmed ? (vm.CellSize, vm.ZoomScale) : null;
        }

        public async Task<ConnectionConfig?> ShowConnectionEditorAsync(ConnectionConfig? existingConfig)
        {
            if (MainWindow == null) return null;

            var vm = new ConnectionEditorViewModel(existingConfig);
            var window = new ConnectionEditorWindow
            {
                DataContext = vm
            };

            vm.CloseAction = () => window.Close();

            await window.ShowDialog(MainWindow);

            return vm.IsConfirmed ? vm.ToConnectionConfig() : null;
        }

        public async Task ShowConnectionManagerAsync(HmiConfiguration config)
        {
            if (MainWindow == null) return;

            var vm = new ConnectionManagerViewModel(config, this);
            var window = new ConnectionManagerWindow
            {
                DataContext = vm
            };

            await window.ShowDialog(MainWindow);
        }

        public async Task<bool> PromptPasswordAsync(string expectedPassword, string title = "Вход в режим редактирования")
        {
            var tcs = new TaskCompletionSource<bool>();
            var vm = new PasswordPromptViewModel
            {
                Title = title,
                ExpectedPassword = expectedPassword
            };

            if (MainWindow != null)
            {
                var window = new PasswordPromptWindow
                {
                    DataContext = vm
                };
                vm.OnResult = (success) =>
                {
                    tcs.TrySetResult(success);
                    window.Close();
                };
                await window.ShowDialog(MainWindow);
                return await tcs.Task;
            }
            else if (ChildWindowService != null)
            {
                var childWindow = ChildWindowService.OpenChildWindow(title, vm);
                if (childWindow != null)
                {
                    childWindow.Width = 360;
                    childWindow.Height = 440;
                    vm.OnResult = (success) =>
                    {
                        tcs.TrySetResult(success);
                        childWindow.CloseCommand.Execute(null);
                    };
                    return await tcs.Task;
                }
            }

            return false;
        }

        public async Task ShowSettingsAsync(HmiConfiguration config, IConfigurationService configService)
        {
            if (MainWindow == null) return;

            var window = new SettingsWindow();
            var vm = new SettingsViewModel(config, configService, this, () => window.Close());
            window.DataContext = vm;

            await window.ShowDialog(MainWindow);
        }

        public async Task<string?> ShowOpenProjectDialogAsync()
        {
            if (MainWindow == null) return null;
            var topLevel = TopLevel.GetTopLevel(MainWindow);
            if (topLevel == null) return null;

            var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = "Загрузить проект HMI",
                AllowMultiple = false,
                FileTypeFilter = new List<FilePickerFileType>
                {
                    new("Файлы проекта HMI (*.json)")
                    {
                        Patterns = new[] { "*.json" },
                        MimeTypes = new[] { "application/json" }
                    },
                    new("Все файлы (*.*)")
                    {
                        Patterns = new[] { "*.*" }
                    }
                }
            });

            if (files != null && files.Count > 0)
            {
                var file = files[0];
                return file.TryGetLocalPath() ?? (file.Path.IsFile ? file.Path.LocalPath : null);
            }

            return null;
        }

        public async Task<string?> ShowSaveProjectAsDialogAsync(string? defaultFileName = null)
        {
            if (MainWindow == null) return null;
            var topLevel = TopLevel.GetTopLevel(MainWindow);
            if (topLevel == null) return null;

            var file = await topLevel.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                Title = "Сохранить проект HMI как...",
                DefaultExtension = "json",
                SuggestedFileName = string.IsNullOrWhiteSpace(defaultFileName) ? "config.json" : defaultFileName,
                FileTypeChoices = new List<FilePickerFileType>
                {
                    new("Файлы проекта HMI (*.json)")
                    {
                        Patterns = new[] { "*.json" },
                        MimeTypes = new[] { "application/json" }
                    },
                    new("Все файлы (*.*)")
                    {
                        Patterns = new[] { "*.*" }
                    }
                }
            });

            if (file != null)
            {
                return file.TryGetLocalPath() ?? (file.Path.IsFile ? file.Path.LocalPath : null);
            }

            return null;
        }
    }
}