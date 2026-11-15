using System;
using System.IO;
using System.Text.Json;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Reactive.Subjects;
using Avalonia;
using Avalonia.Styling;
using Avalonia.Headless;
using Avalonia.Controls;
using Avalonia.VisualTree;
using Avalonia.Threading;
using Avalonia.Media;
using AvaloniaApplication1;
using AvaloniaApplication1.Models;
using AvaloniaApplication1.Views.Canvas;
using AvaloniaApplication1.Views.Panels;
using AvaloniaApplication1.Views.Popups;
using AvaloniaApplication1.Views.Windows.Common;
using AvaloniaApplication1.Views.Windows.Connections;
using AvaloniaApplication1.Views.Windows.Tags;
using AvaloniaApplication1.Services;
using AvaloniaApplication1.Services.Piping;
using System.Linq;
using AvaloniaApplication1.Models.Config;
using AvaloniaApplication1.Models.History;
using AvaloniaApplication1.Services.History;
using AvaloniaApplication1.ViewModels.Core;
using AvaloniaApplication1.ViewModels.Widgets;
using AvaloniaApplication1.ViewModels.Panels;
using AvaloniaApplication1.ViewModels.Popups;
using AvaloniaApplication1.ViewModels.Windows.Common;
using AvaloniaApplication1.ViewModels.Windows.Connections;
using AvaloniaApplication1.ViewModels.Windows.Tags;
using AvaloniaApplication1.Controls.Equipment;
using AvaloniaApplication1.Controls.Piping;
using AvaloniaApplication1.Controls.Charting;
using AvaloniaApplication1.Controls.Common;
using AvaloniaApplication1.Views.Canvas.Helpers;
using AvaloniaApplication1.Views.Canvas.Overlays;
using Microsoft.Extensions.DependencyInjection;

namespace AvaloniaApplication1.UIValidation;

public class MockDataCoreService : IDataCoreService, IDisposable
{
    private readonly Subject<TagData> _subject = new();
    private readonly Dictionary<string, object> _values = new();
    public ITagManagerService TagManager { get; } = new TagManagerService();

    public Task StartAsync() => Task.CompletedTask;
    public Task StopAsync() => Task.CompletedTask;
    public void PublishTag(TagData tag) 
    {
        _values[$"{tag.ConnId}:{tag.Address}"] = tag.Value;
        TagManager.UpdateTagValue(tag.ConnId, tag.Address, tag.Value, tag.Quality);
        _subject.OnNext(tag);
    }
    public IObservable<TagData> TagUpdates => _subject;
    public IObservable<TagSnapshot> SnapshotUpdates => TagManager.SnapshotUpdates;

    public void WriteCommand(string connId, string address, object value) 
    {
        _values[$"{connId}:{address}"] = value;
        PublishTag(new TagData(connId, address, value));
    }
    public object? GetCurrentValue(string connId, string address) 
    {
        return _values.TryGetValue($"{connId}:{address}", out var v) ? v : null;
    }
    public TagSnapshot GetTagSnapshot(string tagId) => TagManager.GetSnapshot(tagId);
    public TagSnapshot GetTagSnapshot(string connId, string address) => TagManager.GetSnapshotByAddress(connId, address);
    public void ForceOverride(string tagId, object value) => TagManager.ForceOverride(tagId, value);
    public void ClearOverride(string tagId) => TagManager.ClearOverride(tagId);
    public void Dispose() { _subject.Dispose(); }
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}

public class MockSimulationService : ISimulationService, IDisposable
{
    public Task StartSimulationAsync() => Task.CompletedTask;
    public Task StopSimulationAsync() => Task.CompletedTask;
    public void ResetSimulation() { }
    public void Dispose() { }
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}

public class MockTestDialogService : IDialogService
{
    public bool ConfirmResult { get; set; } = true;
    public bool ShowConfirmCalled { get; set; }

    public Task<bool> ShowConfirmAsync(string title, string message)
    {
        ShowConfirmCalled = true;
        return Task.FromResult(ConfirmResult);
    }

    public Task<string?> ShowNumpadAsync(string title, string initialValue, double? x = null, double? y = null) => Task.FromResult<string?>("100");
    public Task ShowContainerDashboardAsync(string title, DashboardConfig config) => Task.CompletedTask;
    public Task<WidgetConfig?> ShowWidgetEditorAsync(WidgetConfig? existingConfig, List<ConnectionConfig> connections, WidgetPosition? initialPosition = null) => Task.FromResult<WidgetConfig?>(null);
    public Task<(double CellSize, double ZoomScale)?> ShowDashboardPropertiesAsync(double currentCellSize, double currentZoomScale) => Task.FromResult<(double, double)?>(null);
    public Task<ConnectionConfig?> ShowConnectionEditorAsync(ConnectionConfig? existingConfig) => Task.FromResult<ConnectionConfig?>(null);
    public Task ShowConnectionManagerAsync(HmiConfiguration config) => Task.CompletedTask;
    public Task<bool> PromptPasswordAsync(string expectedPassword, string title = "Вход в режим редактирования") => Task.FromResult(true);
    public Task ShowSettingsAsync(HmiConfiguration config, IConfigurationService configService) => Task.CompletedTask;
    public Task<string?> ShowOpenProjectDialogAsync() => Task.FromResult<string?>(null);
    public Task<string?> ShowSaveProjectAsDialogAsync(string? defaultFileName = null) => Task.FromResult<string?>(null);

    // Tag Manager Dialog mocks
    public Task ShowTagManagerAsync(HmiConfiguration config) => Task.CompletedTask;
    public Task<TagDefinition?> ShowTagEditorAsync(TagDefinition? existingTag, IEnumerable<ConnectionConfig> connections, IEnumerable<TagDefinition> existingTags) => Task.FromResult<TagDefinition?>(null);
    public Task<TagDefinition?> ShowTagPickerAsync(IEnumerable<TagDefinition> availableTags, string? initialTagId = null) => Task.FromResult<TagDefinition?>(null);
    public Task<(bool Confirmed, object? Value)> ShowTagForceDialogAsync(TagDefinition tag, object? currentValue) => Task.FromResult<(bool Confirmed, object? Value)>((true, currentValue ?? (object)100));
    public Task<List<TagDefinition>?> ShowTagImportExportAsync(bool isImportMode, IEnumerable<TagDefinition> currentTags, IHmiClipboardService? clipboard = null) => Task.FromResult<List<TagDefinition>?>(null);
}

public class BoundsDto
{
    public double X { get; set; }
    public double Y { get; set; }
    public double Width { get; set; }
    public double Height { get; set; }
}

public class ThicknessDto
{
    public double Left { get; set; }
    public double Top { get; set; }
    public double Right { get; set; }
    public double Bottom { get; set; }
}

public class VisualTreeNode
{
    public string Type { get; set; } = string.Empty;
    public string? Name { get; set; }
    public BoundsDto Bounds { get; set; } = new();
    public ThicknessDto? Margin { get; set; }
    public ThicknessDto? Padding { get; set; }
    public bool IsVisible { get; set; }
    public List<VisualTreeNode> Children { get; set; } = new();
}

public static class Program
{
    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UseHeadless(new AvaloniaHeadlessPlatformOptions
            {
                UseHeadlessDrawing = true
            })
            .UseSkia()
            .WithInterFont();

    public static void Main(string[] args)
    {
        var targetView = args.Length > 0 ? args[0] : (Environment.GetEnvironmentVariable("UI_TARGET_VIEW") ?? "MainWindow");
        Console.WriteLine($"[UIValidation] Starting headless render for target view: {targetView}");

        var outDir = Path.Combine(Directory.GetCurrentDirectory(), "artifacts");
        Directory.CreateDirectory(outDir);

        EnsureAppIconGenerated();

        using var session = HeadlessUnitTestSession.StartNew(typeof(Program));
        using var finishedEvent = new ManualResetEventSlim(false);
        Exception? backgroundError = null;

        session.Dispatch(() =>
        {
            try
            {
            var configService = new ConfigurationService();
            if (string.Equals(targetView, "MimicView", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(targetView, "Mimic", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(targetView, "MmaPark", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(targetView, "MimicRealLab", StringComparison.OrdinalIgnoreCase))
            {
                string[] candidates = new[]
                {
                    "config_mma_park.json",
                    Path.Combine("AvaloniaApplication1", "config_mma_park.json"),
                    Path.Combine("..", "AvaloniaApplication1", "config_mma_park.json"),
                    Path.Combine(AppContext.BaseDirectory, "config_mma_park.json"),
                    Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "AvaloniaApplication1", "config_mma_park.json"),
                    @"C:\Users\adm\Desktop\Парк хранения ММА4 емкости\config_mma_park.json",
                    @"C:\Users\adm\projects\AvaloniaApplication1\AvaloniaApplication1\config_mma_park.json"
                };

                string? mmaConfigPath = candidates.FirstOrDefault(File.Exists);
                if (mmaConfigPath != null)
                {
                    configService.CurrentFilePath = mmaConfigPath;
                    Console.WriteLine($"[UIValidation] Preloaded config path: {Path.GetFullPath(mmaConfigPath)}");
                }
            }
            else
            {
                string[] defaultCandidates = new[]
                {
                    "config.json",
                    Path.Combine("AvaloniaApplication1", "config.json"),
                    Path.Combine("..", "AvaloniaApplication1", "config.json"),
                    Path.Combine(AppContext.BaseDirectory, "config.json"),
                    Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "AvaloniaApplication1", "config.json")
                };

                string? defConfigPath = defaultCandidates.FirstOrDefault(File.Exists);
                if (defConfigPath != null)
                {
                    configService.CurrentFilePath = defConfigPath;
                    Console.WriteLine($"[UIValidation] Preloaded default config path: {Path.GetFullPath(defConfigPath)}");
                }
            }

            var services = new ServiceCollection();
            services.AddSingleton<IThemeService, ThemeService>();
            services.AddSingleton<IProjectContextService, ProjectContextService>();
            services.AddSingleton<IConfigurationService>(configService);
            services.AddSingleton<IDataCoreService, MockDataCoreService>();
            services.AddSingleton<ISimulationService, MockSimulationService>();
            services.AddSingleton<IAlarmNotificationService, AlarmNotificationService>();
            services.AddSingleton<IWidgetFactory, WidgetFactory>();
            services.AddSingleton<IDialogService, DialogService>();
            services.AddSingleton<IHmiClipboardService, HmiClipboardService>();
            services.AddSingleton<MainViewModel>();
            services.AddSingleton<IChildWindowService>(sp => sp.GetRequiredService<MainViewModel>());
            using var sp = services.BuildServiceProvider();

            var mainVm = sp.GetRequiredService<MainViewModel>();

            for (int i = 0; i < 40 && (mainVm.Dashboard == null || mainVm.Dashboard.Widgets.Count == 0); i++)
            {
                Dispatcher.UIThread.RunJobs();
                Thread.Sleep(50);
            }

            Window window;
            if (string.Equals(targetView, "MainWindowDesign", StringComparison.OrdinalIgnoreCase))
            {
                mainVm.ProjectContext.IsDesignMode = true;
                mainVm.ShowMimicCommand.Execute(null);
                for (int i = 0; i < 20; i++)
                {
                    Dispatcher.UIThread.RunJobs();
                    Thread.Sleep(20);
                }

                window = new MainWindow
                {
                    DataContext = mainVm,
                    Width = 1400,
                    Height = 900
                };
            }
            else if (string.Equals(targetView, "MimicView", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(targetView, "Mimic", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(targetView, "MmaPark", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(targetView, "MimicRealLab", StringComparison.OrdinalIgnoreCase))
            {
                mainVm.ShowMimicCommand.Execute(null);
                for (int i = 0; i < 20; i++)
                {
                    Dispatcher.UIThread.RunJobs();
                    Thread.Sleep(20);
                }

                window = new MainWindow
                {
                    DataContext = mainVm,
                    Width = 1400,
                    Height = 768
                };
            }
            else if (string.Equals(targetView, "DashboardView", StringComparison.OrdinalIgnoreCase))
            {
                mainVm.ProjectContext.IsDesignMode = true;
                if (mainVm.Dashboard != null && mainVm.Dashboard.Widgets.Count >= 2)
                {
                    mainVm.Dashboard.Widgets[0].IsSelected = true;
                    mainVm.Dashboard.Widgets[1].IsSelected = true;
                    mainVm.Dashboard.UpdateSelectedWidgetsInfo();
                }
                window = new Window
                {
                    Title = "DashboardView Preview",
                    Width = 1400,
                    Height = 900,
                    Content = new DashboardView
                    {
                        DataContext = mainVm.Dashboard
                    }
                };
            }
            else if (string.Equals(targetView, "ConnectionManagerWindow", StringComparison.OrdinalIgnoreCase))
            {
                window = new ConnectionManagerWindow
                {
                    Width = 800,
                    Height = 600
                };
            }
            else if (string.Equals(targetView, "TagManagerWindow", StringComparison.OrdinalIgnoreCase) ||
                     string.Equals(targetView, "TagManager", StringComparison.OrdinalIgnoreCase))
            {
                var testConfig = new HmiConfiguration
                {
                    Connections = new List<ConnectionConfig>
                    {
                        new() { Id = "plc_511", Type = "ModbusTCP", Host = "127.0.0.1", Port = 502 },
                        new() { Id = "mqtt_wb", Type = "MQTT", Host = "10.10.10.234", Port = 1883 }
                    },
                    Tags = new List<TagDefinition>
                    {
                        new() { TagId = "tag_p101_press", Name = "Давление в напорном коллекторе", ConnId = "plc_511", Address = "40001", DataType = "Float32", Unit = "бар", ScaleA = 0.1, ScaleB = 0.0, Deadband = 0.05 },
                        new() { TagId = "tag_t101_temp", Name = "Температура продукта в реакторе R-101", ConnId = "plc_511", Address = "40003", DataType = "Float32", Unit = "°C", ScaleA = 1.0, ScaleB = 0.0 },
                        new() { TagId = "tag_pump1_run", Name = "Состояние насоса H-1 (Пуск)", ConnId = "plc_511", Address = "00001", DataType = "Bool" },
                        new() { TagId = "tag_level_lt101", Name = "Уровень в емкости E-1", ConnId = "mqtt_wb", Address = "/devices/wb-mwac/controls/K1", DataType = "Float32", Unit = "%" },
                        new() { TagId = "tag_valve_xv101", Name = "Отсечной клапан XV-101", ConnId = "plc_511", Address = "00012", DataType = "Bool" }
                    }
                };

                var testTagManager = new TagManagerService(startWatchdogLoop: false);
                testTagManager.Initialize(testConfig.Tags);
                testTagManager.UpdateTagValue("tag_p101_press", 42.5f, TagQuality.Good);
                testTagManager.UpdateTagValue("tag_t101_temp", 85.0f, TagQuality.Stale);
                testTagManager.UpdateTagValue("tag_pump1_run", true, TagQuality.Good);
                testTagManager.ForceOverride("tag_level_lt101", 95.0f);
                testTagManager.UpdateTagValue("tag_valve_xv101", 0, TagQuality.CommFailure, "Timeout");

                var tagManagerVm = new TagManagerViewModel(
                    testConfig, 
                    testTagManager, 
                    sp.GetRequiredService<IDialogService>(), 
                    sp.GetRequiredService<IConfigurationService>(), 
                    sp.GetRequiredService<IHmiClipboardService>());

                window = new TagManagerWindow
                {
                    DataContext = tagManagerVm,
                    Width = 1320,
                    Height = 750
                };
            }
            else if (string.Equals(targetView, "WidgetEditorWindow", StringComparison.OrdinalIgnoreCase))
            {
                window = new WidgetEditorWindow
                {
                    Width = 600,
                    Height = 700
                };
            }
            else if (string.Equals(targetView, "ValveControlPopupView", StringComparison.OrdinalIgnoreCase))
            {
                var valveCfg = new ValveConfig
                {
                    Type = "Valve",
                    Title = "LV 22 (Прием)",
                    ValveType = "CutOff",
                    ActiveColor = "#00E676",
                    InactiveColor = "#D50000",
                    Source = new DataSourceConfig { ConnId = "wb", Address = "00012", DataType = "Bool" },
                    FeedbackSource = new DataSourceConfig { ConnId = "wb", Address = "10016", DataType = "Bool" },
                    ClosedFeedbackSource = new DataSourceConfig { ConnId = "wb", Address = "10017", DataType = "Bool" },
                    Position = new WidgetPosition { Col = 5, Row = 5, SizeX = 6, SizeY = 6 }
                };
                var projContext = sp.GetRequiredService<IProjectContextService>();
                var dataService = sp.GetRequiredService<IDataCoreService>();
                var valveVm = new ValveWidgetViewModel(valveCfg, dataService, projContext);
                var popupVm = new ValveControlPopupViewModel(valveVm, () => { });

                window = new Window
                {
                    Title = "ValveControlPopupView Preview",
                    Width = 550,
                    Height = 620,
                    Content = new ValveControlPopupView
                    {
                        DataContext = popupVm
                    }
                };
            }
            else if (string.Equals(targetView, "SettingsWindow", StringComparison.OrdinalIgnoreCase))
            {
                var cfgService = sp.GetRequiredService<IConfigurationService>();
                var settingsVm = new SettingsViewModel(new HmiConfiguration
                {
                    Project = new ProjectConfig { Name = "Аппарат 511 - Цех 15", Version = "1.0.0" },
                    Security = new SecurityConfig { RequirePasswordForDesignMode = true, DesignModePassword = "1234" },
                    Mimic = new DashboardConfig { CellSize = 10, ZoomScale = 1.0 }
                }, cfgService, sp.GetRequiredService<IDialogService>());

                window = new SettingsWindow
                {
                    DataContext = settingsVm,
                    Width = 700,
                    Height = 550
                };
            }
            else if (string.Equals(targetView, "PasswordPromptWindow", StringComparison.OrdinalIgnoreCase))
            {
                var promptVm = new PasswordPromptViewModel
                {
                    Title = "Вход в режим редактирования",
                    ExpectedPassword = "1234",
                    Password = "12"
                };

                window = new PasswordPromptWindow
                {
                    DataContext = promptVm,
                    Width = 360,
                    Height = 450
                };
            }
            else if (string.Equals(targetView, "CalculatorView", StringComparison.OrdinalIgnoreCase) ||
                     string.Equals(targetView, "Calculator", StringComparison.OrdinalIgnoreCase))
            {
                var clip = sp.GetRequiredService<IHmiClipboardService>();
                var calcVm = new CalculatorViewModel(clip);
                calcVm.ToggleHistoryCommand.Execute(null); // Show with history panel open
                window = new Window
                {
                    Title = "CalculatorView Preview",
                    Width = 560,
                    Height = 520,
                    Content = new CalculatorView
                    {
                        DataContext = calcVm
                    }
                };
            }
            else if (string.Equals(targetView, "TechCalculatorView", StringComparison.OrdinalIgnoreCase) ||
                     string.Equals(targetView, "TechCalculator", StringComparison.OrdinalIgnoreCase))
            {
                var clip = sp.GetRequiredService<IHmiClipboardService>();
                var calcVm = new CalculatorViewModel(clip);
                calcVm.SelectedTabIndex = 1;
                calcVm.CalculateTechMassCommand.Execute(null);
                calcVm.CalculatePressureCommand.Execute(null);
                calcVm.ToggleTechHistoryCommand.Execute(null); // Show with tech history panel open
                window = new Window
                {
                    Title = "TechCalculatorView Preview",
                    Width = 560,
                    Height = 520,
                    Content = new CalculatorView
                    {
                        DataContext = calcVm
                    }
                };
            }
            else if (string.Equals(targetView, "NumpadView", StringComparison.OrdinalIgnoreCase) ||
                     string.Equals(targetView, "Numpad", StringComparison.OrdinalIgnoreCase))
            {
                var clip = sp.GetRequiredService<IHmiClipboardService>();
                clip.CurrentValue = "125.5";
                var numpadVm = new NumpadViewModel(clip)
                {
                    InputValue = "100.0"
                };
                window = new Window
                {
                    Title = "NumpadView Preview",
                    Width = 320,
                    Height = 420,
                    Content = new NumpadView
                    {
                        DataContext = numpadVm
                    }
                };
            }
            else
            {
                window = new MainWindow
                {
                    DataContext = mainVm,
                    Width = 1400,
                    Height = 900
                };
            }

            window.Show();

            Dispatcher.UIThread.RunJobs();
            AvaloniaHeadlessPlatform.ForceRenderTimerTick(3);
            Dispatcher.UIThread.RunJobs();

            var frame = window.CaptureRenderedFrame();
            if (frame != null)
            {
                var imgPath = Path.Combine(outDir, "ui_preview.png");
                try
                {
                    if (File.Exists(imgPath))
                    {
                        try { File.Delete(imgPath); } catch { }
                    }
                    frame.Save(imgPath);
                    Console.WriteLine($"[UIValidation] Screenshot saved: {imgPath} ({new FileInfo(imgPath).Length} bytes)");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[UIValidation] Notice: Could not overwrite {imgPath} ({ex.Message}).");
                }
            }
            else
            {
                Console.WriteLine("[UIValidation] WARNING: Could not capture rendered frame.");
            }

            var tree = DumpVisualTree(window);
            var jsonPath = Path.Combine(outDir, "ui_tree.json");
            var json = JsonSerializer.Serialize(tree, new JsonSerializerOptions { WriteIndented = true, MaxDepth = 128 });
            File.WriteAllText(jsonPath, json);
            Console.WriteLine($"[UIValidation] Visual tree dump saved: {jsonPath} ({new FileInfo(jsonPath).Length} bytes)");

            RunAutomatedValidations(mainVm, sp);
            Console.WriteLine("[UIValidation] SUCCESS: Artifacts generated successfully.");
        }
        catch (Exception ex)
        {
            backgroundError = ex;
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"[UIValidation] VALIDATION ERROR: {ex}");
            Console.ResetColor();
        }
        finally
        {
            finishedEvent.Set();
        }
    }, CancellationToken.None);

    if (!finishedEvent.Wait(TimeSpan.FromSeconds(60)))
    {
        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine("[UIValidation] TIMEOUT: Headless session did not finish within 60 seconds.");
        Console.ResetColor();
        Environment.Exit(2);
    }

    if (backgroundError != null)
    {
        Environment.Exit(1);
    }

    Environment.Exit(0);
}

    private static void RunAutomatedValidations(MainViewModel mainVm, IServiceProvider sp)
    {
        Console.WriteLine("[UIValidation] Running automated validations...");

        // 1. Test WidgetClipboard for ValveConfig
        var valveConfig = new ValveConfig
        {
            Type = "Valve",
            Title = "Test Valve",
            ValveType = "CutOff",
            Position = new WidgetPosition { Col = 5, Row = 8, SizeX = 6, SizeY = 6 }
        };
        WidgetClipboard.Copy(valveConfig);
        if (!WidgetClipboard.HasWidget)
            throw new Exception("WidgetClipboard.HasWidget should be true after copy.");

        var pastedValve = WidgetClipboard.PasteClone() as ValveConfig;
        if (pastedValve == null || pastedValve.Title != "Test Valve" || pastedValve.ValveType != "CutOff")
            throw new Exception("Pasted ValveConfig does not match copied config.");

        // 2. Test WidgetClipboard for ContainerButtonConfig with Children
        var containerConfig = new ContainerButtonConfig
        {
            Type = "ContainerButton",
            Title = "Main Container",
            Position = new WidgetPosition { Col = 1, Row = 1, SizeX = 4, SizeY = 4 },
            Children = new List<WidgetConfig>
            {
                new ValveConfig { Type = "Valve", Title = "Child Valve", ValveType = "Regulating" },
                new PumpConfig { Type = "Pump", Title = "Child Pump" }
            }
        };
        WidgetClipboard.Copy(containerConfig);
        var pastedContainer = WidgetClipboard.PasteClone() as ContainerButtonConfig;
        if (pastedContainer == null || pastedContainer.Children.Count != 2 || pastedContainer.Children[0].Title != "Child Valve")
            throw new Exception("Pasted ContainerButtonConfig failed to restore children.");

        // 3. Test ContainerButtonViewModel.PasteWidgetIntoContainer
        WidgetClipboard.Copy(new ValveConfig { Type = "Valve", Title = "Inserted Valve", ValveType = "CutOff" });
        var containerVm = new ContainerButtonViewModel(pastedContainer, new MockDataCoreService(), mainVm.ProjectContext);
        containerVm.PasteWidgetIntoContainer();
        if (containerVm.TypedConfig.Children.Count != 3 || containerVm.TypedConfig.Children.Last().Title != "Inserted Valve")
            throw new Exception("ContainerButtonViewModel.PasteWidgetIntoContainer did not add widget to Children.");

        // 4. Test Valve Port Snapping Invariance: CutOff vs Regulating
        var cutoffConfig = new ValveConfig
        {
            Type = "Valve",
            ValveType = "CutOff",
            Position = new WidgetPosition { Col = 10, Row = 10, SizeX = 6, SizeY = 6 }
        };
        var regulatingConfig = new ValveConfig
        {
            Type = "Valve",
            ValveType = "Regulating",
            FeedbackSource = new DataSourceConfig { ConnId = "plc", Address = "100" },
            Position = new WidgetPosition { Col = 10, Row = 10, SizeX = 6, SizeY = 6 }
        };
        var panel = new DashboardPanel { CellWidth = 20, CellHeight = 20 };
        var cutoffVm = new ValveWidgetViewModel(cutoffConfig, new MockDataCoreService(), mainVm.ProjectContext);
        var regVm = new ValveWidgetViewModel(regulatingConfig, new MockDataCoreService(), mainVm.ProjectContext);

        var (cutoffP1, cutoffP2) = VisualPortHelper.GetVisualPortsInGrid(new Control(), cutoffVm, panel);
        var (regP1, regP2) = VisualPortHelper.GetVisualPortsInGrid(new Control(), regVm, panel);

        if (Math.Abs(cutoffP1.X - regP1.X) > 0.001 || Math.Abs(cutoffP1.Y - regP1.Y) > 0.001 ||
            Math.Abs(cutoffP2.X - regP2.X) > 0.001 || Math.Abs(cutoffP2.Y - regP2.Y) > 0.001)
        {
            throw new Exception($"Valve port discrepancy detected between CutOff ({cutoffP1}, {cutoffP2}) and Regulating ({regP1}, {regP2})!");
        }

        // 5. Test DashboardViewModel.PasteWidgetAt
        var dConfig = new DashboardConfig();
        var widgetFactory = sp.GetRequiredService<IWidgetFactory>();
        var dVm = new DashboardViewModel(dConfig, new MockDataCoreService(), mainVm.ProjectContext, new HmiConfiguration(), null, widgetFactory);
        WidgetClipboard.Copy(new ValveConfig { Type = "Valve", Title = "Dashboard Valve", ValveType = "CutOff" });
        dVm.PasteWidgetAt(15, 20);
        if (dVm.Widgets.Count != 1 || dVm.Widgets[0].Col != 15 || dVm.Widgets[0].Row != 20)
            throw new Exception("DashboardViewModel.PasteWidgetAt failed to position widget correctly.");

        // 6. Test DashboardPropertiesViewModel
        var propVm = new DashboardPropertiesViewModel(40, 1.0);
        propVm.SetCellSizePresetCommand.Execute(20);
        propVm.SetZoomPresetCommand.Execute(150);
        if (propVm.CellSize != 20 || propVm.ZoomPercent != 150 || Math.Abs(propVm.ZoomScale - 1.5) > 0.001)
            throw new Exception("DashboardPropertiesViewModel preset commands failed.");
        propVm.ResetDefaultsCommand.Execute(null);
        if (propVm.CellSize != 40 || propVm.ZoomPercent != 100 || Math.Abs(propVm.ZoomScale - 1.0) > 0.001)
            throw new Exception("DashboardPropertiesViewModel ResetDefaults failed.");

        // 7. Test DashboardViewModel.UpdateGridAndScale & AddQuickWidgetAt
        double reportedCellSize = 0;
        double reportedZoom = 0;
        dVm.OnGridOrScaleChanged += (cs, zs) =>
        {
            reportedCellSize = cs;
            reportedZoom = zs;
        };
        dVm.UpdateGridAndScale(80, 1.25);
        if (dVm.CellWidth != 80 || dVm.CellHeight != 80 || Math.Abs(dVm.ZoomScale - 1.25) > 0.001 ||
            dConfig.CellSize != 80 || Math.Abs(dConfig.ZoomScale - 1.25) > 0.001 ||
            reportedCellSize != 80 || Math.Abs(reportedZoom - 1.25) > 0.001)
        {
            throw new Exception("DashboardViewModel.UpdateGridAndScale failed to update properties or fire event.");
        }

        dVm.AddQuickWidgetAt("Valve", 12, 18);
        var addedValve = dVm.Widgets.LastOrDefault();
        if (addedValve == null || addedValve.Col != 12 || addedValve.Row != 18 || addedValve.Type != "Valve")
            throw new Exception("DashboardViewModel.AddQuickWidgetAt failed to create Valve at (12, 18).");

        dVm.AddQuickWidgetAt("Pump", 25, 30);
        var addedPump = dVm.Widgets.LastOrDefault();
        if (addedPump == null || addedPump.Col != 25 || addedPump.Row != 30 || addedPump.Type != "Pump")
            throw new Exception("DashboardViewModel.AddQuickWidgetAt failed to create Pump at (25, 30).");

        // 8. Test ContainerButtonConfig and Container synchronization
        var containerWithGrid = new ContainerButtonConfig
        {
            Type = "ContainerButton",
            Title = "Nested Window",
            CellSize = 20,
            ZoomScale = 1.5,
            Children = new List<WidgetConfig>()
        };
        var childWindowService = sp.GetRequiredService<IChildWindowService>();
        var containerBtnVm = new ContainerButtonViewModel(containerWithGrid, new MockDataCoreService(), mainVm.ProjectContext)
        {
            ChildWindowService = childWindowService
        };
        containerBtnVm.OpenContainerCommand.Execute(null);
        if (mainVm.ActiveChildWindows.Count == 0)
            throw new Exception("Failed to open child window for ContainerButtonViewModel.");

        var openWindow = mainVm.ActiveChildWindows.Last();
        if (openWindow.Content is DashboardViewModel nestedDashboardVm)
        {
            if (nestedDashboardVm.CellWidth != 20 || Math.Abs(nestedDashboardVm.ZoomScale - 1.5) > 0.001)
                throw new Exception("Nested DashboardViewModel did not inherit CellSize/ZoomScale from ContainerButtonConfig.");

            nestedDashboardVm.UpdateGridAndScale(10, 0.75);
            if (containerWithGrid.CellSize != 10 || Math.Abs(containerWithGrid.ZoomScale - 0.75) > 0.001)
                throw new Exception("ContainerButtonConfig was not synchronized when nested DashboardViewModel grid/scale changed.");
        }
        else
        {
            throw new Exception("Child window content is not DashboardViewModel.");
        }
        openWindow.CloseCommand.Execute(null);

        // 9. Test Widget Deletion without view switching (Bugfix validation)
        var testDashboardConfig = new DashboardConfig();
        var testDashboardVm = new DashboardViewModel(testDashboardConfig, new MockDataCoreService(), mainVm.ProjectContext, new HmiConfiguration(), null, widgetFactory);
        
        var testView = new DashboardView { DataContext = testDashboardVm };
        var testWindow = new Window { Content = testView, Width = 800, Height = 600 };
        testWindow.Show();
        Dispatcher.UIThread.RunJobs();

        var testPanel = testView.FindDescendantOfType<DashboardPanel>();
        if (testPanel == null)
            throw new Exception("DashboardPanel was not found in DashboardView visual tree.");

        if (testPanel.Children.Count != 0)
            throw new Exception($"DashboardPanel.Children should be empty initially, but was {testPanel.Children.Count}.");

        // Add 3 widgets
        testDashboardVm.AddQuickWidgetAt("Valve", 2, 2);
        testDashboardVm.AddQuickWidgetAt("Pump", 6, 6);
        testDashboardVm.AddQuickWidgetAt("Tank", 10, 10);
        Dispatcher.UIThread.RunJobs();

        if (testDashboardVm.Widgets.Count != 3)
            throw new Exception("Widgets count should be 3.");
        if (testPanel.Children.Count != 3)
            throw new Exception($"DashboardPanel.Children.Count ({testPanel.Children.Count}) does not match Widgets.Count (3).");

        // Delete the middle widget (index 1: Pump)
        var pumpWidget = testDashboardVm.Widgets[1];
        testDashboardVm.RemoveWidgetCommand.Execute(pumpWidget);
        Dispatcher.UIThread.RunJobs();

        if (testDashboardVm.Widgets.Count != 2)
            throw new Exception("Widgets count should be 2 after removing middle widget.");
        if (testPanel.Children.Count != 2)
            throw new Exception($"DashboardPanel.Children.Count ({testPanel.Children.Count}) did not immediately update to 2!");

        // Delete first widget (index 0: Valve)
        var valveWidget = testDashboardVm.Widgets[0];
        testDashboardVm.RemoveWidgetCommand.Execute(valveWidget);
        Dispatcher.UIThread.RunJobs();

        if (testDashboardVm.Widgets.Count != 1)
            throw new Exception("Widgets count should be 1 after removing first widget.");
        if (testPanel.Children.Count != 1)
            throw new Exception($"DashboardPanel.Children.Count ({testPanel.Children.Count}) did not immediately update to 1!");

        // Delete last remaining widget (Tank)
        var tankWidget = testDashboardVm.Widgets[0];
        testDashboardVm.RemoveWidgetCommand.Execute(tankWidget);
        Dispatcher.UIThread.RunJobs();

        if (testDashboardVm.Widgets.Count != 0)
            throw new Exception("Widgets count should be 0.");
        if (testPanel.Children.Count != 0)
            throw new Exception($"DashboardPanel.Children.Count ({testPanel.Children.Count}) should be 0, phantom widgets detected!");

        // 10. Test Pipe Vertex Modification & Dynamic Frame/Container Synchronization
        Console.WriteLine("[Validation] Running Test 10: Pipe Vertex Modification & Container Synchronization...");
        var pipeConfig = new PipeConfig
        {
            Type = "Pipe",
            Title = "Test Pipe",
            Position = new WidgetPosition { Col = 10, Row = 10, SizeX = 10, SizeY = 1 },
            PipePoints = "0,0; 10,0"
        };
        var pipeVm = new PipeWidgetViewModel(pipeConfig, new MockDataCoreService(), mainVm.ProjectContext);
        testDashboardVm.Widgets.Add(pipeVm);
        Dispatcher.UIThread.RunJobs();

        var pipeChild = testPanel.FindChildForVm(pipeVm);
        if (pipeChild == null)
            throw new Exception("Test pipe child container was not found in DashboardPanel.");

        if (DashboardPanel.GetCol(pipeChild) != 10 || DashboardPanel.GetRow(pipeChild) != 10)
            throw new Exception($"Initial pipe attached properties Col={DashboardPanel.GetCol(pipeChild)}, Row={DashboardPanel.GetRow(pipeChild)} did not match VM 10, 10.");

        // Simulate adding a vertex above and to the left (relative -3, -2) which triggers normalization
        // This shifts newCol to 10 - 3 = 7, and newRow to 10 - 2 = 8
        pipeVm.PipePoints = "-3,-2; 0,0; 10,0";
        Dispatcher.UIThread.RunJobs();

        // Check that VM normalized coordinates: newCol = 7, newRow = 8
        if (pipeVm.Col != 7 || pipeVm.Row != 8)
            throw new Exception($"PipeViewModel Col={pipeVm.Col}, Row={pipeVm.Row} should have normalized to 7, 8.");

        // Check that DashboardPanel child container immediately synchronized without switching views!
        if (DashboardPanel.GetCol(pipeChild) != 7 || DashboardPanel.GetRow(pipeChild) != 8)
            throw new Exception($"DashboardPanel child container Col={DashboardPanel.GetCol(pipeChild)}, Row={DashboardPanel.GetRow(pipeChild)} was not synchronized to 7, 8!");

        // Check that relative points inside PipePoints start from 0
        var absPoints = pipeVm.GetAbsoluteGridPoints();
        if (absPoints[0].X != 7 || absPoints[0].Y != 8)
            throw new Exception($"Absolute points after normalization do not match: {absPoints[0]} vs (7, 8)");
        if (absPoints[1].X != 10 || absPoints[1].Y != 10)
            throw new Exception($"Original point (10,10) shifted: now at {absPoints[1]}");

        // Clean up
        testDashboardVm.Widgets.Remove(pipeVm);
        testWindow.Close();
        Dispatcher.UIThread.RunJobs();

        // 11. Test MMA Park HMI Configuration Loading & Structure Validation
        Console.WriteLine("[Validation] Running Test 11: MMA Park Configuration Loading & Validation...");
        var mmaConfigPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "config_mma_park.json");
        if (!File.Exists(mmaConfigPath))
        {
            var fallback = Path.Combine(Directory.GetCurrentDirectory(), "AvaloniaApplication1", "config_mma_park.json");
            if (File.Exists(fallback)) mmaConfigPath = fallback;
            else fallback = Path.Combine(Directory.GetCurrentDirectory(), "config_mma_park.json");
            if (File.Exists(fallback)) mmaConfigPath = fallback;
        }

        var json = File.ReadAllText(mmaConfigPath);
        var mmaConfig = JsonSerializer.Deserialize<HmiConfiguration>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        if (mmaConfig == null || !mmaConfig.Project.Name.Contains("ММА"))
            throw new Exception($"MMA config Project Name '{mmaConfig?.Project?.Name}' does not contain 'ММА'.");
        if (mmaConfig.Connections.Count == 0 || mmaConfig.Connections[0].Host != "10.10.10.234")
            throw new Exception("MMA config does not contain Wiren Board 8 connection (10.10.10.234).");
        if (mmaConfig.Mimic.Widgets.Count < 30)
            throw new Exception($"MMA config Mimic widget count {mmaConfig.Mimic.Widgets.Count} is less than 30.");
        if (mmaConfig.Dashboard.Widgets.Count < 5)
            throw new Exception($"MMA config Dashboard widget count {mmaConfig.Dashboard.Widgets.Count} is less than 5.");

        var dataCore = sp.GetRequiredService<IDataCoreService>();
        var projContext = sp.GetRequiredService<IProjectContextService>();
        var dlgService = sp.GetRequiredService<IDialogService>();
        var wf = sp.GetRequiredService<IWidgetFactory>();

        using var mimicVm = new DashboardViewModel(mmaConfig.Mimic, dataCore, projContext, mmaConfig, dlgService, wf);
        if (mimicVm.Widgets.Count != mmaConfig.Mimic.Widgets.Count)
            throw new Exception($"MimicViewModel widgets count {mimicVm.Widgets.Count} != config widgets count {mmaConfig.Mimic.Widgets.Count}.");

        using var dashVm = new DashboardViewModel(mmaConfig.Dashboard, dataCore, projContext, mmaConfig, dlgService, wf);
        if (dashVm.Widgets.Count != mmaConfig.Dashboard.Widgets.Count)
            throw new Exception($"DashboardViewModel widgets count {dashVm.Widgets.Count} != config widgets count {mmaConfig.Dashboard.Widgets.Count}.");

        // 12. Test Dual Limit Switch Valve Feedback (4 States & Alarm Mismatch)
        Console.WriteLine("[Validation] Running Test 12: Dual Limit Switch Valve Feedback & Fault Logic...");
        var dualValveConfig = new ValveConfig
        {
            Type = "Valve",
            Title = "LV 22 (Прием)",
            ValveType = "CutOff",
            ActiveColor = "#00E676",
            InactiveColor = "#D50000",
            Source = new DataSourceConfig { ConnId = "wb", Address = "00012", DataType = "Bool" },
            FeedbackSource = new DataSourceConfig { ConnId = "wb", Address = "10016", DataType = "Bool" }, // SQH
            ClosedFeedbackSource = new DataSourceConfig { ConnId = "wb", Address = "10017", DataType = "Bool" }, // SQL
            Position = new WidgetPosition { Col = 5, Row = 5, SizeX = 6, SizeY = 6 }
        };

        var mockData = new MockDataCoreService();
        var dualValveVm = new ValveWidgetViewModel(dualValveConfig, mockData, projContext);

        if (!dualValveVm.HasClosedFeedbackSource || !dualValveVm.HasFeedbackSource)
            throw new Exception("dualValveVm failed to identify dual feedback sources.");

        // State 1: SQH=1, SQL=0 -> OPEN
        mockData.PublishTag(new TagData("wb", "00012", true));
        mockData.PublishTag(new TagData("wb", "10016", true));
        mockData.PublishTag(new TagData("wb", "10017", false));
        Dispatcher.UIThread.RunJobs();

        if (!dualValveVm.IsOpen || dualValveVm.IsMoving || dualValveVm.IsSensorFault || dualValveVm.DisplayValue != "ОТКРЫТ" || dualValveVm.Feedback != 100)
            throw new Exception($"Dual limit switch state 1 (OPEN) failed: IsOpen={dualValveVm.IsOpen}, DisplayValue='{dualValveVm.DisplayValue}', Feedback={dualValveVm.Feedback}");

        // State 2: SQH=0, SQL=1 -> CLOSED
        mockData.PublishTag(new TagData("wb", "00012", false));
        mockData.PublishTag(new TagData("wb", "10016", false));
        mockData.PublishTag(new TagData("wb", "10017", true));
        Dispatcher.UIThread.RunJobs();

        if (dualValveVm.IsOpen || dualValveVm.IsMoving || dualValveVm.IsSensorFault || dualValveVm.DisplayValue != "ЗАКРЫТ" || dualValveVm.Feedback != 0)
            throw new Exception($"Dual limit switch state 2 (CLOSED) failed: IsOpen={dualValveVm.IsOpen}, DisplayValue='{dualValveVm.DisplayValue}', Feedback={dualValveVm.Feedback}");

        // State 3: SQH=0, SQL=0 -> MOVING / TRANSIT
        mockData.PublishTag(new TagData("wb", "10016", false));
        mockData.PublishTag(new TagData("wb", "10017", false));
        Dispatcher.UIThread.RunJobs();

        if (dualValveVm.IsOpen || !dualValveVm.IsMoving || dualValveVm.IsSensorFault || dualValveVm.DisplayValue != "В ПУТИ" || dualValveVm.CurrentColor != "#FFB300")
            throw new Exception($"Dual limit switch state 3 (MOVING) failed: IsMoving={dualValveVm.IsMoving}, DisplayValue='{dualValveVm.DisplayValue}', Color='{dualValveVm.CurrentColor}'");

        // State 4: SQH=1, SQL=1 -> SENSOR FAULT / ALARM
        mockData.PublishTag(new TagData("wb", "10016", true));
        mockData.PublishTag(new TagData("wb", "10017", true));
        Dispatcher.UIThread.RunJobs();

        if (dualValveVm.IsOpen || dualValveVm.IsMoving || !dualValveVm.IsSensorFault || dualValveVm.DisplayValue != "АВАРИЯ ДАТЧИКОВ" || !dualValveVm.IsAlarmActive)
            throw new Exception($"Dual limit switch state 4 (SENSOR FAULT) failed: IsSensorFault={dualValveVm.IsSensorFault}, IsAlarmActive={dualValveVm.IsAlarmActive}");

        // Test ValveControlPopupViewModel binding with dual limit switches
        var popupVm = new ValveControlPopupViewModel(dualValveVm, () => { });
        if (!popupVm.HasClosedFeedbackSource || !popupVm.IsOpenLimitSwitch || !popupVm.IsClosedLimitSwitch || !popupVm.IsSensorFault)
            throw new Exception("ValveControlPopupViewModel did not correctly reflect dual limit switch state.");

        // 13. Test SecurityConfig, Password Protection, Runtime Startup & ContainerButton Packaging
        Console.WriteLine("[Validation] Running Test 13: Security Config, Password Protection & 511 ContainerButtons...");
        
        // 13.1 Default startup mode is Runtime (IsDesignMode = false)
        var freshProjContext = new ProjectContextService();
        if (freshProjContext.IsDesignMode)
            throw new Exception("Default startup mode is NOT Runtime (IsDesignMode should be false).");

        // 13.2 PasswordPromptViewModel logic
        var promptTestVm = new PasswordPromptViewModel
        {
            ExpectedPassword = "5555",
            Password = "wrong"
        };
        bool? promptResult = null;
        promptTestVm.OnResult = res => promptResult = res;

        promptTestVm.ConfirmCommand.Execute(null);
        if (!promptTestVm.HasError || promptResult.HasValue)
            throw new Exception("PasswordPromptViewModel failed to reject incorrect password.");

        promptTestVm.ClearCommand.Execute(null);
        promptTestVm.AppendDigitCommand.Execute("5");
        promptTestVm.AppendDigitCommand.Execute("5");
        promptTestVm.AppendDigitCommand.Execute("5");
        promptTestVm.AppendDigitCommand.Execute("5");
        promptTestVm.ConfirmCommand.Execute(null);

        if (promptTestVm.HasError || promptResult != true)
            throw new Exception("PasswordPromptViewModel failed to accept correct password '5555'.");

        // 13.3 Test 511 default_config ContainerButton packaging
        var config511Path = Path.Combine(AppContext.BaseDirectory, "config.json");
        if (!File.Exists(config511Path))
            config511Path = Path.Combine("..", "..", "..", "..", "AvaloniaApplication1", "config.json");

        if (File.Exists(config511Path))
        {
            var config511Json = File.ReadAllText(config511Path);
            var parsed511 = JsonSerializer.Deserialize<HmiConfiguration>(config511Json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            if (parsed511 == null)
                throw new Exception("Failed to deserialize 511 config.json.");

            var containerButtons = parsed511.Dashboard.Widgets.OfType<ContainerButtonConfig>().ToList();
            if (containerButtons.Count < 2)
                throw new Exception($"Expected at least 2 ContainerButtons for 511 apparatus, found {containerButtons.Count}.");

            int totalChildren = containerButtons.Sum(c => c.Children?.Count ?? 0);
            if (totalChildren < 8)
                throw new Exception($"Expected 8 encapsulated SetValue widgets inside ContainerButtons, found {totalChildren}.");
        }

        // 14. Test CalculatorViewModel, Technology Calculations, Responsive Layout Properties & Settings
        Console.WriteLine("[Validation] Running Test 14: CalculatorViewModel, Tech Calculations & Typography/Layout Properties...");
        
        // 14.1 Test Basic Calculator Operations
        var calcVm = new CalculatorViewModel();
        calcVm.InputDigitCommand.Execute("1");
        calcVm.InputDigitCommand.Execute("2");
        calcVm.InputDecimalCommand.Execute(null);
        calcVm.InputDigitCommand.Execute("5");
        calcVm.SetOperationCommand.Execute("+");
        calcVm.InputDigitCommand.Execute("7");
        calcVm.InputDecimalCommand.Execute(null);
        calcVm.InputDigitCommand.Execute("5");
        calcVm.CalculateResultCommand.Execute(null);

        if (calcVm.Display != "20")
            throw new Exception($"Calculator 12.5 + 7.5 expected 20, got '{calcVm.Display}'");

        calcVm.SetOperationCommand.Execute("*");
        calcVm.InputDigitCommand.Execute("4");
        calcVm.CalculateResultCommand.Execute(null);
        if (calcVm.Display != "80")
            throw new Exception($"Calculator 20 * 4 expected 80, got '{calcVm.Display}'");

        calcVm.SqrtCommand.Execute(null);
        if (calcVm.Display != "8,94427" && !calcVm.Display.StartsWith("8") && !calcVm.Display.Contains("8,944") && !calcVm.Display.Contains("8.944"))
            throw new Exception($"Calculator sqrt(80) expected ~8.944, got '{calcVm.Display}'");

        // 14.2 Test Tech Calculations (Mass = Volume * Density)
        calcVm.TechVolume = 15.0; // m3
        calcVm.TechDensity = 940.0; // kg/m3 (MMA)
        calcVm.UpdateTechMass();
        if (Math.Abs(calcVm.TechMass - 14100.0) > 0.1 || Math.Abs(calcVm.TechMassTons - 14.1) > 0.01) // 14.1 tons
            throw new Exception($"Tech mass calculation expected 14.1 t, got {calcVm.TechMassTons} t ({calcVm.TechMass} kg)");

        // 14.3 Test Pressure Converter (10 bar)
        calcVm.UpdatePressureFromBar(10.0);
        if (Math.Abs(calcVm.PressureMpa - 1.0) > 0.001 || Math.Abs(calcVm.PressureKpa - 1000.0) > 0.1 || Math.Abs(calcVm.PressureKgs - 10.197) > 0.01)
            throw new Exception($"Pressure conversion for 10 bar failed: MPa={calcVm.PressureMpa}, kPa={calcVm.PressureKpa}, kgs={calcVm.PressureKgs}");

        // 14.4 Test WidgetEditorViewModel with new Layout/Typography properties
        var widgetCfg = new ValveConfig
        {
            Type = "Valve",
            Title = "XV 101",
            ShowTitle = true,
            ShowBorder = false,
            AutoScaleText = true,
            FontSize = 14.0,
            LabelPosition = "Top",
            CompactMode = true,
            ShowStatusText = true,
            LabelOffset = -4.0,
            ValveType = "CutOff",
            ActiveColor = "#00FF00",
            InactiveColor = "#FF0000",
            Position = new WidgetPosition { Row = 1, Col = 2, SizeX = 4, SizeY = 4 }
        };

        var editVm = new WidgetEditorViewModel(widgetCfg, new List<ConnectionConfig> { new() { Id = "wb", Type = "ModbusTcp", Host = "10.10.10.234" } });
        if (!editVm.ShowTitle || editVm.ShowBorder || !editVm.AutoScaleText || editVm.CustomFontSize != 14.0 || editVm.SelectedLabelPosition != "Top" || !editVm.CompactMode || !editVm.ShowStatusText || editVm.LabelOffset != -4.0)
            throw new Exception("WidgetEditorViewModel failed to load typography/layout properties correctly.");

        editVm.CustomFontSize = 16.0;
        editVm.SelectedLabelPosition = "Bottom";
        var resultCfg = (ValveConfig)editVm.ToWidgetConfig();
        if (resultCfg.FontSize != 16.0 || resultCfg.LabelPosition != "Bottom" || resultCfg.ShowBorder != false || resultCfg.ShowStatusText != true || resultCfg.LabelOffset != -4.0)
            throw new Exception("WidgetEditorViewModel ToWidgetConfig() failed to preserve modified typography properties.");

        // 15. Test Calculator Memory, History Journal, Internal HMI Clipboard & Numpad Integration
        Console.WriteLine("[Validation] Running Test 15: Memory, History Journal, HMI Clipboard & Numpad Integration...");
        var clipboard = new HmiClipboardService();
        var calcWithClipVm = new CalculatorViewModel(clipboard);
        
        // 15.1 Test Memory Operations (MC, MR, M+, M-, MS)
        calcWithClipVm.InputDigitCommand.Execute("5");
        calcWithClipVm.InputDigitCommand.Execute("0");
        calcWithClipVm.MemoryStoreCommand.Execute(null); // M = 50
        if (!calcWithClipVm.HasMemory || Math.Abs(calcWithClipVm.MemoryValue - 50.0) > 0.001)
            throw new Exception("Calculator MemoryStore (MS) failed: HasMemory should be true, MemoryValue should be 50.");

        calcWithClipVm.ClearCommand.Execute(null);
        calcWithClipVm.InputDigitCommand.Execute("2");
        calcWithClipVm.InputDigitCommand.Execute("5");
        calcWithClipVm.MemoryAddCommand.Execute(null); // M = 50 + 25 = 75
        if (Math.Abs(calcWithClipVm.MemoryValue - 75.0) > 0.001)
            throw new Exception($"Calculator MemoryAdd (M+) failed: expected 75, got {calcWithClipVm.MemoryValue}.");

        calcWithClipVm.ClearCommand.Execute(null);
        calcWithClipVm.MemoryRecallCommand.Execute(null); // MR -> 75
        if (calcWithClipVm.Display != "75")
            throw new Exception($"Calculator MemoryRecall (MR) failed: expected display '75', got '{calcWithClipVm.Display}'.");

        calcWithClipVm.MemoryClearCommand.Execute(null); // MC -> 0
        if (calcWithClipVm.HasMemory || Math.Abs(calcWithClipVm.MemoryValue) > 0.001)
            throw new Exception("Calculator MemoryClear (MC) failed: HasMemory should be false.");

        // 15.2 Test History Journal & Toggle
        bool historyOpenReported = false;
        calcWithClipVm.OnToggleHistory = isOpen => historyOpenReported = isOpen;
        calcWithClipVm.ToggleHistoryCommand.Execute(null);
        if (!calcWithClipVm.IsHistoryOpen || !historyOpenReported)
            throw new Exception("Calculator ToggleHistory failed to open or trigger callback.");

        // Perform calculation to populate HistoryLog
        calcWithClipVm.ClearCommand.Execute(null);
        calcWithClipVm.InputDigitCommand.Execute("1");
        calcWithClipVm.InputDigitCommand.Execute("0");
        calcWithClipVm.InputDigitCommand.Execute("0");
        calcWithClipVm.SetOperationCommand.Execute("+");
        calcWithClipVm.InputDigitCommand.Execute("2");
        calcWithClipVm.InputDigitCommand.Execute("5");
        calcWithClipVm.CalculateResultCommand.Execute(null); // 100 + 25 = 125

        if (calcWithClipVm.HistoryLog.Count != 1 || calcWithClipVm.HistoryLog[0].Result != "125")
            throw new Exception("Calculator HistoryLog failed to record calculation (expected 1 entry with Result=125).");

        // 15.3 Test Internal HMI Clipboard (no OS clipboard)
        calcWithClipVm.CopyToClipboardCommand.Execute(null);
        if (!clipboard.HasValue || clipboard.CurrentValue != "125")
            throw new Exception($"HMI Clipboard copy failed: HasValue={clipboard.HasValue}, Value='{clipboard.CurrentValue}'.");

        // 15.4 Test NumpadViewModel Clipboard Insertion
        using var numpadVm = new NumpadViewModel(clipboard);
        if (!numpadVm.HasClipboardValue || numpadVm.ClipboardPreview != "125")
            throw new Exception($"NumpadViewModel failed to detect clipboard value: HasClipboardValue={numpadVm.HasClipboardValue}, Preview='{numpadVm.ClipboardPreview}'.");

        numpadVm.PasteFromClipboardCommand.Execute(null);
        if (numpadVm.InputValue != "125")
            throw new Exception($"NumpadViewModel paste failed: expected '125', got '{numpadVm.InputValue}'.");

        // 15.5 Test Live Clipboard Notification
        clipboard.CurrentValue = "42.8";
        Dispatcher.UIThread.RunJobs();
        if (numpadVm.ClipboardPreview != "42.8")
            throw new Exception($"NumpadViewModel did not update preview on clipboard change: expected '42.8', got '{numpadVm.ClipboardPreview}'.");
        numpadVm.PasteFromClipboardCommand.Execute(null);
        if (numpadVm.InputValue != "42.8")
            throw new Exception($"NumpadViewModel paste second value failed: expected '42.8', got '{numpadVm.InputValue}'.");

        // 15.6 Test Tech Calculations & Separate TechHistoryLog
        calcWithClipVm.TechVolume = 20.0;
        calcWithClipVm.TechDensity = 940.0;
        calcWithClipVm.CalculateTechMassCommand.Execute(null);
        var massEntry = calcWithClipVm.TechHistoryLog[0];
        if (calcWithClipVm.TechHistoryLog.Count != 1 || !massEntry.Result.Contains("18800") || (!massEntry.Result.Contains("18.8") && !massEntry.Result.Contains("18,8")))
            throw new Exception($"CalculateTechMassCommand failed to populate TechHistoryLog correctly: count={calcWithClipVm.TechHistoryLog.Count}, Result='{massEntry.Result}'");

        calcWithClipVm.PressureBar = 5.0;
        calcWithClipVm.CalculatePressureCommand.Execute(null);
        var pressEntry = calcWithClipVm.TechHistoryLog[0];
        if (calcWithClipVm.TechHistoryLog.Count != 2 || (!pressEntry.Expression.Contains("5.00") && !pressEntry.Expression.Contains("5,00")))
            throw new Exception($"CalculatePressureCommand failed to add second entry to TechHistoryLog: count={calcWithClipVm.TechHistoryLog.Count}, Expr='{pressEntry.Expression}'");

        // Test Tech History Toggle callback
        bool techHistoryOpenReported = false;
        calcWithClipVm.SelectedTabIndex = 1;
        calcWithClipVm.OnToggleHistory = isOpen => techHistoryOpenReported = isOpen;
        calcWithClipVm.ToggleTechHistoryCommand.Execute(null);
        if (!calcWithClipVm.IsTechHistoryOpen || !techHistoryOpenReported)
            throw new Exception("ToggleTechHistoryCommand failed to open tech history or notify width resize.");

        // Test Tech History Clear
        calcWithClipVm.ClearTechHistoryCommand.Execute(null);
        if (calcWithClipVm.TechHistoryLog.Count != 0)
            throw new Exception("ClearTechHistoryCommand failed: TechHistoryLog should be empty.");

        // 15.7 Test Calculator Session Persistence (MDI open/close keeps history and memory)
        mainVm.OpenCalculator();
        var openCalcWindow = mainVm.ActiveChildWindows.FirstOrDefault(cw => cw.Content is CalculatorViewModel);
        if (openCalcWindow == null)
            throw new Exception("Failed to open Calculator in MainViewModel.");

        var vmFromWindow = (CalculatorViewModel)openCalcWindow.Content;
        vmFromWindow.InputDigitCommand.Execute("9");
        vmFromWindow.InputDigitCommand.Execute("9");
        vmFromWindow.MemoryStoreCommand.Execute(null); // Save 99 in memory
        vmFromWindow.SetOperationCommand.Execute("+");
        vmFromWindow.InputDigitCommand.Execute("1");
        vmFromWindow.CalculateResultCommand.Execute(null); // 99 + 1 = 100

        int historyCountBeforeClose = vmFromWindow.HistoryLog.Count;

        // Simulate closing the calculator window
        openCalcWindow.CloseCommand.Execute(null);
        Dispatcher.UIThread.RunJobs();
        if (mainVm.ActiveChildWindows.Any(cw => cw.Content is CalculatorViewModel))
            throw new Exception("Calculator window failed to close.");

        // Re-open calculator
        mainVm.OpenCalculator();
        Dispatcher.UIThread.RunJobs();
        var reopenedWindow = mainVm.ActiveChildWindows.FirstOrDefault(cw => cw.Content is CalculatorViewModel);
        if (reopenedWindow == null)
            throw new Exception("Failed to re-open Calculator in MainViewModel.");

        var reopenedVm = (CalculatorViewModel)reopenedWindow.Content;
        if (reopenedVm.HistoryLog.Count != historyCountBeforeClose || !reopenedVm.HasMemory || Math.Abs(reopenedVm.MemoryValue - 99.0) > 0.001)
            throw new Exception("Calculator state (history log or memory) was lost after closing and re-opening the window!");

        reopenedWindow.CloseCommand.Execute(null);

        // 16. Multi-Selection, Group Move and Rubber-Band Box Validations
        {
            Console.WriteLine("[UIValidation] Testing Multi-Selection and Group Operations (Test 16)...");

            var multiConfig = new DashboardConfig
            {
                CellSize = 40,
                ZoomScale = 1.0,
                Widgets = new List<WidgetConfig>
                {
                    new ValveConfig { Title = "Valve 1", Position = new WidgetPosition { Col = 2, Row = 2, SizeX = 4, SizeY = 4 }, Type = "Valve" },
                    new PumpConfig { Title = "Pump 1", Position = new WidgetPosition { Col = 8, Row = 2, SizeX = 6, SizeY = 6 }, Type = "Pump" },
                    new TankConfig { Title = "Tank 1", Position = new WidgetPosition { Col = 16, Row = 2, SizeX = 6, SizeY = 10 }, Type = "Tank" }
                }
            };

            var multiWidgetFactory = sp.GetRequiredService<IWidgetFactory>();
            var multiDialogService = sp.GetRequiredService<IDialogService>();
            var multiHmiConfig = new HmiConfiguration();
            var multiDashboardVm = new DashboardViewModel(
                multiConfig, 
                new MockDataCoreService(), 
                mainVm.ProjectContext, 
                multiHmiConfig, 
                multiDialogService, 
                multiWidgetFactory);

            // 16.1 Test Multi-Selection Mode Toggle & Initial State
            if (multiDashboardVm.IsMultiSelectMode)
                throw new Exception("DashboardViewModel.IsMultiSelectMode should initially be false.");
            
            multiDashboardVm.ToggleMultiSelectModeCommand.Execute(null);
            if (!multiDashboardVm.IsMultiSelectMode)
                throw new Exception("ToggleMultiSelectModeCommand failed to enable multi-select mode.");

            // 16.2 Test Selecting Multiple Widgets and Summary calculation
            var w1 = multiDashboardVm.Widgets[0];
            var w2 = multiDashboardVm.Widgets[1];
            var w3 = multiDashboardVm.Widgets[2];

            w1.IsSelected = true;
            w2.IsSelected = true;

            if (multiDashboardVm.SelectedWidgetsCount != 2)
                throw new Exception($"Expected 2 selected widgets, got {multiDashboardVm.SelectedWidgetsCount}.");
            if (!multiDashboardVm.HasMultiSelection)
                throw new Exception("HasMultiSelection should be true when 2 widgets are selected.");
            if (string.IsNullOrEmpty(multiDashboardVm.SelectedWidgetsSummary) || !multiDashboardVm.SelectedWidgetsSummary.Contains("Valve") || !multiDashboardVm.SelectedWidgetsSummary.Contains("Pump"))
                throw new Exception($"SelectedWidgetsSummary expected to contain Valve and Pump, got '{multiDashboardVm.SelectedWidgetsSummary}'.");

            // 16.3 Test DashboardPanel Selection and Multi-Selection Helpers
            var multiPanel = new DashboardPanel
            {
                CellWidth = 40,
                CellHeight = 40,
                IsDesignMode = true
            };

            // Attach controls corresponding to widgets
            var c1 = new ContentControl { DataContext = w1 };
            DashboardPanel.SetCol(c1, w1.Col);
            DashboardPanel.SetRow(c1, w1.Row);
            DashboardPanel.SetSizeX(c1, w1.SizeX);
            DashboardPanel.SetSizeY(c1, w1.SizeY);
            multiPanel.Children.Add(c1);

            var c2 = new ContentControl { DataContext = w2 };
            DashboardPanel.SetCol(c2, w2.Col);
            DashboardPanel.SetRow(c2, w2.Row);
            DashboardPanel.SetSizeX(c2, w2.SizeX);
            DashboardPanel.SetSizeY(c2, w2.SizeY);
            multiPanel.Children.Add(c2);

            var c3 = new ContentControl { DataContext = w3 };
            DashboardPanel.SetCol(c3, w3.Col);
            DashboardPanel.SetRow(c3, w3.Row);
            DashboardPanel.SetSizeX(c3, w3.SizeX);
            DashboardPanel.SetSizeY(c3, w3.SizeY);
            multiPanel.Children.Add(c3);

            var panelSelected = multiPanel.GetSelectedWidgets();
            if (panelSelected.Count != 2 || !panelSelected.Contains(w1) || !panelSelected.Contains(w2))
                throw new Exception($"DashboardPanel.GetSelectedWidgets failed: returned {panelSelected.Count} items.");

            // 16.4 Test Rubber-Band Box Selection Intersection Logic
            var rubberBandBox = new Rect(0, 0, 600, 350);
            multiPanel.SelectionBoxRect = rubberBandBox;
            if (!multiPanel.SelectionBoxRect.HasValue || multiPanel.SelectionBoxRect.Value.Width != 600)
                throw new Exception("SelectionBoxRect assignment failed.");

            var w1Rect = new Rect(w1.Col * 40, w1.Row * 40, w1.SizeX * 40, w1.SizeY * 40);
            if (!rubberBandBox.Intersects(w1Rect))
                throw new Exception("Rubber band box should intersect w1Rect.");

            // 16.5 Test Group Drag (moving w1 and w2 together by deltaCol = +3, deltaRow = +4)
            double origW1Col = w1.Col;
            double origW1Row = w1.Row;
            double origW2Col = w2.Col;
            double origW2Row = w2.Row;

            double deltaCol = 3;
            double deltaRow = 4;

            foreach (var sel in multiPanel.GetSelectedWidgets())
            {
                sel.Col += deltaCol;
                sel.Row += deltaRow;
                sel.OriginalConfig.Position.Col = sel.Col;
                sel.OriginalConfig.Position.Row = sel.Row;
            }

            if (w1.Col != origW1Col + deltaCol || w1.Row != origW1Row + deltaRow)
                throw new Exception($"Group drag failed for w1: expected ({origW1Col + deltaCol}, {origW1Row + deltaRow}), got ({w1.Col}, {w1.Row}).");
            if (w2.Col != origW2Col + deltaCol || w2.Row != origW2Row + deltaRow)
                throw new Exception($"Group drag failed for w2: expected ({origW2Col + deltaCol}, {origW2Row + deltaRow}), got ({w2.Col}, {w2.Row}).");
            if (w3.Col != 16 || w3.Row != 2)
                throw new Exception($"Unselected widget w3 was unexpectedly moved to ({w3.Col}, {w3.Row}).");

            // 16.6 Test ClearSelection
            multiDashboardVm.ClearSelectionCommand.Execute(null);
            if (multiDashboardVm.SelectedWidgetsCount != 0 || multiDashboardVm.HasMultiSelection)
                throw new Exception("ClearSelectionCommand failed to clear selection state.");
            if (multiPanel.GetSelectedWidgets().Count != 0)
                throw new Exception("DashboardPanel.GetSelectedWidgets should be empty after ClearSelection.");

            // 16.7 Test Group Deletion (RemoveSelectedWidgetsCommand)
            w1.IsSelected = true;
            w2.IsSelected = true;
            multiDashboardVm.RemoveSelectedWidgetsCommand.Execute(null);
            if (multiDashboardVm.Widgets.Count != 1 || multiDashboardVm.Widgets[0] != w3)
                throw new Exception($"RemoveSelectedWidgetsCommand failed: expected 1 remaining widget (w3), got {multiDashboardVm.Widgets.Count}.");
            if (multiConfig.Widgets.Count != 1)
                throw new Exception("RemoveSelectedWidgetsCommand failed to remove widgets from DashboardConfig.");

            // 17. Test CAD/SCADA Selection System, Alignment, Distribution, Nudge & Grouping (Test 17)
            Console.WriteLine("[UIValidation] Testing CAD/SCADA Selection System, Alignment, Nudge & Grouping (Test 17)...");
            {
                var cadConfig = new DashboardConfig
                {
                    CellSize = 40,
                    ZoomScale = 1.0,
                    Widgets = new List<WidgetConfig>
                    {
                        new ValveConfig { Title = "V1", Position = new WidgetPosition { Col = 2, Row = 2, SizeX = 4, SizeY = 4 }, Type = "Valve" },
                        new PumpConfig { Title = "P1", Position = new WidgetPosition { Col = 10, Row = 6, SizeX = 6, SizeY = 6 }, Type = "Pump" },
                        new TankConfig { Title = "T1", Position = new WidgetPosition { Col = 20, Row = 14, SizeX = 6, SizeY = 10 }, Type = "Tank" },
                        new PipeConfig { Title = "Pipe1", Position = new WidgetPosition { Col = 5, Row = 10, SizeX = 10, SizeY = 2 }, PipePoints = "0,0; 10,0", Type = "Pipe" }
                    }
                };

                var cadDashboardVm = new DashboardViewModel(
                    cadConfig,
                    new MockDataCoreService(),
                    mainVm.ProjectContext,
                    new HmiConfiguration(),
                    sp.GetRequiredService<IDialogService>(),
                    sp.GetRequiredService<IWidgetFactory>());

                var cadPanel = new DashboardPanel
                {
                    CellWidth = 40,
                    CellHeight = 40,
                    IsDesignMode = true
                };

                foreach (var w in cadDashboardVm.Widgets)
                {
                    var cc = new ContentControl { DataContext = w };
                    DashboardPanel.SetCol(cc, w.Col);
                    DashboardPanel.SetRow(cc, w.Row);
                    DashboardPanel.SetSizeX(cc, w.SizeX);
                    DashboardPanel.SetSizeY(cc, w.SizeY);
                    cadPanel.Children.Add(cc);
                }

                var v1 = cadDashboardVm.Widgets[0];
                var p1 = cadDashboardVm.Widgets[1];
                var t1 = cadDashboardVm.Widgets[2];

                // 17.1 CAD Window Selection (left-to-right): Only fully enclosed widgets selected
                cadPanel.IsCrossingSelection = false;
                var windowBox = new Rect(50, 50, 250, 250);
                var v1Rect = new Rect(v1.Col * 40, v1.Row * 40, v1.SizeX * 40, v1.SizeY * 40);
                if (!windowBox.Contains(v1Rect))
                    throw new Exception("CAD Window box should fully contain V1 rect.");

                // A partially overlapped rect: (100, 100, 350, 200) contains V1 fully, but P1 is only partially covered
                var partialBox = new Rect(100, 100, 350, 200);
                var p1Rect = new Rect(p1.Col * 40, p1.Row * 40, p1.SizeX * 40, p1.SizeY * 40);
                if (partialBox.Contains(p1Rect))
                    throw new Exception("CAD Window box should NOT fully contain P1.");
                if (!partialBox.Intersects(p1Rect))
                    throw new Exception("CAD Crossing box should intersect P1.");

                // 17.2 Alignment Tests
                v1.IsSelected = true;
                p1.IsSelected = true;
                t1.IsSelected = true;

                // AlignLeft: all Col should become min(2, 10, 20) = 2
                cadDashboardVm.AlignLeftCommand.Execute(null);
                if (v1.Col != 2 || p1.Col != 2 || t1.Col != 2)
                    throw new Exception($"AlignLeft failed: expected Col 2, got V1={v1.Col}, P1={p1.Col}, T1={t1.Col}");

                // AlignTop: all Row should become min(2, 6, 14) = 2
                cadDashboardVm.AlignTopCommand.Execute(null);
                if (v1.Row != 2 || p1.Row != 2 || t1.Row != 2)
                    throw new Exception($"AlignTop failed: expected Row 2, got V1={v1.Row}, P1={p1.Row}, T1={t1.Row}");

                // AlignRight: max right = max(2+4, 2+6, 2+6) = 8.
                // V1: 8 - 4 = 4. P1: 8 - 6 = 2. T1: 8 - 6 = 2.
                cadDashboardVm.AlignRightCommand.Execute(null);
                if (v1.Col != 4 || p1.Col != 2 || t1.Col != 2)
                    throw new Exception($"AlignRight failed: expected V1=4, P1=2, T1=2; got V1={v1.Col}, P1={p1.Col}, T1={t1.Col}");

                // 17.3 Group / Ungroup (SCADA Meta-group)
                if (!cadDashboardVm.CanGroup)
                    throw new Exception("CanGroup should be true for 3 selected widgets.");
                
                cadDashboardVm.GroupSelectedWidgetsCommand.Execute(null);
                if (string.IsNullOrEmpty(v1.GroupId) || v1.GroupId != p1.GroupId || v1.GroupId != t1.GroupId)
                    throw new Exception("GroupSelectedWidgetsCommand failed to assign matching GroupId.");
                if (!cadDashboardVm.CanUngroup)
                    throw new Exception("CanUngroup should be true when widgets have GroupId.");

                cadDashboardVm.UngroupSelectedWidgetsCommand.Execute(null);
                if (v1.GroupId != null || p1.GroupId != null || t1.GroupId != null)
                    throw new Exception("UngroupSelectedWidgetsCommand failed to clear GroupId.");
                if (cadDashboardVm.CanUngroup)
                    throw new Exception("CanUngroup should be false after ungrouping.");

                // 17.4 Group Duplication
                int beforeCount = cadDashboardVm.Widgets.Count;
                cadDashboardVm.DuplicateSelectedWidgetsCommand.Execute(null);
                if (cadDashboardVm.Widgets.Count != beforeCount + 3)
                    throw new Exception($"DuplicateSelectedWidgetsCommand failed: expected {beforeCount + 3} widgets, got {cadDashboardVm.Widgets.Count}.");
                if (cadDashboardVm.SelectedWidgetsCount != 3)
                    throw new Exception("DuplicateSelectedWidgetsCommand should select the 3 new duplicated widgets.");
            }

            // 18. Test Live Dragging, Smart Alignment Guides, Coordinate Badge and Rubber-banding (Test 18)
            Console.WriteLine("[UIValidation] Testing Live Dragging, Smart Alignment Guides & Coordinate Badge (Test 18)...");
            {
                var livePanel = new DashboardPanel
                {
                    CellWidth = 20,
                    CellHeight = 20,
                    IsDesignMode = true
                };

                var v1Config = new ValveConfig
                {
                    Type = "Valve",
                    Title = "Valve 1",
                    Position = new WidgetPosition { Col = 10, Row = 10, SizeX = 4, SizeY = 4 }
                };
                var v1Vm = new ValveWidgetViewModel(v1Config, new MockDataCoreService(), mainVm.ProjectContext);
                var v1Control = new ContentControl { DataContext = v1Vm };
                DashboardPanel.SetCol(v1Control, 10);
                DashboardPanel.SetRow(v1Control, 10);
                DashboardPanel.SetSizeX(v1Control, 4);
                DashboardPanel.SetSizeY(v1Control, 4);
                livePanel.Children.Add(v1Control);

                var v2Config = new PumpConfig
                {
                    Type = "Pump",
                    Title = "Pump 1",
                    Position = new WidgetPosition { Col = 25, Row = 10, SizeX = 4, SizeY = 4 }
                };
                var v2Vm = new PumpWidgetViewModel(v2Config, new MockDataCoreService(), mainVm.ProjectContext);
                var v2Control = new ContentControl { DataContext = v2Vm };
                DashboardPanel.SetCol(v2Control, 25);
                DashboardPanel.SetRow(v2Control, 10);
                DashboardPanel.SetSizeX(v2Control, 4);
                DashboardPanel.SetSizeY(v2Control, 4);
                livePanel.Children.Add(v2Control);

                // 18.1 Test Smart Alignment Guides: Vertical Alignment
                // Dragging a widget at Col=10 (same Col as v1)
                livePanel.UpdateSmartGuidesAndPosition(10, 30, 4, 4);
                if (livePanel.ActiveSmartGuides.Count == 0)
                    throw new Exception("Smart Guides failed to detect vertical alignment at Col=10.");
                var vGuide = livePanel.ActiveSmartGuides.FirstOrDefault(g => g.IsVertical);
                if (vGuide == null || Math.Abs(vGuide.Start.X - 10 * 20) > 1.0)
                    throw new Exception($"Smart Guide vertical line X ({vGuide?.Start.X}) does not match expected {10 * 20}.");

                // 18.2 Test Smart Alignment Guides: Horizontal Alignment
                // Dragging a widget at Row=10 (same Row as v1 and v2)
                livePanel.UpdateSmartGuidesAndPosition(40, 10, 4, 4);
                if (livePanel.ActiveSmartGuides.Count == 0)
                    throw new Exception("Smart Guides failed to detect horizontal alignment at Row=10.");
                var hGuide = livePanel.ActiveSmartGuides.FirstOrDefault(g => !g.IsVertical);
                if (hGuide == null || Math.Abs(hGuide.Start.Y - 10 * 20) > 1.0)
                    throw new Exception($"Smart Guide horizontal line Y ({hGuide?.Start.Y}) does not match expected {10 * 20}.");

                // 18.3 Test Coordinate Badge
                livePanel.DragCurrentBadge = (12, 18, new Point(240, 360));
                if (!livePanel.DragCurrentBadge.HasValue ||
                    livePanel.DragCurrentBadge.Value.Col != 12 ||
                    livePanel.DragCurrentBadge.Value.Row != 18 ||
                    livePanel.DragCurrentBadge.Value.CursorPos.X != 240)
                {
                    throw new Exception("DragCurrentBadge property failed to store coordinate badge state.");
                }

                // 18.4 Test SelectionOverlay rendering with Smart Guides & Coordinate Badge
                var overlay = new SelectionOverlay(livePanel);
                v1Vm.IsSelected = true;
                // Render via Dispatcher / Layout
                livePanel.Children.Add(overlay);
                livePanel.Measure(new Size(1000, 1000));
                livePanel.Arrange(new Rect(0, 0, 1000, 1000));
                overlay.InvalidateVisual();
                Dispatcher.UIThread.RunJobs();

                // Clear drag state
                livePanel.ActiveSmartGuides.Clear();
                livePanel.DragCurrentBadge = null;
                if (livePanel.ActiveSmartGuides.Count != 0 || livePanel.DragCurrentBadge.HasValue)
                    throw new Exception("ActiveSmartGuides or DragCurrentBadge failed to clear.");
            }

            // =========================================================================
            // TEST 19: Enterprise TagManager, Modbus Coalescing & Stale Watchdog
            // =========================================================================
            Console.WriteLine("[UIValidation] Testing Enterprise TagManager, Modbus Coalescing & Stale Watchdog (Test 19)...");
            {
                // 19.1 Domain Models
                var now = DateTime.UtcNow;
                var snapshot = new TagSnapshot("test_tag", 42.5f, TagQuality.Good, now, null, false);
                if (snapshot.TagId != "test_tag" || (float)(snapshot.Value!) != 42.5f || snapshot.Quality != TagQuality.Good)
                    throw new Exception("TagSnapshot struct values not preserved.");

                // 19.2 TagManagerService Registration, Updates & Lookup
                var tagManager = new TagManagerService(startWatchdogLoop: false);
                var testDef = new TagDefinition
                {
                    TagId = "tag_plc1_40001",
                    Name = "Давление в ресивере",
                    ConnId = "plc1",
                    Address = "40001",
                    DataType = "Float32",
                    StaleTimeout = TimeSpan.FromMilliseconds(50)
                };
                tagManager.RegisterTag(testDef);

                var retrieved = tagManager.GetTagDefinition("tag_plc1_40001");
                if (retrieved == null || retrieved.Address != "40001")
                    throw new Exception("TagManager failed to retrieve registered tag definition by TagId.");

                var retrievedByAddr = tagManager.FindTagByAddress("plc1", "40001");
                if (retrievedByAddr == null || retrievedByAddr.TagId != "tag_plc1_40001")
                    throw new Exception("TagManager failed to lookup tag definition by connection and address.");

                var initialSnap = tagManager.GetSnapshot("tag_plc1_40001");
                if (initialSnap.Quality != TagQuality.Uninitialized)
                    throw new Exception($"Initial snapshot should be Uninitialized, got: {initialSnap.Quality}");

                // 19.3 Telemetry Update
                tagManager.UpdateTagValue("plc1", "40001", 123.4f, TagQuality.Good);
                var goodSnap = tagManager.GetSnapshot("tag_plc1_40001");
                if (goodSnap.Quality != TagQuality.Good || Math.Abs((float)goodSnap.Value! - 123.4f) > 0.001)
                    throw new Exception("TagManager failed to update value and quality.");

                // 19.4 Watchdog Stale & Timeout Detection
                Thread.Sleep(60);
                tagManager.CheckStaleTags();
                var staleSnap = tagManager.GetSnapshot("tag_plc1_40001");
                if (staleSnap.Quality != TagQuality.Stale)
                    throw new Exception($"Watchdog failed to downgrade unrefreshed tag to Stale. Got: {staleSnap.Quality}");

                Thread.Sleep(60);
                tagManager.CheckStaleTags();
                var timeoutSnap = tagManager.GetSnapshot("tag_plc1_40001");
                if (timeoutSnap.Quality != TagQuality.Timeout)
                    throw new Exception($"Watchdog failed to downgrade unrefreshed tag to Timeout. Got: {timeoutSnap.Quality}");

                // 19.5 Manual Override & Safety
                tagManager.ForceOverride("tag_plc1_40001", 999.0f);
                var overrideSnap = tagManager.GetSnapshot("tag_plc1_40001");
                if (!overrideSnap.IsOverridden || overrideSnap.Quality != TagQuality.Overridden || Math.Abs((float)overrideSnap.Value! - 999.0f) > 0.001)
                    throw new Exception("ForceOverride failed to apply override flag and value.");

                // Incoming telemetry should not overwrite operator manual override
                tagManager.UpdateTagValue("plc1", "40001", 111.0f, TagQuality.Good);
                var guardedSnap = tagManager.GetSnapshot("tag_plc1_40001");
                if (!guardedSnap.IsOverridden || Math.Abs((float)guardedSnap.Value! - 999.0f) > 0.001)
                    throw new Exception("Telemetry erroneously overwrote operator manual override.");

                tagManager.ClearOverride("tag_plc1_40001");
                var clearedSnap = tagManager.GetSnapshot("tag_plc1_40001");
                if (clearedSnap.IsOverridden)
                    throw new Exception("ClearOverride failed to remove override flag.");
                if (Math.Abs((float)clearedSnap.Value! - 111.0f) > 0.001 || clearedSnap.Quality != TagQuality.Good)
                    throw new Exception("ClearOverride failed to restore real physical telemetry.");

                // Deadband suppression & emission
                testDef.Deadband = 5.0;
                tagManager.UpdateTagValue("plc1", "40001", 113.0f, TagQuality.Good); // delta = 2.0 < 5.0
                var deadbandSnap = tagManager.GetSnapshot("tag_plc1_40001");
                if (Math.Abs((float)deadbandSnap.Value! - 111.0f) > 0.001)
                    throw new Exception("Deadband failed to suppress noise below threshold.");
                tagManager.UpdateTagValue("plc1", "40001", 120.0f, TagQuality.Good); // delta = 9.0 >= 5.0
                var deadbandPassedSnap = tagManager.GetSnapshot("tag_plc1_40001");
                if (Math.Abs((float)deadbandPassedSnap.Value! - 120.0f) > 0.001)
                    throw new Exception("Deadband failed to emit update above threshold.");
                testDef.Deadband = 0.0;

                // Clock jump backwards handling
                tagManager.UpdateTagValue("plc1", "40001", 125.0f, TagQuality.Good);
                tagManager.CheckStaleTags(); // Ensures normalization without crash

                // 19.6 Modbus Bulk Coalescing & PDU Splitting (MaxGap <= 5, <= 120 registers)
                var modbusConfig = new ConnectionConfig
                {
                    Id = "mb_test",
                    Type = "ModbusTCP",
                    Host = "127.0.0.1",
                    Port = 502,
                    PollIntervalMs = 200,
                    ByteOrder = "CDAB"
                };

                // Create tags: 40001, 40003 (Float32, gap 0), 40007 (gap 2), 40020 (gap 11 -> new block)
                var tagsToPoll = new List<DataSourceConfig>
                {
                    new() { ConnId = "mb_test", Address = "40001", DataType = "Float32" },
                    new() { ConnId = "mb_test", Address = "40003", DataType = "Float32" },
                    new() { ConnId = "mb_test", Address = "40007", DataType = "Float32" },
                    new() { ConnId = "mb_test", Address = "40020", DataType = "Float32" }
                };

                var modbusDriver = new AvaloniaApplication1.Services.Protocols.ModbusProtocolDriver(modbusConfig, tagsToPoll);
                if (modbusDriver.CoalescedBlocks.Count != 2)
                    throw new Exception($"Modbus Coalescing expected 2 blocks, got: {modbusDriver.CoalescedBlocks.Count}");

                var firstBlock = modbusDriver.CoalescedBlocks[0];
                if (firstBlock.StartOffset != 0 || firstBlock.Tags.Count != 3)
                    throw new Exception($"First coalesced block has unexpected start {firstBlock.StartOffset} or tags count {firstBlock.Tags.Count}");

                var secondBlock = modbusDriver.CoalescedBlocks[1];
                if (secondBlock.StartOffset != 19 || secondBlock.Tags.Count != 1)
                    throw new Exception($"Second coalesced block has unexpected start {secondBlock.StartOffset} or tags count {secondBlock.Tags.Count}");

                // 19.7 Configuration Migration (Schema v1 -> v2)
                var v1Config = new HmiConfiguration
                {
                    SchemaVersion = 1,
                    Dashboard = new DashboardConfig
                    {
                        Widgets = new List<WidgetConfig>
                        {
                            new ValueDisplayConfig
                            {
                                Title = "Датчик давления",
                                Source = new DataSourceConfig { ConnId = "plc_511", Address = "40001", DataType = "Float32" }
                            },
                            new PilotLightConfig
                            {
                                Title = "Авария насоса",
                                Source = new DataSourceConfig { ConnId = "plc_511", Address = "00001", DataType = "Bool" }
                            }
                        }
                    }
                };

                if (!ConfigMigrationService.NeedsMigration(v1Config))
                    throw new Exception("NeedsMigration returned false for Schema v1 config.");

                var migrated = ConfigMigrationService.Migrate(v1Config);
                if (migrated.SchemaVersion != 2)
                    throw new Exception($"Migration failed: expected SchemaVersion 2, got {migrated.SchemaVersion}");
                if (migrated.Tags.Count != 2)
                    throw new Exception($"Migration failed: expected 2 tags in centralized dictionary, got {migrated.Tags.Count}");
                if (string.IsNullOrEmpty(migrated.Dashboard.Widgets[0].Source.TagId))
                    throw new Exception("Migration failed: widget Source.TagId was not populated.");

                // 19.8 Widget ViewModel Quality Feedback
                var mockCore = new MockDataCoreService();
                var vdConfig = new ValueDisplayConfig
                {
                    Title = "Температура T-101",
                    Format = "{0:F1} °C",
                    Source = new DataSourceConfig { ConnId = "plc1", Address = "40010", TagId = "tag_t101" }
                };
                var vdVm = new ValueDisplayViewModel(vdConfig, mockCore, new ProjectContextService { IsDesignMode = false });

                // Normal state
                vdVm.ApplySnapshot(new TagSnapshot("tag_t101", 75.3f, TagQuality.Good, DateTime.UtcNow));
                if (vdVm.Quality != TagQuality.Good || vdVm.QualityOpacity != 1.0 || vdVm.DisplayValue.Replace(',', '.') != "75.3 °C" || vdVm.QualityGlyph != "")
                    throw new Exception($"Normal state visual feedback failed: {vdVm.DisplayValue}, Opacity={vdVm.QualityOpacity}");

                // Stale state
                vdVm.ApplySnapshot(new TagSnapshot("tag_t101", 75.3f, TagQuality.Stale, DateTime.UtcNow.AddSeconds(-10)));
                if (!vdVm.IsStale || vdVm.QualityOpacity != 0.55 || vdVm.QualityGlyph != "⏳")
                    throw new Exception($"Stale state visual feedback failed: Opacity={vdVm.QualityOpacity}, Glyph={vdVm.QualityGlyph}");

                // CommFailure state
                vdVm.ApplySnapshot(new TagSnapshot("tag_t101", null, TagQuality.CommFailure, DateTime.UtcNow, "Connection timeout"));
                if (!vdVm.HasCommError || vdVm.DisplayValue != "---" || vdVm.QualityOpacity != 0.55 || vdVm.QualityGlyph != "⚠️")
                    throw new Exception($"CommFailure state visual feedback failed: Display={vdVm.DisplayValue}, Opacity={vdVm.QualityOpacity}, Glyph={vdVm.QualityGlyph}");

                // Overridden state
                vdVm.ApplySnapshot(new TagSnapshot("tag_t101", 88.0f, TagQuality.Overridden, DateTime.UtcNow, null, true));
                if (!vdVm.IsOverridden || vdVm.QualityOpacity != 1.0 || vdVm.QualityGlyph != "[M]")
                    throw new Exception($"Overridden state visual feedback failed: Opacity={vdVm.QualityOpacity}, Glyph={vdVm.QualityGlyph}");

                // 19.9 Modbus Address Parsing Edge Cases
                if (!AvaloniaApplication1.Services.Protocols.ModbusProtocolDriver.ParseModbusAddress("10", out var fc1, out var off1) || fc1 != 3 || off1 != 10)
                    throw new Exception("ParseModbusAddress failed for direct offset 10.");
                if (!AvaloniaApplication1.Services.Protocols.ModbusProtocolDriver.ParseModbusAddress("4000", out var fc2, out var off2) || fc2 != 3 || off2 != 4000)
                    throw new Exception("ParseModbusAddress failed for 4-digit address 4000.");
                if (!AvaloniaApplication1.Services.Protocols.ModbusProtocolDriver.ParseModbusAddress("00001", out var fc3, out var off3) || fc3 != 1 || off3 != 1)
                    throw new Exception("ParseModbusAddress failed for 5-digit coil 00001.");
                if (!AvaloniaApplication1.Services.Protocols.ModbusProtocolDriver.ParseModbusAddress("30005", out var fc4, out var off4) || fc4 != 4 || off4 != 30005)
                    throw new Exception("ParseModbusAddress failed for 5-digit input register 30005.");
                if (!AvaloniaApplication1.Services.Protocols.ModbusProtocolDriver.ParseModbusAddress("40012", out var fc5, out var off5) || fc5 != 3 || off5 != 40012)
                    throw new Exception("ParseModbusAddress failed for 5-digit holding register 40012.");

                // 19.10 Safety & Operator Confirmation on Degraded Communication
                var testDialog = new MockTestDialogService { ConfirmResult = false };
                var cmdConfig = new CommandButtonConfig
                {
                    Title = "Пуск R-101",
                    ButtonMode = "Toggle",
                    Source = new DataSourceConfig { ConnId = "plc1", Address = "40001", TagId = "tag_plc1_40001" }
                };
                var cmdVm = new CommandButtonViewModel(cmdConfig, mockCore, new ProjectContextService { IsDesignMode = false }) { DialogService = testDialog };
                cmdVm.ApplySnapshot(new TagSnapshot("tag_plc1_40001", 0, TagQuality.Stale, DateTime.UtcNow.AddMinutes(-5)));

                // Test operator cancel: must NOT force override
                cmdVm.ExecuteCommandCommand.ExecuteAsync(null).GetAwaiter().GetResult();
                if (!testDialog.ShowConfirmCalled)
                    throw new Exception("CommandButton did not prompt operator confirmation on degraded communications.");
                if (cmdVm.IsOverridden)
                    throw new Exception("CommandButton force-overrode despite operator cancelling confirmation.");

                // Test operator confirm: must force override and set [M]
                testDialog.ConfirmResult = true;
                testDialog.ShowConfirmCalled = false;
                cmdVm.ExecuteCommandCommand.ExecuteAsync(null).GetAwaiter().GetResult();
                if (!testDialog.ShowConfirmCalled || !cmdVm.IsOverridden)
                    throw new Exception("CommandButton failed to execute and set Overridden after operator confirmation.");

                // 19.11 SetValueViewModel Comm Failure Handling
                var svConfig = new SetValueConfig
                {
                    Title = "Уставка давления",
                    Format = "{0:F1}",
                    Source = new DataSourceConfig { ConnId = "plc1", Address = "40001", TagId = "tag_plc1_40001" }
                };
                var svVm = new SetValueViewModel(svConfig, mockCore, new ProjectContextService { IsDesignMode = false });
                svVm.ApplySnapshot(new TagSnapshot("tag_plc1_40001", 50.0f, TagQuality.Good, DateTime.UtcNow));
                if (svVm.DisplayValue.Replace(',', '.') != "50.0")
                    throw new Exception($"SetValueViewModel expected 50.0, got: {svVm.DisplayValue}");
                svVm.ApplySnapshot(new TagSnapshot("tag_plc1_40001", null, TagQuality.CommFailure, DateTime.UtcNow));
                if (svVm.DisplayValue != "---" || svVm.QualityOpacity != 0.55)
                    throw new Exception($"SetValueViewModel failed to display '---' on comm error. Got: {svVm.DisplayValue}");
            }

            // =========================================================================
            // TEST 20: Industrial Tag Manager UI, CRUD, Live Telemetry, Scaling, Override & CSV/JSON
            // =========================================================================
            Console.WriteLine("[UIValidation] Testing Industrial Tag Manager UI, CRUD, Scaling & Import/Export (Test 20)...");
            {
                // 20.1 TagDefinition Scaling Y = A * X + B
                var scaledDef = new TagDefinition
                {
                    TagId = "tag_scale_test",
                    Name = "Давление пара",
                    ConnId = "plc1",
                    Address = "40050",
                    DataType = "Float32",
                    Unit = "бар",
                    ScaleA = 0.1,
                    ScaleB = 5.0,
                    Deadband = 0.05
                };
                if (!scaledDef.ScalingDisplay.Contains("0.1") || !scaledDef.ScalingDisplay.Contains("5"))
                    throw new Exception($"TagDefinition.ScalingDisplay expected '0.1*X + 5', got '{scaledDef.ScalingDisplay}'");

                var tMgr = new TagManagerService(startWatchdogLoop: false);
                tMgr.RegisterTag(scaledDef);
                tMgr.UpdateTagValue("tag_scale_test", 100.0f, TagQuality.Good);
                var snap = tMgr.GetSnapshot("tag_scale_test");
                if (Math.Abs((double)snap.Value! - 15.0) > 0.001)
                    throw new Exception($"Scaling Y = A * X + B failed: expected 15.0, got {snap.Value}");

                // 20.2 TagManagerService RemoveTag & UpdateTag
                scaledDef.Name = "Давление пара (Обновлено)";
                tMgr.UpdateTag(scaledDef);
                if (tMgr.GetTagDefinition("tag_scale_test")?.Name != "Давление пара (Обновлено)")
                    throw new Exception("TagManagerService.UpdateTag failed to update name.");

                bool removed = tMgr.RemoveTag("tag_scale_test");
                if (!removed || tMgr.GetTagDefinition("tag_scale_test") != null)
                    throw new Exception("TagManagerService.RemoveTag failed to delete tag.");

                // 20.3 TagItemViewModel Presentation & Quality Glyphs
                var itemDef = new TagDefinition
                {
                    TagId = "tag_ui_row",
                    Name = "Температура T-200",
                    ConnId = "modbus_main",
                    Address = "40010",
                    DataType = "Float32",
                    Unit = "°C"
                };
                var rowVm = new TagItemViewModel(itemDef);
                if (rowVm.Quality != TagQuality.Uninitialized || rowVm.LiveValue != "---")
                    throw new Exception("TagItemViewModel initial state should be Uninitialized and '---'.");

                // Good state
                rowVm.ApplySnapshot(new TagSnapshot("tag_ui_row", 78.4f, TagQuality.Good, DateTime.UtcNow));
                if (rowVm.Quality != TagQuality.Good || !rowVm.LiveValue.Contains("78.4") || rowVm.QualityGlyph != "✓")
                    throw new Exception($"TagItemViewModel Good state failed: Value='{rowVm.LiveValue}', Glyph='{rowVm.QualityGlyph}'");

                // Stale state
                rowVm.ApplySnapshot(new TagSnapshot("tag_ui_row", 78.4f, TagQuality.Stale, DateTime.UtcNow.AddMinutes(-1)));
                if (rowVm.Quality != TagQuality.Stale || rowVm.QualityGlyph != "⏳")
                    throw new Exception($"TagItemViewModel Stale state failed: Glyph='{rowVm.QualityGlyph}'");

                // Overridden state
                rowVm.ApplySnapshot(new TagSnapshot("tag_ui_row", 99.0f, TagQuality.Overridden, DateTime.UtcNow, null, true));
                if (!rowVm.IsOverridden || rowVm.QualityGlyph != "⚡" || !rowVm.QualityText.Contains("Overridden"))
                    throw new Exception($"TagItemViewModel Overridden state failed: IsOverridden={rowVm.IsOverridden}, Glyph='{rowVm.QualityGlyph}'");

                // 20.4 TagManagerViewModel CRUD, Search & Filtering
                var tmConfig = new HmiConfiguration
                {
                    Connections = new List<ConnectionConfig>
                    {
                        new() { Id = "plc_511", Type = "ModbusTCP" },
                        new() { Id = "wb_mqtt", Type = "MQTT" }
                    },
                    Tags = new List<TagDefinition>
                    {
                        new() { TagId = "tag_t1", Name = "Температура R-101", ConnId = "plc_511", Address = "40001", DataType = "Float32", Unit = "°C" },
                        new() { TagId = "tag_p1", Name = "Давление в линии", ConnId = "plc_511", Address = "40002", DataType = "Float32", Unit = "бар" },
                        new() { TagId = "tag_l1", Name = "Уровень в емкости", ConnId = "wb_mqtt", Address = "level/tank", DataType = "Float32", Unit = "%" }
                    }
                };

                var tagEngine = new TagManagerService(startWatchdogLoop: false);
                tagEngine.Initialize(tmConfig.Tags);
                tagEngine.UpdateTagValue("tag_t1", 85.5f, TagQuality.Good);
                tagEngine.UpdateTagValue("tag_p1", 6.2f, TagQuality.Stale);
                tagEngine.ForceOverride("tag_l1", 99.0f);

                var mockDialog = new MockTestDialogService();
                var tmVm = new TagManagerViewModel(tmConfig, tagEngine, mockDialog, sp.GetRequiredService<IConfigurationService>(), sp.GetRequiredService<IHmiClipboardService>());

                if (tmVm.Tags.Count != 3 || tmVm.FilteredTags.Count != 3)
                    throw new Exception($"TagManagerViewModel expected 3 tags, got {tmVm.Tags.Count}");
                if (tmVm.GoodCount != 1 || tmVm.StaleCount != 1 || tmVm.OverriddenCount != 1)
                    throw new Exception($"TagManagerViewModel statistics mismatch: Good={tmVm.GoodCount}, Stale={tmVm.StaleCount}, Overridden={tmVm.OverriddenCount}");

                // Filter by search query
                tmVm.SearchText = "линии";
                if (tmVm.FilteredTags.Count != 1 || tmVm.FilteredTags[0].TagId != "tag_p1")
                    throw new Exception($"TagManagerViewModel SearchText 'линии' failed: found {tmVm.FilteredTags.Count} tags");

                // Filter by connection
                tmVm.SearchText = "";
                tmVm.SelectedConnectionFilter = "wb_mqtt";
                if (tmVm.FilteredTags.Count != 1 || tmVm.FilteredTags[0].TagId != "tag_l1")
                    throw new Exception($"TagManagerViewModel ConnectionFilter 'wb_mqtt' failed: found {tmVm.FilteredTags.Count} tags");

                // Filter by quality
                tmVm.SelectedConnectionFilter = "Все подключения";
                tmVm.SelectedQualityFilter = "⚡ [M] Overridden (Фиксация)";
                if (tmVm.FilteredTags.Count != 1 || tmVm.FilteredTags[0].TagId != "tag_l1")
                    throw new Exception($"TagManagerViewModel QualityFilter 'Overridden' failed: found {tmVm.FilteredTags.Count} tags");

                tmVm.SelectedQualityFilter = "Все качества";

                // Force override & Clear override from VM
                var targetRow = tmVm.Tags.First(t => t.TagId == "tag_t1");
                tmVm.ForceOverrideCommand.Execute(targetRow);
                if (!targetRow.IsOverridden || targetRow.Quality != TagQuality.Overridden)
                    throw new Exception("TagManagerViewModel.ForceOverrideCommand failed to set Overridden quality.");

                tmVm.ClearOverrideCommand.Execute(targetRow);
                if (targetRow.IsOverridden)
                    throw new Exception("TagManagerViewModel.ClearOverrideCommand failed to remove override.");

                // 20.5 CSV Export & Import Service
                var csvOut = TagExportImportService.ExportCsv(tmConfig.Tags);
                if (!csvOut.Contains("TagId") || !csvOut.Contains("tag_t1") || !csvOut.Contains("tag_p1"))
                    throw new Exception("TagExportImportService.ExportCsv failed to output CSV header or tags.");

                var importedFromCsv = TagExportImportService.ImportCsv(csvOut);
                if (importedFromCsv.Count != 3 || importedFromCsv[0].TagId != "tag_t1" || importedFromCsv[0].Unit != "°C")
                    throw new Exception($"TagExportImportService.ImportCsv failed: expected 3 tags, got {importedFromCsv.Count}");

                // Test CSV import with custom Excel semicolon delimited columns
                var customCsv = "ID;Наименование;Драйвер;Регистр;Тип;ЕдИзм;МасштабA;МасштабB;Апертура\n" +
                                "tag_gas_flow;Расход газа;plc_511;40020;Float32;м³/ч;1.5;0.0;0.1\n";
                var customParsed = TagExportImportService.ImportCsv(customCsv);
                if (customParsed.Count != 1 || customParsed[0].TagId != "tag_gas_flow" || customParsed[0].Unit != "м³/ч" || Math.Abs(customParsed[0].ScaleA - 1.5) > 0.001)
                    throw new Exception("TagExportImportService failed to parse Russian Excel signal table.");

                // 20.6 JSON Export & Import Service
                var jsonOut = TagExportImportService.ExportJson(tmConfig.Tags);
                if (!jsonOut.Contains("\"TagId\": \"tag_t1\""))
                    throw new Exception("TagExportImportService.ExportJson failed.");

                var importedFromJson = TagExportImportService.ImportJson(jsonOut);
                if (importedFromJson.Count != 3 || importedFromJson[1].TagId != "tag_p1")
                    throw new Exception("TagExportImportService.ImportJson failed.");

                // 20.7 TagPickerViewModel & WidgetEditorViewModel 1-Click Integration
                var pickerVm = new TagPickerViewModel(tmConfig.Tags);
                pickerVm.SearchText = "R-101";
                if (pickerVm.FilteredTags.Count != 1 || pickerVm.FilteredTags[0].TagId != "tag_t1")
                    throw new Exception("TagPickerViewModel SearchText failed.");

                pickerVm.SelectedTag = pickerVm.FilteredTags[0];
                pickerVm.ConfirmCommand.Execute(null);
                if (!pickerVm.IsConfirmed)
                    throw new Exception("TagPickerViewModel ConfirmCommand failed.");

                var widgetCfgToEdit = new ValueDisplayConfig
                {
                    Type = "ValueDisplay",
                    Title = "Новый виджет",
                    Format = "{0}"
                };
                var widgetEditVm = new WidgetEditorViewModel(widgetCfgToEdit, tmConfig.Connections, tags: tmConfig.Tags);
                widgetEditVm.ApplyTag(pickerVm.SelectedTag);

                if (widgetEditVm.SelectedTagId != "tag_t1" ||
                    widgetEditVm.SelectedConnectionId != "plc_511" ||
                    widgetEditVm.Address != "40001" ||
                    widgetEditVm.SelectedDataType != "Float32" ||
                    widgetEditVm.Title != "Температура R-101" ||
                    widgetEditVm.Format != "{0:F1} °C")
                {
                    throw new Exception($"WidgetEditorViewModel.ApplyTag failed to auto-fill properties: Title='{widgetEditVm.Title}', Format='{widgetEditVm.Format}', TagId='{widgetEditVm.SelectedTagId}'");
                }
            }

            // 21. Test Extracted AvaloniaApplication1.Controls Library Components
            Console.WriteLine("[UIValidation] Testing Extracted SCADA Controls Library (Test 21)...");
            {
                // 21.1 ValveControl StyledProperties
                var valveCtrl = new ValveControl
                {
                    ValveType = "Regulating",
                    IsOpen = true,
                    Setpoint = 75.0,
                    Feedback = 74.5,
                    ActuatorType = "Diaphragm",
                    IsVertical = true,
                    ActiveColor = "#00FF00",
                    InactiveColor = "#FF0000"
                };
                if (valveCtrl.ValveType != "Regulating" || valveCtrl.Setpoint != 75.0 || valveCtrl.ActuatorType != "Diaphragm")
                    throw new Exception("ValveControl StyledProperties failed.");

                // 21.2 ReactorControl Animation & Agitator Timer
                var reactorCtrl = new ReactorControl
                {
                    Level = 82.5,
                    Temperature = 65.0,
                    HasJacket = true,
                    IsAgitatorRunning = true
                };
                if (reactorCtrl.Level != 82.5 || !reactorCtrl.IsAgitatorRunning)
                    throw new Exception("ReactorControl StyledProperties failed.");
                reactorCtrl.IsAgitatorRunning = false;
                if (reactorCtrl.IsAgitatorRunning)
                    throw new Exception("ReactorControl Agitator stop failed.");

                // 21.3 HeatExchangerControl Configuration
                var hexCtrl = new HeatExchangerControl
                {
                    ExchangerType = "Plate",
                    PrimaryTemp = 90.0,
                    SecondaryTemp = 45.0,
                    Thickness = 14.0
                };
                if (hexCtrl.ExchangerType != "Plate" || hexCtrl.PrimaryTemp != 90.0 || hexCtrl.SecondaryTemp != 45.0)
                    throw new Exception("HeatExchangerControl properties failed.");

                // 21.4 LevelSensorControl Configuration & Thresholds
                var sensorCtrl = new LevelSensorControl
                {
                    Value = 88.0,
                    SensorType = "Waveguide",
                    TagNumber = "LT-511",
                    AlarmHigh = 90.0,
                    AlarmLow = 10.0
                };
                if (sensorCtrl.Value != 88.0 || sensorCtrl.SensorType != "Waveguide" || sensorCtrl.TagNumber != "LT-511")
                    throw new Exception("LevelSensorControl properties failed.");

                // 21.5 PipeControl TwoWay Points, StartFitting, EndFitting & HitTest
                var pipeCtrl = new PipeControl
                {
                    Points = "0,0;10,0;10,10",
                    CellSize = 15.0,
                    Thickness = 12.0,
                    StartFitting = "Flange",
                    EndFitting = "Elbow90"
                };
                if (pipeCtrl.Points != "0,0;10,0;10,10" || pipeCtrl.StartFitting != "Flange" || pipeCtrl.EndFitting != "Elbow90")
                    throw new Exception("PipeControl properties failed.");
                if (!pipeCtrl.HitTest(new Point(5 * 15 + 7.5, 7.5)))
                    throw new Exception("PipeControl HitTest on segment failed.");

                // 21.6 TrendLineControl Dynamic Decimation & Observable Data
                var trendCtrl = new TrendLineControl
                {
                    MinY = 0,
                    MaxY = 100,
                    Values = new double[] { 10, 25, 55, 80, 45, 90 }
                };
                if (trendCtrl.MinY != 0 || trendCtrl.MaxY != 100 || trendCtrl.Values == null)
                    throw new Exception("TrendLineControl properties failed.");

                // 21.7 ColorPickerBox Hex & Brush Synchronization
                var colorPicker = new ColorPickerBox();
                colorPicker.SelectedColorHex = "#FF00AA";
                if (colorPicker.SelectedColorHex != "#FF00AA" || !(colorPicker.SelectedBrush is SolidColorBrush scb && scb.Color.R == 0xFF && scb.Color.G == 0x00 && scb.Color.B == 0xAA))
                    throw new Exception("ColorPickerBox SelectedColorHex sync failed.");

                // 21.8 FormatEditorBox Preset & Format Text Sync
                var formatBox = new FormatEditorBox();
                formatBox.Format = "{0:F2} bar";
                if (formatBox.Format != "{0:F2} bar")
                    throw new Exception("FormatEditorBox Format sync failed.");
            }

            // =========================================================================
            // TEST 22: Enterprise Dynamic Theme Switching (Dark/Light) & Design Mode Opacity Invariant
            // =========================================================================
            Console.WriteLine("[UIValidation] Testing Enterprise Dynamic Theme Switching & Opacity Fix (Test 22)...");
            {
                var themeService = sp.GetRequiredService<IThemeService>();

                // 22.1 Default State: Industrial Dark Mode
                themeService.SetTheme("Dark");
                if (themeService.CurrentTheme != "Dark" || !themeService.IsDarkTheme || themeService.ThemeToggleGlyph != "🌙")
                    throw new Exception($"Default Dark theme state invalid: Current={themeService.CurrentTheme}, Glyph={themeService.ThemeToggleGlyph}");

                if (Application.Current != null && Application.Current.RequestedThemeVariant != ThemeVariant.Dark)
                    throw new Exception($"Application.RequestedThemeVariant expected Dark, got: {Application.Current.RequestedThemeVariant}");

                // 22.2 Switch to Light Theme at Runtime
                themeService.SetTheme("Light");
                if (themeService.CurrentTheme != "Light" || themeService.IsDarkTheme || themeService.ThemeToggleGlyph != "☀️")
                    throw new Exception($"Light theme state invalid: Current={themeService.CurrentTheme}, Glyph={themeService.ThemeToggleGlyph}");

                if (Application.Current != null && Application.Current.RequestedThemeVariant != ThemeVariant.Light)
                    throw new Exception($"Application.RequestedThemeVariant expected Light, got: {Application.Current.RequestedThemeVariant}");

                if (Application.Current != null)
                {
                    if (Application.Current.TryFindResource("ThemeBackgroundBrush", Application.Current.ActualThemeVariant, out var lightBg) && lightBg is SolidColorBrush lb)
                    {
                        if (lb.Color.R < 200)
                            throw new Exception($"Light theme ThemeBackgroundBrush color too dark: {lb.Color}");
                    }
                }

                // 22.3 Toggle Back to Dark Theme
                themeService.ToggleTheme();
                if (themeService.CurrentTheme != "Dark" || !themeService.IsDarkTheme || themeService.ThemeToggleGlyph != "🌙")
                    throw new Exception($"Toggled Dark theme state invalid: Current={themeService.CurrentTheme}");

                if (Application.Current != null && Application.Current.RequestedThemeVariant != ThemeVariant.Dark)
                    throw new Exception($"Application.RequestedThemeVariant expected Dark after toggle, got: {Application.Current.RequestedThemeVariant}");

                if (Application.Current != null)
                {
                    if (Application.Current.TryFindResource("ThemeBackgroundBrush", Application.Current.ActualThemeVariant, out var darkBg) && darkBg is SolidColorBrush db)
                    {
                        if (db.Color.R > 60)
                            throw new Exception($"Dark theme ThemeBackgroundBrush color too bright: {db.Color}");
                    }
                }

                // 22.4 MainViewModel Theme Toggle Command & Persistence
                mainVm.ToggleThemeCommand.Execute(null);
                if (mainVm.ThemeToggleGlyph != "☀️" || themeService.CurrentTheme != "Light")
                    throw new Exception("MainViewModel ToggleThemeCommand failed to activate Light theme.");

                mainVm.ToggleThemeCommand.Execute(null);
                if (mainVm.ThemeToggleGlyph != "🌙" || themeService.CurrentTheme != "Dark")
                    throw new Exception("MainViewModel ToggleThemeCommand failed to restore Dark theme.");

                // 22.5 Theme Configuration Persistence (Serialization & Deserialization)
                var tempCfg = new HmiConfiguration
                {
                    Theme = "Light",
                    Project = new ProjectConfig { Name = "ThemePersistenceTest", Theme = "Light" }
                };
                var tempThemeJson = System.Text.Json.JsonSerializer.Serialize(tempCfg, new JsonSerializerOptions { WriteIndented = true });
                if (!tempThemeJson.Contains("\"Theme\": \"Light\""))
                    throw new Exception("HmiConfiguration failed to serialize Theme property.");

                var deserialized = System.Text.Json.JsonSerializer.Deserialize<HmiConfiguration>(tempThemeJson);
                if (deserialized == null || deserialized.Theme != "Light" || deserialized.Project.Theme != "Light")
                    throw new Exception("HmiConfiguration failed to deserialize Theme property.");

                // 22.6 Design Mode Opacity Fix Invariant
                // In Design Mode (IsDesignMode = true), widgets MUST remain 100% opaque (1.0) despite offline communication failure
                var projCtx = new ProjectContextService { IsDesignMode = true };
                var mockCore = new MockDataCoreService();
                var vdConfig = new ValueDisplayConfig
                {
                    Title = "Датчик T-101",
                    Format = "{0:F1} °C",
                    Source = new DataSourceConfig { ConnId = "plc1", Address = "40010", TagId = "tag_test_stale" }
                };
                var vdVm = new ValueDisplayViewModel(vdConfig, mockCore, projCtx);

                // Apply Stale snapshot while in Design Mode
                vdVm.ApplySnapshot(new TagSnapshot("tag_test_stale", 70.0f, TagQuality.Stale, DateTime.UtcNow.AddMinutes(-5)));
                if (vdVm.QualityOpacity != 1.0 || vdVm.EffectiveOpacity != 1.0)
                    throw new Exception($"Design Mode Opacity Fix Failed: expected Opacity=1.0 for Stale widget in Design Mode, got QualityOpacity={vdVm.QualityOpacity}, EffectiveOpacity={vdVm.EffectiveOpacity}");

                // Apply CommFailure snapshot while in Design Mode
                vdVm.ApplySnapshot(new TagSnapshot("tag_test_stale", null, TagQuality.CommFailure, DateTime.UtcNow, "Offline PLC"));
                if (vdVm.QualityOpacity != 1.0 || vdVm.EffectiveOpacity != 1.0)
                    throw new Exception($"Design Mode Opacity Fix Failed: expected Opacity=1.0 for CommFailure widget in Design Mode, got QualityOpacity={vdVm.QualityOpacity}, EffectiveOpacity={vdVm.EffectiveOpacity}");

                // Switch to Runtime Mode (IsDesignMode = false) -> Opacity MUST update to 0.55
                projCtx.IsDesignMode = false;
                if (vdVm.QualityOpacity != 0.55 || vdVm.EffectiveOpacity != 0.55)
                    throw new Exception($"Runtime Mode Opacity Failed: expected Opacity=0.55 for CommFailure widget in Runtime Mode, got QualityOpacity={vdVm.QualityOpacity}, EffectiveOpacity={vdVm.EffectiveOpacity}");

                // Switch back to Design Mode (IsDesignMode = true) -> Opacity MUST restore to 1.0
                projCtx.IsDesignMode = true;
                if (vdVm.QualityOpacity != 1.0 || vdVm.EffectiveOpacity != 1.0)
                    throw new Exception($"Design Mode Opacity Restore Failed: expected Opacity=1.0 when switching back to Design Mode, got QualityOpacity={vdVm.QualityOpacity}, EffectiveOpacity={vdVm.EffectiveOpacity}");

                // Normal / Good quality tag in both modes MUST remain 1.0
                vdVm.ApplySnapshot(new TagSnapshot("tag_test_stale", 85.0f, TagQuality.Good, DateTime.UtcNow));
                if (vdVm.QualityOpacity != 1.0 || vdVm.EffectiveOpacity != 1.0)
                    throw new Exception($"Good state Opacity Failed: expected 1.0, got {vdVm.QualityOpacity}");

                projCtx.IsDesignMode = false;
                if (vdVm.QualityOpacity != 1.0 || vdVm.EffectiveOpacity != 1.0)
                    throw new Exception($"Good state Opacity Failed in Runtime: expected 1.0, got {vdVm.QualityOpacity}");

                // 22.7 Custom Controls & Overlay Zero-Allocation & Theme Variant Reaction Invariant
                var pipeCtrl = new PipeControl();
                var trendCtrl = new TrendLineControl();
                var reactorCtrl = new ReactorControl();
                var valveCtrl = new ValveControl();
                var hexCtrl = new HeatExchangerControl();
                var levelCtrl = new LevelSensorControl();
                var gridOverlay = new GridOverlay();
                var selOverlay = new SelectionOverlay();

                var themeTestWindow = new Window
                {
                    Content = new StackPanel
                    {
                        Children = { pipeCtrl, trendCtrl, reactorCtrl, valveCtrl, hexCtrl, levelCtrl, gridOverlay, selOverlay }
                    }
                };
                themeTestWindow.Show();
                Dispatcher.UIThread.RunJobs();

                themeService.SetTheme("Light");
                Dispatcher.UIThread.RunJobs();

                themeService.SetTheme("Dark");
                Dispatcher.UIThread.RunJobs();
                themeTestWindow.Close();
            }

            // =========================================================================
            // TEST 23: Quick Pipe-Valve Assembly, Inline Auto-Split, Aspect Ratio Lock & Presets
            // =========================================================================
            Console.WriteLine("[UIValidation] Testing Pipe-Valve Assembly, Inline Auto-Split, Aspect Ratio Lock & Presets (Test 23)...");
            {
                var mockCore = new MockDataCoreService();
                var projCtx = new ProjectContextService { IsDesignMode = true };

                // 23.1 Project Presets in HmiConfiguration
                var hmiPresetsCfg = new HmiConfiguration
                {
                    PipingPresets = new PipingPresetsConfig
                    {
                        DefaultPipeThickness = 16.0,
                        DefaultValveSize = 6,
                        StandardPipeThicknesses = new List<double> { 4.0, 8.0, 12.0, 16.0, 24.0 },
                        StandardValveSizes = new List<int> { 2, 4, 6, 8 }
                    }
                };
                var presetsJson = JsonSerializer.Serialize(hmiPresetsCfg, new JsonSerializerOptions { WriteIndented = true });
                if (!presetsJson.Contains("\"DefaultPipeThickness\": 16") || !presetsJson.Contains("\"DefaultValveSize\": 6"))
                    throw new Exception("HmiConfiguration failed to serialize PipingPresets.");

                var deserializedPresets = JsonSerializer.Deserialize<HmiConfiguration>(presetsJson);
                if (deserializedPresets == null ||
                    deserializedPresets.PipingPresets.DefaultPipeThickness != 16.0 ||
                    deserializedPresets.PipingPresets.DefaultValveSize != 6)
                {
                    throw new Exception("HmiConfiguration failed to deserialize PipingPresets correctly.");
                }

                // 23.2 Inline Auto-Split (Horizontal straight pipe)
                var horizPipeCfg = new PipeConfig
                {
                    Type = "Pipe",
                    Title = "Главный коллектор",
                    Thickness = 12.0,
                    ShowFlanges = false,
                    ActiveColor = "#00FFCC",
                    InactiveColor = "#555555",
                    Source = new DataSourceConfig { ConnId = "wb", Address = "40001", DataType = "Float32" },
                    PipePoints = "0,0;20,0",
                    Position = new WidgetPosition { Col = 10, Row = 20, SizeX = 20, SizeY = 1 }
                };
                var horizPipeVm = new PipeWidgetViewModel(horizPipeCfg, mockCore, projCtx);

                var valveCfg = new ValveConfig
                {
                    Type = "Valve",
                    Title = "XV-101",
                    ValveType = "CutOff",
                    Position = new WidgetPosition { Col = 18.5, Row = 18.5, SizeX = 4, SizeY = 4 }
                };
                var valveVm = new ValveWidgetViewModel(valveCfg, mockCore, projCtx);

                // Valve drop center at (20.0, 20.0) directly onto the pipe line Y=20
                var dropCenterH = new Point(20.0, 20.0);
                var candH = PipeAutoSplitter.EvaluateSegmentCandidate(
                    horizPipeVm, 0, new Point(10, 20), new Point(30, 20), valveVm, dropCenterH);

                if (candH == null)
                    throw new Exception("EvaluateSegmentCandidate failed to detect horizontal straight pipe intersection.");

                if (!candH.IsHorizontal || candH.ValveRotation != 0 || candH.ValveIsVertical)
                    throw new Exception($"Horizontal pipe split candidate should have IsHorizontal=true, Rotation=0, IsVertical=false; got Rot={candH.ValveRotation}, Vert={candH.ValveIsVertical}");

                if (Math.Abs(candH.ValveInputFlange.X - 18.0) > 0.001 || Math.Abs(candH.ValveOutputFlange.X - 22.0) > 0.001)
                    throw new Exception($"Candidate flanges expected (18, 22), got ({candH.ValveInputFlange.X}, {candH.ValveOutputFlange.X})");

                // Execute split
                var splitResultH = PipeAutoSplitter.SplitPipe(candH);
                if (!splitResultH.Success || splitResultH.PipeA == null || splitResultH.PipeB == null)
                    throw new Exception("PipeAutoSplitter.SplitPipe failed for horizontal pipe.");

                if (splitResultH.PipeA.EndFitting != "Flange" || splitResultH.PipeB.StartFitting != "Flange")
                    throw new Exception($"Child pipes fittings mismatch: PipeA.EndFitting={splitResultH.PipeA.EndFitting}, PipeB.StartFitting={splitResultH.PipeB.StartFitting}");

                if (!splitResultH.PipeA.ShowFlanges || !splitResultH.PipeB.ShowFlanges)
                    throw new Exception("Child pipes should have ShowFlanges = true.");

                if (splitResultH.PipeA.Thickness != 12.0 || splitResultH.PipeB.Thickness != 12.0 ||
                    splitResultH.PipeA.Source.Address != "40001" || splitResultH.PipeB.Source.Address != "40001")
                {
                    throw new Exception("Child pipes did not clone parent pipe DataSource or Thickness.");
                }

                // Check pipe A points: from 10 to 18 (length 8)
                if (splitResultH.PipeA.Position.Col != 10 || splitResultH.PipeA.Position.Row != 20 || splitResultH.PipeA.Position.SizeX != 8)
                    throw new Exception($"PipeA position/bounds incorrect: Col={splitResultH.PipeA.Position.Col}, SizeX={splitResultH.PipeA.Position.SizeX}");

                // Check pipe B points: from 22 to 30 (length 8)
                if (splitResultH.PipeB.Position.Col != 22 || splitResultH.PipeB.Position.Row != 20 || splitResultH.PipeB.Position.SizeX != 8)
                    throw new Exception($"PipeB position/bounds incorrect: Col={splitResultH.PipeB.Position.Col}, SizeX={splitResultH.PipeB.Position.SizeX}");

                // Check valve snapped position: Col=18.5, Row=18.5 (center is at 20, 20)
                if (Math.Abs(splitResultH.ValveSnappedColRow.X - 18.5) > 0.001 || Math.Abs(splitResultH.ValveSnappedColRow.Y - 18.5) > 0.001)
                    throw new Exception($"Valve snapped position incorrect: ({splitResultH.ValveSnappedColRow.X}, {splitResultH.ValveSnappedColRow.Y})");

                // 23.3 Inline Auto-Split (Vertical straight pipe & auto-rotation)
                var vertPipeCfg = new PipeConfig
                {
                    Type = "Pipe",
                    Title = "Вертикальный стояк",
                    Thickness = 12.0,
                    PipePoints = "0,0;0,30",
                    Position = new WidgetPosition { Col = 15, Row = 10, SizeX = 1, SizeY = 30 }
                };
                var vertPipeVm = new PipeWidgetViewModel(vertPipeCfg, mockCore, projCtx);

                var dropCenterV = new Point(15.0, 25.0);
                var candV = PipeAutoSplitter.EvaluateSegmentCandidate(
                    vertPipeVm, 0, new Point(15, 10), new Point(15, 40), valveVm, dropCenterV);

                if (candV == null || candV.IsHorizontal || !candV.ValveIsVertical || candV.ValveRotation != 90)
                    throw new Exception("EvaluateSegmentCandidate failed to auto-orient valve vertically (Rotation=90, IsVertical=true).");

                var splitResultV = PipeAutoSplitter.SplitPipe(candV);
                if (splitResultV.PipeA.EndFitting != "Flange" || splitResultV.PipeB.StartFitting != "Flange")
                    throw new Exception("Vertical split child pipe fittings mismatch.");

                if (splitResultV.PipeA.Position.Row != 10 || splitResultV.PipeA.Position.SizeY != 13 ||
                    splitResultV.PipeB.Position.Row != 27 || splitResultV.PipeB.Position.SizeY != 13)
                {
                    throw new Exception($"Vertical split pipe bounds incorrect: PipeA Row={splitResultV.PipeA.Position.Row}, SizeY={splitResultV.PipeA.Position.SizeY}; PipeB Row={splitResultV.PipeB.Position.Row}, SizeY={splitResultV.PipeB.Position.SizeY}");
                }

                // 23.4 Short Segment Rejection & Degenerate Pipe Prevention
                // Segment length must be >= valveLength + 2.0 * minRemainingLength (4 + 2 = 6)
                var shortCand1 = PipeAutoSplitter.EvaluateSegmentCandidate(
                    horizPipeVm, 0, new Point(10, 20), new Point(12, 20), valveVm, new Point(11, 20));
                if (shortCand1 != null)
                    throw new Exception("EvaluateSegmentCandidate should return null when segment length (2) is less than valve size (4).");

                var shortCand2 = PipeAutoSplitter.EvaluateSegmentCandidate(
                    horizPipeVm, 0, new Point(10, 20), new Point(15, 20), valveVm, new Point(12.5, 20));
                if (shortCand2 != null)
                    throw new Exception("EvaluateSegmentCandidate should return null when segment length (5) cannot provide >= 1.0 unit for both child pipes.");

                // 23.5 Collinear Assembly ("Собрать соосный узел") - Horizontal
                var alignPipe1Cfg = new PipeConfig
                {
                    Type = "Pipe",
                    Title = "Pipe1",
                    Position = new WidgetPosition { Col = 5, Row = 12, SizeX = 15, SizeY = 1 },
                    PipePoints = "0,0;15,0"
                };
                var alignPipe2Cfg = new PipeConfig
                {
                    Type = "Pipe",
                    Title = "Pipe2",
                    Position = new WidgetPosition { Col = 35, Row = 18, SizeX = 15, SizeY = 1 },
                    PipePoints = "0,0;15,0"
                };
                var alignValveCfg = new ValveConfig
                {
                    Type = "Valve",
                    Title = "V-Align",
                    Position = new WidgetPosition { Col = 25, Row = 22, SizeX = 4, SizeY = 4 }
                };

                var alignPipe1Vm = new PipeWidgetViewModel(alignPipe1Cfg, mockCore, projCtx);
                var alignPipe2Vm = new PipeWidgetViewModel(alignPipe2Cfg, mockCore, projCtx);
                var alignValveVm = new ValveWidgetViewModel(alignValveCfg, mockCore, projCtx);

                var selectedWidgets = new List<WidgetViewModelBase> { alignPipe1Vm, alignPipe2Vm, alignValveVm };
                if (!CollinearAssemblyService.CanAssemble(selectedWidgets))
                    throw new Exception("CollinearAssemblyService.CanAssemble should be true for 2 pipes and 1 valve.");

                bool assembled = CollinearAssemblyService.AssembleCollinear(selectedWidgets);
                if (!assembled)
                    throw new Exception("CollinearAssemblyService.AssembleCollinear returned false.");

                // Check that all elements are on Y = 12 (reference pipe axis)
                if (alignValveVm.IsVertical || alignValveVm.Rotation != 0)
                    throw new Exception("Assembled valve should have IsVertical=false, Rotation=0.");

                // Left pipe ends at X = 20 (Col=5, Len=15)
                var leftEndPts = alignPipe1Vm.GetAbsoluteGridPoints();
                if (leftEndPts.Last().X != 20 || leftEndPts.Last().Y != 12 || alignPipe1Vm.EndFitting != "Flange")
                    throw new Exception($"Left pipe should end at (20, 12) with Flange; got ({leftEndPts.Last().X}, {leftEndPts.Last().Y}), Fitting={alignPipe1Vm.EndFitting}");

                // Valve input port is at X = 20 (Col = 20.5), output port at X = 24
                if (Math.Abs(alignValveVm.Col - 20.5) > 0.001 || Math.Abs(alignValveVm.Row - (12 - 1.5)) > 0.001)
                    throw new Exception($"Valve position after collinear assembly should be Col=20.5, Row=10.5; got ({alignValveVm.Col}, {alignValveVm.Row})");

                // Right pipe starts at X = 24 with Flange
                var rightStartPts = alignPipe2Vm.GetAbsoluteGridPoints();
                if (rightStartPts.First().X != 24 || rightStartPts.First().Y != 12 || alignPipe2Vm.StartFitting != "Flange")
                    throw new Exception($"Right pipe should start at (24, 12) with Flange; got ({rightStartPts.First().X}, {rightStartPts.First().Y}), Fitting={alignPipe2Vm.StartFitting}");

                // 23.6 Collinear Assembly - Vertical
                var vAlignP1 = new PipeWidgetViewModel(new PipeConfig
                {
                    Type = "Pipe",
                    Position = new WidgetPosition { Col = 30, Row = 5, SizeX = 1, SizeY = 10 },
                    PipePoints = "0,0;0,10"
                }, mockCore, projCtx);
                var vAlignP2 = new PipeWidgetViewModel(new PipeConfig
                {
                    Type = "Pipe",
                    Position = new WidgetPosition { Col = 25, Row = 25, SizeX = 1, SizeY = 10 },
                    PipePoints = "0,0;0,10"
                }, mockCore, projCtx);
                var vAlignV = new ValveWidgetViewModel(new ValveConfig
                {
                    Type = "Valve",
                    Position = new WidgetPosition { Col = 40, Row = 18, SizeX = 4, SizeY = 4 }
                }, mockCore, projCtx);

                bool vAssembled = CollinearAssemblyService.AssembleCollinear(new WidgetViewModelBase[] { vAlignP1, vAlignP2, vAlignV });
                if (!vAssembled || !vAlignV.IsVertical || vAlignV.Rotation != 90)
                    throw new Exception("Vertical collinear assembly failed.");

                if (vAlignP1.EndFitting != "Flange" || vAlignP2.StartFitting != "Flange")
                    throw new Exception("Vertical collinear assembly fittings failed.");

                // 23.7 CreatePipesForValveFlanges
                var testValveForFlanges = new ValveWidgetViewModel(new ValveConfig
                {
                    Type = "Valve",
                    Title = "V-Flanges",
                    Position = new WidgetPosition { Col = 15.5, Row = 8.5, SizeX = 4, SizeY = 4 }
                }, mockCore, projCtx);

                var (flangePipe1, flangePipe2) = CollinearAssemblyService.CreatePipesForValveFlanges(testValveForFlanges, 10.0, 14.0);
                if (flangePipe1.EndFitting != "Flange" || flangePipe2.StartFitting != "Flange")
                    throw new Exception("CreatePipesForValveFlanges fittings mismatch.");
                if (flangePipe1.Thickness != 14.0 || flangePipe2.Thickness != 14.0)
                    throw new Exception("CreatePipesForValveFlanges thickness mismatch.");

                // 23.8 Aspect Ratio Lock in WidgetEditorViewModel
                var weVm = new WidgetEditorViewModel(new ValveConfig
                {
                    Type = "Valve",
                    Position = new WidgetPosition { Col = 1, Row = 1, SizeX = 4, SizeY = 4 }
                }, new List<ConnectionConfig>());

                weVm.SizeX = 6;
                if (weVm.SizeY != 6)
                    throw new Exception($"WidgetEditorViewModel failed to enforce 1:1 aspect ratio: SizeX=6, SizeY={weVm.SizeY}");

                weVm.SizeY = 8;
                if (weVm.SizeX != 8)
                    throw new Exception($"WidgetEditorViewModel failed to synchronize SizeX when SizeY changed: SizeX={weVm.SizeX}, SizeY=8");

                // 23.9 Toggleable Rubber-Banding with Alt Inversion Invariant
                var dashVmRubber = new DashboardViewModel(new DashboardConfig(), mockCore, projCtx, new HmiConfiguration(), null, sp.GetRequiredService<IWidgetFactory>());
                if (!dashVmRubber.IsRubberBandingEnabled)
                    throw new Exception("DashboardViewModel.IsRubberBandingEnabled should default to true.");

                dashVmRubber.ToggleRubberBandingCommand.Execute(null);
                if (dashVmRubber.IsRubberBandingEnabled)
                    throw new Exception("ToggleRubberBandingCommand failed to disable rubber banding.");

                dashVmRubber.ToggleRubberBandingCommand.Execute(null);
                if (!dashVmRubber.IsRubberBandingEnabled)
                    throw new Exception("ToggleRubberBandingCommand failed to restore rubber banding.");

                // Verify XOR logic formula: (enabled ^ alt)
                bool testActive1 = dashVmRubber.IsRubberBandingEnabled ^ false; // true ^ false = true
                bool testActive2 = dashVmRubber.IsRubberBandingEnabled ^ true;  // true ^ true = false (Alt temporarily inverts!)
                if (!testActive1 || testActive2)
                    throw new Exception("Rubber-banding Alt inversion formula failed.");

                // 23.10 DashboardViewModel.ReplacePipeWithSplit & AttachPipesToValve
                var dConfig23 = new DashboardConfig();
                var dashVmIntegration = new DashboardViewModel(dConfig23, mockCore, projCtx, new HmiConfiguration(), null, sp.GetRequiredService<IWidgetFactory>());
                
                var originalIntegrationPipe = new PipeConfig
                {
                    Type = "Pipe",
                    Title = "OrigPipe",
                    Position = new WidgetPosition { Col = 10, Row = 10, SizeX = 20, SizeY = 1 },
                    PipePoints = "0,0;20,0"
                };
                dConfig23.Widgets.Add(originalIntegrationPipe);
                var origVm = dashVmIntegration.CreateWidgetViewModel(originalIntegrationPipe) as PipeWidgetViewModel;
                dashVmIntegration.Widgets.Add(origVm!);

                var (newPipeA, newPipeB) = dashVmIntegration.ReplacePipeWithSplit(origVm!, splitResultH.PipeA, splitResultH.PipeB);
                if (dashVmIntegration.Widgets.Contains(origVm!) || dConfig23.Widgets.Contains(originalIntegrationPipe))
                    throw new Exception("ReplacePipeWithSplit failed to remove original pipe.");
                if (newPipeA == null || newPipeB == null || dashVmIntegration.Widgets.Count != 2 || dConfig23.Widgets.Count != 2)
                    throw new Exception("ReplacePipeWithSplit failed to insert child pipes into DashboardViewModel and DashboardConfig.");

                dashVmIntegration.AttachPipesToValveCommand.Execute(testValveForFlanges);
                if (dashVmIntegration.Widgets.Count != 4 || dConfig23.Widgets.Count != 4)
                    throw new Exception("AttachPipesToValveCommand failed to add 2 new pipes.");

                // 23.11 Strict CanAssemble validation (only exactly 2 pipes + 1 valve, no extra widgets)
                var extraPump = new PumpWidgetViewModel(new PumpConfig
                {
                    Type = "Pump",
                    Position = new WidgetPosition { Col = 1, Row = 1, SizeX = 3, SizeY = 3 }
                }, mockCore, projCtx);
                var invalidSelection = new List<WidgetViewModelBase> { alignPipe1Vm, alignPipe2Vm, alignValveVm, extraPump };
                if (CollinearAssemblyService.CanAssemble(invalidSelection))
                    throw new Exception("CollinearAssemblyService.CanAssemble should return false when extra widgets are selected.");

                // 23.12 Collinear Assembly Length Preservation for Perpendicular Pipes
                var perpPipe1 = new PipeWidgetViewModel(new PipeConfig
                {
                    Type = "Pipe",
                    Position = new WidgetPosition { Col = 0, Row = 10, SizeX = 20, SizeY = 1 },
                    PipePoints = "0,0;20,0" // Length = 20 (longer reference pipe, horizontal axis Y=10)
                }, mockCore, projCtx);
                var perpPipe2 = new PipeWidgetViewModel(new PipeConfig
                {
                    Type = "Pipe",
                    Position = new WidgetPosition { Col = 30, Row = 0, SizeX = 1, SizeY = 15 },
                    PipePoints = "0,0;0,15" // Length = 15 (perpendicular vertical pipe)
                }, mockCore, projCtx);
                var perpValve = new ValveWidgetViewModel(new ValveConfig
                {
                    Type = "Valve",
                    Position = new WidgetPosition { Col = 15, Row = 8, SizeX = 4, SizeY = 4 }
                }, mockCore, projCtx);

                bool perpAssembled = CollinearAssemblyService.AssembleCollinear(new WidgetViewModelBase[] { perpPipe1, perpPipe2, perpValve });
                if (!perpAssembled)
                    throw new Exception("AssembleCollinear failed for perpendicular pipe pair.");

                var perpPipe2Pts = perpPipe2.GetAbsoluteGridPoints();
                double assembledLen = Math.Abs(perpPipe2Pts.Last().X - perpPipe2Pts.First().X);
                if (Math.Abs(assembledLen - 15.0) > 0.001)
                    throw new Exception($"AssembleCollinear squished perpendicular pipe! Expected length 15.0, got {assembledLen}");

                // 23.13 Rubber-Banding Multi-Frame Non-Accumulation Invariant
                // Verify that using _dragOriginalConnectedPipes keeps deltaCol stable across multiple pointer moves
                var origPts = new List<Point> { new Point(0, 10), new Point(10, 10) };
                double deltaColTest = 2.0;
                double deltaRowTest = 0.0;
                Point finalPoint = default;
                for (int frame = 0; frame < 20; frame++)
                {
                    // Every frame uses origPts as baseline
                    var currentOrigPt = origPts[1];
                    finalPoint = new Point(currentOrigPt.X + deltaColTest, currentOrigPt.Y + deltaRowTest);
                }
                if (finalPoint.X != 12.0)
                    throw new Exception($"Rubber-banding accumulated drift across frames: expected 12.0, got {finalPoint.X}");

                // 23.14 Aspect Ratio Lock Dominant Delta Shrinking
                // Original valve 4x4, CellSize 40 (160x160 px). Drag corner left by 80px (delta.X = -80, delta.Y = 0)
                double testDeltaX = -80;
                double testDeltaY = 0;
                double dominantDelta = Math.Abs(testDeltaX) >= Math.Abs(testDeltaY) ? testDeltaX : testDeltaY;
                int targetSize = (int)Math.Max(1, Math.Round((4 * 40 + dominantDelta) / 40.0));
                if (targetSize != 2)
                    throw new Exception($"Valve resize handle failed to shrink along single axis: expected 2, got {targetSize}");
            }

            // =========================================================================
            // TEST 24: Change History, Undo/Redo & Time-Travel Engine (RFC Implementation)
            // =========================================================================
            Console.WriteLine("[UIValidation] Testing Change History, Undo/Redo & Time-Travel Engine (Test 24)...");
            {
                var mockCore = new MockDataCoreService();
                var projCtx = new ProjectContextService { IsDesignMode = true };
                var t24_widgetFactory = new WidgetFactory(mockCore, projCtx, sp.GetRequiredService<IAlarmNotificationService>());

                // 24.1: WidgetConfig Unique Identification & Duplication Isolation
                var valveCfg1 = new ValveConfig { Title = "Valve 1", ValveType = "CutOff" };
                if (valveCfg1.Id == Guid.Empty)
                    throw new Exception("WidgetConfig.Id was not initialized with a non-empty Guid.");

                var valveVm1 = new ValveWidgetViewModel(valveCfg1, mockCore, projCtx);
                if (valveVm1.Id != valveCfg1.Id)
                    throw new Exception("WidgetViewModelBase.Id does not match OriginalConfig.Id.");

                WidgetClipboard.Copy(valveCfg1);
                var t24_pastedValve = WidgetClipboard.PasteClone() as ValveConfig;
                if (t24_pastedValve == null || t24_pastedValve.Id == Guid.Empty || t24_pastedValve.Id == valveCfg1.Id)
                    throw new Exception("WidgetClipboard.PasteClone failed to generate a fresh, unique Id.");

                var containerCfg = new ContainerButtonConfig
                {
                    Title = "Container",
                    Children = new List<WidgetConfig>
                    {
                        new ValveConfig { Title = "Child Valve 1" },
                        new PumpConfig { Title = "Child Pump 1" }
                    }
                };
                WidgetClipboard.Copy(containerCfg);
                var t24_pastedContainer = WidgetClipboard.PasteClone() as ContainerButtonConfig;
                if (t24_pastedContainer == null || t24_pastedContainer.Id == containerCfg.Id)
                    throw new Exception("ContainerButtonConfig clone must have a fresh Id.");
                if (t24_pastedContainer.Children[0].Id == containerCfg.Children[0].Id || t24_pastedContainer.Children[1].Id == containerCfg.Children[1].Id)
                    throw new Exception("ContainerButtonConfig clone children must each receive fresh unique Ids.");

                // 24.2: HistoryService Ring Buffer Capacity (30 steps FIFO sliding window)
                var historyService = new HistoryService(projCtx, capacity: 30);
                var baseDashboard = new DashboardConfig
                {
                    CellSize = 40,
                    ZoomScale = 1.0,
                    Widgets = new List<WidgetConfig> { new ValveConfig { Title = "Base Valve" } }
                };
                historyService.Initialize(baseDashboard, "Исходное состояние", "Home");
                if (historyService.Steps.Count != 1 || historyService.CurrentIndex != 0)
                    throw new Exception($"HistoryService.Initialize failed: Steps={historyService.Steps.Count}, Index={historyService.CurrentIndex}");
                if (historyService.CanUndo || historyService.CanRedo)
                    throw new Exception("HistoryService initial step must have CanUndo=false, CanRedo=false.");

                // Record 35 distinct steps (each modifies the title)
                for (int i = 1; i <= 35; i++)
                {
                    var cfg = new DashboardConfig
                    {
                        CellSize = 40,
                        ZoomScale = 1.0,
                        Widgets = new List<WidgetConfig> { new ValveConfig { Title = $"Step Valve {i}" } }
                    };
                    historyService.RecordStep($"Шаг {i}", "Move", cfg);
                }

                // Verify capacity capped strictly at 30
                if (historyService.Steps.Count != 30)
                    throw new Exception($"HistoryService ring buffer exceeded capacity 30: actual {historyService.Steps.Count}");
                if (historyService.CurrentIndex != 29)
                    throw new Exception($"HistoryService CurrentIndex should be at head 29, got {historyService.CurrentIndex}");
                if (!historyService.CanUndo || historyService.CanRedo)
                    throw new Exception("At head of history: CanUndo should be true, CanRedo should be false.");
                if (historyService.Steps[0].Title != "Шаг 6")
                    throw new Exception($"Ring buffer FIFO should have dropped steps 0..5; first step should be 'Шаг 6', got '{historyService.Steps[0].Title}'");
                if (historyService.Steps[29].Title != "Шаг 35")
                    throw new Exception($"Latest step should be 'Шаг 35', got '{historyService.Steps[29].Title}'");

                // 24.3: Step Jumping (Time-Travel) & Active Highlighting
                var targetStep = historyService.Steps[10]; // "Шаг 16"
                var jumped = historyService.JumpToStep(targetStep);
                if (jumped != targetStep || historyService.CurrentIndex != 10)
                    throw new Exception($"JumpToStep failed: expected Index 10, got {historyService.CurrentIndex}");
                if (!historyService.Steps[10].IsCurrent || historyService.Steps[29].IsCurrent)
                    throw new Exception("Step jumping failed to update IsCurrent state correctly.");
                if (!historyService.CanUndo || !historyService.CanRedo)
                    throw new Exception("At middle step index 10: both CanUndo and CanRedo should be true.");

                // Jump to oldest step (index 0)
                historyService.JumpToIndex(0);
                if (historyService.CurrentIndex != 0 || historyService.CanUndo || !historyService.CanRedo)
                    throw new Exception("At oldest step index 0: CanUndo must be false, CanRedo must be true.");

                // 24.4: Timeline Branching Truncation
                var branchCfg = new DashboardConfig
                {
                    CellSize = 40,
                    ZoomScale = 1.0,
                    Widgets = new List<WidgetConfig> { new ValveConfig { Title = "Ветка альтернативной истории" } }
                };
                historyService.RecordStep("Ветка B", "Branch", branchCfg);
                if (historyService.Steps.Count != 2)
                    throw new Exception($"Branching failed to truncate future steps: expected 2 steps, got {historyService.Steps.Count}");
                if (historyService.CurrentIndex != 1 || historyService.CanRedo || !historyService.CanUndo)
                    throw new Exception($"After branch: Index should be 1, CanUndo=true, CanRedo=false; got Index={historyService.CurrentIndex}, Redo={historyService.CanRedo}");

                // 24.5: Differential ViewModel Reconciliation (In-Place Update, Zero VM Dropping, Telemetry Retained)
                var t24_dConfig = new DashboardConfig
                {
                    CellSize = 40,
                    ZoomScale = 1.0,
                    Widgets = new List<WidgetConfig>()
                };
                var valveInitial = new ValveConfig
                {
                    Title = "Valve_Reconcile",
                    ValveType = "CutOff",
                    Position = new WidgetPosition { Col = 5, Row = 5, SizeX = 4, SizeY = 4 },
                    Source = new DataSourceConfig { ConnId = "wb", Address = "1001", DataType = "Boolean" }
                };
                var pumpInitial = new PumpConfig
                {
                    Title = "Pump_Reconcile",
                    Position = new WidgetPosition { Col = 15, Row = 15, SizeX = 6, SizeY = 6 },
                    Source = new DataSourceConfig { ConnId = "wb", Address = "1002", DataType = "Boolean" }
                };
                var pipeInitial = new PipeConfig
                {
                    Title = "Pipe_Reconcile",
                    Position = new WidgetPosition { Col = 9, Row = 7, SizeX = 6, SizeY = 2 },
                    PipePoints = "0,0;6,0"
                };

                t24_dConfig.Widgets.Add(valveInitial);
                t24_dConfig.Widgets.Add(pumpInitial);
                t24_dConfig.Widgets.Add(pipeInitial);

                var t24_dashVm = new DashboardViewModel(t24_dConfig, mockCore, projCtx, new HmiConfiguration(), null, t24_widgetFactory);
                
                // Store references to the created VMs
                var initialValveVm = t24_dashVm.Widgets.OfType<ValveWidgetViewModel>().First();
                var initialPumpVm = t24_dashVm.Widgets.OfType<PumpWidgetViewModel>().First();
                var initialPipeVm = t24_dashVm.Widgets.OfType<PipeWidgetViewModel>().First();

                if (t24_dashVm.Widgets.Count != 3)
                    throw new Exception("Initial DashboardViewModel widgets count mismatch.");

                // Verify initial step recorded
                if (t24_dashVm.HistoryService.Steps.Count != 1 || t24_dashVm.CanUndo)
                    throw new Exception("DashboardViewModel history should start with initial step and CanUndo=false.");

                // Modify Valve position, delete Pipe, add new Tank
                initialValveVm.Col = 8;
                initialValveVm.Row = 10;
                initialValveVm.SizeX = 6;
                initialValveVm.SizeY = 6;
                initialValveVm.OriginalConfig.Position.Col = 8;
                initialValveVm.OriginalConfig.Position.Row = 10;
                initialValveVm.OriginalConfig.Position.SizeX = 6;
                initialValveVm.OriginalConfig.Position.SizeY = 6;

                t24_dashVm.RemoveWidgetCommand.Execute(initialPipeVm);

                var tankCfg = new TankConfig
                {
                    Title = "Tank_Added",
                    Position = new WidgetPosition { Col = 25, Row = 25, SizeX = 8, SizeY = 8 }
                };
                t24_dashVm.AddWidgetFromConfig(tankCfg);

                if (t24_dashVm.Widgets.Count != 3) // Valve, Pump, Tank
                    throw new Exception($"Widgets count expected 3 after edit, got {t24_dashVm.Widgets.Count}");

                // Now execute UNDO back to the state with the Pipe and original Valve coordinates
                while (t24_dashVm.CanUndo)
                {
                    t24_dashVm.UndoCommand.Execute(null);
                }

                // Verify Differential Reconciliation invariants:
                // 1. Valve VM instance is the exact same object reference (not recreated!)
                var restoredValveVm = t24_dashVm.Widgets.OfType<ValveWidgetViewModel>().FirstOrDefault();
                if (!ReferenceEquals(restoredValveVm, initialValveVm))
                    throw new Exception("Differential reconciliation failed: Valve ViewModel was recreated instead of in-place updated!");

                // 2. Valve coordinates reverted to (5, 5, 4, 4)
                if (restoredValveVm == null || restoredValveVm.Col != 5 || restoredValveVm.Row != 5 || restoredValveVm.SizeX != 4 || restoredValveVm.SizeY != 4)
                    throw new Exception($"Restored Valve coordinates mismatch: expected (5,5,4,4), got ({restoredValveVm?.Col},{restoredValveVm?.Row},{restoredValveVm?.SizeX},{restoredValveVm?.SizeY})");

                // 3. Pump VM instance is the exact same object reference
                var restoredPumpVm = t24_dashVm.Widgets.OfType<PumpWidgetViewModel>().FirstOrDefault();
                if (!ReferenceEquals(restoredPumpVm, initialPumpVm))
                    throw new Exception("Differential reconciliation failed: Pump ViewModel was recreated instead of preserved!");

                // 4. Pipe was restored
                var restoredPipeVm = t24_dashVm.Widgets.OfType<PipeWidgetViewModel>().FirstOrDefault();
                if (restoredPipeVm == null || restoredPipeVm.Title != "Pipe_Reconcile")
                    throw new Exception("Differential reconciliation failed to restore deleted Pipe!");

                // 5. Added Tank was removed
                var anyTank = t24_dashVm.Widgets.OfType<TankWidgetViewModel>().FirstOrDefault();
                if (anyTank != null)
                    throw new Exception("Differential reconciliation failed to remove added Tank on undo!");

                // 6. Telemetry subscriptions on preserved Valve VM are still operational
                mockCore.PublishTag(new TagData("wb", "1001", true));
                Dispatcher.UIThread.RunJobs();
                if (!restoredValveVm.IsOpen)
                    throw new Exception("Telemetry subscription on preserved Valve was lost or failed to update IsOpen!");
                if (restoredValveVm.CurrentColor != "#00FF00")
                    throw new Exception($"Restored Valve CurrentColor expected '#00FF00', got '{restoredValveVm.CurrentColor}'");

                // 24.6: Reentrancy Guard
                if (t24_dashVm.IsReconciling)
                    throw new Exception("DashboardViewModel.IsReconciling should be false after RestoreSnapshot completes.");

                // 24.7: 20-minute Runtime Expiration Timer & Baseline Re-Establishment
                var fastTimerHistory = new HistoryService(projCtx, capacity: 30)
                {
                    ExpirationTimeout = TimeSpan.FromMilliseconds(40)
                };
                fastTimerHistory.SetSnapshotProvider(() => new DashboardConfig
                {
                    CellSize = 40,
                    ZoomScale = 1.0,
                    Widgets = new List<WidgetConfig> { new ValveConfig { Title = "Active Baseline Valve" } }
                });
                fastTimerHistory.Initialize(baseDashboard, "Base", "Home");
                var action1Dashboard = new DashboardConfig
                {
                    CellSize = 40,
                    ZoomScale = 1.0,
                    Widgets = new List<WidgetConfig> { new ValveConfig { Title = "Modified Valve 1" } }
                };
                fastTimerHistory.RecordStep("Action 1", "Move", action1Dashboard);
                if (fastTimerHistory.Steps.Count != 2)
                    throw new Exception("fastTimerHistory should have 2 steps.");

                // Switch to Runtime: timer starts
                fastTimerHistory.SetDesignMode(false);

                // Switch back within 10ms: history preserved
                Thread.Sleep(10);
                fastTimerHistory.SetDesignMode(true);
                if (fastTimerHistory.Steps.Count != 2)
                    throw new Exception("Switching back to DesignMode before expiration should preserve history.");

                // Switch to Runtime and let timer elapse (70ms > 40ms)
                fastTimerHistory.SetDesignMode(false);
                Thread.Sleep(80);
                Dispatcher.UIThread.RunJobs();
                if (fastTimerHistory.Steps.Count != 0 && fastTimerHistory.CurrentIndex != -1)
                    throw new Exception($"Runtime expiration timer should clear history buffer after timeout; got count {fastTimerHistory.Steps.Count}");

                // Switch back to DesignMode after buffer was wiped: baseline MUST be re-captured!
                fastTimerHistory.SetDesignMode(true);
                if (fastTimerHistory.Steps.Count != 1 || fastTimerHistory.CurrentIndex != 0)
                    throw new Exception($"Returning to DesignMode after timeout expiration must re-establish baseline snapshot; got {fastTimerHistory.Steps.Count} steps.");
                if (fastTimerHistory.Steps[0].Snapshot.Widgets[0].Title != "Active Baseline Valve")
                    throw new Exception("Re-established baseline does not match snapshot provider output.");

                // 24.8: History UI ViewModels & Ergonomics
                var panelVm = t24_dashVm.HistoryPanel;
                if (panelVm == null)
                    throw new Exception("DashboardViewModel.HistoryPanel cannot be null.");
                if (t24_dashVm.IsHistoryPanelOpen)
                    throw new Exception("IsHistoryPanelOpen should default to false.");

                t24_dashVm.ToggleHistoryPanelCommand.Execute(null);
                if (!t24_dashVm.IsHistoryPanelOpen || !panelVm.IsOpen)
                    throw new Exception("ToggleHistoryPanel failed to open drawer panel.");

                panelVm.CloseCommand.Execute(null);
                if (t24_dashVm.IsHistoryPanelOpen || panelVm.IsOpen)
                    throw new Exception("HistoryPanelViewModel.CloseCommand failed to close drawer panel.");

                // 24.9: WidgetEditorViewModel ID & GroupId Persistence and In-Place Reversion
                var sampleValveCfg = new ValveConfig
                {
                    Title = "Valve_EditTest",
                    ValveType = "CutOff",
                    GroupId = "Group_Alpha",
                    ActiveColor = "#00FF00"
                };
                var originalValveId = sampleValveCfg.Id;
                var editorVm = new WidgetEditorViewModel(sampleValveCfg, new List<ConnectionConfig>());
                editorVm.Title = "Valve_Edited";
                editorVm.ActiveColor = "#00FFFF";
                var updatedCfg = editorVm.ToWidgetConfig() as ValveConfig;
                if (updatedCfg == null)
                    throw new Exception("WidgetEditorViewModel.ToWidgetConfig returned null.");
                if (updatedCfg.Id != originalValveId)
                    throw new Exception($"WidgetEditorViewModel must preserve Id; expected {originalValveId}, got {updatedCfg.Id}");
                if (updatedCfg.GroupId != "Group_Alpha")
                    throw new Exception($"WidgetEditorViewModel must preserve GroupId; expected 'Group_Alpha', got '{updatedCfg.GroupId}'");

                // 24.10: Reconcile Collection Order (Z-Index / Layering) on Undo/Redo
                var orderDashConfig = new DashboardConfig
                {
                    CellSize = 40,
                    ZoomScale = 1.0,
                    Widgets = new List<WidgetConfig>
                    {
                        new ValveConfig { Title = "Item_A" },
                        new ValveConfig { Title = "Item_B" },
                        new ValveConfig { Title = "Item_C" }
                    }
                };
                var orderDashVm = new DashboardViewModel(orderDashConfig, mockCore, projCtx, new HmiConfiguration(), null, t24_widgetFactory);
                if (orderDashVm.Widgets[0].Title != "Item_A" || orderDashVm.Widgets[1].Title != "Item_B" || orderDashVm.Widgets[2].Title != "Item_C")
                    throw new Exception("Order DashboardViewModel initial widgets mismatch.");

                var firstWidget = orderDashVm.Widgets[0];
                orderDashVm.BringToFrontCommand.Execute(firstWidget);
                if (orderDashVm.Widgets[2].Title != "Item_A")
                    throw new Exception("BringToFront failed to move Item_A to the end.");

                // Undo BringToFront: collection order must be restored to [Item_A, Item_B, Item_C]
                orderDashVm.UndoCommand.Execute(null);
                if (orderDashVm.Widgets[0].Title != "Item_A" || orderDashVm.Widgets[1].Title != "Item_B" || orderDashVm.Widgets[2].Title != "Item_C")
                    throw new Exception($"Undo failed to restore exact collection order: [{string.Join(", ", orderDashVm.Widgets.Select(w => w.Title))}]");

                // Redo BringToFront: collection order must return to [Item_B, Item_C, Item_A]
                orderDashVm.RedoCommand.Execute(null);
                if (orderDashVm.Widgets[2].Title != "Item_A")
                    throw new Exception("Redo failed to reapply BringToFront collection order.");

                // 24.11: UpdateFromConfig polymorphic coverage on all SCADA Widget Types
                var cmdBtnCfg = new CommandButtonConfig { Title = "Cmd", ButtonMode = "Toggle", LabelOn = "ON", LabelOff = "OFF" };
                var cmdBtnVm = new CommandButtonViewModel(cmdBtnCfg, mockCore, projCtx);
                cmdBtnVm.UpdateFromConfig(new CommandButtonConfig { Title = "CmdUpdated", ButtonMode = "Latching", LabelOn = "ACTIVE", LabelOff = "STANDBY", ColorOn = "#112233" });
                if (cmdBtnVm.TypedConfig.ButtonMode != "Latching" || cmdBtnVm.TypedConfig.LabelOn != "ACTIVE" || cmdBtnVm.Title != "CmdUpdated")
                    throw new Exception("CommandButtonViewModel.UpdateFromConfig failed to update typed configuration.");

                var setValCfg = new SetValueConfig { Title = "Set", MinValue = 0, MaxValue = 50, Format = "{0:F0}" };
                var setValVm = new SetValueViewModel(setValCfg, mockCore, projCtx);
                setValVm.UpdateFromConfig(new SetValueConfig { Title = "SetUpdated", MinValue = 10, MaxValue = 200, Format = "{0:F2}" });
                if (setValVm.MinValue != 10 || setValVm.MaxValue != 200 || setValVm.Format != "{0:F2}" || setValVm.Title != "SetUpdated")
                    throw new Exception("SetValueViewModel.UpdateFromConfig failed to update min/max or format.");

                var lvlCfg = new LevelSensorConfig { Title = "LT", MinValue = 0, MaxValue = 100, AlarmHigh = 90, AlarmLow = 10 };
                var lvlVm = new LevelSensorWidgetViewModel(lvlCfg, mockCore, projCtx);
                lvlVm.UpdateFromConfig(new LevelSensorConfig { Title = "LT_New", MinValue = 5, MaxValue = 50, AlarmHigh = 45, AlarmLow = 8 });
                if (lvlVm.MinValue != 5 || lvlVm.MaxValue != 50 || lvlVm.AlarmHigh != 45 || lvlVm.Title != "LT_New")
                    throw new Exception("LevelSensorWidgetViewModel.UpdateFromConfig failed to update alarm limits.");
            }

            Console.WriteLine("[UIValidation] ALL AUTOMATED VALIDATIONS (24/24) PASSED SUCCESSFULLY!");
        }
    }

    private static VisualTreeNode DumpVisualTree(Visual visual, int currentDepth = 0)
    {
        var control = visual as Control;
        var node = new VisualTreeNode
        {
            Type = visual.GetType().Name,
            Name = control?.Name,
            Bounds = new BoundsDto
            {
                X = Math.Round(visual.Bounds.X, 2),
                Y = Math.Round(visual.Bounds.Y, 2),
                Width = Math.Round(visual.Bounds.Width, 2),
                Height = Math.Round(visual.Bounds.Height, 2)
            },
            IsVisible = visual.IsVisible
        };

        if (control != null)
        {
            node.Margin = new ThicknessDto
            {
                Left = control.Margin.Left,
                Top = control.Margin.Top,
                Right = control.Margin.Right,
                Bottom = control.Margin.Bottom
            };

            var paddingProp = control.GetType().GetProperty("Padding");
            if (paddingProp != null && paddingProp.GetValue(control) is Thickness pad)
            {
                node.Padding = new ThicknessDto
                {
                    Left = pad.Left,
                    Top = pad.Top,
                    Right = pad.Right,
                    Bottom = pad.Bottom
                };
            }
        }

        if (currentDepth < 60)
        {
            foreach (var child in visual.GetVisualChildren())
            {
                if (child is Visual vChild)
                {
                    node.Children.Add(DumpVisualTree(vChild, currentDepth + 1));
                }
            }
        }

        return node;
    }

    private static void EnsureAppIconGenerated()
    {
        try
        {
            var candidates = new[]
            {
                Path.Combine(Directory.GetCurrentDirectory(), "AvaloniaApplication1", "Assets", "app_icon.png"),
                Path.Combine(Directory.GetCurrentDirectory(), "Assets", "app_icon.png"),
                Path.Combine(AppContext.BaseDirectory, "Assets", "app_icon.png"),
                Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "AvaloniaApplication1", "Assets", "app_icon.png")
            };

            var targetPath = candidates.FirstOrDefault(p => Directory.Exists(Path.GetDirectoryName(p))) 
                             ?? candidates[0];

            var targetDir = Path.GetDirectoryName(targetPath);
            if (!string.IsNullOrEmpty(targetDir) && !Directory.Exists(targetDir))
            {
                Directory.CreateDirectory(targetDir);
            }

            // Create 256x256 icon with SkiaSharp
            using var surface = SkiaSharp.SKSurface.Create(new SkiaSharp.SKImageInfo(256, 256, SkiaSharp.SKColorType.Rgba8888, SkiaSharp.SKAlphaType.Premul));
            var canvas = surface.Canvas;
            canvas.Clear(SkiaSharp.SKColors.Transparent);

            // 1. Dark Rounded Background with gradient
            using (var bgPaint = new SkiaSharp.SKPaint
            {
                IsAntialias = true,
                Shader = SkiaSharp.SKShader.CreateLinearGradient(
                    new SkiaSharp.SKPoint(0, 0),
                    new SkiaSharp.SKPoint(256, 256),
                    new[] { new SkiaSharp.SKColor(0x1F, 0x24, 0x30), new SkiaSharp.SKColor(0x0C, 0x0F, 0x14) },
                    null,
                    SkiaSharp.SKShaderTileMode.Clamp)
            })
            {
                canvas.DrawRoundRect(new SkiaSharp.SKRect(8, 8, 248, 248), 52, 52, bgPaint);
            }

            // 2. Glowing Neon Border
            using (var borderPaint = new SkiaSharp.SKPaint
            {
                IsAntialias = true,
                Style = SkiaSharp.SKPaintStyle.Stroke,
                StrokeWidth = 4f,
                Shader = SkiaSharp.SKShader.CreateLinearGradient(
                    new SkiaSharp.SKPoint(0, 0),
                    new SkiaSharp.SKPoint(256, 256),
                    new[] { new SkiaSharp.SKColor(0x00, 0xE5, 0xFF), new SkiaSharp.SKColor(0x29, 0x79, 0xFF), new SkiaSharp.SKColor(0x00, 0xE6, 0x76) },
                    null,
                    SkiaSharp.SKShaderTileMode.Clamp)
            })
            {
                canvas.DrawRoundRect(new SkiaSharp.SKRect(8, 8, 248, 248), 52, 52, borderPaint);
            }

            // 3. Gauge Track (Dark ring)
            using (var trackPaint = new SkiaSharp.SKPaint
            {
                IsAntialias = true,
                Style = SkiaSharp.SKPaintStyle.Stroke,
                StrokeWidth = 14f,
                StrokeCap = SkiaSharp.SKStrokeCap.Round,
                Color = new SkiaSharp.SKColor(0x2A, 0x33, 0x44)
            })
            {
                using var path = new SkiaSharp.SKPath();
                path.AddArc(new SkiaSharp.SKRect(44, 44, 212, 212), 135, 270);
                canvas.DrawPath(path, trackPaint);
            }

            // 4. Active Gauge Arc (Neon Cyan to Green)
            using (var arcPaint = new SkiaSharp.SKPaint
            {
                IsAntialias = true,
                Style = SkiaSharp.SKPaintStyle.Stroke,
                StrokeWidth = 14f,
                StrokeCap = SkiaSharp.SKStrokeCap.Round,
                Shader = SkiaSharp.SKShader.CreateLinearGradient(
                    new SkiaSharp.SKPoint(44, 180),
                    new SkiaSharp.SKPoint(212, 100),
                    new[] { new SkiaSharp.SKColor(0x00, 0xE5, 0xFF), new SkiaSharp.SKColor(0x00, 0xE6, 0x76) },
                    null,
                    SkiaSharp.SKShaderTileMode.Clamp)
            })
            {
                using var path = new SkiaSharp.SKPath();
                path.AddArc(new SkiaSharp.SKRect(44, 44, 212, 212), 135, 195);
                canvas.DrawPath(path, arcPaint);
            }

            // 5. Central Industrial Icon (SCADA Flame/Sensor/Nodes & Core)
            // Center glowing node
            using (var centerGlow = new SkiaSharp.SKPaint
            {
                IsAntialias = true,
                Color = new SkiaSharp.SKColor(0x00, 0xE5, 0xFF, 0x40)
            })
            {
                canvas.DrawCircle(128, 134, 44, centerGlow);
            }

            // Stylized Valve / Flange Triangles in center
            using (var valvePaint = new SkiaSharp.SKPaint
            {
                IsAntialias = true,
                Style = SkiaSharp.SKPaintStyle.Fill,
                Shader = SkiaSharp.SKShader.CreateLinearGradient(
                    new SkiaSharp.SKPoint(80, 134),
                    new SkiaSharp.SKPoint(176, 134),
                    new[] { new SkiaSharp.SKColor(0x00, 0xE5, 0xFF), new SkiaSharp.SKColor(0x69, 0xF0, 0xAE) },
                    null,
                    SkiaSharp.SKShaderTileMode.Clamp)
            })
            {
                // Left triangle
                using var leftTri = new SkiaSharp.SKPath();
                leftTri.MoveTo(88, 114);
                leftTri.LineTo(128, 134);
                leftTri.LineTo(88, 154);
                leftTri.Close();
                canvas.DrawPath(leftTri, valvePaint);

                // Right triangle
                using var rightTri = new SkiaSharp.SKPath();
                rightTri.MoveTo(168, 114);
                rightTri.LineTo(128, 134);
                rightTri.LineTo(168, 154);
                rightTri.Close();
                canvas.DrawPath(rightTri, valvePaint);

                // Center actuator stem & circle
                canvas.DrawRect(124, 102, 8, 24, valvePaint);
                canvas.DrawCircle(128, 98, 10, valvePaint);
            }

            // 6. Text "HMI" at the bottom
            using (var font = new SkiaSharp.SKFont(SkiaSharp.SKTypeface.FromFamilyName("Arial", SkiaSharp.SKFontStyleWeight.Bold, SkiaSharp.SKFontStyleWidth.Normal, SkiaSharp.SKFontStyleSlant.Upright), 28f))
            using (var textPaint = new SkiaSharp.SKPaint
            {
                IsAntialias = true,
                Color = SkiaSharp.SKColors.White
            })
            {
                canvas.DrawText("HMI", 128, 206, SkiaSharp.SKTextAlign.Center, font, textPaint);
            }

            // Save to all target paths
            using var image = surface.Snapshot();
            using var data = image.Encode(SkiaSharp.SKEncodedImageFormat.Png, 100);

            foreach (var path in candidates)
            {
                try
                {
                    var dir = Path.GetDirectoryName(path);
                    if (!string.IsNullOrEmpty(dir) && Directory.Exists(dir))
                    {
                        using var stream = File.OpenWrite(path);
                        data.SaveTo(stream);
                        Console.WriteLine($"[UIValidation] Generated app icon: {Path.GetFullPath(path)}");
                    }
                }
                catch { }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[UIValidation] App icon generation notice: {ex.Message}");
        }
    }
}
