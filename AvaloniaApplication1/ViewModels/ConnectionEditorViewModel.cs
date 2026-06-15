using System;
using System.Linq;
using AvaloniaApplication1.Models.Config;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AvaloniaApplication1.ViewModels
{
    public partial class ConnectionEditorViewModel : ViewModelBase
    {
        private readonly bool _isEditMode;

        // --- Connection properties ---
        public string[] AvailableTypes => ConnectionTypes.All;
        public string[] AvailableByteOrders => ByteOrders.All;

        [ObservableProperty]
        private string _id = string.Empty;

        [ObservableProperty]
        private string _selectedType = ConnectionTypes.All[0];

        [ObservableProperty]
        private string _host = "127.0.0.1";

        [ObservableProperty]
        private int _port = 502;

        [ObservableProperty]
        private int _pollIntervalMs = 500;

        [ObservableProperty]
        private string _selectedByteOrder = ByteOrders.All[0];

        // --- Visibility ---
        [ObservableProperty]
        private bool _showModbusSettings = true;

        [ObservableProperty]
        private bool _isIdReadOnly;

        // --- Result ---
        public bool IsConfirmed { get; private set; }
        public Action? CloseAction { get; set; }

        public ConnectionEditorViewModel(ConnectionConfig? existingConfig)
        {
            _isEditMode = existingConfig != null;
            IsIdReadOnly = _isEditMode;

            if (existingConfig != null)
            {
                Id = existingConfig.Id;
                SelectedType = existingConfig.Type;
                Host = existingConfig.Host;
                Port = existingConfig.Port;
                PollIntervalMs = existingConfig.PollIntervalMs;
                SelectedByteOrder = existingConfig.ByteOrder;
            }

            UpdateVisibility();
        }

        partial void OnSelectedTypeChanged(string value)
        {
            UpdateVisibility();

            // Set sensible defaults for port when switching types
            if (value == "MQTT" && Port == 502)
                Port = 1883;
            else if (value != "MQTT" && Port == 1883)
                Port = 502;
        }

        private void UpdateVisibility()
        {
            ShowModbusSettings = SelectedType != "MQTT";
        }

        [RelayCommand]
        private void Save()
        {
            if (string.IsNullOrWhiteSpace(Id))
                return; // basic validation

            IsConfirmed = true;
            CloseAction?.Invoke();
        }

        [RelayCommand]
        private void Cancel()
        {
            IsConfirmed = false;
            CloseAction?.Invoke();
        }

        public ConnectionConfig ToConnectionConfig()
        {
            return new ConnectionConfig
            {
                Id = Id,
                Type = SelectedType,
                Host = Host,
                Port = Port,
                PollIntervalMs = PollIntervalMs,
                ByteOrder = SelectedByteOrder
            };
        }
    }
}
