using System;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;

namespace AvaloniaApplication1.Views
{
    public partial class DashboardView : UserControl
    {
        public DashboardView()
        {
            InitializeComponent();
            AddHandler(PointerWheelChangedEvent, OnPointerWheelChanged, RoutingStrategies.Tunnel);
        }

        private void OnPointerWheelChanged(object? sender, PointerWheelEventArgs e)
        {
            if (e.KeyModifiers.HasFlag(KeyModifiers.Control) && e.KeyModifiers.HasFlag(KeyModifiers.Shift))
            {
                if (DataContext is ViewModels.DashboardViewModel viewModel && viewModel.ProjectContext.IsDesignMode)
                {
                    double delta = e.Delta.Y > 0 ? 0.1 : -0.1;
                    viewModel.ZoomScale = Math.Clamp(viewModel.ZoomScale + delta, 0.5, 3.0);
                    e.Handled = true;
                }
            }
        }

        private void ContextMenu_Opened(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            if (sender is ContextMenu menu)
            {
                var target = menu.PlacementTarget;
                var tag = target?.Tag;
                var parent = menu.Parent;
                var parentTag = (parent is Control c) ? c.Tag : null;
                var dc = menu.DataContext;
                
                var logLines = new System.Collections.Generic.List<string>();
                logLines.Add("--- ContextMenu Opened Diagnostic ---");
                logLines.Add($"Timestamp: {DateTime.Now}");
                logLines.Add($"Menu: {menu.GetType().FullName}");
                logLines.Add($"Menu.DataContext: {(dc == null ? "null" : dc.GetType().FullName)}");
                logLines.Add($"PlacementTarget: {(target == null ? "null" : target.GetType().FullName)}");
                logLines.Add($"PlacementTarget.Tag: {(tag == null ? "null" : tag.GetType().FullName)}");
                logLines.Add($"Parent: {(parent == null ? "null" : parent.GetType().FullName)}");
                logLines.Add($"Parent.Tag: {(parentTag == null ? "null" : parentTag.GetType().FullName)}");
                
                if (menu.ItemsSource != null)
                {
                    logLines.Add($"ItemsSource: {menu.ItemsSource.GetType().FullName}");
                }
                else
                {
                    logLines.Add("ItemsSource is null");
                }
                
                var items = menu.Items;
                if (items != null)
                {
                    logLines.Add($"Items count: {items.Count}");
                    for (int i = 0; i < items.Count; i++)
                    {
                        var item = items[i];
                        if (item is MenuItem mi)
                        {
                            logLines.Add($"  Item {i}: Header='{mi.Header}', Command={(mi.Command == null ? "null" : mi.Command.GetType().FullName)}, CommandParameter={(mi.CommandParameter == null ? "null" : mi.CommandParameter.GetType().FullName)}, IsEnabled={mi.IsEnabled}");
                        }
                        else
                        {
                            logLines.Add($"  Item {i}: {item.GetType().FullName}");
                        }
                    }
                }
                
                System.IO.File.WriteAllLines(@"c:\Users\ess2\source\repos\AvaloniaApplication1\debug_log.txt", logLines);
            }
        }
    }
}