namespace Xamlity.Properties
{
    public class FrameworkPropertyMetadata : PropertyMetadata
    {
        public FrameworkPropertyMetadata(object defaultValue, FrameworkPropertyMetadataOptions flags,
            PropertyChangedCallback propertyChangedCallback = null) : base(defaultValue, propertyChangedCallback)
        {
            AffectsMeasure = (flags & FrameworkPropertyMetadataOptions.AffectsMeasure) != 0;
            AffectsArrange = (flags & FrameworkPropertyMetadataOptions.AffectsArrange) != 0;
            AffectsRender = (flags & FrameworkPropertyMetadataOptions.AffectsRender) != 0;
            Inherits = (flags & FrameworkPropertyMetadataOptions.Inherits) != 0;
        }

        public bool AffectsMeasure { get; }
        public bool AffectsArrange { get; }
        public bool AffectsRender { get; }
        public bool Inherits { get; }
    }
}
