namespace Xamlity.Markup
{
    public enum XamlAssetReferenceKind
    {
        Image,
        ResourceDictionary
    }

    public sealed class XamlAssetReference
    {
        public XamlAssetReference(string value, int line, int column,
            XamlAssetReferenceKind kind = XamlAssetReferenceKind.Image)
        {
            Value = value;
            Line = line;
            Column = column;
            Kind = kind;
        }

        public string Value { get; }
        public int Line { get; }
        public int Column { get; }
        public XamlAssetReferenceKind Kind { get; }
    }
}
