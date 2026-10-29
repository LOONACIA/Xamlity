using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using Xamlity.Controls;
using Xamlity.Layout;
using Xamlity.Markup;
using Xamlity.Properties;
using Xamlity.Rendering;
using Xamlity.Tree;

namespace Xamlity.Core.Tests.Markup
{
    public class XamlParserTests
    {
        private const string Namespaces = "xmlns=\"https://github.com/loonacia/xamlity\" " +
            "xmlns:x=\"http://schemas.microsoft.com/winfx/2006/xaml\"";

        [Test]
        public void ParsesDocumentWithOriginalValuesNamesOrderedChildrenAndLocations()
        {
            string text = "<Border " + Namespaces + "\n" +
                "  x:Name=\"frame\" Width=\" Auto \" Background=\"#8aBc\">\n" +
                "  <StackPanel Orientation=\"horizontal\">\n" +
                "    <TextBlock Text=\"  한글 &amp; text  \" FontSize=\"1.25e1\" />\n" +
                "    <Border Height=\"32\" />\n" +
                "  </StackPanel>\n</Border>";
            XamlParseResult result = XamlParser.Parse(text, "view.xaml");
            Assert.That(result.Errors, Is.Empty);
            XamlObjectNode root = result.Document.Root;
            Assert.That(root.TypeName, Is.EqualTo("Xamlity.Controls.Border, Xamlity.Core"));
            Assert.That(Type.GetType(root.TypeName), Is.EqualTo(typeof(Border)));
            Assert.That(root.Name, Is.EqualTo("frame"));
            AssertLocation(root.Line, root.Column, 1, 2);
            Assert.That(root.Properties.Select(p => p.PropertyName), Is.EqualTo(new[] { "Width", "Background", "Child" }));
            Assert.That(root.Properties[0].TextValue, Is.EqualTo(" Auto "));
            Assert.That(root.Properties[0].Objects, Is.Empty);
            AssertLocation(root.Properties[0].Line, root.Properties[0].Column, 2, 18);
            Assert.That(root.Properties[1].TextValue, Is.EqualTo("#8aBc"));
            XamlPropertyNode content = root.Properties[2];
            Assert.That(content.TextValue, Is.Null);
            AssertLocation(content.Line, content.Column, 3, 4);
            XamlObjectNode panel = content.Objects.Single();
            Assert.That(panel.TypeName, Is.EqualTo("Xamlity.Layout.StackPanel, Xamlity.Core"));
            Assert.That(panel.Name, Is.Null);
            AssertLocation(panel.Line, panel.Column, 3, 4);
            Assert.That(panel.Properties[0].TextValue, Is.EqualTo("horizontal"));
            XamlPropertyNode children = panel.Properties[1];
            Assert.That(children.PropertyName, Is.EqualTo("Children"));
            Assert.That(children.Objects.Select(c => c.TypeName), Is.EqualTo(new[] {
                "Xamlity.Controls.TextBlock, Xamlity.Core", "Xamlity.Controls.Border, Xamlity.Core" }));
            AssertLocation(children.Line, children.Column, 4, 6);
            Assert.That(children.Objects[0].Properties[0].TextValue, Is.EqualTo("  한글 & text  "));
            Assert.That(children.Objects[0].Properties[1].TextValue, Is.EqualTo("1.25e1"));
            AssertLocation(children.Objects[0].Properties[0].Line, children.Objects[0].Properties[0].Column, 4, 16);
            AssertLocation(children.Objects[1].Line, children.Objects[1].Column, 5, 6);
        }

        [Test]
        public void ContentPropertyIsInheritedAndTextBlockHasNone()
        {
            Assert.That(typeof(Border).GetCustomAttribute<ContentPropertyAttribute>(true).Name, Is.EqualTo("Child"));
            Assert.That(typeof(StackPanel).GetCustomAttribute<ContentPropertyAttribute>(true).Name, Is.EqualTo("Children"));
            Assert.That(typeof(TextBlock).GetCustomAttribute<ContentPropertyAttribute>(true), Is.Null);
            var usage = typeof(ContentPropertyAttribute).GetCustomAttribute<AttributeUsageAttribute>();
            Assert.That(usage.ValidOn, Is.EqualTo(AttributeTargets.Class));
            Assert.That(usage.Inherited, Is.True);
        }

        [Test]
        public void AcceptsNamespaceAliasesCommentsWhitespaceAndEmptyContent()
        {
            var result = XamlParser.Parse("<?xml version=\"1.0\"?>\n" +
                "<ui:StackPanel xmlns:ui=\"https://github.com/loonacia/xamlity\" " +
                "xmlns:unused=\"urn:unused\" xmlns:q=\"http://schemas.microsoft.com/winfx/2006/xaml\">\n" +
                "<!-- comment --><?note ignored?><ui:TextBlock q:Name=\"label\" Text=\"\" />\n" +
                "<![CDATA[  ]]></ui:StackPanel><!-- trailing -->", "view.xaml");
            Assert.That(result.Errors, Is.Empty);
            Assert.That(result.Document.Root.Properties.Single().Objects.Single().Name, Is.EqualTo("label"));
        }

        [TestCase("Missing")]
        [TestCase("Panel")]
        [TestCase("Thickness")]
        [TestCase("UIElement")]
        [TestCase("FrameworkElement")]
        [TestCase("border")]
        public void RejectsUnknownAbstractNonElementAndUnmappedTypes(string name)
        {
            AssertError(XamlParser.Parse("\n<" + name + " " + Namespaces + " />", "view.xaml"),
                2, 2, "Unknown element '" + name + "'.");
        }

        [TestCase("")]
        [TestCase("urn:other")]
        [TestCase("clr-namespace:Xamlity.Controls;assembly=Xamlity.Core")]
        public void RejectsUnsupportedElementNamespace(string xmlNamespace)
        {
            AssertError(XamlParser.Parse("\n<Border xmlns=\"" + xmlNamespace + "\" />", "view.xaml"),
                2, 2, "XML namespace '" + xmlNamespace + "' is not supported.");
        }

        [TestCase("x:Class=\"View\"", "XAML directive 'x:Class' is not supported.")]
        [TestCase("x:Key=\"key\"", "XAML directive 'x:Key' is not supported.")]
        [TestCase("x:Type=\"Border\"", "XAML directive 'x:Type' is not supported.")]
        [TestCase("x:DataType=\"View\"", "XAML directive 'x:DataType' is not supported.")]
        [TestCase("Unknown=\"1\"", "Unknown or read-only property 'Unknown' on type 'Border'.")]
        [TestCase("IsMeasureValid=\"true\"", "Unknown or read-only property 'IsMeasureValid' on type 'Border'.")]
        [TestCase("RenderSize=\"1\"", "Unknown or read-only property 'RenderSize' on type 'Border'.")]
        [TestCase("Grid.Row=\"1\"", "Attached properties are not supported: 'Grid.Row'.")]
        [TestCase("Border.Width=\"1\"", "Attached properties are not supported: 'Border.Width'.")]
        [TestCase("Width=\"12px\"", "Property 'Width': '12px' is not a valid Double value.")]
        [TestCase("MinWidth=\"Auto\"", "Property 'MinWidth': 'Auto' is not a valid Double value.")]
        [TestCase("Child=\"TextBlock\"", "Property 'Child': Cannot convert text to type 'Xamlity.Tree.UIElement'.")]
        [TestCase("Background=\"nocolor\"", "Property 'Background': 'nocolor' is not a valid Brush value.")]
        [TestCase("Name=\"{Binding Name}\"", "Property 'Name': Markup extensions are not supported.")]
        [TestCase("x:Name=\"{Binding Name}\"", "Markup extensions are not supported.")]
        public void ReportsAttributeErrorsAtAttributeName(string attribute, string message)
        {
            AssertError(XamlParser.Parse("<Border " + Namespaces + "\n  " + attribute + " />", "view.xaml"),
                2, 3, message);
        }

        [Test]
        public void RejectsUnsupportedAttributeNamespace()
        {
            AssertError(XamlParser.Parse("<Border " + Namespaces + " xmlns:c=\"urn:other\"\n  c:Width=\"1\" />", "view.xaml"),
                2, 3, "XML namespace 'urn:other' is not supported.");
        }

        [TestCase("<Border.Child />", "Property 'Child' requires exactly one object.")]
        [TestCase("<x:Type />", "XAML directive 'x:Type' is not supported.")]
        [TestCase("<Border.Resources />", "Unknown or read-only property 'Resources' on type 'Border'.")]
        public void RejectsUnsupportedElementSyntax(string child, string message)
        {
            AssertError(XamlParser.Parse("<Border " + Namespaces + ">\n  " + child + "\n</Border>", "view.xaml"),
                2, 4, message);
        }

        [Test]
        public void ParsesBrushPropertyElementWithObjectAndPropertyLocations()
        {
            var result = XamlParser.Parse("<Border " + Namespaces + ">\n" +
                "  <Border.Background>\n    <SolidColorBrush Color=\"#336699\"/>\n" +
                "  </Border.Background>\n</Border>", "view.xaml");
            Assert.That(result.Errors, Is.Empty);
            XamlPropertyNode background = result.Document.Root.Properties.Single();
            Assert.That(background.PropertyName, Is.EqualTo("Background"));
            Assert.That(background.TextValue, Is.Null);
            AssertLocation(background.Line, background.Column, 2, 4);
            XamlObjectNode brush = background.Objects.Single();
            Assert.That(brush.TypeName, Is.EqualTo("Xamlity.Rendering.SolidColorBrush, Xamlity.Core"));
            AssertLocation(brush.Line, brush.Column, 3, 6);
            XamlPropertyNode color = brush.Properties.Single();
            Assert.That(color.PropertyName, Is.EqualTo("Color"));
            Assert.That(color.TextValue, Is.EqualTo("#336699"));
            AssertLocation(color.Line, color.Column, 3, 22);
        }

        [TestCase("StackPanel")]
        [TestCase("Panel")]
        public void ParsesExplicitCollectionWithCurrentOrBaseOwner(string owner)
        {
            var result = XamlParser.Parse("<StackPanel " + Namespaces + ">\n  <" + owner + ".Children>\n" +
                "    <Border />\n    <TextBlock />\n  </" + owner + ".Children>\n</StackPanel>", "view.xaml");
            Assert.That(result.Errors, Is.Empty);
            XamlPropertyNode children = result.Document.Root.Properties.Single();
            Assert.That(children.PropertyName, Is.EqualTo("Children"));
            Assert.That(children.TextValue, Is.Null);
            AssertLocation(children.Line, children.Column, 2, 4);
            Assert.That(children.Objects.Select(c => Type.GetType(c.TypeName)),
                Is.EqualTo(new[] { typeof(Border), typeof(TextBlock) }));
        }

        [Test]
        public void ParsesExplicitSingleChildAndEmptyCollectionWithNamespaceAlias()
        {
            var result = XamlParser.Parse("<Border " + Namespaces + ">\n  <Border.Child>\n" +
                "    <StackPanel><ui:Panel.Children xmlns:ui=\"https://github.com/loonacia/xamlity\" />" +
                "</StackPanel>\n  </Border.Child>\n</Border>", "view.xaml");
            Assert.That(result.Errors, Is.Empty);
            XamlPropertyNode child = result.Document.Root.Properties.Single();
            Assert.That(child.PropertyName, Is.EqualTo("Child"));
            AssertLocation(child.Line, child.Column, 2, 4);
            XamlPropertyNode children = child.Objects.Single().Properties.Single();
            Assert.That(children.PropertyName, Is.EqualTo("Children"));
            Assert.That(children.TextValue, Is.Null);
            Assert.That(children.Objects, Is.Empty);
        }

        [TestCase("<Border.Child></Border.Child>", "Property 'Child' requires exactly one object.")]
        [TestCase("<Border.Child><Border /><Border /></Border.Child>", "Property 'Child' requires exactly one object.")]
        [TestCase("<Grid.Row />", "Attached property elements are not supported: 'Grid.Row'.")]
        [TestCase("<Canvas.Left />", "Attached property elements are not supported: 'Canvas.Left'.")]
        [TestCase("<TextBlock.Text />", "Attached property elements are not supported: 'TextBlock.Text'.")]
        [TestCase("<Border.IsMeasureValid />", "Unknown or read-only property 'IsMeasureValid' on type 'Border'.")]
        [TestCase("<Border.Child xmlns=\"urn:other\" />", "XML namespace 'urn:other' is not supported.")]
        public void ReportsPropertyElementErrorsAtOpeningElement(string child, string message)
        {
            AssertError(XamlParser.Parse("<Border " + Namespaces + ">\n  " + child + "\n</Border>", "view.xaml"),
                2, 4, message);
        }

        [TestCase("Width=\"1\"")]
        [TestCase("x:Name=\"children\"")]
        [TestCase("xml:space=\"preserve\"")]
        public void RejectsPropertyElementAttributesAtAttributeLocation(string attribute)
        {
            AssertError(XamlParser.Parse("<StackPanel " + Namespaces + ">\n  <StackPanel.Children\n    " +
                attribute + " />\n</StackPanel>", "view.xaml"), 3, 5,
                "Attributes on property elements are not supported.");
        }

        [Test]
        public void RejectsDirectlyNestedPropertyElements()
        {
            AssertError(XamlParser.Parse("<StackPanel " + Namespaces + ">\n  <StackPanel.Children>\n" +
                "    <Border.Child />\n  </StackPanel.Children>\n</StackPanel>", "view.xaml"),
                3, 6, "Property elements cannot directly contain property elements.");
        }

        [TestCase("abc", 24)]
        [TestCase("<![CDATA[abc]]>", 33)]
        public void RejectsTextInCollectionPropertyElement(string text, int column)
        {
            AssertError(XamlParser.Parse("<StackPanel " + Namespaces + ">\n  <StackPanel.Children>" + text +
                "</StackPanel.Children>\n</StackPanel>", "view.xaml"), 2, column,
                "Text content is not supported; use a property attribute.");
        }

        [Test]
        public void RejectsTextOnlySinglePropertyElement()
        {
            var result = XamlParser.Parse("<TextBlock " + Namespaces + ">\n  <TextBlock.Text>abc</TextBlock.Text>\n</TextBlock>", "view.xaml");
            Assert.That(result.Document, Is.Null);
            Assert.That(result.Errors.Select(e => (e.Line, e.Column, e.Message)), Is.EqualTo(new[] {
                (2, 19, "Text content is not supported; use a property attribute."),
                (2, 4, "Property 'Text' requires exactly one object.") }));
        }

        [TestCase("Border", "", "", "Child", "UIElement")]
        [TestCase("Border", "<Border.Child>\n", "</Border.Child>", "Child", "UIElement")]
        [TestCase("StackPanel", "", "", "Children", "UIElement")]
        [TestCase("StackPanel", "<StackPanel.Children>\n", "</StackPanel.Children>", "Children", "UIElement")]
        public void RejectsIncompatibleImplicitAndExplicitContent(string owner, string opening, string closing,
            string property, string target)
        {
            AssertError(XamlParser.Parse("<" + owner + " " + Namespaces + ">\n" + opening +
                "  <SolidColorBrush />\n" + closing + "</" + owner + ">", "view.xaml"),
                opening.Length == 0 ? 2 : 3, 4,
                $"Type 'SolidColorBrush' is not assignable to property '{property}' of type '{target}'.");
        }

        [Test]
        public void RejectsIncompatibleObjectProperty()
        {
            AssertError(XamlParser.Parse("<Border " + Namespaces + ">\n  <Border.Background>\n" +
                "    <Border />\n  </Border.Background>\n</Border>", "view.xaml"), 3, 6,
                "Type 'Border' is not assignable to property 'Background' of type 'Brush'.");
        }

        [Test]
        public void RejectsNonElementRootAndNameOnNonFrameworkElement()
        {
            AssertError(XamlParser.Parse("\n<SolidColorBrush " + Namespaces + " />", "view.xaml"),
                2, 2, "Root element 'SolidColorBrush' must be a UIElement.");
            AssertError(XamlParser.Parse("<Border " + Namespaces + ">\n  <Border.Background>\n" +
                "    <SolidColorBrush\n      x:Name=\"brush\" />\n  </Border.Background>\n</Border>", "view.xaml"),
                4, 7, "x:Name is only supported on FrameworkElement types, not 'SolidColorBrush'.");
        }

        [Test]
        public void RejectsAttributeAndPropertyElementDuplicate()
        {
            AssertError(XamlParser.Parse("<Border " + Namespaces + " Background=\"Red\">\n" +
                "  <Border.Background><SolidColorBrush /></Border.Background>\n</Border>", "view.xaml"),
                2, 4, "Property 'Background' is set more than once.");
        }

        [TestCase("Border", "Child")]
        [TestCase("StackPanel", "Children")]
        public void RejectsDuplicateExplicitContentAndMixingWithImplicitContent(string owner, string property)
        {
            string explicitContent = "<" + owner + "." + property + "><Border /></" + owner + "." + property + ">";
            foreach (var pair in new[] { (explicitContent, explicitContent), (explicitContent, "<Border />"),
                ("<Border />", explicitContent) })
                AssertError(XamlParser.Parse("<" + owner + " " + Namespaces + ">\n  " + pair.Item1 +
                    "\n  " + pair.Item2 + "\n</" + owner + ">", "view.xaml"),
                    3, 4, $"Property '{property}' is set more than once.");
        }

        [TestCase("#aBc", 255, 170, 187, 204)]
        [TestCase("#8aBc", 136, 170, 187, 204)]
        [TestCase(" #336699 ", 255, 51, 102, 153)]
        [TestCase("#8012AbEf", 128, 18, 171, 239)]
        [TestCase(" transparent ", 0, 255, 255, 255)]
        [TestCase("rEd", 255, 255, 0, 0)]
        [TestCase("LightGoldenrodYellow", 255, 250, 250, 210)]
        public void ConvertsColorUsingBrushColorSyntax(string text, byte a, byte r, byte g, byte b)
        {
            Assert.That(XamlValueConverter.TryConvert(text, typeof(Color), null, out object value, out string error), Is.True, error);
            Assert.That(value, Is.EqualTo(Color.FromArgb(a, r, g, b)));
            Assert.That(error, Is.Null);
        }

        [TestCase("")]
        [TestCase("#12")]
        [TestCase("#12345")]
        [TestCase("#1234567")]
        [TestCase("#123456789")]
        [TestCase("#zzzzzz")]
        [TestCase("# 12345")]
        [TestCase("RebeccaPurple")]
        [TestCase("ActiveBorder")]
        [TestCase("sc#1,1,1,1")]
        public void RejectsInvalidColors(string text)
        {
            Assert.That(XamlValueConverter.TryConvert(text, typeof(Color), null, out object value, out string error), Is.False);
            Assert.That(value, Is.Null);
            Assert.That(error, Is.EqualTo($"'{text}' is not a valid Color value."));
        }

        [Test]
        public void ReportsBrushColorConversionErrorAtAttribute()
        {
            AssertError(XamlParser.Parse("<Border " + Namespaces + ">\n  <Border.Background>\n" +
                "    <SolidColorBrush Color=\"bad\" />\n  </Border.Background>\n</Border>", "view.xaml"),
                3, 22, "Property 'Color': 'bad' is not a valid Color value.");
        }

        [Test]
        public void RejectsChildrenOnTextBlock()
        {
            AssertError(XamlParser.Parse("<TextBlock " + Namespaces + ">\n  <Border />\n</TextBlock>", "view.xaml"),
                2, 4, "Type 'TextBlock' has no content property.");
        }

        [Test]
        public void RejectsMultipleBorderChildrenAtSecondChild()
        {
            AssertError(XamlParser.Parse("<Border " + Namespaces + ">\n  <TextBlock />\n  <Border />\n</Border>", "view.xaml"),
                3, 4, "Content property 'Child' accepts only one child.");
        }

        [Test]
        public void RejectsTextContent()
        {
            string opening = "<TextBlock " + Namespaces + ">";
            // XmlReader locates the whole text node, including its leading newline.
            AssertError(XamlParser.Parse(opening + "\nhello</TextBlock>", "view.xaml"),
                1, opening.Length + 1, "Text content is not supported; use a property attribute.");
        }

        [Test]
        public void RejectsCDataContent()
        {
            AssertError(XamlParser.Parse("<TextBlock " + Namespaces + ">\n<![CDATA[hello]]></TextBlock>", "view.xaml"),
                2, 10, "Text content is not supported; use a property attribute.");
        }

        [TestCase("Name=\"first\"", "x:Name=\"second\"")]
        [TestCase("x:Name=\"first\"", "Name=\"second\"")]
        public void RejectsNameAndXNameDuplicate(string first, string second)
        {
            AssertError(XamlParser.Parse("<Border " + Namespaces + " " + first + "\n  " + second + " />", "view.xaml"),
                2, 3, "Property 'Name' is set more than once.");
        }

        [Test]
        public void RejectsDuplicatePropertyThroughNamespaceAlias()
        {
            AssertError(XamlParser.Parse("<Border " + Namespaces + " xmlns:ui=\"https://github.com/loonacia/xamlity\" " +
                "Width=\"1\"\n  ui:Width=\"2\" />", "view.xaml"), 2, 3, "Property 'Width' is set more than once.");
        }

        [Test]
        public void RejectsAttributeAndImplicitContentDuplicate()
        {
            var result = XamlParser.Parse("<Border " + Namespaces + "\n  Child=\"child\">\n  <TextBlock />\n</Border>", "view.xaml");
            Assert.That(result.Document, Is.Null);
            Assert.That(result.Errors.Count, Is.EqualTo(2));
            Assert.That(result.Errors[0].Message, Does.Contain("Cannot convert text"));
            AssertLocation(result.Errors[0].Line, result.Errors[0].Column, 2, 3);
            Assert.That(result.Errors[1].Message, Is.EqualTo("Property 'Child' is set more than once."));
            AssertLocation(result.Errors[1].Line, result.Errors[1].Column, 3, 4);
        }

        [Test]
        public void CollectsErrorsAcrossAttributesUnknownSubtreesAndSiblings()
        {
            var result = XamlParser.Parse("<StackPanel " + Namespaces + "\n  Width=\"bad\"\n  Unknown=\"1\">\n" +
                "  <Missing>\n    <TextBlock FontSize=\"large\" />\n  </Missing>\n" +
                "  <Border Padding=\"1,2,3\" />\n</StackPanel>", "view.xaml");
            Assert.That(result.Document, Is.Null);
            Assert.That(result.Errors.Select(e => (e.Line, e.Column)), Is.EqualTo(new[] {
                (2, 3), (3, 3), (4, 4), (5, 16), (7, 11) }));
            Assert.That(result.Errors.Select(e => e.Message), Is.EqualTo(new[] {
                "Property 'Width': 'bad' is not a valid Double value.",
                "Unknown or read-only property 'Unknown' on type 'StackPanel'.",
                "Unknown element 'Missing'.",
                "Property 'FontSize': 'large' is not a valid Double value.",
                "Property 'Padding': '1,2,3' is not a valid Thickness value." }));
        }

        [TestCase("<Border " + Namespaces + ">\n<TextBlock />\n</StackPanel>", 3, 3, "does not match")]
        [TestCase("<Border " + Namespaces + "\n  Width=\"1\" Width=\"2\" />", 2, 13, "duplicate attribute")]
        [TestCase("<Border " + Namespaces + " />\n<Border />", 2, 2, "multiple root")]
        [TestCase("<!DOCTYPE Border [<!ENTITY value 'text'>]>\n<Border " + Namespaces + " />", 1, 1, "DTD")]
        [TestCase("", 1, 1, "Root element is missing")]
        public void StopsOnOneXmlSyntaxError(string text, int line, int column, string message)
        {
            var result = XamlParser.Parse(text, "view.xaml");
            Assert.That(result.Document, Is.Null);
            Assert.That(result.Errors.Count, Is.EqualTo(1));
            AssertLocation(result.Errors[0].Line, result.Errors[0].Column, line, column);
            Assert.That(result.Errors[0].Message, Does.Contain(message));
        }

        [Test]
        public void FormatsErrorForImportDiagnostics()
        {
            var error = new XamlError("Assets/UI/view.xaml", 12, 8, "Unknown element 'Button'.");
            Assert.That(error.File, Is.EqualTo("Assets/UI/view.xaml"));
            Assert.That(error.ToString(), Is.EqualTo("Assets/UI/view.xaml(12,8): Unknown element 'Button'."));
        }

        [Test]
        public void RetainsSemanticErrorsBeforeFirstXmlSyntaxErrorAndStops()
        {
            var result = XamlParser.Parse("<Border " + Namespaces + "\n  Width=\"bad\">\n</TextBlock><Missing />", "view.xaml");
            Assert.That(result.Document, Is.Null);
            Assert.That(result.Errors.Count, Is.EqualTo(2));
            Assert.That(result.Errors[0].Message, Does.Contain("not a valid Double"));
            AssertLocation(result.Errors[0].Line, result.Errors[0].Column, 2, 3);
            Assert.That(result.Errors[1].Message, Does.Contain("does not match"));
            AssertLocation(result.Errors[1].Line, result.Errors[1].Column, 3, 3);
        }

        [Test]
        public void ParseCallsDoNotShareErrorsOrNodes()
        {
            Assert.That(XamlParser.Parse("<Missing " + Namespaces + " />", "bad.xaml").Errors, Is.Not.Empty);
            var first = XamlParser.Parse("<Border " + Namespaces + " />", "first.xaml");
            var second = XamlParser.Parse("<Border " + Namespaces + " />", "second.xaml");
            Assert.That(first.Errors, Is.Empty);
            Assert.That(second.Errors, Is.Empty);
            Assert.That(first.Document.Root, Is.Not.SameAs(second.Document.Root));
        }

        [TestCase("", typeof(string), "")]
        [TestCase("  text  ", typeof(string), "  text  ")]
        [TestCase("12.5", typeof(double), 12.5)]
        [TestCase(" -1.25e2 ", typeof(double), -125.0)]
        [TestCase("NaN", typeof(double), double.NaN)]
        [TestCase("Infinity", typeof(double), double.PositiveInfinity)]
        [TestCase(" -2147483648 ", typeof(int), int.MinValue)]
        [TestCase("2147483647", typeof(int), int.MaxValue)]
        [TestCase("TrUe", typeof(bool), true)]
        [TestCase(" false ", typeof(bool), false)]
        [TestCase("hOriZontAl", typeof(Orientation), Orientation.Horizontal)]
        [TestCase("1", typeof(Orientation), Orientation.Vertical)]
        [TestCase("17", typeof(Orientation), (Orientation)17)]
        [TestCase("Left, Right", typeof(HorizontalAlignment), HorizontalAlignment.Right)]
        [TestCase("AffectsMeasure, AffectsRender", typeof(FrameworkPropertyMetadataOptions),
            FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsRender)]
        [SetCulture("fr-FR")]
        public void ConvertsSupportedScalarValuesInvariantly(string text, Type type, object expected)
        {
            Assert.That(XamlValueConverter.TryConvert(text, type, null, out object value, out string error), Is.True, error);
            Assert.That(value, Is.EqualTo(expected));
            Assert.That(error, Is.Null);
        }

        [Test]
        public void AutoIsOnlyAcceptedForWidthAndHeight()
        {
            foreach (DependencyProperty dp in new[] { FrameworkElement.WidthProperty, FrameworkElement.HeightProperty })
            {
                Assert.That(XamlValueConverter.TryConvert(" aUtO ", typeof(double), dp, out object value, out string error), Is.True, error);
                Assert.That(value, Is.EqualTo(double.NaN));
            }
            foreach (DependencyProperty dp in new[] { FrameworkElement.MinWidthProperty, FrameworkElement.MaxHeightProperty,
                TextBlock.FontSizeProperty, null })
                Assert.That(XamlValueConverter.TryConvert("Auto", typeof(double), dp, out _, out _), Is.False);
        }

        [TestCase("2", 2, 2, 2, 2)]
        [TestCase("1,2", 1, 2, 1, 2)]
        [TestCase("1 2", 1, 2, 1, 2)]
        [TestCase("1,2,3,4", 1, 2, 3, 4)]
        [TestCase(" 1 , 2  3, 4 ", 1, 2, 3, 4)]
        [TestCase("1\t2\r\n3 4", 1, 2, 3, 4)]
        [TestCase("-1.5,2e1", -1.5, 20, -1.5, 20)]
        [SetCulture("fr-FR")]
        public void ConvertsThickness(string text, double left, double top, double right, double bottom)
        {
            Assert.That(XamlValueConverter.TryConvert(text, typeof(Thickness), null, out object value, out string error), Is.True, error);
            Assert.That(value, Is.EqualTo(new Thickness(left, top, right, bottom)));
        }

        [TestCase("#aBc", 255, 170, 187, 204)]
        [TestCase("#8aBc", 136, 170, 187, 204)]
        [TestCase("#12AbEf", 255, 18, 171, 239)]
        [TestCase("#8012AbEf", 128, 18, 171, 239)]
        [TestCase(" transparent ", 0, 255, 255, 255)]
        [TestCase("rEd", 255, 255, 0, 0)]
        [TestCase("LightGoldenrodYellow", 255, 250, 250, 210)]
        public void ConvertsBrush(string text, byte a, byte r, byte g, byte b)
        {
            Assert.That(XamlValueConverter.TryConvert(text, typeof(Brush), null, out object value, out string error), Is.True, error);
            Assert.That(value, Is.TypeOf<SolidColorBrush>());
            Assert.That(((SolidColorBrush)value).Color, Is.EqualTo(Color.FromArgb(a, r, g, b)));
        }

        [Test]
        public void ConvertsAll141WpfNamedColorsWithoutSystemColors()
        {
            // System.Drawing shares WPF's named colors, with the later RebeccaPurple addition excluded.
            var colors = Enum.GetValues<System.Drawing.KnownColor>()
                .Select(System.Drawing.Color.FromKnownColor)
                .Where(c => !c.IsSystemColor && c.Name != "RebeccaPurple").ToArray();
            Assert.That(colors.Length, Is.EqualTo(141));
            foreach (var expected in colors)
            {
                Assert.That(XamlValueConverter.TryConvert(expected.Name.ToLowerInvariant(), typeof(Brush), null,
                    out object value, out string error), Is.True, error);
                Assert.That(((SolidColorBrush)value).Color,
                    Is.EqualTo(Color.FromArgb(expected.A, expected.R, expected.G, expected.B)), expected.Name);
            }
        }

        [TestCase("", typeof(double))]
        [TestCase("1,5", typeof(double))]
        [TestCase("12px", typeof(double))]
        [TestCase("2cm", typeof(double))]
        [TestCase("1,000", typeof(double))]
        [TestCase("2147483648", typeof(int))]
        [TestCase("1.0", typeof(int))]
        [TestCase("1", typeof(bool))]
        [TestCase("yes", typeof(bool))]
        [TestCase("diagonal", typeof(Orientation))]
        [TestCase("", typeof(Orientation))]
        [TestCase("2147483648", typeof(Orientation))]
        [TestCase("", typeof(Thickness))]
        [TestCase("1,2,3", typeof(Thickness))]
        [TestCase("1,2,3,4,5", typeof(Thickness))]
        [TestCase("1,,2", typeof(Thickness))]
        [TestCase("1,", typeof(Thickness))]
        [TestCase(",1", typeof(Thickness))]
        [TestCase("one", typeof(Thickness))]
        [TestCase("Auto", typeof(Thickness))]
        [TestCase("", typeof(Brush))]
        [TestCase("#12", typeof(Brush))]
        [TestCase("#12345", typeof(Brush))]
        [TestCase("#1234567", typeof(Brush))]
        [TestCase("#123456789", typeof(Brush))]
        [TestCase("#zzzzzz", typeof(Brush))]
        [TestCase("# 12345", typeof(Brush))]
        [TestCase("RebeccaPurple", typeof(Brush))]
        [TestCase("ActiveBorder", typeof(Brush))]
        [TestCase("sc#1,1,1,1", typeof(Brush))]
        [TestCase("TextBlock", typeof(UIElement))]
        [TestCase("1", typeof(Size))]
        [TestCase("{Binding Name}", typeof(string))]
        [TestCase(" {StaticResource Color}", typeof(Brush))]
        public void RejectsInvalidAndUnsupportedConversions(string text, Type type)
        {
            Assert.That(XamlValueConverter.TryConvert(text, type, null, out object value, out string error), Is.False);
            Assert.That(value, Is.Null);
            Assert.That(error, Is.Not.Null.And.Not.Empty);
        }

        [Test]
        public void ResolvesDependencyPropertiesBeforeClrAndRunsBaseInitializers()
        {
            Type propertyType = XamlTypeResolver.ResolveProperty(typeof(DerivedProperties), "Value", out DependencyProperty dp);
            Assert.That(propertyType, Is.EqualTo(typeof(double)));
            Assert.That(dp, Is.SameAs(ClrProperties.ValueProperty));
            Assert.That(XamlTypeResolver.ResolveProperty(typeof(ClrProperties), "Text", out dp), Is.EqualTo(typeof(string)));
            Assert.That(dp, Is.Null);
            foreach (string name in new[] { "PrivateSetter", "ReadOnly", "StaticProperty", "Item", "Missing" })
                Assert.That(XamlTypeResolver.ResolveProperty(typeof(ClrProperties), name, out _), Is.Null, name);
        }

        public class ClrProperties : DependencyObject
        {
            public static readonly DependencyProperty ValueProperty = DependencyProperty.Register(
                "Value", typeof(double), typeof(ClrProperties));
            public string Value { get; set; }
            public string Text { get; set; }
            public string PrivateSetter { get; private set; }
            public string ReadOnly => "value";
            public static string StaticProperty { get; set; }
            public string this[int index] { get => "value"; set { } }
        }

        public class DerivedProperties : ClrProperties { }

        private static void AssertError(XamlParseResult result, int line, int column, string message)
        {
            Assert.That(result.Document, Is.Null);
            Assert.That(result.Errors.Count, Is.EqualTo(1), string.Join("\n", result.Errors));
            Assert.That(result.Errors[0].File, Is.EqualTo("view.xaml"));
            Assert.That(result.Errors[0].Message, Is.EqualTo(message));
            AssertLocation(result.Errors[0].Line, result.Errors[0].Column, line, column);
        }

        private static void AssertLocation(int line, int column, int expectedLine, int expectedColumn)
        {
            Assert.That(line, Is.EqualTo(expectedLine));
            Assert.That(column, Is.EqualTo(expectedColumn));
        }
    }
}
