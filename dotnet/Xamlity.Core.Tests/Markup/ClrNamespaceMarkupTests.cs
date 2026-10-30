using System;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using NUnit.Framework;
using Xamlity.Controls;
using Xamlity.Data;
using Xamlity.Layout;
using Xamlity.Markup;
using Xamlity.Rendering;

namespace Xamlity.Core.Tests.Markup
{
    public class ClrNamespaceMarkupTests
    {
        private const string Mapping = "clr-namespace:Xamlity.Core.Tests.Markup;assembly=Xamlity.Core.Tests";
        private const string Namespaces = "xmlns=\"https://github.com/loonacia/xamlity\" " +
            "xmlns:x=\"http://schemas.microsoft.com/winfx/2006/xaml\" xmlns:t=\"" + Mapping + "\"";

        [Test]
        public void ConverterPropertiesAndParameterApplyAfterIrRoundTripAndTrackSourceChanges()
        {
            var text = Build<TextBlock>(ConverterXaml("<t:UpperCaseConverter Suffix=\"!\"/>",
                "ConverterParameter=\"prefix:\""));
            var model = new ClrNamespaceModel { Name = "first" };
            text.DataContext = model;
            Assert.That(text.Text, Is.EqualTo("prefix:FIRST!"));
            model.Name = "second";
            Assert.That(text.Text, Is.EqualTo("prefix:SECOND!"));
            Binding binding = BindingOperations.GetBindingExpression(text, TextBlock.TextProperty).ParentBinding;
            Assert.That(binding.Converter, Is.TypeOf<UpperCaseConverter>());
            Assert.That(((UpperCaseConverter)binding.Converter).Suffix, Is.EqualTo("!"));
            Assert.That(binding.ConverterParameter, Is.EqualTo("prefix:"));
        }

        [Test]
        public void TwoWayConverterReceivesParameterAndWritesBackToSource()
        {
            var text = Build<TextBlock>(ConverterXaml("<t:UpperCaseConverter Suffix=\"!\"/>",
                "Mode=\"TwoWay\" ConverterParameter=\"prefix:\""));
            var model = new ClrNamespaceModel { Name = "first" };
            text.DataContext = model;
            text.SetValue(TextBlock.TextProperty, "prefix:SECOND!");
            Assert.That(model.Name, Is.EqualTo("second"));
            model.Name = "third";
            Assert.That(text.Text, Is.EqualTo("prefix:THIRD!"));
        }

        [Test]
        public void CoreTypesCanBeMappedThroughClrNamespace()
        {
            const string mapping = "clr-namespace:Xamlity.Controls;assembly=Xamlity.Core";
            XamlParseResult result = Parse("<Border xmlns=\"" + mapping + "\" Width=\"12\"/>");
            Assert.That(result.Document.Root.TypeName, Is.EqualTo("Xamlity.Controls.Border, Xamlity.Core"));
            Assert.That(((Border)XamlBuilder.Build(result.Document)).Width, Is.EqualTo(12));
            Assert.That(result.ReferencedAssemblies, Is.EqualTo(new[] { "Xamlity.Core" }));
        }

        [Test]
        public void CustomElementsWorkAsRootAndChildrenWithInheritedContentAndBindings()
        {
            var root = Build<MyBorder>("<t:MyBorder " + Namespaces + " x:Name=\"frame\" Width=\"50\">" +
                "<StackPanel><t:MyBorder x:Name=\"inner\"><TextBlock Text=\"{Binding Name}\"/>" +
                "</t:MyBorder></StackPanel></t:MyBorder>");
            Assert.That(root.Name, Is.EqualTo("frame"));
            Assert.That(root.Width, Is.EqualTo(50));
            var inner = (MyBorder)((StackPanel)root.Child).Children.Single();
            Assert.That(inner.Name, Is.EqualTo("inner"));
            root.DataContext = new ClrNamespaceModel { Name = "inherited" };
            Assert.That(((TextBlock)inner.Child).Text, Is.EqualTo("inherited"));
        }

        [TestCase("Border")]
        [TestCase("t:MyBorder")]
        [TestCase("c:Border")]
        public void PropertyElementOwnerResolvesByNamespaceAndAllowsBaseTypes(string owner)
        {
            var border = Build<MyBorder>("<t:MyBorder " + Namespaces +
                " xmlns:c=\"clr-namespace:Xamlity.Controls;assembly=Xamlity.Core\">" +
                "<" + owner + ".Background><SolidColorBrush Color=\"Red\"/></" + owner + ".Background></t:MyBorder>");
            Assert.That(((SolidColorBrush)border.Background).Color, Is.EqualTo(Color.FromRgb(255, 0, 0)));
        }

        [Test]
        public void AbstractBaseTypeCanOwnAPropertyElement()
        {
            var border = Build<ConcreteBorder>("<t:ConcreteBorder " + Namespaces + ">" +
                "<t:AbstractBorder.Background><SolidColorBrush Color=\"Blue\"/>" +
                "</t:AbstractBorder.Background></t:ConcreteBorder>");
            Assert.That(((SolidColorBrush)border.Background).Color, Is.EqualTo(Color.FromRgb(0, 0, 255)));
        }

        [Test]
        public void SameOwnerNameInAnotherNamespaceDoesNotMatch()
        {
            AssertError("<t:MyBorder " + Namespaces +
                " xmlns:other=\"clr-namespace:Xamlity.Core.Tests.Markup.Other;assembly=Xamlity.Core.Tests\">\n" +
                "  <other:MyBorder.Background><SolidColorBrush/></other:MyBorder.Background></t:MyBorder>",
                "Attached property elements are not supported: 'MyBorder.Background'.", 2, 4);
        }

        [TestCase("clr-namespace:Game.UI", "clr-namespace mapping 'clr-namespace:Game.UI' must specify 'assembly='.")]
        [TestCase("clr-namespace:;assembly=Game", "Invalid clr-namespace mapping 'clr-namespace:;assembly=Game'.")]
        [TestCase("clr-namespace:", "Invalid clr-namespace mapping 'clr-namespace:'.")]
        [TestCase("clr-namespace:Game.UI;assembly=", "Invalid clr-namespace mapping 'clr-namespace:Game.UI;assembly='.")]
        [TestCase("clr-namespace:Game.UI;Assembly=Game", "Invalid clr-namespace mapping 'clr-namespace:Game.UI;Assembly=Game'.")]
        [TestCase("clr-namespace:Game.UI;assembly=Game;extra=1", "Invalid clr-namespace mapping 'clr-namespace:Game.UI;assembly=Game;extra=1'.")]
        [TestCase("clr-namespace:Game.UI;assembly=Game, Version=1.0.0.0", "Invalid clr-namespace mapping 'clr-namespace:Game.UI;assembly=Game, Version=1.0.0.0'.")]
        public void InvalidMappingsReportTheDeclarationLocationEvenWhenUnused(string mapping, string message)
        {
            AssertError("<Border xmlns=\"https://github.com/loonacia/xamlity\"\n  xmlns:t=\"" + mapping + "\"/>",
                message, 2, 3);
            AssertError("<t:MyBorder\n  xmlns:t=\"" + mapping + "\"/>", message, 2, 3);
        }

        [Test]
        public void UnknownTypeReportsObjectLocation()
        {
            AssertError("<Border " + Namespaces + ">\n  <t:Missing/></Border>",
                "Unknown type 'Missing' in namespace 'Xamlity.Core.Tests.Markup' of assembly 'Xamlity.Core.Tests'.", 2, 4);
        }

        [Test]
        public void MissingAssemblyReportsUnknownTypeAndStillRecordsDependency()
        {
            const string xaml = "<Border xmlns=\"https://github.com/loonacia/xamlity\" " +
                "xmlns:t=\"clr-namespace:Game.UI;assembly=Xamlity.Missing.Assembly\">\n  <t:Missing/></Border>";
            AssertError(xaml, "Unknown type 'Missing' in namespace 'Game.UI' of assembly 'Xamlity.Missing.Assembly'.", 2, 4);
            Assert.That(XamlParser.Parse(xaml, "clr.xaml").ReferencedAssemblies,
                Is.EqualTo(new[] { "Xamlity.Missing.Assembly" }));
        }

        [TestCase(typeof(AbstractBorder))]
        [TestCase(typeof(NoDefaultConstructor))]
        [TestCase(typeof(PrivateConstructor))]
        [TestCase(typeof(InternalBorder))]
        [TestCase(typeof(ValueTypeElement))]
        [TestCase(typeof(IValueTypeElement))]
        public void NonCreatableTypesReportObjectLocation(Type type)
        {
            AssertError("<Border " + Namespaces + ">\n  <t:" + type.Name + "/></Border>",
                CreationError(type), 2, 4);
        }

        [Test]
        public void GenericTypesCannotBeCreated()
        {
            // CLR generic arity uses a backtick, which cannot occur in an XML element name.
            Assert.That(XamlTypeResolver.Resolve(Mapping, typeof(GenericBorder<>).Name, out string error), Is.Null);
            Assert.That(error, Is.EqualTo(CreationError(typeof(GenericBorder<>))));
            AssertError("<t:MyBorder " + Namespaces + "\n  x:TypeArguments=\"TextBlock\"/>",
                "XAML directive 'x:TypeArguments' is not supported.", 2, 3);
        }

        [Test]
        public void NonConverterCannotBeAssignedToConverterProperty()
        {
            AssertError(ConverterXaml("\n  <t:MyBorder/>"),
                "Type 'MyBorder' is not assignable to property 'Converter' of type 'IValueConverter'.", 2, 4);
        }

        [Test]
        public void RootMustStillBeAUiElement()
        {
            AssertError("\n<t:UpperCaseConverter " + Namespaces + "/>",
                "Root element 'UpperCaseConverter' must be a UIElement or ResourceDictionary.", 2, 2);
        }

        [Test]
        public void NameStillRequiresAFrameworkElement()
        {
            AssertError(ConverterXaml("<t:UpperCaseConverter\n  x:Name=\"converter\"/>"),
                "x:Name is only supported on FrameworkElement types, not 'UpperCaseConverter'.", 2, 3);
        }

        [TestCase("t:Foo.Bar")]
        [TestCase("t:MyBorder.Width")]
        [TestCase("t:Width")]
        public void ClrNamespaceAttributesRemainUnsupported(string attribute)
        {
            AssertError("<t:MyBorder " + Namespaces + "\n  " + attribute + "=\"1\"/>",
                "XML namespace '" + Mapping + "' is not supported.", 2, 3);
        }

        [Test]
        public void ReferencesIncludeUnusedMappingsInDeclarationOrderWithoutDuplicates()
        {
            XamlParseResult result = Parse("<Border " + Namespaces +
                " xmlns:a=\"clr-namespace:Unused;assembly=Unused.First\"" +
                " xmlns:b=\"clr-namespace:Other;assembly=Xamlity.Core.Tests\">" +
                "<Border.Child xmlns:c=\"clr-namespace:Unused;assembly=Unused.Second\">" +
                "<Border xmlns:t=\"clr-namespace:Unused;assembly=Unused.Third\"/>" +
                "</Border.Child></Border>");
            Assert.That(result.ReferencedAssemblies,
                Is.EqualTo(new[] { "Xamlity.Core.Tests", "Unused.First", "Unused.Second", "Unused.Third" }));
            Assert.That(Parse("<Border xmlns=\"https://github.com/loonacia/xamlity\"/>").ReferencedAssemblies, Is.Empty);
        }

        [Test]
        public void ReferencesSurviveErrorsAndIncludeDeclarationsInSkippedPropertySubtrees()
        {
            XamlParseResult result = XamlParser.Parse("<Border " + Namespaces + " Width=\"bad\">" +
                "<Border.Child><Border.Child xmlns:a=\"clr-namespace:A;assembly=Skipped.Outer\">" +
                "<Missing xmlns:b=\"clr-namespace:B;assembly=Skipped.Inner\"/>" +
                "</Border.Child></Border.Child>" +
                "<Missing xmlns:c=\"clr-namespace:C;assembly=After.Error\"/></Border>", "clr.xaml");
            Assert.That(result.Document, Is.Null);
            Assert.That(result.Errors, Is.Not.Empty);
            Assert.That(result.ReferencedAssemblies,
                Is.EqualTo(new[] { "Xamlity.Core.Tests", "Skipped.Outer", "Skipped.Inner", "After.Error" }));
        }

        [Test]
        public void ReferencesReadBeforeAnXmlSyntaxErrorAreRetained()
        {
            XamlParseResult result = XamlParser.Parse("<Border " + Namespaces + "></TextBlock>", "clr.xaml");
            Assert.That(result.Document, Is.Null);
            Assert.That(result.Errors.Count, Is.EqualTo(1));
            Assert.That(result.ReferencedAssemblies, Is.EqualTo(new[] { "Xamlity.Core.Tests" }));
        }

        private static string CreationError(Type type) => $"Type '{type.FullName}' cannot be created from XAML; " +
            "it must be a public non-abstract class with a public parameterless constructor.";

        private static string ConverterXaml(string converter, string attributes = "") =>
            "<TextBlock " + Namespaces + "><TextBlock.Text><Binding Path=\"Name\" " + attributes + ">" +
            "<Binding.Converter>" + converter + "</Binding.Converter></Binding></TextBlock.Text></TextBlock>";

        private static XamlParseResult Parse(string xaml)
        {
            XamlParseResult result = XamlParser.Parse(xaml, "clr.xaml");
            Assert.That(result.Errors, Is.Empty, string.Join("\n", result.Errors));
            return result;
        }

        private static T Build<T>(string xaml) => (T)XamlBuilder.Build(
            XamlIrSerializer.Deserialize(XamlIrSerializer.Serialize(Parse(xaml).Document)));

        private static void AssertError(string xaml, string message, int line, int column)
        {
            XamlParseResult result = XamlParser.Parse(xaml, "clr.xaml");
            Assert.That(result.Document, Is.Null);
            Assert.That(result.Errors.Count, Is.EqualTo(1), string.Join("\n", result.Errors));
            Assert.That(result.Errors[0].Message, Is.EqualTo(message));
            Assert.That(result.Errors[0].File, Is.EqualTo("clr.xaml"));
            Assert.That((result.Errors[0].Line, result.Errors[0].Column), Is.EqualTo((line, column)));
        }
    }

    public class UpperCaseConverter : IValueConverter
    {
        public string Suffix { get; set; } = "";

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
            parameter + ((string)value).ToUpperInvariant() + Suffix;

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            string text = (string)value;
            int prefixLength = ((string)parameter ?? "").Length;
            return text.Substring(prefixLength, text.Length - prefixLength - Suffix.Length).ToLowerInvariant();
        }
    }

    public class MyBorder : Border { }
    public abstract class AbstractBorder : Border { }
    public class ConcreteBorder : AbstractBorder { }
    public class NoDefaultConstructor : Border { public NoDefaultConstructor(int value) { } }
    public class PrivateConstructor : Border { private PrivateConstructor() { } }
    internal class InternalBorder : Border { }
    public struct ValueTypeElement { }
    public interface IValueTypeElement { }
    public class GenericBorder<T> : Border { }

    public class ClrNamespaceModel : INotifyPropertyChanged
    {
        private string name;
        public string Name { get => name; set { name = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Name))); } }
        public event PropertyChangedEventHandler PropertyChanged;
    }
}

namespace Xamlity.Core.Tests.Markup.Other
{
    public class MyBorder : Border { }
}
