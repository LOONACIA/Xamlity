using System;

namespace Xamlity.Markup
{
    public sealed class XamlBuildException : Exception
    {
        public XamlBuildException(int line, int column, string message, Exception innerException = null)
            : base($"({line},{column}): {message}", innerException)
        {
            Line = line;
            Column = column;
        }

        public int Line { get; }
        public int Column { get; }
    }
}
