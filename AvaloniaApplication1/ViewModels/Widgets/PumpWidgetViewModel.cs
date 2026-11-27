using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using Avalonia.Threading;
using AvaloniaApplication1.Controls.Primitives;
using AvaloniaApplication1.Models;
using AvaloniaApplication1.Models.Config;
using AvaloniaApplication1.Services;
using AvaloniaApplication1.ViewModels.Core;
using AvaloniaApplication1.ViewModels.Popups;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AvaloniaApplication1.ViewModels.Widgets
{
    public class DecodedStatusBit
    {
        public int BitIndex { get; init; }
        public string Text { get; init; } = string.Empty;
        public StatusSeverity Severity { get; init; }
        public string ColorHex { get; init; } = "#94A3B8";
        public bool IsActive { get; init; }
    }

    public partial class PumpWidgetViewModel : WidgetViewModelBase
    {
        public PumpConfig TypedConfig => (PumpConfig)OriginalConfig;

        [ObservableProperty]
        private EquipmentOperationalState _operationalState = EquipmentOperationalState.Stopped;

        [ObservableProperty]
        private bool _isRunning;

        [ObservableProperty]
        private bool _isCommandOn;

        [ObservableProperty]
        private bool _isFeedbackOn;

        [ObservableProperty]
        private bool _isManualMode = true;

        [ObservableProperty]
        private bool _isFault;

        [ObservableProperty]
        private bool _hasWarning;

        [ObservableProperty]
        private double _setpointFrequency;

        [ObservableProperty]
        private double _actualFrequency;

        [ObservableProperty]
        private double _actualCurrent;

        [ObservableProperty]
        private uint _statusWord;

        [ObservableProperty]
        private string _frequencyText = string.Empty;

        [ObservableProperty]
        private string _currentText = string.Empty;

        [ObservableProperty]
        private string _displayValue = "STOPPED";

        [ObservableProperty]
        private string _currentColor = "#FF0000";

        public ObservableCollection<DecodedStatusBit> ActiveStatusBits { get; } = new();

        public PumpControlMode ControlMode => TypedConfig.ControlMode;
        public bool IsVfdMode => TypedConfig.ControlMode == PumpControlMode.CombinedVfd;
        public bool HasFeedbackSource => !string.IsNullOrEmpty(TypedConfig.FeedbackSource?.Address);
        public bool HasModeSource => !string.IsNullOrEmpty(TypedConfig.ModeSource?.Address);
        public bool HasSetpointSource => !string.IsNullOrEmpty(TypedConfig.SetpointSource?.Address);
        public bool HasFrequencyFeedbackSource => !string.IsNullOrEmpty(TypedConfig.FrequencyFeedbackSource?.Address);
        public bool HasCurrentFeedbackSource => !string.IsNullOrEmpty(TypedConfig.CurrentFeedbackSource?.Address);
        public bool HasStatusWordSource => !string.IsNullOrEmpty(TypedConfig.StatusWordSource?.Address);

        public string ActiveColor => TypedConfig.ActiveColor ?? string.Empty;
        public string InactiveColor => TypedConfig.InactiveColor ?? string.Empty;
        public int Rotation => TypedConfig.Rotation;
        public string FrequencyUnit => string.IsNullOrEmpty(TypedConfig.FrequencyUnit) ? "Hz" : TypedConfig.FrequencyUnit;
        public double MinFrequency => TypedConfig.MinFrequency;
        public double MaxFrequency => TypedConfig.MaxFrequency > TypedConfig.MinFrequency ? TypedConfig.MaxFrequency : 50.0;

        private CompositeDisposable _secondaryDisposables = new();

        public PumpWidgetViewModel(PumpConfig config, IDataCoreService dataService, IProjectContextService projectContext)
            : base(config, dataService, projectContext)
        {
            SubscribeSecondarySources();
            UpdateState();
        }

        private void SubscribeSecondarySources()
        {
            _secondaryDisposables.Dispose();
            _secondaryDisposables = new CompositeDisposable();

            SubscribeTag(TypedConfig.ModeSource);
            SubscribeTag(TypedConfig.FeedbackSource);
            SubscribeTag(TypedConfig.SetpointSource);
            SubscribeTag(TypedConfig.FrequencyFeedbackSource);
            SubscribeTag(TypedConfig.CurrentFeedbackSource);
            SubscribeTag(TypedConfig.StatusWordSource);
        }

        private void SubscribeTag(DataSourceConfig? src)
        {
            if (src == null || string.IsNullOrEmpty(src.ConnId) || string.IsNullOrEmpty(src.Address))
                return;

            DataService.TagUpdates
                .Where(t => t.ConnId == src.ConnId && t.Address == src.Address)
                .Subscribe(_ => DispatchUpdate())
                .DisposeWith(_secondaryDisposables);
        }

        private void DispatchUpdate()
        {
            if (Dispatcher.UIThread.CheckAccess())
                UpdateState();
            else
                Dispatcher.UIThread.Post(UpdateState);
        }

        protected override void OnTagValueUpdated(object newValue)
        {
            UpdateState();
        }

        private void UpdateState()
        {
            // 1. Primary Start/Stop command coil
            if (Source != null && !string.IsNullOrEmpty(Source.Address))
            {
                var cmdVal = DataService.GetCurrentValue(Source.ConnId, Source.Address);
                IsCommandOn = cmdVal != null && TagValueConverter.ToBool(cmdVal);
            }
            else
            {
                IsCommandOn = false;
            }

            // 2. ModeSource (false/0 = Manual, true/1 = Auto)
            if (HasModeSource)
            {
                var modeVal = DataService.GetCurrentValue(TypedConfig.ModeSource!.ConnId, TypedConfig.ModeSource.Address);
                IsManualMode = modeVal == null || !TagValueConverter.ToBool(modeVal);
            }
            else
            {
                IsManualMode = true;
            }

            // 3. Discrete feedback
            if (HasFeedbackSource)
            {
                var fbVal = DataService.GetCurrentValue(TypedConfig.FeedbackSource!.ConnId, TypedConfig.FeedbackSource.Address);
                IsFeedbackOn = fbVal != null && TagValueConverter.ToBool(fbVal);
            }
            else
            {
                IsFeedbackOn = IsCommandOn;
            }

            // 4. VFD Setpoint, Frequency, Current, StatusWord
            if (IsVfdMode)
            {
                if (HasSetpointSource)
                {
                    var spVal = DataService.GetCurrentValue(TypedConfig.SetpointSource!.ConnId, TypedConfig.SetpointSource.Address);
                    if (TryToDouble(spVal, out var sp))
                        SetpointFrequency = sp;
                }

                if (HasFrequencyFeedbackSource)
                {
                    var fqVal = DataService.GetCurrentValue(TypedConfig.FrequencyFeedbackSource!.ConnId, TypedConfig.FrequencyFeedbackSource.Address);
                    if (TryToDouble(fqVal, out var fq))
                        ActualFrequency = fq;
                }
                else
                {
                    ActualFrequency = IsCommandOn ? SetpointFrequency : 0.0;
                }
                FrequencyText = $"{ActualFrequency:F1} {FrequencyUnit}";

                if (HasCurrentFeedbackSource)
                {
                    var curVal = DataService.GetCurrentValue(TypedConfig.CurrentFeedbackSource!.ConnId, TypedConfig.CurrentFeedbackSource.Address);
                    if (TryToDouble(curVal, out var cur))
                        ActualCurrent = cur;
                }
                CurrentText = $"{ActualCurrent:F1} A";

                if (HasStatusWordSource)
                {
                    var swVal = DataService.GetCurrentValue(TypedConfig.StatusWordSource!.ConnId, TypedConfig.StatusWordSource.Address);
                    StatusWord = TryToUInt(swVal);
                }
                else
                {
                    StatusWord = 0;
                }

                DecodeStatusWord();
            }
            else
            {
                FrequencyText = string.Empty;
                CurrentText = string.Empty;
                IsFault = false;
                HasWarning = false;
                ActiveStatusBits.Clear();
            }

            // 5. Running & OperationalState resolution
            if (ControlMode == PumpControlMode.DiscreteWithFeedback && HasFeedbackSource)
            {
                IsRunning = IsFeedbackOn;
            }
            else if (ControlMode == PumpControlMode.CombinedVfd)
            {
                if (HasFeedbackSource)
                    IsRunning = IsFeedbackOn;
                else if (HasFrequencyFeedbackSource)
                    IsRunning = IsCommandOn || ActualFrequency > 0.5;
                else
                    IsRunning = IsCommandOn;
            }
            else
            {
                IsRunning = IsCommandOn;
            }

            if (Quality == TagQuality.CommFailure)
            {
                OperationalState = EquipmentOperationalState.Offline;
            }
            else if (IsFault)
            {
                OperationalState = EquipmentOperationalState.Fault;
            }
            else if ((ControlMode == PumpControlMode.DiscreteWithFeedback || ControlMode == PumpControlMode.CombinedVfd)
                     && HasFeedbackSource && IsCommandOn != IsFeedbackOn)
            {
                OperationalState = EquipmentOperationalState.Transit;
            }
            else if (IsRunning)
            {
                OperationalState = EquipmentOperationalState.Running;
            }
            else
            {
                OperationalState = EquipmentOperationalState.Stopped;
            }

            // 6. Display text & fallback color
            DisplayValue = OperationalState switch
            {
                EquipmentOperationalState.Offline => "OFFLINE",
                EquipmentOperationalState.Fault => "FAULT",
                EquipmentOperationalState.Transit => IsCommandOn ? "STARTING" : "STOPPING",
                EquipmentOperationalState.Running => IsVfdMode ? FrequencyText : "RUNNING",
                _ => "STOPPED"
            };

            var isIndustrialGray = string.Equals(
                Avalonia.Application.Current?.ActualThemeVariant?.Key?.ToString(),
                "IndustrialGray",
                StringComparison.OrdinalIgnoreCase);

            CurrentColor = OperationalState switch
            {
                EquipmentOperationalState.Fault => "#EF4444",
                EquipmentOperationalState.Transit => "#0284C7",
                EquipmentOperationalState.Offline => "#6B7280",
                EquipmentOperationalState.Running => isIndustrialGray
                    ? "#334155"
                    : (string.IsNullOrEmpty(ActiveColor) ? "#22C55E" : ActiveColor),
                _ => isIndustrialGray
                    ? "#525A65"
                    : (string.IsNullOrEmpty(InactiveColor) ? "#64748B" : InactiveColor)
            };
        }

        private void DecodeStatusWord()
        {
            bool anyFault = false;
            bool anyWarning = false;
            var decoded = new List<DecodedStatusBit>();

            if (TypedConfig.StatusWordMap != null)
            {
                foreach (var mapping in TypedConfig.StatusWordMap)
                {
                    if (mapping.BitIndex < 0 || mapping.BitIndex > 31) continue;
                    bool isSet = (StatusWord & (1u << mapping.BitIndex)) != 0;
                    if (!isSet) continue;

                    if (mapping.Severity == StatusSeverity.Fault) anyFault = true;
                    if (mapping.Severity == StatusSeverity.Warning) anyWarning = true;

                    var color = !string.IsNullOrWhiteSpace(mapping.CustomColorHex)
                        ? mapping.CustomColorHex
                        : mapping.Severity switch
                        {
                            StatusSeverity.Fault => "#EF4444",
                            StatusSeverity.Warning => "#F59E0B",
                            StatusSeverity.Info => "#38BDF8",
                            _ => "#22C55E"
                        };

                    decoded.Add(new DecodedStatusBit
                    {
                        BitIndex = mapping.BitIndex,
                        Text = string.IsNullOrWhiteSpace(mapping.Text) ? $"Bit {mapping.BitIndex}" : mapping.Text,
                        Severity = mapping.Severity,
                        ColorHex = color,
                        IsActive = true
                    });
                }
            }

            IsFault = anyFault;
            HasWarning = anyWarning;

            ActiveStatusBits.Clear();
            foreach (var item in decoded)
                ActiveStatusBits.Add(item);
        }

        private static bool TryToDouble(object? val, out double result)
        {
            result = 0;
            if (val == null) return false;
            if (val is double d) { result = d; return true; }
            if (val is float f) { result = f; return true; }
            if (val is int i) { result = i; return true; }
            if (val is long l) { result = l; return true; }
            if (val is bool b) { result = b ? 1.0 : 0.0; return true; }
            return double.TryParse(val.ToString()?.Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out result);
        }

        private static uint TryToUInt(object? val)
        {
            if (val == null) return 0;
            if (val is uint u) return u;
            if (val is int i) return i >= 0 ? (uint)i : 0;
            if (val is long l) return l >= 0 ? (uint)l : 0;
            if (val is ulong ul) return (uint)ul;
            if (val is ushort us) return us;
            if (val is short s) return s >= 0 ? (uint)s : 0;
            if (val is byte b) return b;
            if (double.TryParse(val.ToString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var d))
                return d >= 0 ? (uint)Math.Round(d) : 0;
            return 0;
        }

        public bool SendStartCommand()
        {
            if (Source == null || string.IsNullOrEmpty(Source.Address)) return false;
            if (HasModeSource && !IsManualMode) return false; // Interlock: blocked in Auto mode
            DataService.WriteCommand(Source.ConnId, Source.Address, true);
            return true;
        }

        public bool SendStopCommand()
        {
            if (Source == null || string.IsNullOrEmpty(Source.Address)) return false;
            if (HasModeSource && !IsManualMode) return false; // Interlock: blocked in Auto mode
            DataService.WriteCommand(Source.ConnId, Source.Address, false);
            return true;
        }

        public bool WriteSetpoint(double value)
        {
            if (!IsVfdMode || !HasSetpointSource) return false;
            if (HasModeSource && !IsManualMode) return false; // Interlock: blocked in Auto mode
            var clamped = Math.Clamp(value, MinFrequency, MaxFrequency);
            DataService.WriteCommand(TypedConfig.SetpointSource!.ConnId, TypedConfig.SetpointSource.Address, clamped);
            return true;
        }

        public void SetMode(bool manual)
        {
            if (!HasModeSource) return;
            // Convention: false = Manual, true = Auto
            DataService.WriteCommand(TypedConfig.ModeSource!.ConnId, TypedConfig.ModeSource.Address, !manual);
        }

        private ChildWindowViewModel? _controlWindow;

        [RelayCommand]
        private void OpenControlPopup()
        {
            if (ProjectContext?.IsDesignMode == true) return;
            if (Source == null && !IsVfdMode) return;

            if (_controlWindow != null)
            {
                _controlWindow.CloseAction?.Invoke();
                _controlWindow = null;
                return;
            }

            PumpFaceplateViewModel? popupVm = null;
            popupVm = new PumpFaceplateViewModel(this, () => _controlWindow?.CloseAction?.Invoke());

            _controlWindow = ChildWindowService?.OpenChildWindow($"{Title} [Управление насосом]", (object)popupVm);
            if (_controlWindow != null)
            {
                var originalClose = _controlWindow.CloseAction;
                _controlWindow.CloseAction = () =>
                {
                    popupVm.Dispose();
                    originalClose?.Invoke();
                    _controlWindow = null;
                };
            }
            else
            {
                popupVm.Dispose();
            }
        }

        public override void UpdateFromConfig(WidgetConfig newConfig)
        {
            base.UpdateFromConfig(newConfig);
            if (newConfig is PumpConfig pumpConfig)
            {
                TypedConfig.ActiveColor = pumpConfig.ActiveColor;
                TypedConfig.InactiveColor = pumpConfig.InactiveColor;
                TypedConfig.ControlMode = pumpConfig.ControlMode;
                TypedConfig.ModeSource = pumpConfig.ModeSource;
                TypedConfig.FeedbackSource = pumpConfig.FeedbackSource;
                TypedConfig.SetpointSource = pumpConfig.SetpointSource;
                TypedConfig.FrequencyFeedbackSource = pumpConfig.FrequencyFeedbackSource;
                TypedConfig.CurrentFeedbackSource = pumpConfig.CurrentFeedbackSource;
                TypedConfig.StatusWordSource = pumpConfig.StatusWordSource;
                TypedConfig.FrequencyUnit = pumpConfig.FrequencyUnit;
                TypedConfig.MinFrequency = pumpConfig.MinFrequency;
                TypedConfig.MaxFrequency = pumpConfig.MaxFrequency;
                TypedConfig.Rotation = pumpConfig.Rotation;
                TypedConfig.StatusWordMap = pumpConfig.StatusWordMap ?? new();

                SubscribeSecondarySources();

                OnPropertyChanged(nameof(ActiveColor));
                OnPropertyChanged(nameof(InactiveColor));
                OnPropertyChanged(nameof(ControlMode));
                OnPropertyChanged(nameof(IsVfdMode));
                OnPropertyChanged(nameof(HasModeSource));
                OnPropertyChanged(nameof(HasFeedbackSource));
                OnPropertyChanged(nameof(HasSetpointSource));
                OnPropertyChanged(nameof(HasFrequencyFeedbackSource));
                OnPropertyChanged(nameof(HasCurrentFeedbackSource));
                OnPropertyChanged(nameof(HasStatusWordSource));
                OnPropertyChanged(nameof(FrequencyUnit));
                OnPropertyChanged(nameof(MinFrequency));
                OnPropertyChanged(nameof(MaxFrequency));
                OnPropertyChanged(nameof(Rotation));

                UpdateState();
            }
        }

        public override void Dispose()
        {
            _secondaryDisposables.Dispose();
            base.Dispose();
        }
    }
}
