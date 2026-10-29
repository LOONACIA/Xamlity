namespace Xamlity.Markup
{
    public sealed class XamlAssetReference
    {
        public XamlAssetReference(string value, int line, int column)
        {
            Value = value;
            Line = line;
            Column = column;
        }

        public string Value { get; }
        public int Line { get; }
        public int Column { get; }
    }
}
