using System;
using Avalonia.Controls;
using Avalonia.Input;
using AvaloniaApplication1.ViewModels;

namespace AvaloniaApplication1.Views
{
    public partial class CalculatorView : UserControl
    {
        public CalculatorView()
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

            if (DataContext is CalculatorViewModel vm)
            {
                // Проверка комбинаций с Ctrl
                if (e.KeyModifiers.HasFlag(KeyModifiers.Control))
                {
                    if (e.Key == Key.C)
                    {
                        vm.CopyToClipboardCommand.Execute(null);
                        e.Handled = true;
                        return;
                    }
                    if (e.Key == Key.V)
                    {
                        vm.PasteFromClipboardCommand.Execute(null);
                        e.Handled = true;
                        return;
                    }
                }

                switch (e.Key)
                {
                    case Key.D0:
                    case Key.NumPad0:
                        vm.InputDigitCommand.Execute("0");
                        e.Handled = true;
                        break;
                    case Key.D1:
                    case Key.NumPad1:
                        vm.InputDigitCommand.Execute("1");
                        e.Handled = true;
                        break;
                    case Key.D2:
                    case Key.NumPad2:
                        vm.InputDigitCommand.Execute("2");
                        e.Handled = true;
                        break;
                    case Key.D3:
                    case Key.NumPad3:
                        vm.InputDigitCommand.Execute("3");
                        e.Handled = true;
                        break;
                    case Key.D4:
                    case Key.NumPad4:
                        vm.InputDigitCommand.Execute("4");
                        e.Handled = true;
                        break;
                    case Key.D5:
                    case Key.NumPad5:
                        vm.InputDigitCommand.Execute("5");
                        e.Handled = true;
                        break;
                    case Key.D6:
                    case Key.NumPad6:
                        vm.InputDigitCommand.Execute("6");
                        e.Handled = true;
                        break;
                    case Key.D7:
                    case Key.NumPad7:
                        vm.InputDigitCommand.Execute("7");
                        e.Handled = true;
                        break;
                    case Key.D8:
                    case Key.NumPad8:
                        vm.InputDigitCommand.Execute("8");
                        e.Handled = true;
                        break;
                    case Key.D9:
                    case Key.NumPad9:
                        vm.InputDigitCommand.Execute("9");
                        e.Handled = true;
                        break;
                    case Key.Decimal:
                    case Key.OemPeriod:
                    case Key.OemComma:
                        vm.InputDecimalCommand.Execute(null);
                        e.Handled = true;
                        break;
                    case Key.Add:
                    case Key.OemPlus:
                        vm.SetOperationCommand.Execute("+");
                        e.Handled = true;
                        break;
                    case Key.Subtract:
                    case Key.OemMinus:
                        vm.SetOperationCommand.Execute("-");
                        e.Handled = true;
                        break;
                    case Key.Multiply:
                        vm.SetOperationCommand.Execute("×");
                        e.Handled = true;
                        break;
                    case Key.Divide:
                        vm.SetOperationCommand.Execute("÷");
                        e.Handled = true;
                        break;
                    case Key.Back:
                        vm.BackspaceCommand.Execute(null);
                        e.Handled = true;
                        break;
                    case Key.Enter:
                        vm.CalculateResultCommand.Execute(null);
                        e.Handled = true;
                        break;
                    case Key.Escape:
                        vm.CloseWindowCommand.Execute(null);
                        e.Handled = true;
                        break;
                }
            }
        }
    }
}
