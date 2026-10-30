using System;
using System.Collections.Generic;
using System.ComponentModel;
using NUnit.Framework;
using Xamlity.Controls;
using Xamlity.Core.Tests.Rendering;
using Xamlity.Data;
using Xamlity.Layout;
using Xamlity.Markup;
using Xamlity.Properties;
using Xamlity.Styling;
using Xamlity.Tree;

namespace Xamlity.Core.Tests.Styling
{
    public class TriggerTests
    {
        private const string Namespaces = "xmlns='https://github.com/loonacia/xamlity' " +
            "xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml' " +
            "xmlns:t='clr-namespace:Xamlity.Core.Tests.Styling;assembly=Xamlity.Core.Tests'";

        [Test]
        public void StyleTriggerTransitionsNotifyAndRespectSetterAndLocalPrecedence()
        {
            var style = StyleWith(When(TriggerTestControl.ActiveProperty, true, 20));
            style.Setters.Add(new Setter(FrameworkElement.WidthProperty, 10.0));
            var control = new TriggerTestControl { Style = style };
            control.Changes.Clear();
            control.Active = true;
            Assert.That(control.Width, Is.EqualTo(20));
            Assert.That(control.WidthChanges, Is.EqualTo(1));
            control.Active = true;
            Assert.That(control.WidthChanges, Is.EqualTo(1));
            control.Width = 30;
            control.Active = false;
            control.Active = true;
            Assert.That(control.Width, Is.EqualTo(30));
            Assert.That(control.WidthChanges, Is.EqualTo(2));
            control.ClearValue(FrameworkElement.WidthProperty);
            Assert.That(control.Width, Is.EqualTo(20));
            control.Active = false;
            Assert.That(control.Width, Is.EqualTo(10));
            Assert.That(control.WidthChanges, Is.EqualTo(4));
        }

        [Test]
        public void LastActiveTriggerAndDerivedStyleWinAndRemovalRevealsEarlierValues()
        {
            var basis = StyleWith(When(TriggerTestControl.ActiveProperty, true, 10));
            var style = new Style(typeof(TriggerTestControl), basis);
            style.Triggers.Add(When(TriggerTestControl.OtherProperty, true, 20));
            var last = When(TriggerTestControl.OtherProperty, true, 25);
            last.Setters.Add(new Setter(FrameworkElement.WidthProperty, 30.0));
            style.Triggers.Add(last);
            var control = new TriggerTestControl { Active = true, Style = style };
            Assert.That(control.Width, Is.EqualTo(10));
            control.Other = true;
            Assert.That(control.Width, Is.EqualTo(30));
            control.Active = false;
            Assert.That(control.Width, Is.EqualTo(30));
            control.Active = true;
            control.Other = false;
            Assert.That(control.Width, Is.EqualTo(10));
            Assert.That(basis.Triggers[0].IsSealed, Is.True);
        }

        [Test]
        public void ReplacementDetachesOldConditionsAndSharedStylesHaveIndependentState()
        {
            var style = StyleWith(When(TriggerTestControl.ActiveProperty, true, 10));
            var first = new TriggerTestControl { Active = true, Style = style };
            var second = new TriggerTestControl { Style = style };
            first.Style = StyleWith(When(TriggerTestControl.OtherProperty, true, 20));
            Assert.That(first.Width, Is.NaN);
            first.Active = false;
            first.Active = true;
            Assert.That(first.Width, Is.NaN);
            first.Other = true;
            second.Active = true;
            Assert.That(first.Width, Is.EqualTo(20));
            Assert.That(second.Width, Is.EqualTo(10));
            first.Style = null;
            Assert.That(first.Width, Is.NaN);
            Assert.That(second.Width, Is.EqualTo(10));
        }

        [Test]
        public void BindingAndInheritedConditionChangesReevaluateTriggers()
        {
            var source = new State();
            var control = new TriggerTestControl { Style = StyleWith(When(TriggerTestControl.ActiveProperty, true, 20)) };
            control.DataContext = source;
            control.SetBinding(TriggerTestControl.ActiveProperty, new Binding(nameof(State.Active)));
            source.Active = true;
            Assert.That(control.Width, Is.EqualTo(20));
            source.Active = false;
            Assert.That(control.Width, Is.NaN);

            control.ClearValue(FrameworkElement.DataContextProperty);
            control.Style = StyleWith(When(FrameworkElement.DataContextProperty, "on", 30));
            var parent = new Border { Child = control, DataContext = "on" };
            Assert.That(control.Width, Is.EqualTo(30));
            parent.DataContext = "off";
            Assert.That(control.Width, Is.NaN);
            parent.DataContext = "on";
            parent.Child = null;
            Assert.That(control.Width, Is.NaN);
        }

        [Test]
        public void OnlyChangedConditionIsComparedAndOnlyRelatedSettersAreUpdated()
        {
            var value = new CountedValue();
            var unrelated = When(FrameworkElement.DataContextProperty, value, 40);
            unrelated.Setters.Clear();
            unrelated.Setters.Add(new Setter(FrameworkElement.HeightProperty, 40.0));
            var actual = new CountedValue();
            var control = new TriggerTestControl { DataContext = actual,
                Style = StyleWith(When(TriggerTestControl.ActiveProperty, true, 20), unrelated) };
            Assert.That(actual.Comparisons, Is.GreaterThan(0));
            actual.Comparisons = 0;
            control.Changes.Clear();
            control.Active = true;
            Assert.That(actual.Comparisons, Is.Zero);
            Assert.That(control.WidthChanges, Is.EqualTo(1));
            Assert.That(control.Changes.Exists(change => change.Property == FrameworkElement.HeightProperty), Is.False);
        }

        [Test]
        public void ConditionChangesCanActivateAnotherTrigger()
        {
            var first = new Trigger { Property = TriggerTestControl.ActiveProperty, Value = true };
            first.Setters.Add(new Setter(TriggerTestControl.OtherProperty, true));
            var control = new TriggerTestControl { Style = StyleWith(first, When(TriggerTestControl.OtherProperty, true, 20)) };
            control.Active = true;
            Assert.That(control.Width, Is.EqualTo(20));
            control.Active = false;
            Assert.That(control.Width, Is.NaN);
        }

        [Test]
        public void EqualSetterObjectsStillUseTheLastActiveTriggersActualValue()
        {
            var firstValue = new CountedValue();
            var lastValue = new CountedValue();
            var first = new Trigger { Property = TriggerTestControl.ActiveProperty, Value = true };
            first.Setters.Add(new Setter(FrameworkElement.DataContextProperty, firstValue));
            var last = new Trigger { Property = TriggerTestControl.OtherProperty, Value = true };
            last.Setters.Add(new Setter(FrameworkElement.DataContextProperty, lastValue));
            var control = new TriggerTestControl { Active = true, Style = StyleWith(first, last) };
            Assert.That(control.DataContext, Is.SameAs(firstValue));
            control.Other = true;
            Assert.That(control.DataContext, Is.SameAs(lastValue));
            control.Other = false;
            Assert.That(control.DataContext, Is.SameAs(firstValue));
        }

        [Test]
        public void TemplateTriggersOverrideTemplateBindingAndUseSeparateControllerLayer()
        {
            var template = Template("<ControlTemplate.Triggers><Trigger Property='Active' Value='True'>" +
                "<Setter TargetName='part' Property='Width' Value='40'/><Setter Property='Height' Value='50'/>" +
                "</Trigger></ControlTemplate.Triggers><Border x:Name='part' Width='{TemplateBinding Width}'/>");
            var control = new TriggerTestControl { Template = template, Width = 20 };
            control.ApplyTemplate();
            var part = (Border)control.GetTemplateChild("part");
            Assert.That(part.Width, Is.EqualTo(20));
            control.Active = true;
            Assert.That(part.Width, Is.EqualTo(40));
            Assert.That(control.Height, Is.EqualTo(50));
            control.Width = 25;
            Assert.That(part.Width, Is.EqualTo(40));
            control.Active = false;
            Assert.That(part.Width, Is.EqualTo(25));
            Assert.That(control.Height, Is.NaN);
            var styleTrigger = new Trigger { Property = TriggerTestControl.ActiveProperty, Value = true };
            styleTrigger.Setters.Add(new Setter(FrameworkElement.HeightProperty, 60.0));
            control.Style = StyleWith(styleTrigger);
            control.Active = true;
            Assert.That(control.Height, Is.EqualTo(60));
            control.Style = null;
            Assert.That(control.Height, Is.EqualTo(50));
        }

        [Test]
        public void TemplateReplacementAndRemovalClearBothLayersAndDetachConditions()
        {
            var template = Template("<Border x:Name='part' Width='{TemplateBinding Width}'/>" +
                "<ControlTemplate.Triggers><Trigger Property='Active' Value='True'>" +
                "<Setter TargetName='part' Property='Width' Value='40'/><Setter Property='Height' Value='50'/>" +
                "</Trigger></ControlTemplate.Triggers>");
            var control = new TriggerTestControl { Template = template, Width = 20, Active = true };
            var second = new TriggerTestControl { Template = template, Width = 10, Active = true };
            control.ApplyTemplate();
            second.ApplyTemplate();
            var old = (Border)control.GetTemplateChild("part");
            control.Template = Template("<Border x:Name='part'/>");
            Assert.That(old.Width, Is.EqualTo(20));
            Assert.That(control.Height, Is.NaN);
            control.ApplyTemplate();
            control.Active = false;
            control.Active = true;
            Assert.That(old.Width, Is.EqualTo(20));
            Assert.That(control.Height, Is.NaN);
            Assert.That(second.Height, Is.EqualTo(50));
            second.Template = null;
            Assert.That(second.Height, Is.NaN);
        }

        [Test]
        public void TemplateTriggerOrderAndChildLocalValuesPreserveUnderlyingValues()
        {
            var control = new TriggerTestControl { Active = true,
                Template = Template("<Border x:Name='part' Width='5'/><ControlTemplate.Triggers>" +
                    "<Trigger Property='Active' Value='True'><Setter TargetName='part' Property='Width' Value='10'/></Trigger>" +
                    "<Trigger Property='Other' Value='True'><Setter TargetName='part' Property='Width' Value='20'/></Trigger>" +
                    "</ControlTemplate.Triggers>") };
            control.ApplyTemplate();
            var part = (Border)control.GetTemplateChild("part");
            Assert.That(part.Width, Is.EqualTo(10));
            control.Other = true;
            Assert.That(part.Width, Is.EqualTo(20));
            part.Width = 30;
            control.Other = false;
            Assert.That(part.Width, Is.EqualTo(30));
            part.ClearValue(FrameworkElement.WidthProperty);
            Assert.That(part.Width, Is.EqualTo(10));
            control.Active = false;
            Assert.That(part.Width, Is.EqualTo(5));
        }

        [Test]
        public void ThemeTriggersLoseToApplicationSettersAndSurviveExplicitStyleReplacement()
        {
            var theme = StyleWith(When(TriggerTestControl.ActiveProperty, true, 20));
            theme.Setters.Add(new Setter(FrameworkElement.WidthProperty, 10.0));
            var application = new Style(typeof(TriggerTestControl));
            application.Setters.Add(new Setter(FrameworkElement.WidthProperty, 30.0));
            var control = new TriggerTestControl { Active = true };
            control.DefaultStyleKey = typeof(TriggerTestControl);
            var root = new UIRoot(control, null, default, new ResourceDictionary { [typeof(TriggerTestControl)] = application },
                new ResourceDictionary { [typeof(TriggerTestControl)] = theme });
            Assert.That(control.Width, Is.EqualTo(30));
            control.Style = null;
            Assert.That(control.Width, Is.EqualTo(20));
            control.Active = false;
            Assert.That(control.Width, Is.EqualTo(10));
            GC.KeepAlive(root);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void SealFreezesTriggersSettersAndAllCollectionMutations(bool template)
        {
            var trigger = When(TriggerTestControl.ActiveProperty, true, 20);
            var style = StyleWith(trigger);
            var controlTemplate = new ControlTemplate();
            controlTemplate.Triggers.Add(trigger);
            TriggerCollection collection = template ? controlTemplate.Triggers : style.Triggers;
            if (template) controlTemplate.Seal(); else style.Seal();
            Assert.That(trigger.IsSealed, Is.True);
            Assert.That(trigger.Setters[0].IsSealed, Is.True);
            Assert.Throws<InvalidOperationException>(() => trigger.Property = FrameworkElement.WidthProperty);
            Assert.Throws<InvalidOperationException>(() => trigger.Value = false);
            Assert.Throws<InvalidOperationException>(() => trigger.Setters.Clear());
            Assert.Throws<InvalidOperationException>(() => ((Setter)trigger.Setters[0]).TargetName = "part");
            Assert.Throws<InvalidOperationException>(() => collection.Add(new Trigger()));
            Assert.Throws<InvalidOperationException>(() => collection[0] = new Trigger());
            Assert.Throws<InvalidOperationException>(() => collection.RemoveAt(0));
            Assert.Throws<InvalidOperationException>(() => collection.Clear());
        }

        [Test]
        public void TriggerCollectionRejectsNull()
        {
            var collection = new TriggerCollection();
            Assert.Throws<ArgumentNullException>(() => collection.Add(null));
            collection.Add(new Trigger());
            Assert.Throws<ArgumentNullException>(() => collection[0] = null);
        }

        [Test]
        public void SealValidatesConditionAndSetterValues()
        {
            Assert.That(Assert.Throws<InvalidOperationException>(() => StyleWith(new Trigger()).Seal()).Message,
                Is.EqualTo("Trigger.Property must not be null."));
            var trigger = new Trigger { Property = TriggerTestControl.ActiveProperty, Value = "bad" };
            Assert.That(Assert.Throws<InvalidOperationException>(() => StyleWith(trigger).Seal()).Message,
                Is.EqualTo("'bad' is not a valid value for the 'Active' property on a Trigger."));
            trigger.Value = true;
            trigger.Setters.Add(new Setter(FrameworkElement.WidthProperty, "bad"));
            Assert.That(Assert.Throws<InvalidOperationException>(() => StyleWith(trigger).Seal()).Message,
                Is.EqualTo("'bad' is not a valid value for the 'Width' property on a Setter."));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void StyleRejectsTargetNameInNormalAndTriggerSetters(bool inTrigger)
        {
            var style = new Style(typeof(Control));
            var setter = new Setter(FrameworkElement.WidthProperty, 20.0, "part");
            if (inTrigger)
            {
                var trigger = new Trigger { Property = FrameworkElement.WidthProperty, Value = 10.0 };
                trigger.Setters.Add(setter);
                style.Triggers.Add(trigger);
            }
            else style.Setters.Add(setter);
            Assert.That(Assert.Throws<InvalidOperationException>(style.Seal).Message,
                Is.EqualTo("'TargetName' property cannot be set on a Style Setter."));
        }

        [Test]
        public void CodeTemplateRejectsMissingTargetWhenApplied()
        {
            var template = new ControlTemplate();
            var trigger = When(TriggerTestControl.ActiveProperty, true, 20);
            ((Setter)trigger.Setters[0]).TargetName = "missing";
            template.Triggers.Add(trigger);
            var control = new TriggerTestControl { Template = template };
            Assert.That(Assert.Throws<InvalidOperationException>(() => control.ApplyTemplate()).Message,
                Is.EqualTo("Cannot find the Trigger target 'missing'."));
        }

        [Test]
        public void XamlStyleUsesTargetTypeRegardlessOfAttributeOrderAndStaticResources()
        {
            var resources = new ResourceDictionary { ["on"] = true, ["size"] = 42.0 };
            var control = Build<TriggerTestControl>("<t:TriggerTestControl " + Namespaces + ">" +
                "<Control.Style><Style TargetType='{x:Type t:TriggerTestControl}'><Style.Triggers>" +
                "<Trigger Value='{StaticResource on}' Property='Active'>" +
                "<Setter Value='{StaticResource size}' Property='Width'/></Trigger>" +
                "</Style.Triggers></Style></Control.Style></t:TriggerTestControl>", resources);
            Assert.That(control.Width, Is.NaN);
            control.Active = true;
            Assert.That(control.Width, Is.EqualTo(42));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void TemplateTargetNameResolvesChildTypeBeforeOrAfterContent(bool triggersFirst)
        {
            const string content = "<TextBlock x:Name='label'/>";
            const string triggers = "<ControlTemplate.Triggers><Trigger Value='True' Property='Active'>" +
                "<Setter Property='Text' Value='active' TargetName='label'/></Trigger></ControlTemplate.Triggers>";
            var control = new TriggerTestControl { Active = true,
                Template = Template(triggersFirst ? triggers + content : content + triggers) };
            control.ApplyTemplate();
            Assert.That(((TextBlock)control.GetTemplateChild("label")).Text, Is.EqualTo("active"));
        }

        [Test]
        public void TemplateWithoutTargetTypeDefaultsToControlAndSupportsQualifiedProperties()
        {
            var control = Build<Control>("<Control " + Namespaces + " Width='20'><Control.Template><ControlTemplate>" +
                "<ControlTemplate.Triggers><Trigger Property='Width' Value='20'>" +
                "<Setter Property='Control.Height' Value='30'/></Trigger></ControlTemplate.Triggers>" +
                "<Border/></ControlTemplate></Control.Template></Control>");
            control.ApplyTemplate();
            Assert.That(control.Height, Is.EqualTo(30));
        }

        [Test]
        public void ResourceTargetTypeAndBasedOnTriggersBuildWithTheSameResolutionRules()
        {
            var resources = Build<ResourceDictionary>("<ResourceDictionary " + Namespaces + ">" +
                "<Style x:Key='base' TargetType='{StaticResource type}'><Style.Triggers>" +
                "<Trigger Property='Active' Value='True'><Setter Property='Width' Value='10'/></Trigger>" +
                "</Style.Triggers></Style><Style x:Key='derived' TargetType='{StaticResource type}' BasedOn='{StaticResource base}'>" +
                "<Style.Triggers><Trigger Property='Active' Value='True'><Setter Property='Width' Value='20'/></Trigger>" +
                "</Style.Triggers></Style><ControlTemplate x:Key='template' TargetType='{StaticResource type}'>" +
                "<ControlTemplate.Triggers><Trigger Property='Active' Value='True'>" +
                "<Setter Property='Height' Value='30'/></Trigger></ControlTemplate.Triggers><Border/>" +
                "</ControlTemplate></ResourceDictionary>", new ResourceDictionary { ["type"] = typeof(TriggerTestControl) });
            var control = new TriggerTestControl { Active = true, Style = (Style)resources["derived"],
                Template = (ControlTemplate)resources["template"] };
            control.ApplyTemplate();
            Assert.That(control.Width, Is.EqualTo(20));
            Assert.That(control.Height, Is.EqualTo(30));
        }

        [TestCase("\n <Trigger Value='True'/>", "Trigger requires a Property.")]
        [TestCase("\n <Trigger Property='Active'/>", "Trigger requires a Value.")]
        [TestCase("<Trigger\n  Property='Missing' Value='True'/>", "Cannot resolve the Trigger Property 'Missing'. Verify that the owning type is the Style's TargetType, or use Class.Property syntax to specify the Property.")]
        [TestCase("<Trigger Property='Active'\n  Value='bad'/>", "Property 'Value': 'bad' is not a valid Boolean value.")]
        [TestCase("<Trigger Property='Active' Value='True'><Setter Property='Width' Value='10'\n  TargetName='part'/></Trigger>", "'TargetName' property cannot be set on a Style Setter.")]
        [TestCase("<Trigger Property='Active' Value='True'\n  SourceName='part'/>", "Unknown or read-only property 'SourceName' on type 'Trigger'.")]
        public void InvalidStyleTriggersReportExactMessageAndLocation(string trigger, string message)
        {
            AssertError("<t:TriggerTestControl " + Namespaces + "><Control.Style><Style TargetType='t:TriggerTestControl'>" +
                "<Style.Triggers>" + trigger + "</Style.Triggers></Style></Control.Style></t:TriggerTestControl>", message);
        }

        [TestCase("missing", "Cannot find the Trigger target 'missing'.")]
        [TestCase("inner", "Cannot find the Trigger target 'inner'.")]
        public void MissingAndNestedTemplateNamesReportSetterLocation(string target, string message)
        {
            AssertError(TemplateXaml("<ControlTemplate.Triggers><Trigger Property='Active' Value='True'>" +
                "<Setter Property='Width' Value='10'\n  TargetName='" + target + "'/></Trigger></ControlTemplate.Triggers>" +
                "<Control><Control.Template><ControlTemplate><Border x:Name='inner'/></ControlTemplate>" +
                "</Control.Template></Control>"), message);
        }

        [TestCase("\n <Trigger Value='True'/>", "Trigger requires a Property.")]
        [TestCase("\n <Trigger Property='Active'/>", "Trigger requires a Value.")]
        [TestCase("<Trigger\n  Property='Missing' Value='True'/>", "Cannot resolve the Trigger Property 'Missing'. Verify that the owning type is the Style's TargetType, or use Class.Property syntax to specify the Property.")]
        [TestCase("<Trigger Property='Active'\n  Value='bad'/>", "Property 'Value': 'bad' is not a valid Boolean value.")]
        [TestCase("<Trigger Property='Active' Value='True'><Setter TargetName='part'\n  Property='Missing' Value='10'/></Trigger>", "Cannot resolve the Style Property 'Missing'. Verify that the owning type is the Style's TargetType, or use Class.Property syntax to specify the Property.")]
        [TestCase("<Trigger Property='Active' Value='True'><Setter TargetName='part' Property='Width'\n  Value='bad'/></Trigger>", "Property 'Value': 'bad' is not a valid Double value.")]
        public void InvalidTemplateTriggersReportExactMessageAndLocation(string trigger, string message)
        {
            AssertError(TemplateXaml("<ControlTemplate.Triggers>" + trigger +
                "</ControlTemplate.Triggers><Border x:Name='part'/>"), message);
        }

        [Test]
        public void UnknownObjectValuesReturnParseErrorsWithoutThrowing()
        {
            var result = XamlParser.Parse(TemplateXaml("<ControlTemplate.Triggers>" +
                "<Trigger Property='Active'><Trigger.Value><Unknown/></Trigger.Value></Trigger>" +
                "</ControlTemplate.Triggers><Border/>"), "triggers.xaml");
            Assert.That(result.Document, Is.Null);
            Assert.That(result.Errors.Count, Is.EqualTo(1));
            Assert.That(result.Errors[0].Message, Is.EqualTo("Unknown element 'Unknown'."));
        }

        [Test]
        public void StyleRejectsResourceTargetNameDuringParsing()
        {
            AssertError("<Control " + Namespaces + "><Control.Style><Style TargetType='Control'>" +
                "<Setter Property='Width' Value='10'\n  TargetName='{StaticResource name}'/></Style></Control.Style></Control>",
                "'TargetName' property cannot be set on a Style Setter.");
        }

        [Test]
        public void StyleSetterTargetNameReportsAttributeLocation()
        {
            AssertError("<Control " + Namespaces + "><Control.Style><Style TargetType='Control'>" +
                "<Setter Property='Width' Value='10'\n  TargetName='part'/></Style></Control.Style></Control>",
                "'TargetName' property cannot be set on a Style Setter.");
        }

        [Test]
        public void StaticResourceConditionIsValidatedAtBuildTimeWithValueLocation()
        {
            string xaml = "<t:TriggerTestControl " + Namespaces + "><Control.Style><Style TargetType='t:TriggerTestControl'>" +
                "<Style.Triggers><Trigger Property='Active'\n  Value='{StaticResource bad}'/>" +
                "</Style.Triggers></Style></Control.Style></t:TriggerTestControl>";
            var error = Assert.Throws<XamlBuildException>(() => Build<Control>(xaml, new ResourceDictionary { ["bad"] = "wrong" }));
            Assert.That((error.Line, error.Column), Is.EqualTo((2, 3)));
            Assert.That(error.Message, Does.Contain("'wrong' is not a valid value for the 'Active' property on a Trigger."));
        }

        [Test]
        public void UnchangedLayoutAndRenderWithActiveTriggersAllocateNothing()
        {
            var control = new TriggerTestControl { Active = true,
                Style = StyleWith(When(TriggerTestControl.ActiveProperty, true, 20)),
                Template = Template("<Border x:Name='part'/><ControlTemplate.Triggers>" +
                    "<Trigger Property='Active' Value='True'><Setter TargetName='part' Property='Width' Value='15'/>" +
                    "</Trigger></ControlTemplate.Triggers>") };
            var root = new UIRoot(control);
            var drawing = new EmptyDrawingContext();
            var viewport = new Size(100, 100);
            for (int i = 0; i < 100; i++) { root.UpdateLayout(viewport); root.Render(drawing); }
            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 100; i++) { root.UpdateLayout(viewport); root.Render(drawing); }
            Assert.That(GC.GetAllocatedBytesForCurrentThread() - before, Is.Zero);
        }

        private static Trigger When(DependencyProperty property, object value, double width)
        {
            var trigger = new Trigger { Property = property, Value = value };
            trigger.Setters.Add(new Setter(FrameworkElement.WidthProperty, width));
            return trigger;
        }

        private static Style StyleWith(params Trigger[] triggers)
        {
            var style = new Style(typeof(TriggerTestControl));
            foreach (Trigger trigger in triggers) style.Triggers.Add(trigger);
            return style;
        }

        private static string TemplateXaml(string content) => "<t:TriggerTestControl " + Namespaces + ">" +
            "<Control.Template><ControlTemplate TargetType='t:TriggerTestControl'>" + content +
            "</ControlTemplate></Control.Template></t:TriggerTestControl>";

        private static ControlTemplate Template(string content) => Build<Control>(TemplateXaml(content)).Template;

        private static T Build<T>(string xaml, ResourceDictionary resources = null)
        {
            var parsed = XamlParser.Parse(xaml, "triggers.xaml");
            Assert.That(parsed.Errors, Is.Empty, string.Join("\n", parsed.Errors));
            var document = XamlIrSerializer.Deserialize(XamlIrSerializer.Serialize(parsed.Document));
            return (T)XamlBuilder.Build(document, null, resources, null);
        }

        private static void AssertError(string xaml, string message)
        {
            var result = XamlParser.Parse(xaml, "triggers.xaml");
            Assert.That(result.Document, Is.Null);
            Assert.That(result.Errors.Count, Is.EqualTo(1), string.Join("\n", result.Errors));
            Assert.That(result.Errors[0].Message, Is.EqualTo(message));
            Assert.That(result.Errors[0].File, Is.EqualTo("triggers.xaml"));
            Assert.That((result.Errors[0].Line, result.Errors[0].Column), Is.EqualTo((2, 3)));
        }

        private sealed class CountedValue
        {
            internal int Comparisons;
            public override bool Equals(object obj) { Comparisons++; return obj is CountedValue; }
            public override int GetHashCode() => 0;
        }

        private sealed class State : INotifyPropertyChanged
        {
            private bool active;
            public bool Active
            {
                get => active;
                set { active = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Active))); }
            }
            public event PropertyChangedEventHandler PropertyChanged;
        }
    }

    public class TriggerTestControl : Control
    {
        public static readonly DependencyProperty ActiveProperty = DependencyProperty.Register(
            nameof(Active), typeof(bool), typeof(TriggerTestControl), new PropertyMetadata(false));
        public static readonly DependencyProperty OtherProperty = DependencyProperty.Register(
            nameof(Other), typeof(bool), typeof(TriggerTestControl), new PropertyMetadata(false));
        public bool Active { get => (bool)GetValue(ActiveProperty); set => SetValue(ActiveProperty, value); }
        public bool Other { get => (bool)GetValue(OtherProperty); set => SetValue(OtherProperty, value); }
        public readonly List<DependencyPropertyChangedEventArgs> Changes = new List<DependencyPropertyChangedEventArgs>();
        public int WidthChanges => Changes.FindAll(change => change.Property == WidthProperty).Count;
        protected override void OnPropertyChanged(DependencyPropertyChangedEventArgs e)
        {
            base.OnPropertyChanged(e);
            Changes.Add(e);
        }
    }
}
