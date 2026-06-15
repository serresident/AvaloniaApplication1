using System;
using System.Globalization;
using Avalonia.Data.Converters;

namespace AvaloniaApplication1.Converters
{
    /// <summary>
    /// Converter that compares a string value to a target and returns true if equal.
    /// Used as static instances for XAML binding to valve type visibility.
    /// </summary>
    public class StringEqualsConverter : IValueConverter
    {
        public string TargetValue { get; set; } = string.Empty;

        /// <summary>Pre-configured instance for CutOff valve type.</summary>
        public static readonly StringEqualsConverter CutOff = new() { TargetValue = "CutOff" };

        /// <summary>Pre-configured instance for Regulating valve type.</summary>
        public static readonly StringEqualsConverter Regulating = new() { TargetValue = "Regulating" };

        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is string str)
            {
                return string.Equals(str, TargetValue, StringComparison.OrdinalIgnoreCase);
            }
            return false;
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }
}
