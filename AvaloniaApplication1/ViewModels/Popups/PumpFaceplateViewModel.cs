using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using AvaloniaApplication1.ViewModels.Core;
using AvaloniaApplication1.ViewModels.Widgets;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AvaloniaApplication1.ViewModels.Popups
{
    public partial class PumpFaceplateViewModel : ViewModelBase, IDisposable
    {
        [ObservableProperty]
        private PumpWidgetViewModel _pumpViewModel;

        [ObservableProperty]
        private double _tempSetpoint;

        [ObservableProperty]
        private string _setpointInputText = "0.0";

        [ObservableProperty]
        private bool _isKeypadVisible;

        private readonly Action _closeAction;

        public bool HasModeSource => PumpViewModel.HasModeSource;
        public bool IsVfdMode => PumpViewModel.IsVfdMode;
        public bool IsManualMode => PumpViewModel.IsManualMode;
        public bool CanOperateManual => !PumpViewModel.HasModeSource || PumpViewModel.IsManualMode;
        public double MinFrequency => PumpViewModel.MinFrequency;
        public double MaxFrequency => PumpViewModel.MaxFrequency;
        public string FrequencyUnit => PumpViewModel.FrequencyUnit;
        public string StatusWordHex => $"0x{PumpViewModel.StatusWord:X4}";
        public ObservableCollection<DecodedStatusBit> ActiveStatusBits => PumpViewModel.ActiveStatusBits;

        public PumpFaceplateViewModel(PumpWidgetViewModel pumpViewModel, Action closeAction)
        {
            PumpViewModel = pumpViewModel;
            _closeAction = closeAction;

            TempSetpoint = Math.Clamp(PumpViewModel.SetpointFrequency, MinFrequency, MaxFrequency);
            SetpointInputText = TempSetpoint.ToString("F1", CultureInfo.InvariantCulture);

            PumpViewModel.PropertyChanged += OnPumpPropertyChanged;
        }

        private void OnPumpPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(PumpWidgetViewModel.IsManualMode))
            {
                OnPropertyChanged(nameof(IsManualMode));
                OnPropertyChanged(nameof(CanOperateManual));
            }
            else if (e.PropertyName == nameof(PumpWidgetViewModel.StatusWord))
            {
                OnPropertyChanged(nameof(StatusWordHex));
            }
            else if (e.PropertyName == nameof(PumpWidgetViewModel.SetpointFrequency) && !IsManualMode)
            {
                TempSetpoint = Math.Clamp(PumpViewModel.SetpointFrequency, MinFrequency, MaxFrequency);
                SetpointInputText = TempSetpoint.ToString("F1", CultureInfo.InvariantCulture);
            }
        }

        [RelayCommand]
        private void SetManualMode()
        {
            PumpViewModel.SetMode(true);
        }

        [RelayCommand]
        private void SetAutoMode()
        {
            PumpViewModel.SetMode(false);
        }

        [RelayCommand]
        private void StartPump()
        {
            PumpViewModel.SendStartCommand();
        }

        [RelayCommand]
        private void StopPump()
        {
            PumpViewModel.SendStopCommand();
        }

        [RelayCommand]
        private void ApplySetpoint()
        {
            if (double.TryParse(SetpointInputText.Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out var val))
            {
                var clamped = Math.Clamp(val, MinFrequency, MaxFrequency);
                TempSetpoint = clamped;
                SetpointInputText = clamped.ToString("F1", CultureInfo.InvariantCulture);
                PumpViewModel.WriteSetpoint(clamped);
            }
        }

        [RelayCommand]
        private void ToggleKeypad()
        {
            IsKeypadVisible = !IsKeypadVisible;
        }

        [RelayCommand]
        private void KeypadDigit(string digit)
        {
            if (digit == "." && SetpointInputText.Contains('.')) return;
            if (SetpointInputText == "0" || SetpointInputText == "0.0")
                SetpointInputText = digit == "." ? "0." : digit;
            else
                SetpointInputText += digit;
        }

        [RelayCommand]
        private void KeypadBackspace()
        {
            if (SetpointInputText.Length > 1)
                SetpointInputText = SetpointInputText[..^1];
            else
                SetpointInputText = "0";
        }

        [RelayCommand]
        private void KeypadClear()
        {
            SetpointInputText = "0";
        }

        [RelayCommand]
        private void KeypadConfirm()
        {
            ApplySetpoint();
            IsKeypadVisible = false;
        }

        [RelayCommand]
        private void Close()
        {
            _closeAction?.Invoke();
        }

        public void Dispose()
        {
            PumpViewModel.PropertyChanged -= OnPumpPropertyChanged;
        }
    }
}
