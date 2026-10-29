using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Xml;
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
            XamlObjectNode root = null;
            var settings = new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null };
            try
            {
                using (var input = new StringReader(text))
                using (XmlReader reader = XmlReader.Create(input, settings))
                {
                    var parser = new Parser(reader, fileName, errors, assetReferences);
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
                errors.AsReadOnly(), assetReferences.AsReadOnly());
        }

        private sealed class Parser
        {
            private readonly XmlReader reader;
            private readonly IXmlLineInfo location;
            private readonly string fileName;
            private readonly List<XamlError> errors;
            private readonly List<XamlAssetReference> assetReferences;

            internal Parser(XmlReader reader, string fileName, List<XamlError> errors,
                List<XamlAssetReference> assetReferences)
            {
                this.reader = reader;
                location = (IXmlLineInfo)reader;
                this.fileName = fileName;
                this.errors = errors;
                this.assetReferences = assetReferences;
            }

            // Starts on an element and leaves the reader on the following node.
            internal XamlObjectNode ReadObject(bool isRoot = false, Type targetType = null, string propertyName = null)
            {
                string name = reader.LocalName;
                Type type = XamlTypeResolver.Resolve(reader.NamespaceURI, name);
                var node = new XamlObjectNode { Line = location.LineNumber, Column = location.LinePosition };
                if (name.Contains("."))
                    Error($"Property elements are not supported: '{name}'.");
                else if (reader.NamespaceURI == XamlTypeResolver.XamlNamespace)
                    Error($"XAML directive '{reader.Name}' is not supported.");
                else if (reader.NamespaceURI != XamlTypeResolver.DefaultNamespace)
                    Error($"XML namespace '{reader.NamespaceURI}' is not supported.");
                else if (type == null)
                    Error($"Unknown element '{name}'.");
                if (type != null)
                {
                    node.TypeName = $"{type.FullName}, {type.Assembly.GetName().Name}";
                    if (isRoot && !typeof(UIElement).IsAssignableFrom(type))
                        Error($"Root element '{name}' must be a UIElement.");
                    if (targetType != null && !targetType.IsAssignableFrom(type))
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
                        if (type != null)
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
                string name = reader.LocalName;
                int separator = name.IndexOf('.');
                string ownerName = name.Substring(0, separator);
                string propertyName = name.Substring(separator + 1);
                var node = new XamlPropertyNode { PropertyName = propertyName,
                    Line = location.LineNumber, Column = location.LinePosition };
                bool matchesOwner = false;
                for (Type current = ownerType; current != null; current = current.BaseType)
                    if (current.Name == ownerName)
                        matchesOwner = true;

                Type propertyType = null;
                if (reader.NamespaceURI != XamlTypeResolver.DefaultNamespace)
                    Error($"XML namespace '{reader.NamespaceURI}' is not supported.");
                else if (ownerType != null)
                {
                    if (!matchesOwner)
                        Error($"Attached property elements are not supported: '{name}'.");
                    else
                    {
                        propertyType = XamlTypeResolver.ResolveProperty(ownerType, propertyName, out DependencyProperty dp, allowCollections: true);
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
                                reader.Skip();
                            }
                            else
                                node.Objects.Add(ReadObject(targetType: childType, propertyName: propertyName));
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
                        if (!XamlValueConverter.TryConvert(reader.Value, typeof(string), null, out _, out string error))
                            Error(error);
                        node.Name = reader.Value;
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
                        if (!typeof(DependencyObject).IsAssignableFrom(type))
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
                    if (propertyType == null)
                        Error($"Unknown or read-only property '{name}' on type '{type.Name}'.");
                    else if (propertyType == typeof(ImageSource))
                    {
                        if (string.IsNullOrWhiteSpace(reader.Value))
                            Error($"Property '{name}': Image asset paths must not be empty.");
                        else if (reader.Value.TrimStart().StartsWith("{", StringComparison.Ordinal))
                            Error($"Property '{name}': Markup extensions are not supported.");
                        else
                            assetReferences.Add(new XamlAssetReference(reader.Value,
                                location.LineNumber, location.LinePosition));
                    }
                    else if (!XamlValueConverter.TryConvert(reader.Value, propertyType, dp, out _, out string error))
                        Error($"Property '{name}': {error}");
                    node.Properties.Add(new XamlPropertyNode { PropertyName = name, OwnerTypeName = ownerTypeName, TextValue = reader.Value,
                        Line = location.LineNumber, Column = location.LinePosition });
                }
                reader.MoveToElement();
            }

            private void Error(string message) =>
                errors.Add(new XamlError(fileName, location.LineNumber, location.LinePosition, message));
        }
    }
}
