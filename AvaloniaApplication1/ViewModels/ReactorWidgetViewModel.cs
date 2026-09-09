using System;
using System.Globalization;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using AvaloniaApplication1.Models.Config;
using AvaloniaApplication1.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ReactiveUI;

namespace AvaloniaApplication1.ViewModels
{
    public partial class ReactorWidgetViewModel : WidgetViewModelBase
    {
        public ReactorConfig TypedConfig => (ReactorConfig)OriginalConfig;

        [ObservableProperty]
        private double _currentLevel;

        [ObservableProperty]
        private double _temperature = 20.0;

        [ObservableProperty]
        private bool _isAgitatorRunning;

        [ObservableProperty]
        private string _displayValue = "---";

        public double MinValue => TypedConfig.MinValue;
        public double MaxValue => TypedConfig.MaxValue == 0 ? 100 : TypedConfig.MaxValue;
        public bool HasJacket => TypedConfig.HasJacket;
        public string ActiveColor => string.IsNullOrEmpty(TypedConfig.ActiveColor) ? "#00FF00" : TypedConfig.ActiveColor;
        public string InactiveColor => string.IsNullOrEmpty(TypedConfig.InactiveColor) ? "#555555" : TypedConfig.InactiveColor;
        public string Format => string.IsNullOrEmpty(TypedConfig.Format) ? "{0:F1} %" : TypedConfig.Format;

        public ReactorWidgetViewModel(ReactorConfig config, IDataCoreService dataService, IProjectContextService projectContext)
            : base(config, dataService, projectContext)
        {
            // Rx-подписка на статус мешалки (AgitatorSource)
            if (config.AgitatorSource != null && !string.IsNullOrEmpty(config.AgitatorSource.ConnId) && !string.IsNullOrEmpty(config.AgitatorSource.Address))
            {
                var agSource = config.AgitatorSource;
                var initialAg = DataService.GetCurrentValue(agSource.ConnId, agSource.Address);
                if (initialAg != null)
                {
                    IsAgitatorRunning = TagValueConverter.ToBool(initialAg);
                }

                DataService.TagUpdates
                    .Where(tag => tag.ConnId == agSource.ConnId && tag.Address == agSource.Address)
                    .Sample(TimeSpan.FromMilliseconds(100))
                    .ObserveOn(RxApp.MainThreadScheduler)
                    .Subscribe(tag => IsAgitatorRunning = TagValueConverter.ToBool(tag.Value))
                    .DisposeWith(Disposables);
            }

            // Rx-подписка на температуру реактора (TempSource)
            if (config.TempSource != null && !string.IsNullOrEmpty(config.TempSource.ConnId) && !string.IsNullOrEmpty(config.TempSource.Address))
            {
                var tempSource = config.TempSource;
                var initialTemp = DataService.GetCurrentValue(tempSource.ConnId, tempSource.Address);
                if (initialTemp != null)
                {
                    Temperature = TagValueConverter.ToDouble(initialTemp);
                }

                DataService.TagUpdates
                    .Where(tag => tag.ConnId == tempSource.ConnId && tag.Address == tempSource.Address)
                    .Sample(TimeSpan.FromMilliseconds(100))
                    .ObserveOn(RxApp.MainThreadScheduler)
                    .Subscribe(tag => Temperature = TagValueConverter.ToDouble(tag.Value))
                    .DisposeWith(Disposables);
            }

            UpdateLevel();
        }

        protected override void OnTagValueUpdated(object newValue)
        {
            UpdateLevel();
        }

        private void UpdateLevel()
        {
            if (Source == null) return;
            var val = DataService.GetCurrentValue(Source.ConnId, Source.Address);
            if (val != null)
            {
                double dVal = TagValueConverter.ToDouble(val);
                CurrentLevel = Math.Clamp(dVal, MinValue, MaxValue);
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
                DisplayValue = "---";
            }
        }

        [RelayCommand]
        private void ToggleAgitator()
        {
            if (ProjectContext.IsDesignMode) return;

            if (TypedConfig.AgitatorSource != null && 
                !string.IsNullOrEmpty(TypedConfig.AgitatorSource.ConnId) && 
                !string.IsNullOrEmpty(TypedConfig.AgitatorSource.Address))
            {
                bool nextState = !IsAgitatorRunning;
                DataService.WriteCommand(TypedConfig.AgitatorSource.ConnId, TypedConfig.AgitatorSource.Address, nextState);
            }
        }
    }
}
