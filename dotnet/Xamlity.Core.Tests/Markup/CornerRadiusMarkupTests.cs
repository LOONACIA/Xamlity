using System.Globalization;
using NUnit.Framework;
using Xamlity.Controls;
using Xamlity.Core.Tests.Rendering;
using Xamlity.Layout;
using Xamlity.Markup;
using Xamlity.Tree;

namespace Xamlity.Core.Tests.Markup
{
    public class CornerRadiusMarkupTests
    {
        private const string Namespace = "xmlns=\"https://github.com/loonacia/xamlity\"";

        [TestCase("8", 8, 8, 8, 8)]
        [TestCase("0", 0, 0, 0, 0)]
        [TestCase("1,2,3,4", 1, 2, 3, 4)]
        [TestCase("1 2 3 4", 1, 2, 3, 4)]
        [TestCase(" 1.5, 2.5 3.5,4.5 ", 1.5, 2.5, 3.5, 4.5)]
        [TestCase("1e1", 10, 10, 10, 10)]
        public void ValidValuesRoundTripAndRenderUsingInvariantCulture(string text, double tl, double tr, double br, double bl)
        {
            CultureInfo previous = CultureInfo.CurrentCulture;
            try
            {
                CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("fr-FR");
                XamlParseResult result = XamlParser.Parse("<Border " + Namespace +
                    " Background=\"Blue\" CornerRadius=\"" + text + "\"/>", "rounded.xaml");
                Assert.That(result.Errors, Is.Empty);
                byte[] bytes = XamlIrSerializer.Serialize(result.Document);
                XamlDocument restored = XamlIrSerializer.Deserialize(bytes);
                Assert.That(XamlIrSerializer.Serialize(restored), Is.EqualTo(bytes));
                var border = (Border)XamlBuilder.Build(restored);
                var expected = new CornerRadius(tl, tr, br, bl);
                Assert.That(border.CornerRadius, Is.EqualTo(expected));
                var root = new UIRoot(border);
                root.UpdateLayout(new Size(100, 80));
                var context = new RecordingDrawingContext();
                root.Render(context);
                if (expected == new CornerRadius(0))
                    Assert.That(context.Rectangles, Has.Count.EqualTo(1));
                else
                    Assert.That(context.RoundedRectangles[0].Radius, Is.EqualTo(expected));
            }
            finally
            {
                CultureInfo.CurrentCulture = previous;
            }
        }

        [TestCase("")]
        [TestCase(" ")]
        [TestCase("1,2")]
        [TestCase("1 2 3")]
        [TestCase("1,2,3,4,5")]
        [TestCase("1,,2,3,4")]
        [TestCase(",1")]
        [TestCase("1,")]
        [TestCase("-1")]
        [TestCase("NaN")]
        [TestCase("Infinity")]
        [TestCase("-Infinity")]
        [TestCase("1e999")]
        [TestCase("-1,2,3,4")]
        [TestCase("1,-2,3,4")]
        [TestCase("1,2,-3,4")]
        [TestCase("1,2,3,-4")]
        [TestCase("1,NaN,3,4")]
        [TestCase("1,2,Infinity,4")]
        [TestCase("1,2,3,NaN")]
        [TestCase("Auto")]
        [TestCase("8px")]
        public void InvalidValuesAreImportErrorsWithLocations(string text)
        {
            XamlParseResult result = XamlParser.Parse("<Border " + Namespace + "\n  CornerRadius=\"" + text + "\"/>",
                "rounded.xaml");
            Assert.That(result.Document, Is.Null);
            Assert.That(result.Errors, Has.Count.EqualTo(1));
            XamlError error = result.Errors[0];
            Assert.That(error.File, Is.EqualTo("rounded.xaml"));
            Assert.That(error.Line, Is.EqualTo(2));
            Assert.That(error.Column, Is.EqualTo(3));
            Assert.That(error.Message, Does.Contain("CornerRadius"));
        }
    }
}
