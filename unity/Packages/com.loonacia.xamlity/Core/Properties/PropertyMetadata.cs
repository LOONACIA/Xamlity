namespace Xamlity.Properties
{
    public class PropertyMetadata
    {
        public PropertyMetadata(object defaultValue) : this(defaultValue, null)
        {
        }

        public PropertyMetadata(object defaultValue, PropertyChangedCallback propertyChangedCallback)
        {
            DefaultValue = defaultValue;
            PropertyChangedCallback = propertyChangedCallback;
        }

        public object DefaultValue { get; }
        public PropertyChangedCallback PropertyChangedCallback { get; }
    }
}
