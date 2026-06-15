using System;
using AvaloniaApplication1.Models.Config;
using AvaloniaApplication1.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using Avalonia.Threading;

namespace AvaloniaApplication1.ViewModels
{
    public partial class ValueDisplayViewModel : WidgetViewModelBase
    {
        [ObservableProperty]
        private string _displayValue = "---";

        public ValueDisplayViewModel(WidgetConfig config, IMockDataService dataService, IProjectContextService projectContext) 
            : base(config, dataService, projectContext)
        {
            DataService.TagValueChanged += OnTagValueChanged;
            UpdateDisplayValue();
        }

        private void OnTagValueChanged(object? sender, (string ConnId, string Address, object Value) e)
        {
            if (Source != null && e.ConnId == Source.ConnId && e.Address == Source.Address)
            {
                Dispatcher.UIThread.Post(UpdateDisplayValue);
            }
        }

        public string Format => string.IsNullOrEmpty(OriginalConfig.Format) ? "{0}" : OriginalConfig.Format;

        private void UpdateDisplayValue()
        {
            if (Source == null) return;
            var val = DataService.GetCurrentValue(Source.ConnId, Source.Address);
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

        public override void Dispose()
        {
            DataService.TagValueChanged -= OnTagValueChanged;
            base.Dispose();
        }
    }
}