using System;
using System.Linq;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Microsoft.Extensions.DependencyInjection;
using AvaloniaApplication1.Services;
using AvaloniaApplication1.Services.Protocols;
using AvaloniaApplication1.ViewModels;

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
                desktop.MainWindow = new MainWindow
                {
                    DataContext = Services.GetRequiredService<MainViewModel>()
                };
            }

            base.OnFrameworkInitializationCompleted();
        }

        private void ConfigureServices(IServiceCollection services)
        {
            // Services
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

            services.AddSingleton<DataCoreService>();
            services.AddSingleton<IDataCoreService>(sp => sp.GetRequiredService<DataCoreService>());
            
            services.AddSingleton<SimulationService>();
            services.AddSingleton<ISimulationService>(sp => sp.GetRequiredService<SimulationService>());
            
            services.AddSingleton<IDialogService, DialogService>();

            // ViewModels
            services.AddSingleton<IWidgetFactory, WidgetFactory>();
            services.AddSingleton<MainViewModel>();
        }
    }
}