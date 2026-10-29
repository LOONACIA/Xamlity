using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Reflection;
using Xamlity.Properties;
using Xamlity.Tree;

namespace Xamlity.Data
{
    public sealed class BindingExpression : Expression
    {
        private static readonly Dictionary<(Type, string), PropertyInfo> Properties =
            new Dictionary<(Type, string), PropertyInfo>();

        private readonly FrameworkElement element;
        private readonly string[] segments;
        private readonly object[] sources;
        private readonly PropertyChangedEventHandler[] handlers;
        private FrameworkElement contextElement;
        private object value = DependencyProperty.UnsetValue;
        private bool attached;

        internal BindingExpression(FrameworkElement target, DependencyProperty dp, Binding binding)
        {
            element = target;
            Target = target;
            TargetProperty = dp;
            ParentBinding = binding;
            segments = binding.Path?.Segments ?? Array.Empty<string>();
            sources = new object[segments.Length];
            handlers = new PropertyChangedEventHandler[segments.Length];
            for (int i = 0; i < segments.Length; i++)
            {
                int index = i;
                handlers[i] = (sender, e) =>
                {
                    if (attached && ReferenceEquals(sender, sources[index]) &&
                        (string.IsNullOrEmpty(e.PropertyName) || e.PropertyName == segments[index]))
                        Evaluate(index, sources[index]);
                };
            }
        }

        public Binding ParentBinding { get; }
        public DependencyObject Target { get; }
        public DependencyProperty TargetProperty { get; }

        internal override object GetValue() => value;
        internal override bool TrySetValue(object newValue) => false;

        internal override void Attach()
        {
            if (TargetProperty == FrameworkElement.DataContextProperty)
                element.InheritanceParentChanged += RefreshContext;
            RefreshContext();
            attached = true;
        }

        internal override void Detach()
        {
            attached = false;
            element.InheritanceParentChanged -= RefreshContext;
            if (contextElement != null)
                contextElement.DataContextChanged -= ContextChanged;
            contextElement = null;
            UnsubscribeFrom(0);
        }

        private void RefreshContext()
        {
            if (contextElement != null)
                contextElement.DataContextChanged -= ContextChanged;
            contextElement = TargetProperty == FrameworkElement.DataContextProperty
                ? element.InheritanceParent as FrameworkElement : element;
            if (contextElement != null)
                contextElement.DataContextChanged += ContextChanged;
            Evaluate(0, contextElement?.DataContext);
        }

        private void ContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (attached)
                Evaluate(0, contextElement.DataContext);
        }

        private void UnsubscribeFrom(int start)
        {
            for (int i = start; i < sources.Length; i++)
            {
                if (sources[i] is INotifyPropertyChanged notifier)
                    notifier.PropertyChanged -= handlers[i];
                sources[i] = null;
            }
        }

        private void Evaluate(int start, object source)
        {
            UnsubscribeFrom(start);
            object result = source ?? DependencyProperty.UnsetValue;
            for (int i = start; i < segments.Length; i++)
            {
                if (result == null || ReferenceEquals(result, DependencyProperty.UnsetValue))
                {
                    result = DependencyProperty.UnsetValue;
                    break;
                }
                sources[i] = result;
                if (result is INotifyPropertyChanged notifier)
                    notifier.PropertyChanged += handlers[i];
                PropertyInfo property = FindProperty(result.GetType(), segments[i]);
                if (property == null)
                {
                    result = DependencyProperty.UnsetValue;
                    break;
                }
                try
                {
                    result = property.GetValue(result);
                }
                catch (TargetInvocationException)
                {
                    result = DependencyProperty.UnsetValue;
                    break;
                }
            }

            object oldValue = value;
            value = ReferenceEquals(result, DependencyProperty.UnsetValue) ? result : ConvertValue(result);
            if (attached)
                Target.OnExpressionValueChanged(TargetProperty, this, oldValue);
        }

        private static PropertyInfo FindProperty(Type type, string name)
        {
            if (Properties.TryGetValue((type, name), out PropertyInfo property))
                return property;
            for (Type current = type; current != null; current = current.BaseType)
            {
                foreach (PropertyInfo candidate in current.GetProperties(
                    BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
                {
                    if (candidate.Name == name && candidate.GetGetMethod() != null &&
                        candidate.GetIndexParameters().Length == 0)
                    {
                        property = candidate;
                        break;
                    }
                }
                if (property != null)
                    break;
            }
            Properties[(type, name)] = property;
            return property;
        }

        private object ConvertValue(object source)
        {
            Type targetType = TargetProperty.PropertyType;
            if (source == null)
                return !targetType.IsValueType || Nullable.GetUnderlyingType(targetType) != null
                    ? null : DependencyProperty.UnsetValue;
            if (targetType.IsInstanceOfType(source))
                return source;
            try
            {
                if (targetType == typeof(string))
                    return source is IFormattable formattable
                        ? formattable.ToString(null, CultureInfo.InvariantCulture) : source.ToString();
                if (source is IConvertible && typeof(IConvertible).IsAssignableFrom(targetType))
                    return Convert.ChangeType(source, targetType, CultureInfo.InvariantCulture);
            }
            catch (InvalidCastException) { }
            catch (FormatException) { }
            catch (OverflowException) { }
            return DependencyProperty.UnsetValue;
        }
    }
}
