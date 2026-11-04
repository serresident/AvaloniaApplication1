using System;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AvaloniaApplication1.ViewModels
{
    public partial class ConfirmPromptViewModel : ViewModelBase
    {
        [ObservableProperty]
        private string _title = "Подтверждение действия";

        [ObservableProperty]
        private string _message = string.Empty;

        public Action<bool>? OnResult { get; set; }

        [RelayCommand]
        private void Confirm()
        {
            OnResult?.Invoke(true);
        }

        [RelayCommand]
        private void Cancel()
        {
            OnResult?.Invoke(false);
        }
    }
}
