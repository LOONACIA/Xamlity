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
using Xamlity.Unity;

namespace Xamlity.Editor.Tests
{
    public class XamlImporterTests
    {
        private const string ValidPath = "Packages/com.loonacia.xamlity/Tests/Editor/TestAssets/Valid.xaml";
        private const string TempFolder = "Assets/XamlityTestTemp";
        private const string BrokenPath = TempFolder + "/Broken.xaml";

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
    }
}
