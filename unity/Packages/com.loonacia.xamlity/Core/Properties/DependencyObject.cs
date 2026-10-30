using System;
using System.Collections.Generic;

namespace Xamlity.Properties
{
    public class DependencyObject
    {
        private readonly Dictionary<DependencyProperty, object> localValues = new Dictionary<DependencyProperty, object>();
        private Dictionary<DependencyProperty, object[]> layerValues;

        public object GetValue(DependencyProperty dp)
        {
            if (dp == null)
                throw new ArgumentNullException(nameof(dp));

            for (DependencyObject current = this; current != null; current = current.InheritanceParent)
            {
                if (current.localValues.TryGetValue(dp, out var value))
                    return ResolveValue(dp, value);
                if (current.TryGetLayerValue(dp, out value))
                    return value;
                if (!dp.Inherits)
                    break;
            }
            return dp.DefaultMetadata.DefaultValue;
        }

        public void SetValue(DependencyProperty dp, object value)
        {
            if (dp == null)
                throw new ArgumentNullException(nameof(dp));
            if (value is Expression)
                throw new ArgumentException("Expressions cannot be set through SetValue.", nameof(value));
            if (value == null ? dp.PropertyType.IsValueType : !dp.PropertyType.IsInstanceOfType(value))
                throw new ArgumentException("The value does not match the property type.", nameof(value));

            object oldValue = GetValue(dp);
            if (ReadLocalValue(dp) is Expression expression)
            {
                if (expression.TrySetValue(value))
                    return;
                expression.Detach();
            }
            localValues[dp] = value;
            NotifyPropertyChanged(dp, oldValue, value);
        }

        public void ClearValue(DependencyProperty dp)
        {
            object oldValue = GetValue(dp);
            if (ReadLocalValue(dp) is Expression expression)
                expression.Detach();
            if (localValues.Remove(dp))
                NotifyPropertyChanged(dp, oldValue, GetValue(dp));
        }

        public object ReadLocalValue(DependencyProperty dp)
        {
            if (dp == null)
                throw new ArgumentNullException(nameof(dp));
            return localValues.TryGetValue(dp, out var value) ? value : DependencyProperty.UnsetValue;
        }

        internal void SetLayerValue(DependencyProperty dp, ValueLayer layer, object value)
        {
            if (dp == null)
                throw new ArgumentNullException(nameof(dp));
            if (value is Expression || ReferenceEquals(value, DependencyProperty.UnsetValue))
                throw new ArgumentException("Expressions and UnsetValue cannot be stored in a value layer.", nameof(value));
            if (value == null ? dp.PropertyType.IsValueType : !dp.PropertyType.IsInstanceOfType(value))
                throw new ArgumentException("The value does not match the property type.", nameof(value));

            object oldValue = GetValue(dp);
            if (layerValues == null)
                layerValues = new Dictionary<DependencyProperty, object[]>();
            if (!layerValues.TryGetValue(dp, out var values))
            {
                values = new object[(int)ValueLayer.DefaultStyle + 1];
                for (int i = 0; i < values.Length; i++)
                    values[i] = DependencyProperty.UnsetValue;
                layerValues.Add(dp, values);
            }
            values[(int)layer] = value;
            NotifyPropertyChanged(dp, oldValue, GetValue(dp));
        }

        internal void ClearLayerValue(DependencyProperty dp, ValueLayer layer)
        {
            object oldValue = GetValue(dp);
            if (layerValues == null || !layerValues.TryGetValue(dp, out var values))
                return;
            values[(int)layer] = DependencyProperty.UnsetValue;
            if (!TryGetLayerValue(dp, out _))
                layerValues.Remove(dp);
            NotifyPropertyChanged(dp, oldValue, GetValue(dp));
        }

        private bool TryGetLayerValue(DependencyProperty dp, out object value)
        {
            if (layerValues != null && layerValues.TryGetValue(dp, out var values))
            {
                for (int i = 0; i < values.Length; i++)
                {
                    if (!ReferenceEquals(values[i], DependencyProperty.UnsetValue))
                    {
                        value = values[i];
                        return true;
                    }
                }
            }
            value = null;
            return false;
        }

        private bool HasOwnValue(DependencyProperty dp) =>
            localValues.ContainsKey(dp) || (layerValues != null && layerValues.ContainsKey(dp));

        protected virtual void OnPropertyChanged(DependencyPropertyChangedEventArgs e)
        {
        }

        internal void SetExpression(DependencyProperty dp, Expression expression)
        {
            object oldValue = GetValue(dp);
            if (ReadLocalValue(dp) is Expression previous)
                previous.Detach();
            localValues[dp] = expression;
            expression.Attach();
            NotifyPropertyChanged(dp, oldValue, GetValue(dp));
        }

        internal void OnExpressionValueChanged(DependencyProperty dp, Expression expression, object oldValue)
        {
            if (ReferenceEquals(ReadLocalValue(dp), expression))
                NotifyPropertyChanged(dp, ResolveValue(dp, oldValue), GetValue(dp));
        }

        private static object ResolveValue(DependencyProperty dp, object value)
        {
            if (value is Expression expression)
                value = expression.GetValue();
            return ReferenceEquals(value, DependencyProperty.UnsetValue) ? dp.DefaultMetadata.DefaultValue : value;
        }

        protected internal virtual DependencyObject InheritanceParent => null;
        protected internal virtual int InheritanceChildrenCount => 0;

        protected internal virtual DependencyObject GetInheritanceChild(int index)
        {
            throw new ArgumentOutOfRangeException(nameof(index));
        }

        protected internal virtual void OnInheritanceParentChanged(DependencyObject oldParent)
        {
            var properties = DependencyProperty.RegisteredInheritedProperties;
            for (int i = 0; i < properties.Count; i++)
            {
                DependencyProperty dp = properties[i];
                if (HasOwnValue(dp))
                    continue;
                object oldValue = oldParent == null ? dp.DefaultMetadata.DefaultValue : oldParent.GetValue(dp);
                NotifyPropertyChanged(dp, oldValue, GetValue(dp));
            }
        }

        private void NotifyPropertyChanged(DependencyProperty dp, object oldValue, object newValue)
        {
            if (Equals(oldValue, newValue))
                return;

            var args = new DependencyPropertyChangedEventArgs(dp, oldValue, newValue);
            dp.DefaultMetadata.PropertyChangedCallback?.Invoke(this, args);
            OnPropertyChanged(args);

            if (!dp.Inherits)
                return;
            for (int i = 0; i < InheritanceChildrenCount; i++)
            {
                DependencyObject child = GetInheritanceChild(i);
                if (!child.HasOwnValue(dp))
                    child.NotifyPropertyChanged(dp, oldValue, newValue);
            }
        }
    }
}
