using System;
using System.Collections.Generic;
using Xamlity.Properties;

namespace Xamlity.Styling
{
    // One instance owns one style or template's trigger layers on an element.
    internal sealed class TriggerInstance
    {
        private readonly DependencyObject owner;
        private readonly Dictionary<DependencyProperty, List<Condition>> conditions =
            new Dictionary<DependencyProperty, List<Condition>>();
        private readonly List<Slot> slots = new List<Slot>();
        private bool detached;

        internal TriggerInstance(DependencyObject owner, IEnumerable<TriggerBase> triggers, ValueLayer layer,
            Dictionary<string, DependencyObject> names = null)
        {
            this.owner = owner;
            foreach (Trigger trigger in triggers)
            {
                var condition = new Condition { Trigger = trigger };
                if (!conditions.TryGetValue(trigger.Property, out var group))
                {
                    group = new List<Condition>();
                    conditions.Add(trigger.Property, group);
                }
                group.Add(condition);
                foreach (Setter setter in trigger.Setters)
                {
                    DependencyObject target = owner;
                    ValueLayer targetLayer = layer;
                    if (setter.TargetName != null)
                    {
                        if (names == null || !names.TryGetValue(setter.TargetName, out target))
                            throw new InvalidOperationException($"Cannot find the Trigger target '{setter.TargetName}'.");
                        targetLayer = ValueLayer.ParentTemplateTrigger;
                    }
                    Slot slot = slots.Find(item => ReferenceEquals(item.Target, target) &&
                        item.Property == setter.Property && item.Layer == targetLayer);
                    if (slot == null)
                    {
                        slot = new Slot { Target = target, Property = setter.Property, Layer = targetLayer };
                        slots.Add(slot);
                    }
                    slot.Values.Add((condition, setter.Value));
                    if (!condition.Slots.Contains(slot))
                        condition.Slots.Add(slot);
                }
            }
        }

        internal void Start()
        {
            foreach (var group in conditions.Values)
                foreach (Condition condition in group)
                    condition.Active = Equals(owner.GetValue(condition.Trigger.Property), condition.Trigger.Value);
            for (int i = 0; i < slots.Count && !detached; i++)
                slots[i].Update();
        }

        internal void OnPropertyChanged(DependencyProperty property)
        {
            if (detached || !conditions.TryGetValue(property, out var group))
                return;
            foreach (Condition condition in group)
                condition.Active = Equals(owner.GetValue(property), condition.Trigger.Value);
            foreach (Condition condition in group)
                foreach (Slot slot in condition.Slots)
                {
                    if (detached)
                        return;
                    slot.Update();
                }
        }

        internal void Detach()
        {
            detached = true;
            foreach (Slot slot in slots)
                slot.Target.ClearLayerValue(slot.Property, slot.Layer);
        }

        private sealed class Condition
        {
            internal Trigger Trigger;
            internal bool Active;
            internal readonly List<Slot> Slots = new List<Slot>();
        }

        private sealed class Slot
        {
            internal DependencyObject Target;
            internal DependencyProperty Property;
            internal ValueLayer Layer;
            internal readonly List<(Condition Condition, object Value)> Values =
                new List<(Condition, object)>();
            private object appliedValue = DependencyProperty.UnsetValue;

            internal void Update()
            {
                object value = DependencyProperty.UnsetValue;
                for (int i = Values.Count - 1; i >= 0; i--)
                    if (Values[i].Condition.Active)
                    {
                        value = Values[i].Value;
                        break;
                    }
                if (ReferenceEquals(value, appliedValue))
                    return;
                appliedValue = value;
                if (ReferenceEquals(value, DependencyProperty.UnsetValue))
                    Target.ClearLayerValue(Property, Layer);
                else
                    Target.SetLayerValue(Property, Layer, value);
            }
        }
    }
}
