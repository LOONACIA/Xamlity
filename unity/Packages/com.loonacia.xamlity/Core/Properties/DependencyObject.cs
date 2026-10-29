using System;
using System.Collections.Generic;

namespace Xamlity.Properties
{
    public class DependencyObject
    {
        private readonly Dictionary<DependencyProperty, object> localValues = new Dictionary<DependencyProperty, object>();

        public object GetValue(DependencyProperty dp)
        {
            if (dp == null)
                throw new ArgumentNullException(nameof(dp));

            for (DependencyObject current = this; current != null; current = current.InheritanceParent)
            {
                if (current.localValues.TryGetValue(dp, out var value))
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
            if (value == null ? dp.PropertyType.IsValueType : !dp.PropertyType.IsInstanceOfType(value))
                throw new ArgumentException("The value does not match the property type.", nameof(value));

            object oldValue = GetValue(dp);
            localValues[dp] = value;
            NotifyPropertyChanged(dp, oldValue, value);
        }

        public void ClearValue(DependencyProperty dp)
        {
            object oldValue = GetValue(dp);
            if (localValues.Remove(dp))
                NotifyPropertyChanged(dp, oldValue, GetValue(dp));
        }

        public object ReadLocalValue(DependencyProperty dp)
        {
            if (dp == null)
                throw new ArgumentNullException(nameof(dp));
            return localValues.TryGetValue(dp, out var value) ? value : DependencyProperty.UnsetValue;
        }

        protected virtual void OnPropertyChanged(DependencyPropertyChangedEventArgs e)
        {
        }

        protected internal virtual DependencyObject InheritanceParent => null;
        protected internal virtual int InheritanceChildrenCount => 0;

        protected internal virtual DependencyObject GetInheritanceChild(int index)
        {
            throw new ArgumentOutOfRangeException(nameof(index));
        }

        protected internal void OnInheritanceParentChanged(DependencyObject oldParent)
        {
            var properties = DependencyProperty.RegisteredInheritedProperties;
            for (int i = 0; i < properties.Count; i++)
            {
                DependencyProperty dp = properties[i];
                if (localValues.ContainsKey(dp))
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
                if (!child.localValues.ContainsKey(dp))
                    child.NotifyPropertyChanged(dp, oldValue, newValue);
            }
        }
    }
}
