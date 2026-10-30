using System;
using System.Linq;
using NUnit.Framework;
using Xamlity.Controls;
using Xamlity.Layout;
using Xamlity.Markup;
using Xamlity.Rendering;
using Xamlity.Tree;

namespace Xamlity.Core.Tests.Markup
{
    public class StyleMarkupTests
    {
        private const string Namespaces = "xmlns=\"https://github.com/loonacia/xamlity\" " +
            "xmlns:x=\"http://schemas.microsoft.com/winfx/2006/xaml\" " +
            "xmlns:local=\"clr-namespace:Xamlity.Core.Tests.Markup;assembly=Xamlity.Core.Tests\"";
        private const string BindingError = "A 'Binding' can only be set on a dependency property of a FrameworkElement.";

        [Test]
        public void TextSettersApplyAfterIrRoundTripRegardlessOfAttributeOrder()
        {
            var border = Build<Border>(Wrap("<Style TargetType=\"Border\">" +
                "<Setter Property=\"Background\" Value=\"Red\"/>" +
                "<Setter Value=\"1,2,3,4\" Property=\"Padding\"/>" +
                "<Setter Property=\"Width\" Value=\"Auto\"/>" +
                "<Setter Property=\"Height\" Value=\"Auto\"/>" +
                "<Setter Property=\"HorizontalAlignment\" Value=\"Right\"/>" +
                "<Setter Property=\"Canvas.Left\" Value=\"12\"/></Style>"));
            Assert.That(((SolidColorBrush)border.Background).Color, Is.EqualTo(Color.FromArgb(255, 255, 0, 0)));
            Assert.That(border.Padding, Is.EqualTo(new Thickness(1, 2, 3, 4)));
            Assert.That(double.IsNaN(border.Width), Is.True);
            Assert.That(double.IsNaN(border.Height), Is.True);
            Assert.That(border.HorizontalAlignment, Is.EqualTo(HorizontalAlignment.Right));
            Assert.That(Canvas.GetLeft(border), Is.EqualTo(12));
            Assert.That(border.Style.IsSealed, Is.True);
        }

        [Test]
        public void QualifiedNormalPropertyIsInheritedByChildren()
        {
            var panel = Build<StackPanel>("<StackPanel " + Namespaces + "><StackPanel.Style>" +
                "<Style TargetType=\"StackPanel\"><Setter Property=\"TextBlock.FontSize\" Value=\"28\"/>" +
                "</Style></StackPanel.Style><TextBlock/></StackPanel>");
            Assert.That(((TextBlock)panel.Children[0]).FontSize, Is.EqualTo(28));
        }

        [Test]
        public void ObjectSetterValueAndExplicitSettersCollectionWork()
        {
            var border = Build<Border>(Wrap("<Style TargetType=\"Border\"><Style.Setters>" +
                "<Setter Property=\"Background\"><Setter.Value><SolidColorBrush Color=\"Blue\"/>" +
                "</Setter.Value></Setter></Style.Setters></Style>"));
            Assert.That(((SolidColorBrush)border.Background).Color, Is.EqualTo(Color.FromArgb(255, 0, 0, 255)));
        }

        [TestCase(true)]
        [TestCase(false)]
        public void BasedOnAndLocalValuePrecedenceSurviveRoundTrip(bool localFirst)
        {
            string style = "<Border.Style><Style TargetType=\"Border\"><Style.BasedOn>" +
                "<Style TargetType=\"FrameworkElement\"><Setter Property=\"Width\" Value=\"25\"/>" +
                "<Setter Property=\"Height\" Value=\"30\"/></Style></Style.BasedOn>" +
                "<Setter Property=\"Height\" Value=\"40\"/></Style></Border.Style>";
            string xaml = localFirst ? "<Border " + Namespaces + " Width=\"70\">" + style + "</Border>" :
                "<Border " + Namespaces + ">" + style + "</Border>";
            var border = Build<Border>(xaml);
            if (!localFirst)
                border.Width = 70;
            Assert.That(border.Width, Is.EqualTo(70));
            Assert.That(border.Height, Is.EqualTo(40));
            Assert.That(border.Style.BasedOn.IsSealed, Is.True);
            border.ClearValue(FrameworkElement.WidthProperty);
            Assert.That(border.Width, Is.EqualTo(25));
        }

        [TestCase("Border")]
        [TestCase("{x:Type Border}")]
        [TestCase("{x:Type TypeName=Border}")]
        public void TargetTypeIsNormalizedInIr(string value)
        {
            XamlDocument document = RoundTrip(Wrap("<Style TargetType=\"" + value + "\"/>"));
            XamlObjectNode style = document.Root.Properties.Single().Objects.Single();
            Assert.That(style.Properties.Single().TextValue, Is.EqualTo("Xamlity.Controls.Border, Xamlity.Core"));
            Assert.That(((Border)XamlBuilder.Build(document)).Style.TargetType, Is.EqualTo(typeof(Border)));
        }

        [TestCase("local:NoDefaultConstructor")]
        [TestCase("{x:Type local:NoDefaultConstructor}")]
        public void TypeValuedClrPropertiesDoNotRequireAConstructor(string value)
        {
            XamlDocument document = RoundTrip("<local:TypePropertyBorder " + Namespaces +
                " ObjectType=\"" + value + "\"/>");
            Assert.That(document.Root.Properties.Single().TextValue,
                Is.EqualTo("Xamlity.Core.Tests.Markup.NoDefaultConstructor, Xamlity.Core.Tests"));
            Assert.That(((TypePropertyBorder)XamlBuilder.Build(document)).ObjectType,
                Is.EqualTo(typeof(NoDefaultConstructor)));
        }

        [Test]
        public void QualifiedSetterAttributesUseTheSamePropertyResolution()
        {
            var border = Build<Border>(Wrap("<Style TargetType=\"Border\">" +
                "<Setter Setter.Value=\"18\" Setter.Property=\"Width\"/></Style>"));
            Assert.That(border.Width, Is.EqualTo(18));
        }

        [TestCase("local:MyBorder")]
        [TestCase("{x:Type local:MyBorder}")]
        [TestCase("{x:Type TypeName=local:MyBorder}")]
        public void UserTargetTypeUsesClrNamespaceMapping(string value)
        {
            var border = Build<MyBorder>("<local:MyBorder " + Namespaces + "><Border.Style>" +
                "<Style TargetType=\"" + value + "\"><Setter Property=\"Width\" Value=\"42\"/>" +
                "</Style></Border.Style></local:MyBorder>");
            Assert.That(border.Style.TargetType, Is.EqualTo(typeof(MyBorder)));
            Assert.That(border.Width, Is.EqualTo(42));
        }

        [Test]
        public void AbstractTargetTypeDoesNotRequireAConstructor()
        {
            var border = Build<ConcreteBorder>("<local:ConcreteBorder " + Namespaces + "><Border.Style>" +
                "<Style TargetType=\"{x:Type local:AbstractBorder}\"/>" +
                "</Border.Style></local:ConcreteBorder>");
            Assert.That(border.Style.TargetType, Is.EqualTo(typeof(AbstractBorder)));
        }

        [Test]
        public void ImageSetterTracksAndResolvesItsAssetPath()
        {
            string xaml = "<Image " + Namespaces + "><Image.Style><Style TargetType=\"Image\">\n" +
                "  <Setter Value=\"Assets/picture.png\" Property=\"Source\"/>" +
                "</Style></Image.Style></Image>";
            XamlParseResult result = XamlParser.Parse(xaml, "style.xaml");
            Assert.That(result.Errors, Is.Empty);
            Assert.That(result.AssetReferences.Count, Is.EqualTo(1));
            Assert.That(result.AssetReferences[0].Value, Is.EqualTo("Assets/picture.png"));
            Assert.That((result.AssetReferences[0].Line, result.AssetReferences[0].Column), Is.EqualTo((2, 11)));
            var resolver = new ImageResolver();
            var image = (Image)XamlBuilder.Build(RoundTrip(xaml), resolver);
            Assert.That(resolver.Path, Is.EqualTo("Assets/picture.png"));
            Assert.That(image.Source, Is.SameAs(resolver.Source));
        }

        [TestCase("<Style/>", "Style requires a TargetType.", "Style")]
        [TestCase("<Style TargetType=\"Missing\"/>", "Unknown element 'Missing'.", "TargetType")]
        [TestCase("<Style TargetType=\"{x:Type Missing}\"/>", "Unknown element 'Missing'.", "TargetType")]
        [TestCase("<Style TargetType=\"TextBlock\"/>", "'TextBlock' TargetType does not match type of element 'Border'.", "Style")]
        [TestCase("<Style TargetType=\"local:Missing\"/>", "Unknown type 'Missing' in namespace 'Xamlity.Core.Tests.Markup' of assembly 'Xamlity.Core.Tests'.", "TargetType")]
        public void StyleErrorsHaveSourceLocations(string style, string message, string token)
        {
            AssertError(Wrap(style), message, token);
        }

        [TestCase("<Setter Value=\"12\"/>", "Setter requires a Property.", "Setter")]
        [TestCase("<Setter Property=\"Width\"/>", "Setter requires a Value.", "Setter")]
        [TestCase("<Setter Property=\"Width\" Value=\"bad\"/>", "Property 'Value': 'bad' is not a valid Double value.", "Value")]
        [TestCase("<Setter Property=\"local:MyBorder.Width\" Value=\"12\"/>", "Setter.Property does not support prefixed owner types.", "Property=")]
        [TestCase("<Setter Property=\"Style\" Value=\"bad\"/>", "Style object is not allowed to affect the Style property of the object to which it applies.", "Property=")]
        [TestCase("<Setter Property=\"Background\"><Setter.Value><Border/></Setter.Value></Setter>", "Type 'Border' is not assignable to property 'Value' of type 'Brush'.", "Border/")]
        [TestCase("<Setter Property=\"Background\" Value=\"Red\"><Setter.Value><SolidColorBrush/></Setter.Value></Setter>", "Property 'Value' is set more than once.", "Setter.Value>")]
        [TestCase("<Setter Property=\"Background\"><Setter.Value/></Setter>", "Property 'Value' requires exactly one object.", "Setter.Value/")]
        [TestCase("<Setter Property=\"Background\"><Setter.Value><SolidColorBrush/><SolidColorBrush/></Setter.Value></Setter>", "Property 'Value' requires exactly one object.", "Setter.Value>")]
        [TestCase("<Setter Property=\"Width\" Value=\"{Binding Width}\"/>", BindingError, "Value=")]
        [TestCase("<Setter Property=\"Width\"><Setter.Value><Binding/></Setter.Value></Setter>", BindingError, "Binding/")]
        [TestCase("<Setter Property=\"Width\" Value=\"{x:Type Border}\"/>", "Markup extension 'x:Type' can only be used for Type values.", "Value=")]
        [TestCase("<Setter Property=\"{x:Type Border}\" Value=\"12\"/>", "Markup extension 'x:Type' can only be used for Type values.", "Property=")]
        public void SetterErrorsHaveSourceLocations(string setter, string message, string token)
        {
            AssertError(Wrap("<Style TargetType=\"Border\">\n    " + setter + "</Style>"), message, token);
        }

        [TestCase("Missing")]
        [TestCase("Child")]
        [TestCase("Missing.Width")]
        [TestCase("Border.Child")]
        public void MissingAndClrPropertiesAreRejected(string property)
        {
            AssertError(Wrap("<Style TargetType=\"Border\">\n    <Setter Property=\"" + property + "\" Value=\"12\"/></Style>"),
                $"Cannot resolve the Style Property '{property}'. Verify that the owning type is the Style's TargetType, or use Class.Property syntax to specify the Property.", "Property=");
        }

        [TestCase("{x:Type}")]
        [TestCase("{x:Type Border, TextBlock}")]
        [TestCase("{x:Type Border, TypeName=Border}")]
        [TestCase("{x:Type Other=Border}")]
        [TestCase("{x:Type TypeName=Border, TypeName=Border}")]
        [TestCase("{x:Type TypeName=}")]
        public void TypeExtensionRequiresExactlyOneTypeName(string value)
        {
            AssertError(Wrap("<Style TargetType=\"" + value + "\"/>"),
                "Markup extension 'x:Type' requires exactly one TypeName.", "TargetType=");
        }

        [Test]
        public void BasedOnTargetTypeMustBeABaseType()
        {
            AssertError(Wrap("<Style TargetType=\"Border\"><Style.BasedOn>" +
                "<Style TargetType=\"TextBlock\"/></Style.BasedOn></Style>"),
                "Can only base on a Style with target type that is base type 'Border'.", "Style TargetType");
        }

        [TestCase("Property", "Missing")]
        [TestCase("Value", "bad")]
        public void BuilderReportsSetterFailuresAtSetterLocation(string property, string value)
        {
            XamlDocument document = RoundTrip(Wrap("<Style TargetType=\"Border\">\n    " +
                "<Setter Value=\"12\" Property=\"Width\"/></Style>"));
            XamlObjectNode setter = document.Root.Properties.Single().Objects.Single().Properties
                .Single(p => p.PropertyName == "Setters").Objects.Single();
            setter.Properties.Single(p => p.PropertyName == property).TextValue = value;
            XamlBuildException exception = Assert.Throws<XamlBuildException>(() => XamlBuilder.Build(document));
            Assert.That((exception.Line, exception.Column), Is.EqualTo((setter.Line, setter.Column)));
            Assert.That(exception.Message, Does.Contain("Cannot build Setter:"));
        }

        [Test]
        public void BuilderReportsObjectValueFailureAtSetterLocation()
        {
            XamlDocument document = RoundTrip(Wrap("<Style TargetType=\"Border\">\n    " +
                "<Setter Property=\"Background\"><Setter.Value><SolidColorBrush/>" +
                "</Setter.Value></Setter></Style>"));
            XamlObjectNode setter = document.Root.Properties.Single().Objects.Single().Properties
                .Single(p => p.PropertyName == "Setters").Objects.Single();
            setter.Properties.Single(p => p.PropertyName == "Value").Objects.Single().TypeName =
                typeof(Border).AssemblyQualifiedName;
            XamlBuildException exception = Assert.Throws<XamlBuildException>(() => XamlBuilder.Build(document));
            Assert.That((exception.Line, exception.Column), Is.EqualTo((setter.Line, setter.Column)));
            Assert.That(exception.Message, Does.Contain("is not a valid value for the 'Background' property on a Setter."));
        }

        private static string Wrap(string style) => "<Border " + Namespaces + ">\n  <Border.Style>\n    " +
            style + "\n  </Border.Style>\n</Border>";

        private static XamlDocument RoundTrip(string xaml)
        {
            XamlParseResult result = XamlParser.Parse(xaml, "style.xaml");
            Assert.That(result.Errors, Is.Empty, string.Join("\n", result.Errors));
            return XamlIrSerializer.Deserialize(XamlIrSerializer.Serialize(result.Document));
        }

        private static T Build<T>(string xaml) => (T)XamlBuilder.Build(RoundTrip(xaml));

        private static void AssertError(string xaml, string message, string token)
        {
            XamlParseResult result = XamlParser.Parse(xaml, "style.xaml");
            Assert.That(result.Document, Is.Null);
            Assert.That(result.Errors.Count, Is.EqualTo(1), string.Join("\n", result.Errors));
            XamlError error = result.Errors.Single();
            Assert.That(error.Message, Is.EqualTo(message));
            Assert.That(error.File, Is.EqualTo("style.xaml"));
            int index = xaml.IndexOf(token, xaml.IndexOf("\n    ", StringComparison.Ordinal) + 5, StringComparison.Ordinal);
            string preceding = xaml.Substring(0, index);
            Assert.That((error.Line, error.Column), Is.EqualTo((preceding.Count(c => c == '\n') + 1,
                index - preceding.LastIndexOf('\n'))));
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
            public ImageSource ResolveImage(string path) { Path = path; return Source; }
        }
    }

    public class TypePropertyBorder : Border
    {
        public Type ObjectType { get; set; }
    }
}
