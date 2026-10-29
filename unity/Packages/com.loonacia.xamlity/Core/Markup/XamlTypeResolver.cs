using System;
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

        internal static Type Resolve(string xmlNamespace, string name)
        {
            if (xmlNamespace != DefaultNamespace)
                return null;

            Assembly assembly = typeof(UIElement).Assembly;
            Type type = assembly.GetType("Xamlity.Controls." + name)
                ?? assembly.GetType("Xamlity.Layout." + name);
            return type != null && type.IsPublic && !type.IsAbstract &&
                typeof(UIElement).IsAssignableFrom(type) && type.GetConstructor(Type.EmptyTypes) != null
                ? type : null;
        }

        internal static Type ResolveProperty(Type type, string name, out DependencyProperty dp)
        {
            // Reflection alone does not run the initializers that register dependency properties.
            for (Type current = type; current != null; current = current.BaseType)
                RuntimeHelpers.RunClassConstructor(current.TypeHandle);
            dp = DependencyProperty.FromName(type, name);
            if (dp != null)
                return dp.PropertyType;

            PropertyInfo property = type.GetProperty(name, BindingFlags.Public | BindingFlags.Instance);
            return property != null && property.SetMethod != null && property.SetMethod.IsPublic &&
                property.GetIndexParameters().Length == 0 ? property.PropertyType : null;
        }
    }
}
