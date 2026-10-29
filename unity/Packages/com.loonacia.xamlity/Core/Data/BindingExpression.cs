using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Reflection;
using Xamlity.Diagnostics;
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
        private bool updatingSource;
        private PropertyInfo sourceProperty;

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
                    if (attached && !updatingSource && ReferenceEquals(sender, sources[index]) &&
                        (string.IsNullOrEmpty(e.PropertyName) || e.PropertyName == segments[index]))
                        Evaluate(index, sources[index]);
                };
            }
        }

        public Binding ParentBinding { get; }
        public DependencyObject Target { get; }
        public DependencyProperty TargetProperty { get; }

        internal override object GetValue() => value;
        internal override bool TrySetValue(object newValue)
        {
            if (ParentBinding.Mode != BindingMode.TwoWay)
                return false;

            object oldValue = value;
            value = newValue;
            Target.OnExpressionValueChanged(TargetProperty, this, oldValue);
            if (!attached || updatingSource || sourceProperty == null)
                return true;

            object source = sources[sources.Length - 1];
            PropertyInfo property = sourceProperty;
            if (property.GetSetMethod() == null)
            {
                XamlityLog.Warning($"Binding source error: property '{property.Name}' on '{source.GetType().FullName}' is read-only. {Location}");
                return true;
            }

            object converted = ParentBinding.Converter == null
                ? ConvertValue(newValue, property.PropertyType)
                : ValidateConverterResult(ParentBinding.Converter.ConvertBack(newValue, property.PropertyType,
                    ParentBinding.ConverterParameter, CultureInfo.InvariantCulture), property.PropertyType);
            if (ReferenceEquals(converted, DependencyProperty.UnsetValue))
                return true;

            updatingSource = true;
            try
            {
                property.SetValue(source, converted);
            }
            finally
            {
                updatingSource = false;
            }
            if (attached)
                Evaluate(0, contextElement?.DataContext);
            return true;
        }

        private string Location => $"Path '{ParentBinding.Path?.Path}', target '{Target.GetType().Name}.{TargetProperty.Name}'.";

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
            if (!updatingSource)
                Evaluate(0, contextElement?.DataContext);
        }

        private void ContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (attached && !updatingSource)
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
            sourceProperty = null;
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
                    XamlityLog.Warning($"Binding path error: property '{segments[i]}' not found on '{result.GetType().FullName}'. {Location}");
                    result = DependencyProperty.UnsetValue;
                    break;
                }
                try
                {
                    result = property.GetValue(result);
                    if (i == segments.Length - 1)
                        sourceProperty = property;
                }
                catch (TargetInvocationException)
                {
                    result = DependencyProperty.UnsetValue;
                    break;
                }
            }

            object oldValue = value;
            value = ReferenceEquals(result, DependencyProperty.UnsetValue) ? result
                : ParentBinding.Converter == null ? ConvertValue(result, TargetProperty.PropertyType)
                : ValidateConverterResult(ParentBinding.Converter.Convert(result, TargetProperty.PropertyType,
                    ParentBinding.ConverterParameter, CultureInfo.InvariantCulture), TargetProperty.PropertyType);
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

        private object ValidateConverterResult(object result, Type targetType)
        {
            if (ReferenceEquals(result, DependencyProperty.UnsetValue) || IsCompatible(result, targetType))
                return result;
            return ConversionError(result, targetType);
        }

        private static bool IsCompatible(object value, Type type) => value == null
            ? !type.IsValueType || Nullable.GetUnderlyingType(type) != null : type.IsInstanceOfType(value);

        private object ConversionError(object source, Type targetType)
        {
            XamlityLog.Warning($"Binding conversion error: cannot convert '{source ?? "null"}' ({source?.GetType().FullName ?? "null"}) to '{targetType.FullName}'. {Location}");
            return DependencyProperty.UnsetValue;
        }

        private object ConvertValue(object source, Type targetType)
        {
            if (IsCompatible(source, targetType))
                return source;
            if (source == null)
                return ConversionError(source, targetType);
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
            return ConversionError(source, targetType);
        }
    }
}
