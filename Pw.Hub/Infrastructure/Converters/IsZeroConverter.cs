using System;
using System.Globalization;
using System.Windows.Data;

namespace Pw.Hub.Infrastructure.Converters;

public class IsZeroConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        try
        {
            if (value == null) return false;
            // Try numeric conversions
            if (value is int i) return i == 0;
            if (value is long l) return l == 0L;
            if (value is double d) return Math.Abs(d) < double.Epsilon;
            if (value is float f) return Math.Abs(f) < float.Epsilon;
            if (value is decimal m) return m == 0m;
            // Try parse from string
            if (value is string s && int.TryParse(s, out var parsed)) return parsed == 0;
            return false;
        }
        catch
        {
            return false;
        }
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
