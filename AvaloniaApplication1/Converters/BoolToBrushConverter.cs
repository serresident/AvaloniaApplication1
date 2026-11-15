using System;
using System.Globalization;
using Avalonia;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace AvaloniaApplication1.Converters
{
    public class BoolToBrushConverter : IValueConverter
    {
        public static readonly BoolToBrushConverter Instance = new();

        public IBrush TrueBrush { get; set; } = Brushes.Red;
        public IBrush FalseBrush { get; set; } = Brushes.DarkRed;

        public string? TrueResourceKey { get; set; }
        public string? FalseResourceKey { get; set; }

        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is bool b)
            {
                if (b)
                {
                    if (!string.IsNullOrEmpty(TrueResourceKey) && Application.Current != null)
                    {
                        if (Application.Current.TryGetResource(TrueResourceKey, Application.Current.ActualThemeVariant, out var res) && res is IBrush br)
                            return br;
                    }
                    return TrueBrush;
                }
                else
                {
                    if (!string.IsNullOrEmpty(FalseResourceKey) && Application.Current != null)
                    {
                        if (Application.Current.TryGetResource(FalseResourceKey, Application.Current.ActualThemeVariant, out var res) && res is IBrush br)
                            return br;
                    }
                    return FalseBrush;
                }
            }
            return FalseBrush;
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
