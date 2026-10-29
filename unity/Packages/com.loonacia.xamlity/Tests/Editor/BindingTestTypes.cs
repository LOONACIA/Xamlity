using System;
using System.ComponentModel;
using System.Globalization;
using Xamlity.Data;

namespace Xamlity.Editor.Tests
{
    public sealed class BindingTestViewModel : INotifyPropertyChanged
    {
        private string name;

        public string Name
        {
            get => name;
            set
            {
                if (name == value)
                    return;
                name = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Name)));
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        public int SubscriberCount => PropertyChanged?.GetInvocationList().Length ?? 0;
    }

    public sealed class BindingTestConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
            ((string)value)?.ToUpperInvariant();

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
            throw new NotSupportedException();
    }
}
