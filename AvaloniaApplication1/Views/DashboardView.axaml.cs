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
    }
}