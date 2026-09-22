using System;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AvaloniaApplication1.ViewModels
{
    public partial class PasswordPromptViewModel : ViewModelBase
    {
        [ObservableProperty]
        private string _title = "Вход в режим редактирования";

        [ObservableProperty]
        private string _password = string.Empty;

        [ObservableProperty]
        private string _errorMessage = string.Empty;

        [ObservableProperty]
        private bool _hasError;

        public string ExpectedPassword { get; set; } = string.Empty;

        public Action<bool>? OnResult { get; set; }

        [RelayCommand]
        private void Confirm()
        {
            if (string.Equals(Password, ExpectedPassword, StringComparison.Ordinal))
            {
                HasError = false;
                ErrorMessage = string.Empty;
                OnResult?.Invoke(true);
            }
            else
            {
                HasError = true;
                ErrorMessage = "Неверный пароль!";
                Password = string.Empty;
            }
        }

        [RelayCommand]
        private void Cancel()
        {
            OnResult?.Invoke(false);
        }

        [RelayCommand]
        private void AppendDigit(string digit)
        {
            HasError = false;
            ErrorMessage = string.Empty;
            Password += digit;
        }

        [RelayCommand]
        private void Backspace()
        {
            if (!string.IsNullOrEmpty(Password))
            {
                Password = Password.Substring(0, Password.Length - 1);
            }
        }

        [RelayCommand]
        private void Clear()
        {
            Password = string.Empty;
            HasError = false;
            ErrorMessage = string.Empty;
        }
    }
}
