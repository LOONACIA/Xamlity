using System;
using System.Collections.Generic;
using System.IO;
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
        internal const string ClrNamespacePrefix = "clr-namespace:";
        private static readonly Dictionary<(string, string), Type> Types = new Dictionary<(string, string), Type>();

        internal static Type Resolve(string xmlNamespace, string name, bool requireConstructor = true) =>
            Resolve(xmlNamespace, name, out _, requireConstructor);

        internal static Type Resolve(string xmlNamespace, string name, out string error, bool requireConstructor = true)
        {
            error = null;
            if (xmlNamespace != null && xmlNamespace.StartsWith(ClrNamespacePrefix, StringComparison.Ordinal))
            {
                if (!TryParseClrNamespace(xmlNamespace, out string clrNamespace, out string assemblyName, out error))
                    return null;
                if (!Types.TryGetValue((xmlNamespace, name), out Type mappedType))
                {
                    try
                    {
                        mappedType = Type.GetType($"{clrNamespace}.{name}, {assemblyName}");
                    }
                    catch (Exception exception) when (exception is FileNotFoundException ||
                        exception is FileLoadException || exception is BadImageFormatException ||
                        exception is TypeLoadException || exception is ArgumentException)
                    {
                        mappedType = null;
                    }
                    if (mappedType != null)
                        Types.Add((xmlNamespace, name), mappedType);
                }
                if (mappedType == null)
                    error = $"Unknown type '{name}' in namespace '{clrNamespace}' of assembly '{assemblyName}'.";
                else if (requireConstructor && !CanCreate(mappedType))
                    error = $"Type '{mappedType.FullName}' cannot be created from XAML; it must be a public non-abstract class with a public parameterless constructor.";
                return error == null ? mappedType : null;
            }
            if (xmlNamespace != DefaultNamespace)
            {
                error = $"XML namespace '{xmlNamespace}' is not supported.";
                return null;
            }

            Assembly assembly = typeof(UIElement).Assembly;
            Type type = assembly.GetType("Xamlity.Controls." + name)
                ?? assembly.GetType("Xamlity.Layout." + name)
                ?? assembly.GetType("Xamlity.Rendering." + name)
                ?? assembly.GetType("Xamlity.Data." + name)
                ?? assembly.GetType("Xamlity.Styling." + name)
                ?? (requireConstructor ? null : assembly.GetType("Xamlity.Tree." + name));
            if (type != null && type.IsPublic && (!requireConstructor || CanCreate(type)))
                return type;
            error = $"Unknown element '{name}'.";
            return null;
        }

        private static bool CanCreate(Type type) => type.IsVisible && type.IsClass && !type.IsAbstract &&
            !type.IsGenericType && !type.ContainsGenericParameters && type.GetConstructor(Type.EmptyTypes) != null;

        internal static Type ResolveDataType(string xmlNamespace, string name, out string error)
        {
            Type type = Resolve(xmlNamespace, name, out error, requireConstructor: false);
            if (type == null || type.IsVisible)
                return type;
            error = TryParseClrNamespace(xmlNamespace, out string clrNamespace, out string assemblyName, out _)
                ? $"Unknown type '{name}' in namespace '{clrNamespace}' of assembly '{assemblyName}'."
                : $"Unknown element '{name}'.";
            return null;
        }

        internal static bool TryParseClrNamespace(string uri, out string clrNamespace, out string assemblyName,
            out string error)
        {
            clrNamespace = null;
            assemblyName = null;
            error = $"Invalid clr-namespace mapping '{uri}'.";
            if (!uri.StartsWith(ClrNamespacePrefix, StringComparison.Ordinal))
                return false;
            string[] parts = uri.Substring(ClrNamespacePrefix.Length).Split(';');
            if (string.IsNullOrWhiteSpace(parts[0]) || parts[0].IndexOfAny(new[] { '=', ',', ' ', '\t', '\r', '\n' }) >= 0)
                return false;
            if (parts.Length == 1)
            {
                error = $"clr-namespace mapping '{uri}' must specify 'assembly='.";
                return false;
            }
            if (parts.Length != 2 || !parts[1].StartsWith("assembly=", StringComparison.Ordinal))
                return false;
            string assembly = parts[1].Substring("assembly=".Length);
            if (string.IsNullOrWhiteSpace(assembly) || assembly != assembly.Trim() ||
                assembly.IndexOfAny(new[] { '=', ',', '/', '\\', ':' }) >= 0)
                return false;
            clrNamespace = parts[0];
            assemblyName = assembly;
            error = null;
            return true;
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

        internal static DependencyProperty ResolveSetterProperty(Type targetType, string name, out string error,
            bool trigger = false)
        {
            error = null;
            if (name.Contains(":"))
            {
                error = $"{(trigger ? "Trigger" : "Setter")}.Property does not support prefixed owner types.";
                return null;
            }
            int separator = name.IndexOf('.');
            Type owner = separator < 0 ? targetType :
                Resolve(DefaultNamespace, name.Substring(0, separator), requireConstructor: false);
            DependencyProperty property = owner == null ? null :
                ResolveDependencyProperty(owner, separator < 0 ? name : name.Substring(separator + 1));
            if (property == null)
                error = $"Cannot resolve the {(trigger ? "Trigger" : "Style")} Property '{name}'. Verify that the owning type is the Style's TargetType, or use Class.Property syntax to specify the Property.";
            else if (property == FrameworkElement.StyleProperty)
                error = "Style object is not allowed to affect the Style property of the object to which it applies.";
            return error == null ? property : null;
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
