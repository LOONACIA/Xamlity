using System;
using System.Globalization;
using Xamlity.Data;

namespace Xamlity.Samples.Binding
{
    public sealed class RatioToWidthConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return (double)value * double.Parse((string)parameter, CultureInfo.InvariantCulture);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotSupportedException("RatioToWidthConverter only supports one-way conversion.");
        }
    }
}
