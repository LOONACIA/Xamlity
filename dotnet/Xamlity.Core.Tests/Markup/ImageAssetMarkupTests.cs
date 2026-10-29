using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Xamlity.Controls;
using Xamlity.Layout;
using Xamlity.Markup;
using Xamlity.Rendering;

namespace Xamlity.Core.Tests.Markup
{
    public class ImageAssetMarkupTests
    {
        private const string Namespace = "xmlns=\"https://github.com/loonacia/xamlity\"";

        [Test]
        public void ReferencesPreserveRawValuesLocationsAndDocumentOrderIncludingDuplicates()
        {
            XamlParseResult result = XamlParser.Parse("<StackPanel " + Namespace + ">\n" +
                "  <Image Source=\"./icons/../한글.png\"/>\n" +
                "  <Border><Border.Child><Image\n    Source=\"Assets/second.png\"/></Border.Child></Border>\n" +
                "  <Image Source=\"./icons/../한글.png\"/>\n</StackPanel>", "images.xaml");

            Assert.That(result.Errors, Is.Empty);
            Assert.That(result.AssetReferences.Select(r => r.Value),
                Is.EqualTo(new[] { "./icons/../한글.png", "Assets/second.png", "./icons/../한글.png" }));
            Assert.That(result.AssetReferences.Select(r => (r.Line, r.Column)),
                Is.EqualTo(new[] { (2, 10), (4, 5), (5, 10) }));
            var resolver = new Resolver();
            XamlDocument restored = XamlIrSerializer.Deserialize(XamlIrSerializer.Serialize(result.Document));
            var panel = (StackPanel)XamlBuilder.Build(restored, resolver);
            Assert.That(resolver.Values, Is.EqualTo(result.AssetReferences.Select(r => r.Value)));
            Assert.That(((Image)panel.Children[0]).Source, Is.SameAs(resolver.Source));
            Assert.That(((Image)((Border)panel.Children[1]).Child).Source, Is.SameAs(resolver.Source));
            Assert.That(((Image)panel.Children[2]).Source, Is.SameAs(resolver.Source));
        }

        [TestCase("")]
        [TestCase("   ")]
        [TestCase("  {StaticResource Image}")]
        public void InvalidImageValuesAreParseErrorsWithLocations(string value)
        {
            XamlParseResult result = ParseImage(value);
            Assert.That(result.Document, Is.Null);
            Assert.That(result.AssetReferences, Is.Empty);
            Assert.That(result.Errors.Count, Is.EqualTo(1));
            Assert.That(result.Errors[0].File, Is.EqualTo("images.xaml"));
            Assert.That(result.Errors[0].Line, Is.EqualTo(2));
            Assert.That(result.Errors[0].Column, Is.EqualTo(3));
        }

        [TestCase("missing.png")]
        [TestCase("/abs.png")]
        [TestCase("pack://images/icon.png")]
        [TestCase("  image.png  ")]
        public void ParserPreservesPathsWithoutCheckingEngineSpecificRules(string value)
        {
            XamlParseResult result = ParseImage(value);
            Assert.That(result.Errors, Is.Empty);
            Assert.That(result.AssetReferences.Single().Value, Is.EqualTo(value));
            Assert.That(result.Document.Root.Properties.Single().TextValue, Is.EqualTo(value));
        }

        [Test]
        public void BuildingImageWithoutResolverReportsPropertyLocation()
        {
            XamlBuildException error = Assert.Throws<XamlBuildException>(() =>
                XamlBuilder.Build(ParseImage("image.png").Document));
            Assert.That(error.Line, Is.EqualTo(2));
            Assert.That(error.Column, Is.EqualTo(3));
            Assert.That(error.Message, Does.Contain("asset resolver"));
        }

        [Test]
        public void ResolverCanReturnNullForMissingRuntimeReference()
        {
            var resolver = new Resolver { Source = null };
            var image = (Image)XamlBuilder.Build(ParseImage("missing.png").Document, resolver);
            Assert.That(image.Source, Is.Null);
            Assert.That(resolver.Values, Is.EqualTo(new[] { "missing.png" }));
        }

        [Test]
        public void ResolverExceptionsIncludePropertyLocation()
        {
            XamlBuildException error = Assert.Throws<XamlBuildException>(() =>
                XamlBuilder.Build(ParseImage("image.png").Document, new FailingResolver()));
            Assert.That(error.Line, Is.EqualTo(2));
            Assert.That(error.Column, Is.EqualTo(3));
            Assert.That(error.InnerException, Is.TypeOf<InvalidOperationException>());
        }

        [Test]
        public void ImagesWithoutSourceNeedNoResolver()
        {
            XamlParseResult result = XamlParser.Parse("<Image " + Namespace + "/>", "images.xaml");
            Assert.That(result.Errors, Is.Empty);
            Assert.That(result.AssetReferences, Is.Empty);
            Assert.That(((Image)XamlBuilder.Build(result.Document)).Source, Is.Null);
        }

        private static XamlParseResult ParseImage(string value) =>
            XamlParser.Parse("<Image " + Namespace + "\n  Source=\"" + value + "\"/>", "images.xaml");

        private sealed class Resolver : IXamlAssetResolver
        {
            internal readonly List<string> Values = new List<string>();
            internal ImageSource Source = new TestImageSource();

            public ImageSource ResolveImage(string value)
            {
                Values.Add(value);
                return Source;
            }
        }

        private sealed class FailingResolver : IXamlAssetResolver
        {
            public ImageSource ResolveImage(string value) => throw new InvalidOperationException("Resolution failed.");
        }

        private sealed class TestImageSource : ImageSource
        {
            public override double Width => 32;
            public override double Height => 16;
        }
    }
}
