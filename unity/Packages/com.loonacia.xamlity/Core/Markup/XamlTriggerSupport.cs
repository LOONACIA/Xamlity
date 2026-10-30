using System;
using System.Collections.Generic;
using Xamlity.Styling;

namespace Xamlity.Markup
{
    internal static class XamlTriggerSupport
    {
        internal static XamlPropertyNode FindProperty(XamlObjectNode node, string name) =>
            node.Properties.Find(property => property.PropertyName == name);

        internal static Dictionary<string, Type> GetTargetTypes(XamlObjectNode template)
        {
            var names = new Dictionary<string, Type>(StringComparer.Ordinal);
            XamlPropertyNode content = FindProperty(template, "TemplateContent");
            if (content != null)
                foreach (XamlObjectNode child in content.Objects)
                    Visit(child);
            return names;

            void Visit(XamlObjectNode node)
            {
                Type type = node.TypeName == null ? null : Type.GetType(node.TypeName, false);
                if (type == typeof(ControlTemplate))
                    return;
                string name = node.Name ?? FindProperty(node, "Name")?.TextValue;
                if (name != null && type != null)
                    names[name] = type;
                foreach (XamlPropertyNode property in node.Properties)
                    foreach (XamlObjectNode child in property.Objects)
                        Visit(child);
            }
        }

        internal static Type ResolveTargetType(Type ownerType, string targetName,
            Dictionary<string, Type> names)
        {
            if (targetName == null)
                return ownerType;
            if (names == null)
                throw new InvalidOperationException("'TargetName' property cannot be set on a Style Setter.");
            if (!names.TryGetValue(targetName, out Type type))
                throw new InvalidOperationException($"Cannot find the Trigger target '{targetName}'.");
            return type;
        }
    }
}
