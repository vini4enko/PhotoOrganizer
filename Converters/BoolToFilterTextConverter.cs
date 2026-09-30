using System;
using System.Globalization;
using Avalonia.Data.Converters;

namespace ExifApp.Converters;

public class BoolToFilterTextConverter : IValueConverter
{
    public static readonly BoolToFilterTextConverter Instance = new();

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value is true
            ? "(показаны главные теги)"
            : "(показаны все теги)";
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotImplementedException();
}