using Xamlity.Rendering;

namespace Xamlity.Markup
{
    public interface IXamlAssetResolver
    {
        ImageSource ResolveImage(string value);
    }
}
