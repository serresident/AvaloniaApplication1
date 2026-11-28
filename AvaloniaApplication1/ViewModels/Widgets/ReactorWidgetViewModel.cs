using System;
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
    public partial class ReactorWidgetViewModel : WidgetViewModelBase
    {
        public ReactorConfig TypedConfig => (ReactorConfig)OriginalConfig;

        [ObservableProperty]
        private double _currentLevel;

        [ObservableProperty]
        private double _temperature = 20.0;

        [ObservableProperty]
        private double _tempSetpoint = 20.0;

        [ObservableProperty]
        private bool _isAgitatorRunning;

        [ObservableProperty]
        private bool _isAgitatorCommandOn;

        [ObservableProperty]
        private bool _isAgitatorFeedbackOn;

        [ObservableProperty]
        private bool _isManualMode = true;

        [ObservableProperty]
        private bool _isFault;

        [ObservableProperty]
        private bool _hasTempAlarm;

        [ObservableProperty]
        private bool _hasLevelAlarm;

        [ObservableProperty]
        private EquipmentOperationalState _operationalState = EquipmentOperationalState.Stopped;

        [ObservableProperty]
        private string _displayValue = "---";

        [ObservableProperty]
        private string _statusText = "ОСТАНОВЛЕН";

        [ObservableProperty]
        private string _currentColor = "#555555";

        private readonly IChildWindowService? _childWindowService;
        private readonly IThemeService? _themeService;
        private ChildWindowViewModel? _controlWindow;

        public double MinValue => TypedConfig.MinValue;
        public double MaxValue => TypedConfig.MaxValue == 0 ? 100 : TypedConfig.MaxValue;
        public double MinTemp => TypedConfig.MinTemp;
        public double MaxTemp => TypedConfig.MaxTemp <= TypedConfig.MinTemp ? 200 : TypedConfig.MaxTemp;
        public double HighTempAlarm => TypedConfig.HighTempAlarm;
        public double HighLevelAlarm => TypedConfig.HighLevelAlarm;
        public double LowLevelAlarm => TypedConfig.LowLevelAlarm;
        public bool HasJacket => TypedConfig.HasJacket;
        public string ActiveColor => string.IsNullOrEmpty(TypedConfig.ActiveColor) ? "#00FF00" : TypedConfig.ActiveColor;
        public string InactiveColor => string.IsNullOrEmpty(TypedConfig.InactiveColor) ? "#555555" : TypedConfig.InactiveColor;
        public string Format => string.IsNullOrEmpty(TypedConfig.Format) ? "{0:F1} %" : TypedConfig.Format;

        public bool HasAgitatorSource => !string.IsNullOrEmpty(TypedConfig.AgitatorSource?.Address) || !string.IsNullOrEmpty(TypedConfig.AgitatorSource?.TagId);
        public bool HasAgitatorFeedbackSource => !string.IsNullOrEmpty(TypedConfig.AgitatorFeedbackSource?.Address) || !string.IsNullOrEmpty(TypedConfig.AgitatorFeedbackSource?.TagId);
        public bool HasTempSource => !string.IsNullOrEmpty(TypedConfig.TempSource?.Address) || !string.IsNullOrEmpty(TypedConfig.TempSource?.TagId);
        public bool HasTempSetpointSource => !string.IsNullOrEmpty(TypedConfig.TempSetpointSource?.Address) || !string.IsNullOrEmpty(TypedConfig.TempSetpointSource?.TagId);
        public bool HasModeTag => !string.IsNullOrEmpty(TypedConfig.ModeSource?.Address) || !string.IsNullOrEmpty(TypedConfig.ModeSource?.TagId);

        public ReactorWidgetViewModel(
            ReactorConfig config,
            IDataCoreService dataService,
            IProjectContextService projectContext,
            IChildWindowService? childWindowService = null,
            IThemeService? themeService = null)
            : base(config, dataService, projectContext)
        {
            _childWindowService = childWindowService;
            _themeService = themeService;

            if (_themeService != null)
            {
                _themeService.PropertyChanged += OnThemePropertyChanged;
            }

            SubscribeSecondarySources();
            UpdateState();
        }

        private void OnThemePropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(IThemeService.CurrentTheme) || e.PropertyName == nameof(IThemeService.IsIndustrialGrayTheme))
            {
                Dispatcher.UIThread.Post(UpdateState);
            }
        }

        private void SubscribeSecondarySources()
        {
            SubscribeSource(TypedConfig.AgitatorSource);
            SubscribeSource(TypedConfig.AgitatorFeedbackSource);
            SubscribeSource(TypedConfig.TempSource);
            SubscribeSource(TypedConfig.TempSetpointSource);
            SubscribeSource(TypedConfig.ModeSource);
        }

        private void SubscribeSource(DataSourceConfig? src)
        {
            if (src == null) return;
            if (string.IsNullOrEmpty(src.TagId) && (string.IsNullOrEmpty(src.ConnId) || string.IsNullOrEmpty(src.Address)))
                return;

            DataService.SnapshotUpdates
                .Where(s => MatchesDataSource(s, src))
                .Subscribe(_ => Dispatcher.UIThread.Post(UpdateState))
                .DisposeWith(Disposables);
        }

        private bool MatchesDataSource(TagSnapshot s, DataSourceConfig src)
        {
            if (!string.IsNullOrEmpty(src.TagId) && string.Equals(s.TagId, src.TagId, StringComparison.OrdinalIgnoreCase))
                return true;

            if (!string.IsNullOrEmpty(src.ConnId) && !string.IsNullOrEmpty(src.Address))
            {
                var genId = ConfigMigrationService.GenerateTagId(src.ConnId, src.Address);
                if (string.Equals(s.TagId, genId, StringComparison.OrdinalIgnoreCase))
                    return true;

                var reg = DataService.TagManager?.FindTagByAddress(src.ConnId, src.Address);
                if (reg != null && string.Equals(s.TagId, reg.TagId, StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            return false;
        }

        private object? ReadSourceValue(DataSourceConfig? src)
        {
            if (src == null) return null;
            if (!string.IsNullOrEmpty(src.TagId))
            {
                var snap = DataService.GetTagSnapshot(src.TagId);
                if (snap.Quality != TagQuality.Uninitialized && snap.Value != null)
                    return snap.Value;
            }
            if (!string.IsNullOrEmpty(src.ConnId) && !string.IsNullOrEmpty(src.Address))
                return DataService.GetCurrentValue(src.ConnId, src.Address);
            return null;
        }

        protected override void OnTagValueUpdated(object newValue) => UpdateState();
        protected override void OnTagSnapshotUpdated(TagSnapshot snapshot) => UpdateState();

        public void UpdateState()
        {
            // 1. Level (Source)
            var levelVal = ReadSourceValue(Source);
            if (levelVal != null)
            {
                double dVal = TagValueConverter.ToDouble(levelVal);
                CurrentLevel = Math.Clamp(dVal, MinValue, MaxValue);
                HasLevelAlarm = CurrentLevel >= HighLevelAlarm || CurrentLevel <= LowLevelAlarm;
                try
                {
                    DisplayValue = string.Format(CultureInfo.InvariantCulture, Format, CurrentLevel);
                }
                catch
                {
                    DisplayValue = $"{CurrentLevel.ToString("F1", CultureInfo.InvariantCulture)} %";
                }
            }
            else
            {
                HasLevelAlarm = false;
                DisplayValue = "---";
            }

            // 2. Temperature & Setpoint
            var tempVal = ReadSourceValue(TypedConfig.TempSource);
            if (tempVal != null)
            {
                Temperature = TagValueConverter.ToDouble(tempVal);
                HasTempAlarm = HasJacket && Temperature >= HighTempAlarm;
            }
            else
            {
                HasTempAlarm = false;
            }

            var spVal = ReadSourceValue(TypedConfig.TempSetpointSource);
            if (spVal != null)
            {
                TempSetpoint = Math.Clamp(TagValueConverter.ToDouble(spVal), MinTemp, MaxTemp);
            }

            // 3. Mode (Manual / Auto)
            if (HasModeTag)
            {
                var modeVal = ReadSourceValue(TypedConfig.ModeSource);
                if (modeVal != null)
                {
                    // true/1 = Auto, false/0 = Manual
                    IsManualMode = !TagValueConverter.ToBool(modeVal);
                }
            }
            else
            {
                IsManualMode = true;
            }

            // 4. Agitator Command & Feedback
            var agVal = ReadSourceValue(TypedConfig.AgitatorSource);
            IsAgitatorCommandOn = agVal != null && TagValueConverter.ToBool(agVal);

            if (HasAgitatorFeedbackSource)
            {
                var fbVal = ReadSourceValue(TypedConfig.AgitatorFeedbackSource);
                IsAgitatorFeedbackOn = fbVal != null && TagValueConverter.ToBool(fbVal);
                IsAgitatorRunning = IsAgitatorFeedbackOn;
            }
            else
            {
                IsAgitatorFeedbackOn = IsAgitatorCommandOn;
                IsAgitatorRunning = IsAgitatorCommandOn;
            }

            // 5. Operational State & ISA-101 Color
            bool isOffline = Quality == TagQuality.Bad || Quality == TagQuality.CommFailure || Quality == TagQuality.Timeout;
            bool isTransit = HasAgitatorFeedbackSource && (IsAgitatorCommandOn != IsAgitatorFeedbackOn);
            IsFault = HasTempAlarm || HasLevelAlarm;

            if (isOffline)
            {
                OperationalState = EquipmentOperationalState.Offline;
                StatusText = "НЕТ СВЯЗИ";
            }
            else if (IsFault)
            {
                OperationalState = EquipmentOperationalState.Fault;
                StatusText = HasTempAlarm ? "ПЕРЕГРЕВ" : "АВАРИЯ УРОВНЯ";
            }
            else if (isTransit)
            {
                OperationalState = EquipmentOperationalState.Transit;
                StatusText = IsAgitatorCommandOn ? "ЗАПУСК" : "ОСТАНОВ";
            }
            else if (IsAgitatorRunning)
            {
                OperationalState = EquipmentOperationalState.Running;
                StatusText = "МЕШАЛКА ВКЛ";
            }
            else
            {
                OperationalState = EquipmentOperationalState.Stopped;
                StatusText = "ОСТАНОВЛЕН";
            }

            bool isGray = _themeService?.IsIndustrialGrayTheme == true ||
                          string.Equals(Avalonia.Application.Current?.ActualThemeVariant?.Key?.ToString(), "IndustrialGray", StringComparison.OrdinalIgnoreCase);
            CurrentColor = OperationalState switch
            {
                EquipmentOperationalState.Fault => "#DC2626",
                EquipmentOperationalState.Transit => "#0284C7",
                EquipmentOperationalState.Offline => "#6B7280",
                EquipmentOperationalState.Running => isGray ? "#334155" : ActiveColor,
                _ => isGray ? "#525A65" : InactiveColor
            };
        }

        [RelayCommand]
        private void OpenControlPopup()
        {
            if (ProjectContext?.IsDesignMode == true) return;

            if (_controlWindow != null)
            {
                _controlWindow.CloseAction?.Invoke();
                _controlWindow = null;
                return;
            }

            var childSvc = _childWindowService ?? ChildWindowService;
            if (childSvc != null)
            {
                ReactorFaceplateViewModel? popupVm = null;
                popupVm = new ReactorFaceplateViewModel(this, () => _controlWindow?.CloseAction?.Invoke());

                _controlWindow = childSvc.OpenChildWindow($"Реактор: {Title}", (object)popupVm);
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
            }
            else
            {
                ToggleAgitator();
            }
        }

        [RelayCommand]
        public void ToggleAgitator()
        {
            if (ProjectContext?.IsDesignMode == true) return;
            if (HasModeTag && !IsManualMode) return;

            if (TypedConfig.AgitatorSource != null &&
                !string.IsNullOrEmpty(TypedConfig.AgitatorSource.ConnId) &&
                !string.IsNullOrEmpty(TypedConfig.AgitatorSource.Address))
            {
                bool nextState = !IsAgitatorCommandOn;
                DataService.WriteCommand(TypedConfig.AgitatorSource.ConnId, TypedConfig.AgitatorSource.Address, nextState);
            }
        }

        public void SetAgitatorCommand(bool run)
        {
            if (ProjectContext?.IsDesignMode == true) return;
            if (HasModeTag && !IsManualMode) return;

            if (TypedConfig.AgitatorSource != null &&
                !string.IsNullOrEmpty(TypedConfig.AgitatorSource.ConnId) &&
                !string.IsNullOrEmpty(TypedConfig.AgitatorSource.Address))
            {
                DataService.WriteCommand(TypedConfig.AgitatorSource.ConnId, TypedConfig.AgitatorSource.Address, run);
            }
        }

        public void SetTemperatureSetpoint(double temp)
        {
            if (ProjectContext?.IsDesignMode == true) return;
            if (HasModeTag && !IsManualMode) return;

            double clamped = Math.Clamp(temp, MinTemp, MaxTemp);
            if (TypedConfig.TempSetpointSource != null &&
                !string.IsNullOrEmpty(TypedConfig.TempSetpointSource.ConnId) &&
                !string.IsNullOrEmpty(TypedConfig.TempSetpointSource.Address))
            {
                DataService.WriteCommand(TypedConfig.TempSetpointSource.ConnId, TypedConfig.TempSetpointSource.Address, clamped);
            }
        }

        public void SetMode(bool manual)
        {
            if (ProjectContext?.IsDesignMode == true || !HasModeTag) return;

            if (TypedConfig.ModeSource != null &&
                !string.IsNullOrEmpty(TypedConfig.ModeSource.ConnId) &&
                !string.IsNullOrEmpty(TypedConfig.ModeSource.Address))
            {
                // true = Auto, false = Manual
                DataService.WriteCommand(TypedConfig.ModeSource.ConnId, TypedConfig.ModeSource.Address, !manual);
            }
        }

        public override void UpdateFromConfig(WidgetConfig newConfig)
        {
            base.UpdateFromConfig(newConfig);
            if (newConfig is ReactorConfig rConfig)
            {
                TypedConfig.MinValue = rConfig.MinValue;
                TypedConfig.MaxValue = rConfig.MaxValue;
                TypedConfig.MinTemp = rConfig.MinTemp;
                TypedConfig.MaxTemp = rConfig.MaxTemp;
                TypedConfig.HighTempAlarm = rConfig.HighTempAlarm;
                TypedConfig.HighLevelAlarm = rConfig.HighLevelAlarm;
                TypedConfig.LowLevelAlarm = rConfig.LowLevelAlarm;
                TypedConfig.HasJacket = rConfig.HasJacket;
                TypedConfig.ActiveColor = rConfig.ActiveColor;
                TypedConfig.InactiveColor = rConfig.InactiveColor;
                TypedConfig.Format = rConfig.Format;
                TypedConfig.AgitatorSource = rConfig.AgitatorSource;
                TypedConfig.AgitatorFeedbackSource = rConfig.AgitatorFeedbackSource;
                TypedConfig.TempSource = rConfig.TempSource;
                TypedConfig.TempSetpointSource = rConfig.TempSetpointSource;
                TypedConfig.ModeSource = rConfig.ModeSource;

                OnPropertyChanged(nameof(MinValue));
                OnPropertyChanged(nameof(MaxValue));
                OnPropertyChanged(nameof(MinTemp));
                OnPropertyChanged(nameof(MaxTemp));
                OnPropertyChanged(nameof(HasJacket));
                OnPropertyChanged(nameof(ActiveColor));
                OnPropertyChanged(nameof(InactiveColor));
                OnPropertyChanged(nameof(Format));
                UpdateState();
            }
        }

        public override void Dispose()
        {
            if (_themeService != null)
            {
                _themeService.PropertyChanged -= OnThemePropertyChanged;
            }
            base.Dispose();
        }
    }
}
