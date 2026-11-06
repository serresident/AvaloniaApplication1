using System;
using System.Globalization;
using AvaloniaApplication1.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AvaloniaApplication1.ViewModels
{
    public partial class TagForceViewModel : ViewModelBase
    {
        public TagDefinition Tag { get; }

        public string TagId => Tag.TagId;
        public string Name => Tag.Name;
        public string ConnId => Tag.ConnId;
        public string Address => Tag.Address;
        public string DataType => Tag.DataType;
        public string? Unit => Tag.Unit;

        public bool IsBoolType => string.Equals(Tag.DataType, "Bool", StringComparison.OrdinalIgnoreCase);

        [ObservableProperty]
        private string _currentValueDisplay = "---";

        [ObservableProperty]
        private string _inputValue = string.Empty;

        [ObservableProperty]
        private bool _boolValue = true;

        [ObservableProperty]
        private string? _errorMessage;

        public bool IsConfirmed { get; private set; }
        public object? ResultValue { get; private set; }
        public Action? CloseAction { get; set; }

        public TagForceViewModel(TagDefinition tag, object? currentValue)
        {
            Tag = tag;
            CurrentValueDisplay = currentValue?.ToString() ?? "---";

            if (IsBoolType)
            {
                if (currentValue is bool b) BoolValue = !b; // Suggest toggle
                else BoolValue = true;
                InputValue = BoolValue ? "1" : "0";
            }
            else
            {
                InputValue = currentValue?.ToString() ?? "0";
            }
        }

        [RelayCommand]
        private void SetPresetValue(string val)
        {
            InputValue = val;
        }

        [RelayCommand]
        private void Confirm()
        {
            ErrorMessage = null;

            if (IsBoolType)
            {
                ResultValue = BoolValue;
                IsConfirmed = true;
                CloseAction?.Invoke();
                return;
            }

            if (string.IsNullOrWhiteSpace(InputValue))
            {
                ErrorMessage = "Введите значение для фиксации.";
                return;
            }

            var text = InputValue.Trim().Replace(',', '.');

            if (string.Equals(DataType, "Float32", StringComparison.OrdinalIgnoreCase))
            {
                if (float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out float f))
                    ResultValue = f;
                else { ErrorMessage = "Некорректное вещественное число (Float32)."; return; }
            }
            else if (string.Equals(DataType, "Double", StringComparison.OrdinalIgnoreCase))
            {
                if (double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out double d))
                    ResultValue = d;
                else { ErrorMessage = "Некорректное число двойной точности (Double)."; return; }
            }
            else if (string.Equals(DataType, "Int16", StringComparison.OrdinalIgnoreCase))
            {
                if (short.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out short s))
                    ResultValue = s;
                else { ErrorMessage = "Некорректное целое число (Int16, от -32768 до 32767)."; return; }
            }
            else if (string.Equals(DataType, "UInt16", StringComparison.OrdinalIgnoreCase))
            {
                if (ushort.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out ushort us))
                    ResultValue = us;
                else { ErrorMessage = "Некорректное целое число (UInt16, от 0 до 65535)."; return; }
            }
            else if (string.Equals(DataType, "Int32", StringComparison.OrdinalIgnoreCase))
            {
                if (int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int i))
                    ResultValue = i;
                else { ErrorMessage = "Некорректное целое число (Int32)."; return; }
            }
            else if (string.Equals(DataType, "UInt32", StringComparison.OrdinalIgnoreCase))
            {
                if (uint.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out uint ui))
                    ResultValue = ui;
                else { ErrorMessage = "Некорректное целое число (UInt32)."; return; }
            }
            else
            {
                // Fallback / String
                ResultValue = text;
            }

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
