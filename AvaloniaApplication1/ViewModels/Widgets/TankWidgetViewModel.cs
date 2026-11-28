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
    public partial class TankWidgetViewModel : WidgetViewModelBase
    {
        public TankConfig TypedConfig => (TankConfig)OriginalConfig;

        [ObservableProperty]
        private double _currentLevel;

        [ObservableProperty]
        private string _displayValue = "---";

        [ObservableProperty]
        private bool _isHighAlarm;

        [ObservableProperty]
        private bool _isLowAlarm;

        [ObservableProperty]
        private bool _isOffline;

        [ObservableProperty]
        private string _displayColor = "#E5C158";

        private readonly IThemeService? _themeService;

        public double MinValue => TypedConfig.MinValue;
        public double MaxValue => TypedConfig.MaxValue == 0 ? 100 : TypedConfig.MaxValue;
        public double AlarmHigh => TypedConfig.AlarmHigh;
        public double AlarmLow => TypedConfig.AlarmLow;
        public bool ShowAlarmMarkers => TypedConfig.ShowAlarmMarkers;
        public string VesselStyle => string.IsNullOrEmpty(TypedConfig.VesselStyle) ? "Cylindrical" : TypedConfig.VesselStyle;
        public string Format => string.IsNullOrEmpty(TypedConfig.Format) ? "{0:F1} %" : TypedConfig.Format;
        public string ValueColor => string.IsNullOrEmpty(TypedConfig.ValueColor) ? "#E5C158" : TypedConfig.ValueColor;
        public double ValueFontSize => TypedConfig.ValueFontSize <= 0 ? 14 : TypedConfig.ValueFontSize;

        public TankWidgetViewModel(
            TankConfig config,
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
                DisplayValue = "---";
                UpdateDisplayColor();
                return;
            }

            var val = DataService.GetCurrentValue(Source.ConnId, Source.Address);
            if (val != null)
            {
                double dVal = TagValueConverter.ToDouble(val);
                CurrentLevel = Math.Clamp(dVal, MinValue, MaxValue);
                IsHighAlarm = !IsOffline && CurrentLevel >= AlarmHigh;
                IsLowAlarm = !IsOffline && CurrentLevel <= AlarmLow;

                try
                {
                    DisplayValue = string.Format(CultureInfo.InvariantCulture, Format, CurrentLevel);
                }
                catch
                {
                    DisplayValue = $"{CurrentLevel.ToString("F1", CultureInfo.InvariantCulture)}";
                }
            }
            else
            {
                IsHighAlarm = false;
                IsLowAlarm = false;
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
            if (newConfig is TankConfig tankConfig)
            {
                TypedConfig.MinValue = tankConfig.MinValue;
                TypedConfig.MaxValue = tankConfig.MaxValue;
                TypedConfig.AlarmHigh = tankConfig.AlarmHigh;
                TypedConfig.AlarmLow = tankConfig.AlarmLow;
                TypedConfig.ShowAlarmMarkers = tankConfig.ShowAlarmMarkers;
                TypedConfig.VesselStyle = tankConfig.VesselStyle;
                TypedConfig.Format = tankConfig.Format;
                TypedConfig.ValueColor = tankConfig.ValueColor;
                TypedConfig.ValueFontSize = tankConfig.ValueFontSize;

                OnPropertyChanged(nameof(MinValue));
                OnPropertyChanged(nameof(MaxValue));
                OnPropertyChanged(nameof(AlarmHigh));
                OnPropertyChanged(nameof(AlarmLow));
                OnPropertyChanged(nameof(ShowAlarmMarkers));
                OnPropertyChanged(nameof(VesselStyle));
                OnPropertyChanged(nameof(Format));
                OnPropertyChanged(nameof(ValueColor));
                OnPropertyChanged(nameof(ValueFontSize));
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
