using System;
using System.Globalization;
using System.Windows.Data;

namespace Pw.Hub.Infrastructure.Converters
{
    /// <summary>
    /// Converts a zero-based AlternationIndex to a subtle ordinal string like "1.".
    /// </summary>
    public class OneBasedIndexConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            try
            {
                if (value == null || value == System.Windows.DependencyProperty.UnsetValue)
                    return string.Empty;
                if (value is int i)
                {
                    var oneBased = i + 1;
                    return oneBased.ToString(culture) + ".";
                }
                if (int.TryParse(value.ToString(), out var parsed))
                {
                    return (parsed + 1).ToString(culture) + ".";
                }
            }
            catch { }
            return string.Empty;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            // Not needed
            return Binding.DoNothing;
        }
    }
}
