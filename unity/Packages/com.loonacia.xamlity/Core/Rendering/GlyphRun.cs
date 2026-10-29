namespace Xamlity.Rendering
{
    public sealed class GlyphRun
    {
        public GlyphRun(FontHandle font, double fontSize, uint[] glyphIndices, double[] advanceWidths)
        {
            Font = font;
            FontSize = fontSize;
            GlyphIndices = glyphIndices;
            AdvanceWidths = advanceWidths;
        }

        public FontHandle Font { get; }
        public double FontSize { get; }
        public uint[] GlyphIndices { get; }
        public double[] AdvanceWidths { get; }
    }
}
