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

                if (existingConfig is ValueDisplayConfig vd) { Format = vd.Format; ValueColor = vd.ValueColor; ValueFontSize = vd.ValueFontSize; }
                if (existingConfig is PilotLightConfig pl) { TrueColor = pl.TrueColor; FalseColor = pl.FalseColor; }
                if (existingConfig is CommandButtonConfig cb) { SelectedButtonMode = cb.ButtonMode; }
                if (existingConfig is SliderConfig sl) { MinValue = sl.MinValue; MaxValue = sl.MaxValue; }
                if (existingConfig is SetValueConfig sv) { Format = sv.Format; MinValue = sv.MinValue; MaxValue = sv.MaxValue; ValueColor = sv.ValueColor; ValueFontSize = sv.ValueFontSize; }
                if (existingConfig is RealTimeTrendConfig rt) { Format = rt.Format; MinValue = rt.MinValue; MaxValue = rt.MaxValue; }
                if (existingConfig is PipeConfig pc) { PipePoints = pc.PipePoints; ActiveColor = pc.ActiveColor; InactiveColor = pc.InactiveColor; ShowFlanges = pc.ShowFlanges; Thickness = pc.Thickness; SelectedStartFitting = pc.StartFitting; SelectedEndFitting = pc.EndFitting; }
                if (existingConfig is ValveConfig vc) { SelectedValveType = vc.ValveType; ActiveColor = vc.ActiveColor; InactiveColor = vc.InactiveColor; FeedbackConnectionId = vc.FeedbackSource?.ConnId; FeedbackAddress = vc.FeedbackSource?.Address ?? string.Empty; IsVertical = vc.IsVertical; SelectedActuatorType = vc.ActuatorType; SelectedRotation = vc.Rotation; Tolerance = vc.Tolerance; ModeConnectionId = vc.ModeSource?.ConnId; ModeAddress = vc.ModeSource?.Address ?? string.Empty; }
                if (existingConfig is TankConfig tc) { Format = tc.Format; MinValue = tc.MinValue; MaxValue = tc.MaxValue; ValueColor = tc.ValueColor; ValueFontSize = tc.ValueFontSize; }
                if (existingConfig is PumpConfig pu) { ActiveColor = pu.ActiveColor; InactiveColor = pu.InactiveColor; ControlWindowWidth = pu.ControlWindowWidth; ControlWindowHeight = pu.ControlWindowHeight; }
                if (existingConfig is HeatExchangerConfig he) { Format = he.Format; ActiveColor = he.ActiveColor; InactiveColor = he.InactiveColor; ShowFlanges = he.ShowFlanges; }
                if (existingConfig is ReactorConfig re) { Format = re.Format; MinValue = re.MinValue; MaxValue = re.MaxValue; ActiveColor = re.ActiveColor; InactiveColor = re.InactiveColor; }
                if (existingConfig is LevelSensorConfig ls) { Format = ls.Format; MinValue = ls.MinValue; MaxValue = ls.MaxValue; ValueColor = ls.ValueColor; }
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
                case "HeatExchanger":
                    SizeX = 14;
                    SizeY = 8;
                    ValueColor = "#00FFCC";
                    break;
                case "Reactor":
                    SizeX = 14;
                    SizeY = 20;
                    ValueColor = "#00FF00";
                    break;
                case "LevelSensor":
                    SizeX = 6;
                    SizeY = 10;
                    ValueColor = "#00B4FF";
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
            ShowValueDisplaySettings = SelectedWidgetType == "ValueDisplay" || SelectedWidgetType == "SetValue" || SelectedWidgetType == "Tank" || SelectedWidgetType == "HeatExchanger" || SelectedWidgetType == "Reactor" || SelectedWidgetType == "LevelSensor";
            ShowCommandButtonSettings = SelectedWidgetType == "CommandButton";
            ShowSliderSettings = SelectedWidgetType == "Slider" || SelectedWidgetType == "SetValue" || SelectedWidgetType == "RealTimeTrend" || SelectedWidgetType == "Tank" || SelectedWidgetType == "Reactor" || SelectedWidgetType == "LevelSensor";
            ShowPilotLightSettings = SelectedWidgetType == "PilotLight";
            ShowPipeSettings = SelectedWidgetType == "Pipe";
            ShowValveSettings = SelectedWidgetType == "Valve";
            ShowIndustrialSettings = SelectedWidgetType == "Pipe" || SelectedWidgetType == "Valve" || SelectedWidgetType == "Pump" || SelectedWidgetType == "HeatExchanger" || SelectedWidgetType == "Reactor";
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
            WidgetConfig config = SelectedWidgetType switch
            {
                "ValueDisplay" => new ValueDisplayConfig { Format = Format, ValueColor = ValueColor, ValueFontSize = ValueFontSize },
                "PilotLight" => new PilotLightConfig { TrueColor = TrueColor, FalseColor = FalseColor },
                "CommandButton" => new CommandButtonConfig { ButtonMode = SelectedButtonMode },
                "Slider" => new SliderConfig { MinValue = MinValue, MaxValue = MaxValue },
                "SetValue" => new SetValueConfig { Format = Format, MinValue = MinValue, MaxValue = MaxValue, ValueColor = ValueColor, ValueFontSize = ValueFontSize },
                "RealTimeTrend" => new RealTimeTrendConfig { Format = Format, MinValue = MinValue, MaxValue = MaxValue },
                "Pipe" => new PipeConfig { PipePoints = PipePoints, ActiveColor = ActiveColor, InactiveColor = InactiveColor, ShowFlanges = ShowFlanges, Thickness = Thickness, StartFitting = SelectedStartFitting, EndFitting = SelectedEndFitting },
                "Valve" => new ValveConfig { 
                    ValveType = SelectedValveType, 
                    ActiveColor = ActiveColor, 
                    InactiveColor = InactiveColor, 
                    FeedbackSource = !string.IsNullOrEmpty(FeedbackAddress) ? new DataSourceConfig { ConnId = SelectedConnectionId ?? string.Empty, Address = FeedbackAddress, DataType = SelectedDataType } : null,
                    IsVertical = IsVertical,
                    ActuatorType = SelectedActuatorType,
                    Rotation = SelectedRotation,
                    AlarmDisabled = false,
                    Tolerance = Tolerance == 0 ? 10.0 : Tolerance,
                    ModeSource = !string.IsNullOrEmpty(ModeAddress) ? new DataSourceConfig { ConnId = SelectedConnectionId ?? string.Empty, Address = ModeAddress, DataType = "Bool" } : null
                },
                "Tank" => new TankConfig { Format = Format, MinValue = MinValue, MaxValue = MaxValue, ValueColor = ValueColor, ValueFontSize = ValueFontSize },
                "Pump" => new PumpConfig { ActiveColor = ActiveColor, InactiveColor = InactiveColor, ControlWindowWidth = ControlWindowWidth, ControlWindowHeight = ControlWindowHeight },
                "HeatExchanger" => new HeatExchangerConfig { ExchangerType = "ShellAndTube", Format = Format, ActiveColor = ActiveColor, InactiveColor = InactiveColor, ShowFlanges = ShowFlanges },
                "Reactor" => new ReactorConfig { MinValue = MinValue, MaxValue = MaxValue, Format = Format, HasJacket = true, ActiveColor = ActiveColor, InactiveColor = InactiveColor },
                "LevelSensor" => new LevelSensorConfig { SensorType = "Radar", TagNumber = Title, Unit = "%", Format = Format, MinValue = MinValue, MaxValue = MaxValue, AlarmHigh = 90, AlarmLow = 10, ValueColor = ValueColor },
                "ContainerButton" => new ContainerButtonConfig(),
                _ => new WidgetConfigBase()
            };

            config.Type = SelectedWidgetType;
            config.Title = Title;
            config.Position = new WidgetPosition { Row = Row, Col = Col, SizeX = SizeX, SizeY = SizeY };
            config.Source = new DataSourceConfig { ConnId = SelectedConnectionId ?? string.Empty, Address = Address, DataType = SelectedDataType };

            return config;
        }
    }
}
