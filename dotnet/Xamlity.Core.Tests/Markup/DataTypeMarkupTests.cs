using System;
using System.ComponentModel;
using System.Linq;
using NUnit.Framework;
using Xamlity.Controls;
using Xamlity.Layout;
using Xamlity.Markup;

namespace Xamlity.Core.Tests.Markup
{
    public class DataTypeMarkupTests
    {
        private const string Mapping = "clr-namespace:Xamlity.Core.Tests.Markup;assembly=Xamlity.Core.Tests";
        private const string Namespaces = "xmlns=\"https://github.com/loonacia/xamlity\" " +
            "xmlns:x=\"http://schemas.microsoft.com/winfx/2006/xaml\" xmlns:t=\"" + Mapping + "\"";

        [TestCase("Name")]
        [TestCase("Player.Name")]
        [TestCase("Inherited")]
        [TestCase("ReadOnly")]
        [TestCase("")]
        [TestCase(".")]
        public void ValidPathsUseDeclaredProperties(string path)
        {
            Parse(Attribute("DataTypeModel", "{Binding Path=" + path + "}"));
        }

        [TestCase("{Binding}")]
        [TestCase("{Binding Mode=TwoWay}")]
        [TestCase("{Binding Path=., Mode=TwoWay}")]
        [TestCase("{Binding Path=Player.Name, Mode=TwoWay}")]
        public void MissingOrEmptyPathsAndWritableTwoWayPathsPassValidation(string binding)
        {
            Parse(Attribute("DataTypeModel", binding));
        }

        [TestCase("IDataTypeModel", "Inherited")]
        [TestCase("AbstractDataTypeModel", "Name")]
        [TestCase("StructDataTypeModel", "Name")]
        [TestCase("NoConstructorDataTypeModel", "Name")]
        [TestCase("PrivateConstructorDataTypeModel", "Name")]
        [TestCase("DerivedDataTypeModel", "Player.Name")]
        [TestCase("IDerivedDataTypeModel", "Player.Name")]
        [TestCase("ICombinedDataTypeModel", "Player.Name")]
        public void PublicTypesDoNotNeedToBeCreatableAndMostDerivedPropertiesWin(string type, string path)
        {
            Parse(Attribute(type, "{Binding " + path + "}"));
        }

        [Test]
        public void UnprefixedDataTypeUsesTheLocalDefaultNamespace()
        {
            Parse("<ui:TextBlock xmlns:ui=\"https://github.com/loonacia/xamlity\" " +
                "xmlns:x=\"http://schemas.microsoft.com/winfx/2006/xaml\" xmlns=\"" + Mapping + "\" " +
                "x:DataType=\"DataTypeModel\" Text=\"{ui:Binding Name}\"/>");
            Parse("<TextBlock " + Namespaces + " x:DataType=\"Thickness\" Text=\"{Binding Left}\"/>");
        }

        [Test]
        public void PrefixUsesTheDeclarationAtTheElementAndAttributeOrderDoesNotMatter()
        {
            Parse("<StackPanel " + Namespaces + " x:DataType=\"t:DataTypeModel\">" +
                "<TextBlock Text=\"{Binding Width}\" x:DataType=\"t:Border\" " +
                "xmlns:t=\"clr-namespace:Xamlity.Controls;assembly=Xamlity.Core\"/>" +
                "<TextBlock Text=\"{Binding Name}\"/></StackPanel>");
        }

        [TestCase("Nmae", "Nmae", typeof(DataTypeModel))]
        [TestCase("Player.Nmae.Length", "Nmae", typeof(PlayerModel))]
        [TestCase("Item", "Item", typeof(DataTypeModel))]
        [TestCase("PrivateGetter", "PrivateGetter", typeof(DataTypeModel))]
        [TestCase("StaticProperty", "StaticProperty", typeof(DataTypeModel))]
        [TestCase("Field", "Field", typeof(DataTypeModel))]
        [TestCase("DeclaredObject.Name", "Name", typeof(object))]
        public void InvalidPathsReportTheBindingAttributeLocation(string path, string segment, Type type)
        {
            AssertError(Attribute("DataTypeModel", "{Binding " + path + "}"), Missing(path, segment, type), 2, 3);
        }

        [TestCase("ReadOnly")]
        [TestCase("PrivateSetter")]
        public void TwoWayRequiresAPublicSetter(string path)
        {
            AssertError(Attribute("DataTypeModel", "{Binding " + path + ", Mode=TwoWay}"),
                NoSetter(path, path, typeof(DataTypeModel)), 2, 3);
        }

        [Test]
        public void NestedTwoWayOnlyRequiresTheLastSetter()
        {
            Parse(Attribute("DataTypeModel", "{Binding ReadOnlyPlayer.Name, Mode=TwoWay}"));
            AssertError(Attribute("DataTypeModel", "{Binding Player.ReadOnly, Mode=TwoWay}"),
                NoSetter("Player.ReadOnly", "ReadOnly", typeof(PlayerModel)), 2, 3);
        }

        [TestCase("Missing")]
        [TestCase("InternalDataTypeModel")]
        public void UnknownOrNonPublicDataTypesReportTheDirectiveLocation(string type)
        {
            AssertError("<TextBlock " + Namespaces + "\n  x:DataType=\"t:" + type + "\"/>",
                "Unknown type '" + type + "' in namespace 'Xamlity.Core.Tests.Markup' of assembly 'Xamlity.Core.Tests'.", 2, 3);
        }

        [TestCase("{x:Type t:DataTypeModel}")]
        [TestCase("{}t:DataTypeModel")]
        [TestCase("{Binding}")]
        public void DataTypeRejectsMarkupExtensions(string value)
        {
            AssertError("<TextBlock " + Namespaces + "\n  x:DataType=\"" + value + "\"/>",
                "x:DataType does not support markup extensions; use 'prefix:TypeName'.", 2, 3);
        }

        [TestCase("Name", "OneWay")]
        [TestCase("{}Name", "OneWay")]
        [TestCase("Player.Name", "TwoWay")]
        [TestCase("", "OneWay")]
        [TestCase(".", "OneWay")]
        public void ObjectBindingsUseTheSameValidation(string path, string mode)
        {
            Parse(ObjectBinding(path, mode));
        }

        [Test]
        public void ObjectBindingErrorsUseTheBindingElementLocation()
        {
            AssertError(ObjectBinding("Player.Nmae", "OneWay"), Missing("Player.Nmae", "Nmae", typeof(PlayerModel)), 3, 6);
            AssertError(ObjectBinding("ReadOnly", "TwoWay"), NoSetter("ReadOnly", "ReadOnly", typeof(DataTypeModel)), 3, 6);
        }

        [Test]
        public void ConvertersDoNotSuppressPathValidationOrCheckTargetCompatibility()
        {
            string xaml = "<TextBlock " + Namespaces + " x:DataType=\"t:DataTypeModel\"><TextBlock.Text>\n" +
                "  <Binding Path=\"Name\"><Binding.Converter><t:UpperCaseConverter/>" +
                "</Binding.Converter></Binding></TextBlock.Text></TextBlock>";
            var text = (TextBlock)Build(xaml);
            text.DataContext = new DataTypeModel { Name = "first" };
            Assert.That(text.Text, Is.EqualTo("FIRST"));
            AssertError(xaml.Replace("Path=\"Name\"", "Path=\"Nmae\""),
                Missing("Nmae", "Nmae", typeof(DataTypeModel)), 2, 4);
            Parse("<Border " + Namespaces + " x:DataType=\"t:DataTypeModel\" Width=\"{Binding Player}\"/>");
        }

        [Test]
        public void DocumentsWithoutDataTypeDoNotValidatePaths()
        {
            Parse("<TextBlock " + Namespaces + " Text=\"{Binding Nmae}\"/>");
            Parse("<TextBlock " + Namespaces + "><TextBlock.Text><Binding Path=\"Nmae\"/>" +
                "</TextBlock.Text></TextBlock>");
        }

        [Test]
        public void ChildOverridesApplyToDescendantsAndDoNotLeakToSiblings()
        {
            string xaml = "<StackPanel " + Namespaces + " x:DataType=\"t:DataTypeModel\">" +
                "<Border x:DataType=\"t:PlayerModel\"><TextBlock Text=\"{Binding Score}\"/></Border>\n" +
                "  <TextBlock Text=\"{Binding Player.Name}\"/></StackPanel>";
            Parse(xaml);
            AssertError(xaml.Replace("{Binding Player.Name}", "{Binding Score}"),
                Missing("Score", "Score", typeof(DataTypeModel)), 2, 14);
            Parse("<StackPanel " + Namespaces + "><TextBlock x:DataType=\"t:PlayerModel\" Text=\"{Binding Score}\"/>" +
                "<TextBlock Text=\"{Binding Typo}\"/></StackPanel>");
        }

        [TestCase(false)]
        [TestCase(true)]
        public void DataContextBindingsUseTheParentScopeAndRuntimeBindingsStillUpdate(bool objectElement)
        {
            string context = objectElement ? "" : " DataContext=\"{Binding Player}\"";
            string contextObject = objectElement ? "<Border.DataContext><Binding Path=\"Player\"/></Border.DataContext>" : "";
            var panel = (StackPanel)Build("<StackPanel " + Namespaces + " x:DataType=\"t:DataTypeModel\">" +
                "<Border" + context + " Width=\"{Binding Score}\" x:DataType=\"t:PlayerModel\">" + contextObject +
                "<TextBlock Text=\"{Binding Name, Mode=TwoWay}\"/></Border></StackPanel>");
            var model = new DataTypeModel { Player = new PlayerModel { Name = "first" } };
            panel.DataContext = model;
            var border = (Border)panel.Children.Single();
            var text = (TextBlock)border.Child;
            Assert.That(border.DataContext, Is.SameAs(model.Player));
            Assert.That(border.Width, Is.EqualTo(42));
            Assert.That(text.Text, Is.EqualTo("first"));
            model.Player.Name = "second";
            Assert.That(text.Text, Is.EqualTo("second"));
            text.SetValue(TextBlock.TextProperty, "third");
            Assert.That(model.Player.Name, Is.EqualTo("third"));
        }

        [Test]
        public void DataContextBindingWithoutAParentScopeIsNotValidatedAgainstItsOwnType()
        {
            Parse("<Border " + Namespaces + " x:DataType=\"t:PlayerModel\" DataContext=\"{Binding Typo}\"/>");
            Parse("<Border " + Namespaces + " x:DataType=\"t:PlayerModel\"><Border.DataContext>" +
                "<Binding Path=\"Typo\"/></Border.DataContext></Border>");
        }

        [Test]
        public void DataContextErrorsUseTheParentTypeEvenWhenTheChildTypeHasTheProperty()
        {
            AssertError("<Border " + Namespaces + " x:DataType=\"t:DataTypeModel\"><Border x:DataType=\"t:PlayerModel\"\n" +
                "  DataContext=\"{Binding Score}\"/></Border>", Missing("Score", "Score", typeof(DataTypeModel)), 2, 3);
            AssertError("<Border " + Namespaces + " x:DataType=\"t:DataTypeModel\"><Border x:DataType=\"t:PlayerModel\">" +
                "<Border.DataContext>\n  <Binding Path=\"Score\"/></Border.DataContext></Border></Border>",
                Missing("Score", "Score", typeof(DataTypeModel)), 2, 4);
        }

        [Test]
        public void DataTypeIsAcceptedOnNonVisualAndPropertyElementsWithoutEnteringTheIr()
        {
            XamlParseResult result = Parse("<TextBlock " + Namespaces + "><TextBlock.Text x:DataType=\"t:DataTypeModel\">" +
                "<Binding x:DataType=\"t:PlayerModel\" Path=\"Score\"><Binding.Converter>" +
                "<t:UpperCaseConverter x:DataType=\"t:DataTypeModel\"/>" +
                "</Binding.Converter></Binding></TextBlock.Text></TextBlock>");
            XamlObjectNode binding = result.Document.Root.Properties.Single().Objects.Single();
            Assert.That(binding.Properties.Select(p => p.PropertyName), Is.EqualTo(new[] { "Path", "Converter" }));
            Assert.That(binding.Properties.Last().Objects.Single().Properties, Is.Empty);
            Assert.That(result.ReferencedAssemblies, Is.EqualTo(new[] { "Xamlity.Core.Tests" }));
        }

        private static string Attribute(string type, string binding) =>
            "<TextBlock " + Namespaces + " x:DataType=\"t:" + type + "\"\n  Text=\"" + binding + "\"/>";

        private static string ObjectBinding(string path, string mode) =>
            "<TextBlock " + Namespaces + " x:DataType=\"t:DataTypeModel\">\n  <TextBlock.Text>\n" +
            "    <Binding Path=\"" + path + "\" Mode=\"" + mode + "\"/>\n  </TextBlock.Text>\n</TextBlock>";

        private static string Missing(string path, string segment, Type type) =>
            $"Binding path '{path}': property '{segment}' was not found on type '{type.FullName}' (x:DataType).";

        private static string NoSetter(string path, string segment, Type type) =>
            $"Binding path '{path}': property '{segment}' on type '{type.FullName}' has no public setter; TwoWay bindings require one.";

        private static XamlParseResult Parse(string xaml)
        {
            XamlParseResult result = XamlParser.Parse(xaml, "datatype.xaml");
            Assert.That(result.Errors, Is.Empty, string.Join("\n", result.Errors));
            return result;
        }

        private static object Build(string xaml) => XamlBuilder.Build(
            XamlIrSerializer.Deserialize(XamlIrSerializer.Serialize(Parse(xaml).Document)));

        private static void AssertError(string xaml, string message, int line, int column)
        {
            XamlParseResult result = XamlParser.Parse(xaml, "datatype.xaml");
            Assert.That(result.Document, Is.Null);
            Assert.That(result.Errors.Count, Is.EqualTo(1), string.Join("\n", result.Errors));
            Assert.That(result.Errors[0].Message, Is.EqualTo(message));
            Assert.That(result.Errors[0].File, Is.EqualTo("datatype.xaml"));
            Assert.That((result.Errors[0].Line, result.Errors[0].Column), Is.EqualTo((line, column)));
        }
    }

    public class BaseDataTypeModel
    {
        public string Inherited { get; set; }
    }

    public class DataTypeModel : BaseDataTypeModel
    {
        public string Name { get; set; }
        public PlayerModel Player { get; set; }
        public PlayerModel ReadOnlyPlayer => Player;
        public object DeclaredObject => Player;
        public string ReadOnly => "read-only";
        public string PrivateSetter { get; private set; }
        public string PrivateGetter { private get; set; }
        public string this[int index] => "indexed";
        public static string StaticProperty { get; set; }
        public string Field;
    }

    public class PlayerModel : INotifyPropertyChanged
    {
        private string name;
        public string Name { get => name; set { name = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Name))); } }
        public double Score => 42;
        public string ReadOnly => "read-only";
        public event PropertyChangedEventHandler PropertyChanged;
    }

    public interface IBaseDataTypeModel { string Inherited { get; } }
    public interface IDataTypeModel : IBaseDataTypeModel { }
    public abstract class AbstractDataTypeModel { public abstract string Name { get; } }
    public struct StructDataTypeModel { public string Name { get; set; } }
    public class NoConstructorDataTypeModel { public NoConstructorDataTypeModel(int value) { } public string Name { get; set; } }
    public class PrivateConstructorDataTypeModel { private PrivateConstructorDataTypeModel() { } public string Name { get; set; } }
    internal class InternalDataTypeModel { }
    public class HiddenDataTypeModel { public object Player { get; set; } }
    public class DerivedDataTypeModel : HiddenDataTypeModel { public new PlayerModel Player { get; set; } }
    public interface IHiddenDataTypeModel { object Player { get; } }
    public interface IDerivedDataTypeModel : IHiddenDataTypeModel { new PlayerModel Player { get; } }
    public interface ICombinedDataTypeModel : IHiddenDataTypeModel, IDerivedDataTypeModel { }
}
