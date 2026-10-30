using System;
using Xamlity.Data;
using Xamlity.Markup;
using Xamlity.Properties;

namespace Xamlity.Styling
{
    [ContentProperty(nameof(Setters))]
    public sealed class Trigger : TriggerBase
    {
        private DependencyProperty property;
        private object value;

        public DependencyProperty Property
        {
            get => property;
            set { CheckSealed(); property = value; }
        }

        public object Value
        {
            get => value;
            set { CheckSealed(); this.value = value; }
        }

        public SetterBaseCollection Setters { get; } = new SetterBaseCollection();

        internal override void Seal()
        {
            if (IsSealed)
                return;
            if (property == null)
                throw new InvalidOperationException("Trigger.Property must not be null.");
            if (value is Expression || value is Binding || ReferenceEquals(value, DependencyProperty.UnsetValue)
                || (value == null ? property.PropertyType.IsValueType : !property.PropertyType.IsInstanceOfType(value)))
                throw new InvalidOperationException($"'{value}' is not a valid value for the '{property.Name}' property on a Trigger.");
            Setters.Seal();
            base.Seal();
        }

        private void CheckSealed()
        {
            if (IsSealed)
                throw new InvalidOperationException("Cannot modify a sealed Trigger.");
        }
    }
}
