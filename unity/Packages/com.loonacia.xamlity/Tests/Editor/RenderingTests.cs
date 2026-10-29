using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.TestTools.Constraints;
using UnityEngine.TextCore.LowLevel;
using Xamlity.Controls;
using Xamlity.Rendering;
using Xamlity.Unity;
using Color = UnityEngine.Color;
using CoreColor = Xamlity.Rendering.Color;
using Is = UnityEngine.TestTools.Constraints.Is;
using Rect = Xamlity.Layout.Rect;
using Point = Xamlity.Layout.Point;

namespace Xamlity.Editor.Tests
{
    public class RenderingTests
    {
        private const string TopBandPath = "Packages/com.loonacia.xamlity/Tests/Editor/TestAssets/TopBand.xaml";
        private const string KoreanTextPath = "Packages/com.loonacia.xamlity/Tests/Editor/TestAssets/KoreanText.xaml";
        private readonly List<GameObject> objects = new List<GameObject>();
        private Mesh mesh;
        private RenderTexture target;
        private Texture2D pixels;
        private RenderTexture previousTarget;
        private TMP_FontAsset fontAsset;

        [SetUp]
        public void SetUp()
        {
            previousTarget = RenderTexture.active;
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var gameObject in objects)
            {
                var root = gameObject.GetComponent<XamlityRoot>();
                if (root != null)
                    root.Unregister();
                Object.DestroyImmediate(gameObject);
            }
            objects.Clear();
            RenderTexture.active = previousTarget;
            if (target != null)
            {
                target.Release();
                Object.DestroyImmediate(target);
            }
            if (pixels != null)
                Object.DestroyImmediate(pixels);
            if (mesh != null)
                Object.DestroyImmediate(mesh);
            if (fontAsset != null)
            {
                var textures = fontAsset.atlasTextures;
                var material = fontAsset.material;
                Object.DestroyImmediate(fontAsset);
                foreach (var texture in textures)
                    if (texture != null)
                        Object.DestroyImmediate(texture);
                if (material != null)
                    Object.DestroyImmediate(material);
            }
        }

        [TestCase(1024)]
        [TestCase(128)]
        public void KoreanGlyphQuadsUseMetricsPaddingAdvancesAndCurrentAtlas(int atlasSize)
        {
            CreateFont(atlasSize);
            var face = fontAsset.faceInfo;
            face.scale = 1.25f;
            fontAsset.faceInfo = face;
            var run = MeasureKorean();
            fontAsset.glyphLookupTable[run.GlyphIndices[1]].scale = 0.75f;
            run = MeasureKorean();
            var brush = new SolidColorBrush(CoreColor.FromArgb(128, 10, 20, 30));
            var origin = new Point(11, 70);
            mesh = new Mesh();
            var context = new MeshDrawingContext();
            context.Begin(128);
            context.DrawGlyphRun(brush, run, origin);
            context.Upload(mesh);

            Assert.That(mesh.vertexCount, Is.EqualTo(8));
            Assert.That(mesh.colors32, Is.All.EqualTo(new Color32(10, 20, 30, 128)));
            Assert.That(mesh.subMeshCount, Is.EqualTo(atlasSize == 128 ? 2 : 1));
            var vertices = mesh.vertices;
            var uvs = mesh.uv;
            double advance = 0;
            for (int i = 0; i < 2; i++)
            {
                var glyph = fontAsset.glyphLookupTable[run.GlyphIndices[i]];
                var atlas = fontAsset.atlasTextures[glyph.atlasIndex];
                int batch = atlasSize == 128 ? i : 0;
                Assert.That(context.Batches[batch].Texture, Is.SameAs(atlas));
                Assert.That(atlas.width, Is.EqualTo(atlasSize));
                double scale = 48.0 / face.pointSize * face.scale * glyph.scale;
                double left = origin.X + advance + (glyph.metrics.horizontalBearingX - fontAsset.atlasPadding) * scale;
                double top = 128 - origin.Y + (glyph.metrics.horizontalBearingY + fontAsset.atlasPadding) * scale;
                Assert.That(vertices[i * 4].x, Is.EqualTo(left).Within(0.0001));
                Assert.That(vertices[i * 4].y, Is.EqualTo(top).Within(0.0001));
                Assert.That(vertices[i * 4 + 2].x,
                    Is.EqualTo(left + (glyph.metrics.width + 2 * fontAsset.atlasPadding) * scale).Within(0.0001));
                Assert.That(vertices[i * 4 + 2].y,
                    Is.EqualTo(top - (glyph.metrics.height + 2 * fontAsset.atlasPadding) * scale).Within(0.0001));
                Assert.That(uvs[i * 4].x,
                    Is.EqualTo((glyph.glyphRect.x - fontAsset.atlasPadding) / (float)atlas.width));
                Assert.That(uvs[i * 4].y,
                    Is.EqualTo((glyph.glyphRect.y + glyph.glyphRect.height + fontAsset.atlasPadding) / (float)atlas.height));
                advance += run.AdvanceWidths[i];
            }
            foreach (var uv in uvs)
            {
                Assert.That(uv.x, Is.InRange(0f, 1f));
                Assert.That(uv.y, Is.InRange(0f, 1f));
            }
        }

        [Test]
        public void SolidAndTextBatchesKeepOrderAndMergeOnlyConsecutiveMatches()
        {
            CreateFont();
            var run = MeasureKorean();
            var brush = new SolidColorBrush(CoreColor.FromRgb(255, 255, 255));
            mesh = new Mesh();
            var context = new MeshDrawingContext();
            context.Begin(128);
            context.DrawRectangle(brush, new Rect(0, 0, 100, 100));
            context.DrawGlyphRun(brush, run, new Point(0, 60));
            context.DrawGlyphRun(brush, run, new Point(0, 120));
            context.DrawRectangle(brush, new Rect(0, 0, 10, 10));
            context.Upload(mesh);

            Assert.That(mesh.subMeshCount, Is.EqualTo(3));
            Assert.That(context.Batches[0].Texture, Is.Null);
            Assert.That(context.Batches[1].Texture, Is.SameAs(fontAsset.atlasTextures[0]));
            Assert.That(context.Batches[2].Texture, Is.Null);
            Assert.That(mesh.GetIndices(0), Is.EqualTo(new[] { 0, 1, 2, 0, 2, 3 }));
            Assert.That(mesh.GetIndices(1).Length, Is.EqualTo(24));
            Assert.That(mesh.GetIndices(1)[0], Is.EqualTo(4));
            Assert.That(mesh.GetIndices(2), Is.EqualTo(new[] { 20, 21, 22, 20, 22, 23 }));

            context.Begin(128);
            context.DrawGlyphRun(brush, run, new Point(0, 60));
            context.Upload(mesh);
            Assert.That(mesh.subMeshCount, Is.EqualTo(1));
            Assert.That(context.Batches.Count, Is.EqualTo(1));
            Assert.That(mesh.vertexCount, Is.EqualTo(8));
        }

        [TestCase(1024)]
        [TestCase(128)]
        public void GameCameraDrawsKoreanTextInsideRenderSize(int atlasSize)
        {
            CreateFont(atlasSize);
            var root = CreateRoot(KoreanTextPath, fontAsset);
            Assert.That(fontAsset.characterLookupTable.ContainsKey('한'), Is.False);
            root.Refresh(256, 128);
            var size = root.Tree.RootElement.RenderSize;
            Assert.That(size.Width, Is.GreaterThan(0).And.LessThan(256));
            Assert.That(size.Height, Is.GreaterThan(0).And.LessThan(128));
            Assert.That(root.Mesh.subMeshCount, Is.EqualTo(atlasSize == 128 ? 2 : 1));
            Render(CreateCamera());

            var brightPixels = new int[2];
            for (int y = 0; y < 128; y++)
                for (int x = 0; x < 256; x++)
                {
                    bool inside = x < size.Width && 127 - y < size.Height;
                    if (inside && pixels.GetPixel(x, y).r > 128 / 255f)
                        brightPixels[x < size.Width / 2 ? 0 : 1]++;
                    if (!inside)
                        AssertPixel(x, y, Color.black);
                }
            Assert.That(brightPixels[0], Is.GreaterThan(100), "The first Korean glyph must be visible.");
            Assert.That(brightPixels[1], Is.GreaterThan(100), "The second Korean glyph must be visible.");
        }

        [Test]
        public void GameCameraPreservesGraySrgbTextInLinearColorSpace()
        {
            Assert.That(QualitySettings.activeColorSpace, Is.EqualTo(ColorSpace.Linear),
                "This sRGB pixel regression test requires a Linear color space project.");
            CreateFont();
            var root = CreateRoot(KoreanTextPath, fontAsset);
            ((TextBlock)root.Tree.RootElement).Foreground = new SolidColorBrush(CoreColor.FromRgb(128, 128, 128));
            root.Refresh(256, 128);
            var size = root.Tree.RootElement.RenderSize;
            Render(CreateCamera());

            float maximumRed = 0;
            for (int y = 0; y < 128; y++)
                for (int x = 0; x < 256; x++)
                    if (x < size.Width && 127 - y < size.Height)
                        maximumRed = Mathf.Max(maximumRed, pixels.GetPixel(x, y).r);

            Assert.That(maximumRed, Is.EqualTo(128 / 255f).Within(3 / 255f),
                "Fully covered glyph pixels must preserve the sRGB foreground color.");
        }

        [Test]
        public void UnchangedTextRefreshReusesMeshWithoutAllocating()
        {
            CreateFont();
            var root = CreateRoot(KoreanTextPath, fontAsset);
            root.Refresh(256, 128);
            var originalMesh = root.Mesh;
            Assert.That(originalMesh.vertexCount, Is.EqualTo(8));

            Assert.That(() => root.Refresh(256, 128), Is.Not.AllocatingGCMemory());

            Assert.That(root.MeshRebuildCount, Is.EqualTo(1));
            Assert.That(root.Mesh, Is.SameAs(originalMesh));
            Assert.That(root.Tree.IsRenderDirty, Is.False);
        }

        private void CreateFont(int atlasSize = 1024)
        {
            var source = AssetDatabase.LoadAssetAtPath<Font>("Assets/Fonts/NotoSansKR-Regular.otf");
            Assert.That(source, Is.Not.Null);
            fontAsset = TMP_FontAsset.CreateFontAsset(source, 90, 9, GlyphRenderMode.SDFAA,
                atlasSize, atlasSize, AtlasPopulationMode.Dynamic, true);
            Assert.That(fontAsset, Is.Not.Null);
        }

        private GlyphRun MeasureKorean()
        {
            var font = new FontHandle(fontAsset);
            var measurer = new TmpTextMeasurer();
            Assert.That(measurer.TryGetGlyph(font, '한', 48, out var first), Is.True);
            Assert.That(measurer.TryGetGlyph(font, '글', 48, out var second), Is.True);
            return new GlyphRun(font, 48, new[] { first.GlyphIndex, second.GlyphIndex },
                new[] { first.AdvanceWidth, second.AdvanceWidth });
        }

        [Test]
        public void RectangleHasFourFlippedVerticesAndByteColors()
        {
            mesh = new Mesh();
            var context = new MeshDrawingContext();
            context.Begin(128);
            context.DrawRectangle(new SolidColorBrush(CoreColor.FromArgb(128, 10, 20, 30)),
                new Rect(5, 8, 20, 40));
            context.Upload(mesh);

            Assert.That(mesh.vertices, Is.EqualTo(new[]
            {
                new Vector3(5, 120, 0), new Vector3(25, 120, 0),
                new Vector3(25, 80, 0), new Vector3(5, 80, 0)
            }));
            Assert.That(mesh.colors32, Is.All.EqualTo(new Color32(10, 20, 30, 128)));
            Assert.That(mesh.triangles, Is.EqualTo(new[] { 0, 1, 2, 0, 2, 3 }));
        }

        [Test]
        public void PartialClipCutsRectangleVerticesWithoutChangingColorOrBatch()
        {
            mesh = new Mesh();
            var context = new MeshDrawingContext();
            context.Begin(128);
            context.PushClip(new Rect(10, 16, 10, 20));
            context.DrawRectangle(new SolidColorBrush(CoreColor.FromArgb(128, 10, 20, 30)),
                new Rect(5, 8, 20, 40));
            context.Pop();
            context.Upload(mesh);

            Assert.That(mesh.vertices, Is.EqualTo(new[]
            {
                new Vector3(10, 112, 0), new Vector3(20, 112, 0),
                new Vector3(20, 92, 0), new Vector3(10, 92, 0)
            }));
            Assert.That(mesh.colors32, Is.All.EqualTo(new Color32(10, 20, 30, 128)));
            Assert.That(mesh.uv, Is.All.EqualTo(Vector2.zero));
            Assert.That(mesh.subMeshCount, Is.EqualTo(1));
            Assert.That(mesh.triangles, Is.EqualTo(new[] { 0, 1, 2, 0, 2, 3 }));
        }

        [TestCase(20, 0, 10, 10)]
        [TestCase(10, 0, 10, 10)]
        [TestCase(0, 10, 10, 10)]
        [TestCase(0, 0, 0, 10)]
        [TestCase(0, 0, 10, 0)]
        public void FullyClippedRectangleProducesNoVerticesOrBatches(double x, double y, double width, double height)
        {
            mesh = new Mesh();
            var context = new MeshDrawingContext();
            context.Begin(128);
            context.PushClip(new Rect(x, y, width, height));
            context.DrawRectangle(new SolidColorBrush(CoreColor.FromRgb(255, 0, 0)), new Rect(0, 0, 10, 10));
            context.Upload(mesh);

            Assert.That(mesh.vertexCount, Is.Zero);
            Assert.That(mesh.subMeshCount, Is.Zero);
            Assert.That(context.Batches, Is.Empty);
        }

        [Test]
        public void NestedClipsIntersectAndMixedOpacityPopsRestoreBothStates()
        {
            mesh = new Mesh();
            var context = new MeshDrawingContext();
            var brush = new SolidColorBrush(CoreColor.FromRgb(255, 0, 0));
            var rect = new Rect(0, 0, 100, 100);
            context.Begin(128);
            context.PushClip(new Rect(10, 20, 50, 50));
            context.PushOpacity(0.5);
            context.PushClip(new Rect(30, 10, 50, 40));
            context.DrawRectangle(brush, rect);
            context.PushClip(new Rect(90, 90, 10, 10));
            context.DrawRectangle(brush, rect);
            context.Pop();
            context.Pop();
            context.DrawRectangle(brush, rect);
            context.Pop();
            context.DrawRectangle(brush, rect);
            context.Pop();
            context.DrawRectangle(brush, rect);
            context.Upload(mesh);

            var vertices = mesh.vertices;
            var colors = mesh.colors32;
            var expected = new[]
            {
                new Rect(30, 20, 30, 30), new Rect(10, 20, 50, 50),
                new Rect(10, 20, 50, 50), rect
            };
            Assert.That(vertices.Length, Is.EqualTo(16));
            for (int i = 0; i < expected.Length; i++)
            {
                Assert.That(vertices[i * 4], Is.EqualTo(new Vector3((float)expected[i].Left,
                    (float)(128 - expected[i].Top), 0)));
                Assert.That(vertices[i * 4 + 2], Is.EqualTo(new Vector3((float)expected[i].Right,
                    (float)(128 - expected[i].Bottom), 0)));
                for (int j = 0; j < 4; j++)
                    Assert.That(colors[i * 4 + j].a, Is.EqualTo(i < 2 ? 128 : 255));
            }
            Assert.That(mesh.subMeshCount, Is.EqualTo(1));
            Assert.That(mesh.GetIndices(0).Length, Is.EqualTo(24));
            Assert.Throws<System.InvalidOperationException>(() => context.Pop());
        }

        [Test]
        public void ClippedGlyphInterpolatesUvsOnAllFourEdges()
        {
            CreateFont();
            var measured = MeasureKorean();
            var run = new GlyphRun(measured.Font, measured.FontSize,
                new[] { measured.GlyphIndices[0] }, new[] { measured.AdvanceWidths[0] });
            var brush = new SolidColorBrush(CoreColor.FromRgb(255, 255, 255));
            var origin = new Point(10, 70);
            mesh = new Mesh();
            var context = new MeshDrawingContext();
            context.Begin(128);
            context.DrawGlyphRun(brush, run, origin);
            context.Upload(mesh);
            var originalVertices = mesh.vertices;
            var originalUvs = mesh.uv;
            float left = Mathf.Lerp(originalVertices[0].x, originalVertices[2].x, 0.25f);
            float right = Mathf.Lerp(originalVertices[0].x, originalVertices[2].x, 0.75f);
            float top = Mathf.Lerp(originalVertices[0].y, originalVertices[2].y, 0.2f);
            float bottom = Mathf.Lerp(originalVertices[0].y, originalVertices[2].y, 0.6f);
            context.Begin(128);
            context.PushClip(new Rect(left, 128 - top, right - left, top - bottom));
            context.DrawGlyphRun(brush, run, origin);
            context.Pop();
            context.Upload(mesh);

            Assert.That(mesh.vertexCount, Is.EqualTo(4));
            Assert.That(mesh.vertices, Is.EqualTo(new[]
            {
                new Vector3(left, top, 0), new Vector3(right, top, 0),
                new Vector3(right, bottom, 0), new Vector3(left, bottom, 0)
            }));
            var uvs = mesh.uv;
            float[] horizontalFractions = { 0.25f, 0.75f, 0.75f, 0.25f };
            float[] verticalFractions = { 0.2f, 0.2f, 0.6f, 0.6f };
            for (int i = 0; i < 4; i++)
            {
                Assert.That(uvs[i].x, Is.EqualTo(Mathf.Lerp(originalUvs[0].x, originalUvs[2].x,
                    horizontalFractions[i])).Within(0.00001));
                Assert.That(uvs[i].y, Is.EqualTo(Mathf.Lerp(originalUvs[0].y, originalUvs[2].y,
                    verticalFractions[i])).Within(0.00001));
            }
            Assert.That(context.Batches[0].Texture, Is.SameAs(fontAsset.atlasTextures[0]));
        }

        [Test]
        public void FullyClippedGlyphsProduceNoBatchAndDoNotSplitSolidBatch()
        {
            CreateFont();
            var run = MeasureKorean();
            var brush = new SolidColorBrush(CoreColor.FromRgb(255, 255, 255));
            mesh = new Mesh();
            var context = new MeshDrawingContext();
            context.Begin(128);
            context.PushClip(new Rect(1000, 1000, 10, 10));
            context.DrawGlyphRun(brush, run, new Point(0, 60));
            context.Upload(mesh);
            Assert.That(mesh.vertexCount, Is.Zero);
            Assert.That(context.Batches, Is.Empty);
            context.Pop();
            context.DrawRectangle(brush, new Rect(0, 0, 10, 10));
            context.PushClip(new Rect(1000, 1000, 10, 10));
            context.DrawGlyphRun(brush, run, new Point(0, 60));
            context.Pop();
            context.DrawRectangle(brush, new Rect(0, 0, 10, 10));
            context.Upload(mesh);

            Assert.That(mesh.vertexCount, Is.EqualTo(8));
            Assert.That(mesh.subMeshCount, Is.EqualTo(1));
            Assert.That(context.Batches[0].Texture, Is.Null);
        }

        [Test]
        public void BeginClearsClipAndOpacityStates()
        {
            mesh = new Mesh();
            var context = new MeshDrawingContext();
            context.Begin(128);
            context.PushOpacity(0.5);
            context.PushClip(new Rect(0, 0, 1, 1));
            context.Begin(128);
            context.DrawRectangle(new SolidColorBrush(CoreColor.FromRgb(255, 0, 0)), new Rect(0, 0, 10, 10));
            context.Upload(mesh);

            Assert.That(mesh.vertices[2], Is.EqualTo(new Vector3(10, 118, 0)));
            Assert.That(mesh.colors32, Is.All.EqualTo(new Color32(255, 0, 0, 255)));
            Assert.Throws<System.InvalidOperationException>(() => context.Pop());
        }

        [Test]
        public void GameCameraClipsOversizedRedChildAtBorderRightEdge()
        {
            var root = CreateRoot();
            var border = (Border)root.Tree.RootElement;
            border.Width = 64;
            border.Height = 128;
            border.HorizontalAlignment = Xamlity.Layout.HorizontalAlignment.Left;
            border.ClipToBounds = true;
            border.Background = null;
            border.Child = new Border { Width = 128, Height = 128,
                HorizontalAlignment = Xamlity.Layout.HorizontalAlignment.Left,
                Background = new SolidColorBrush(CoreColor.FromRgb(255, 0, 0)) };
            root.Refresh(256, 128);
            Render(CreateCamera());

            for (int y = 0; y < 128; y++)
                for (int x = 0; x < 256; x++)
                    AssertPixel(x, y, x < 64 ? Color.red : Color.black);
        }

        [Test]
        public void UnchangedClipRefreshReusesMeshWithoutAllocating()
        {
            var root = CreateRoot();
            ((Border)root.Tree.RootElement).ClipToBounds = true;
            root.Refresh(256, 128);
            var originalMesh = root.Mesh;

            Assert.That(() => root.Refresh(256, 128), Is.Not.AllocatingGCMemory());
            Assert.That(root.MeshRebuildCount, Is.EqualTo(1));
            Assert.That(root.Mesh, Is.SameAs(originalMesh));
        }

        [Test]
        public void NestedOpacityMultipliesAlphaAndPopRestoresIt()
        {
            mesh = new Mesh();
            var context = new MeshDrawingContext();
            var brush = new SolidColorBrush(CoreColor.FromRgb(10, 20, 30));
            var rect = new Rect(0, 0, 10, 10);
            context.Begin(128);
            context.DrawRectangle(brush, rect);
            context.PushOpacity(0.5);
            context.DrawRectangle(brush, rect);
            context.PushOpacity(0.5);
            context.DrawRectangle(brush, rect);
            context.Pop();
            context.DrawRectangle(brush, rect);
            context.Pop();
            context.DrawRectangle(brush, rect);
            context.Upload(mesh);

            var colors = mesh.colors32;
            byte[] alphas = { 255, 128, 64, 128, 255 };
            Assert.That(colors.Length, Is.EqualTo(alphas.Length * 4));
            for (int i = 0; i < colors.Length; i++)
                Assert.That(colors[i], Is.EqualTo(new Color32(10, 20, 30, alphas[i / 4])));
            Assert.That(mesh.subMeshCount, Is.EqualTo(1));
            Assert.Throws<System.InvalidOperationException>(() => context.Pop());
        }

        [TestCase(0.5, 1.0, 64)]
        [TestCase(2.0, 0.25, 64)]
        [TestCase(2.0, 1.0, 128)]
        [TestCase(-1.0, 1.0, 0)]
        [TestCase(0.0, 1.0, 0)]
        [TestCase(double.NaN, 1.0, 0)]
        public void OpacityProductIsClampedAndMultipliesBrushAlpha(double first, double second, int alpha)
        {
            mesh = new Mesh();
            var context = new MeshDrawingContext();
            context.Begin(128);
            context.PushOpacity(first);
            context.PushOpacity(second);
            context.DrawRectangle(new SolidColorBrush(CoreColor.FromArgb(128, 10, 20, 30)),
                new Rect(0, 0, 10, 10));
            context.Upload(mesh);

            Assert.That(mesh.colors32, Is.All.EqualTo(new Color32(10, 20, 30, (byte)alpha)));
        }

        [Test]
        public void BeginClearsOpacityStack()
        {
            mesh = new Mesh();
            var context = new MeshDrawingContext();
            Assert.Throws<System.InvalidOperationException>(() => context.Pop());
            context.Begin(128);
            context.PushOpacity(0.5);
            context.PushOpacity(0.25);
            context.Begin(128);
            context.DrawRectangle(new SolidColorBrush(CoreColor.FromRgb(10, 20, 30)),
                new Rect(0, 0, 10, 10));
            context.Upload(mesh);

            Assert.That(mesh.colors32, Is.All.EqualTo(new Color32(10, 20, 30, 255)));
            Assert.Throws<System.InvalidOperationException>(() => context.Pop());
        }

        [Test]
        public void GlyphOpacityMultipliesBrushAlpha()
        {
            CreateFont();
            var run = MeasureKorean();
            mesh = new Mesh();
            var context = new MeshDrawingContext();
            context.Begin(128);
            context.PushOpacity(0.5);
            context.DrawGlyphRun(new SolidColorBrush(CoreColor.FromArgb(128, 10, 20, 30)), run, new Point(0, 60));
            context.Pop();
            context.Upload(mesh);

            Assert.That(mesh.vertexCount, Is.EqualTo(8));
            Assert.That(mesh.colors32, Is.All.EqualTo(new Color32(10, 20, 30, 64)));
        }

        [Test]
        public void UnchangedOpacityRefreshReusesMeshWithoutAllocating()
        {
            var root = CreateRoot();
            ((Border)root.Tree.RootElement).Opacity = 0.5;
            root.Refresh(256, 128);
            var originalMesh = root.Mesh;

            long before = System.GC.GetAllocatedBytesForCurrentThread();
            root.Refresh(256, 128);
            long allocated = System.GC.GetAllocatedBytesForCurrentThread() - before;
            Assert.That(allocated, Is.Zero);
            Assert.That(() => root.Refresh(256, 128), Is.Not.AllocatingGCMemory());

            Assert.That(root.MeshRebuildCount, Is.EqualTo(1));
            Assert.That(root.Mesh, Is.SameAs(originalMesh));
            Assert.That(root.Mesh.colors32, Is.All.EqualTo(new Color32(255, 0, 0, 128)));
        }

        [Test]
        public void GameCameraBlendsHalfOpacityRedBandInLinearColorSpace()
        {
            Assert.That(QualitySettings.activeColorSpace, Is.EqualTo(ColorSpace.Linear),
                "This opacity pixel regression test requires a Linear color space project.");
            var root = CreateRoot();
            root.Refresh(256, 128);
            ((Border)root.Tree.RootElement).Opacity = 0.5;
            root.Refresh(256, 128);
            Assert.That(root.MeshRebuildCount, Is.EqualTo(2));
            Render(CreateCamera());

            // Linear blending encodes half red as approximately 188 in the sRGB target.
            for (int y = 0; y < 128; y++)
                for (int x = 0; x < 256; x++)
                {
                    Color actual = pixels.GetPixel(x, y);
                    Assert.That(actual.r, Is.EqualTo(y >= 64 ? 188 / 255f : 0).Within(3 / 255f));
                    Assert.That(actual.g, Is.Zero);
                    Assert.That(actual.b, Is.Zero);
                }
        }

        [Test]
        public void ConsecutiveSolidCommandsShareOneBatchAndKeepTheirOrder()
        {
            mesh = new Mesh();
            var context = new MeshDrawingContext();
            context.Begin(128);
            context.DrawRectangle(new SolidColorBrush(CoreColor.FromRgb(255, 0, 0)), new Rect(0, 0, 64, 64));
            context.DrawRectangle(new SolidColorBrush(CoreColor.FromRgb(0, 0, 255)), new Rect(0, 0, 32, 32));
            context.Upload(mesh);

            Assert.That(mesh.subMeshCount, Is.EqualTo(1));
            Assert.That(mesh.vertexCount, Is.EqualTo(8));
            Assert.That(mesh.GetIndices(0), Is.EqualTo(new[] { 0, 1, 2, 0, 2, 3, 4, 5, 6, 4, 6, 7 }));
            Assert.That(mesh.colors32[0], Is.EqualTo(new Color32(255, 0, 0, 255)));
            Assert.That(mesh.colors32[4], Is.EqualTo(new Color32(0, 0, 255, 255)));

            context.Begin(128);
            context.Upload(mesh);
            Assert.That(mesh.vertexCount, Is.Zero);
            Assert.That(mesh.subMeshCount, Is.Zero);
        }

        [Test]
        public void UnchangedRefreshReusesMeshWithoutAllocating()
        {
            var root = CreateRoot();
            root.Refresh(256, 128);
            var originalMesh = root.Mesh;
            Assert.That(root.MeshRebuildCount, Is.EqualTo(1));

            Assert.That(() => root.Refresh(256, 128), Is.Not.AllocatingGCMemory());

            Assert.That(root.MeshRebuildCount, Is.EqualTo(1));
            Assert.That(root.Mesh, Is.SameAs(originalMesh));
            Assert.That(root.Tree.IsRenderDirty, Is.False);
        }

        [Test]
        public void ResizeAndBrushChangeRebuildTheExistingMesh()
        {
            var root = CreateRoot();
            root.Refresh(256, 128);
            var originalMesh = root.Mesh;
            root.Refresh(256, 256);
            Assert.That(root.MeshRebuildCount, Is.EqualTo(2));
            Assert.That(root.Mesh.vertices[0].y, Is.EqualTo(256));

            ((Border)root.Tree.RootElement).Background = new SolidColorBrush(CoreColor.FromRgb(0, 0, 255));
            root.Refresh(256, 256);
            Assert.That(root.MeshRebuildCount, Is.EqualTo(3));
            Assert.That(root.Mesh, Is.SameAs(originalMesh));
            Assert.That(root.Mesh.colors32, Is.All.EqualTo(new Color32(0, 0, 255, 255)));
        }

        [Test]
        public void RegistrationIsUniqueAndReregistrationMovesRootToTheEnd()
        {
            var first = CreateRoot();
            var second = CreateRoot();
            first.Register();
            first.Unregister();
            Assert.That(XamlityRoot.ActiveRoots, Has.No.Member(first));
            first.Register();
            Assert.That(XamlityRoot.ActiveRoots[XamlityRoot.ActiveRoots.Count - 2], Is.SameAs(second));
            Assert.That(XamlityRoot.ActiveRoots[XamlityRoot.ActiveRoots.Count - 1], Is.SameAs(first));
        }

        [Test]
        public void GameCameraDrawsRedTopHalfAndBlackBottomHalf()
        {
            var root = CreateRoot();
            root.Refresh(256, 128);
            var camera = CreateCamera();
            Render(camera);

            for (int y = 0; y < 128; y++)
                for (int x = 0; x < 256; x++)
                    AssertPixel(x, y, y >= 64 ? Color.red : Color.black);
        }

        [Test]
        public void GameCameraPreservesGraySrgbBackgroundInLinearColorSpace()
        {
            Assert.That(QualitySettings.activeColorSpace, Is.EqualTo(ColorSpace.Linear),
                "This sRGB pixel regression test requires a Linear color space project.");
            var root = CreateRoot();
            ((Border)root.Tree.RootElement).Background = new SolidColorBrush(CoreColor.FromRgb(128, 128, 128));
            root.Refresh(256, 128);
            Render(CreateCamera());

            Color actual = pixels.GetPixel(128, 96);
            Assert.That(actual.r, Is.EqualTo(128 / 255f).Within(2 / 255f), "Red inside the gray band");
            Assert.That(actual.g, Is.EqualTo(128 / 255f).Within(2 / 255f), "Green inside the gray band");
            Assert.That(actual.b, Is.EqualTo(128 / 255f).Within(2 / 255f), "Blue inside the gray band");
        }

        [Test]
        public void RegisteredRootsBlendInRegistrationOrder()
        {
            var first = CreateRoot();
            first.Refresh(256, 128);
            var second = CreateRoot();
            ((Border)second.Tree.RootElement).Background = new SolidColorBrush(CoreColor.FromArgb(128, 0, 0, 255));
            second.Refresh(256, 128);
            var camera = CreateCamera();
            Render(camera);
            // ReadPixels returns the sRGB encoding of the linear blended result.
            var expected = QualitySettings.activeColorSpace == ColorSpace.Linear
                ? new Color(127 / 255f, 0, 128 / 255f).gamma
                : new Color(127 / 255f, 0, 128 / 255f);
            AssertPixel(128, 96, expected);
            AssertPixel(128, 32, Color.black);
        }

        private XamlityRoot CreateRoot(string path = TopBandPath, TMP_FontAsset defaultFont = null)
        {
            var gameObject = new GameObject("Xamlity test root");
            objects.Add(gameObject);
            var root = gameObject.AddComponent<XamlityRoot>();
            var asset = AssetDatabase.LoadAssetAtPath<XamlAsset>(path);
            Assert.That(asset, Is.Not.Null);
            root.Build(asset, defaultFont);
            root.Register();
            return root;
        }

        private Camera CreateCamera()
        {
            Assert.That(SystemInfo.graphicsDeviceType, Is.Not.EqualTo(GraphicsDeviceType.Null),
                "Pixel rendering requires a graphics device; the batchmode editor has none.");
            var gameObject = new GameObject("Xamlity test camera");
            objects.Add(gameObject);
            var camera = gameObject.AddComponent<Camera>();
            camera.enabled = false;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Color.black;
            camera.cullingMask = 0;
            camera.allowHDR = false;
            camera.allowMSAA = false;
            target = new RenderTexture(256, 128, 24, RenderTextureFormat.ARGB32);
            target.Create();
            camera.targetTexture = target;

            var renderer = camera.GetUniversalAdditionalCameraData().scriptableRenderer;
            Assert.That(renderer, Is.Not.Null, "Pixel rendering requires an active URP renderer.");
            var property = typeof(ScriptableRenderer).GetProperty("rendererFeatures",
                BindingFlags.Instance | BindingFlags.NonPublic);
            var features = (List<ScriptableRendererFeature>)property.GetValue(renderer);
            var feature = features.Find(item => item is XamlityRendererFeature && item.isActive);
            Assert.That(feature, Is.Not.Null, "The active URP renderer must contain an enabled XamlityRendererFeature.");
            var serialized = new SerializedObject(feature);
            Assert.That(serialized.FindProperty("solidShader").objectReferenceValue, Is.Not.Null,
                "XamlityRendererFeature must reference the Xamlity/Solid shader.");
            Assert.That(serialized.FindProperty("textShader").objectReferenceValue, Is.Not.Null,
                "XamlityRendererFeature must reference the Xamlity/Text shader.");
            return camera;
        }

        private void Render(Camera camera)
        {
            camera.Render();
            RenderTexture.active = target;
            pixels = new Texture2D(256, 128, TextureFormat.RGBA32, false);
            pixels.ReadPixels(new UnityEngine.Rect(0, 0, 256, 128), 0, 0);
            pixels.Apply();
        }

        private void AssertPixel(int x, int y, Color expected)
        {
            Color actual = pixels.GetPixel(x, y);
            Assert.That(actual.r, Is.EqualTo(expected.r).Within(0.02f), $"Red at ({x}, {y})");
            Assert.That(actual.g, Is.EqualTo(expected.g).Within(0.02f), $"Green at ({x}, {y})");
            Assert.That(actual.b, Is.EqualTo(expected.b).Within(0.02f), $"Blue at ({x}, {y})");
        }
    }
}
