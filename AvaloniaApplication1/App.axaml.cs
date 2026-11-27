using System;
using System.Linq;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Microsoft.Extensions.DependencyInjection;
using AvaloniaApplication1.Services;
using AvaloniaApplication1.Services.Protocols;
using AvaloniaApplication1.ViewModels.Core;
using AvaloniaApplication1.ViewModels.Widgets;
using AvaloniaApplication1.ViewModels.Windows.Common;
using AvaloniaApplication1.Views.Windows.Common;

namespace AvaloniaApplication1
{
    public partial class App : Application
    {
        public static IServiceProvider? Services { get; private set; }

        public override void Initialize()
        {
            AvaloniaXamlLoader.Load(this);
        }

        public override void OnFrameworkInitializationCompleted()
        {
            var services = new ServiceCollection();
            ConfigureServices(services);
            Services = services.BuildServiceProvider();

            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                var mainVm = Services.GetRequiredService<MainViewModel>();
                desktop.MainWindow = new MainWindow
                {
                    DataContext = mainVm
                };
                desktop.ShutdownRequested += (_, _) =>
                {
                    mainVm.Dispose();
                    if (Services is IDisposable disposableProvider)
                    {
                        disposableProvider.Dispose();
                    }
                };
            }

#if DEBUG
            try
            {
                var debugBridge = Services.GetService<AvaloniaApplication1.Services.Debug.IScadaDebugBridge>();
                _ = debugBridge?.StartAsync();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ScadaDebugBridge] Init failed: {ex.Message}");
            }
#endif

            base.OnFrameworkInitializationCompleted();
        }

        private void ConfigureServices(IServiceCollection services)
        {
            // Services
            services.AddSingleton<IThemeService, ThemeService>();
            services.AddSingleton<IProjectContextService, ProjectContextService>();
            services.AddSingleton<IConfigurationService, ConfigurationService>();
            
            // Factory for protocol drivers
            services.AddSingleton<IProtocolDriverFactory>(sp => 
            {
                var factory = new AvaloniaApplication1.Services.Protocols.ProtocolDriverFactory();
                // Register our known drivers
                factory.RegisterDriver("MQTT", (config, tags) => new AvaloniaApplication1.Services.Protocols.MqttProtocolDriver(config, tags.Where(t => t.Address != null).Select(t => t.Address!).Distinct()));
                factory.RegisterDriver("ModbusTCP", (config, tags) => new AvaloniaApplication1.Services.Protocols.ModbusProtocolDriver(config, tags));
                factory.RegisterDriver("ModbusRTUOverTCP", (config, tags) => new AvaloniaApplication1.Services.Protocols.ModbusProtocolDriver(config, tags));
                return factory;
            });

            services.AddSingleton<ITagManagerService, TagManagerService>();
            services.AddSingleton<DataCoreService>();
            services.AddSingleton<IDataCoreService>(sp => sp.GetRequiredService<DataCoreService>());
            
            services.AddSingleton<SimulationService>();
            services.AddSingleton<ISimulationService>(sp => sp.GetRequiredService<SimulationService>());
            
            services.AddSingleton<AlarmNotificationService>();
            services.AddSingleton<IAlarmNotificationService>(sp => sp.GetRequiredService<AlarmNotificationService>());
            
            services.AddSingleton<IDialogService, DialogService>();
            services.AddSingleton<IHmiClipboardService, HmiClipboardService>();
            services.AddTransient<AvaloniaApplication1.Services.History.IHistoryService, AvaloniaApplication1.Services.History.HistoryService>();

            // ViewModels
            services.AddSingleton<IWidgetFactory, WidgetFactory>();
            services.AddSingleton<MainViewModel>();

#if DEBUG
            services.AddSingleton<AvaloniaApplication1.Services.Debug.IScadaDebugBridge, AvaloniaApplication1.Services.Debug.ScadaDebugBridge>();
#endif
        }
    }
}