using System.Collections.Generic;

namespace Xamlity.Markup
{
    public sealed class XamlParseResult
    {
        internal XamlParseResult(XamlDocument document, IReadOnlyList<XamlError> errors,
            IReadOnlyList<XamlAssetReference> assetReferences)
        {
            Document = document;
            Errors = errors;
            AssetReferences = assetReferences;
        }

        public XamlDocument Document { get; }
        public IReadOnlyList<XamlError> Errors { get; }
        public IReadOnlyList<XamlAssetReference> AssetReferences { get; }
    }
}
