using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Xml;
using Xamlity.Data;
using Xamlity.Properties;
using Xamlity.Rendering;
using Xamlity.Tree;

namespace Xamlity.Markup
{
    public static class XamlParser
    {
        public static XamlParseResult Parse(string text, string fileName)
        {
            var errors = new List<XamlError>();
            var assetReferences = new List<XamlAssetReference>();
            var referencedAssemblies = new List<string>();
            XamlObjectNode root = null;
            var settings = new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null };
            try
            {
                using (var input = new StringReader(text))
                using (XmlReader reader = XmlReader.Create(input, settings))
                {
                    var parser = new Parser(reader, fileName, errors, assetReferences, referencedAssemblies);
                    reader.MoveToContent();
                    root = parser.ReadObject(isRoot: true);
                    // Read through EOF so malformed trailing XML is also reported.
                    while (reader.Read()) { }
                }
            }
            catch (XmlException exception)
            {
                errors.Add(new XamlError(fileName, Math.Max(1, exception.LineNumber),
                    Math.Max(1, exception.LinePosition), exception.Message));
            }
            return new XamlParseResult(errors.Count == 0 ? new XamlDocument { Root = root } : null,
                errors.AsReadOnly(), assetReferences.AsReadOnly(), referencedAssemblies.AsReadOnly());
        }

        private sealed class Parser
        {
            private readonly XmlReader reader;
            private readonly IXmlLineInfo location;
            private readonly string fileName;
            private readonly List<XamlError> errors;
            private readonly List<XamlAssetReference> assetReferences;
            private readonly List<string> referencedAssemblies;
            private readonly HashSet<string> invalidMappings = new HashSet<string>(StringComparer.Ordinal);

            internal Parser(XmlReader reader, string fileName, List<XamlError> errors,
                List<XamlAssetReference> assetReferences, List<string> referencedAssemblies)
            {
                this.reader = reader;
                location = (IXmlLineInfo)reader;
                this.fileName = fileName;
                this.errors = errors;
                this.assetReferences = assetReferences;
                this.referencedAssemblies = referencedAssemblies;
            }

            // Starts on an element and leaves the reader on the following node.
            internal XamlObjectNode ReadObject(bool isRoot = false, Type targetType = null,
                string propertyName = null, bool allowBinding = false)
            {
                ReadNamespaceDeclarations();
                string name = reader.LocalName;
                Type type = XamlTypeResolver.Resolve(reader.NamespaceURI, name, out string typeError);
                var node = new XamlObjectNode { Line = location.LineNumber, Column = location.LinePosition };
                if (name.Contains("."))
                    Error($"Property elements are not supported: '{name}'.");
                else if (reader.NamespaceURI == XamlTypeResolver.XamlNamespace)
                    Error($"XAML directive '{reader.Name}' is not supported.");
                else if (type == null && !invalidMappings.Contains(reader.NamespaceURI))
                    Error(typeError);
                if (type != null)
                {
                    node.TypeName = $"{type.FullName}, {type.Assembly.GetName().Name}";
                    if (isRoot && !typeof(UIElement).IsAssignableFrom(type))
                        Error($"Root element '{name}' must be a UIElement.");
                    if (type == typeof(Binding) && !allowBinding)
                        Error(BindingTargetError);
                    else if (type != typeof(Binding) && targetType != null && !targetType.IsAssignableFrom(type))
                        Error($"Type '{name}' is not assignable to property '{propertyName}' of type '{targetType.Name}'.");
                }

                var assigned = new HashSet<string>(StringComparer.Ordinal);
                ReadAttributes(type, node, assigned);
                string contentName = type?.GetCustomAttribute<ContentPropertyAttribute>(true)?.Name;
                Type contentType = contentName == null ? null :
                    XamlTypeResolver.ResolveProperty(type, contentName, out _, allowCollections: true);
                Type collection = XamlTypeResolver.GetCollectionInterface(contentType);
                Type childType = collection == null ? contentType : collection.GetGenericArguments()[0];
                XamlPropertyNode content = null;
                bool empty = reader.IsEmptyElement;
                reader.Read();
                if (empty)
                    return node;

                while (reader.NodeType != XmlNodeType.EndElement && !reader.EOF)
                {
                    if (reader.NodeType == XmlNodeType.Element)
                    {
                        if (reader.LocalName.Contains("."))
                        {
                            ReadPropertyElement(type, node, assigned);
                            continue;
                        }
                        if (type != null && XamlTypeResolver.Resolve(reader.NamespaceURI, reader.LocalName) != typeof(Binding))
                        {
                            if (contentName == null)
                                Error($"Type '{name}' has no content property.");
                            else if (content == null)
                            {
                                if (!assigned.Add(contentName))
                                    Error($"Property '{contentName}' is set more than once.");
                                content = new XamlPropertyNode { PropertyName = contentName,
                                    Line = location.LineNumber, Column = location.LinePosition };
                                node.Properties.Add(content);
                            }
                            else if (collection == null)
                                Error($"Content property '{contentName}' accepts only one child.");
                        }
                        XamlObjectNode child = ReadObject(targetType: childType, propertyName: contentName);
                        content?.Objects.Add(child);
                    }
                    else
                    {
                        if ((reader.NodeType == XmlNodeType.Text || reader.NodeType == XmlNodeType.CDATA ||
                            reader.NodeType == XmlNodeType.SignificantWhitespace) &&
                            !string.IsNullOrWhiteSpace(reader.Value))
                            Error("Text content is not supported; use a property attribute.");
                        reader.Read();
                    }
                }
                reader.Read();
                return node;
            }

            private void ReadPropertyElement(Type ownerType, XamlObjectNode owner, HashSet<string> assigned)
            {
                ReadNamespaceDeclarations();
                string name = reader.LocalName;
                int separator = name.IndexOf('.');
                string ownerName = name.Substring(0, separator);
                string propertyName = name.Substring(separator + 1);
                var node = new XamlPropertyNode { PropertyName = propertyName,
                    Line = location.LineNumber, Column = location.LinePosition };
                Type declaredOwner = XamlTypeResolver.Resolve(reader.NamespaceURI, ownerName,
                    out string ownerError, requireConstructor: false);
                bool matchesOwner = declaredOwner != null && declaredOwner.IsClass &&
                    ownerType != null && declaredOwner.IsAssignableFrom(ownerType);

                Type propertyType = null;
                DependencyProperty dp = null;
                bool invalidMapping = invalidMappings.Contains(reader.NamespaceURI);
                if (!invalidMapping && declaredOwner == null && reader.NamespaceURI != XamlTypeResolver.DefaultNamespace)
                    Error(ownerError);
                else if (!invalidMapping && ownerType != null)
                {
                    if (!matchesOwner)
                        Error($"Attached property elements are not supported: '{name}'.");
                    else
                    {
                        propertyType = XamlTypeResolver.ResolveProperty(ownerType, propertyName, out dp, allowCollections: true);
                        if (dp != null && dp.IsAttached)
                        {
                            Error($"Attached property elements are not supported: '{name}'.");
                            propertyType = null;
                        }
                        else if (propertyType == null)
                            Error($"Unknown or read-only property '{propertyName}' on type '{ownerType.Name}'.");
                        if (!assigned.Add(propertyName))
                            Error($"Property '{propertyName}' is set more than once.");
                        owner.Properties.Add(node);
                    }
                }

                while (reader.MoveToNextAttribute())
                    if (reader.NamespaceURI != "http://www.w3.org/2000/xmlns/")
                        Error("Attributes on property elements are not supported.");
                reader.MoveToElement();
                Type collection = XamlTypeResolver.GetCollectionInterface(propertyType);
                Type childType = collection == null ? propertyType : collection.GetGenericArguments()[0];
                bool empty = reader.IsEmptyElement;
                reader.Read();
                if (!empty)
                {
                    while (reader.NodeType != XmlNodeType.EndElement && !reader.EOF)
                    {
                        if (reader.NodeType == XmlNodeType.Element)
                        {
                            if (reader.LocalName.Contains("."))
                            {
                                Error("Property elements cannot directly contain property elements.");
                                SkipPropertyElement();
                            }
                            else
                                node.Objects.Add(ReadObject(targetType: childType, propertyName: propertyName,
                                    allowBinding: collection == null && IsBindingTarget(ownerType, dp)));
                        }
                        else
                        {
                            if ((reader.NodeType == XmlNodeType.Text || reader.NodeType == XmlNodeType.CDATA ||
                                reader.NodeType == XmlNodeType.SignificantWhitespace) &&
                                !string.IsNullOrWhiteSpace(reader.Value))
                                Error("Text content is not supported; use a property attribute.");
                            reader.Read();
                        }
                    }
                    reader.Read();
                }
                if (propertyType != null && collection == null && node.Objects.Count != 1)
                    errors.Add(new XamlError(fileName, node.Line, node.Column,
                        $"Property '{propertyName}' requires exactly one object."));
            }

            private void ReadNamespaceDeclarations()
            {
                while (reader.MoveToNextAttribute())
                {
                    if (reader.NamespaceURI != "http://www.w3.org/2000/xmlns/" ||
                        !reader.Value.StartsWith(XamlTypeResolver.ClrNamespacePrefix, StringComparison.Ordinal))
                        continue;
                    if (!XamlTypeResolver.TryParseClrNamespace(reader.Value, out _, out string assembly, out string error))
                    {
                        if (invalidMappings.Add(reader.Value))
                            Error(error);
                    }
                    else if (!referencedAssemblies.Contains(assembly))
                        referencedAssemblies.Add(assembly);
                }
                reader.MoveToElement();
            }

            private void SkipPropertyElement()
            {
                int depth = reader.Depth;
                do
                {
                    if (reader.NodeType == XmlNodeType.Element)
                        ReadNamespaceDeclarations();
                    reader.Read();
                } while (!reader.EOF && reader.Depth > depth);
                if (reader.NodeType == XmlNodeType.EndElement && reader.Depth == depth)
                    reader.Read();
            }

            private void ReadAttributes(Type type, XamlObjectNode node, HashSet<string> assigned)
            {
                while (reader.MoveToNextAttribute())
                {
                    if (reader.NamespaceURI == "http://www.w3.org/2000/xmlns/")
                        continue;
                    string name = reader.LocalName;
                    bool xName = reader.NamespaceURI == XamlTypeResolver.XamlNamespace && name == "Name";
                    if (reader.NamespaceURI == XamlTypeResolver.XamlNamespace && !xName)
                    {
                        Error($"XAML directive '{reader.Name}' is not supported.");
                        continue;
                    }
                    if (!xName && reader.NamespaceURI.Length != 0 &&
                        reader.NamespaceURI != XamlTypeResolver.DefaultNamespace)
                    {
                        Error($"XML namespace '{reader.NamespaceURI}' is not supported.");
                        continue;
                    }
                    Type attachedOwner = null;
                    bool qualified = name.Contains(".");
                    if (qualified)
                    {
                        int separator = name.IndexOf('.');
                        if (separator == 0 || separator == name.Length - 1 || separator != name.LastIndexOf('.'))
                        {
                            Error($"Invalid property name '{name}'; expected 'Owner.Property'.");
                            continue;
                        }
                        string ownerName = name.Substring(0, separator);
                        Type owner = XamlTypeResolver.Resolve(XamlTypeResolver.DefaultNamespace, ownerName,
                            requireConstructor: false);
                        if (owner == null)
                        {
                            Error($"Unknown type '{ownerName}'.");
                            continue;
                        }
                        name = name.Substring(separator + 1);
                        if (type != null && !owner.IsAssignableFrom(type))
                            attachedOwner = owner;
                    }
                    string ownerTypeName = attachedOwner == null ? null :
                        $"{attachedOwner.FullName}, {attachedOwner.Assembly.GetName().Name}";
                    if (!assigned.Add(attachedOwner == null ? name : ownerTypeName + "." + name))
                        Error($"Property '{name}' is set more than once.");
                    if (xName)
                    {
                        if (type != null && !typeof(FrameworkElement).IsAssignableFrom(type))
                            Error($"x:Name is only supported on FrameworkElement types, not '{type.Name}'.");
                        if (!XamlValueConverter.TryConvert(reader.Value, typeof(string), null, out object value, out string error))
                            Error(error);
                        node.Name = value as string;
                        continue;
                    }
                    if (type == null)
                        continue;
                    DependencyProperty dp;
                    Type propertyType;
                    if (attachedOwner != null)
                    {
                        dp = XamlTypeResolver.ResolveDependencyProperty(attachedOwner, name);
                        if (dp == null || !dp.IsAttached)
                        {
                            Error($"Unknown attached property '{name}' on type '{attachedOwner.Name}'.");
                            continue;
                        }
                        if (!typeof(DependencyObject).IsAssignableFrom(type) && !IsMarkupExtension(reader.Value))
                        {
                            Error($"Attached properties require a DependencyObject, not '{type.Name}'.");
                            continue;
                        }
                        propertyType = dp.PropertyType;
                    }
                    else
                    {
                        propertyType = XamlTypeResolver.ResolveProperty(type, name, out dp);
                        if (!qualified && dp != null && dp.IsAttached)
                            propertyType = null;
                    }
                    var property = new XamlPropertyNode { PropertyName = name, OwnerTypeName = ownerTypeName,
                        Line = location.LineNumber, Column = location.LinePosition };
                    ReadAttributeValue(type, propertyType, dp, property);
                    node.Properties.Add(property);
                }
                reader.MoveToElement();
            }

            private const string BindingTargetError =
                "A 'Binding' can only be set on a dependency property of a FrameworkElement.";

            private static bool IsBindingTarget(Type type, DependencyProperty dp) =>
                type != null && typeof(FrameworkElement).IsAssignableFrom(type) && dp != null;

            private static bool IsMarkupExtension(string value) =>
                !value.StartsWith("{}", StringComparison.Ordinal) &&
                value.TrimStart().StartsWith("{", StringComparison.Ordinal);

            private void ReadAttributeValue(Type type, Type propertyType, DependencyProperty dp, XamlPropertyNode property)
            {
                if (propertyType == null)
                    Error($"Unknown or read-only property '{property.PropertyName}' on type '{type.Name}'.");
                else if (IsMarkupExtension(reader.Value))
                {
                    XamlObjectNode binding = ReadBindingExtension(reader.Value.TrimStart());
                    if (binding != null)
                    {
                        if (!IsBindingTarget(type, dp))
                            Error(BindingTargetError);
                        property.Objects.Add(binding);
                    }
                    return;
                }
                else
                    ValidateText(property.PropertyName, reader.Value, propertyType, dp);
                property.TextValue = reader.Value;
            }

            private void ValidateText(string name, string text, Type propertyType, DependencyProperty dp)
            {
                if (propertyType == typeof(ImageSource))
                {
                    XamlValueConverter.TryConvert(text, typeof(string), null, out object path, out _);
                    if (string.IsNullOrWhiteSpace((string)path))
                        Error($"Property '{name}': Image asset paths must not be empty.");
                    else
                        assetReferences.Add(new XamlAssetReference((string)path,
                            location.LineNumber, location.LinePosition));
                }
                else if (!XamlValueConverter.TryConvert(text, propertyType, dp, out _, out string error))
                    Error($"Property '{name}': {error}");
            }

            private XamlObjectNode ReadBindingExtension(string text)
            {
                if (!XamlMarkupExtensionParser.TryParse(text, out string name, out var arguments, out string error))
                {
                    Error(error);
                    return null;
                }
                int separator = name.IndexOf(':');
                string xmlNamespace = reader.LookupNamespace(separator < 0 ? "" : name.Substring(0, separator));
                string localName = separator < 0 ? name : name.Substring(separator + 1);
                if (XamlTypeResolver.Resolve(xmlNamespace, localName) != typeof(Binding))
                {
                    Error($"Markup extension '{name}' is not supported.");
                    return null;
                }
                var binding = new XamlObjectNode { TypeName = $"{typeof(Binding).FullName}, {typeof(Binding).Assembly.GetName().Name}",
                    Line = location.LineNumber, Column = location.LinePosition };
                var assigned = new HashSet<string>(StringComparer.Ordinal);
                int positionalCount = 0;
                foreach (var argument in arguments)
                {
                    if (argument.Name == null && ++positionalCount > 1)
                    {
                        Error("Markup extension 'Binding' accepts at most one positional argument (Path).");
                        return null;
                    }
                    string propertyName = argument.Name ?? "Path";
                    if (!assigned.Add(propertyName))
                        Error($"Property '{propertyName}' is set more than once.");
                    Type propertyType = XamlTypeResolver.ResolveProperty(typeof(Binding), propertyName, out DependencyProperty dp);
                    if (propertyType == null)
                        Error($"Unknown or read-only property '{propertyName}' on type 'Binding'.");
                    else
                        ValidateText(propertyName, argument.Value, propertyType, dp);
                    binding.Properties.Add(new XamlPropertyNode { PropertyName = propertyName, TextValue = argument.Value,
                        Line = location.LineNumber, Column = location.LinePosition });
                }
                return binding;
            }

            private void Error(string message) =>
                errors.Add(new XamlError(fileName, location.LineNumber, location.LinePosition, message));
        }
    }
}
