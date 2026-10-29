namespace Xamlity.Markup
{
    public sealed class XamlError
    {
        public XamlError(string file, int line, int column, string message)
        {
            File = file;
            Line = line;
            Column = column;
            Message = message;
        }

        public string File { get; }
        public int Line { get; }
        public int Column { get; }
        public string Message { get; }

        public override string ToString() => $"{File}({Line},{Column}): {Message}";
    }
}
