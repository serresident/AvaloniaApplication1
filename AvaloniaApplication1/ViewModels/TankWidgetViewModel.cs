using System;
using AvaloniaApplication1.Models.Config;
using AvaloniaApplication1.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using Avalonia.Threading;

namespace AvaloniaApplication1.ViewModels
{
    public partial class TankWidgetViewModel : WidgetViewModelBase
    {
        public TankConfig TypedConfig => (TankConfig)OriginalConfig;

        [ObservableProperty]
        private double _currentLevel;

        [ObservableProperty]
        private string _displayValue = "---";

        public double MinValue => TypedConfig.MinValue;
        public double MaxValue => TypedConfig.MaxValue == 0 ? 100 : TypedConfig.MaxValue;
        public string Format => string.IsNullOrEmpty(TypedConfig.Format) ? "{0:F1} %" : TypedConfig.Format;
        public string ValueColor => string.IsNullOrEmpty(TypedConfig.ValueColor) ? "#E5C158" : TypedConfig.ValueColor;
        public double ValueFontSize => TypedConfig.ValueFontSize <= 0 ? 14 : TypedConfig.ValueFontSize;

        public TankWidgetViewModel(TankConfig config, IDataCoreService dataService, IProjectContextService projectContext) 
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
            if (val != null && TryConvertDouble(val, out double dVal))
            {
                // Clamp
                CurrentLevel = Math.Clamp(dVal, MinValue, MaxValue);
                try
                {
                    DisplayValue = string.Format(Format, CurrentLevel);
                }
                catch
                {
                    DisplayValue = $"{CurrentLevel:F1}";
                }
            }
            else
            {
                DisplayValue = "---";
            }
        }

        private static bool TryConvertDouble(object valObj, out double result)
        {
            if (valObj is float f) { result = f; return true; }
            if (valObj is double d) { result = d; return true; }
            if (valObj is int i) { result = i; return true; }
            if (valObj is short s) { result = s; return true; }
            if (valObj is ushort us) { result = us; return true; }
            if (valObj is uint ui) { result = ui; return true; }
            return double.TryParse(valObj.ToString(), out result);
        }

        public override void Dispose()
        {
            base.Dispose();
        }
    }
}
