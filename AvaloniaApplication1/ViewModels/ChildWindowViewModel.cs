using System;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AvaloniaApplication1.ViewModels
{
    public partial class ChildWindowViewModel : ViewModelBase
    {
        [ObservableProperty]
        private string _title = string.Empty;

        [ObservableProperty]
        private double _x = 50;

        [ObservableProperty]
        private double _y = 50;

        [ObservableProperty]
        private double _width = 500;

        [ObservableProperty]
        private double _height = 380;

        [ObservableProperty]
        private object _content;

        [ObservableProperty]
        private int _zIndex = 1;

        [ObservableProperty]
        private bool _isMinimized;

        private double _preMinimizeHeight = 380;

        public Action? CloseAction { get; set; }

        public ChildWindowViewModel(string title, object content)
        {
            Title = title;
            Content = content;
        }

        [RelayCommand]
        private void Close()
        {
            CloseAction?.Invoke();
        }

        [RelayCommand]
        private void ToggleMinimize()
        {
            IsMinimized = !IsMinimized;
            if (IsMinimized)
            {
                _preMinimizeHeight = Height;
                Height = 36; // Collapsed height to title bar only
            }
            else
            {
                Height = _preMinimizeHeight;
            }
        }
    }
}
