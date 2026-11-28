using System;
using System.ComponentModel;
using System.Globalization;
using Avalonia.Threading;
using AvaloniaApplication1.Models;
using AvaloniaApplication1.Models.Config;
using AvaloniaApplication1.Services;
using AvaloniaApplication1.ViewModels.Core;
using CommunityToolkit.Mvvm.ComponentModel;

namespace AvaloniaApplication1.ViewModels.Widgets
{
    public partial class LevelSensorWidgetViewModel : WidgetViewModelBase
    {
        public LevelSensorConfig TypedConfig => (LevelSensorConfig)OriginalConfig;

        [ObservableProperty]
        private double _currentValue;

        [ObservableProperty]
        private string _displayValue = "---";

        [ObservableProperty]
        private bool _isAlarm;

        [ObservableProperty]
        private bool _isHighAlarm;

        [ObservableProperty]
        private bool _isLowAlarm;

        [ObservableProperty]
        private bool _isOffline;

        [ObservableProperty]
        private string _displayColor = "#FFFFFF";

        private readonly IThemeService? _themeService;

        public string TagNumber => TypedConfig.TagNumber ?? "LT-101";
        public string SensorType => TypedConfig.SensorType ?? "Radar";
        public string Unit => TypedConfig.Unit ?? "%";
        public double MinValue => TypedConfig.MinValue;
        public double MaxValue => TypedConfig.MaxValue == 0 ? 100 : TypedConfig.MaxValue;
        public double AlarmHigh => TypedConfig.AlarmHigh == 0 ? 90 : TypedConfig.AlarmHigh;
        public double AlarmLow => TypedConfig.AlarmLow;
        public string Format => string.IsNullOrEmpty(TypedConfig.Format) ? "{0:F1}" : TypedConfig.Format;
        public string ValueColor => string.IsNullOrEmpty(TypedConfig.ValueColor) ? "#FFFFFF" : TypedConfig.ValueColor;

        public LevelSensorWidgetViewModel(
            LevelSensorConfig config,
            IDataCoreService dataService,
            IProjectContextService projectContext,
            IThemeService? themeService = null)
            : base(config, dataService, projectContext)
        {
            _themeService = themeService;
            if (_themeService != null)
            {
                _themeService.PropertyChanged += OnThemePropertyChanged;
            }
            UpdateState();
        }

        private void OnThemePropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(IThemeService.CurrentTheme) || e.PropertyName == nameof(IThemeService.IsIndustrialGrayTheme))
            {
                Dispatcher.UIThread.Post(UpdateState);
            }
        }

        protected override void OnTagValueUpdated(object newValue) => UpdateState();
        protected override void OnTagSnapshotUpdated(TagSnapshot snapshot) => UpdateState();

        public void UpdateState()
        {
            IsOffline = Quality == TagQuality.Bad || Quality == TagQuality.CommFailure || Quality == TagQuality.Timeout;

            if (Source == null)
            {
                IsHighAlarm = false;
                IsLowAlarm = false;
                IsAlarm = false;
                DisplayValue = "---";
                UpdateDisplayColor();
                return;
            }

            var val = DataService.GetCurrentValue(Source.ConnId, Source.Address);
            if (val != null)
            {
                CurrentValue = TagValueConverter.ToDouble(val);
                IsHighAlarm = !IsOffline && CurrentValue >= AlarmHigh;
                IsLowAlarm = !IsOffline && CurrentValue <= AlarmLow;
                IsAlarm = IsHighAlarm || IsLowAlarm;
                try
                {
                    DisplayValue = $"{string.Format(CultureInfo.InvariantCulture, Format, CurrentValue)} {Unit}";
                }
                catch
                {
                    DisplayValue = $"{CurrentValue.ToString("F1", CultureInfo.InvariantCulture)} {Unit}";
                }
            }
            else
            {
                IsHighAlarm = false;
                IsLowAlarm = false;
                IsAlarm = false;
                DisplayValue = "---";
            }

            UpdateDisplayColor();
        }

        private void UpdateDisplayColor()
        {
            bool isGray = _themeService?.IsIndustrialGrayTheme == true ||
                          string.Equals(Avalonia.Application.Current?.ActualThemeVariant?.Key?.ToString(), "IndustrialGray", StringComparison.OrdinalIgnoreCase);

            if (IsOffline)
                DisplayColor = "#6B7280";
            else if (IsHighAlarm)
                DisplayColor = "#DC2626";
            else if (IsLowAlarm)
                DisplayColor = "#D97706";
            else
                DisplayColor = isGray ? "#334155" : ValueColor;
        }

        public override void UpdateFromConfig(WidgetConfig newConfig)
        {
            base.UpdateFromConfig(newConfig);
            if (newConfig is LevelSensorConfig lConfig)
            {
                TypedConfig.SensorType = lConfig.SensorType;
                TypedConfig.TagNumber = lConfig.TagNumber;
                TypedConfig.Unit = lConfig.Unit;
                TypedConfig.Format = lConfig.Format;
                TypedConfig.MinValue = lConfig.MinValue;
                TypedConfig.MaxValue = lConfig.MaxValue;
                TypedConfig.AlarmHigh = lConfig.AlarmHigh;
                TypedConfig.AlarmLow = lConfig.AlarmLow;
                TypedConfig.ValueColor = lConfig.ValueColor;
                OnPropertyChanged(nameof(SensorType));
                OnPropertyChanged(nameof(TagNumber));
                OnPropertyChanged(nameof(Unit));
                OnPropertyChanged(nameof(Format));
                OnPropertyChanged(nameof(MinValue));
                OnPropertyChanged(nameof(MaxValue));
                OnPropertyChanged(nameof(AlarmHigh));
                OnPropertyChanged(nameof(AlarmLow));
                OnPropertyChanged(nameof(ValueColor));
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
