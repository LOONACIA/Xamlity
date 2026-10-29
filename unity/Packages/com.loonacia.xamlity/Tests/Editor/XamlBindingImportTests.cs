using System;
using System.Diagnostics;
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
    public class XamlBindingImportTests
    {
        private const string TempFolder = "Assets/XamlityTestTemp";
        internal const string XamlPath = TempFolder + "/Binding.xaml";
        private const string AssemblyName = "Xamlity.Editor.Tests";
        private const string Namespaces = "xmlns=\"https://github.com/loonacia/xamlity\" " +
            "xmlns:x=\"http://schemas.microsoft.com/winfx/2006/xaml\" " +
            "xmlns:t=\"clr-namespace:Xamlity.Editor.Tests;assembly=Xamlity.Editor.Tests\"";
        private const string ValidXaml = "<StackPanel " + Namespaces + " x:DataType=\"t:BindingTestViewModel\">\n" +
            "  <TextBlock Text=\"{Binding Name}\"/>\n" +
            "  <TextBlock><TextBlock.Text><Binding Path=\"Name\">\n" +
            "    <Binding.Converter><t:BindingTestConverter/></Binding.Converter>\n" +
            "  </Binding></TextBlock.Text></TextBlock>\n" +
            "</StackPanel>";
        private const string MissingTypeXaml = "<Border " + Namespaces + ">\n  <t:MissingView/>\n</Border>";
        private const string MissingTypeError = "(2,4): Unknown type 'MissingView' in namespace " +
            "'Xamlity.Editor.Tests' of assembly 'Xamlity.Editor.Tests'.";
        private const string TypoXaml = "<TextBlock " + Namespaces + " x:DataType=\"t:BindingTestViewModel\"\n" +
            "  Text=\"{Binding Naem}\"/>";
        private const string TypoError = "(2,3): Binding path 'Naem': property 'Naem' was not found on type " +
            "'Xamlity.Editor.Tests.BindingTestViewModel' (x:DataType).";

        [OneTimeSetUp]
        public void RegisterDependencies()
        {
            var stopwatch = Stopwatch.StartNew();
            XamlAssemblyDependencies.RegisterAll();
            stopwatch.Stop();
            TestContext.Out.WriteLine($"XAML assembly dependency registration: {stopwatch.Elapsed.TotalMilliseconds:F2} ms (excluding Refresh).");
            AssetDatabase.Refresh();
        }

        [TearDown]
        public void TearDown()
        {
            try
            {
                if (AssetDatabase.IsValidFolder(TempFolder))
                    Assert.That(AssetDatabase.DeleteAsset(TempFolder), Is.True);
            }
            finally
            {
                XamlAssemblyDependencies.RegisterAll();
                AssetDatabase.Refresh();
            }
        }

        [Test]
        public void ImportedBindingsBuildAndUpdateUsingUserViewModelAndConverter()
        {
            XamlAsset asset = Import(ValidXaml);
            Assert.That(asset.HasIr, Is.True);
            var panel = (StackPanel)asset.Build();
            var viewModel = new BindingTestViewModel { Name = "first" };

            panel.DataContext = viewModel;

            Assert.That(((TextBlock)panel.Children[0]).Text, Is.EqualTo("first"));
            Assert.That(((TextBlock)panel.Children[1]).Text, Is.EqualTo("FIRST"));
            viewModel.Name = "second";
            Assert.That(((TextBlock)panel.Children[0]).Text, Is.EqualTo("second"));
            Assert.That(((TextBlock)panel.Children[1]).Text, Is.EqualTo("SECOND"));
        }

        [TestCase(TypoXaml, TypoError)]
        [TestCase(MissingTypeXaml, MissingTypeError)]
        public void InvalidBindingOrUserTypeLogsLocationAndKeepsAssetWithoutIr(string xaml, string error)
        {
            ExpectError(error);

            XamlAsset asset = Import(xaml);

            Assert.That(asset, Is.Not.Null);
            Assert.That(asset.HasIr, Is.False);
            Assert.Throws<InvalidOperationException>(() => asset.Build());
        }

        [TestCase(ValidXaml, null)]
        [TestCase(MissingTypeXaml, MissingTypeError)]
        [TestCase(TypoXaml, TypoError)]
        public void ReferencedAssemblyHashChangesAutomaticallyReimportXaml(string xaml, string error)
        {
            ExpectError(error);
            XamlAsset asset = Import(xaml);
            Assert.That(asset.HasIr, Is.EqualTo(error == null));
            int importCount = XamlBindingImportObserver.ImportCount;

            ExpectError(error);
            AssetDatabase.RegisterCustomDependency(XamlAssemblyDependencies.GetDependencyName(AssemblyName),
                Hash128.Compute(Guid.NewGuid().ToString()));
            AssetDatabase.Refresh();

            Assert.That(XamlBindingImportObserver.ImportCount, Is.GreaterThan(importCount));
            Assert.That(AssetDatabase.LoadAssetAtPath<XamlAsset>(XamlPath).HasIr, Is.EqualTo(error == null));
            importCount = XamlBindingImportObserver.ImportCount;

            ExpectError(error);
            XamlAssemblyDependencies.RegisterAll();
            AssetDatabase.Refresh();

            Assert.That(XamlBindingImportObserver.ImportCount, Is.GreaterThan(importCount));
            Assert.That(AssetDatabase.LoadAssetAtPath<XamlAsset>(XamlPath).HasIr, Is.EqualTo(error == null));
            importCount = XamlBindingImportObserver.ImportCount;

            XamlAssemblyDependencies.RegisterAll();
            AssetDatabase.Refresh();

            Assert.That(XamlBindingImportObserver.ImportCount, Is.EqualTo(importCount));
        }

        [TestCase(ValidXaml, null)]
        [TestCase(MissingTypeXaml, MissingTypeError)]
        public void UnreferencedAssemblyHashChangeDoesNotReimportXaml(string xaml, string error)
        {
            ExpectError(error);
            XamlAsset asset = Import(xaml);
            int importCount = XamlBindingImportObserver.ImportCount;

            AssetDatabase.RegisterCustomDependency(XamlAssemblyDependencies.GetDependencyName("Xamlity.Editor"),
                Hash128.Compute(Guid.NewGuid().ToString()));
            AssetDatabase.Refresh();

            Assert.That(XamlBindingImportObserver.ImportCount, Is.EqualTo(importCount));
            Assert.That(AssetDatabase.LoadAssetAtPath<XamlAsset>(XamlPath), Is.SameAs(asset));
        }

        private static XamlAsset Import(string xaml)
        {
            AssetDatabase.CreateFolder("Assets", "XamlityTestTemp");
            File.WriteAllText(XamlPath, xaml, Encoding.UTF8);
            AssetDatabase.ImportAsset(XamlPath, ImportAssetOptions.ForceSynchronousImport);
            return AssetDatabase.LoadAssetAtPath<XamlAsset>(XamlPath);
        }

        private static void ExpectError(string error)
        {
            if (error != null)
                LogAssert.Expect(LogType.Error, new Regex(Regex.Escape(XamlPath + error)));
        }
    }

    internal sealed class XamlBindingImportObserver : AssetPostprocessor
    {
        internal static int ImportCount { get; private set; }

        private static void OnPostprocessAllAssets(string[] importedAssets, string[] deletedAssets,
            string[] movedAssets, string[] movedFromAssetPaths)
        {
            foreach (string path in importedAssets)
            {
                if (path == XamlBindingImportTests.XamlPath)
                    ImportCount++;
            }
        }
    }
}
