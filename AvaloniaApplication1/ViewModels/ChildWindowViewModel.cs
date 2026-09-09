using System;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AvaloniaApplication1.ViewModels
{
    public partial class ChildWindowViewModel : ViewModelBase, IDisposable
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

        public void Move(double deltaX, double deltaY, double startX, double startY, double maxBoundsX, double maxBoundsY)
        {
            double targetX = startX + deltaX;
            double targetY = startY + deltaY;

            if (maxBoundsY > 0)
            {
                targetY = Math.Clamp(targetY, 0, Math.Max(0, maxBoundsY - 36));
                targetX = Math.Clamp(targetX, -Width + 100, Math.Max(100, maxBoundsX - 100));
            }
            else
            {
                targetY = Math.Max(0, targetY);
            }

            X = targetX;
            Y = targetY;
        }

        public void Resize(double deltaX, double deltaY, double startWidth, double startHeight, double maxBoundsX, double maxBoundsY)
        {
            double newWidth = Math.Max(200, startWidth + deltaX);
            double newHeight = Math.Max(120, startHeight + deltaY);

            if (maxBoundsX > 0 && maxBoundsY > 0)
            {
                newWidth = Math.Min(newWidth, Math.Max(200, maxBoundsX - X));
                newHeight = Math.Min(newHeight, Math.Max(120, maxBoundsY - Y));
            }

            Width = newWidth;
            Height = newHeight;
        }

        public void Dispose()
        {
            (Content as IDisposable)?.Dispose();
            CloseAction = null;
            GC.SuppressFinalize(this);
        }
    }
}
