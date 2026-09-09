using System;
using System.Globalization;

namespace AvaloniaApplication1.Services
{
    public static class TagValueConverter
    {
        public static bool ToBool(object? value) => value switch
        {
            bool b    => b,
            int i     => i > 0,
            float f   => f > 0,
            double d  => d > 0,
            string s  => s is "1" || s.Equals("true", StringComparison.OrdinalIgnoreCase),
            _         => double.TryParse(value?.ToString(),
                             NumberStyles.Float,
                             CultureInfo.InvariantCulture,
                             out var n) && n > 0
        };

        public static double ToDouble(object? value, double fallback = 0.0) => value switch
        {
            double d  => d,
            float f   => f,
            int i     => i,
            bool b    => b ? 1.0 : 0.0,
            _         => double.TryParse(value?.ToString(),
                             NumberStyles.Float,
                             CultureInfo.InvariantCulture,
                             out var n) ? n : fallback
        };
    }
}
