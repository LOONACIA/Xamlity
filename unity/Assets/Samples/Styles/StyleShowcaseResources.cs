using Xamlity.Styling;

namespace Xamlity.Samples.Styles
{
    public sealed class StyleShowcaseResources : ResourceDictionary
    {
        public StyleShowcaseResources()
        {
            // Numeric object elements are not supported by the XAML parser yet.
            Add("BodyFontSize", 26d);
        }
    }
}
