using System.Collections.Generic;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools.Constraints;
using UnityEngine.TextCore.LowLevel;
using Xamlity.Rendering;
using Xamlity.Unity;
using Is = UnityEngine.TestTools.Constraints.Is;

namespace Xamlity.Editor.Tests
{
    // Development-repository tests: depend on the font in the sample project's Assets folder.
    public class TmpTextMeasurerTests
    {
        private TMP_FontAsset fontAsset;
        private FontHandle font;
        private TmpTextMeasurer measurer;

        [SetUp]
        public void SetUp()
        {
            var source = AssetDatabase.LoadAssetAtPath<Font>("Assets/Fonts/NotoSansKR-Regular.otf");
            Assert.That(source, Is.Not.Null);
            fontAsset = TMP_FontAsset.CreateFontAsset(source, 90, 9, GlyphRenderMode.SDFAA,
                1024, 1024, AtlasPopulationMode.Dynamic, true);
            Assert.That(fontAsset, Is.Not.Null);
            font = new FontHandle(fontAsset);
            measurer = new TmpTextMeasurer();
        }

        [TearDown]
        public void TearDown()
        {
            DestroyFontAsset(fontAsset);
        }

        private static void DestroyFontAsset(TMP_FontAsset asset)
        {
            if (asset == null)
                return;

            var textures = asset.atlasTextures;
            var material = asset.material;
            Object.DestroyImmediate(asset);
            foreach (var texture in textures)
                if (texture != null)
                    Object.DestroyImmediate(texture);
            if (material != null)
                Object.DestroyImmediate(material);
        }

        [TestCase('한')]
        [TestCase('글')]
        [TestCase('A')]
        public void MissingCharactersAreAddedToStandaloneFontAndAtlas(char character)
        {
            Assert.That(fontAsset.characterLookupTable.ContainsKey(character), Is.False);

            Assert.That(measurer.TryGetGlyph(font, character, 24, out var glyph), Is.True);

            Assert.That(glyph.AdvanceWidth, Is.GreaterThan(0));
            Assert.That(fontAsset.characterLookupTable.ContainsKey(character), Is.True);
            var sourceGlyph = fontAsset.characterLookupTable[character].glyph;
            Assert.That(glyph.GlyphIndex, Is.EqualTo(sourceGlyph.index));
            Assert.That(sourceGlyph.glyphRect.width, Is.GreaterThan(0));
            Assert.That(sourceGlyph.glyphRect.height, Is.GreaterThan(0));
            Assert.That(fontAsset.atlasTextures[0], Is.Not.Null);
            Assert.That(fontAsset.atlasTextures[0].width, Is.EqualTo(1024));
            Assert.That(fontAsset.atlasTextures[0].height, Is.EqualTo(1024));
        }

        [Test]
        public void FontMetricsScaleWithFontSizeAndFaceScale()
        {
            var face = fontAsset.faceInfo;
            face.scale = 1.5f;
            fontAsset.faceInfo = face;

            var small = measurer.GetFontMetrics(font, 24);
            var large = measurer.GetFontMetrics(font, 48);

            Assert.That(small.Ascent, Is.GreaterThan(0));
            Assert.That(small.Descent, Is.GreaterThan(0));
            Assert.That(small.LineHeight, Is.GreaterThan(0));
            double scale = 24.0 / face.pointSize * face.scale;
            Assert.That(small.Ascent, Is.EqualTo(face.ascentLine * scale).Within(0.000001));
            Assert.That(small.Descent, Is.EqualTo(-face.descentLine * scale).Within(0.000001));
            Assert.That(small.LineHeight, Is.EqualTo(face.lineHeight * scale).Within(0.000001));
            Assert.That(large.Ascent, Is.EqualTo(small.Ascent * 2));
            Assert.That(large.Descent, Is.EqualTo(small.Descent * 2));
            Assert.That(large.LineHeight, Is.EqualTo(small.LineHeight * 2));
        }

        [Test]
        public void AdvanceScalesWithFontSizeFaceScaleAndGlyphScale()
        {
            Assert.That(measurer.TryGetGlyph(font, '한', 24, out _), Is.True);
            var face = fontAsset.faceInfo;
            face.scale = 1.5f;
            fontAsset.faceInfo = face;
            var sourceGlyph = fontAsset.characterLookupTable['한'].glyph;
            sourceGlyph.scale = 0.75f;

            Assert.That(measurer.TryGetGlyph(font, '한', 24, out var small), Is.True);
            Assert.That(measurer.TryGetGlyph(font, '한', 48, out var large), Is.True);

            double expected = sourceGlyph.metrics.horizontalAdvance *
                (24.0 / face.pointSize * face.scale * sourceGlyph.scale);
            Assert.That(small.AdvanceWidth, Is.EqualTo(expected).Within(0.000001));
            Assert.That(large.AdvanceWidth, Is.EqualTo(small.AdvanceWidth * 2));
        }

        [Test]
        public void CharacterAbsentFromSourceFontReturnsFalse()
        {
            Assert.That(measurer.TryGetGlyph(font, 0xE000, 24, out var glyph), Is.False);
            Assert.That(fontAsset.characterLookupTable.ContainsKey(0xE000), Is.False);
            Assert.That(glyph.GlyphIndex, Is.Zero);
            Assert.That(glyph.AdvanceWidth, Is.Zero);
        }

        [Test]
        public void StaticFontReturnsExistingGlyphButDoesNotAddMissingCharacter()
        {
            Assert.That(measurer.TryGetGlyph(font, 'A', 24, out _), Is.True);
            fontAsset.atlasPopulationMode = AtlasPopulationMode.Static;

            Assert.That(measurer.TryGetGlyph(font, 'A', 24, out var glyph), Is.True);
            Assert.That(glyph.AdvanceWidth, Is.GreaterThan(0));
            Assert.That(measurer.TryGetGlyph(font, '한', 24, out _), Is.False);
            Assert.That(fontAsset.characterLookupTable.ContainsKey('한'), Is.False);
        }

        [Test]
        public void FallbackFontsAreNotQueried()
        {
            var primary = TMP_FontAsset.CreateFontAsset(fontAsset.sourceFontFile, 90, 9,
                GlyphRenderMode.SDFAA, 1024, 1024, AtlasPopulationMode.Dynamic, true);
            try
            {
                Assert.That(measurer.TryGetGlyph(font, '한', 24, out _), Is.True);
                primary.atlasPopulationMode = AtlasPopulationMode.Static;
                primary.fallbackFontAssetTable = new List<TMP_FontAsset> { fontAsset };

                Assert.That(measurer.TryGetGlyph(new FontHandle(primary), '한', 24, out _), Is.False);
            }
            finally
            {
                DestroyFontAsset(primary);
            }
        }

        [Test]
        public void ExistingGlyphLookupsDoNotAllocate()
        {
            Assert.That(measurer.TryGetGlyph(font, '한', 24, out _), Is.True);
            Assert.That(measurer.TryGetGlyph(font, '글', 24, out _), Is.True);
            Assert.That(measurer.TryGetGlyph(font, 'A', 24, out _), Is.True);

            Assert.That(() =>
            {
                measurer.TryGetGlyph(font, '한', 24, out _);
                measurer.TryGetGlyph(font, '글', 24, out _);
                measurer.TryGetGlyph(font, 'A', 24, out _);
            }, Is.Not.AllocatingGCMemory());
        }
    }
}
