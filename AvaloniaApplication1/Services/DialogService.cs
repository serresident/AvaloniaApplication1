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
        private Window? MainWindow => (Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)?.MainWindow;

        public async Task<string?> ShowNumpadAsync(string title, string initialValue)
        {
            if (MainWindow == null) return null;

            var vm = new NumpadViewModel { InputValue = initialValue };
            var window = new NumpadWindow
            {
                DataContext = vm,
                Title = title
            };

            var result = await window.ShowDialog<string?>(MainWindow);
            return result;
        }

        public async Task ShowContainerDashboardAsync(string title, DashboardConfig config)
        {
            if (MainWindow == null || App.Services == null) return;

            var dataCoreService = App.Services.GetRequiredService<IDataCoreService>();
            var projectContext = App.Services.GetRequiredService<IProjectContextService>();
            var dialogService = App.Services.GetRequiredService<IDialogService>();
            var widgetFactory = App.Services.GetRequiredService<IWidgetFactory>();
            
            // Create a minimal HmiConfiguration for the container context
            var containerConfig = new HmiConfiguration
            {
                Dashboard = config,
                Connections = new List<ConnectionConfig>() // Container doesn't manage connections
            };
            
            var dashboardVm = new DashboardViewModel(
                config, 
                dataCoreService, 
                projectContext,
                containerConfig,
                dialogService,
                widgetFactory);
            
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