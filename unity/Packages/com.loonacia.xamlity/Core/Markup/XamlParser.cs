using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Xml;
using Xamlity.Data;
using Xamlity.Properties;
using Xamlity.Rendering;
using Xamlity.Styling;
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
            private HashSet<string> templateNames;
            private List<(XamlObjectNode Extension, DependencyProperty Target)> templateBindings;

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
                string propertyName = null, bool allowBinding = false, Type parentDataType = null,
                Type styleTargetType = null, Type styleOwnerType = null, bool resourceEntry = false,
                bool deferredStyleTarget = false)
            {
                ReadNamespaceDeclarations();
                Type dataType = ReadDataType(parentDataType);
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
                    if (isRoot && !typeof(UIElement).IsAssignableFrom(type) && !typeof(ResourceDictionary).IsAssignableFrom(type))
                        Error($"Root element '{name}' must be a UIElement or ResourceDictionary.");
                    if (type == typeof(Binding) && !allowBinding)
                        Error(BindingTargetError);
                    else if (type != typeof(Binding) && targetType != null && !targetType.IsAssignableFrom(type))
                        Error($"Type '{name}' is not assignable to property '{propertyName}' of type '{targetType.Name}'.");
                }

                var assigned = new HashSet<string>(StringComparer.Ordinal);
                DependencyProperty setterProperty = null;
                if (type == typeof(Setter))
                {
                    while (reader.MoveToNextAttribute())
                        if ((reader.LocalName == nameof(Setter.Property) || reader.LocalName == "Setter.Property") &&
                            (reader.NamespaceURI.Length == 0 || reader.NamespaceURI == XamlTypeResolver.DefaultNamespace))
                        {
                            if (!IsMarkupExtension(reader.Value) && (!deferredStyleTarget || reader.Value.Contains(".")))
                            {
                                setterProperty = XamlTypeResolver.ResolveSetterProperty(styleTargetType, reader.Value, out string error);
                                if (error != null)
                                    Error(error);
                            }
                            break;
                        }
                    reader.MoveToElement();
                }
                ReadAttributes(type, node, assigned, dataType, parentDataType, setterProperty, resourceEntry);
                if (node.Name != null && templateNames != null && !templateNames.Add(node.Name))
                    errors.Add(new XamlError(fileName, node.Line, node.Column,
                        $"Name '{node.Name}' is already defined in this template."));
                bool isTemplate = type == typeof(ControlTemplate);
                HashSet<string> outerTemplateNames = templateNames;
                var outerTemplateBindings = templateBindings;
                if (isTemplate)
                {
                    templateNames = new HashSet<string>(StringComparer.Ordinal);
                    templateBindings = new List<(XamlObjectNode, DependencyProperty)>();
                }
                bool isDictionary = type != null && typeof(ResourceDictionary).IsAssignableFrom(type);
                var resourceKeys = new HashSet<object>();
                bool isStyle = type != null && typeof(Style).IsAssignableFrom(type);
                styleTargetType = isStyle ? GetStyleTargetType(node) : styleTargetType;
                if (isStyle)
                    deferredStyleTarget = HasObjectProperty(node, nameof(Style.TargetType));
                string contentName = type?.GetCustomAttribute<ContentPropertyAttribute>(true)?.Name;
                Type contentType = contentName == null ? null :
                    XamlTypeResolver.ResolveProperty(type, contentName, out _, allowCollections: true);
                Type collection = XamlTypeResolver.GetCollectionInterface(contentType);
                Type childType = collection == null ? contentType : collection.GetGenericArguments()[0];
                XamlPropertyNode content = null;
                bool empty = reader.IsEmptyElement;
                reader.Read();
                if (empty)
                {
                    if (type == typeof(Binding))
                        ValidateBindingPath(node, dataType);
                    ValidateStyleAndSetter(type, node, assigned, styleTargetType, styleOwnerType);
                    templateNames = outerTemplateNames;
                    templateBindings = outerTemplateBindings;
                    return node;
                }

                while (reader.NodeType != XmlNodeType.EndElement && !reader.EOF)
                {
                    if (reader.NodeType == XmlNodeType.Element)
                    {
                        if (reader.LocalName.Contains("."))
                        {
                            ReadPropertyElement(type, node, assigned, dataType, parentDataType,
                                isStyle ? styleTargetType : null, setterProperty, isStyle && deferredStyleTarget);
                            continue;
                        }
                        if (isDictionary)
                        {
                            XamlObjectNode entry = ReadObject(parentDataType: dataType, resourceEntry: true);
                            ValidateResourceEntry(entry, resourceKeys);
                            var entries = new XamlPropertyNode { PropertyName = "Entries", Line = entry.Line, Column = entry.Column };
                            entries.Objects.Add(entry);
                            node.Properties.Add(entries);
                            continue;
                        }
                        if (isTemplate)
                        {
                            if (content == null)
                            {
                                content = new XamlPropertyNode { PropertyName = "TemplateContent",
                                    Line = location.LineNumber, Column = location.LinePosition };
                                node.Properties.Add(content);
                            }
                            else
                                Error("ControlTemplate can have only one root element.");
                            content.Objects.Add(ReadObject(targetType: typeof(FrameworkElement),
                                propertyName: "TemplateContent", parentDataType: dataType));
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
                            else if (node.Properties[node.Properties.Count - 1] != content)
                            {
                                content = new XamlPropertyNode { PropertyName = contentName,
                                    Line = location.LineNumber, Column = location.LinePosition };
                                node.Properties.Add(content);
                            }
                        }
                        XamlObjectNode child = ReadObject(targetType: childType, propertyName: contentName,
                            parentDataType: dataType, styleTargetType: isStyle ? styleTargetType : null,
                            deferredStyleTarget: isStyle && deferredStyleTarget);
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
                if (type == typeof(Binding))
                    ValidateBindingPath(node, dataType);
                ValidateStyleAndSetter(type, node, assigned, styleTargetType, styleOwnerType);
                if (isTemplate && !HasObjectProperty(node, nameof(ControlTemplate.TargetType)))
                {
                    Type templateTarget = GetStyleTargetType(node) ?? typeof(Controls.Control);
                    foreach (var binding in templateBindings)
                    {
                        TemplateBindingExtension.ResolveProperty(templateTarget, binding.Extension.Properties[0].TextValue,
                            binding.Target, out string error);
                        if (error != null)
                            errors.Add(new XamlError(fileName, binding.Extension.Line, binding.Extension.Column, error));
                    }
                }
                templateNames = outerTemplateNames;
                templateBindings = outerTemplateBindings;
                return node;
            }

            private static Type GetStyleTargetType(XamlObjectNode node)
            {
                foreach (XamlPropertyNode property in node.Properties)
                    if (property.PropertyName == nameof(Style.TargetType) && property.TextValue != null)
                        return Type.GetType(property.TextValue, throwOnError: false);
                return null;
            }

            private void ValidateResourceEntry(XamlObjectNode node, HashSet<object> keys)
            {
                Type type = node.TypeName == null ? null : Type.GetType(node.TypeName, throwOnError: false);
                object key = node.Key;
                if (key == null && type != null && typeof(Style).IsAssignableFrom(type))
                {
                    key = GetStyleTargetType(node);
                    if (key == null && HasObjectProperty(node, nameof(Style.TargetType)))
                        return;
                }
                if (key == null)
                    errors.Add(new XamlError(fileName, node.Line, node.Column, "Resource dictionary entries require an x:Key."));
                else if (!keys.Add(key))
                    errors.Add(new XamlError(fileName, node.Line, node.Column,
                        $"Duplicate resource key '{(key is Type keyType ? keyType.Name : key)}'."));
            }

            private static bool HasObjectProperty(XamlObjectNode node, string name)
            {
                foreach (XamlPropertyNode property in node.Properties)
                    if (property.PropertyName == name && property.Objects.Count == 1)
                        return true;
                return false;
            }

            private void ValidateStyleAndSetter(Type type, XamlObjectNode node, HashSet<string> assigned,
                Type targetType, Type ownerType)
            {
                if (type == typeof(Setter))
                {
                    if (!assigned.Contains(nameof(Setter.Property)))
                        NodeError("Setter requires a Property.");
                    if (!assigned.Contains(nameof(Setter.Value)))
                        NodeError("Setter requires a Value.");
                }
                else if (type != null && typeof(Style).IsAssignableFrom(type))
                {
                    if (!assigned.Contains(nameof(Style.TargetType)))
                        NodeError("Style requires a TargetType.");
                    if (targetType == null)
                        return;
                    if (ownerType != null && !targetType.IsAssignableFrom(ownerType))
                        NodeError($"'{targetType.Name}' TargetType does not match type of element '{ownerType.Name}'.");
                    foreach (XamlPropertyNode property in node.Properties)
                        if (property.PropertyName == nameof(Style.BasedOn) && property.Objects.Count == 1)
                        {
                            Type baseType = GetStyleTargetType(property.Objects[0]);
                            if (baseType != null && !baseType.IsAssignableFrom(targetType))
                                NodeError($"Can only base on a Style with target type that is base type '{targetType.Name}'.");
                        }
                }

                void NodeError(string message) => errors.Add(new XamlError(fileName, node.Line, node.Column, message));
            }

            private void ReadPropertyElement(Type ownerType, XamlObjectNode owner, HashSet<string> assigned,
                Type dataType, Type parentDataType, Type styleTargetType, DependencyProperty setterProperty,
                bool deferredStyleTarget)
            {
                ReadNamespaceDeclarations();
                dataType = ReadDataType(dataType);
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
                        if (ownerType == typeof(Setter) && propertyName == nameof(Setter.Value))
                            propertyType = setterProperty?.PropertyType ?? typeof(object);
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
                    if (reader.NamespaceURI != "http://www.w3.org/2000/xmlns/" && !IsDataTypeAttribute())
                        Error(reader.NamespaceURI == XamlTypeResolver.XamlNamespace && reader.LocalName == "Key"
                            ? "x:Key is only valid on resource dictionary entries."
                            : "Attributes on property elements are not supported.");
                reader.MoveToElement();
                Type collection = ownerType == typeof(Setter) && propertyName == nameof(Setter.Value)
                    ? null : XamlTypeResolver.GetCollectionInterface(propertyType);
                Type childType = collection == null ? propertyType : collection.GetGenericArguments()[0];
                bool resources = propertyType == typeof(ResourceDictionary);
                if (resources)
                    childType = null;
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
                            {
                                bool isBinding = XamlTypeResolver.Resolve(reader.NamespaceURI, reader.LocalName) == typeof(Binding);
                                node.Objects.Add(ReadObject(targetType: childType, propertyName: propertyName,
                                    allowBinding: collection == null && IsBindingTarget(ownerType, dp),
                                    parentDataType: isBinding && dp == FrameworkElement.DataContextProperty ? parentDataType : dataType,
                                    styleTargetType: styleTargetType,
                                    styleOwnerType: propertyType == typeof(Style) && propertyName != nameof(Style.BasedOn)
                                        ? ownerType : null, resourceEntry: resources, deferredStyleTarget: deferredStyleTarget));
                            }
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
                if (resources)
                {
                    bool dictionaryAssignment = node.Objects.Count == 1 && node.Objects[0].Key == null && node.Objects[0].TypeName != null &&
                        typeof(ResourceDictionary).IsAssignableFrom(Type.GetType(node.Objects[0].TypeName, throwOnError: false));
                    if (!dictionaryAssignment)
                    {
                        var keys = new HashSet<object>();
                        foreach (XamlObjectNode entry in node.Objects)
                            ValidateResourceEntry(entry, keys);
                    }
                }
                else if (propertyType != null && collection == null && node.Objects.Count != 1)
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

            private bool IsDataTypeAttribute() =>
                reader.NamespaceURI == XamlTypeResolver.XamlNamespace && reader.LocalName == "DataType";

            private Type ReadDataType(Type inheritedType)
            {
                if (!reader.MoveToAttribute("DataType", XamlTypeResolver.XamlNamespace))
                    return inheritedType;
                string name = reader.Value.Trim();
                Type type = null;
                if (name.StartsWith("{", StringComparison.Ordinal))
                    Error("x:DataType does not support markup extensions; use 'prefix:TypeName'.");
                else
                {
                    int separator = name.IndexOf(':');
                    string xmlNamespace = reader.LookupNamespace(separator < 0 ? "" : name.Substring(0, separator));
                    string localName = separator < 0 ? name : name.Substring(separator + 1);
                    type = XamlTypeResolver.ResolveDataType(xmlNamespace, localName, out string error);
                    if (type == null && !invalidMappings.Contains(xmlNamespace))
                        Error(error);
                }
                reader.MoveToElement();
                return type;
            }

            private void ReadAttributes(Type type, XamlObjectNode node, HashSet<string> assigned,
                Type dataType, Type parentDataType, DependencyProperty setterProperty, bool resourceEntry)
            {
                while (reader.MoveToNextAttribute())
                {
                    if (reader.NamespaceURI == "http://www.w3.org/2000/xmlns/" || IsDataTypeAttribute())
                        continue;
                    string name = reader.LocalName;
                    if (reader.NamespaceURI == XamlTypeResolver.XamlNamespace && name == "Key")
                    {
                        if (!resourceEntry)
                            Error("x:Key is only valid on resource dictionary entries.");
                        else if (IsMarkupExtension(reader.Value))
                        {
                            node.Key = reader.Value;
                            Error("x:Key does not support markup extensions.");
                        }
                        else
                        {
                            XamlValueConverter.TryConvert(reader.Value, typeof(string), null, out object key, out _);
                            node.Key = (string)key;
                        }
                        continue;
                    }
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
                    if (type == typeof(Setter) && name == nameof(Setter.Property) && !IsMarkupExtension(reader.Value))
                        property.TextValue = reader.Value;
                    else if (type == typeof(Setter) && name == nameof(Setter.Value))
                    {
                        ReadAttributeValue(type, setterProperty?.PropertyType ?? typeof(object), setterProperty, property, dataType);
                    }
                    else
                        ReadAttributeValue(type, propertyType, dp, property,
                            dp == FrameworkElement.DataContextProperty ? parentDataType : dataType);
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

            private void ReadAttributeValue(Type type, Type propertyType, DependencyProperty dp, XamlPropertyNode property,
                Type dataType)
            {
                if (propertyType == null)
                    Error($"Unknown or read-only property '{property.PropertyName}' on type '{type.Name}'.");
                else if (IsMarkupExtension(reader.Value))
                {
                    string text = reader.Value.TrimStart();
                    if (!XamlMarkupExtensionParser.TryParse(text, out string name, out var arguments, out string error))
                    {
                        Error(error);
                        return;
                    }
                    int separator = name.IndexOf(':');
                    string xmlNamespace = reader.LookupNamespace(separator < 0 ? "" : name.Substring(0, separator));
                    string localName = separator < 0 ? name : name.Substring(separator + 1);
                    if (xmlNamespace == XamlTypeResolver.DefaultNamespace && localName == "TemplateBinding")
                    {
                        ReadTemplateBindingExtension(type, dp, property, arguments);
                        return;
                    }
                    if (xmlNamespace == XamlTypeResolver.DefaultNamespace && localName == "StaticResource")
                    {
                        XamlObjectNode extension = ReadResourceExtension(arguments);
                        if (extension != null)
                            property.Objects.Add(extension);
                        return;
                    }
                    if (xmlNamespace == XamlTypeResolver.XamlNamespace && localName == "Type")
                    {
                        if (propertyType != typeof(Type))
                            Error("Markup extension 'x:Type' can only be used for Type values.");
                        else
                            property.TextValue = ReadTypeExtension(arguments);
                        return;
                    }
                    XamlObjectNode binding = ReadBindingExtension(name, arguments);
                    if (binding != null)
                    {
                        if (!IsBindingTarget(type, dp))
                            Error(BindingTargetError);
                        ValidateBindingPath(binding, dataType);
                        property.Objects.Add(binding);
                    }
                    return;
                }
                else if (propertyType == typeof(Type))
                {
                    property.TextValue = ReadTypeName(reader.Value);
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

            private string ReadTypeName(string name)
            {
                name = name.Trim();
                int separator = name.IndexOf(':');
                string xmlNamespace = reader.LookupNamespace(separator < 0 ? "" : name.Substring(0, separator));
                string localName = separator < 0 ? name : name.Substring(separator + 1);
                Type type = XamlTypeResolver.Resolve(xmlNamespace, localName, out string error, requireConstructor: false);
                if (type == null && !invalidMappings.Contains(xmlNamespace))
                    Error(error);
                return type == null ? null : $"{type.FullName}, {type.Assembly.GetName().Name}";
            }

            private XamlObjectNode ReadResourceExtension(
                List<(string Name, string Value, XamlMarkupExtensionParser.Extension Nested)> arguments)
            {
                if (arguments.Count != 1 || (arguments[0].Name != null && arguments[0].Name != "ResourceKey"))
                {
                    Error("Markup extension 'StaticResource' requires exactly one ResourceKey.");
                    return null;
                }
                var extension = new XamlObjectNode
                {
                    TypeName = $"{typeof(StaticResourceExtension).FullName}, {typeof(StaticResourceExtension).Assembly.GetName().Name}",
                    Line = location.LineNumber, Column = location.LinePosition
                };
                var key = new XamlPropertyNode { PropertyName = "ResourceKey", TextValue = arguments[0].Value,
                    Line = location.LineNumber, Column = location.LinePosition };
                if (arguments[0].Nested != null)
                {
                    XamlObjectNode nested = ReadNestedExtension(arguments[0].Nested, typeKey: true);
                    if (nested == null)
                        return null;
                    key.Objects.Add(nested);
                }
                extension.Properties.Add(key);
                return extension;
            }

            private void ReadTemplateBindingExtension(Type type, DependencyProperty dp, XamlPropertyNode property,
                List<(string Name, string Value, XamlMarkupExtensionParser.Extension Nested)> arguments)
            {
                if (templateBindings == null)
                {
                    Error(TemplateBindingExtension.ContextError);
                    return;
                }
                if (!typeof(DependencyObject).IsAssignableFrom(type) || dp == null)
                {
                    Error(TemplateBindingExtension.TargetError);
                    return;
                }
                if (arguments.Count != 1 || (arguments[0].Name != null && arguments[0].Name != "Property"))
                {
                    Error("Markup extension 'TemplateBinding' accepts only Property.");
                    return;
                }
                if (arguments[0].Nested != null)
                {
                    Error($"Nested markup extension '{arguments[0].Nested.Name}' is not supported here.");
                    return;
                }
                var extension = new XamlObjectNode
                {
                    TypeName = $"{typeof(TemplateBindingExtension).FullName}, {typeof(TemplateBindingExtension).Assembly.GetName().Name}",
                    Line = location.LineNumber, Column = location.LinePosition
                };
                extension.Properties.Add(new XamlPropertyNode { PropertyName = "Property", TextValue = arguments[0].Value,
                    Line = location.LineNumber, Column = location.LinePosition });
                property.Objects.Add(extension);
                templateBindings.Add((extension, dp));
            }

            private string ReadTypeExtension(
                List<(string Name, string Value, XamlMarkupExtensionParser.Extension Nested)> arguments)
            {
                foreach (var argument in arguments)
                    if (argument.Nested != null)
                    {
                        Error($"Nested markup extension '{argument.Nested.Name}' is not supported here.");
                        return null;
                    }
                if (arguments.Count != 1 || (arguments[0].Name != null && arguments[0].Name != "TypeName") ||
                    string.IsNullOrWhiteSpace(arguments[0].Value))
                {
                    Error("Markup extension 'x:Type' requires exactly one TypeName.");
                    return null;
                }
                return ReadTypeName(arguments[0].Value);
            }

            private XamlObjectNode ReadNestedExtension(XamlMarkupExtensionParser.Extension extension, bool typeKey)
            {
                int separator = extension.Name.IndexOf(':');
                string xmlNamespace = reader.LookupNamespace(separator < 0 ? "" : extension.Name.Substring(0, separator));
                string localName = separator < 0 ? extension.Name : extension.Name.Substring(separator + 1);
                if (!typeKey && xmlNamespace == XamlTypeResolver.DefaultNamespace && localName == "StaticResource")
                    return ReadResourceExtension(extension.Arguments);
                if (typeKey && xmlNamespace == XamlTypeResolver.XamlNamespace && localName == "Type")
                {
                    string typeName = ReadTypeExtension(extension.Arguments);
                    if (typeName == null)
                        return null;
                    var node = new XamlObjectNode
                    {
                        TypeName = $"{typeof(TypeExtension).FullName}, {typeof(TypeExtension).Assembly.GetName().Name}",
                        Line = location.LineNumber, Column = location.LinePosition
                    };
                    node.Properties.Add(new XamlPropertyNode { PropertyName = "TypeName", TextValue = typeName,
                        Line = location.LineNumber, Column = location.LinePosition });
                    return node;
                }
                Error($"Nested markup extension '{extension.Name}' is not supported here.");
                return null;
            }

            private XamlObjectNode ReadBindingExtension(string name,
                List<(string Name, string Value, XamlMarkupExtensionParser.Extension Nested)> arguments)
            {
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
                    if (argument.Name == null && argument.Nested != null)
                    {
                        Error($"Nested markup extension '{argument.Nested.Name}' is not supported here.");
                        return null;
                    }
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
                    else if (argument.Nested == null)
                        ValidateText(propertyName, argument.Value, propertyType, dp);
                    var property = new XamlPropertyNode { PropertyName = propertyName, TextValue = argument.Value,
                        Line = location.LineNumber, Column = location.LinePosition };
                    if (argument.Nested != null)
                    {
                        XamlObjectNode nested = ReadNestedExtension(argument.Nested, typeKey: false);
                        if (nested == null)
                            return null;
                        property.Objects.Add(nested);
                    }
                    binding.Properties.Add(property);
                }
                return binding;
            }

            private void Error(string message) =>
                errors.Add(new XamlError(fileName, location.LineNumber, location.LinePosition, message));

            private void ValidateBindingPath(XamlObjectNode binding, Type dataType)
            {
                if (dataType == null)
                    return;
                PropertyPath path = null;
                BindingMode mode = BindingMode.OneWay;
                foreach (XamlPropertyNode property in binding.Properties)
                {
                    Type valueType = property.PropertyName == "Path" ? typeof(PropertyPath) :
                        property.PropertyName == "Mode" ? typeof(BindingMode) : null;
                    if (valueType == null || property.TextValue == null)
                        continue;
                    if (!XamlValueConverter.TryConvert(property.TextValue, valueType, null, out object value, out _))
                        return;
                    if (value is PropertyPath propertyPath)
                        path = propertyPath;
                    else
                        mode = (BindingMode)value;
                }
                if (path == null)
                    return;
                for (int i = 0; i < path.Segments.Length; i++)
                {
                    string segment = path.Segments[i];
                    PropertyInfo property = FindPathProperty(dataType, segment);
                    string error = null;
                    if (property == null)
                        error = $"Binding path '{path.Path}': property '{segment}' was not found on type '{dataType.FullName}' (x:DataType).";
                    else if (mode == BindingMode.TwoWay && i == path.Segments.Length - 1 && property.GetSetMethod() == null)
                        error = $"Binding path '{path.Path}': property '{segment}' on type '{dataType.FullName}' has no public setter; TwoWay bindings require one.";
                    if (error != null)
                    {
                        errors.Add(new XamlError(fileName, binding.Line, binding.Column, error));
                        return;
                    }
                    dataType = property.PropertyType;
                }
            }

            private static PropertyInfo FindPathProperty(Type type, string name)
            {
                for (Type current = type; current != null; current = current.BaseType)
                    foreach (PropertyInfo property in current.GetProperties(
                        BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
                        if (property.Name == name && property.GetGetMethod() != null && property.GetIndexParameters().Length == 0)
                            return property;
                PropertyInfo inherited = null;
                if (type.IsInterface)
                    foreach (Type baseInterface in type.GetInterfaces())
                    {
                        PropertyInfo candidate = FindPathProperty(baseInterface, name);
                        if (candidate != null && (inherited == null ||
                            inherited.DeclaringType.IsAssignableFrom(candidate.DeclaringType)))
                            inherited = candidate;
                    }
                return inherited;
            }
        }
    }
}
