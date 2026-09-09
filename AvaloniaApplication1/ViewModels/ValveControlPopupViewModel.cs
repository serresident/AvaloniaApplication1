using System;
using System.ComponentModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AvaloniaApplication1.ViewModels
{
    public partial class ValveControlPopupViewModel : ViewModelBase
    {
        [ObservableProperty]
        private ValveWidgetViewModel _valveViewModel;

        [ObservableProperty]
        private double _tempSetpoint;

        [ObservableProperty]
        private string _setpointInputText = "0";

        [ObservableProperty]
        private bool _isManualMode = true;

        [ObservableProperty]
        private bool _isKeypadVisible;

        public bool HasModeTag => ValveViewModel.TypedConfig.ModeSource != null && 
                                  !string.IsNullOrEmpty(ValveViewModel.TypedConfig.ModeSource.Address);

        public bool IsRegulating => string.Equals(ValveViewModel.ValveType, "Regulating", StringComparison.OrdinalIgnoreCase);

        public string SliderTooltipText => "Переместите ползунок или используйте колесо мыши для изменения уставки от 0 до 100. Шаг = 1. Для точного ввода используйте цифровую клавиатуру.";

        public string OpenButtonText => IsRegulating ? "ОТКРЫТЬ (100%)" : "ОТКРЫТЬ";
        public string CloseButtonText => IsRegulating ? "ЗАКРЫТЬ (0%)" : "ЗАКРЫТЬ";

        public string AlarmStateText
        {
            get
            {
                if (ValveViewModel.AlarmDisabled) return "ОТКЛЮЧЕНА";
                if (ValveViewModel.IsAlarmActive) return "АКТИВНА";
                return "НЕТ";
            }
        }

        private readonly Action _closeAction;
        private double _preKeypadSetpoint;
        private string _preKeypadText = "0";
        private bool _isFirstKeypadInput;

        public ValveControlPopupViewModel(ValveWidgetViewModel valveViewModel, Action closeAction)
        {
            ValveViewModel = valveViewModel;
            _closeAction = closeAction;

            // Initialize TempSetpoint to backing valve setpoint
            TempSetpoint = Math.Round(ValveViewModel.Setpoint);
            SetpointInputText = TempSetpoint.ToString("F0");

            // Load initial mode from DataService
            if (HasModeTag)
            {
                var modeVal = ValveViewModel.DataService.GetCurrentValue(
                    ValveViewModel.TypedConfig.ModeSource!.ConnId, 
                    ValveViewModel.TypedConfig.ModeSource.Address);
                
                // true = Auto, false = Manual
                if (modeVal is bool b) IsManualMode = !b;
                else if (modeVal is int i) IsManualMode = (i == 0);
                else if (double.TryParse(modeVal?.ToString(), NumberStyles.Float, CultureInfo.InvariantCulture, out double num)) IsManualMode = (num == 0);
                else IsManualMode = true;
            }
            else
            {
                IsManualMode = true;
            }

            ValveViewModel.PropertyChanged += OnValveViewModelPropertyChanged;
        }

        private void OnValveViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(ValveWidgetViewModel.Setpoint))
            {
                // In Auto mode, track setpoint in real time
                if (!IsManualMode)
                {
                    TempSetpoint = Math.Round(ValveViewModel.Setpoint);
                    SetpointInputText = TempSetpoint.ToString("F0");
                }
            }
            else if (e.PropertyName == nameof(ValveWidgetViewModel.IsAlarmActive) || 
                     e.PropertyName == nameof(ValveWidgetViewModel.AlarmDisabled))
            {
                OnPropertyChanged(nameof(AlarmStateText));
            }
        }

        partial void OnIsManualModeChanged(bool value)
        {
            if (HasModeTag)
            {
                // auto = true (1), manual = false (0)
                object writeVal = !value;
                var modeSrc = ValveViewModel.TypedConfig.ModeSource!;
                if (modeSrc.DataType == "Float32" || modeSrc.DataType == "Float")
                    writeVal = !value ? 1.0f : 0.0f;
                else if (modeSrc.DataType == "Int16" || modeSrc.DataType == "Int32")
                    writeVal = !value ? 1 : 0;

                ValveViewModel.DataService.WriteCommand(modeSrc.ConnId, modeSrc.Address, writeVal);
            }

            // Auto -> Manual: inherit current controller output setpoint (do not reset)
            if (value)
            {
                TempSetpoint = Math.Round(ValveViewModel.Setpoint);
                SetpointInputText = TempSetpoint.ToString("F0");
            }
        }

        [RelayCommand]
        private void WriteSetpoint()
        {
            if (IsRegulating)
            {
                if (!IsManualMode) return; // Locked in Auto

                // Write TempSetpoint to backing tag
                object valToWrite = TempSetpoint;
                var source = ValveViewModel.TypedConfig.Source;
                if (source.DataType == "Float32" || source.DataType == "Float")
                    valToWrite = (float)TempSetpoint;
                else if (source.DataType == "Int16")
                    valToWrite = (short)TempSetpoint;
                else if (source.DataType == "Int32")
                    valToWrite = (int)TempSetpoint;

                ValveViewModel.DataService.WriteCommand(source.ConnId, source.Address, valToWrite);
            }
            else
            {
                // Cutoff write command is handled by OPEN/CLOSE buttons directly
            }
        }

        [RelayCommand]
        private void WriteCutoffOpen()
        {
            WriteCutoff(true);
        }

        [RelayCommand]
        private void WriteCutoffClose()
        {
            WriteCutoff(false);
        }

        private void WriteCutoff(bool open)
        {
            object valToWrite = open;
            var source = ValveViewModel.TypedConfig.Source;
            if (source.DataType == "Float32" || source.DataType == "Float")
                valToWrite = open ? 100.0f : 0.0f;
            else if (source.DataType == "Int16" || source.DataType == "Int32")
                valToWrite = open ? 100 : 0;

            ValveViewModel.DataService.WriteCommand(source.ConnId, source.Address, valToWrite);
            
            // Set temporary local setpoint representation
            TempSetpoint = open ? 100 : 0;
            SetpointInputText = TempSetpoint.ToString("F0");
        }

        [RelayCommand]
        private void ToggleKeypad()
        {
            if (!IsManualMode) return; // Locked in Auto

            IsKeypadVisible = !IsKeypadVisible;
            if (IsKeypadVisible)
            {
                // Store backup values for cancellation
                _preKeypadSetpoint = TempSetpoint;
                _preKeypadText = SetpointInputText;
                _isFirstKeypadInput = true;
            }
        }

        [RelayCommand]
        private void NumpadAppend(string c)
        {
            if (!IsManualMode) return;

            string currentText;
            if (_isFirstKeypadInput)
            {
                _isFirstKeypadInput = false;
                currentText = (c == "00") ? "0" : c;
            }
            else
            {
                currentText = SetpointInputText;
                if (currentText == "0" && c != "00")
                {
                    currentText = c;
                }
                else if (c == "00")
                {
                    if (currentText != "0" && currentText != "")
                    {
                        currentText += "00";
                    }
                }
                else
                {
                    currentText += c;
                }
            }

            if (double.TryParse(currentText, NumberStyles.Float, CultureInfo.InvariantCulture, out double val))
            {
                if (val <= 100)
                {
                    SetpointInputText = currentText;
                    TempSetpoint = val;
                }
            }
        }

        [RelayCommand]
        private void NumpadBackspace()
        {
            if (!IsManualMode) return;
            _isFirstKeypadInput = false;

            if (SetpointInputText.Length > 1)
            {
                SetpointInputText = SetpointInputText.Substring(0, SetpointInputText.Length - 1);
            }
            else
            {
                SetpointInputText = "0";
            }

            if (double.TryParse(SetpointInputText, NumberStyles.Float, CultureInfo.InvariantCulture, out double val))
            {
                TempSetpoint = val;
            }
        }

        [RelayCommand]
        private void NumpadClear()
        {
            if (!IsManualMode) return;
            _isFirstKeypadInput = false;

            SetpointInputText = "0";
            TempSetpoint = 0;
        }

        [RelayCommand]
        private void NumpadConfirm()
        {
            if (!IsManualMode) return;
            WriteSetpoint();
            IsKeypadVisible = false;
        }

        [RelayCommand]
        private void NumpadCancel()
        {
            // Discard typing, restore backup setpoint
            TempSetpoint = _preKeypadSetpoint;
            SetpointInputText = _preKeypadText;
            IsKeypadVisible = false;
        }

        [RelayCommand]
        private void DisableAlarm()
        {
            ValveViewModel.AlarmDisabled = true;
            OnPropertyChanged(nameof(AlarmStateText));
        }

        [RelayCommand]
        private void ResetAlarm()
        {
            ValveViewModel.AlarmDisabled = false;
            OnPropertyChanged(nameof(AlarmStateText));
        }

        [RelayCommand]
        private void ToggleManual() => IsManualMode = true;

        [RelayCommand]
        private void ToggleAuto() => IsManualMode = false;

        [RelayCommand]
        private void Close()
        {
            _closeAction.Invoke();
        }

        public void Dispose()
        {
            ValveViewModel.PropertyChanged -= OnValveViewModelPropertyChanged;
        }
    }
}
