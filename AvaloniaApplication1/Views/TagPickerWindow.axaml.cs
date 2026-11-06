using Avalonia.Controls;
using Avalonia.Input;
using AvaloniaApplication1.ViewModels;

namespace AvaloniaApplication1.Views
{
    public partial class TagPickerWindow : Window
    {
        public TagPickerWindow()
        {
            InitializeComponent();
        }

        private void OnRowDoubleTapped(object? sender, TappedEventArgs e)
        {
            if (DataContext is TagPickerViewModel vm && vm.SelectedTag != null)
            {
                vm.ConfirmCommand.Execute(null);
            }
        }
    }
}
