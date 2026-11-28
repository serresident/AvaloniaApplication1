using System;
using System.ComponentModel;
using System.Globalization;
using AvaloniaApplication1.ViewModels.Core;
using AvaloniaApplication1.ViewModels.Widgets;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AvaloniaApplication1.ViewModels.Popups
{
    public partial class ReactorFaceplateViewModel : ViewModelBase, IDisposable
    {
        [ObservableProperty]
        private ReactorWidgetViewModel _reactorViewModel;

        [ObservableProperty]
        private double _tempSetpointInput;

        [ObservableProperty]
        private string _setpointInputText = "20";

        [ObservableProperty]
        private bool _isKeypadVisible;

        private bool _isFirstKeypadInput;
        private readonly Action _closeAction;

        public bool HasModeSource => ReactorViewModel.HasModeTag;
        public bool HasTempSetpointSource => ReactorViewModel.HasTempSetpointSource;
        public bool IsManualMode => ReactorViewModel.IsManualMode;
        public bool CanOperateManual => !ReactorViewModel.HasModeTag || ReactorViewModel.IsManualMode;

        public ReactorFaceplateViewModel(ReactorWidgetViewModel reactorViewModel, Action closeAction)
        {
            _reactorViewModel = reactorViewModel;
            _closeAction = closeAction;

            _tempSetpointInput = Math.Round(reactorViewModel.TempSetpoint, 1);
            _setpointInputText = _tempSetpointInput.ToString("F1", CultureInfo.InvariantCulture);

            _reactorViewModel.PropertyChanged += OnReactorPropertyChanged;
        }

        private void OnReactorPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(ReactorWidgetViewModel.IsManualMode))
            {
                OnPropertyChanged(nameof(IsManualMode));
                OnPropertyChanged(nameof(CanOperateManual));
            }
            else if (e.PropertyName == nameof(ReactorWidgetViewModel.TempSetpoint) && !IsKeypadVisible)
            {
                TempSetpointInput = Math.Round(ReactorViewModel.TempSetpoint, 1);
                SetpointInputText = TempSetpointInput.ToString("F1", CultureInfo.InvariantCulture);
            }
        }

        [RelayCommand]
        private void SetManualMode() => ReactorViewModel.SetMode(true);

        [RelayCommand]
        private void SetAutoMode() => ReactorViewModel.SetMode(false);

        [RelayCommand]
        private void StartAgitator() => ReactorViewModel.SetAgitatorCommand(true);

        [RelayCommand]
        private void StopAgitator() => ReactorViewModel.SetAgitatorCommand(false);

        [RelayCommand]
        private void ApplyTempSetpoint()
        {
            if (!CanOperateManual) return;
            ReactorViewModel.SetTemperatureSetpoint(TempSetpointInput);
        }

        [RelayCommand]
        private void ToggleKeypad()
        {
            if (!CanOperateManual) return;
            IsKeypadVisible = !IsKeypadVisible;
            if (IsKeypadVisible)
            {
                SetpointInputText = TempSetpointInput.ToString("F1", CultureInfo.InvariantCulture);
                _isFirstKeypadInput = true;
            }
        }

        [RelayCommand]
        private void KeypadDigit(string digit)
        {
            if (_isFirstKeypadInput)
            {
                SetpointInputText = digit == "." ? "0." : digit;
                _isFirstKeypadInput = false;
                return;
            }

            if (digit == "." && SetpointInputText.Contains('.')) return;
            if (SetpointInputText == "0" && digit != ".")
                SetpointInputText = digit;
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
            _isFirstKeypadInput = false;
        }

        [RelayCommand]
        private void KeypadClear()
        {
            SetpointInputText = "0";
            _isFirstKeypadInput = false;
        }

        [RelayCommand]
        private void KeypadConfirm()
        {
            if (double.TryParse(SetpointInputText.Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out double val))
            {
                TempSetpointInput = Math.Clamp(val, ReactorViewModel.MinTemp, ReactorViewModel.MaxTemp);
                SetpointInputText = TempSetpointInput.ToString("F1", CultureInfo.InvariantCulture);
                ReactorViewModel.SetTemperatureSetpoint(TempSetpointInput);
            }
            IsKeypadVisible = false;
        }

        [RelayCommand]
        private void Close() => _closeAction?.Invoke();

        public void Dispose()
        {
            ReactorViewModel.PropertyChanged -= OnReactorPropertyChanged;
        }
    }
}
