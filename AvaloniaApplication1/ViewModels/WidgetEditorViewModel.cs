using System;
using System.Collections.Generic;
using System.Linq;
using AvaloniaApplication1.Models.Config;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AvaloniaApplication1.ViewModels
{
    public partial class WidgetEditorViewModel : ViewModelBase
    {
        // --- Widget Type ---
        public string[] AvailableWidgetTypes => WidgetTypes.All;

        [ObservableProperty]
        private string _selectedWidgetType = WidgetTypes.All[0];

        [ObservableProperty]
        private string _title = string.Empty;

        // --- Position ---
        [ObservableProperty]
        private double _row;

        [ObservableProperty]
        private double _col;

        [ObservableProperty]
        private int _sizeX = 1;

        [ObservableProperty]
        private int _sizeY = 1;

        // --- Data Source ---
        public List<string> AvailableConnections { get; }

        [ObservableProperty]
        private string? _selectedConnectionId;

        [ObservableProperty]
        private string _address = string.Empty;

        public string[] AvailableDataTypes => DataTypes.All;

        [ObservableProperty]
        private string _selectedDataType = DataTypes.All[0];

        // --- Widget-specific: ValueDisplay ---
        [ObservableProperty]
        private string _format = "{0}";

        [ObservableProperty]
        private string _valueColor = string.Empty;

        [ObservableProperty]
        private double _valueFontSize;

        // --- Widget-specific: CommandButton ---
        public string[] AvailableButtonModes => ButtonModes.All;

        [ObservableProperty]
        private string _selectedButtonMode = ButtonModes.All[0];

        // --- Widget-specific: Slider ---
        [ObservableProperty]
        private double _minValue = 0;

        [ObservableProperty]
        private double _maxValue = 100;

        // --- Widget-specific: PilotLight ---
        [ObservableProperty]
        private string _trueColor = "#00FF00";

        [ObservableProperty]
        private string _falseColor = "#440000";

        // --- Widget-specific: Industrial / Mimic ---
        [ObservableProperty]
        private string _pipePoints = string.Empty;

        [ObservableProperty]
        private string _activeColor = "#00FF00";

        [ObservableProperty]
        private string _inactiveColor = "#FF0000";

        public string[] AvailableValveTypes => new[] { "CutOff", "Regulating" };

        [ObservableProperty]
        private string _selectedValveType = "CutOff";

        [ObservableProperty]
        private bool _showFlanges = true;

        [ObservableProperty]
        private double _thickness = 12.0;

        public string[] AvailableFittings => new[] { "None", "Flange", "Elbow90", "ElbowMinus90", "Elbow45", "ElbowMinus45" };

        [ObservableProperty]
        private string _selectedStartFitting = "None";

        [ObservableProperty]
        private string _selectedEndFitting = "None";

        // --- Widget-specific: Regulating Valve Feedback ---
        [ObservableProperty]
        private string? _feedbackConnectionId;

        [ObservableProperty]
        private string _feedbackAddress = string.Empty;

        [ObservableProperty]
        private double _controlWindowWidth = 320;

        [ObservableProperty]
        private double _controlWindowHeight = 280;

        [ObservableProperty]
        private bool _showRegulatingSettings;

        [ObservableProperty]
        private bool _isVertical;

        public int[] AvailableRotations => new[] { 0, 90, 180, 270 };

        [ObservableProperty]
        private int _selectedRotation = 0;

        [ObservableProperty]
        private double _tolerance = 10.0;

        [ObservableProperty]
        private string? _modeConnectionId;

        [ObservableProperty]
        private string _modeAddress = string.Empty;

        public string[] AvailableActuatorTypes => new[] { "Solenoid", "Diaphragm", "Manual", "None" };

        [ObservableProperty]
        private string _selectedActuatorType = "Solenoid";

        // --- Visibility helpers ---
        [ObservableProperty]
        private bool _showValueDisplaySettings;

        [ObservableProperty]
        private bool _showCommandButtonSettings;

        [ObservableProperty]
        private bool _showSliderSettings;

        [ObservableProperty]
        private bool _showPilotLightSettings;

        [ObservableProperty]
        private bool _showPipeSettings;

        [ObservableProperty]
        private bool _showValveSettings;

        [ObservableProperty]
        private bool _showIndustrialSettings;

        private bool _isEditing;

        // --- Result ---
        public bool IsConfirmed { get; private set; }
        public Action? CloseAction { get; set; }

        public WidgetEditorViewModel(WidgetConfig? existingConfig, List<ConnectionConfig> connections)
        {
            AvailableConnections = connections.Select(c => c.Id).ToList();

            if (existingConfig != null)
            {
                _isEditing = true;
                SelectedWidgetType = existingConfig.Type;
                Title = existingConfig.Title;
                Row = existingConfig.Position.Row;
                Col = existingConfig.Position.Col;
                SizeX = existingConfig.Position.SizeX;
                SizeY = existingConfig.Position.SizeY;
                SelectedConnectionId = existingConfig.Source.ConnId;
                Address = existingConfig.Source.Address;
                SelectedDataType = string.IsNullOrEmpty(existingConfig.Source.DataType) 
                    ? DataTypes.All[0] 
                    : existingConfig.Source.DataType;
                Format = existingConfig.Format;
                SelectedButtonMode = existingConfig.ButtonMode;
                MinValue = existingConfig.MinValue;
                MaxValue = existingConfig.MaxValue;
                TrueColor = existingConfig.TrueColor;
                FalseColor = existingConfig.FalseColor;
                PipePoints = existingConfig.PipePoints;
                ActiveColor = string.IsNullOrEmpty(existingConfig.ActiveColor) ? "#00FF00" : existingConfig.ActiveColor;
                InactiveColor = string.IsNullOrEmpty(existingConfig.InactiveColor) ? "#FF0000" : existingConfig.InactiveColor;
                SelectedValveType = string.IsNullOrEmpty(existingConfig.ValveType) ? "CutOff" : existingConfig.ValveType;
                ShowFlanges = existingConfig.ShowFlanges;
                Thickness = existingConfig.Thickness == 0 ? 12.0 : existingConfig.Thickness;
                SelectedStartFitting = string.IsNullOrEmpty(existingConfig.StartFitting) ? "None" : existingConfig.StartFitting;
                SelectedEndFitting = string.IsNullOrEmpty(existingConfig.EndFitting) ? "None" : existingConfig.EndFitting;
                FeedbackConnectionId = existingConfig.FeedbackSource?.ConnId;
                FeedbackAddress = existingConfig.FeedbackSource?.Address ?? string.Empty;
                ControlWindowWidth = existingConfig.ControlWindowWidth > 0 ? existingConfig.ControlWindowWidth : 320;
                ControlWindowHeight = existingConfig.ControlWindowHeight > 0 ? existingConfig.ControlWindowHeight : 280;
                IsVertical = existingConfig.IsVertical;
                SelectedActuatorType = string.IsNullOrEmpty(existingConfig.ActuatorType) ? "Solenoid" : existingConfig.ActuatorType;
                SelectedRotation = existingConfig.Rotation;
                Tolerance = existingConfig.Tolerance == 0 ? 10.0 : existingConfig.Tolerance;
                ModeConnectionId = existingConfig.ModeSource?.ConnId;
                ModeAddress = existingConfig.ModeSource?.Address ?? string.Empty;

                ValueColor = string.IsNullOrEmpty(existingConfig.ValueColor) 
                    ? (existingConfig.Type == "SetValue" ? "#FFD700" : (existingConfig.Type == "Tank" ? "#E5C158" : "#00FF00"))
                    : existingConfig.ValueColor;
                
                ValueFontSize = existingConfig.ValueFontSize <= 0
                    ? (existingConfig.Type == "SetValue" ? 22 : (existingConfig.Type == "Tank" ? 14 : 28))
                    : existingConfig.ValueFontSize;
            }
            else
            {
                _isEditing = false;
                SetDefaultSizes(SelectedWidgetType);
            }

            UpdateVisibility();
        }

        private void SetDefaultSizes(string type)
        {
            ValueColor = "#00FF00";
            ValueFontSize = 28;
            switch (type)
            {
                case "Valve":
                case "Pump":
                    SizeX = 6;
                    SizeY = 6;
                    break;
                case "Tank":
                    SizeX = 12;
                    SizeY = 20;
                    ValueColor = "#E5C158";
                    ValueFontSize = 14;
                    break;
                case "Pipe":
                    SizeX = 16;
                    SizeY = 2;
                    break;
                case "ValueDisplay":
                    SizeX = 8;
                    SizeY = 4;
                    ValueColor = "#00FF00";
                    ValueFontSize = 28;
                    break;
                case "SetValue":
                    SizeX = 8;
                    SizeY = 4;
                    ValueColor = "#FFD700";
                    ValueFontSize = 22;
                    break;
                case "RealTimeTrend":
                    SizeX = 16;
                    SizeY = 10;
                    break;
                case "Slider":
                    SizeX = 12;
                    SizeY = 3;
                    break;
                default:
                    SizeX = 4;
                    SizeY = 4;
                    break;
            }
        }

        partial void OnSelectedWidgetTypeChanged(string value)
        {
            UpdateVisibility();
            if (value == "Pipe" && string.IsNullOrEmpty(PipePoints))
            {
                PipePoints = "0,0;10,0";
            }
            if (!_isEditing)
            {
                SetDefaultSizes(value);
            }
        }

        private void UpdateVisibility()
        {
            ShowValueDisplaySettings = SelectedWidgetType == "ValueDisplay" || SelectedWidgetType == "SetValue" || SelectedWidgetType == "Tank";
            ShowCommandButtonSettings = SelectedWidgetType == "CommandButton";
            ShowSliderSettings = SelectedWidgetType == "Slider" || SelectedWidgetType == "SetValue" || SelectedWidgetType == "RealTimeTrend" || SelectedWidgetType == "Tank";
            ShowPilotLightSettings = SelectedWidgetType == "PilotLight";
            ShowPipeSettings = SelectedWidgetType == "Pipe";
            ShowValveSettings = SelectedWidgetType == "Valve";
            ShowIndustrialSettings = SelectedWidgetType == "Pipe" || SelectedWidgetType == "Valve" || SelectedWidgetType == "Pump";
            ShowRegulatingSettings = SelectedWidgetType == "Valve";
        }

        [RelayCommand]
        private void Save()
        {
            IsConfirmed = true;
            CloseAction?.Invoke();
        }

        [RelayCommand]
        private void Cancel()
        {
            IsConfirmed = false;
            CloseAction?.Invoke();
        }

        public WidgetConfig ToWidgetConfig()
        {
            return new WidgetConfig
            {
                Type = SelectedWidgetType,
                Title = Title,
                Position = new WidgetPosition
                {
                    Row = Row,
                    Col = Col,
                    SizeX = SizeX,
                    SizeY = SizeY
                },
                Source = new DataSourceConfig
                {
                    ConnId = SelectedConnectionId ?? string.Empty,
                    Address = Address,
                    DataType = SelectedDataType
                },
                Format = Format,
                ButtonMode = SelectedButtonMode,
                MinValue = MinValue,
                MaxValue = MaxValue,
                TrueColor = TrueColor,
                FalseColor = FalseColor,
                PipePoints = PipePoints,
                ActiveColor = ActiveColor,
                InactiveColor = InactiveColor,
                ValveType = SelectedValveType,
                ShowFlanges = ShowFlanges,
                Thickness = Thickness,
                StartFitting = SelectedStartFitting,
                EndFitting = SelectedEndFitting,
                FeedbackSource = !string.IsNullOrEmpty(FeedbackAddress) 
                    ? new DataSourceConfig { ConnId = FeedbackConnectionId ?? string.Empty, Address = FeedbackAddress, DataType = SelectedDataType }
                    : null,
                ControlWindowWidth = ControlWindowWidth,
                ControlWindowHeight = ControlWindowHeight,
                IsVertical = IsVertical,
                ActuatorType = SelectedActuatorType,
                Rotation = SelectedRotation,
                Tolerance = Tolerance == 0 ? 10.0 : Tolerance,
                ModeSource = !string.IsNullOrEmpty(ModeAddress)
                    ? new DataSourceConfig { ConnId = ModeConnectionId ?? string.Empty, Address = ModeAddress, DataType = "Bool" }
                    : null,
                ValueColor = ValueColor,
                ValueFontSize = ValueFontSize
            };
        }
    }
}
