using System;
using System.Collections.Generic;
using System.Reflection;
using Xamlity.Properties;
using Xamlity.Rendering;
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

        private static object BuildObject(XamlObjectNode node, IXamlAssetResolver assetResolver)
        {
            try
            {
                Type type = GetType(node.TypeName);
                object instance = Activator.CreateInstance(type);
                if (node.Name != null)
                    ((FrameworkElement)instance).Name = node.Name;
                foreach (XamlPropertyNode property in node.Properties)
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
                    object value;
                    if (propertyType == typeof(ImageSource))
                    {
                        if (assetResolver == null)
                            throw new InvalidOperationException("ImageSource properties require an asset resolver.");
                        value = assetResolver.ResolveImage(node.TextValue);
                    }
                    else if (!XamlValueConverter.TryConvert(node.TextValue, propertyType, dp, out value, out string error))
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
                            add.Invoke(children, new[] { BuildObject(child, assetResolver) });
                    }
                    else
                    {
                        if (node.Objects.Count != 1)
                            throw new InvalidOperationException("A single content property requires exactly one object.");
                        object value = BuildObject(node.Objects[0], assetResolver);
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
