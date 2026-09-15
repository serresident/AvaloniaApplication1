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
            var services = new ServiceCollection();
            services.AddSingleton<IProjectContextService, ProjectContextService>();
            services.AddSingleton<IConfigurationService, ConfigurationService>();
            services.AddSingleton<IDataCoreService, MockDataCoreService>();
            services.AddSingleton<ISimulationService, MockSimulationService>();
            services.AddSingleton<IAlarmNotificationService, AlarmNotificationService>();
            services.AddSingleton<IWidgetFactory, WidgetFactory>();
            services.AddSingleton<IDialogService, DialogService>();
            services.AddSingleton<MainViewModel>();
            using var sp = services.BuildServiceProvider();

            var mainVm = sp.GetRequiredService<MainViewModel>();

            for (int i = 0; i < 20 && (mainVm.Dashboard == null || mainVm.Dashboard.Widgets.Count == 0); i++)
            {
                Dispatcher.UIThread.RunJobs();
                Thread.Sleep(50);
            }

            Window window;
            if (string.Equals(targetView, "MimicView", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(targetView, "Mimic", StringComparison.OrdinalIgnoreCase))
            {
                mainVm.ShowMimicCommand.Execute(null);
                Dispatcher.UIThread.RunJobs();
                window = new MainWindow
                {
                    DataContext = mainVm,
                    Width = 1400,
                    Height = 900
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

            RunAutomatedValidations(mainVm, sp);

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
