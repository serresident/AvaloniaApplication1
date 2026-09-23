using System;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using AvaloniaApplication1.Services;

namespace AvaloniaApplication1.ViewModels
{
    public partial class NumpadViewModel : ViewModelBase, IDisposable
    {
        private readonly IHmiClipboardService? _clipboard;

        [ObservableProperty]
        private string _inputValue = "";

        [ObservableProperty]
        private bool _hasClipboardValue;

        [ObservableProperty]
        private string _clipboardPreview = "";

        private bool _isFirstKey = true;
        
        public Action<string>? OnConfirm;
        public Action? OnCancel;

        public NumpadViewModel(IHmiClipboardService? clipboard = null)
        {
            _clipboard = clipboard;
            UpdateClipboardState();

            if (_clipboard != null)
            {
                _clipboard.ValueChanged += OnClipboardValueChanged;
            }
        }

        private void OnClipboardValueChanged(string? _)
        {
            Avalonia.Threading.Dispatcher.UIThread.Post(UpdateClipboardState);
        }

        private void UpdateClipboardState()
        {
            if (_clipboard != null && _clipboard.HasValue)
            {
                HasClipboardValue = true;
                ClipboardPreview = _clipboard.CurrentValue ?? "";
            }
            else
            {
                HasClipboardValue = false;
                ClipboardPreview = "";
            }
        }

        [RelayCommand]
        public void PasteFromClipboard()
        {
            if (_clipboard != null && _clipboard.HasValue && !string.IsNullOrWhiteSpace(_clipboard.CurrentValue))
            {
                InputValue = _clipboard.CurrentValue.Trim();
                _isFirstKey = false;
            }
        }

        public void Dispose()
        {
            if (_clipboard != null)
            {
                _clipboard.ValueChanged -= OnClipboardValueChanged;
            }
        }

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