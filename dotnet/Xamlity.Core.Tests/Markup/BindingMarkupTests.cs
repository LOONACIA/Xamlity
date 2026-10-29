using System;
using System.ComponentModel;
using System.Linq;
using NUnit.Framework;
using Xamlity.Controls;
using Xamlity.Data;
using Xamlity.Layout;
using Xamlity.Markup;
using Xamlity.Rendering;
using Xamlity.Tree;

namespace Xamlity.Core.Tests.Markup
{
    public class BindingMarkupTests
    {
        private const string Namespaces = "xmlns=\"https://github.com/loonacia/xamlity\" " +
            "xmlns:x=\"http://schemas.microsoft.com/winfx/2006/xaml\"";
        private const string TargetError =
            "A 'Binding' can only be set on a dependency property of a FrameworkElement.";

        [TestCase("{Binding}", new string[0], new string[0])]
        [TestCase("{Binding Name}", new[] { "Path" }, new[] { "Name" })]
        [TestCase("{Binding Path=Player.Hp, Mode=TwoWay}", new[] { "Path", "Mode" }, new[] { "Player.Hp", "TwoWay" })]
        [TestCase("{Binding Path='A', ConverterParameter='x, y'}", new[] { "Path", "ConverterParameter" }, new[] { "A", "x, y" })]
        [TestCase("{Binding Path= A , Mode= OneWay }", new[] { "Path", "Mode" }, new[] { "A", "OneWay" })]
        [TestCase("{Binding ConverterParameter='x=y}z'}", new[] { "ConverterParameter" }, new[] { "x=y}z" })]
        [TestCase("{Binding ConverterParameter=\"x=y,z}\"}", new[] { "ConverterParameter" }, new[] { "x=y,z}" })]
        [TestCase("{Binding ConverterParameter=x\\,y\\=z\\}q\\\\r}", new[] { "ConverterParameter" }, new[] { "x,y=z}q\\r" })]
        [TestCase("{Binding ConverterParameter='it\\'s'}", new[] { "ConverterParameter" }, new[] { "it's" })]
        public void ExtensionsBecomeBindingObjectsWithAttributeLocations(string extension, string[] names, string[] values)
        {
            XamlDocument document = RoundTrip(Parse(Attribute("TextBlock", "Text", extension)));
            XamlPropertyNode property = document.Root.Properties.Single();
            Assert.That(property.PropertyName, Is.EqualTo("Text"));
            Assert.That(property.TextValue, Is.Null);
            Assert.That((property.Line, property.Column), Is.EqualTo((2, 3)));
            XamlObjectNode binding = property.Objects.Single();
            Assert.That(Type.GetType(binding.TypeName), Is.EqualTo(typeof(Binding)));
            Assert.That((binding.Line, binding.Column), Is.EqualTo((2, 3)));
            Assert.That(binding.Properties.Select(p => p.PropertyName), Is.EqualTo(names));
            Assert.That(binding.Properties.Select(p => p.TextValue), Is.EqualTo(values));
            Assert.That(binding.Properties.Select(p => (p.Line, p.Column)), Is.All.EqualTo((2, 3)));
            Assert.That(binding.Properties.All(p => p.Objects.Count == 0), Is.True);
        }

        [Test]
        public void ObjectElementsPreserveTheirOwnLocationsAndProperties()
        {
            XamlDocument document = RoundTrip(Parse("<TextBlock " + Namespaces + ">\n" +
                "  <TextBlock.Text>\n    <Binding Path=\"Name\" Mode=\"TwoWay\"/>\n" +
                "  </TextBlock.Text>\n</TextBlock>"));
            XamlPropertyNode property = document.Root.Properties.Single();
            XamlObjectNode binding = property.Objects.Single();
            Assert.That(property.TextValue, Is.Null);
            Assert.That((property.Line, property.Column), Is.EqualTo((2, 4)));
            Assert.That(Type.GetType(binding.TypeName), Is.EqualTo(typeof(Binding)));
            Assert.That((binding.Line, binding.Column), Is.EqualTo((3, 6)));
            Assert.That(binding.Properties.Select(p => (p.Line, p.Column)), Is.EqualTo(new[] { (3, 14), (3, 26) }));
            Assert.That(binding.Properties.Select(p => p.TextValue), Is.EqualTo(new[] { "Name", "TwoWay" }));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void OneWayUpdatesAfterRoundTripAndDataContextAssignment(bool objectElement)
        {
            string xaml = objectElement
                ? "<TextBlock " + Namespaces + "><TextBlock.Text><Binding Path=\"Name\"/></TextBlock.Text></TextBlock>"
                : Attribute("TextBlock", "Text", "{Binding Name}");
            var text = Build<TextBlock>(xaml);
            var model = new Model { Name = "first" };
            text.DataContext = model;
            Assert.That(text.Text, Is.EqualTo("first"));
            model.Name = "second";
            Assert.That(text.Text, Is.EqualTo("second"));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void TwoWayWritesTargetChangesToTheViewModel(bool objectElement)
        {
            string xaml = objectElement
                ? "<TextBlock " + Namespaces + "><TextBlock.Text><Binding Path=\"Name\" Mode=\"TwoWay\"/></TextBlock.Text></TextBlock>"
                : Attribute("TextBlock", "Text", "{Binding Path=Name, Mode=TwoWay}");
            var text = Build<TextBlock>(xaml);
            var model = new Model { Name = "before" };
            text.DataContext = model;
            text.SetValue(TextBlock.TextProperty, "after");
            Assert.That(model.Name, Is.EqualTo("after"));
            model.Name = "again";
            Assert.That(text.Text, Is.EqualTo("again"));
        }

        [Test]
        public void AttachedBindingPositionsTheChildAndTracksChanges()
        {
            var canvas = Build<Canvas>("<Canvas " + Namespaces + "><Border Canvas.Left=\"{Binding X}\" " +
                "Width=\"10\" Height=\"20\"/></Canvas>");
            var model = new Model { X = 12 };
            canvas.DataContext = model;
            var root = new UIRoot(canvas);
            root.UpdateLayout(new Size(100, 100));
            var child = (Border)canvas.Children.Single();
            Assert.That(child.VisualOffset, Is.EqualTo(new Point(12, 0)));
            model.X = 35;
            root.UpdateLayout(new Size(100, 100));
            Assert.That(child.VisualOffset, Is.EqualTo(new Point(35, 0)));
        }

        [Test]
        public void DataContextBindingUsesParentContextAndUpdatesChildBindings()
        {
            var panel = Build<StackPanel>("<StackPanel " + Namespaces + ">" +
                "<Border DataContext=\"{Binding Player}\"><TextBlock Text=\"{Binding Name}\"/></Border></StackPanel>");
            var model = new Model { Player = new Model { Name = "one" } };
            panel.DataContext = model;
            var border = (Border)panel.Children.Single();
            var text = (TextBlock)border.Child;
            Assert.That(border.DataContext, Is.SameAs(model.Player));
            Assert.That(text.Text, Is.EqualTo("one"));
            model.Player.Name = "two";
            Assert.That(text.Text, Is.EqualTo("two"));
            model.Player = new Model { Name = "three" };
            Assert.That(text.Text, Is.EqualTo("three"));
        }

        [Test]
        public void NestedPathTracksReplacementAfterRoundTrip()
        {
            var text = Build<TextBlock>(Attribute("TextBlock", "Text", "{Binding Player.Name}"));
            var model = new Model { Player = new Model { Name = "one" } };
            text.DataContext = model;
            Assert.That(text.Text, Is.EqualTo("one"));
            model.Player = new Model { Name = "two" };
            Assert.That(text.Text, Is.EqualTo("two"));
        }

        [TestCase("{Binding}")]
        [TestCase("{Binding .}")]
        [TestCase("{Binding Path=}")]
        [TestCase("{Binding ''}")]
        public void EmptyPathsBindTheContextItself(string extension)
        {
            var text = Build<TextBlock>(Attribute("TextBlock", "Text", extension));
            text.DataContext = "context";
            Assert.That(text.Text, Is.EqualTo("context"));
        }

        [Test]
        public void OrdinaryNameCanBeBound()
        {
            var border = Build<Border>(Attribute("Border", "Name", "{Binding Name}"));
            border.DataContext = new Model { Name = "boundName" };
            Assert.That(border.Name, Is.EqualTo("boundName"));
        }

        [Test]
        public void NamespaceAliasResolvesBindingInTheDefaultXamlityNamespace()
        {
            var text = Build<TextBlock>("<ui:TextBlock xmlns:ui=\"https://github.com/loonacia/xamlity\" " +
                "Text=\"{ui:Binding Name}\"/>");
            text.DataContext = new Model { Name = "aliased" };
            Assert.That(text.Text, Is.EqualTo("aliased"));
        }

        [TestCase("{}{0} HP", "{0} HP")]
        [TestCase("{}", "")]
        [TestCase("{}{}", "{}")]
        [TestCase("{}{Binding Name}", "{Binding Name}")]
        public void EscapedTextRemainsRawInIrAndBecomesLiteralOnBuild(string input, string expected)
        {
            XamlDocument document = RoundTrip(Parse(Attribute("TextBlock", "Text", input)));
            Assert.That(document.Root.Properties.Single().TextValue, Is.EqualTo(input));
            Assert.That(((TextBlock)XamlBuilder.Build(document)).Text, Is.EqualTo(expected));
        }

        [Test]
        public void ObjectTypedTextIsAString()
        {
            var text = Build<TextBlock>("<TextBlock " + Namespaces + " DataContext=\"abc\" Text=\"{Binding}\"/>");
            Assert.That(text.DataContext, Is.TypeOf<string>().And.EqualTo("abc"));
            Assert.That(text.Text, Is.EqualTo("abc"));
        }

        [TestCase("abc", "abc")]
        [TestCase("'x, y'", "x, y")]
        [TestCase("' x=y} '", " x=y} ")]
        [TestCase("'\\{literal}'", "{literal}")]
        [TestCase("\\{literal\\}", "{literal}")]
        public void ConverterParameterRetainsDecodedLiteralText(string input, string expected)
        {
            var text = Build<TextBlock>(Attribute("TextBlock", "Text", "{Binding ConverterParameter=" + input + "}"));
            Binding binding = BindingOperations.GetBindingExpression(text, TextBlock.TextProperty).ParentBinding;
            Assert.That(binding.ConverterParameter, Is.TypeOf<string>().And.EqualTo(expected));
        }

        [Test]
        public void BindingPropertiesCanContainOrdinaryObjectElements()
        {
            var text = Build<TextBlock>("<TextBlock " + Namespaces + "><TextBlock.Text><Binding>" +
                "<Binding.ConverterParameter><SolidColorBrush Color=\"Red\"/></Binding.ConverterParameter>" +
                "</Binding></TextBlock.Text></TextBlock>");
            Binding binding = BindingOperations.GetBindingExpression(text, TextBlock.TextProperty).ParentBinding;
            Assert.That(((SolidColorBrush)binding.ConverterParameter).Color, Is.EqualTo(Color.FromRgb(255, 0, 0)));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void ImageBindingIsNotAnAssetReferenceAndNeedsNoResolver(bool objectElement)
        {
            string xaml = objectElement
                ? "<Image " + Namespaces + "><Image.Source><Binding Path=\"Image\"/></Image.Source></Image>"
                : Attribute("Image", "Source", "{Binding Image}");
            XamlParseResult result = Parse(xaml);
            Assert.That(result.AssetReferences, Is.Empty);
            var image = (Image)XamlBuilder.Build(RoundTrip(result));
            var source = new TestImageSource();
            image.DataContext = new { Image = source };
            Assert.That(image.Source, Is.SameAs(source));
        }

        [Test]
        public void EscapedImagePathsUseTheSameLiteralForImportAndBuild()
        {
            XamlParseResult result = Parse(Attribute("Image", "Source", "{}{icon}.png"));
            Assert.That(result.Document.Root.Properties.Single().TextValue, Is.EqualTo("{}{icon}.png"));
            Assert.That(result.AssetReferences.Single().Value, Is.EqualTo("{icon}.png"));
            var resolver = new ImageResolver();
            var image = (Image)XamlBuilder.Build(RoundTrip(result), resolver);
            Assert.That(resolver.Path, Is.EqualTo("{icon}.png"));
            Assert.That(image.Source, Is.SameAs(resolver.Source));
        }

        [TestCase("{StaticResource c}", "Markup extension 'StaticResource' is not supported.")]
        [TestCase("{TemplateBinding Text}", "Markup extension 'TemplateBinding' is not supported.")]
        [TestCase("{x:Type TextBlock}", "Markup extension 'x:Type' is not supported.")]
        [TestCase("{x:Static Something}", "Markup extension 'x:Static' is not supported.")]
        [TestCase("{Unknown}", "Markup extension 'Unknown' is not supported.")]
        [TestCase("{x:Binding}", "Markup extension 'x:Binding' is not supported.")]
        [TestCase("{Binding Converter={StaticResource c}}", "Nested markup extensions are not supported.")]
        [TestCase("{Binding", "Invalid markup extension syntax: expected closing '}'.")]
        [TestCase("{Binding Path='A}", "Invalid markup extension syntax: unterminated quoted value.")]
        [TestCase("{ }", "Invalid markup extension syntax: expected a type name.")]
        [TestCase("{Binding =A}", "Invalid markup extension syntax: expected an argument name before '='.")]
        [TestCase("{Binding Path=A, B}", "Invalid markup extension syntax: positional arguments must precede named arguments.")]
        [TestCase("{Binding}text", "Invalid markup extension syntax: unexpected text after '}'.")]
        [TestCase("{Binding A,}", "Invalid markup extension syntax: expected an argument.")]
        [TestCase("{Binding Path='A'B}", "Invalid markup extension syntax: unexpected text after a quoted value.")]
        [TestCase("{Binding A, B}", "Markup extension 'Binding' accepts at most one positional argument (Path).")]
        [TestCase("{Binding A, Path=B}", "Property 'Path' is set more than once.")]
        [TestCase("{Binding Path=A, Path=B}", "Property 'Path' is set more than once.")]
        [TestCase("{Binding ElementName=label}", "Unknown or read-only property 'ElementName' on type 'Binding'.")]
        [TestCase("{Binding Mode=X}", "Property 'Mode': 'X' is not a valid BindingMode value.")]
        [TestCase("{Binding Mode=OneTime}", "Property 'Mode': 'OneTime' is not a valid BindingMode value.")]
        [TestCase("{Binding Mode=99}", "Property 'Mode': '99' is not a valid BindingMode value.")]
        [TestCase("{Binding A[0]}", "Property 'Path': Property path 'A[0]' is not supported; use dot-separated property names.")]
        [TestCase("{Binding Converter=abc}", "Property 'Converter': Cannot convert text to type 'Xamlity.Data.IValueConverter'.")]
        public void ExtensionErrorsHaveExactMessagesAndAttributeLocations(string value, string message)
        {
            AssertError(Attribute("TextBlock", "Text", value), message, 2, 3);
        }

        [Test]
        public void RejectsBindingOnClrProperties()
        {
            AssertError("<Border " + Namespaces + "><Border.Background><SolidColorBrush\n" +
                "  Color=\"{Binding}\"/></Border.Background></Border>", TargetError, 2, 3);
            AssertError("<Border " + Namespaces + "><Border.Background><SolidColorBrush><SolidColorBrush.Color>\n" +
                "  <Binding/></SolidColorBrush.Color></SolidColorBrush></Border.Background></Border>", TargetError, 2, 4);
        }

        [TestCase("<Border.Background><SolidColorBrush\n  Color=\"{Binding}\"/></Border.Background>", 2, 3)]
        [TestCase("<Border.Background><SolidColorBrush\n  Canvas.Left=\"{Binding}\"/></Border.Background>", 2, 3)]
        [TestCase("<Border.DataContext><Binding\n  Path=\"{Binding}\"/></Border.DataContext>", 2, 3)]
        [TestCase("<Border.Background><SolidColorBrush><SolidColorBrush.Color>\n  <Binding/>" +
            "</SolidColorBrush.Color></SolidColorBrush></Border.Background>", 2, 4)]
        [TestCase("<Border.DataContext><Binding><Binding.ConverterParameter>\n  <Binding/>" +
            "</Binding.ConverterParameter></Binding></Border.DataContext>", 2, 4)]
        public void RejectsBindingOnNonFrameworkElementProperties(string content, int line, int column)
        {
            AssertError("<Border " + Namespaces + ">" + content + "</Border>", TargetError, line, column);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void RejectsBindingOnRowDefinitionProperties(bool objectElement)
        {
            string row = objectElement
                ? "<RowDefinition><RowDefinition.Height>\n  <Binding/></RowDefinition.Height></RowDefinition>"
                : "<RowDefinition\n  Height=\"{Binding}\"/>";
            AssertError("<Grid " + Namespaces + "><Grid.RowDefinitions>" + row + "</Grid.RowDefinitions></Grid>",
                TargetError, 2, objectElement ? 4 : 3);
        }

        [TestCase("Border", "")]
        [TestCase("TextBlock", "")]
        [TestCase("StackPanel", "")]
        [TestCase("StackPanel", "StackPanel.Children")]
        [TestCase("Grid", "Grid.RowDefinitions")]
        public void RejectsBindingAsImplicitContentOrCollectionItem(string element, string property)
        {
            string open = property.Length == 0 ? "" : "<" + property + ">";
            string close = property.Length == 0 ? "" : "</" + property + ">";
            AssertError("<" + element + " " + Namespaces + ">" + open + "\n  <Binding/>" + close + "</" + element + ">",
                TargetError, 2, 4);
        }

        [TestCase("Path=\"A[0]\"", "Property 'Path': Property path 'A[0]' is not supported; use dot-separated property names.")]
        [TestCase("ElementName=\"label\"", "Unknown or read-only property 'ElementName' on type 'Binding'.")]
        [TestCase("Mode=\"X\"", "Property 'Mode': 'X' is not a valid BindingMode value.")]
        [TestCase("Mode=\"99\"", "Property 'Mode': '99' is not a valid BindingMode value.")]
        public void BindingObjectAttributesUseOrdinaryValidation(string attribute, string message)
        {
            AssertError("<TextBlock " + Namespaces + "><TextBlock.Text><Binding\n  " + attribute +
                "/></TextBlock.Text></TextBlock>", message, 2, 3);
        }

        [Test]
        public void ConverterPropertyElementUsesOrdinaryTypeValidation()
        {
            AssertError("<TextBlock " + Namespaces + "><TextBlock.Text><Binding><Binding.Converter>\n" +
                "  <SolidColorBrush/></Binding.Converter></Binding></TextBlock.Text></TextBlock>",
                "Type 'SolidColorBrush' is not assignable to property 'Converter' of type 'IValueConverter'.", 2, 4);
        }

        [Test]
        public void BindingObjectMustBeTheOnlyValueInASingleProperty()
        {
            AssertError("<TextBlock " + Namespaces + ">\n  <TextBlock.Text><Binding/><Binding/></TextBlock.Text></TextBlock>",
                "Property 'Text' requires exactly one object.", 2, 4);
        }

        [Test]
        public void SetBindingFailureIsWrappedAtThePropertyLocation()
        {
            XamlDocument document = RoundTrip(Parse(Attribute("TextBlock", "Text", "{Binding Mode=TwoWay}")));
            var exception = Assert.Throws<XamlBuildException>(() => XamlBuilder.Build(document));
            Assert.That((exception.Line, exception.Column), Is.EqualTo((2, 3)));
            Assert.That(exception.Message, Is.EqualTo("(2,3): Cannot set property 'Text': Two-way binding requires a property path."));
            Assert.That(exception.InnerException, Is.TypeOf<InvalidOperationException>());
        }

        [Test]
        public void BuilderRejectsBindingOnAFrameworkElementClrProperty()
        {
            XamlDocument document = RoundTrip(Parse(Attribute("TextBlock", "Text", "{Binding Name}")));
            document.Root.TypeName = typeof(ClrElement).AssemblyQualifiedName;
            document.Root.Properties.Single().PropertyName = "Label";
            var exception = Assert.Throws<XamlBuildException>(() => XamlBuilder.Build(document));
            Assert.That((exception.Line, exception.Column), Is.EqualTo((2, 3)));
            Assert.That(exception.Message, Is.EqualTo("(2,3): Cannot set property 'Label': " + TargetError));
        }

        public class ClrElement : FrameworkElement
        {
            public string Label { get; set; }
        }

        private static string Attribute(string element, string name, string value) =>
            "<" + element + " " + Namespaces + "\n  " + name + "=\"" + value.Replace("&", "&amp;").Replace("\"", "&quot;") + "\"/>";

        private static XamlParseResult Parse(string xaml)
        {
            XamlParseResult result = XamlParser.Parse(xaml, "binding.xaml");
            Assert.That(result.Errors, Is.Empty, string.Join("\n", result.Errors));
            return result;
        }

        private static XamlDocument RoundTrip(XamlParseResult result) =>
            XamlIrSerializer.Deserialize(XamlIrSerializer.Serialize(result.Document));

        private static T Build<T>(string xaml) => (T)XamlBuilder.Build(RoundTrip(Parse(xaml)));

        private static void AssertError(string xaml, string message, int line, int column)
        {
            XamlParseResult result = XamlParser.Parse(xaml, "binding.xaml");
            Assert.That(result.Document, Is.Null);
            Assert.That(result.Errors.Count, Is.EqualTo(1), string.Join("\n", result.Errors));
            Assert.That(result.Errors[0].Message, Is.EqualTo(message));
            Assert.That(result.Errors[0].File, Is.EqualTo("binding.xaml"));
            Assert.That((result.Errors[0].Line, result.Errors[0].Column), Is.EqualTo((line, column)));
        }

        private sealed class Model : INotifyPropertyChanged
        {
            private string name;
            private double x;
            private Model player;
            public string Name { get => name; set { name = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Name))); } }
            public double X { get => x; set { x = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(X))); } }
            public Model Player { get => player; set { player = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Player))); } }
            public event PropertyChangedEventHandler PropertyChanged;
        }

        private sealed class TestImageSource : ImageSource
        {
            public override double Width => 10;
            public override double Height => 20;
        }

        private sealed class ImageResolver : IXamlAssetResolver
        {
            internal string Path;
            internal readonly ImageSource Source = new TestImageSource();
            public ImageSource ResolveImage(string value) { Path = value; return Source; }
        }
    }
}
