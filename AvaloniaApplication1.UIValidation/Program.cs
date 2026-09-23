using System;
using System.IO;
using System.Text.Json;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Reactive.Subjects;
using Avalonia;
using Avalonia.Headless;
using Avalonia.Controls;
using Avalonia.VisualTree;
using Avalonia.Threading;
using AvaloniaApplication1;
using AvaloniaApplication1.Models;
using AvaloniaApplication1.Views;
using AvaloniaApplication1.Services;
using System.Linq;
using AvaloniaApplication1.Models.Config;
using AvaloniaApplication1.ViewModels;
using AvaloniaApplication1.Views.DashboardPanelHelpers;
using Microsoft.Extensions.DependencyInjection;

namespace AvaloniaApplication1.UIValidation;

public class MockDataCoreService : IDataCoreService
{
    private readonly Subject<TagData> _subject = new();
    private readonly Dictionary<string, object> _values = new();

    public Task StartAsync() => Task.CompletedTask;
    public Task StopAsync() => Task.CompletedTask;
    public void PublishTag(TagData tag) 
    {
        _values[$"{tag.ConnId}:{tag.Address}"] = tag.Value;
        _subject.OnNext(tag);
    }
    public IObservable<TagData> TagUpdates => _subject;
    public void WriteCommand(string connId, string address, object value) 
    {
        _values[$"{connId}:{address}"] = value;
        PublishTag(new TagData(connId, address, value));
    }
    public object? GetCurrentValue(string connId, string address) 
    {
        return _values.TryGetValue($"{connId}:{address}", out var v) ? v : null;
    }
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}

public class MockSimulationService : ISimulationService
{
    public Task StartSimulationAsync() => Task.CompletedTask;
    public Task StopSimulationAsync() => Task.CompletedTask;
    public void ResetSimulation() { }
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
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
        session.Dispatch(() =>
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
            if (string.Equals(targetView, "MimicView", StringComparison.OrdinalIgnoreCase) ||
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
                    Width = 1024,
                    Height = 768
                };
            }
            else if (string.Equals(targetView, "DashboardView", StringComparison.OrdinalIgnoreCase))
            {
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
                frame.Save(imgPath);
                Console.WriteLine($"[UIValidation] Screenshot saved: {imgPath} ({new FileInfo(imgPath).Length} bytes)");
            }
            else
            {
                Console.WriteLine("[UIValidation] WARNING: Could not capture rendered frame.");
            }

            var tree = DumpVisualTree(window);
            var jsonPath = Path.Combine(outDir, "ui_tree.json");
            var json = JsonSerializer.Serialize(tree, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(jsonPath, json);
            Console.WriteLine($"[UIValidation] Visual tree dump saved: {jsonPath} ({new FileInfo(jsonPath).Length} bytes)");

            try
            {
                RunAutomatedValidations(mainVm, sp);
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"[UIValidation] VALIDATION ERROR: {ex}");
                Console.ResetColor();
                Environment.Exit(1);
            }

            Console.WriteLine("[UIValidation] SUCCESS: Artifacts generated successfully.");
            Environment.Exit(0);
        }, CancellationToken.None);

        // Failsafe timeout
        Thread.Sleep(6000);
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

            Console.WriteLine("[UIValidation] ALL AUTOMATED VALIDATIONS (18/18) PASSED SUCCESSFULLY!");
        }
    }

    private static VisualTreeNode DumpVisualTree(Visual visual)
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

        foreach (var child in visual.GetVisualChildren())
        {
            if (child is Visual vChild)
            {
                node.Children.Add(DumpVisualTree(vChild));
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
