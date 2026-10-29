using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using Xamlity.Properties;
using Xamlity.Tree;

namespace Xamlity.Markup
{
    internal static class XamlTypeResolver
    {
        internal const string DefaultNamespace = "https://github.com/loonacia/xamlity";
        internal const string XamlNamespace = "http://schemas.microsoft.com/winfx/2006/xaml";

        internal static Type Resolve(string xmlNamespace, string name, bool requireConstructor = true)
        {
            if (xmlNamespace != DefaultNamespace)
                return null;

            Assembly assembly = typeof(UIElement).Assembly;
            Type type = assembly.GetType("Xamlity.Controls." + name)
                ?? assembly.GetType("Xamlity.Layout." + name)
                ?? assembly.GetType("Xamlity.Rendering." + name)
                ?? assembly.GetType("Xamlity.Data." + name)
                ?? (requireConstructor ? null : assembly.GetType("Xamlity.Tree." + name));
            return type != null && type.IsPublic && type.IsClass && (!requireConstructor ||
                (!type.IsAbstract && type.GetConstructor(Type.EmptyTypes) != null))
                ? type : null;
        }

        internal static Type ResolveProperty(Type type, string name, out DependencyProperty dp,
            bool allowCollections = false)
        {
            dp = ResolveDependencyProperty(type, name);
            if (dp != null)
                return dp.PropertyType;

            PropertyInfo property = type.GetProperty(name, BindingFlags.Public | BindingFlags.Instance);
            return property != null && property.GetIndexParameters().Length == 0 &&
                ((property.SetMethod != null && property.SetMethod.IsPublic) ||
                (allowCollections && property.GetMethod != null && property.GetMethod.IsPublic &&
                    GetCollectionInterface(property.PropertyType) != null)) ? property.PropertyType : null;
        }

        internal static DependencyProperty ResolveDependencyProperty(Type type, string name)
        {
            // Reflection alone does not run the initializers that register dependency properties.
            for (Type current = type; current != null; current = current.BaseType)
                RuntimeHelpers.RunClassConstructor(current.TypeHandle);
            return DependencyProperty.FromName(type, name);
        }

        internal static Type GetCollectionInterface(Type type)
        {
            if (type == null)
                return null;
            if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(ICollection<>))
                return type;
            foreach (Type implemented in type.GetInterfaces())
                if (implemented.IsGenericType && implemented.GetGenericTypeDefinition() == typeof(ICollection<>))
                    return implemented;
            return null;
        }
    }
}
