using System;
using System.Collections.Generic;
using System.Reflection;
using Xamlity.Data;
using Xamlity.Properties;
using Xamlity.Rendering;
using Xamlity.Styling;
using Xamlity.Tree;

namespace Xamlity.Markup
{
    public static class XamlBuilder
    {
        private static readonly Dictionary<string, Type> Types = new Dictionary<string, Type>();
        private static readonly Dictionary<(Type, string), PropertyInfo> Properties =
            new Dictionary<(Type, string), PropertyInfo>();
        private static readonly Dictionary<Type, MethodInfo> CollectionAddMethods = new Dictionary<Type, MethodInfo>();

        public static object Build(XamlDocument document) => Build(document, null);

        public static object Build(XamlDocument document, IXamlAssetResolver assetResolver)
        {
            if (document == null)
                throw new ArgumentNullException(nameof(document));
            return BuildObject(document.Root, assetResolver);
        }

        private static object BuildObject(XamlObjectNode node, IXamlAssetResolver assetResolver, Style style = null)
        {
            try
            {
                Type type = GetType(node.TypeName);
                if (type == typeof(Setter))
                    return BuildSetter(node, style, assetResolver);
                object instance = Activator.CreateInstance(type);
                if (node.Name != null)
                    ((FrameworkElement)instance).Name = node.Name;
                if (instance is Style)
                    foreach (XamlPropertyNode property in node.Properties)
                        if (property.PropertyName == nameof(Style.TargetType))
                            ApplyProperty(instance, type, property, assetResolver);
                foreach (XamlPropertyNode property in node.Properties)
                    if (!(instance is Style) || property.PropertyName != nameof(Style.TargetType))
                        ApplyProperty(instance, type, property, assetResolver);
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

        private static Setter BuildSetter(XamlObjectNode node, Style style, IXamlAssetResolver assetResolver)
        {
            try
            {
                XamlPropertyNode propertyNode = null;
                XamlPropertyNode valueNode = null;
                foreach (XamlPropertyNode property in node.Properties)
                {
                    if (property.PropertyName == nameof(Setter.Property))
                        propertyNode = property;
                    else if (property.PropertyName == nameof(Setter.Value))
                        valueNode = property;
                    else
                        throw new InvalidOperationException($"Unknown or read-only property '{property.PropertyName}'.");
                }
                if (propertyNode?.TextValue == null)
                    throw new InvalidOperationException("Setter requires a Property.");
                DependencyProperty dp = XamlTypeResolver.ResolveSetterProperty(style?.TargetType,
                    propertyNode.TextValue, out string error);
                if (error != null)
                    throw new InvalidOperationException(error);
                if (valueNode == null)
                    throw new InvalidOperationException("Setter requires a Value.");
                object value;
                if (valueNode.TextValue != null)
                    value = ConvertText(valueNode.TextValue, dp.PropertyType, dp, assetResolver);
                else
                {
                    if (valueNode.Objects.Count != 1)
                        throw new InvalidOperationException("Property 'Value' requires exactly one object.");
                    value = BuildObject(valueNode.Objects[0], assetResolver);
                }
                var setter = new Setter(dp, value);
                setter.Seal();
                return setter;
            }
            catch (Exception exception)
            {
                throw new XamlBuildException(node.Line, node.Column,
                    $"Cannot build Setter: {exception.Message}", exception);
            }
        }

        private static object ConvertText(string text, Type propertyType, DependencyProperty dp,
            IXamlAssetResolver assetResolver)
        {
            if (propertyType == typeof(ImageSource))
            {
                if (assetResolver == null)
                    throw new InvalidOperationException("ImageSource properties require an asset resolver.");
                if (!XamlValueConverter.TryConvert(text, typeof(string), null, out object path, out string pathError))
                    throw new InvalidOperationException(pathError);
                return assetResolver.ResolveImage((string)path);
            }
            if (!XamlValueConverter.TryConvert(text, propertyType, dp, out object value, out string error))
                throw new InvalidOperationException(error);
            return value;
        }

        private static void ApplyProperty(object instance, Type type, XamlPropertyNode node,
            IXamlAssetResolver assetResolver)
        {
            try
            {
                DependencyProperty dp;
                Type propertyType;
                if (node.OwnerTypeName != null)
                {
                    Type owner = GetType(node.OwnerTypeName);
                    dp = XamlTypeResolver.ResolveDependencyProperty(owner, node.PropertyName);
                    if (dp == null || !dp.IsAttached)
                        throw new InvalidOperationException($"Unknown attached property '{node.PropertyName}' on type '{owner.Name}'.");
                    propertyType = dp.PropertyType;
                }
                else
                    propertyType = XamlTypeResolver.ResolveProperty(type, node.PropertyName,
                        out dp, allowCollections: node.TextValue == null);
                if (propertyType == null)
                    throw new InvalidOperationException($"Unknown or read-only property '{node.PropertyName}'.");
                if (node.TextValue != null)
                {
                    object value = ConvertText(node.TextValue, propertyType, dp, assetResolver);
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
                            add.Invoke(children, new[] { BuildObject(child, assetResolver, instance as Style) });
                    }
                    else
                    {
                        if (node.Objects.Count != 1)
                            throw new InvalidOperationException("A single content property requires exactly one object.");
                        object value = BuildObject(node.Objects[0], assetResolver);
                        if (value is Binding binding)
                        {
                            if (!(instance is FrameworkElement element) || dp == null)
                                throw new InvalidOperationException("A 'Binding' can only be set on a dependency property of a FrameworkElement.");
                            BindingOperations.SetBinding(element, dp, binding);
                        }
                        else if (dp != null)
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

        private static Type GetType(string name)
        {
            if (!Types.TryGetValue(name, out Type type))
            {
                type = Type.GetType(name, throwOnError: true);
                Types.Add(name, type);
            }
            return type;
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
