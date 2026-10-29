using System.Collections.Generic;

namespace Xamlity.Markup
{
    public sealed class XamlParseResult
    {
        internal XamlParseResult(XamlDocument document, IReadOnlyList<XamlError> errors)
        {
            Document = document;
            Errors = errors;
        }

        public XamlDocument Document { get; }
        public IReadOnlyList<XamlError> Errors { get; }
    }
}
