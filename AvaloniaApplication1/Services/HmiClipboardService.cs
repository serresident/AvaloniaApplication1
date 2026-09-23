using System;

namespace AvaloniaApplication1.Services
{
    /// <summary>
    /// Реализация изолированного внутреннего буфера HMI.
    /// </summary>
    public class HmiClipboardService : IHmiClipboardService
    {
        private string? _currentValue;

        public string? CurrentValue
        {
            get => _currentValue;
            set
            {
                if (_currentValue != value)
                {
                    _currentValue = value;
                    ValueChanged?.Invoke(_currentValue);
                }
            }
        }

        public bool HasValue => !string.IsNullOrEmpty(_currentValue);

        public event Action<string?>? ValueChanged;

        public void Clear()
        {
            CurrentValue = null;
        }
    }
}
