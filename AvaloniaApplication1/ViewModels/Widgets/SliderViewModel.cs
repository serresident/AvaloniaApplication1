using AvaloniaApplication1.ViewModels.Core;
using AvaloniaApplication1.Models.Config;
using AvaloniaApplication1.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using Avalonia.Threading;

namespace AvaloniaApplication1.ViewModels.Widgets
{
    public partial class SliderViewModel : WidgetViewModelBase
    {
        public SliderConfig TypedConfig => (SliderConfig)OriginalConfig;

        [ObservableProperty]
        private double _sliderValue;

        private bool _isUpdatingFromSource;

        public SliderViewModel(SliderConfig config, IDataCoreService dataService, IProjectContextService projectContext) 
            : base(config, dataService, projectContext)
        {
            UpdateFromSource();
        }

        protected override void OnTagValueUpdated(object newValue)
        {
            UpdateFromSource();
        }

        private void UpdateFromSource()
        {
            if (Source == null) return;
            var val = DataService.GetCurrentValue(Source.ConnId, Source.Address);
            
            _isUpdatingFromSource = true;
            if (val is float f)
                SliderValue = f;
            else if (val is double d)
                SliderValue = d;
            else if (val is int i)
                SliderValue = i;
            _isUpdatingFromSource = false;
        }

        partial void OnSliderValueChanged(double value)
        {
            if (ProjectContext?.IsDesignMode == true) return;

            if (!_isUpdatingFromSource && Source != null)
            {
                // Write back to mock service
                DataService.WriteCommand(Source.ConnId, Source.Address, (float)value);
            }
        }

        public double MinValue => TypedConfig.MinValue;
        public double MaxValue => TypedConfig.MaxValue == 0 ? 100 : TypedConfig.MaxValue;

        public override void UpdateFromConfig(WidgetConfig newConfig)
        {
            base.UpdateFromConfig(newConfig);
            if (newConfig is SliderConfig sliderConfig)
            {
                TypedConfig.MinValue = sliderConfig.MinValue;
                TypedConfig.MaxValue = sliderConfig.MaxValue;
                OnPropertyChanged(nameof(MinValue));
                OnPropertyChanged(nameof(MaxValue));
            }
        }

        public override void Dispose()
        {
            base.Dispose();
        }
    }
}
