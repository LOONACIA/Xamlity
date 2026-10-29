namespace Xamlity.Rendering
{
    public readonly struct GlyphMetrics
    {
        public GlyphMetrics(uint glyphIndex, double advanceWidth)
        {
            GlyphIndex = glyphIndex;
            AdvanceWidth = advanceWidth;
        }

        public uint GlyphIndex { get; }
        public double AdvanceWidth { get; }
    }
}
