using System;
using Avalonia.Controls;
using Avalonia.Input;
using AvaloniaApplication1.ViewModels;

namespace AvaloniaApplication1.Views
{
    public partial class NumpadView : UserControl
    {
        public NumpadView()
        {
            InitializeComponent();
            Focusable = true;
        }

        protected override void OnAttachedToVisualTree(Avalonia.VisualTreeAttachmentEventArgs e)
        {
            base.OnAttachedToVisualTree(e);
            this.Focus();
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);

            if (DataContext is NumpadViewModel vm)
            {
                switch (e.Key)
                {
                    case Key.D0:
                    case Key.NumPad0:
                        vm.AppendCharCommand.Execute("0");
                        e.Handled = true;
                        break;
                    case Key.D1:
                    case Key.NumPad1:
                        vm.AppendCharCommand.Execute("1");
                        e.Handled = true;
                        break;
                    case Key.D2:
                    case Key.NumPad2:
                        vm.AppendCharCommand.Execute("2");
                        e.Handled = true;
                        break;
                    case Key.D3:
                    case Key.NumPad3:
                        vm.AppendCharCommand.Execute("3");
                        e.Handled = true;
                        break;
                    case Key.D4:
                    case Key.NumPad4:
                        vm.AppendCharCommand.Execute("4");
                        e.Handled = true;
                        break;
                    case Key.D5:
                    case Key.NumPad5:
                        vm.AppendCharCommand.Execute("5");
                        e.Handled = true;
                        break;
                    case Key.D6:
                    case Key.NumPad6:
                        vm.AppendCharCommand.Execute("6");
                        e.Handled = true;
                        break;
                    case Key.D7:
                    case Key.NumPad7:
                        vm.AppendCharCommand.Execute("7");
                        e.Handled = true;
                        break;
                    case Key.D8:
                    case Key.NumPad8:
                        vm.AppendCharCommand.Execute("8");
                        e.Handled = true;
                        break;
                    case Key.D9:
                    case Key.NumPad9:
                        vm.AppendCharCommand.Execute("9");
                        e.Handled = true;
                        break;
                    case Key.Decimal:
                    case Key.OemPeriod:
                    case Key.OemComma:
                        vm.AppendCharCommand.Execute(".");
                        e.Handled = true;
                        break;
                    case Key.Back:
                        vm.BackspaceCommand.Execute(null);
                        e.Handled = true;
                        break;
                    case Key.Enter:
                        vm.ConfirmCommand.Execute(null);
                        e.Handled = true;
                        break;
                    case Key.Escape:
                        vm.CancelCommand.Execute(null);
                        e.Handled = true;
                        break;
                }
            }
        }
    }
}
