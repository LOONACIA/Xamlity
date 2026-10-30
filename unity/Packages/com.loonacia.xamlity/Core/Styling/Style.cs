using System;
using System.Collections.Generic;
using Xamlity.Markup;
using Xamlity.Properties;

namespace Xamlity.Styling
{
    [ContentProperty(nameof(Setters))]
    public class Style
    {
        private Type targetType;
        private Style basedOn;
        private bool isSealing;

        public Style() { }
        public Style(Type targetType) : this(targetType, null) { }

        public Style(Type targetType, Style basedOn)
        {
            this.targetType = targetType;
            this.basedOn = basedOn;
        }

        public Type TargetType
        {
            get => targetType;
            set
            {
                CheckSealed();
                targetType = value;
            }
        }

        public Style BasedOn
        {
            get => basedOn;
            set
            {
                CheckSealed();
                basedOn = value;
            }
        }

        public SetterBaseCollection Setters { get; } = new SetterBaseCollection();
        public TriggerCollection Triggers { get; } = new TriggerCollection();
        internal List<Trigger> AllTriggers { get; private set; }
        public bool IsSealed { get; private set; }
        internal Dictionary<DependencyProperty, object> SetterValues { get; private set; }

        public void Seal()
        {
            if (IsSealed)
                return;
            if (isSealing)
                throw new InvalidOperationException("Style BasedOn chain contains a cycle.");
            if (targetType == null)
                throw new InvalidOperationException("Style must have a TargetType.");

            isSealing = true;
            try
            {
                if (basedOn != null)
                {
                    basedOn.Seal();
                    if (!basedOn.TargetType.IsAssignableFrom(targetType))
                        throw new InvalidOperationException($"Can only base on a Style with target type that is base type '{targetType.Name}'.");
                }
                foreach (Setter setter in Setters)
                    setter.ValidateStyleTarget();
                foreach (Trigger trigger in Triggers)
                    foreach (Setter setter in trigger.Setters)
                        setter.ValidateStyleTarget();
                Setters.Seal();
                Triggers.Seal();
                AllTriggers = basedOn == null ? new List<Trigger>() : new List<Trigger>(basedOn.AllTriggers);
                foreach (Trigger trigger in Triggers)
                    AllTriggers.Add(trigger);
                SetterValues = basedOn == null
                    ? new Dictionary<DependencyProperty, object>()
                    : new Dictionary<DependencyProperty, object>(basedOn.SetterValues);
                foreach (Setter setter in Setters)
                    SetterValues[setter.Property] = setter.Value;
                IsSealed = true;
            }
            finally
            {
                isSealing = false;
            }
        }

        private void CheckSealed()
        {
            if (IsSealed)
                throw new InvalidOperationException("Cannot modify a sealed Style.");
        }
    }
}
