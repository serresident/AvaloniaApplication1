using AvaloniaApplication1.ViewModels.Windows.Tags;
using AvaloniaApplication1.ViewModels.Core;
using System;
using Avalonia.Controls;
using Avalonia.Input;
using AvaloniaApplication1.ViewModels;

namespace AvaloniaApplication1.Views.Windows.Tags
{
    public partial class TagManagerWindow : Window
    {
        public TagManagerWindow()
        {
            InitializeComponent();
        }

        private void OnCloseClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            Close();
        }

        private void OnRowDoubleTapped(object? sender, TappedEventArgs e)
        {
            if (DataContext is TagManagerViewModel vm && vm.SelectedTag != null)
            {
                vm.EditTagCommand.Execute(null);
            }
        }
    }
}
