namespace Xamlity.Rendering
{
    public interface ITextMeasurer
    {
        FontMetrics GetFontMetrics(FontHandle font, double fontSize);
        bool TryGetGlyph(FontHandle font, int codePoint, double fontSize, out GlyphMetrics glyph);
    }
}
