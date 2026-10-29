using System;
using System.Collections.Generic;

namespace Xamlity.Properties
{
    public sealed class DependencyProperty
    {
        private static readonly Dictionary<(Type, string), DependencyProperty> Registry =
            new Dictionary<(Type, string), DependencyProperty>();
        private static readonly List<DependencyProperty> InheritedProperties = new List<DependencyProperty>();

        public static readonly object UnsetValue = new object();

        private DependencyProperty(string name, Type propertyType, Type ownerType, PropertyMetadata defaultMetadata)
        {
            Name = name;
            PropertyType = propertyType;
            OwnerType = ownerType;
            DefaultMetadata = defaultMetadata;
        }

        public string Name { get; }
        public Type PropertyType { get; }
        public Type OwnerType { get; }
        public PropertyMetadata DefaultMetadata { get; }

        internal bool Inherits => DefaultMetadata is FrameworkPropertyMetadata metadata && metadata.Inherits;
        internal static IReadOnlyList<DependencyProperty> RegisteredInheritedProperties => InheritedProperties;

        public static DependencyProperty Register(string name, Type propertyType, Type ownerType,
            PropertyMetadata typeMetadata = null)
        {
            if (string.IsNullOrEmpty(name))
                throw new ArgumentException("Property name cannot be null or empty.", nameof(name));
            if (propertyType == null)
                throw new ArgumentNullException(nameof(propertyType));
            if (ownerType == null)
                throw new ArgumentNullException(nameof(ownerType));
            if (Registry.ContainsKey((ownerType, name)))
                throw new ArgumentException("The property is already registered for this owner type.", nameof(name));

            if (typeMetadata == null)
                typeMetadata = new PropertyMetadata(propertyType.IsValueType ? Activator.CreateInstance(propertyType) : null);

            object defaultValue = typeMetadata.DefaultValue;
            bool validDefault = defaultValue == null
                ? !propertyType.IsValueType || Nullable.GetUnderlyingType(propertyType) != null
                : propertyType.IsInstanceOfType(defaultValue);
            if (!validDefault)
                throw new ArgumentException("The default value does not match the property type.", nameof(typeMetadata));

            var property = new DependencyProperty(name, propertyType, ownerType, typeMetadata);
            Registry.Add((ownerType, name), property);
            if (property.Inherits)
                InheritedProperties.Add(property);
            return property;
        }

        internal static DependencyProperty FromName(Type ownerType, string name)
        {
            for (Type type = ownerType; type != null; type = type.BaseType)
            {
                if (Registry.TryGetValue((type, name), out var property))
                    return property;
            }
            return null;
        }
    }
}
