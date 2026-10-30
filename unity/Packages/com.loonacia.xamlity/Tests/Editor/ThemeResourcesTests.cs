using System;
using System.Collections.Generic;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;
using Xamlity.Controls;
using Xamlity.Layout;
using Xamlity.Markup;
using Xamlity.Styling;
using Xamlity.Unity;
using Object = UnityEngine.Object;

namespace Xamlity.Editor.Tests
{
    public sealed class ThemeResourcesTests
    {
        private const string Namespaces = "xmlns=\"https://github.com/loonacia/xamlity\" " +
            "xmlns:x=\"http://schemas.microsoft.com/winfx/2006/xaml\"";
        private readonly List<XamlAsset> assets = new List<XamlAsset>();
        private GameObject gameObject;
        private XamlityRoot root;
        private TMP_FontAsset fontAsset;

        [SetUp]
        public void SetUp()
        {
            gameObject = new GameObject("Xamlity theme test root");
            root = gameObject.AddComponent<XamlityRoot>();
            root.ReferenceResolution = new Vector2Int(256, 128);
        }

        [TearDown]
        public void TearDown()
        {
            if (root != null)
                root.Release();
            Object.DestroyImmediate(gameObject);
            foreach (var asset in assets)
                Object.DestroyImmediate(asset);
            assets.Clear();
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

        [Test]
        public void PackageThemeLoadsImportedStylesForBothContentControls()
        {
            var asset = Resources.Load<XamlAsset>("Xamlity/Generic");

            Assert.That(asset, Is.Not.Null);
            Assert.That(asset.HasIr, Is.True);
            var theme = asset.BuildResourceDictionary(null);
            foreach (var type in new[] { typeof(ContentControl), typeof(UserControl) })
            {
                Assert.That(theme[type], Is.TypeOf<Style>());
                Assert.That(((Style)theme[type]).TargetType, Is.EqualTo(type));
            }
        }

        [Test]
        public void UserControlThemeRendersBackgroundAndPaddedKoreanContent()
        {
            CreateFont();
            var asset = CreateAsset("<UserControl " + Namespaces + " Background=\"Red\" Padding=\"10\">" +
                "<TextBlock Text=\"한글\"/></UserControl>");

            root.Build(asset, fontAsset);
            root.Refresh(256, 128);

            var control = (UserControl)root.Tree.RootElement;
            Assert.That(control.Template, Is.Not.Null);
            var text = (TextBlock)control.Content;
            Assert.That(text.VisualParent.VisualOffset, Is.EqualTo(new Point(10, 10)));
            Assert.That(text.RenderSize, Is.EqualTo(new Size(236, 108)));
            Assert.That(root.Mesh.vertexCount, Is.EqualTo(12));
            Assert.That(root.Batches.Count, Is.EqualTo(2));
            Assert.That(root.Batches[0].Kind, Is.EqualTo(MeshDrawingContext.BatchKind.Solid));
            Assert.That(root.Batches[1].Kind, Is.EqualTo(MeshDrawingContext.BatchKind.Text));
            Assert.That(root.Batches[1].Texture, Is.SameAs(fontAsset.atlasTextures[0]));
        }

        [Test]
        public void ContentControlThemeRendersStringContent()
        {
            CreateFont();
            var asset = CreateAsset("<ContentControl " + Namespaces + " Content=\"텍스트\"/>");

            root.Build(asset, fontAsset);
            root.Refresh(256, 128);

            Assert.That(((ContentControl)root.Tree.RootElement).Template, Is.Not.Null);
            Assert.That(root.Mesh.vertexCount, Is.EqualTo(12));
            Assert.That(root.Batches.Count, Is.EqualTo(1));
            Assert.That(root.Batches[0].Kind, Is.EqualTo(MeshDrawingContext.BatchKind.Text));
        }

        [Test]
        public void ViewStaticResourceAndRuntimeLookupUseApplicationDictionary()
        {
            var application = CreateAsset("<ResourceDictionary " + Namespaces + ">" +
                "<SolidColorBrush x:Key=\"Accent\" Color=\"Red\"/></ResourceDictionary>");
            var view = CreateAsset("<Border " + Namespaces + " Background=\"{StaticResource Accent}\"/>");

            root.Build(view, applicationResources: application);
            root.Refresh(256, 128);

            var border = (Border)root.Tree.RootElement;
            Assert.That(border.Background, Is.SameAs(root.Tree.ApplicationResources["Accent"]));
            Assert.That(border.FindResource("Accent"), Is.SameAs(border.Background));
            Assert.That(root.Mesh.vertexCount, Is.EqualTo(4));
        }

        [Test]
        public void ApplicationImplicitStyleAppliesToViewTextBlock()
        {
            var application = CreateAsset("<ResourceDictionary " + Namespaces + ">" +
                "<Style TargetType=\"TextBlock\"><Setter Property=\"FontSize\" Value=\"31\"/></Style>" +
                "</ResourceDictionary>");
            var view = CreateAsset("<Border " + Namespaces + "><TextBlock Text=\"Styled\"/></Border>");

            root.Build(view, applicationResources: application);

            var text = (TextBlock)((Border)root.Tree.RootElement).Child;
            Assert.That(text.Style, Is.SameAs(root.Tree.ApplicationResources[typeof(TextBlock)]));
            Assert.That(text.FontSize, Is.EqualTo(31));
        }

        [Test]
        public void ApplicationStaticResourceFallsBackToPackageTheme()
        {
            var application = CreateAsset("<ResourceDictionary " + Namespaces + ">" +
                "<Style TargetType=\"ContentControl\" BasedOn=\"{StaticResource {x:Type ContentControl}}\">" +
                "<Setter Property=\"FontSize\" Value=\"30\"/></Style></ResourceDictionary>");
            var view = CreateAsset("<ContentControl " + Namespaces + " Content=\"Themed\"/>");

            root.Build(view, applicationResources: application);

            var style = (Style)root.Tree.ApplicationResources[typeof(ContentControl)];
            Assert.That(style.BasedOn, Is.SameAs(root.Tree.ThemeResources[typeof(ContentControl)]));
            var control = (ContentControl)root.Tree.RootElement;
            Assert.That(control.Style, Is.SameAs(style));
            Assert.That(control.Template, Is.Not.Null);
            Assert.That(control.FontSize, Is.EqualTo(30));
        }

        [Test]
        public void RebuildCreatesFreshApplicationAndThemeDictionaries()
        {
            var application = CreateAsset("<ResourceDictionary " + Namespaces + ">" +
                "<SolidColorBrush x:Key=\"Accent\" Color=\"Red\"/></ResourceDictionary>");
            var view = CreateAsset("<Border " + Namespaces + " Background=\"{StaticResource Accent}\"/>");
            root.Build(view, applicationResources: application);
            var first = root.Tree;

            root.Build(view, applicationResources: application);

            Assert.That(root.Tree.ApplicationResources, Is.Not.SameAs(first.ApplicationResources));
            Assert.That(root.Tree.ThemeResources, Is.Not.SameAs(first.ThemeResources));
            Assert.That(root.Tree.ApplicationResources["Accent"], Is.Not.SameAs(first.ApplicationResources["Accent"]));
            Assert.That(root.Tree.ThemeResources[typeof(ContentControl)],
                Is.Not.SameAs(first.ThemeResources[typeof(ContentControl)]));
        }

        [Test]
        public void PublicBuildDoesNotInjectThemeOrApplicationResources()
        {
            var view = CreateAsset("<ContentControl " + Namespaces + " Content=\"Unstyled\"/>");

            Assert.That(((ContentControl)view.Build()).Template, Is.Null);
        }

        [Test]
        public void DictionaryRootIsRejectedByBothViewBuildPaths()
        {
            var asset = CreateAsset("<ResourceDictionary " + Namespaces + "/>");
            asset.name = "App";

            Assert.That(Assert.Throws<InvalidOperationException>(() => asset.Build()).Message,
                Is.EqualTo("XAML asset 'App' must have a UIElement root."));
            Assert.That(Assert.Throws<InvalidOperationException>(() => root.Build(asset)).Message,
                Is.EqualTo("XAML asset 'App' must have a UIElement root."));
        }

        [Test]
        public void ElementRootIsRejectedAsApplicationResources()
        {
            var asset = CreateAsset("<Border " + Namespaces + "/>");
            asset.name = "View";

            Assert.That(Assert.Throws<InvalidOperationException>(() => asset.BuildResourceDictionary(null)).Message,
                Is.EqualTo("XAML asset 'View' must have a ResourceDictionary root."));
            Assert.That(Assert.Throws<InvalidOperationException>(() => root.Build(asset, applicationResources: asset)).Message,
                Is.EqualTo("XAML asset 'View' must have a ResourceDictionary root."));
        }

        [Test]
        public void ImportErrorsAreRejectedByBothAssetBuildPaths()
        {
            var asset = ScriptableObject.CreateInstance<XamlAsset>();
            assets.Add(asset);
            asset.name = "Broken";

            Assert.That(Assert.Throws<InvalidOperationException>(() => asset.Build()).Message,
                Is.EqualTo("XAML asset 'Broken' has import errors."));
            Assert.That(Assert.Throws<InvalidOperationException>(() => asset.BuildResourceDictionary(null)).Message,
                Is.EqualTo("XAML asset 'Broken' has import errors."));
        }

        private XamlAsset CreateAsset(string xaml)
        {
            var result = XamlParser.Parse(xaml, "theme-test.xaml");
            Assert.That(result.Errors, Is.Empty);
            var asset = ScriptableObject.CreateInstance<XamlAsset>();
            assets.Add(asset);
            asset.Initialize(XamlIrSerializer.Serialize(result.Document), Array.Empty<string>(), Array.Empty<Object>());
            return asset;
        }

        private void CreateFont()
        {
            var source = AssetDatabase.LoadAssetAtPath<Font>("Assets/Fonts/NotoSansKR-Regular.otf");
            Assert.That(source, Is.Not.Null);
            fontAsset = TMP_FontAsset.CreateFontAsset(source, 90, 9, GlyphRenderMode.SDFAA,
                1024, 1024, AtlasPopulationMode.Dynamic, true);
            Assert.That(fontAsset, Is.Not.Null);
        }
    }
}
