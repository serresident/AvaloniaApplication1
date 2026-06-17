using AvaloniaApplication1.Models.Config;
using AvaloniaApplication1.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using Avalonia.Threading;

namespace AvaloniaApplication1.ViewModels
{
    public partial class SliderViewModel : WidgetViewModelBase
    {
        [ObservableProperty]
        private double _sliderValue;

        private bool _isUpdatingFromSource;

        public SliderViewModel(WidgetConfig config, IMockDataService dataService, IProjectContextService projectContext) 
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
            if (ProjectContext.IsDesignMode) return;

            if (!_isUpdatingFromSource && Source != null)
            {
                // Write back to mock service
                DataService.WriteCommand(Source.ConnId, Source.Address, (float)value);
            }
        }

        public override void Dispose()
        {
            base.Dispose();
        }
    }
}