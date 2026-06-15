using System;
using AvaloniaApplication1.Models.Config;
using AvaloniaApplication1.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using Avalonia.Threading;

namespace AvaloniaApplication1.ViewModels
{
    public partial class TankWidgetViewModel : WidgetViewModelBase
    {
        [ObservableProperty]
        private double _currentLevel;

        [ObservableProperty]
        private string _displayValue = "---";

        public double MinValue => OriginalConfig.MinValue;
        public double MaxValue => OriginalConfig.MaxValue == 0 ? 100 : OriginalConfig.MaxValue;
        public string Format => string.IsNullOrEmpty(OriginalConfig.Format) ? "{0:F1} %" : OriginalConfig.Format;

        public TankWidgetViewModel(WidgetConfig config, IMockDataService dataService, IProjectContextService projectContext) 
            : base(config, dataService, projectContext)
        {
            DataService.TagValueChanged += OnTagValueChanged;
            UpdateState();
        }

        private void OnTagValueChanged(object? sender, (string ConnId, string Address, object Value) e)
        {
            if (Source != null && e.ConnId == Source.ConnId && e.Address == Source.Address)
            {
                Dispatcher.UIThread.Post(UpdateState);
            }
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
            DataService.TagValueChanged -= OnTagValueChanged;
            base.Dispose();
        }
    }
}
