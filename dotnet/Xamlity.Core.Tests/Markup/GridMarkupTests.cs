using System.Globalization;
using System.Linq;
using NUnit.Framework;
using Xamlity.Core.Tests.Rendering;
using Xamlity.Layout;
using Xamlity.Markup;
using Xamlity.Tree;

namespace Xamlity.Core.Tests.Markup
{
    public class GridMarkupTests
    {
        private const string Namespace = "xmlns=\"https://github.com/loonacia/xamlity\"";

        [Test]
        public void DefinitionsAndAttachedPropertiesRoundTripAndRenderAtAbsoluteCoordinates()
        {
            string text = "<Border " + Namespace + " Padding=\"7,11,0,0\">\n" +
                "  <Grid>\n" +
                "    <Grid.ColumnDefinitions>\n" +
                "      <ColumnDefinition Width=\"100\"/><ColumnDefinition Width=\"Auto\"/><ColumnDefinition/>\n" +
                "    </Grid.ColumnDefinitions>\n" +
                "    <Grid.RowDefinitions>\n" +
                "      <RowDefinition Height=\"Auto\"/><RowDefinition/><RowDefinition Height=\"2*\"/>\n" +
                "    </Grid.RowDefinitions>\n" +
                "    <Border Grid.Row=\"0\" Grid.Column=\"1\" Width=\"40\" Height=\"20\" Background=\"Red\"/>\n" +
                "    <Border Grid.Row=\"2\" Grid.Column=\"2\" MinWidth=\"10\" MinHeight=\"10\" Background=\"Blue\"/>\n" +
                "    <Border Grid.Row=\"1\" Grid.Column=\"0\" Width=\"30\" Height=\"30\" " +
                "HorizontalAlignment=\"Left\" VerticalAlignment=\"Top\" Background=\"Green\"/>\n" +
                "  </Grid>\n</Border>";
            XamlParseResult result = XamlParser.Parse(text, "grid.xaml");
            Assert.That(result.Errors, Is.Empty);
            byte[] bytes = XamlIrSerializer.Serialize(result.Document);
            XamlDocument restored = XamlIrSerializer.Deserialize(bytes);
            Assert.That(XamlIrSerializer.Serialize(restored), Is.EqualTo(bytes));
            var border = (Xamlity.Controls.Border)XamlBuilder.Build(restored);
            var grid = (Grid)border.Child;
            var root = new UIRoot(border);
            root.UpdateLayout(new Size(307, 211));
            Assert.That(grid.DesiredSize, Is.EqualTo(new Size(150, 60)));
            Assert.That(grid.ColumnDefinitions.Select(c => c.ActualWidth), Is.EqualTo(new[] { 100.0, 40.0, 160.0 }));
            Assert.That(grid.RowDefinitions.Select(r => r.ActualHeight), Is.EqualTo(new[] { 20.0, 60.0, 120.0 }));
            Assert.That(Grid.GetRow(grid.Children[1]), Is.EqualTo(2));
            Assert.That(Grid.GetColumn(grid.Children[1]), Is.EqualTo(2));
            var context = new RecordingDrawingContext();
            root.Render(context);
            Assert.That(context.Rectangles.Select(r => r.Rect), Is.EqualTo(new[] {
                new Rect(107, 11, 40, 20), new Rect(147, 91, 160, 120), new Rect(7, 31, 30, 30) }));
        }

        [TestCase("Auto", GridUnitType.Auto, 1)]
        [TestCase(" aUtO ", GridUnitType.Auto, 1)]
        [TestCase("*", GridUnitType.Star, 1)]
        [TestCase("2.5*", GridUnitType.Star, 2.5)]
        [TestCase("0*", GridUnitType.Star, 0)]
        [TestCase("100", GridUnitType.Pixel, 100)]
        [TestCase(" 12.5 ", GridUnitType.Pixel, 12.5)]
        [TestCase("1e2", GridUnitType.Pixel, 100)]
        public void GridLengthTextConvertsForBothDefinitionProperties(string text, GridUnitType unit, double value)
        {
            CultureInfo previous = CultureInfo.CurrentCulture;
            try
            {
                CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("fr-FR");
                XamlParseResult result = XamlParser.Parse("<Grid " + Namespace + ">" +
                    "<Grid.ColumnDefinitions><ColumnDefinition Width=\"" + text + "\"/></Grid.ColumnDefinitions>" +
                    "<Grid.RowDefinitions><RowDefinition Height=\"" + text +
                    "\"/></Grid.RowDefinitions></Grid>", "grid.xaml");
                Assert.That(result.Errors, Is.Empty);
                var grid = (Grid)XamlBuilder.Build(result.Document);
                Assert.That(grid.ColumnDefinitions[0].Width, Is.EqualTo(new GridLength(value, unit)));
                Assert.That(grid.RowDefinitions[0].Height, Is.EqualTo(new GridLength(value, unit)));
            }
            finally
            {
                CultureInfo.CurrentCulture = previous;
            }
        }

        [TestCase("")]
        [TestCase("-1")]
        [TestCase("-2*")]
        [TestCase("NaN")]
        [TestCase("NaN*")]
        [TestCase("Infinity")]
        [TestCase("Infinity*")]
        [TestCase("1e999")]
        [TestCase("12px")]
        [TestCase("2cm")]
        [TestCase("2.5px*")]
        [TestCase("Auto*")]
        [TestCase("**")]
        [TestCase("2,5*")]
        public void InvalidGridLengthsAreImportErrorsWithLocations(string text)
        {
            XamlParseResult result = XamlParser.Parse("<Grid " + Namespace + ">\n" +
                "<Grid.ColumnDefinitions>\n<ColumnDefinition Width=\"" + text +
                "\"/>\n</Grid.ColumnDefinitions></Grid>", "grid.xaml");
            Assert.That(result.Document, Is.Null);
            Assert.That(result.Errors, Has.Count.EqualTo(1));
            XamlError error = result.Errors[0];
            Assert.That(error.File, Is.EqualTo("grid.xaml"));
            Assert.That(error.Line, Is.EqualTo(3));
            Assert.That(error.Column, Is.EqualTo(19));
            Assert.That(error.Message, Does.Contain("GridLength"));
        }

        [TestCase("ColumnDefinition", "ActualWidth")]
        [TestCase("RowDefinition", "ActualHeight")]
        public void ReadOnlyDefinitionPropertiesAreRejected(string type, string property)
        {
            string collection = type == "RowDefinition" ? "RowDefinitions" : "ColumnDefinitions";
            XamlParseResult result = XamlParser.Parse("<Grid " + Namespace + "><Grid." + collection + "><" + type +
                " " + property + "=\"20\"/></Grid." + collection + "></Grid>", "grid.xaml");
            Assert.That(result.Errors, Is.Not.Empty);
            Assert.That(result.Document, Is.Null);
        }

        [Test]
        public void DefinitionCollectionsRejectWrongObjectTypesDuringImport()
        {
            XamlParseResult result = XamlParser.Parse("<Grid " + Namespace + ">" +
                "<Grid.RowDefinitions><ColumnDefinition/></Grid.RowDefinitions></Grid>", "grid.xaml");
            Assert.That(result.Errors, Is.Not.Empty);
            Assert.That(result.Document, Is.Null);
        }
    }
}
