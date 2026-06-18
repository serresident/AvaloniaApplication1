using System;
using System.Threading.Tasks;
using AvaloniaApplication1.Models.Config;
using AvaloniaApplication1.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Avalonia.Threading;

namespace AvaloniaApplication1.ViewModels
{
    public partial class SetValueViewModel : WidgetViewModelBase
    {
        public SetValueConfig TypedConfig => (SetValueConfig)OriginalConfig;

        [ObservableProperty]
        private string _displayValue = "---";

        [ObservableProperty]
        private double _rawValue;

        public double MinValue => TypedConfig.MinValue;
        public double MaxValue => TypedConfig.MaxValue;
        public string Format => string.IsNullOrEmpty(TypedConfig.Format) ? "{0}" : TypedConfig.Format;
        public string ValueColor => string.IsNullOrEmpty(TypedConfig.ValueColor) ? "#FFD700" : TypedConfig.ValueColor;
        public double ValueFontSize => TypedConfig.ValueFontSize <= 0 ? 22 : TypedConfig.ValueFontSize;

        public SetValueViewModel(SetValueConfig config, IDataCoreService dataService, IProjectContextService projectContext) 
            : base(config, dataService, projectContext)
        {
            UpdateDisplayValue();
        }

        protected override void OnTagValueUpdated(object newValue)
        {
            UpdateDisplayValue();
        }

        private void UpdateDisplayValue()
        {
            if (Source == null) return;
            var val = DataService.GetCurrentValue(Source.ConnId, Source.Address);
            if (val != null && TryConvertDouble(val, out double dVal))
            {
                RawValue = dVal;
                try
                {
                    DisplayValue = string.Format(Format, RawValue);
                }
                catch
                {
                    DisplayValue = RawValue.ToString("F2");
                }
            }
            else
            {
                DisplayValue = "---";
            }
        }

        [RelayCommand]
        private async Task InputSetpointAsync()
        {
            if (ProjectContext.IsDesignMode) return;

            if (DialogService == null) return;

            // Estimate coordinates (bottom right of the caller widget)
            double x = (Col * 150) + 100;
            double y = (Row * 150) + 100;

            var result = await DialogService.ShowNumpadAsync(Title, RawValue.ToString("G"), x, y);
            if (result != null && double.TryParse(result, out double parsedVal))
            {
                // Clamp the value to configured Min/Max
                parsedVal = Math.Clamp(parsedVal, MinValue, MaxValue);

                // Write the new value (convert to appropriate type based on configuration)
                object valueToWrite = parsedVal;
                if (Source != null)
                {
                    if (Source.DataType == "Float32" || Source.DataType == "Float")
                        valueToWrite = (float)parsedVal;
                    else if (Source.DataType == "Int16")
                        valueToWrite = (short)parsedVal;
                    else if (Source.DataType == "UInt16")
                        valueToWrite = (ushort)parsedVal;
                    else if (Source.DataType == "Int32")
                        valueToWrite = (int)parsedVal;
                    else if (Source.DataType == "UInt32")
                        valueToWrite = (uint)parsedVal;
                    else if (Source.DataType == "Double")
                        valueToWrite = parsedVal;
                    else if (Source.DataType == "Bool")
                        valueToWrite = parsedVal != 0;

                    DataService.WriteCommand(Source.ConnId, Source.Address, valueToWrite);
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
            base.Dispose();
        }
    }
}
