using System.Collections.Generic;

namespace Xamlity.Markup
{
    public sealed class XamlParseResult
    {
        internal XamlParseResult(XamlDocument document, IReadOnlyList<XamlError> errors,
            IReadOnlyList<XamlAssetReference> assetReferences, IReadOnlyList<string> referencedAssemblies)
        {
            Document = document;
            Errors = errors;
            AssetReferences = assetReferences;
            ReferencedAssemblies = referencedAssemblies;
        }

        public XamlDocument Document { get; }
        public IReadOnlyList<XamlError> Errors { get; }
        public IReadOnlyList<XamlAssetReference> AssetReferences { get; }
        public IReadOnlyList<string> ReferencedAssemblies { get; }
    }
}
