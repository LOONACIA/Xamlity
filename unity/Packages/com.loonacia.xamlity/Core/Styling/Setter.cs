using System;
using Xamlity.Data;
using Xamlity.Properties;
using Xamlity.Tree;

namespace Xamlity.Styling
{
    public sealed class Setter : SetterBase
    {
        private DependencyProperty property;
        private object value;
        private string targetName;

        public Setter() { }

        public Setter(DependencyProperty property, object value)
        {
            this.property = property;
            this.value = value;
        }

        public Setter(DependencyProperty property, object value, string targetName) : this(property, value)
        {
            this.targetName = targetName;
        }

        public string TargetName
        {
            get => targetName;
            set { CheckSealed(); targetName = value; }
        }

        internal void ValidateStyleTarget()
        {
            if (targetName != null)
                throw new InvalidOperationException("'TargetName' property cannot be set on a Style Setter.");
        }

        public DependencyProperty Property
        {
            get => property;
            set
            {
                CheckSealed();
                property = value;
            }
        }

        public object Value
        {
            get => value;
            set
            {
                CheckSealed();
                this.value = value;
            }
        }

        internal override void Seal()
        {
            if (IsSealed)
                return;
            if (property == null)
                throw new InvalidOperationException("Setter.Property must not be null.");
            if (property == FrameworkElement.StyleProperty)
                throw new InvalidOperationException("Style object is not allowed to affect the Style property of the object to which it applies.");
            if (value is Expression || value is Binding || ReferenceEquals(value, DependencyProperty.UnsetValue)
                || (value == null ? property.PropertyType.IsValueType : !property.PropertyType.IsInstanceOfType(value)))
                throw new InvalidOperationException($"'{value}' is not a valid value for the '{property.Name}' property on a Setter.");
            base.Seal();
        }

        private void CheckSealed()
        {
            if (IsSealed)
                throw new InvalidOperationException("Cannot modify a sealed Setter.");
        }
    }
}
