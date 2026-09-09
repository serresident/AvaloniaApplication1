using System;
using System.Globalization;
using AvaloniaApplication1.Models.Config;
using AvaloniaApplication1.Services;
using CommunityToolkit.Mvvm.ComponentModel;

namespace AvaloniaApplication1.ViewModels
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

        public string TagNumber => TypedConfig.TagNumber ?? "LT-101";
        public string SensorType => TypedConfig.SensorType ?? "Radar";
        public string Unit => TypedConfig.Unit ?? "%";
        public double MinValue => TypedConfig.MinValue;
        public double MaxValue => TypedConfig.MaxValue == 0 ? 100 : TypedConfig.MaxValue;
        public double AlarmHigh => TypedConfig.AlarmHigh == 0 ? 90 : TypedConfig.AlarmHigh;
        public double AlarmLow => TypedConfig.AlarmLow;
        public string Format => string.IsNullOrEmpty(TypedConfig.Format) ? "{0:F1}" : TypedConfig.Format;
        public string ValueColor => string.IsNullOrEmpty(TypedConfig.ValueColor) ? "#FFFFFF" : TypedConfig.ValueColor;

        public LevelSensorWidgetViewModel(LevelSensorConfig config, IDataCoreService dataService, IProjectContextService projectContext)
            : base(config, dataService, projectContext)
        {
            UpdateState();
        }

        protected override void OnTagValueUpdated(object newValue)
        {
            UpdateState();
        }

        private void UpdateState()
        {
            if (Source == null) return;
            var val = DataService.GetCurrentValue(Source.ConnId, Source.Address);
            if (val != null)
            {
                CurrentValue = TagValueConverter.ToDouble(val);
                IsAlarm = CurrentValue >= AlarmHigh || CurrentValue <= AlarmLow;
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
                DisplayValue = "---";
            }
        }
    }
}
