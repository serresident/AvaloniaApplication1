using System;
using AvaloniaApplication1.Models.Config;
using AvaloniaApplication1.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using Avalonia.Threading;

namespace AvaloniaApplication1.ViewModels
{
    public partial class PilotLightViewModel : WidgetViewModelBase
    {
        public PilotLightConfig TypedConfig => (PilotLightConfig)OriginalConfig;

        [ObservableProperty]
        private bool _isOn;

        public PilotLightViewModel(PilotLightConfig config, IDataCoreService dataService, IProjectContextService projectContext) 
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
            if (val is bool b)
                IsOn = b;
            else if (val is int i)
                IsOn = i > 0;
        }

        public override void Dispose()
        {
            base.Dispose();
        }
    }
}
