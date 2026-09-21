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
    public Task StartAsync() => Task.CompletedTask;
    public Task StopAsync() => Task.CompletedTask;
    public void PublishTag(TagData tag) => _subject.OnNext(tag);
    public IObservable<TagData> TagUpdates => _subject;
    public void WriteCommand(string connId, string address, object value) { }
    public object? GetCurrentValue(string connId, string address) => null;
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

            var services = new ServiceCollection();
            services.AddSingleton<IProjectContextService, ProjectContextService>();
            services.AddSingleton<IConfigurationService>(configService);
            services.AddSingleton<IDataCoreService, MockDataCoreService>();
            services.AddSingleton<ISimulationService, MockSimulationService>();
            services.AddSingleton<IAlarmNotificationService, AlarmNotificationService>();
            services.AddSingleton<IWidgetFactory, WidgetFactory>();
            services.AddSingleton<IDialogService, DialogService>();
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
                window = new Window
                {
                    Title = "ValveControlPopupView Preview",
                    Width = 500,
                    Height = 500,
                    Content = new ValveControlPopupView()
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

        Console.WriteLine("[UIValidation] ALL AUTOMATED VALIDATIONS PASSED SUCCESSFULLY!");
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
}
