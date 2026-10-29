using System;
using System.Collections.Generic;
using System.Reflection;
using Xamlity.Properties;
using Xamlity.Tree;

namespace Xamlity.Markup
{
    public static class XamlBuilder
    {
        private static readonly Dictionary<string, Type> Types = new Dictionary<string, Type>();
        private static readonly Dictionary<(Type, string), PropertyInfo> Properties =
            new Dictionary<(Type, string), PropertyInfo>();
        private static readonly Dictionary<Type, MethodInfo> CollectionAddMethods = new Dictionary<Type, MethodInfo>();

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
                Type propertyType = XamlTypeResolver.ResolveProperty(type, node.PropertyName,
                    out DependencyProperty dp, allowCollections: node.TextValue == null);
                if (propertyType == null)
                    throw new InvalidOperationException($"Unknown or read-only property '{node.PropertyName}'.");
                if (node.TextValue != null)
                {
                    if (!XamlValueConverter.TryConvert(node.TextValue, propertyType, dp, out object value, out string error))
                        throw new InvalidOperationException(error);
                    if (dp != null)
                        ((DependencyObject)instance).SetValue(dp, value);
                    else
                        GetProperty(type, node.PropertyName).SetValue(instance, value);
                }
                else
                {
                    Type collection = XamlTypeResolver.GetCollectionInterface(propertyType);
                    if (collection != null)
                    {
                        object children = GetProperty(type, node.PropertyName).GetValue(instance);
                        if (!CollectionAddMethods.TryGetValue(collection, out MethodInfo add))
                        {
                            add = collection.GetMethod("Add");
                            CollectionAddMethods.Add(collection, add);
                        }
                        foreach (XamlObjectNode child in node.Objects)
                            add.Invoke(children, new[] { BuildObject(child) });
                    }
                    else
                    {
                        if (node.Objects.Count != 1)
                            throw new InvalidOperationException("A single content property requires exactly one object.");
                        object value = BuildObject(node.Objects[0]);
                        if (dp != null)
                            ((DependencyObject)instance).SetValue(dp, value);
                        else
                            GetProperty(type, node.PropertyName).SetValue(instance, value);
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
