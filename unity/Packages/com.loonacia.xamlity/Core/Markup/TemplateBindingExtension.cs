using System;
using Xamlity.Properties;

namespace Xamlity.Markup
{
    internal sealed class TemplateBindingExtension
    {
        internal const string ContextError = "TemplateBinding can only be used within a ControlTemplate.";
        internal const string TargetError = "TemplateBinding can only be set on a dependency property.";

        internal static DependencyProperty ResolveProperty(Type targetType, string name, DependencyProperty target,
            out string error)
        {
            int separator = name.IndexOf('.');
            Type owner = separator < 0 ? targetType :
                XamlTypeResolver.Resolve(XamlTypeResolver.DefaultNamespace, name.Substring(0, separator), requireConstructor: false);
            DependencyProperty source = owner == null || name.Contains(":") ? null :
                XamlTypeResolver.ResolveDependencyProperty(owner, separator < 0 ? name : name.Substring(separator + 1));
            error = source == null
                ? $"Cannot resolve the TemplateBinding Property '{name}' on type '{targetType.Name}'."
                : !target.PropertyType.IsAssignableFrom(source.PropertyType)
                    ? $"TemplateBinding source property '{name}' of type '{source.PropertyType.Name}' cannot be assigned to property '{target.Name}' of type '{target.PropertyType.Name}'."
                    : null;
            return error == null ? source : null;
        }
    }
}
