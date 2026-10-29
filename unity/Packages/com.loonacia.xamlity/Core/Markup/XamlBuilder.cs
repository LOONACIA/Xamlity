using System;
using System.Collections.Generic;
using System.Reflection;
using Xamlity.Layout;
using Xamlity.Properties;
using Xamlity.Tree;

namespace Xamlity.Markup
{
    public static class XamlBuilder
    {
        private static readonly Dictionary<string, Type> Types = new Dictionary<string, Type>();
        private static readonly Dictionary<(Type, string), PropertyInfo> Properties =
            new Dictionary<(Type, string), PropertyInfo>();

        public static object Build(XamlDocument document)
        {
            if (document == null)
                throw new ArgumentNullException(nameof(document));
            return BuildObject(document.Root);
        }

        private static object BuildObject(XamlObjectNode node)
        {
            try
            {
                if (!Types.TryGetValue(node.TypeName, out Type type))
                {
                    type = Type.GetType(node.TypeName, throwOnError: true);
                    Types.Add(node.TypeName, type);
                }
                object instance = Activator.CreateInstance(type);
                if (node.Name != null)
                    ((FrameworkElement)instance).Name = node.Name;
                foreach (XamlPropertyNode property in node.Properties)
                    ApplyProperty(instance, type, property);
                return instance;
            }
            catch (XamlBuildException)
            {
                throw;
            }
            catch (Exception exception)
            {
                throw new XamlBuildException(node.Line, node.Column,
                    $"Cannot build type '{node.TypeName}': {exception.Message}", exception);
            }
        }

        private static void ApplyProperty(object instance, Type type, XamlPropertyNode node)
        {
            try
            {
                if (node.TextValue != null)
                {
                    Type propertyType = XamlTypeResolver.ResolveProperty(type, node.PropertyName, out DependencyProperty dp);
                    if (propertyType == null)
                        throw new InvalidOperationException($"Unknown or read-only property '{node.PropertyName}'.");
                    if (!XamlValueConverter.TryConvert(node.TextValue, propertyType, dp, out object value, out string error))
                        throw new InvalidOperationException(error);
                    if (dp != null)
                        ((DependencyObject)instance).SetValue(dp, value);
                    else
                        GetProperty(type, node.PropertyName).SetValue(instance, value);
                }
                else
                {
                    PropertyInfo property = GetProperty(type, node.PropertyName);
                    if (property.PropertyType == typeof(UIElementCollection))
                    {
                        var children = (UIElementCollection)property.GetValue(instance);
                        foreach (XamlObjectNode child in node.Objects)
                            children.Add((UIElement)BuildObject(child));
                    }
                    else
                    {
                        if (node.Objects.Count != 1)
                            throw new InvalidOperationException("A single content property requires exactly one object.");
                        property.SetValue(instance, BuildObject(node.Objects[0]));
                    }
                }
            }
            catch (XamlBuildException)
            {
                throw;
            }
            catch (Exception exception)
            {
                throw new XamlBuildException(node.Line, node.Column,
                    $"Cannot set property '{node.PropertyName}': {exception.Message}", exception);
            }
        }

        private static PropertyInfo GetProperty(Type type, string name)
        {
            if (!Properties.TryGetValue((type, name), out PropertyInfo property))
            {
                property = type.GetProperty(name, BindingFlags.Public | BindingFlags.Instance)
                    ?? throw new InvalidOperationException($"Unknown property '{name}' on type '{type.FullName}'.");
                Properties.Add((type, name), property);
            }
            return property;
        }
    }
}
