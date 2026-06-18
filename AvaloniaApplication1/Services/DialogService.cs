using System.Collections.Generic;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
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
                // Use remembered position if available, otherwise default
                if (_lastNumpadX.HasValue && _lastNumpadY.HasValue)
                {
                    childWindow.X = _lastNumpadX.Value;
                    childWindow.Y = _lastNumpadY.Value;
                }
                else
                {
                    childWindow.X = 100;
                    childWindow.Y = 100;
                }
                
                // Numpad doesn't need to be resizable, lock sizes
                childWindow.Width = 320;
                childWindow.Height = 400;

                vm.OnConfirm = (val) => 
                {
                    tcs.TrySetResult(val);
                    if (childWindow != null)
                    {
                        _lastNumpadX = childWindow.X;
                        _lastNumpadY = childWindow.Y;
                    }
                    childWindow.CloseCommand.Execute(null);
                };
                
                vm.OnCancel = () => 
                {
                    tcs.TrySetResult(null);
                    if (childWindow != null)
                    {
                        _lastNumpadX = childWindow.X;
                        _lastNumpadY = childWindow.Y;
                    }
                    childWindow.CloseCommand.Execute(null);
                };

                // Handle window close via X button
                var originalClose = childWindow.CloseAction;
                childWindow.CloseAction = () =>
                {
                    if (childWindow != null)
                    {
                        _lastNumpadX = childWindow.X;
                        _lastNumpadY = childWindow.Y;
                    }
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

        public async Task<WidgetConfig?> ShowWidgetEditorAsync(WidgetConfig? existingConfig, List<ConnectionConfig> connections)
        {
            if (MainWindow == null) return null;

            var vm = new WidgetEditorViewModel(existingConfig, connections);
            var window = new WidgetEditorWindow
            {
                DataContext = vm
            };

            vm.CloseAction = () => window.Close();

            await window.ShowDialog(MainWindow);

            return vm.IsConfirmed ? vm.ToWidgetConfig() : null;
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
    }
}