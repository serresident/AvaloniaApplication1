using System;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using AvaloniaApplication1.ViewModels;

namespace AvaloniaApplication1.Views
{
    public partial class TagManagerWindow : Window
    {
        public TagManagerWindow()
        {
            InitializeComponent();
        }

        protected override void OnOpened(EventArgs e)
        {
            base.OnOpened(e);
            if (DataContext is TagManagerViewModel vm)
            {
                vm.CloseAction ??= Close;
            }
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            if (e.Key == Key.Escape)
            {
                if (DataContext is TagManagerViewModel vm)
                {
                    vm.CloseCommand.Execute(null);
                }
                else
                {
                    Close();
                }
                e.Handled = true;
            }
        }

        private void OnHeaderClick(object? sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.CommandParameter is string col && DataContext is TagManagerViewModel vm)
            {
                vm.SortCommand.Execute(col);
            }
        }

        private void OnCloseClick(object? sender, RoutedEventArgs e)
        {
            if (DataContext is TagManagerViewModel vm)
            {
                vm.CloseCommand.Execute(null);
            }
            else
            {
                Close();
            }
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
