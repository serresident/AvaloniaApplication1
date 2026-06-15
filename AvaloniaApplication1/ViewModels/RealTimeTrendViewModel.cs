using System;
using System.Collections.ObjectModel;
using AvaloniaApplication1.Models.Config;
using AvaloniaApplication1.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using Avalonia.Threading;

namespace AvaloniaApplication1.ViewModels
{
    public partial class RealTimeTrendViewModel : WidgetViewModelBase
    {
        [ObservableProperty]
        private string _latestValueDisplay = "---";

        public ObservableCollection<double> Values { get; } = new();

        public double MinY => OriginalConfig.MinValue;
        public double MaxY => OriginalConfig.MaxValue;
        public string Format => string.IsNullOrEmpty(OriginalConfig.Format) ? "{0}" : OriginalConfig.Format;

        public RealTimeTrendViewModel(WidgetConfig config, IMockDataService dataService, IProjectContextService projectContext) 
            : base(config, dataService, projectContext)
        {
            DataService.TagValueChanged += OnTagValueChanged;
            
            // Pre-fill history queue
            double initialVal = 0.0;
            if (Source != null)
            {
                var val = DataService.GetCurrentValue(Source.ConnId, Source.Address);
                if (val != null)
                {
                    TryConvertDouble(val, out initialVal);
                }
            }

            for (int i = 0; i < 100; i++)
            {
                Values.Add(initialVal);
            }
            try
            {
                LatestValueDisplay = string.Format(Format, initialVal);
            }
            catch
            {
                LatestValueDisplay = initialVal.ToString("F2");
            }
        }

        private void OnTagValueChanged(object? sender, (string ConnId, string Address, object Value) e)
        {
            if (Source != null && e.ConnId == Source.ConnId && e.Address == Source.Address)
            {
                Dispatcher.UIThread.Post(() => AddValue(e.Value));
            }
        }

        private void AddValue(object valObj)
        {
            if (TryConvertDouble(valObj, out double dVal))
            {
                if (Values.Count > 0)
                {
                    Values.RemoveAt(0);
                }
                Values.Add(dVal);
                try
                {
                    LatestValueDisplay = string.Format(Format, dVal);
                }
                catch
                {
                    LatestValueDisplay = dVal.ToString("F2");
                }
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
