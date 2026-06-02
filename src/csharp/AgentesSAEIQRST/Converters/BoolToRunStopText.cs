using System;
using System.Globalization;
using Avalonia.Data.Converters;

namespace AgentesSAEIQRST.Converters
{
    public class BoolToRunStopText : IValueConverter
    {
        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
            => (value is bool b && b) ? "Stop" : "Run";

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }
}
