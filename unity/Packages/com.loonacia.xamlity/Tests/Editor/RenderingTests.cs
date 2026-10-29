using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.TestTools.Constraints;
using Xamlity.Controls;
using Xamlity.Rendering;
using Xamlity.Unity;
using Color = UnityEngine.Color;
using CoreColor = Xamlity.Rendering.Color;
using Is = UnityEngine.TestTools.Constraints.Is;
using Rect = Xamlity.Layout.Rect;

namespace Xamlity.Editor.Tests
{
    public class RenderingTests
    {
        private const string TopBandPath = "Packages/com.loonacia.xamlity/Tests/Editor/TestAssets/TopBand.xaml";
        private readonly List<GameObject> objects = new List<GameObject>();
        private Mesh mesh;
        private RenderTexture target;
        private Texture2D pixels;
        private RenderTexture previousTarget;

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

        private XamlityRoot CreateRoot()
        {
            var gameObject = new GameObject("Xamlity test root");
            objects.Add(gameObject);
            var root = gameObject.AddComponent<XamlityRoot>();
            var asset = AssetDatabase.LoadAssetAtPath<XamlAsset>(TopBandPath);
            Assert.That(asset, Is.Not.Null);
            root.Build(asset);
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
