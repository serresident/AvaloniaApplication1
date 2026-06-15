using Avalonia.Controls;
using Avalonia.Input;
using AvaloniaApplication1.ViewModels;
using System;

namespace AvaloniaApplication1.Views
{
    public partial class ValveControlPopupView : UserControl
    {
        public ValveControlPopupView()
        {
            InitializeComponent();
            
            // Subscribe to mouse-wheel scrolling over the slider to increment/decrement setpoint
            var slider = this.FindControl<Slider>("SetpointSlider");
            if (slider != null)
            {
                slider.PointerWheelChanged += (s, e) =>
                {
                    if (DataContext is ValveControlPopupViewModel vm && vm.IsManualMode)
                    {
                        double delta = e.Delta.Y > 0 ? 1 : -1;
                        double newVal = Math.Clamp(vm.TempSetpoint + delta, 0, 100);
                        
                        vm.TempSetpoint = newVal;
                        vm.SetpointInputText = newVal.ToString("F0");
                        
                        e.Handled = true;
                    }
                };
            }
        }
    }
}
