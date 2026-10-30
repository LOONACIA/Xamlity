using Xamlity.Rendering;
using Xamlity.Styling;

namespace Xamlity.Markup
{
    public interface IXamlAssetResolver
    {
        ImageSource ResolveImage(string value);
        ResourceDictionary ResolveResourceDictionary(string value);
    }
}
