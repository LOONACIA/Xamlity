using System;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using Xamlity.Controls;
using Xamlity.Markup;
using Xamlity.Rendering;
using Xamlity.Styling;
using Xamlity.Unity;
using Color = Xamlity.Rendering.Color;
using Object = UnityEngine.Object;

namespace Xamlity.Editor.Tests
{
    public class MergedDictionaryImportTests
    {
        private const string Folder = "Assets/XamlityTestTemp";
        private const string APath = Folder + "/A.xaml";
        private const string BPath = Folder + "/B.xaml";
        private const string Namespaces = "xmlns=\"https://github.com/loonacia/xamlity\" " +
            "xmlns:x=\"http://schemas.microsoft.com/winfx/2006/xaml\"";

        [SetUp]
        public void SetUp() => AssetDatabase.CreateFolder("Assets", "XamlityTestTemp");

        [TearDown]
        public void TearDown()
        {
            if (AssetDatabase.IsValidFolder(Folder))
                Assert.That(AssetDatabase.DeleteAsset(Folder), Is.True);
        }

        [TestCase("B.xaml")]
        [TestCase("./unused/../B.xaml")]
        [TestCase("Assets/XamlityTestTemp/B.xaml")]
        public void ImportedSourceBuildsMergedBrush(string source)
        {
            Import(BPath, BrushDictionary("Red"));
            XamlAsset asset = Import(APath, View(source));
            Assert.That(asset.HasIr, Is.True);
            var serialized = new SerializedObject(asset);
            Assert.That(serialized.FindProperty("assets").GetArrayElementAtIndex(0).objectReferenceValue,
                Is.EqualTo(AssetDatabase.LoadAssetAtPath<XamlAsset>(BPath)));
            AssertBrush(asset, Color.FromRgb(255, 0, 0));
        }

        [Test]
        public void ChangedDictionaryAutomaticallyUpdatesReferringAsset()
        {
            Import(BPath, BrushDictionary("Red"));
            AssertBrush(Import(APath, View("B.xaml")), Color.FromRgb(255, 0, 0));

            Write(BPath, BrushDictionary("Blue"));
            AssetDatabase.Refresh();

            AssertBrush(LoadA(), Color.FromRgb(0, 0, 255));
        }

        [Test]
        public void InvalidDictionaryAutomaticallyReimportsReferrerWithLocationAndRecovers()
        {
            Import(BPath, BrushDictionary("Red"));
            Import(APath, View("B.xaml"));
            LogAssert.Expect(LogType.Error, new Regex(Regex.Escape(BPath + "(2,4): ") + ".*Unknown"));
            ExpectSourceError("B.xaml", "XAML asset has import errors.");
            Write(BPath, "<ResourceDictionary " + Namespaces + ">\n  <Unknown x:Key=\"invalid\"/></ResourceDictionary>");

            AssetDatabase.Refresh();

            Assert.That(LoadA().HasIr, Is.False);
            Write(BPath, BrushDictionary("Blue"));
            AssetDatabase.Refresh();
            AssertBrush(LoadA(), Color.FromRgb(0, 0, 255));
        }

        [Test]
        public void MissingDictionaryAutomaticallyRecoversWhenCreated()
        {
            ExpectSourceError("B.xaml", "Asset was not found.");
            Assert.That(Import(APath, View("B.xaml")).HasIr, Is.False);
            Write(BPath, BrushDictionary("Blue"));

            AssetDatabase.Refresh();

            var asset = LoadA();
            var serialized = new SerializedObject(asset);
            Assert.That(serialized.FindProperty("assetKeys").arraySize, Is.EqualTo(1));
            Assert.That(serialized.FindProperty("assetKeys").GetArrayElementAtIndex(0).stringValue, Is.EqualTo("B.xaml"));
            Assert.That(serialized.FindProperty("assets").arraySize, Is.EqualTo(1));
            Assert.That(serialized.FindProperty("assets").GetArrayElementAtIndex(0).objectReferenceValue,
                Is.EqualTo(AssetDatabase.LoadAssetAtPath<XamlAsset>(BPath)));
            var loaded = AssetDatabase.LoadAssetAtPath<XamlAsset>(BPath);
            Assert.That(loaded != null, Is.True, "Created dictionary must be loaded.");
            Assert.That(loaded.HasIr, Is.True);
            AssertBrush(asset, Color.FromRgb(0, 0, 255));
        }

        [Test]
        public void NewReferrerBeforeDictionaryImportsOnSingleRefresh()
        {
            Write(APath, View("B.xaml"));
            Write(BPath, BrushDictionary("Red"));

            AssetDatabase.Refresh();

            AssertBrush(LoadA(), Color.FromRgb(255, 0, 0));
        }

        [TestCase("missing.xaml", "Asset was not found.")]
        [TestCase("Text.txt", "Asset must be a .xaml file.")]
        [TestCase("/absolute.xaml", "Invalid asset path")]
        [TestCase("pack://application/B.xaml", "Invalid asset path")]
        [TestCase("..\\B.xaml", "Invalid asset path")]
        [TestCase("../../../B.xaml", "Invalid asset path")]
        public void InvalidSourceLogsLocationAndKeepsAssetWithoutIr(string source, string error)
        {
            if (source == "Text.txt")
            {
                Write(Folder + "/Text.txt", "Not XAML");
                AssetDatabase.ImportAsset(Folder + "/Text.txt", ImportAssetOptions.ForceSynchronousImport);
            }
            ExpectSourceError(source, error);
            Assert.That(Import(APath, View(source)).HasIr, Is.False);
        }

        [Test]
        public void NonDictionaryRootLogsSourceLocation()
        {
            Import(BPath, "<Border " + Namespaces + "/>");
            ExpectSourceError("B.xaml", "XAML asset must have a ResourceDictionary root.");
            Assert.That(Import(APath, View("B.xaml")).HasIr, Is.False);
        }

        [Test]
        public void TransitiveSourceReimportsAndEachBuildCreatesFreshDictionaries()
        {
            string cPath = Folder + "/C.xaml";
            Write(APath, View("B.xaml"));
            Write(BPath, "<ResourceDictionary " + Namespaces + "><ResourceDictionary.MergedDictionaries>" +
                "<ResourceDictionary Source=\"C.xaml\"/></ResourceDictionary.MergedDictionaries></ResourceDictionary>");
            Write(cPath, BrushDictionary("Red"));
            AssetDatabase.Refresh();
            XamlAsset asset = LoadA();
            var first = (Border)asset.Build();
            var second = (Border)asset.Build();
            Assert.That(first.Resources.MergedDictionaries[0], Is.Not.SameAs(second.Resources.MergedDictionaries[0]));
            Assert.That(((Border)first.Child).Background, Is.Not.SameAs(((Border)second.Child).Background));
            Write(cPath, BrushDictionary("Blue"));
            AssetDatabase.Refresh();
            AssertBrush(LoadA(), Color.FromRgb(0, 0, 255));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void ReferencedDictionaryUsesOriginalApplicationAndThemeFallbacks(bool withApplication)
        {
            Import(BPath, "<ResourceDictionary " + Namespaces + "><Style x:Key=\"style\" TargetType=\"Border\">" +
                "<Setter Property=\"Background\" Value=\"{StaticResource brush}\"/></Style></ResourceDictionary>");
            XamlAsset asset = Import(APath, View("B.xaml").Replace("Background=\"{StaticResource brush}\"", "Style=\"{StaticResource style}\""));
            var themeBrush = new SolidColorBrush(Color.FromRgb(1, 2, 3));
            var appBrush = new SolidColorBrush(Color.FromRgb(4, 5, 6));
            var theme = new ResourceDictionary { ["brush"] = themeBrush };
            var app = withApplication ? new ResourceDictionary { ["brush"] = appBrush } : null;
            var root = (Border)asset.Build(app, theme);
            Assert.That(((Border)root.Child).Background, Is.SameAs(withApplication ? appBrush : themeBrush));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void RuntimeReferenceCycleThrowsAtSourceNodeAndUnwindsBuildStack(bool selfReference)
        {
            var a = ScriptableObject.CreateInstance<XamlAsset>();
            var b = ScriptableObject.CreateInstance<XamlAsset>();
            try
            {
                Initialize(a, "\n<ResourceDictionary " + Namespaces + " Source=\"B.xaml\"/>", "B.xaml", selfReference ? a : b);
                Initialize(b, "\n\n<ResourceDictionary " + Namespaces + " Source=\"A.xaml\"/>", "A.xaml", a);
                for (int i = 0; i < 2; i++)
                {
                    var error = Assert.Throws<XamlBuildException>(() => a.BuildResourceDictionary(null));
                    Assert.That((error.Line, error.Column), Is.EqualTo((selfReference ? 2 : 3, 2)));
                    Assert.That(error.Message, Is.EqualTo(selfReference
                        ? "(2,2): Resource dictionary 'B.xaml' is part of a reference cycle."
                        : "(3,2): Resource dictionary 'A.xaml' is part of a reference cycle."));
                }
                Initialize(a, BrushDictionary("Blue"), null, null);
                Assert.That(((SolidColorBrush)a.BuildResourceDictionary(null)["brush"]).Color, Is.EqualTo(Color.FromRgb(0, 0, 255)));
            }
            finally
            {
                Object.DestroyImmediate(a);
                Object.DestroyImmediate(b);
            }
        }

        [Test]
        public void RepeatedReferenceIsNotACycle()
        {
            Import(BPath, BrushDictionary("Red"));
            string xaml = View("B.xaml").Replace("</ResourceDictionary.MergedDictionaries>",
                "<ResourceDictionary Source=\"B.xaml\"/></ResourceDictionary.MergedDictionaries>");
            var root = (Border)Import(APath, xaml).Build();
            Assert.That(root.Resources.MergedDictionaries.Count, Is.EqualTo(2));
            Assert.That(root.Resources.MergedDictionaries[0], Is.Not.SameAs(root.Resources.MergedDictionaries[1]));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void MissingRuntimeDictionaryReferenceThrowsWithLocation(bool brokenReference)
        {
            var asset = ScriptableObject.CreateInstance<XamlAsset>();
            var dictionary = ScriptableObject.CreateInstance<XamlAsset>();
            try
            {
                Initialize(asset, "\n<ResourceDictionary " + Namespaces + " Source=\"B.xaml\"/>",
                    brokenReference ? "B.xaml" : null, brokenReference ? dictionary : null);
                Object.DestroyImmediate(dictionary);
                var error = Assert.Throws<XamlBuildException>(() => asset.BuildResourceDictionary(null));
                Assert.That(error.Message, Is.EqualTo("(2,2): Resource dictionary 'B.xaml' could not be resolved."));
            }
            finally
            {
                if (dictionary != null)
                    Object.DestroyImmediate(dictionary);
                Object.DestroyImmediate(asset);
            }
        }

        private static void Initialize(XamlAsset asset, string text, string key, XamlAsset reference)
        {
            XamlParseResult result = XamlParser.Parse(text, "test.xaml");
            Assert.That(result.Errors, Is.Empty);
            asset.Initialize(XamlIrSerializer.Serialize(result.Document), key == null ? Array.Empty<string>() : new[] { key },
                reference == null ? Array.Empty<Object>() : new Object[] { reference });
        }

        private static string View(string source) => "<Border " + Namespaces + "><Border.Resources><ResourceDictionary>" +
            "<ResourceDictionary.MergedDictionaries>\n  <ResourceDictionary Source=\"" + source + "\"/>" +
            "</ResourceDictionary.MergedDictionaries></ResourceDictionary></Border.Resources>" +
            "<Border Background=\"{StaticResource brush}\"/></Border>";

        private static string BrushDictionary(string color) => "<ResourceDictionary " + Namespaces + ">" +
            "<SolidColorBrush x:Key=\"brush\" Color=\"" + color + "\"/></ResourceDictionary>";

        private static void Write(string path, string text) => File.WriteAllText(path, text, Encoding.UTF8);

        private static XamlAsset Import(string path, string text)
        {
            Write(path, text);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            return AssetDatabase.LoadAssetAtPath<XamlAsset>(path);
        }

        private static XamlAsset LoadA() => AssetDatabase.LoadAssetAtPath<XamlAsset>(APath);

        private static void AssertBrush(XamlAsset asset, Color color)
        {
            Assert.That(asset, Is.Not.Null);
            Assert.That(asset.HasIr, Is.True);
            var root = (Border)asset.Build();
            Assert.That(((SolidColorBrush)((Border)root.Child).Background).Color, Is.EqualTo(color));
        }

        private static void ExpectSourceError(string source, string message) => LogAssert.Expect(LogType.Error,
            new Regex(Regex.Escape(APath + "(2,23): Resource dictionary '" + source + "': ") + Regex.Escape(message)));
    }
}
