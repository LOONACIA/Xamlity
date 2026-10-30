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

        [TestCase("{StaticResource {x:Type Border}}")]
        [TestCase("{StaticResource ResourceKey={x:Type TypeName=Border}}")]
        [TestCase("{ StaticResource ResourceKey = { x:Type Border } }")]
        public void TypeKeysResolveImplicitStylesAndBasedOnInOuterScope(string extension)
        {
            var root = Build<Border>("<Border " + Namespaces + "><Border.Resources>" +
                "<Style TargetType=\"Border\"><Setter Property=\"Width\" Value=\"42\"/></Style></Border.Resources>" +
                "<Border><Border.Resources><Style TargetType=\"Border\" BasedOn=\"" + extension + "\">" +
                "<Setter Property=\"Height\" Value=\"24\"/></Style></Border.Resources>" +
                "<Border Style=\"" + extension + "\"/></Border></Border>");
            var inner = (Border)root.Child;
            var child = (Border)inner.Child;
            Assert.That(child.Style, Is.SameAs(inner.Resources[typeof(Border)]));
            Assert.That(child.Style.BasedOn, Is.SameAs(root.Resources[typeof(Border)]));
            Assert.That(child.Width, Is.EqualTo(42));
            Assert.That(child.Height, Is.EqualTo(24));
        }

        [Test]
        public void NamedTypeKeyResolvesClrNamespaceAndPrefixAliases()
        {
            var root = Build<Border>("<Border " + Namespaces +
                " xmlns:local=\"clr-namespace:Xamlity.Core.Tests.Markup;assembly=Xamlity.Core.Tests\"" +
                " xmlns:ui=\"https://github.com/loonacia/xamlity\" xmlns:lang=\"http://schemas.microsoft.com/winfx/2006/xaml\">" +
                "<Border.Resources><Style TargetType=\"local:MyBorder\"><Setter Property=\"Width\" Value=\"13\"/></Style>" +
                "</Border.Resources><local:MyBorder Style=\"{ui:StaticResource ResourceKey={lang:Type local:MyBorder}}\"/></Border>");
            Assert.That(((MyBorder)root.Child).Style, Is.SameAs(root.Resources[typeof(MyBorder)]));
            Assert.That(((MyBorder)root.Child).Width, Is.EqualTo(13));
        }

        [TestCase("{StaticResource {StaticResource key}}", "StaticResource")]
        [TestCase("{StaticResource ResourceKey={Binding Name}}", "Binding")]
        [TestCase("{StaticResource {x:Type {StaticResource key}}}", "StaticResource")]
        [TestCase("{StaticResource {x:Type TypeName={Binding Name}}}", "Binding")]
        [TestCase("{StaticResource {x:Static Key}}", "x:Static")]
        [TestCase("{StaticResource {x:Type {x:Type Border}}}", "x:Type")]
        public void UnsupportedNestedResourceCombinationsReportWrittenName(string extension, string name)
        {
            AssertError("<Border " + Namespaces + "\n  Style=\"" + extension + "\"/>",
                "Nested markup extension '" + name + "' is not supported here.", 2, 3);
        }

        [Test]
        public void TopLevelTypeExtensionRejectsNestedExtensions()
        {
            AssertError("<ResourceDictionary " + Namespaces + "><Style x:Key=\"style\"\n  TargetType=\"{x:Type {StaticResource type}}\"/></ResourceDictionary>",
                "Nested markup extension 'StaticResource' is not supported here.", 2, 3);
        }

        [TestCase("{StaticResource {x:Type Border}", "expected closing '}'")]
        [TestCase("{StaticResource {x:Type Border}}}", "unexpected text after '}'")]
        [TestCase("{StaticResource {x:Type Border}x}", "unexpected text after a nested extension")]
        public void NestedTypeKeySyntaxErrorsKeepAttributeLocation(string extension, string message)
        {
            AssertError("<Border " + Namespaces + "\n  Style=\"" + extension + "\"/>",
                "Invalid markup extension syntax: " + message + ".", 2, 3);
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


        [Test]
        public void SourcePreservesIrPathLocationAndDistinctReferenceKinds()
        {
            XamlParseResult result = XamlParser.Parse("<Border " + Namespaces + "><Border.Resources>" +
                "<ResourceDictionary\n  Source=\"./Themes/../한글.xaml\"/></Border.Resources>" +
                "<Image Source=\"icon.png\"/></Border>", "resources.xaml");
            Assert.That(result.Errors, Is.Empty);
            Assert.That(result.AssetReferences.Select(reference => reference.Kind), Is.EqualTo(new[] {
                XamlAssetReferenceKind.ResourceDictionary, XamlAssetReferenceKind.Image }));
            XamlAssetReference reference = result.AssetReferences[0];
            Assert.That((reference.Value, reference.Line, reference.Column), Is.EqualTo(("./Themes/../한글.xaml", 2, 3)));
            var document = XamlIrSerializer.Deserialize(XamlIrSerializer.Serialize(result.Document));
            XamlPropertyNode source = document.Root.Properties[0].Objects[0].Properties.Single();
            Assert.That((source.PropertyName, source.TextValue, source.Line, source.Column),
                Is.EqualTo(("Source", reference.Value, 2, 3)));
            Assert.That(source.Objects, Is.Empty);
        }

        [TestCase("<ResourceDictionary {0} Source=\"B.xaml\"/>")]
        [TestCase("<t:SeedResources {0} Source=\"B.xaml\"/>")]
        [TestCase("<Border {0}><Border.Resources><ResourceDictionary Source=\"B.xaml\"/></Border.Resources></Border>")]
        [TestCase("<ResourceDictionary {0}><ResourceDictionary x:Key=\"nested\" Source=\"B.xaml\"/></ResourceDictionary>")]
        [TestCase("<ResourceDictionary {0}><ResourceDictionary.MergedDictionaries><ResourceDictionary Source=\"B.xaml\"/>" +
            "</ResourceDictionary.MergedDictionaries></ResourceDictionary>")]
        public void SourceCopiesEntriesAndMergedDictionariesAtAnyPosition(string xaml)
        {
            var brush = new SolidColorBrush(Color.FromRgb(1, 2, 3));
            var resolved = new ResourceDictionary { ["local"] = brush };
            resolved.MergedDictionaries.Add(new ResourceDictionary { ["merged"] = brush });
            var resolver = new DictionaryResolver { Dictionary = resolved };
            XamlParseResult result = XamlParser.Parse(string.Format(xaml, Namespaces), "resources.xaml");
            Assert.That(result.Errors, Is.Empty, string.Join("\n", result.Errors));
            object built = XamlBuilder.Build(XamlIrSerializer.Deserialize(XamlIrSerializer.Serialize(result.Document)), resolver);
            var dictionary = built is Border border ? border.Resources : (ResourceDictionary)built;
            if (dictionary.Contains("nested"))
                dictionary = (ResourceDictionary)dictionary["nested"];
            else if (dictionary.Source == null)
                dictionary = dictionary.MergedDictionaries.Single();
            Assert.That(dictionary.Source, Is.EqualTo(new Uri("B.xaml", UriKind.Relative)));
            Assert.That(dictionary.Count, Is.EqualTo(1));
            Assert.That(dictionary["local"], Is.SameAs(brush));
            Assert.That(dictionary["merged"], Is.SameAs(brush));
            Assert.That(dictionary.MergedDictionaries.Single(), Is.SameAs(resolved.MergedDictionaries[0]));
            Assert.That(resolver.Value, Is.EqualTo("B.xaml"));
            Assert.That(resolved.Source, Is.Null);
        }

        [Test]
        public void SourceDoesNotIgnoreOtherDictionaryAttributes()
        {
            XamlParseResult result = XamlParser.Parse("<t:SeedResources " + Namespaces +
                " Source=\"B.xaml\" Tag=\"kept\"/>", "resources.xaml");
            Assert.That(result.Errors, Is.Empty);
            var dictionary = (SeedResources)XamlBuilder.Build(result.Document,
                new DictionaryResolver { Dictionary = new ResourceDictionary() });
            Assert.That(dictionary.Tag, Is.EqualTo("kept"));
            Assert.That(dictionary.Count, Is.Zero);
        }

        [Test]
        public void StaticResourceFindsSourceMergedDictionary()
        {
            var brush = new SolidColorBrush(Color.FromRgb(1, 2, 3));
            var resolver = new DictionaryResolver { Dictionary = new ResourceDictionary { ["brush"] = brush } };
            XamlParseResult result = XamlParser.Parse("<Border " + Namespaces + "><Border.Resources><ResourceDictionary>" +
                "<ResourceDictionary.MergedDictionaries><ResourceDictionary Source=\"B.xaml\"/>" +
                "</ResourceDictionary.MergedDictionaries></ResourceDictionary></Border.Resources>" +
                "<Border Background=\"{StaticResource brush}\"/></Border>", "resources.xaml");
            Assert.That(result.Errors, Is.Empty);
            var border = (Border)XamlBuilder.Build(result.Document, resolver);
            Assert.That(((Border)border.Child).Background, Is.SameAs(brush));
        }

        [TestCase("<SolidColorBrush x:Key=\"brush\"/>")]
        [TestCase("<ResourceDictionary.MergedDictionaries><ResourceDictionary/></ResourceDictionary.MergedDictionaries>")]
        public void SourceRejectsInlineEntries(string content)
        {
            AssertError("\n<ResourceDictionary " + Namespaces + " Source=\"B.xaml\">" + content + "</ResourceDictionary>",
                "A ResourceDictionary with Source cannot have entries.", 2, 2);
        }

        [TestCase("")]
        [TestCase("   ")]
        public void SourceRejectsEmptyPaths(string value)
        {
            AssertError("<ResourceDictionary " + Namespaces + "\n  Source=\"" + value + "\"/>",
                "Resource dictionary paths must not be empty.", 2, 3);
        }

        [TestCase("{StaticResource path}")]
        [TestCase("{Binding Path}")]
        public void SourceRejectsUnsupportedExtensions(string value)
        {
            AssertError("<ResourceDictionary " + Namespaces + "\n  Source=\"" + value + "\"/>",
                "ResourceDictionary.Source does not support markup extensions.", 2, 3);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void UnresolvedSourceReportsDictionaryNodeLocation(bool missingResolver)
        {
            XamlParseResult result = XamlParser.Parse("\n<ResourceDictionary " + Namespaces + "\n  Source=\"B.xaml\"/>", "resources.xaml");
            Assert.That(result.Errors, Is.Empty);
            var error = Assert.Throws<XamlBuildException>(() => XamlBuilder.Build(result.Document,
                missingResolver ? null : new DictionaryResolver()));
            Assert.That(error.Message, Is.EqualTo("(2,2): Resource dictionary 'B.xaml' could not be resolved."));
            Assert.That((error.Line, error.Column), Is.EqualTo((2, 2)));
        }

        [Test]
        public void SourceCannotBeSetByRuntimeConsumers()
        {
            var property = typeof(ResourceDictionary).GetProperty(nameof(ResourceDictionary.Source));
            Assert.That(property.PropertyType, Is.EqualTo(typeof(Uri)));
            Assert.That(property.GetMethod.IsPublic, Is.True);
            Assert.That(property.SetMethod.IsAssembly, Is.True);
        }

        private sealed class DictionaryResolver : IXamlAssetResolver
        {
            internal ResourceDictionary Dictionary;
            internal string Value;
            public ImageSource ResolveImage(string value) => null;
            public ResourceDictionary ResolveResourceDictionary(string value)
            {
                Value = value;
                return Dictionary;
            }
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
        public string Tag { get; set; }

        public SeedResources()
        {
            Add("number", 12.5);
            Add("color", Color.FromRgb(1, 2, 3));
            Add("Image", new ResourceImageSource());
            Add("type", typeof(Border));
            Add("property", Border.HeightProperty);
            Add("c", "text");
            Add("P", "prefix:");
        }

        private sealed class ResourceImageSource : ImageSource
        {
            public override double Width => 10;
            public override double Height => 20;
        }
    }
}
