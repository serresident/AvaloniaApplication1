using System;
using System.IO;
using System.Text.RegularExpressions;

string rootDir = @"c:\Users\adm\projects\AvaloniaApplication1\AvaloniaApplication1\";

// 1. Create IWidgetFactory
File.WriteAllText(Path.Combine(rootDir, @"ViewModels\IWidgetFactory.cs"), @"using AvaloniaApplication1.Models.Config;

namespace AvaloniaApplication1.ViewModels
{
    public interface IWidgetFactory
    {
        WidgetViewModelBase CreateWidgetViewModel(WidgetConfig config);
    }
}");

// 2. Create WidgetFactory
File.WriteAllText(Path.Combine(rootDir, @"ViewModels\WidgetFactory.cs"), @"using System;
using AvaloniaApplication1.Models.Config;
using AvaloniaApplication1.Services;

namespace AvaloniaApplication1.ViewModels
{
    public class WidgetFactory : IWidgetFactory
    {
        private readonly IMockDataService _dataService;
        private readonly IProjectContextService _projectContext;

        public WidgetFactory(IMockDataService dataService, IProjectContextService projectContext)
        {
            _dataService = dataService;
            _projectContext = projectContext;
        }

        public WidgetViewModelBase CreateWidgetViewModel(WidgetConfig config)
        {
            return config switch
            {
                ValueDisplayConfig c => new ValueDisplayViewModel(c, _dataService, _projectContext),
                PilotLightConfig c => new PilotLightViewModel(c, _dataService, _projectContext),
                ContainerButtonConfig c => new ContainerButtonViewModel(c, _dataService, _projectContext),
                CommandButtonConfig c => new CommandButtonViewModel(c, _dataService, _projectContext),
                SliderConfig c => new SliderViewModel(c, _dataService, _projectContext),
                SetValueConfig c => new SetValueViewModel(c, _dataService, _projectContext),
                RealTimeTrendConfig c => new RealTimeTrendViewModel(c, _dataService, _projectContext),
                PipeConfig c => new PipeWidgetViewModel(c, _dataService, _projectContext),
                ValveConfig c => new ValveWidgetViewModel(c, _dataService, _projectContext),
                TankConfig c => new TankWidgetViewModel(c, _dataService, _projectContext),
                PumpConfig c => new PumpWidgetViewModel(c, _dataService, _projectContext),
                _ => throw new ArgumentException($""Unknown widget config type: {config.GetType().Name}"")
            };
        }
    }
}");

// 3. Patch DashboardViewModel.cs
string dashPath = Path.Combine(rootDir, @"ViewModels\DashboardViewModel.cs");
if (File.Exists(dashPath))
{
    string content = File.ReadAllText(dashPath);
    // Add factory to constructor
    content = content.Replace("IDialogService dialogService)", "IDialogService dialogService, IWidgetFactory widgetFactory)");
    content = content.Replace("private readonly IDialogService _dialogService;", "private readonly IDialogService _dialogService;\n        private readonly IWidgetFactory _widgetFactory;");
    content = content.Replace("_dialogService = dialogService;", "_dialogService = dialogService;\n            _widgetFactory = widgetFactory;");
    
    // Replace switch
    string oldSwitch = @"return widgetConfig.Type switch
            {
                ""ValueDisplay"" => new ValueDisplayViewModel(widgetConfig, _dataService, _projectContext),
                ""PilotLight"" => new PilotLightViewModel(widgetConfig, _dataService, _projectContext),
                ""ContainerButton"" => new ContainerButtonViewModel(widgetConfig, _dataService, _projectContext),
                ""CommandButton"" => new CommandButtonViewModel(widgetConfig, _dataService, _projectContext),
                ""Slider"" => new SliderViewModel(widgetConfig, _dataService, _projectContext),
                ""SetValue"" => new SetValueViewModel(widgetConfig, _dataService, _projectContext),
                ""RealTimeTrend"" => new RealTimeTrendViewModel(widgetConfig, _dataService, _projectContext),
                ""Pipe"" => new PipeWidgetViewModel(widgetConfig, _dataService, _projectContext),
                ""Valve"" => new ValveWidgetViewModel(widgetConfig, _dataService, _projectContext),
                ""Tank"" => new TankWidgetViewModel(widgetConfig, _dataService, _projectContext),
                ""Pump"" => new PumpWidgetViewModel(widgetConfig, _dataService, _projectContext),
                _ => throw new NotImplementedException($""Widget type {widgetConfig.Type} not implemented"")
            };";
    string newSwitch = "return _widgetFactory.CreateWidgetViewModel(widgetConfig);";
    content = content.Replace(oldSwitch.Replace("\r\n", "\n"), newSwitch);
    if(content == File.ReadAllText(dashPath)) content = content.Replace(oldSwitch, newSwitch);

    File.WriteAllText(dashPath, content);
}

// 4. Patch App.axaml.cs
string appPath = Path.Combine(rootDir, @"App.axaml.cs");
if (File.Exists(appPath))
{
    string content = File.ReadAllText(appPath);
    if (!content.Contains("IWidgetFactory"))
    {
        content = content.Replace("services.AddTransient<DashboardViewModel>();", "services.AddTransient<DashboardViewModel>();\n            services.AddSingleton<IWidgetFactory, WidgetFactory>();");
        File.WriteAllText(appPath, content);
    }
}

// 5. Patch ConfigurationService.cs
string confPath = Path.Combine(rootDir, @"Services\ConfigurationService.cs");
if (File.Exists(confPath))
{
    string content = File.ReadAllText(confPath);
    // Regex replace "new WidgetConfig { Type = "X"" with "new XConfig { Type = "X""
    content = Regex.Replace(content, @"new WidgetConfig\s*\{\s*Type\s*=\s*""([^""]+)""", m => $"new {m.Groups[1].Value}Config\n                        {{\n                            Type = \"{m.Groups[1].Value}\"");
    File.WriteAllText(confPath, content);
}

Console.WriteLine("Done.");
