using System;
using TMPro;
using Xamlity.Rendering;

namespace Xamlity.Unity
{
    internal sealed class TmpTextMeasurer : ITextMeasurer
    {
        public FontMetrics GetFontMetrics(FontHandle font, double fontSize)
        {
            var fontAsset = (TMP_FontAsset)font.Source;
            var face = fontAsset.faceInfo;
            double scale = fontSize / face.pointSize * face.scale;
            return new FontMetrics(face.ascentLine * scale, Math.Abs(face.descentLine) * scale,
                face.lineHeight * scale);
        }

        public bool TryGetGlyph(FontHandle font, int codePoint, double fontSize, out GlyphMetrics glyph)
        {
            var fontAsset = (TMP_FontAsset)font.Source;
            uint unicode = (uint)codePoint;
            if (!fontAsset.characterLookupTable.TryGetValue(unicode, out TMP_Character character))
            {
                if (fontAsset.atlasPopulationMode == AtlasPopulationMode.Dynamic)
                    fontAsset.TryAddCharacters(new[] { unicode });

                if (!fontAsset.characterLookupTable.TryGetValue(unicode, out character))
                {
                    glyph = default;
                    return false;
                }
            }

            var face = fontAsset.faceInfo;
            var sourceGlyph = character.glyph;
            double scale = fontSize / face.pointSize * face.scale * sourceGlyph.scale;
            glyph = new GlyphMetrics(sourceGlyph.index, sourceGlyph.metrics.horizontalAdvance * scale);
            return true;
        }
    }
}
