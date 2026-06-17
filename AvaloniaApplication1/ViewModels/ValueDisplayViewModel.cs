using System;
using AvaloniaApplication1.Models.Config;
using AvaloniaApplication1.Services;
using CommunityToolkit.Mvvm.ComponentModel;

namespace AvaloniaApplication1.ViewModels
{
    public partial class ValueDisplayViewModel : WidgetViewModelBase
    {
        [ObservableProperty]
        private string _displayValue = "---";

        public ValueDisplayViewModel(WidgetConfig config, IMockDataService dataService, IProjectContextService projectContext) 
            : base(config, dataService, projectContext)
        {
            UpdateDisplayValue();
        }

        // Rx.NET subscription automatically calls this on the MainThread
        protected override void OnTagValueUpdated(object newValue)
        {
            UpdateDisplayValue(newValue);
        }

        public string Format => string.IsNullOrEmpty(OriginalConfig.Format) ? "{0}" : OriginalConfig.Format;
        public string ValueColor => string.IsNullOrEmpty(OriginalConfig.ValueColor) ? "#00FF00" : OriginalConfig.ValueColor;
        public double ValueFontSize => OriginalConfig.ValueFontSize <= 0 ? 28 : OriginalConfig.ValueFontSize;

        private void UpdateDisplayValue(object? val = null)
        {
            if (Source == null) return;
            val ??= DataService.GetCurrentValue(Source.ConnId, Source.Address);

            if (val != null)
            {
                try
                {
                    DisplayValue = string.Format(Format, val);
                }
                catch
                {
                    DisplayValue = val.ToString() ?? "---";
                }
            }
            else
            {
                DisplayValue = "---";
            }
        }
    }
}