using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Xml;
using Xamlity.Layout;
using Xamlity.Properties;

namespace Xamlity.Markup
{
    public static class XamlParser
    {
        public static XamlParseResult Parse(string text, string fileName)
        {
            var errors = new List<XamlError>();
            XamlObjectNode root = null;
            var settings = new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null };
            try
            {
                using (var input = new StringReader(text))
                using (XmlReader reader = XmlReader.Create(input, settings))
                {
                    var parser = new Parser(reader, fileName, errors);
                    reader.MoveToContent();
                    root = parser.ReadObject();
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
                errors.AsReadOnly());
        }

        private sealed class Parser
        {
            private readonly XmlReader reader;
            private readonly IXmlLineInfo location;
            private readonly string fileName;
            private readonly List<XamlError> errors;

            internal Parser(XmlReader reader, string fileName, List<XamlError> errors)
            {
                this.reader = reader;
                location = (IXmlLineInfo)reader;
                this.fileName = fileName;
                this.errors = errors;
            }

            // Starts on an element and leaves the reader on the following node.
            internal XamlObjectNode ReadObject()
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
                    node.TypeName = $"{type.FullName}, {type.Assembly.GetName().Name}";

                var assigned = new HashSet<string>(StringComparer.Ordinal);
                ReadAttributes(type, node, assigned);
                string contentName = type?.GetCustomAttribute<ContentPropertyAttribute>(true)?.Name;
                bool collection = contentName != null &&
                    type.GetProperty(contentName).PropertyType == typeof(UIElementCollection);
                XamlPropertyNode content = null;
                bool empty = reader.IsEmptyElement;
                reader.Read();
                if (empty)
                    return node;

                while (reader.NodeType != XmlNodeType.EndElement && !reader.EOF)
                {
                    if (reader.NodeType == XmlNodeType.Element)
                    {
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
                            else if (!collection)
                                Error($"Content property '{contentName}' accepts only one child.");
                        }
                        XamlObjectNode child = ReadObject();
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
                    if (name.Contains("."))
                    {
                        Error($"Attached properties are not supported: '{name}'.");
                        continue;
                    }
                    if (!assigned.Add(name))
                        Error($"Property '{name}' is set more than once.");
                    if (xName)
                    {
                        if (!XamlValueConverter.TryConvert(reader.Value, typeof(string), null, out _, out string error))
                            Error(error);
                        node.Name = reader.Value;
                        continue;
                    }
                    if (type == null)
                        continue;
                    Type propertyType = XamlTypeResolver.ResolveProperty(type, name, out DependencyProperty dp);
                    if (propertyType == null)
                        Error($"Unknown or read-only property '{name}' on type '{type.Name}'.");
                    else if (!XamlValueConverter.TryConvert(reader.Value, propertyType, dp, out _, out string error))
                        Error($"Property '{name}': {error}");
                    node.Properties.Add(new XamlPropertyNode { PropertyName = name, TextValue = reader.Value,
                        Line = location.LineNumber, Column = location.LinePosition });
                }
                reader.MoveToElement();
            }

            private void Error(string message) =>
                errors.Add(new XamlError(fileName, location.LineNumber, location.LinePosition, message));
        }
    }
}
