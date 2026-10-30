using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Xamlity.Controls;
using Xamlity.Data;
using Xamlity.Layout;
using Xamlity.Markup;
using Xamlity.Rendering;
using Xamlity.Styling;

namespace Xamlity.Core.Tests.Markup
{
    public class ResourceMarkupTests
    {
        private const string Namespaces = "xmlns=\"https://github.com/loonacia/xamlity\" " +
            "xmlns:x=\"http://schemas.microsoft.com/winfx/2006/xaml\" " +
            "xmlns:t=\"clr-namespace:Xamlity.Core.Tests.Markup;assembly=Xamlity.Core.Tests\"";

        [TestCase("{StaticResource brush}")]
        [TestCase("{StaticResource ResourceKey=brush}")]
        public void ChildBackgroundUsesParentsResourceAfterIrRoundTrip(string extension)
        {
            var border = Build<Border>("<Border " + Namespaces + "><Border.Resources>" +
                "<SolidColorBrush x:Key=\"brush\" Color=\"Red\"/></Border.Resources>" +
                "<Border Background=\"" + extension + "\"/></Border>");
            Assert.That(((Border)border.Child).Background, Is.SameAs(border.Resources["brush"]));
            Assert.That(((SolidColorBrush)border.Resources["brush"]).Color, Is.EqualTo(Color.FromRgb(255, 0, 0)));
        }

        [Test]
        public void EarlierResourcesSupplySetterValuesStyleAndBasedOn()
        {
            var border = Build<Border>("<Border " + Namespaces + "><Border.Resources>" +
                "<SolidColorBrush x:Key=\"brush\" Color=\"Blue\"/>" +
                "<Style x:Key=\"Base\" TargetType=\"Border\"><Setter Property=\"Background\" Value=\"{StaticResource brush}\"/></Style>" +
                "<Style x:Key=\"Key\" TargetType=\"Border\" BasedOn=\"{StaticResource Base}\"><Setter Property=\"Width\" Value=\"42\"/></Style>" +
                "</Border.Resources><Border Style=\"{StaticResource Key}\"/></Border>");
            var child = (Border)border.Child;
            Assert.That(child.Style, Is.SameAs(border.Resources["Key"]));
            Assert.That(child.Style.BasedOn, Is.SameAs(border.Resources["Base"]));
            Assert.That(child.Background, Is.SameAs(border.Resources["brush"]));
            Assert.That(child.Width, Is.EqualTo(42));
        }

        [Test]
        public void ClosestResourcesWinAndOuterResourcesRemainVisible()
        {
            var outer = Build<Border>("<Border " + Namespaces + "><Border.Resources>" +
                "<SolidColorBrush x:Key=\"shared\" Color=\"Red\"/><SolidColorBrush x:Key=\"outer\"/>" +
                "</Border.Resources><Border><Border.Resources><SolidColorBrush x:Key=\"shared\" Color=\"Blue\"/>" +
                "</Border.Resources><Border Background=\"{StaticResource shared}\" BorderBrush=\"{StaticResource outer}\"/></Border></Border>");
            var inner = (Border)outer.Child;
            var child = (Border)inner.Child;
            Assert.That(child.Background, Is.SameAs(inner.Resources["shared"]));
            Assert.That(child.BorderBrush, Is.SameAs(outer.Resources["outer"]));
        }

        [Test]
        public void ExplicitDictionaryAssignmentAndMergedDictionariesUseLexicalScope()
        {
            var border = Build<Border>("<Border " + Namespaces + "><Border.Resources><ResourceDictionary>" +
                "<SolidColorBrush x:Key=\"outer\"/>" +
                "<ResourceDictionary.MergedDictionaries><ResourceDictionary><SolidColorBrush x:Key=\"brush\" Color=\"Red\"/></ResourceDictionary>" +
                "<ResourceDictionary><SolidColorBrush x:Key=\"brush\" Color=\"Blue\"/>" +
                "<Style x:Key=\"style\" TargetType=\"Border\"><Setter Property=\"BorderBrush\" Value=\"{StaticResource outer}\"/>" +
                "<Setter Property=\"Background\" Value=\"{StaticResource brush}\"/></Style></ResourceDictionary></ResourceDictionary.MergedDictionaries>" +
                "<Style x:Key=\"derived\" TargetType=\"Border\" BasedOn=\"{StaticResource style}\"/>" +
                "</ResourceDictionary></Border.Resources><Border Style=\"{StaticResource derived}\"/></Border>");
            Assert.That(border.Resources.MergedDictionaries.Count, Is.EqualTo(2));
            var child = (Border)border.Child;
            Assert.That(child.Background, Is.SameAs(border.Resources.MergedDictionaries[1]["brush"]));
            Assert.That(child.BorderBrush, Is.SameAs(border.Resources["outer"]));
        }

        [Test]
        public void RootDictionarySupportsImplicitStyleKeysAndKeyedDictionaryEntries()
        {
            var dictionary = Build<ResourceDictionary>("<ResourceDictionary " + Namespaces + ">" +
                "<Style TargetType=\"{x:Type Border}\"/><Style x:Key=\"explicit\" TargetType=\"Border\"/>" +
                "<ResourceDictionary x:Key=\"nested\"><SolidColorBrush x:Key=\"brush\"/></ResourceDictionary></ResourceDictionary>");
            Assert.That(((Style)dictionary[typeof(Border)]).TargetType, Is.EqualTo(typeof(Border)));
            Assert.That(dictionary["explicit"], Is.TypeOf<Style>());
            Assert.That(((ResourceDictionary)dictionary["nested"])["brush"], Is.TypeOf<SolidColorBrush>());
            Assert.That(dictionary.Count, Is.EqualTo(3));
        }

        [Test]
        public void EmptyResourcesAreAccepted()
        {
            Assert.That(Build<Border>("<Border " + Namespaces + "><Border.Resources /></Border>").Resources.Count, Is.Zero);
        }

        [Test]
        public void ResourcesBetweenImplicitChildrenAreBuiltInDocumentOrder()
        {
            var panel = Build<StackPanel>("<StackPanel " + Namespaces + "><Border/>" +
                "<StackPanel.Resources><SolidColorBrush x:Key=\"brush\"/></StackPanel.Resources>" +
                "<Border Background=\"{StaticResource brush}\"/></StackPanel>");
            Assert.That(panel.Children.Count, Is.EqualTo(2));
            Assert.That(((Border)panel.Children[1]).Background, Is.SameAs(panel.Resources["brush"]));
        }

        [Test]
        public void DictionaryEntriesAfterMergedDictionariesCanUseThem()
        {
            var dictionary = Build<ResourceDictionary>("<ResourceDictionary " + Namespaces + "><SolidColorBrush x:Key=\"first\"/>" +
                "<ResourceDictionary.MergedDictionaries><ResourceDictionary><SolidColorBrush x:Key=\"brush\"/>" +
                "</ResourceDictionary></ResourceDictionary.MergedDictionaries>" +
                "<Style TargetType=\"Border\"><Setter Property=\"Background\" Value=\"{StaticResource brush}\"/></Style></ResourceDictionary>");
            Assert.That(((Setter)((Style)dictionary[typeof(Border)]).Setters[0]).Value, Is.SameAs(dictionary["brush"]));
        }

        [Test]
        public void ExtensionIrContainsOnlyResourceKeyAndAttributeLocation()
        {
            var document = Parse("<Border " + Namespaces + "\n  Background=\"{StaticResource ResourceKey=brush}\"/>");
            XamlPropertyNode property = document.Root.Properties.Single();
            Assert.That(property.TextValue, Is.Null);
            XamlObjectNode extension = property.Objects.Single();
            Assert.That(Type.GetType(extension.TypeName).Name, Is.EqualTo("StaticResourceExtension"));
            XamlPropertyNode key = extension.Properties.Single();
            Assert.That(key.PropertyName, Is.EqualTo("ResourceKey"));
            Assert.That(key.TextValue, Is.EqualTo("brush"));
            Assert.That((extension.Line, extension.Column, key.Line, key.Column), Is.EqualTo((2, 3, 2, 3)));
        }

        [TestCase("<SolidColorBrush/>", "Resource dictionary entries require an x:Key.")]
        [TestCase("<SolidColorBrush x:Key=\"key\"/>", "Duplicate resource key 'key'.")]
        [TestCase("<Style TargetType=\"Border\"/>", "Duplicate resource key 'Border'.")]
        public void ImplicitResourceDictionaryUsesTheSameEntryValidation(string entry, string message)
        {
            AssertError("<Border " + Namespaces + "><Border.Resources><SolidColorBrush x:Key=\"key\"/>" +
                "<Style TargetType=\"Border\"/>\n  " + entry + "</Border.Resources></Border>", message, 2, 4);
        }

        [Test]
        public void KeyOnPropertyElementIsRejected()
        {
            AssertError("<Border " + Namespaces + "><Border.Resources\n  x:Key=\"key\"/></Border>",
                "x:Key is only valid on resource dictionary entries.", 2, 3);
        }

        [Test]
        public void NestedStaticResourceExtensionsRemainUnsupported()
        {
            AssertError("<Border " + Namespaces + "\n  Style=\"{StaticResource {x:Type Border}}\"/>",
                "Nested markup extensions are not supported.", 2, 3);
        }

        [Test]
        public void StaticResourceWorksForAttachedClrTypeAndImagePropertiesWithoutAssetReferences()
        {
            string xaml = "<Border " + Namespaces + "><Border.Resources><t:SeedResources/></Border.Resources><StackPanel>" +
                "<Border Canvas.Left=\"{StaticResource number}\"><Border.Background><SolidColorBrush Color=\"{StaticResource color}\"/></Border.Background></Border>" +
                "<Image Source=\"  {StaticResource Image}\"/>" +
                "<Border><Border.Style><Style TargetType=\"{StaticResource type}\"/></Border.Style></Border>" +
                "<TextBlock Text=\"{StaticResource c}\"/>" +
                "<TextBlock><TextBlock.Text><Binding ConverterParameter=\"{StaticResource c}\"/></TextBlock.Text></TextBlock>" +
                "</StackPanel></Border>";
            XamlDocument document = Parse(xaml);
            var border = (Border)XamlBuilder.Build(document);
            var children = ((StackPanel)border.Child).Children;
            Assert.That(Canvas.GetLeft(children[0]), Is.EqualTo(12.5));
            Assert.That(((SolidColorBrush)((Border)children[0]).Background).Color, Is.EqualTo(Color.FromRgb(1, 2, 3)));
            Assert.That(((Image)children[1]).Source, Is.SameAs(border.Resources["Image"]));
            Assert.That(((Border)children[2]).Style.TargetType, Is.EqualTo(typeof(Border)));
            Assert.That(((TextBlock)children[3]).Text, Is.EqualTo("text"));
            Assert.That(BindingOperations.GetBindingExpression((TextBlock)children[4], TextBlock.TextProperty)
                .ParentBinding.ConverterParameter, Is.EqualTo("text"));
        }

        [Test]
        public void ResourceSuppliedTargetTypeAndSetterPropertyAreResolvedAtBuildTime()
        {
            var outer = Build<Border>("<Border " + Namespaces + "><Border.Resources><t:SeedResources/></Border.Resources>" +
                "<Border><Border.Resources><Style TargetType=\"{StaticResource type}\"><Style.Setters>" +
                "<Setter Property=\"Width\" Value=\"25\"/><Setter Property=\"{StaticResource property}\" Value=\"30\"/>" +
                "</Style.Setters></Style></Border.Resources></Border></Border>");
            var style = (Style)((Border)outer.Child).Resources[typeof(Border)];
            var target = new Border { Style = style };
            Assert.That(target.Width, Is.EqualTo(25));
            Assert.That(target.Height, Is.EqualTo(30));
        }

        [Test]
        public void DuplicateImplicitKeysFromResourceSuppliedTypesFailAtTheLaterEntry()
        {
            var document = Parse("<Border " + Namespaces + "><Border.Resources><t:SeedResources/></Border.Resources>" +
                "<Border><Border.Resources><Style TargetType=\"Border\"/>\n  <Style TargetType=\"{StaticResource type}\"/>" +
                "</Border.Resources></Border></Border>");
            var error = Assert.Throws<XamlBuildException>(() => XamlBuilder.Build(document));
            Assert.That(error.Message, Is.EqualTo("(2,4): Duplicate resource key 'Border'."));
        }

        [TestCase("")]
        [TestCase("한글")]
        [TestCase("{}{literal}")]
        public void StringKeysAndEscapedKeysSurviveRoundTrip(string key)
        {
            var dictionary = Build<ResourceDictionary>("<ResourceDictionary " + Namespaces + ">" +
                "<SolidColorBrush x:Key=\"" + key + "\"/></ResourceDictionary>");
            Assert.That(dictionary.Contains(key.StartsWith("{}") ? key.Substring(2) : key), Is.True);
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("한글")]
        public void IrVersionThreePreservesKey(string key)
        {
            var document = new XamlDocument { Root = new XamlObjectNode { TypeName = "T", Key = key } };
            byte[] bytes = XamlIrSerializer.Serialize(document);
            Assert.That(BitConverter.ToInt32(bytes, 4), Is.EqualTo(3));
            Assert.That(XamlIrSerializer.Deserialize(bytes).Root.Key, Is.EqualTo(key));
        }

        [Test]
        public void ActualVersionTwoLayoutIsRejected()
        {
            byte[] bytes = { 88, 65, 73, 82, 2, 0, 0, 0, 1, 1, 84, 0,
                1, 0, 0, 0, 2, 0, 0, 0, 0, 0, 0, 0 };
            Assert.Throws<InvalidDataException>(() => XamlIrSerializer.Deserialize(bytes));
        }

        [TestCase("<SolidColorBrush/>", "Resource dictionary entries require an x:Key.")]
        [TestCase("<SolidColorBrush x:Key=\"duplicate\"/>", "Duplicate resource key 'duplicate'.")]
        [TestCase("<Style TargetType=\"{x:Type Border}\"/>", "Duplicate resource key 'Border'.")]
        public void EntryErrorsPointToTheOffendingElement(string entry, string message)
        {
            AssertError("<ResourceDictionary " + Namespaces + "><SolidColorBrush x:Key=\"duplicate\"/>" +
                "<Style TargetType=\"Border\"/>\n  " + entry + "</ResourceDictionary>", message, 2, 4);
        }

        [TestCase("<Border", " />")]
        [TestCase("<Border><Border.Child><Border", "/></Border.Child></Border>")]
        [TestCase("<ResourceDictionary><ResourceDictionary.MergedDictionaries><ResourceDictionary", "/></ResourceDictionary.MergedDictionaries></ResourceDictionary>")]
        public void KeyOutsideDictionaryEntryIsRejected(string start, string end)
        {
            int firstEnd = start.IndexOf('>');
            string xaml = firstEnd < 0 ? start + " " + Namespaces : start.Insert(firstEnd, " " + Namespaces);
            AssertError(xaml + "\n  x:Key=\"key\"" + end, "x:Key is only valid on resource dictionary entries.", 2, 3);
        }

        [Test]
        public void KeyMarkupExtensionIsRejectedAtTheAttribute()
        {
            AssertError("<ResourceDictionary " + Namespaces + "><SolidColorBrush\n  x:Key=\"{x:Type Border}\"/></ResourceDictionary>",
                "x:Key does not support markup extensions.", 2, 3);
        }

        [TestCase("{StaticResource}")]
        [TestCase("{StaticResource a, b}")]
        [TestCase("{StaticResource ResourceKey=a, ResourceKey=b}")]
        [TestCase("{StaticResource Key=a}")]
        [TestCase("{StaticResource a, Other=b}")]
        public void StaticResourceRequiresExactlyOneKey(string value)
        {
            AssertError("<Border " + Namespaces + "\n  Background=\"" + value + "\"/>",
                "Markup extension 'StaticResource' requires exactly one ResourceKey.", 2, 3);
        }

        [TestCase("<Border.Background><SolidColorBrush", "Color", "{StaticResource brush}", "</Border.Background>")]
        [TestCase("<Border", "Width", "{StaticResource brush}", "")]
        public void WrongResourceTypesFailAtTheProperty(string start, string property, string value, string end)
        {
            var document = Parse("<Border " + Namespaces + "><Border.Resources><SolidColorBrush x:Key=\"brush\"/></Border.Resources>" +
                (start.StartsWith("<Border.Background") ? "<Border>" : "") + start + "\n  " + property + "=\"" + value + "\"/>" + end +
                (start.StartsWith("<Border.Background") ? "</Border>" : "") + "</Border>");
            var error = Assert.Throws<XamlBuildException>(() => XamlBuilder.Build(document));
            Assert.That((error.Line, error.Column), Is.EqualTo((2, 3)));
            Assert.That(error.Message, Does.Contain("Cannot set property"));
        }

        [TestCase("<Border", "Background", "missing", " />")]
        [TestCase("<Border", "Background", "later", "><Border.Resources><SolidColorBrush x:Key=\"later\"/></Border.Resources></Border>")]
        [TestCase("<Border><Border.Child><Border", "Background", "later", "/></Border.Child><Border.Resources><SolidColorBrush x:Key=\"later\"/></Border.Resources></Border>")]
        [TestCase("<ResourceDictionary><Style x:Key=\"self\" TargetType=\"Border\"", "BasedOn", "self", "/></ResourceDictionary>")]
        [TestCase("<ResourceDictionary><Style x:Key=\"style\" TargetType=\"Border\"><Setter Property=\"Background\"", "Value", "missing", "/></Style></ResourceDictionary>")]
        public void MissingForwardAndSelfReferencesFailAtExtensionLocation(string start, string property, string key, string end)
        {
            int firstEnd = start.IndexOf('>');
            string xaml = firstEnd < 0 ? start + " " + Namespaces : start.Insert(firstEnd, " " + Namespaces);
            var document = Parse(xaml + "\n  " + property + "=\"{StaticResource " + key + "}\"" + end);
            var error = Assert.Throws<XamlBuildException>(() => XamlBuilder.Build(document));
            Assert.That((error.Line, error.Column), Is.EqualTo((2, 3)));
            Assert.That(error.Message, Is.EqualTo("(2,3): Resource '" + key + "' was not found."));
        }

        private static XamlDocument Parse(string xaml)
        {
            XamlParseResult result = XamlParser.Parse(xaml, "resources.xaml");
            Assert.That(result.Errors, Is.Empty, string.Join("\n", result.Errors));
            Assert.That(result.AssetReferences, Is.Empty);
            return XamlIrSerializer.Deserialize(XamlIrSerializer.Serialize(result.Document));
        }

        private static T Build<T>(string xaml) => (T)XamlBuilder.Build(Parse(xaml));

        private static void AssertError(string xaml, string message, int line, int column)
        {
            XamlParseResult result = XamlParser.Parse(xaml, "resources.xaml");
            Assert.That(result.Document, Is.Null);
            Assert.That(result.Errors.Count, Is.EqualTo(1), string.Join("\n", result.Errors));
            Assert.That(result.Errors[0].Message, Is.EqualTo(message));
            Assert.That(result.Errors[0].File, Is.EqualTo("resources.xaml"));
            Assert.That((result.Errors[0].Line, result.Errors[0].Column), Is.EqualTo((line, column)));
        }
    }

    public class SeedResources : ResourceDictionary
    {
        public SeedResources()
        {
            Add("number", 12.5);
            Add("color", Color.FromRgb(1, 2, 3));
            Add("Image", new ResourceImageSource());
            Add("type", typeof(Border));
            Add("property", Border.HeightProperty);
            Add("c", "text");
        }

        private sealed class ResourceImageSource : ImageSource
        {
            public override double Width => 10;
            public override double Height => 20;
        }
    }
}
