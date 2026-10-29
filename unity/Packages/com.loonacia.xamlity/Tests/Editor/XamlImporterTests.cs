using System;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using Xamlity.Controls;
using Xamlity.Layout;
using Xamlity.Markup;
using Xamlity.Unity;
using Object = UnityEngine.Object;
using Rect = UnityEngine.Rect;

namespace Xamlity.Editor.Tests
{
    public class XamlImporterTests
    {
        private const string ValidPath = "Packages/com.loonacia.xamlity/Tests/Editor/TestAssets/Valid.xaml";
        private const string TempFolder = "Assets/XamlityTestTemp";
        private const string BrokenPath = TempFolder + "/Broken.xaml";
        private const string ImagePath = TempFolder + "/Image.png";
        private const string Namespace = "xmlns=\"https://github.com/loonacia/xamlity\"";

        [TearDown]
        public void TearDown()
        {
            if (AssetDatabase.IsValidFolder(TempFolder))
                Assert.That(AssetDatabase.DeleteAsset(TempFolder), Is.True);
        }

        [Test]
        public void ValidAssetBuildsBorderWithKoreanText()
        {
            var asset = AssetDatabase.LoadAssetAtPath<XamlAsset>(ValidPath);

            Assert.That(asset, Is.Not.Null);
            Assert.That(asset.HasIr, Is.True);
            Assert.That(AssetDatabase.LoadMainAssetAtPath(ValidPath), Is.SameAs(asset));
            Assert.That(AssetImporter.GetAtPath(ValidPath), Is.TypeOf<XamlImporter>());
            Assert.That(asset.Build(), Is.TypeOf<Border>());
            var border = (Border)asset.Build();
            Assert.That(border.Padding, Is.EqualTo(new Thickness(8)));
            Assert.That(border.Child, Is.TypeOf<TextBlock>());
            var text = (TextBlock)border.Child;
            Assert.That(text.Text, Is.EqualTo("안녕하세요, Xamlity!"));
            Assert.That(text.FontSize, Is.EqualTo(24));
        }

        [Test]
        public void BuildCreatesIndependentElementTrees()
        {
            var asset = AssetDatabase.LoadAssetAtPath<XamlAsset>(ValidPath);
            var first = (Border)asset.Build();
            ((TextBlock)first.Child).Text = "Changed";
            var second = (Border)asset.Build();

            Assert.That(second, Is.Not.SameAs(first));
            Assert.That(second.Child, Is.Not.SameAs(first.Child));
            Assert.That(((TextBlock)second.Child).Text, Is.EqualTo("안녕하세요, Xamlity!"));
            Assert.That(second.Child.VisualParent, Is.SameAs(second));
        }

        [Test]
        public void MalformedXmlLogsLocationAndKeepsAssetWithoutIr()
        {
            AssetDatabase.CreateFolder("Assets", "XamlityTestTemp");
            File.WriteAllText(BrokenPath,
                "<Border xmlns=\"https://github.com/loonacia/xamlity\">\n" +
                "  <TextBlock>\n</Border>", Encoding.UTF8);
            LogAssert.Expect(LogType.Error, new Regex(Regex.Escape(BrokenPath +
                "(3,3): The 'TextBlock' start tag on line 2 position 4 does not match the end tag of 'Border'.")));

            AssetDatabase.ImportAsset(BrokenPath, ImportAssetOptions.ForceSynchronousImport);

            var asset = AssetDatabase.LoadAssetAtPath<XamlAsset>(BrokenPath);
            Assert.That(asset, Is.Not.Null);
            Assert.That(AssetDatabase.LoadMainAssetAtPath(BrokenPath), Is.SameAs(asset));
            Assert.That(asset.HasIr, Is.False);
            var exception = Assert.Throws<InvalidOperationException>(() => asset.Build());
            Assert.That(exception.Message, Is.EqualTo("XAML asset 'Broken' has import errors."));
        }

        [Test]
        public void InvalidPropertiesLogEveryErrorAndKeepAssetWithoutIr()
        {
            AssetDatabase.CreateFolder("Assets", "XamlityTestTemp");
            File.WriteAllText(BrokenPath,
                "<Border xmlns=\"https://github.com/loonacia/xamlity\"\n" +
                "  Unknown=\"1\"\n  Other=\"2\" />", Encoding.UTF8);
            LogAssert.Expect(LogType.Error, new Regex(Regex.Escape(BrokenPath +
                "(2,3): Unknown or read-only property 'Unknown' on type 'Border'.")));
            LogAssert.Expect(LogType.Error, new Regex(Regex.Escape(BrokenPath +
                "(3,3): Unknown or read-only property 'Other' on type 'Border'.")));

            AssetDatabase.ImportAsset(BrokenPath, ImportAssetOptions.ForceSynchronousImport);

            var asset = AssetDatabase.LoadAssetAtPath<XamlAsset>(BrokenPath);
            Assert.That(asset, Is.Not.Null);
            Assert.That(asset.HasIr, Is.False);
            Assert.Throws<InvalidOperationException>(() => asset.Build());
        }

        [TestCase("Image.png")]
        [TestCase("./unused/../Image.png")]
        [TestCase("Assets/XamlityTestTemp/Image.png")]
        public void ImagePathsBuildSingleSpriteWithBorder(string value)
        {
            CreateImage(sprite: true);
            XamlAsset asset = ImportImage(value);
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(ImagePath);

            Assert.That(asset.HasIr, Is.True);
            var source = (UnityImageSource)((Image)asset.Build()).Source;
            Assert.That(source.Sprite, Is.SameAs(sprite));
            Assert.That(source.Texture, Is.SameAs(sprite.texture));
            Assert.That(source.Sprite.border, Is.EqualTo(new Vector4(1, 2, 3, 4)));
        }

        [Test]
        public void TextureWithoutSpriteBuildsTextureSource()
        {
            CreateImage(sprite: false);
            XamlAsset asset = ImportImage("Image.png");

            Assert.That(asset.HasIr, Is.True);
            var source = (UnityImageSource)((Image)asset.Build()).Source;
            Assert.That(source.Sprite, Is.Null);
            Assert.That(source.Texture, Is.SameAs(AssetDatabase.LoadAssetAtPath<Texture2D>(ImagePath)));
        }

        [Test]
        public void DuplicateRawValuesAreStoredOnceAndBuildBothImages()
        {
            CreateImage(sprite: false);
            File.WriteAllText(BrokenPath, "<StackPanel " + Namespace + ">" +
                "<Image Source=\"Image.png\"/><Image Source=\"Image.png\"/></StackPanel>", Encoding.UTF8);
            AssetDatabase.ImportAsset(BrokenPath, ImportAssetOptions.ForceSynchronousImport);
            var asset = AssetDatabase.LoadAssetAtPath<XamlAsset>(BrokenPath);
            var serialized = new SerializedObject(asset);
            Assert.That(serialized.FindProperty("assetKeys").arraySize, Is.EqualTo(1));
            Assert.That(serialized.FindProperty("assetKeys").GetArrayElementAtIndex(0).stringValue,
                Is.EqualTo("Image.png"));
            Assert.That(serialized.FindProperty("assets").arraySize, Is.EqualTo(1));
            var panel = (StackPanel)asset.Build();
            Assert.That(((UnityImageSource)((Image)panel.Children[0]).Source).Texture,
                Is.SameAs(((UnityImageSource)((Image)panel.Children[1]).Source).Texture));
        }

        [TestCase("missing.png", "Asset was not found")]
        [TestCase("/abs.png", "Invalid image asset path")]
        [TestCase("pack://images/icon.png", "Invalid image asset path")]
        [TestCase("C:/images/icon.png", "Invalid image asset path")]
        [TestCase("..\\Image.png", "Invalid image asset path")]
        [TestCase("../../../outside.png", "Invalid image asset path")]
        public void InvalidImagePathsLogLocationAndKeepAssetWithoutIr(string value, string message)
        {
            ExpectImageError(message);
            XamlAsset asset = ImportImage(value);

            Assert.That(asset, Is.Not.Null);
            Assert.That(asset.HasIr, Is.False);
            Assert.Throws<InvalidOperationException>(() => asset.Build());
        }

        [Test]
        public void UnsupportedAssetTypeIsAnImportError()
        {
            AssetDatabase.CreateFolder("Assets", "XamlityTestTemp");
            string path = TempFolder + "/Text.txt";
            File.WriteAllText(path, "Not an image", Encoding.UTF8);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            ExpectImageError("Asset must be a Texture2D or a single Sprite");

            Assert.That(ImportImage("Text.txt").HasIr, Is.False);
        }

        [Test]
        public void CreatingMissingImageAutomaticallyReimportsXamlOnRefresh()
        {
            ExpectImageError("Asset was not found");
            Assert.That(ImportImage("Image.png").HasIr, Is.False);

            WritePng();
            AssetDatabase.Refresh();

            var asset = AssetDatabase.LoadAssetAtPath<XamlAsset>(BrokenPath);
            Assert.That(asset.HasIr, Is.True);
            var actual = ((UnityImageSource)((Image)asset.Build()).Source).Texture;
            var expected = AssetDatabase.LoadAssetAtPath<Texture2D>(ImagePath);
            Assert.That(actual.GetEntityId(), Is.EqualTo(expected.GetEntityId()));
        }

        [TestCase("A.xaml", "Z.png")]
        [TestCase("Z.xaml", "A.png")]
        public void NewXamlAndTextureImportTogetherOnOneRefresh(string xamlName, string imageName)
        {
            AssetDatabase.CreateFolder("Assets", "XamlityTestTemp");
            string xamlPath = TempFolder + "/" + xamlName;
            string imagePath = TempFolder + "/" + imageName;
            var texture = new Texture2D(16, 16);
            try
            {
                File.WriteAllBytes(imagePath, texture.EncodeToPNG());
            }
            finally
            {
                Object.DestroyImmediate(texture);
            }
            File.WriteAllText(xamlPath, "<Image " + Namespace + " Source=\"" + imageName + "\"/>", Encoding.UTF8);

            AssetDatabase.Refresh();

            var asset = AssetDatabase.LoadAssetAtPath<XamlAsset>(xamlPath);
            Assert.That(asset, Is.Not.Null);
            Assert.That(asset.HasIr, Is.True);
            Assert.That(((Image)asset.Build()).Source, Is.TypeOf<UnityImageSource>());
            var source = (UnityImageSource)((Image)asset.Build()).Source;
            var expected = AssetDatabase.LoadAssetAtPath<Texture2D>(imagePath);
            Assert.That(expected, Is.Not.Null);
            Assert.That(source.Sprite, Is.Null);
            Assert.That(source.Texture.GetEntityId(), Is.EqualTo(expected.GetEntityId()));
        }

        [Test]
        public void ChangingTextureToSpriteAutomaticallyUpdatesReferencedArtifact()
        {
            CreateImage(sprite: false);
            Assert.That(((UnityImageSource)((Image)ImportImage("Image.png").Build()).Source).Sprite, Is.Null);

            ConfigureSprite();
            AssetDatabase.Refresh();

            var asset = AssetDatabase.LoadAssetAtPath<XamlAsset>(BrokenPath);
            Assert.That(asset.HasIr, Is.True);
            Assert.That(((UnityImageSource)((Image)asset.Build()).Source).Sprite,
                Is.SameAs(AssetDatabase.LoadAssetAtPath<Sprite>(ImagePath)));
        }

        [Test]
        public void MultipleSpritesAreRejectedRegardlessOfMainTexture()
        {
            var texture = new Texture2D(16, 16);
            var first = Sprite.Create(texture, new Rect(0, 0, 8, 8), Vector2.zero);
            var second = Sprite.Create(texture, new Rect(8, 0, 8, 8), Vector2.zero);
            try
            {
                Assert.That(XamlImporter.TrySelectImageAsset(new Object[] { first, texture, second }, texture,
                    out Object selected, out string error), Is.False);
                Assert.That(selected, Is.Null);
                Assert.That(error, Does.Contain("Multiple Sprites"));
            }
            finally
            {
                Object.DestroyImmediate(first);
                Object.DestroyImmediate(second);
                Object.DestroyImmediate(texture);
            }
        }

        [Test]
        public void SelectionUsesMainAssetTypeAndDoesNotAssumeArrayOrder()
        {
            var texture = new Texture2D(16, 16);
            var text = new TextAsset("Text");
            var sprite = Sprite.Create(texture, new Rect(0, 0, 8, 8), Vector2.zero);
            try
            {
                Assert.That(XamlImporter.TrySelectImageAsset(new Object[] { text, sprite, texture }, texture,
                    out Object selected, out _), Is.True);
                Assert.That(selected, Is.SameAs(sprite));
                Assert.That(XamlImporter.TrySelectImageAsset(new Object[] { text, texture }, texture,
                    out selected, out _), Is.True);
                Assert.That(selected, Is.SameAs(texture));
                Assert.That(XamlImporter.TrySelectImageAsset(new Object[] { texture, text }, text,
                    out selected, out _), Is.False);
                Assert.That(selected, Is.Null);
            }
            finally
            {
                Object.DestroyImmediate(sprite);
                Object.DestroyImmediate(text);
                Object.DestroyImmediate(texture);
            }
        }

        [TestCase("Assets/Views/View.xaml", "../Images/icon.png", "Assets/Images/icon.png")]
        [TestCase("Assets/View.xaml", "../Packages/com.example/icon.png", "Packages/com.example/icon.png")]
        [TestCase("Assets/Views/View.xaml", "Packages/com.example/./icon.png", "Packages/com.example/icon.png")]
        [TestCase("Packages/com.example/View.xaml", "./Images/../icon.png", "Packages/com.example/icon.png")]
        public void ProjectAndRelativePathsNormalizeDotSegments(string xamlPath, string value, string expected)
        {
            Assert.That(XamlImporter.TryResolveImagePath(xamlPath, value, out string path, out string error), Is.True);
            Assert.That(path, Is.EqualTo(expected));
            Assert.That(error, Is.Null);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void MissingRuntimeReferencesWarnAndLeaveSourceNull(bool brokenReference)
        {
            var asset = ScriptableObject.CreateInstance<XamlAsset>();
            asset.name = "MissingReference";
            var texture = new Texture2D(16, 16);
            try
            {
                XamlDocument document = XamlParser.Parse("<Image " + Namespace + " Source=\"Image.png\"/>", "test.xaml").Document;
                asset.Initialize(XamlIrSerializer.Serialize(document),
                    brokenReference ? new[] { "Image.png" } : Array.Empty<string>(),
                    brokenReference ? new Object[] { texture } : Array.Empty<Object>());
                Object.DestroyImmediate(texture);
                LogAssert.Expect(LogType.Warning, "Image asset 'Image.png' is missing from XAML asset 'MissingReference'.");

                Assert.That(((Image)asset.Build()).Source, Is.Null);
            }
            finally
            {
                if (texture != null)
                    Object.DestroyImmediate(texture);
                Object.DestroyImmediate(asset);
            }
        }

        private static XamlAsset ImportImage(string value)
        {
            if (!AssetDatabase.IsValidFolder(TempFolder))
                AssetDatabase.CreateFolder("Assets", "XamlityTestTemp");
            File.WriteAllText(BrokenPath, "<Image " + Namespace + "\n  Source=\"" + value + "\"/>", Encoding.UTF8);
            AssetDatabase.ImportAsset(BrokenPath, ImportAssetOptions.ForceSynchronousImport);
            return AssetDatabase.LoadAssetAtPath<XamlAsset>(BrokenPath);
        }

        private static void ExpectImageError(string message) =>
            LogAssert.Expect(LogType.Error, new Regex(Regex.Escape(BrokenPath + "(2,3): ") + ".*" + Regex.Escape(message)));

        private static void CreateImage(bool sprite)
        {
            AssetDatabase.CreateFolder("Assets", "XamlityTestTemp");
            WritePng();
            AssetDatabase.ImportAsset(ImagePath, ImportAssetOptions.ForceSynchronousImport);
            var importer = (TextureImporter)AssetImporter.GetAtPath(ImagePath);
            importer.textureType = TextureImporterType.Default;
            importer.SaveAndReimport();
            if (sprite)
                ConfigureSprite();
        }

        private static void ConfigureSprite()
        {
            var importer = (TextureImporter)AssetImporter.GetAtPath(ImagePath);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spriteBorder = new Vector4(1, 2, 3, 4);
            importer.SaveAndReimport();
        }

        private static void WritePng()
        {
            var texture = new Texture2D(16, 16);
            try
            {
                File.WriteAllBytes(ImagePath, texture.EncodeToPNG());
            }
            finally
            {
                Object.DestroyImmediate(texture);
            }
        }
    }
}
