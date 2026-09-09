using System;
using System.Globalization;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using AvaloniaApplication1.Models.Config;
using AvaloniaApplication1.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using ReactiveUI;

namespace AvaloniaApplication1.ViewModels
{
    public partial class HeatExchangerWidgetViewModel : WidgetViewModelBase
    {
        public HeatExchangerConfig TypedConfig => (HeatExchangerConfig)OriginalConfig;

        [ObservableProperty]
        private double _primaryTemp;

        [ObservableProperty]
        private double _secondaryTemp;

        [ObservableProperty]
        private string _displayValue = "---";

        public string ExchangerType => TypedConfig.ExchangerType ?? "ShellAndTube";
        public string ActiveColor => string.IsNullOrEmpty(TypedConfig.ActiveColor) ? "#00FFCC" : TypedConfig.ActiveColor;
        public string InactiveColor => string.IsNullOrEmpty(TypedConfig.InactiveColor) ? "#777777" : TypedConfig.InactiveColor;
        public bool ShowFlanges => TypedConfig.ShowFlanges;
        public string Format => string.IsNullOrEmpty(TypedConfig.Format) ? "{0:F1} °C" : TypedConfig.Format;

        public HeatExchangerWidgetViewModel(HeatExchangerConfig config, IDataCoreService dataService, IProjectContextService projectContext)
            : base(config, dataService, projectContext)
        {
            // Rx-подписка на второй источник данных (вторичный контур)
            if (config.SecondarySource != null && !string.IsNullOrEmpty(config.SecondarySource.ConnId) && !string.IsNullOrEmpty(config.SecondarySource.Address))
            {
                var secSource = config.SecondarySource;
                var initialVal = DataService.GetCurrentValue(secSource.ConnId, secSource.Address);
                if (initialVal != null && TagValueConverter.ToDouble(initialVal) is double d)
                {
                    SecondaryTemp = d;
                }

                DataService.TagUpdates
                    .Where(tag => tag.ConnId == secSource.ConnId && tag.Address == secSource.Address)
                    .Sample(TimeSpan.FromMilliseconds(100))
                    .ObserveOn(RxApp.MainThreadScheduler)
                    .Subscribe(tag =>
                    {
                        SecondaryTemp = TagValueConverter.ToDouble(tag.Value);
                        UpdateDisplay();
                    })
                    .DisposeWith(Disposables);
            }

            UpdatePrimary();
        }

        protected override void OnTagValueUpdated(object newValue)
        {
            UpdatePrimary();
        }

        private void UpdatePrimary()
        {
            if (Source == null) return;
            var val = DataService.GetCurrentValue(Source.ConnId, Source.Address);
            if (val != null)
            {
                PrimaryTemp = TagValueConverter.ToDouble(val);
                UpdateDisplay();
            }
            else
            {
                DisplayValue = "---";
            }
        }

        private void UpdateDisplay()
        {
            try
            {
                DisplayValue = string.Format(CultureInfo.InvariantCulture, Format, PrimaryTemp);
            }
            catch
            {
                DisplayValue = $"{PrimaryTemp.ToString("F1", CultureInfo.InvariantCulture)} °C";
            }
        }
    }
}
