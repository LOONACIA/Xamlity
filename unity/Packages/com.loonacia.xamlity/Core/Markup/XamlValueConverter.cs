using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using Xamlity.Layout;
using Xamlity.Properties;
using Xamlity.Rendering;
using Xamlity.Tree;

namespace Xamlity.Markup
{
    internal static class XamlValueConverter
    {
        internal static bool TryConvert(string text, Type targetType, DependencyProperty dp,
            out object value, out string error)
        {
            value = null;
            error = null;
            if (text.TrimStart().StartsWith("{", StringComparison.Ordinal))
            {
                error = "Markup extensions are not supported.";
                return false;
            }

            if (targetType == typeof(string))
                value = text;
            else if (targetType == typeof(double))
            {
                if ((dp == FrameworkElement.WidthProperty || dp == FrameworkElement.HeightProperty) &&
                    string.Equals(text.Trim(), "Auto", StringComparison.OrdinalIgnoreCase))
                    value = double.NaN;
                else if (TryDouble(text, out double number))
                    value = number;
            }
            else if (targetType == typeof(int))
            {
                if (int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int number))
                    value = number;
            }
            else if (targetType == typeof(bool))
            {
                if (bool.TryParse(text, out bool boolean))
                    value = boolean;
            }
            else if (targetType.IsEnum)
            {
                try
                {
                    value = new EnumConverter(targetType).ConvertFromInvariantString(text);
                }
                catch (FormatException)
                {
                    // EnumConverter wraps invalid names and numeric overflow in FormatException.
                }
            }
            else if (targetType == typeof(Thickness))
            {
                if (TryThickness(text, out Thickness thickness))
                    value = thickness;
            }
            else if (targetType == typeof(Brush))
            {
                if (TryColor(text.Trim(), out Color color))
                    value = new SolidColorBrush(color);
            }
            else if (targetType == typeof(Color))
            {
                if (TryColor(text.Trim(), out Color color))
                    value = color;
            }
            else
            {
                error = $"Cannot convert text to type '{targetType.FullName}'.";
                return false;
            }

            if (value != null)
                return true;
            error = $"'{text}' is not a valid {targetType.Name} value.";
            return false;
        }

        private static bool TryDouble(string text, out double value) =>
            double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value);

        private static bool TryThickness(string text, out Thickness thickness)
        {
            thickness = default;
            var values = new List<double>(4);
            foreach (string group in text.Split(','))
            {
                string[] tokens = group.Split((char[])null, StringSplitOptions.RemoveEmptyEntries);
                if (tokens.Length == 0)
                    return false;
                foreach (string token in tokens)
                {
                    if (values.Count == 4 || !TryDouble(token, out double number))
                        return false;
                    values.Add(number);
                }
            }
            if (values.Count == 1)
                thickness = new Thickness(values[0]);
            else if (values.Count == 2)
                thickness = new Thickness(values[0], values[1], values[0], values[1]);
            else if (values.Count == 4)
                thickness = new Thickness(values[0], values[1], values[2], values[3]);
            else
                return false;
            return true;
        }

        private static bool TryColor(string text, out Color color)
        {
            color = default;
            uint argb;
            if (text.StartsWith("#", StringComparison.Ordinal))
            {
                string hex = text.Substring(1);
                if (hex.Length != 3 && hex.Length != 4 && hex.Length != 6 && hex.Length != 8)
                    return false;
                if (!uint.TryParse(hex, NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out argb))
                    return false;
                if (hex.Length == 3 || hex.Length == 4)
                {
                    uint alpha = hex.Length == 3 ? 255 : (argb >> 12) * 17;
                    argb = (alpha << 24) | (((argb >> 8) & 15) * 17 << 16) |
                        (((argb >> 4) & 15) * 17 << 8) | ((argb & 15) * 17);
                }
                else if (hex.Length == 6)
                    argb |= 0xFF000000;
            }
            else if (!XamlNamedColors.Values.TryGetValue(text, out argb))
                return false;
            color = Color.FromArgb((byte)(argb >> 24), (byte)(argb >> 16), (byte)(argb >> 8), (byte)argb);
            return true;
        }
    }
}
