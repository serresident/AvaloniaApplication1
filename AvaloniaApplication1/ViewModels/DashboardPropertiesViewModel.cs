using System;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AvaloniaApplication1.ViewModels
{
    public partial class DashboardPropertiesViewModel : ViewModelBase
    {
        [ObservableProperty]
        private double _cellSize;

        [ObservableProperty]
        private double _zoomPercent;

        public double ZoomScale => Math.Round(ZoomPercent / 100.0, 2);

        public bool IsConfirmed { get; private set; }
        public Action? CloseAction { get; set; }

        public DashboardPropertiesViewModel(double currentCellSize, double currentZoomScale)
        {
            CellSize = currentCellSize > 0 ? currentCellSize : 40.0;
            ZoomPercent = Math.Round((currentZoomScale > 0 ? currentZoomScale : 1.0) * 100.0, 0);
        }

        [RelayCommand]
        private void SetCellSizePreset(object? param)
        {
            if (param is string s && double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out double val))
            {
                CellSize = val;
            }
            else if (param is double d)
            {
                CellSize = d;
            }
            else if (param is int i)
            {
                CellSize = i;
            }
        }

        [RelayCommand]
        private void SetZoomPreset(object? param)
        {
            if (param is string s && double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out double val))
            {
                ZoomPercent = val;
            }
            else if (param is double d)
            {
                ZoomPercent = d;
            }
            else if (param is int i)
            {
                ZoomPercent = i;
            }
        }

        [RelayCommand]
        private void ResetDefaults()
        {
            CellSize = 40.0;
            ZoomPercent = 100.0;
        }

        [RelayCommand]
        private void Save()
        {
            IsConfirmed = true;
            CloseAction?.Invoke();
        }

        [RelayCommand]
        private void Cancel()
        {
            IsConfirmed = false;
            CloseAction?.Invoke();
        }
    }
}
