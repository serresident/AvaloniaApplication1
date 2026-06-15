using System;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AvaloniaApplication1.ViewModels
{
    public partial class NumpadViewModel : ViewModelBase
    {
        [ObservableProperty]
        private string _inputValue = "";

        private bool _isFirstKey = true;
        
        public Action<string>? OnConfirm;
        public Action? OnCancel;

        [RelayCommand]
        private void AppendChar(string c)
        {
            if (_isFirstKey)
            {
                if (c == ".")
                {
                    InputValue = "0.";
                }
                else
                {
                    InputValue = c;
                }
                _isFirstKey = false;
            }
            else
            {
                InputValue += c;
            }
        }

        [RelayCommand]
        private void Backspace()
        {
            if (_isFirstKey)
            {
                _isFirstKey = false;
                InputValue = "";
                return;
            }

            if (InputValue.Length > 0)
            {
                InputValue = InputValue.Substring(0, InputValue.Length - 1);
            }
        }

        [RelayCommand]
        private void Clear()
        {
            InputValue = "";
            _isFirstKey = false;
        }

        [RelayCommand]
        private void Confirm()
        {
            OnConfirm?.Invoke(InputValue);
        }

        [RelayCommand]
        private void Cancel()
        {
            OnCancel?.Invoke();
        }
    }
}