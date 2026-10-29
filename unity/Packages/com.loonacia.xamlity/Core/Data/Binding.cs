using System;

namespace Xamlity.Data
{
    public sealed class Binding
    {
        private BindingMode mode = BindingMode.Default;
        private PropertyPath path;
        private IValueConverter converter;
        private object converterParameter;
        private bool isSealed;

        public Binding() { }
        public Binding(string path) { Path = new PropertyPath(path); }

        public PropertyPath Path
        {
            get => path;
            set { CheckSealed(); path = value; }
        }
        public BindingMode Mode
        {
            get => mode;
            set
            {
                CheckSealed();
                if (value != BindingMode.TwoWay && value != BindingMode.OneWay && value != BindingMode.Default)
                    throw new ArgumentException("The binding mode is not supported.", nameof(value));
                mode = value;
            }
        }

        public IValueConverter Converter
        {
            get => converter;
            set { CheckSealed(); converter = value; }
        }

        public object ConverterParameter
        {
            get => converterParameter;
            set { CheckSealed(); converterParameter = value; }
        }

        internal void Seal() => isSealed = true;

        private void CheckSealed()
        {
            if (isSealed)
                throw new InvalidOperationException("Binding cannot be changed after it has been used.");
        }
    }
}
