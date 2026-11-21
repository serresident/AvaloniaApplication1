using System;
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

        protected override void OnOpened(EventArgs e)
        {
            base.OnOpened(e);
            var searchBox = this.FindControl<TextBox>("SearchBox");
            searchBox?.Focus();
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);

            if (e.Key == Key.Escape)
            {
                if (DataContext is TagPickerViewModel vm)
                {
                    vm.CancelCommand.Execute(null);
                    e.Handled = true;
                }
            }
            else if (e.Key == Key.Enter)
            {
                if (DataContext is TagPickerViewModel vm && vm.SelectedTag != null)
                {
                    vm.ConfirmCommand.Execute(null);
                    e.Handled = true;
                }
            }
            else if (e.Key == Key.Down && e.Source is TextBox)
            {
                var listBox = this.FindControl<ListBox>("TagsListBox");
                if (listBox != null)
                {
                    listBox.Focus();
                    if (listBox.SelectedIndex < 0 && listBox.ItemCount > 0)
                    {
                        listBox.SelectedIndex = 0;
                    }
                    e.Handled = true;
                }
            }
            else if (e.Key == Key.Up && e.Source is not TextBox)
            {
                var listBox = this.FindControl<ListBox>("TagsListBox");
                if (listBox != null && listBox.SelectedIndex <= 0)
                {
                    var searchBox = this.FindControl<TextBox>("SearchBox");
                    searchBox?.Focus();
                    e.Handled = true;
                }
            }
        }

        private void OnHeaderClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            if (sender is Button btn && btn.CommandParameter is string col && DataContext is TagPickerViewModel vm)
            {
                vm.SortCommand.Execute(col);
            }
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
