using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Xamlity.Controls;
using Xamlity.Core.Tests.Rendering;
using Xamlity.Layout;
using Xamlity.Markup;
using Xamlity.Properties;
using Xamlity.Rendering;
using Xamlity.Tree;

namespace Xamlity.Core.Tests.Markup
{
    public class XamlBuilderTests
    {
        private const string Namespaces = "xmlns=\"https://github.com/loonacia/xamlity\" " +
            "xmlns:x=\"http://schemas.microsoft.com/winfx/2006/xaml\"";
        private const string View = "<StackPanel " + Namespaces + " x:Name=\"panel\" Margin=\"10,20,0,0\">\n" +
            "  <Border Height=\"11\" />\n" +
            "  <Border x:Name=\"frame\" Width=\"100\" Height=\"80\" HorizontalAlignment=\"Left\"\n" +
            "    Margin=\"5,7,0,0\" Background=\"#123456\" BorderBrush=\"Red\"\n" +
            "    BorderThickness=\"2,3,4,5\" Padding=\"6,8,10,12\">\n" +
            "    <TextBlock x:Name=\"label\" Text=\"한글\" FontSize=\"20\" Foreground=\"Blue\" Margin=\"1,2,0,0\" />\n" +
            "  </Border>\n</StackPanel>";

        [Test]
        public void SerializationPreservesEveryFieldAndOrder()
        {
            XamlDocument original = Parse(View);
            // Preserve both null and empty strings, whitespace, and non-ASCII text.
            original.Root.Name = "";
            original.Root.Properties.Add(new XamlPropertyNode
            {
                PropertyName = "Name", TextValue = "", Line = 9, Column = 17
            });
            original.Root.Properties.Add(new XamlPropertyNode
            {
                PropertyName = "Name", TextValue = "  한글 & text  ", Line = 10, Column = 3
            });
            byte[] bytes = XamlIrSerializer.Serialize(original);
            XamlDocument restored = XamlIrSerializer.Deserialize(bytes);
            AssertObjectEqual(original.Root, restored.Root);
            Assert.That(restored.Root, Is.Not.SameAs(original.Root));
            Assert.That(XamlIrSerializer.Serialize(restored), Is.EqualTo(bytes));
        }

        [Test]
        public void SerializationUsesVersionOneFieldLayout()
        {
            var document = new XamlDocument
            {
                Root = new XamlObjectNode { TypeName = "T", Name = "", Line = 2, Column = 3 }
            };
            document.Root.Properties.Add(new XamlPropertyNode
            {
                PropertyName = "P", Line = 4, Column = 5, TextValue = "한"
            });
            byte[] expected =
            {
                88, 65, 73, 82, 1, 0, 0, 0, // XAIR, version 1
                1, 1, 84, 1, 0, // TypeName, empty Name
                2, 0, 0, 0, 3, 0, 0, 0, 1, 0, 0, 0, // Line, Column, property count
                1, 1, 80, 4, 0, 0, 0, 5, 0, 0, 0, // PropertyName, Line, Column
                1, 3, 0xED, 0x95, 0x9C, 0, 0, 0, 0 // TextValue, object count
            };
            Assert.That(XamlIrSerializer.Serialize(document), Is.EqualTo(expected));
            AssertObjectEqual(document.Root, XamlIrSerializer.Deserialize(expected).Root);
        }

        [Test]
        public void RejectsWrongMagic()
        {
            byte[] data = XamlIrSerializer.Serialize(Parse(View));
            data[0] = (byte)'Y';
            var exception = Assert.Throws<InvalidDataException>(() => XamlIrSerializer.Deserialize(data));
            Assert.That(exception.Message, Does.Contain("magic").And.Contain("XAIR")
                .And.Contain("Expected version 1").And.Contain("actual version 1"));
        }

        [TestCase(0)]
        [TestCase(2)]
        [TestCase(-1)]
        public void RejectsWrongVersion(int version)
        {
            byte[] data = XamlIrSerializer.Serialize(Parse(View));
            using (var writer = new BinaryWriter(new MemoryStream(data)))
            {
                writer.BaseStream.Position = 4;
                writer.Write(version);
            }
            var exception = Assert.Throws<InvalidDataException>(() => XamlIrSerializer.Deserialize(data));
            Assert.That(exception.Message, Does.Contain("Expected version 1").And.Contain("actual version " + version));
        }

        [Test]
        public void RejectsEveryTruncatedPrefixIncludingUtf8StringsAndNestedNodes()
        {
            byte[] data = XamlIrSerializer.Serialize(Parse(View));
            for (int length = 0; length < data.Length; length++)
            {
                byte[] truncated = data.Take(length).ToArray();
                var exception = Assert.Throws<InvalidDataException>(() => XamlIrSerializer.Deserialize(truncated),
                    "Truncated at byte " + length);
                Assert.That(exception.Message, Does.Contain("Expected version 1")
                    .And.Contain(length < 8 ? "actual version unavailable" : "actual version 1"));
            }
        }

        [Test]
        public void ParsedSerializedBuiltViewRendersRectanglesAndKoreanGlyphsAtAbsoluteCoordinates()
        {
            XamlDocument document = XamlIrSerializer.Deserialize(XamlIrSerializer.Serialize(Parse(View)));
            var panel = (StackPanel)XamlBuilder.Build(document);
            Assert.That(panel.Name, Is.EqualTo("panel"));
            Assert.That(panel.Children.Count, Is.EqualTo(2));
            Assert.That(((Border)panel.Children[0]).Height, Is.EqualTo(11));
            var border = (Border)panel.Children[1];
            var text = (TextBlock)border.Child;
            Assert.That(border.Name, Is.EqualTo("frame"));
            Assert.That(text.Name, Is.EqualTo("label"));
            Assert.That(text.Text, Is.EqualTo("한글"));
            Assert.That(border.BorderThickness, Is.EqualTo(new Thickness(2, 3, 4, 5)));
            Assert.That(border.Padding, Is.EqualTo(new Thickness(6, 8, 10, 12)));

            var font = new FontHandle(new object());
            var measurer = new TextBlockTests.FakeTextMeasurer();
            var root = new UIRoot(panel, measurer, font);
            root.UpdateLayout(new Size(400, 300));
            var context = new RecordingDrawingContext();
            root.Render(context);

            Assert.That(context.Rectangles.Select(command => command.Rect), Is.EqualTo(new[]
            {
                new Rect(15, 38, 100, 3), new Rect(15, 113, 100, 5),
                new Rect(15, 41, 2, 72), new Rect(111, 41, 4, 72), new Rect(17, 41, 94, 72)
            }));
            Assert.That(context.Rectangles.Select(command => ((SolidColorBrush)command.Brush).Color),
                Is.EqualTo(new[] { Color.FromRgb(255, 0, 0), Color.FromRgb(255, 0, 0),
                    Color.FromRgb(255, 0, 0), Color.FromRgb(255, 0, 0), Color.FromRgb(0x12, 0x34, 0x56) }));
            Assert.That(context.GlyphRuns.Count, Is.EqualTo(1));
            var glyphs = context.GlyphRuns[0];
            Assert.That(glyphs.BaselineOrigin, Is.EqualTo(new Point(24, 67)));
            Assert.That(((SolidColorBrush)glyphs.Foreground).Color, Is.EqualTo(Color.FromRgb(0, 0, 255)));
            Assert.That(glyphs.Run.Font, Is.EqualTo(font));
            Assert.That(glyphs.Run.FontSize, Is.EqualTo(20));
            Assert.That(glyphs.Run.GlyphIndices, Is.EqualTo(new uint[] { '한' + 1, '글' + 1 }));
            Assert.That(glyphs.Run.AdvanceWidths, Is.EqualTo(new[] { 10.0, 10.0 }));
            Assert.That(measurer.MetricsCalls, Is.EqualTo(1));
            Assert.That(text.DesiredSize, Is.EqualTo(new Size(21, 26)));
        }

        [Test]
        [SetCulture("fr-FR")]
        public void BuildsDependencyPropertiesBeforeClrWrappersAndConvertsClrProperties()
        {
            var document = DocumentFor(typeof(XamlParserTests.DerivedProperties));
            document.Root.Properties.Add(Property("Value", "1.25"));
            document.Root.Properties.Add(Property("Text", "  한글  "));
            var instance = (XamlParserTests.DerivedProperties)XamlBuilder.Build(document);
            Assert.That(instance.GetValue(XamlParserTests.ClrProperties.ValueProperty), Is.EqualTo(1.25));
            Assert.That(instance.Value, Is.Null);
            Assert.That(instance.Text, Is.EqualTo("  한글  "));
        }

        [Test]
        public void RoundTripsPropertyElementsAndRendersBrushColorsInChildOrder()
        {
            string xaml = "<StackPanel " + Namespaces + ">\n  <StackPanel.Children>\n" +
                "    <Border Height=\"20\"><Border.Background><SolidColorBrush Color=\"#336699\"/>" +
                "</Border.Background><Border.Child><Border Height=\"10\"><Border.Background>" +
                "<SolidColorBrush Color=\"Blue\"/></Border.Background></Border></Border.Child></Border>\n" +
                "    <Border Height=\"30\"><Border.Background><SolidColorBrush Color=\"Red\"/>" +
                "</Border.Background></Border>\n  </StackPanel.Children>\n</StackPanel>";
            XamlDocument original = Parse(xaml);
            XamlDocument restored = XamlIrSerializer.Deserialize(XamlIrSerializer.Serialize(original));
            AssertObjectEqual(original.Root, restored.Root);
            var panel = (StackPanel)XamlBuilder.Build(restored);
            Assert.That(panel.Children.Count, Is.EqualTo(2));
            Assert.That(panel.Children.Cast<Border>().Select(b => b.Height), Is.EqualTo(new[] { 20.0, 30.0 }));
            var first = (Border)panel.Children[0];
            Assert.That(first.Background, Is.TypeOf<SolidColorBrush>());
            Assert.That(((SolidColorBrush)first.Background).Color, Is.EqualTo(Color.FromRgb(51, 102, 153)));
            Assert.That(first.Child, Is.TypeOf<Border>());
            var root = new UIRoot(panel);
            root.UpdateLayout(new Size(100, 100));
            var context = new RecordingDrawingContext();
            root.Render(context);
            Assert.That(context.Rectangles.Select(c => ((SolidColorBrush)c.Brush).Color), Is.EqualTo(new[] {
                Color.FromRgb(51, 102, 153), Color.FromRgb(0, 0, 255), Color.FromRgb(255, 0, 0) }));

            // Brushes are not Freezables: changing a brush does not invalidate its owner.
            ((SolidColorBrush)first.Background).Color = Color.FromRgb(1, 2, 3);
            Assert.That(root.IsRenderDirty, Is.False);
        }

        [Test]
        public void BuildsEmptyExplicitCollection()
        {
            var document = Parse("<StackPanel " + Namespaces + "><Panel.Children /></StackPanel>");
            var panel = (StackPanel)XamlBuilder.Build(XamlIrSerializer.Deserialize(XamlIrSerializer.Serialize(document)));
            Assert.That(panel.Children, Is.Empty);
        }

        [Test]
        public void BuildsObjectDependencyPropertyBeforeClrWrapper()
        {
            var document = DocumentFor(typeof(ObjectProperties));
            var property = Property("Value", null);
            property.Objects.Add(DocumentFor(typeof(SolidColorBrush)).Root);
            document.Root.Properties.Add(property);
            var instance = (ObjectProperties)XamlBuilder.Build(document);
            Assert.That(instance.GetValue(ObjectProperties.ValueProperty), Is.TypeOf<SolidColorBrush>());
            Assert.That(instance.Value, Is.Null);
        }

        [TestCase("Brushes")]
        [TestCase("ConcreteBrushes")]
        public void BuildsReadOnlyGenericCollectionsOfNonElements(string name)
        {
            var document = DocumentFor(typeof(ObjectProperties));
            var property = Property(name, null);
            foreach (string color in new[] { "Red", "Blue" })
            {
                XamlObjectNode brush = DocumentFor(typeof(SolidColorBrush)).Root;
                brush.Properties.Add(Property("Color", color));
                property.Objects.Add(brush);
            }
            document.Root.Properties.Add(property);
            // Build twice to exercise the cached ICollection<T>.Add method.
            for (int index = 0; index < 2; index++)
            {
                var instance = (ObjectProperties)XamlBuilder.Build(document);
                IEnumerable<Brush> brushes = name == "Brushes" ? instance.Brushes : instance.ConcreteBrushes;
                Assert.That(brushes.Cast<SolidColorBrush>().Select(b => b.Color),
                    Is.EqualTo(new[] { Color.FromRgb(255, 0, 0), Color.FromRgb(0, 0, 255) }));
            }
        }

        public class ObjectProperties : DependencyObject
        {
            public static readonly DependencyProperty ValueProperty = DependencyProperty.Register(
                "Value", typeof(Brush), typeof(ObjectProperties));
            public Brush Value { get; set; }
            public ICollection<Brush> Brushes { get; } = new List<Brush>();
            public List<Brush> ConcreteBrushes { get; } = new List<Brush>();
        }

        [Test]
        public void AppliesPropertiesInDocumentOrderAndBuildsIndependentTrees()
        {
            var document = DocumentFor(typeof(OrderedProperties));
            document.Root.Properties.Add(Property("Second", "b"));
            document.Root.Properties.Add(Property("First", "a"));
            var instance = (OrderedProperties)XamlBuilder.Build(document);
            Assert.That(instance.Assignments, Is.EqualTo(new[] { "Second:b", "First:a" }));

            var parsed = Parse("<Border " + Namespaces + " Name=\"frame\" Width=\"Auto\"><StackPanel /></Border>");
            var first = (Border)XamlBuilder.Build(parsed);
            var second = (Border)XamlBuilder.Build(parsed);
            Assert.That(first.Name, Is.EqualTo("frame"));
            Assert.That(double.IsNaN(first.Width), Is.True);
            Assert.That(second, Is.Not.SameAs(first));
            Assert.That(second.Child, Is.Not.SameAs(first.Child));
            Assert.That(((StackPanel)first.Child).Children, Is.Empty);
        }

        [TestCase("Width", "bad")]
        [TestCase("Missing", "1")]
        [TestCase("IsMeasureValid", "true")]
        public void ReportsInvalidPropertiesAtPropertyLocation(string name, string value)
        {
            var document = DocumentFor(typeof(Border));
            document.Root.Properties.Add(Property(name, value));
            XamlBuildException exception = AssertBuildError(document, 12, 8);
            Assert.That(exception.Message, Does.Contain(name));
        }

        [Test]
        public void ReportsMissingTypeAtObjectLocation()
        {
            var document = DocumentFor(typeof(Border));
            document.Root.TypeName = "Xamlity.Controls.Missing, Xamlity.Core";
            Assert.That(AssertBuildError(document, 3, 4).Message, Does.Contain("Missing"));
        }

        [Test]
        public void ReportsConstructorAndSetterFailuresWithInnerExceptions()
        {
            AssertBuildError(DocumentFor(typeof(ThrowingConstructor)), 3, 4);
            var document = DocumentFor(typeof(OrderedProperties));
            document.Root.Properties.Add(Property("Fail", "value"));
            Assert.That(AssertBuildError(document, 12, 8).Message, Does.Contain("Fail"));
        }

        [Test]
        public void PreservesNestedFailureLocationInsteadOfReplacingItWithParentLocation()
        {
            var document = Parse(View);
            XamlObjectNode text = document.Root.Properties.Last().Objects[1].Properties.Last().Objects[0];
            text.Properties.Add(Property("FontSize", "bad"));
            Assert.That(AssertBuildError(document, 12, 8).Message, Does.Contain("FontSize"));
        }

        [Test]
        public void ReportsInvalidSingleContentAtPropertyLocation()
        {
            var document = DocumentFor(typeof(Border));
            var child = Property("Child", null);
            child.Objects.Add(DocumentFor(typeof(Border)).Root);
            child.Objects.Add(DocumentFor(typeof(Border)).Root);
            document.Root.Properties.Add(child);
            Assert.That(AssertBuildError(document, 12, 8).Message, Does.Contain("exactly one object"));
        }

        public class OrderedProperties
        {
            public List<string> Assignments { get; } = new List<string>();
            public string First { set => Assignments.Add("First:" + value); }
            public string Second { set => Assignments.Add("Second:" + value); }
            public string Fail { set => throw new InvalidOperationException("Setter failed."); }
        }

        public class ThrowingConstructor
        {
            public ThrowingConstructor() => throw new InvalidOperationException("Constructor failed.");
        }

        private static XamlDocument Parse(string text)
        {
            var result = XamlParser.Parse(text, "view.xaml");
            Assert.That(result.Errors, Is.Empty, string.Join("\n", result.Errors));
            return result.Document;
        }

        private static XamlDocument DocumentFor(Type type) => new XamlDocument
        {
            Root = new XamlObjectNode { TypeName = type.AssemblyQualifiedName, Line = 3, Column = 4 }
        };

        private static XamlPropertyNode Property(string name, string text) => new XamlPropertyNode
        {
            PropertyName = name, TextValue = text, Line = 12, Column = 8
        };

        private static XamlBuildException AssertBuildError(XamlDocument document, int line, int column)
        {
            var exception = Assert.Throws<XamlBuildException>(() => XamlBuilder.Build(document));
            Assert.That(exception.Line, Is.EqualTo(line));
            Assert.That(exception.Column, Is.EqualTo(column));
            Assert.That(exception.Message, Does.StartWith($"({line},{column}): "));
            Assert.That(exception.InnerException, Is.Not.Null);
            return exception;
        }

        private static void AssertObjectEqual(XamlObjectNode expected, XamlObjectNode actual)
        {
            Assert.That(actual.TypeName, Is.EqualTo(expected.TypeName));
            Assert.That(actual.Name, Is.EqualTo(expected.Name));
            Assert.That(actual.Line, Is.EqualTo(expected.Line));
            Assert.That(actual.Column, Is.EqualTo(expected.Column));
            Assert.That(actual.Properties.Count, Is.EqualTo(expected.Properties.Count));
            for (int index = 0; index < expected.Properties.Count; index++)
            {
                XamlPropertyNode expectedProperty = expected.Properties[index];
                XamlPropertyNode actualProperty = actual.Properties[index];
                Assert.That(actualProperty.PropertyName, Is.EqualTo(expectedProperty.PropertyName));
                Assert.That(actualProperty.Line, Is.EqualTo(expectedProperty.Line));
                Assert.That(actualProperty.Column, Is.EqualTo(expectedProperty.Column));
                Assert.That(actualProperty.TextValue, Is.EqualTo(expectedProperty.TextValue));
                Assert.That(actualProperty.Objects.Count, Is.EqualTo(expectedProperty.Objects.Count));
                for (int child = 0; child < expectedProperty.Objects.Count; child++)
                    AssertObjectEqual(expectedProperty.Objects[child], actualProperty.Objects[child]);
            }
        }
    }
}
