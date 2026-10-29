using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools.Constraints;
using Xamlity.Controls;
using Xamlity.Layout;
using Xamlity.Rendering;
using Xamlity.Unity;
using Color = UnityEngine.Color;
using CoreColor = Xamlity.Rendering.Color;
using Is = UnityEngine.TestTools.Constraints.Is;
using Rect = Xamlity.Layout.Rect;

namespace Xamlity.Editor.Tests
{
    public partial class RenderingTests
    {
        private readonly List<Object> imageObjects = new List<Object>();

        private Texture2D CreateImageTexture(int width = 16, int height = 16)
        {
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp
            };
            imageObjects.Add(texture);
            return texture;
        }

        private Sprite CreateImageSprite(Texture2D texture, UnityEngine.Rect rect, Vector4 border = default)
        {
            var sprite = Sprite.Create(texture, rect, Vector2.zero, 100, 0, SpriteMeshType.FullRect, border);
            imageObjects.Add(sprite);
            return sprite;
        }

        private XamlityRoot CreateImageRoot(UnityImageSource source)
        {
            var root = CreateRoot();
            var border = (Border)root.Tree.RootElement;
            border.Background = null;
            border.Child = new Image
            {
                Source = source,
                Stretch = Stretch.Fill,
                Width = 64,
                Height = 32,
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top
            };
            return root;
        }

        private void AssertImageQuad(int index, Rect rectangle, Vector4 uv, double scale = 1)
        {
            var vertices = mesh.vertices;
            var uvs = mesh.uv;
            float left = (float)(rectangle.Left * scale);
            float right = (float)(rectangle.Right * scale);
            float top = (float)(128 - rectangle.Top * scale);
            float bottom = (float)(128 - rectangle.Bottom * scale);
            Assert.That(vertices[index * 4], Is.EqualTo(new Vector3(left, top, 0)));
            Assert.That(vertices[index * 4 + 1], Is.EqualTo(new Vector3(right, top, 0)));
            Assert.That(vertices[index * 4 + 2], Is.EqualTo(new Vector3(right, bottom, 0)));
            Assert.That(vertices[index * 4 + 3], Is.EqualTo(new Vector3(left, bottom, 0)));
            Assert.That(uvs[index * 4], Is.EqualTo(new Vector2(uv.x, uv.w)));
            Assert.That(uvs[index * 4 + 1], Is.EqualTo(new Vector2(uv.z, uv.w)));
            Assert.That(uvs[index * 4 + 2], Is.EqualTo(new Vector2(uv.z, uv.y)));
            Assert.That(uvs[index * 4 + 3], Is.EqualTo(new Vector2(uv.x, uv.y)));
        }

        [Test]
        public void UnityImageSourceUsesPixelDimensionsAndRejectsNull()
        {
            Assert.Throws<System.ArgumentNullException>(() => new UnityImageSource((Texture2D)null));
            Assert.Throws<System.ArgumentNullException>(() => new UnityImageSource((Sprite)null));
            var texture = CreateImageTexture(32, 64);
            var source = new UnityImageSource(texture);
            Assert.That(source.Texture, Is.SameAs(texture));
            Assert.That(source.Sprite, Is.Null);
            Assert.That(source.Width, Is.EqualTo(32));
            Assert.That(source.Height, Is.EqualTo(64));
            var sprite = CreateImageSprite(texture, new UnityEngine.Rect(4, 8, 16, 24));
            source = new UnityImageSource(sprite);
            Assert.That(source.Texture, Is.SameAs(texture));
            Assert.That(source.Sprite, Is.SameAs(sprite));
            Assert.That(source.Width, Is.EqualTo(16));
            Assert.That(source.Height, Is.EqualTo(24));
        }

        [TestCase(false, 1)]
        [TestCase(true, 1)]
        [TestCase(false, 2)]
        [TestCase(true, 2)]
        public void ImageQuadUsesTextureOrSpriteRectUvs(bool useSprite, double scale)
        {
            var texture = CreateImageTexture(32, 64);
            var source = useSprite
                ? new UnityImageSource(CreateImageSprite(texture, new UnityEngine.Rect(4, 16, 16, 32)))
                : new UnityImageSource(texture);
            mesh = new Mesh();
            var context = new MeshDrawingContext();
            context.Begin(128, scale);
            var rect = new Rect(10, 12, 64, 32);
            context.DrawImage(source, rect);
            context.Upload(mesh);

            Assert.That(mesh.vertexCount, Is.EqualTo(4));
            Assert.That(mesh.GetIndices(0), Is.EqualTo(new[] { 0, 1, 2, 0, 2, 3 }));
            AssertImageQuad(0, rect, useSprite ? new Vector4(0.125f, 0.25f, 0.625f, 0.75f)
                : new Vector4(0, 0, 1, 1), scale);
            Assert.That(context.Batches[0].Kind, Is.EqualTo(MeshDrawingContext.BatchKind.Image));
            Assert.That(context.Batches[0].Texture, Is.SameAs(texture));
            Assert.That(mesh.colors32, Is.All.EqualTo(new Color32(255, 255, 255, 255)));
        }

        [TestCase(64, 32, 1)]
        [TestCase(64, 32, 2)]
        [TestCase(6, 6, 1)]
        [TestCase(6, 32, 1)]
        [TestCase(64, 6, 1)]
        [TestCase(8, 8, 1)]
        public void SlicedSpritePreservesBordersAndShrinksThemForSmallTargets(int width, int height, double scale)
        {
            var sprite = CreateImageSprite(CreateImageTexture(), new UnityEngine.Rect(0, 0, 16, 16),
                new Vector4(4, 4, 4, 4));
            mesh = new Mesh();
            var context = new MeshDrawingContext();
            context.Begin(128, scale);
            context.DrawImage(new UnityImageSource(sprite), new Rect(10, 12, width, height));
            context.Upload(mesh);

            double edgeX = System.Math.Min(4, width / 2.0);
            double edgeY = System.Math.Min(4, height / 2.0);
            double[] x = { 10, 10 + edgeX, 10 + width - edgeX, 10 + width };
            double[] y = { 12, 12 + edgeY, 12 + height - edgeY, 12 + height };
            float[] u = { 0, 0.25f, 0.75f, 1 };
            float[] v = { 1, 0.75f, 0.25f, 0 };
            int quad = 0;
            for (int row = 0; row < 3; row++)
                for (int column = 0; column < 3; column++)
                {
                    if (x[column + 1] == x[column] || y[row + 1] == y[row])
                        continue;
                    AssertImageQuad(quad++, new Rect(x[column], y[row], x[column + 1] - x[column], y[row + 1] - y[row]),
                        new Vector4(u[column], v[row + 1], u[column + 1], v[row]), scale);
                }
            Assert.That(quad, Is.EqualTo((width > 8 ? 3 : 2) * (height > 8 ? 3 : 2)));
            Assert.That(mesh.vertexCount, Is.EqualTo(quad * 4));
            Assert.That(mesh.subMeshCount, Is.EqualTo(1));
            Assert.That(mesh.GetIndices(0).Length, Is.EqualTo(quad * 6));
        }

        [Test]
        public void SlicedSpriteUsesAsymmetricBordersAndSubrectInnerUvs()
        {
            var sprite = CreateImageSprite(CreateImageTexture(32, 32), new UnityEngine.Rect(8, 4, 16, 16),
                new Vector4(2, 4, 6, 8));
            mesh = new Mesh();
            var context = new MeshDrawingContext();
            context.Begin(128, 1);
            context.DrawImage(new UnityImageSource(sprite), new Rect(0, 0, 4, 6));
            context.Upload(mesh);

            Assert.That(mesh.vertexCount, Is.EqualTo(16));
            AssertImageQuad(0, new Rect(0, 0, 1, 4), new Vector4(8 / 32f, 12 / 32f, 10 / 32f, 20 / 32f));
            AssertImageQuad(1, new Rect(1, 0, 3, 4), new Vector4(18 / 32f, 12 / 32f, 24 / 32f, 20 / 32f));
            AssertImageQuad(2, new Rect(0, 4, 1, 2), new Vector4(8 / 32f, 4 / 32f, 10 / 32f, 8 / 32f));
            AssertImageQuad(3, new Rect(1, 4, 3, 2), new Vector4(18 / 32f, 4 / 32f, 24 / 32f, 8 / 32f));
        }

        [Test]
        public void ImageBatchesUseKindAndTextureAndMergeOnlyConsecutiveMatches()
        {
            CreateFont();
            var run = MeasureKorean();
            var atlas = new UnityImageSource(fontAsset.atlasTextures[0]);
            var other = new UnityImageSource(CreateImageTexture());
            var brush = new SolidColorBrush(CoreColor.FromRgb(255, 255, 255));
            mesh = new Mesh();
            var context = new MeshDrawingContext();
            context.Begin(128, 1);
            var rect = new Rect(0, 0, 16, 16);
            context.DrawRectangle(brush, rect);
            context.DrawGlyphRun(brush, run, new Point(0, 60));
            context.DrawImage(atlas, rect);
            context.DrawImage(atlas, rect);
            context.DrawImage(other, rect);
            context.DrawImage(atlas, rect);
            context.DrawRectangle(brush, rect);
            context.Upload(mesh);

            Assert.That(mesh.subMeshCount, Is.EqualTo(6));
            var kinds = new[] { MeshDrawingContext.BatchKind.Solid, MeshDrawingContext.BatchKind.Text,
                MeshDrawingContext.BatchKind.Image, MeshDrawingContext.BatchKind.Image,
                MeshDrawingContext.BatchKind.Image, MeshDrawingContext.BatchKind.Solid };
            int[] counts = { 6, 12, 12, 6, 6, 6 };
            int start = 0;
            for (int i = 0; i < kinds.Length; i++)
            {
                Assert.That(context.Batches[i].Kind, Is.EqualTo(kinds[i]));
                Assert.That(context.Batches[i].Start, Is.EqualTo(start));
                Assert.That(context.Batches[i].Count, Is.EqualTo(counts[i]));
                start += counts[i];
            }
            Assert.That(context.Batches[1].Texture, Is.SameAs(context.Batches[2].Texture));
            Assert.That(context.Batches[3].Texture, Is.SameAs(other.Texture));
            Assert.That(context.Batches[4].Texture, Is.SameAs(atlas.Texture));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void ImageClipInterpolatesUvsBeforeScalingAndMultipliesOpacity(bool sliced)
        {
            var texture = CreateImageTexture();
            var source = sliced ? new UnityImageSource(CreateImageSprite(texture, new UnityEngine.Rect(0, 0, 16, 16),
                new Vector4(4, 4, 4, 4))) : new UnityImageSource(texture);
            mesh = new Mesh();
            var context = new MeshDrawingContext();
            context.Begin(128, 2);
            context.PushOpacity(0.5);
            context.PushClip(new Rect(1, 1, 2, 2));
            context.PushOpacity(0.5);
            context.DrawImage(source, new Rect(0, 0, 16, 16));
            context.Pop();
            context.Pop();
            context.Pop();
            context.Upload(mesh);

            Assert.That(mesh.vertexCount, Is.EqualTo(4));
            AssertImageQuad(0, new Rect(1, 1, 2, 2), new Vector4(1 / 16f, 13 / 16f, 3 / 16f, 15 / 16f), 2);
            Assert.That(mesh.colors32, Is.All.EqualTo(new Color32(255, 255, 255, 64)));
        }

        private sealed class OtherImageSource : ImageSource
        {
            public override double Width => 16;
            public override double Height => 16;
        }

        [Test]
        public void UnsupportedEmptyAndFullyClippedImagesProduceNoBatches()
        {
            var source = new UnityImageSource(CreateImageTexture());
            mesh = new Mesh();
            var context = new MeshDrawingContext();
            context.Begin(128, 1);
            context.DrawImage(new OtherImageSource(), new Rect(0, 0, 16, 16));
            context.DrawImage(null, new Rect(0, 0, 16, 16));
            context.DrawImage(source, new Rect(0, 0, 0, 16));
            context.DrawImage(source, new Rect(0, 0, 16, 0));
            context.PushClip(new Rect(20, 20, 10, 10));
            context.DrawImage(source, new Rect(0, 0, 16, 16));
            context.Pop();
            context.Upload(mesh);
            Assert.That(mesh.vertexCount, Is.Zero);
            Assert.That(context.Batches, Is.Empty);
        }

        [Test]
        public void GameCameraDrawsImageTextureAcrossItsTopLeftLayoutRectangle()
        {
            var texture = CreateImageTexture(2, 1);
            texture.SetPixels(new[] { Color.red, Color.green });
            texture.Apply();
            var root = CreateImageRoot(new UnityImageSource(texture));
            root.Refresh(256, 128);
            Render(CreateCamera());

            for (int y = 0; y < 128; y++)
                for (int x = 0; x < 256; x++)
                    AssertPixel(x, y, y < 96 || x >= 64 ? Color.black : x < 32 ? Color.red : Color.green);
        }

        [Test]
        public void GameCameraDrawsSlicedSpriteWithFixedEdgesAndStretchedCenter()
        {
            var texture = CreateImageTexture();
            for (int y = 0; y < 16; y++)
                for (int x = 0; x < 16; x++)
                    texture.SetPixel(x, y, x < 4 || x >= 12 || y < 4 || y >= 12 ? Color.red : Color.green);
            texture.Apply();
            var sprite = CreateImageSprite(texture, new UnityEngine.Rect(0, 0, 16, 16), new Vector4(4, 4, 4, 4));
            var root = CreateImageRoot(new UnityImageSource(sprite));
            root.Refresh(256, 128);
            Render(CreateCamera());

            for (int y = 0; y < 128; y++)
                for (int x = 0; x < 256; x++)
                {
                    Color expected = y < 96 || x >= 64 ? Color.black
                        : x < 4 || x >= 60 || y < 100 || y >= 124 ? Color.red : Color.green;
                    AssertPixel(x, y, expected);
                }
        }

        [TestCase(false)]
        [TestCase(true)]
        public void UnchangedImageRefreshReusesMeshWithoutAllocating(bool sliced)
        {
            var texture = CreateImageTexture();
            var source = sliced ? new UnityImageSource(CreateImageSprite(texture, new UnityEngine.Rect(0, 0, 16, 16),
                new Vector4(4, 4, 4, 4))) : new UnityImageSource(texture);
            var root = CreateImageRoot(source);
            root.Refresh(256, 128);
            var originalMesh = root.Mesh;
            Assert.That(originalMesh.vertexCount, Is.EqualTo(sliced ? 36 : 4));

            long before = System.GC.GetAllocatedBytesForCurrentThread();
            root.Refresh(256, 128);
            long allocated = System.GC.GetAllocatedBytesForCurrentThread() - before;
            Assert.That(allocated, Is.Zero);
            Assert.That(() => root.Refresh(256, 128), Is.Not.AllocatingGCMemory());
            Assert.That(root.MeshRebuildCount, Is.EqualTo(1));
            Assert.That(root.Mesh, Is.SameAs(originalMesh));
        }
    }
}
