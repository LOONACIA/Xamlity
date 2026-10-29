using System;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.TextCore.LowLevel;
using Xamlity.Controls;
using Xamlity.Diagnostics;
using Xamlity.Markup;
using Xamlity.Tree;
using Xamlity.Unity;
using Object = UnityEngine.Object;

namespace Xamlity.Editor.Tests
{
    public sealed class BindingRuntimeTests
    {
        private GameObject gameObject;
        private XamlityRoot root;
        private XamlAsset asset;
        private TMP_FontAsset fontAsset;

        [SetUp]
        public void SetUp()
        {
            var source = AssetDatabase.LoadAssetAtPath<Font>("Assets/Fonts/NotoSansKR-Regular.otf");
            Assert.That(source, Is.Not.Null);
            fontAsset = TMP_FontAsset.CreateFontAsset(source, 90, 9, GlyphRenderMode.SDFAA,
                1024, 1024, AtlasPopulationMode.Dynamic, true);
            Assert.That(fontAsset, Is.Not.Null);
            gameObject = new GameObject("Xamlity binding test root");
            root = gameObject.AddComponent<XamlityRoot>();
            root.ReferenceResolution = new Vector2Int(256, 128);
        }

        [TearDown]
        public void TearDown()
        {
            if (root != null)
                root.Release();
            Object.DestroyImmediate(gameObject);
            Object.DestroyImmediate(asset);
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

        [TestCase(false)]
        [TestCase(true)]
        public void DataContextBeforeOrAfterBuildUpdatesTextAndMesh(bool beforeBuild)
        {
            var model = new BindingTestViewModel { Name = "A" };
            if (beforeBuild)
                root.DataContext = model;
            Build();
            if (!beforeBuild)
                root.DataContext = model;
            root.Refresh(256, 128);

            Assert.That(root.DataContext, Is.SameAs(model));
            Assert.That(((FrameworkElement)root.Tree.RootElement).DataContext, Is.SameAs(model));
            Assert.That(Text.Text, Is.EqualTo("A"));
            Assert.That(root.Mesh.vertexCount, Is.EqualTo(4));
        }

        [Test]
        public void PropertyChangedRebuildsMeshAndUnchangedRefreshDoesNotAllocate()
        {
            var model = new BindingTestViewModel { Name = "A" };
            root.DataContext = model;
            Build();
            root.Refresh(256, 128);
            int rebuildCount = root.MeshRebuildCount;
            Assert.That(root.Mesh.vertexCount, Is.EqualTo(4));

            model.Name = "ABC";
            root.Refresh(256, 128);

            Assert.That(Text.Text, Is.EqualTo("ABC"));
            Assert.That(root.Mesh.vertexCount, Is.EqualTo(12));
            Assert.That(root.MeshRebuildCount, Is.EqualTo(rebuildCount + 1));
            long before = GC.GetAllocatedBytesForCurrentThread();
            root.Refresh(256, 128);
            long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
            Assert.That(allocated, Is.Zero);
            Assert.That(root.MeshRebuildCount, Is.EqualTo(rebuildCount + 1));
        }

        [Test]
        public void InvalidPathLogsWarningThroughEditorStartup()
        {
            Assert.That(XamlityLog.Logger, Is.TypeOf<UnityXamlityLogger>());
            Build("Missing");
            LogAssert.Expect(LogType.Warning,
                "Binding path error: property 'Missing' not found on 'Xamlity.Editor.Tests.BindingTestViewModel'. " +
                "Path 'Missing', target 'TextBlock.Text'.");

            root.DataContext = new BindingTestViewModel();
            root.Refresh(256, 128);
        }

        [Test]
        public void ReplacingDataContextUnsubscribesOldModel()
        {
            var first = new BindingTestViewModel { Name = "A" };
            var second = new BindingTestViewModel { Name = "BC" };
            root.DataContext = first;
            Build();
            Assert.That(first.SubscriberCount, Is.EqualTo(1));

            root.DataContext = second;
            first.Name = "Old";
            root.Refresh(256, 128);

            Assert.That(first.SubscriberCount, Is.Zero);
            Assert.That(second.SubscriberCount, Is.EqualTo(1));
            Assert.That(Text.Text, Is.EqualTo("BC"));
            Assert.That(root.Mesh.vertexCount, Is.EqualTo(8));
        }

        [Test]
        public void DestroyUnsubscribesModelAndClearsOldTreeContext()
        {
            var model = new BindingTestViewModel { Name = "A" };
            root.DataContext = model;
            Build();
            root.Register();
            root.Refresh(256, 128);
            var oldRoot = (FrameworkElement)root.Tree.RootElement;
            var mesh = root.Mesh;
            Assert.That(model.SubscriberCount, Is.EqualTo(1));

            root.Release();

            Assert.That(model.SubscriberCount, Is.Zero);
            Assert.That(oldRoot.DataContext, Is.Null);
            Assert.That(XamlityRoot.ActiveRoots, Has.No.Member(root));
            Assert.That(mesh == null, Is.True);
        }

        [Test]
        public void RebuildClearsOldContextAndReusesHostDataContext()
        {
            var model = new BindingTestViewModel { Name = "A" };
            root.DataContext = model;
            Build();
            var oldRoot = (FrameworkElement)root.Tree.RootElement;
            var oldText = Text;
            Assert.That(model.SubscriberCount, Is.EqualTo(1));

            root.Build(asset, fontAsset);
            model.Name = "BC";
            root.Refresh(256, 128);

            Assert.That(root.Tree.RootElement, Is.Not.SameAs(oldRoot));
            Assert.That(oldRoot.DataContext, Is.Null);
            Assert.That(oldText.Text, Is.Empty);
            Assert.That(model.SubscriberCount, Is.EqualTo(1));
            Assert.That(((FrameworkElement)root.Tree.RootElement).DataContext, Is.SameAs(model));
            Assert.That(Text.Text, Is.EqualTo("BC"));
            Assert.That(root.Mesh.vertexCount, Is.EqualTo(8));
            root.Release();
            Assert.That(model.SubscriberCount, Is.Zero);
        }

        private TextBlock Text => (TextBlock)((Border)root.Tree.RootElement).Child;

        private void Build(string path = "Name")
        {
            var result = XamlParser.Parse(
                "<Border xmlns=\"https://github.com/loonacia/xamlity\">" +
                "<TextBlock Text=\"{Binding " + path + "}\" FontSize=\"24\"/>" +
                "</Border>", "binding.xaml");
            Assert.That(result.Errors, Is.Empty);
            asset = ScriptableObject.CreateInstance<XamlAsset>();
            asset.Initialize(XamlIrSerializer.Serialize(result.Document), Array.Empty<string>(), Array.Empty<Object>());
            root.Build(asset, fontAsset);
        }
    }
}
