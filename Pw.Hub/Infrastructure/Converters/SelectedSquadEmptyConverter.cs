using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace Pw.Hub.Infrastructure.Converters;

public class SelectedSquadEmptyConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        try
        {
            if (values == null || values.Length < 3) return Visibility.Collapsed;
            var currentItem = values[0]; // DataContext of template (Squad)
            var selectedItem = values[1]; // TreeView.SelectedItem
            var accountsCountObj = values[2]; // Accounts.Count

            var isSelectedSame = ReferenceEquals(currentItem, selectedItem);
            if (!isSelectedSame) return Visibility.Collapsed;

            int count = 0;
            if (accountsCountObj is int i) count = i;
            else if (accountsCountObj is string s && int.TryParse(s, out var parsed)) count = parsed;

            return count == 0 ? Visibility.Visible : Visibility.Collapsed;
        }
        catch
        {
            return Visibility.Collapsed;
        }
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
