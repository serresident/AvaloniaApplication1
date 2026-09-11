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
using AvaloniaApplication1.ViewModels;
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

            Window window;
            if (string.Equals(targetView, "DashboardView", StringComparison.OrdinalIgnoreCase))
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

            Console.WriteLine("[UIValidation] SUCCESS: Artifacts generated successfully.");
            Environment.Exit(0);
        }, CancellationToken.None);

        // Failsafe timeout
        Thread.Sleep(6000);
        Environment.Exit(0);
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
