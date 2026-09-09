using System;
using System.Collections.ObjectModel;
using System.Globalization;
using AvaloniaApplication1.Models.Config;
using AvaloniaApplication1.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using Avalonia.Threading;

namespace AvaloniaApplication1.ViewModels
{
    public partial class RealTimeTrendViewModel : WidgetViewModelBase
    {
        public RealTimeTrendConfig TypedConfig => (RealTimeTrendConfig)OriginalConfig;

        [ObservableProperty]
        private string _latestValueDisplay = "---";

        public ObservableCollection<double> Values { get; } = new();

        public double MinY => TypedConfig.MinValue;
        public double MaxY => TypedConfig.MaxValue;
        public string Format => string.IsNullOrEmpty(TypedConfig.Format) ? "{0}" : TypedConfig.Format;

        public RealTimeTrendViewModel(RealTimeTrendConfig config, IDataCoreService dataService, IProjectContextService projectContext) 
            : base(config, dataService, projectContext)
        {
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

        protected override void OnTagValueUpdated(object newValue)
        {
            AddValue(newValue);
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
                    LatestValueDisplay = dVal.ToString("F2", CultureInfo.InvariantCulture);
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
            return double.TryParse(valObj.ToString(), NumberStyles.Float, CultureInfo.InvariantCulture, out result);
        }

        public override void Dispose()
        {
            base.Dispose();
        }
    }
}
