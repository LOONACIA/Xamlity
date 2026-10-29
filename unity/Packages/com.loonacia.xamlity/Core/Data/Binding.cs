using System;

namespace Xamlity.Data
{
    public sealed class Binding
    {
        private BindingMode mode = BindingMode.Default;

        public Binding() { }
        public Binding(string path) { Path = new PropertyPath(path); }

        public PropertyPath Path { get; set; }
        public BindingMode Mode
        {
            get => mode;
            set
            {
                if (value != BindingMode.OneWay && value != BindingMode.Default)
                    throw new ArgumentException("The binding mode is not supported.", nameof(value));
                mode = value;
            }
        }
    }
}
