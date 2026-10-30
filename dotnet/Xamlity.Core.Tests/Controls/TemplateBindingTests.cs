using System;
using System.Linq;
using NUnit.Framework;
using Xamlity.Controls;
using Xamlity.Core.Tests.Rendering;
using Xamlity.Layout;
using Xamlity.Markup;
using Xamlity.Properties;
using Xamlity.Rendering;
using Xamlity.Styling;
using Xamlity.Tree;

namespace Xamlity.Core.Tests.Controls
{
    public class TemplateBindingTests
    {
        private const string Namespaces = "xmlns='https://github.com/loonacia/xamlity' " +
            "xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml' " +
            "xmlns:t='clr-namespace:Xamlity.Core.Tests.Controls;assembly=Xamlity.Core.Tests'";
        private const string BorderContent = "<Border x:Name='part' Background='{TemplateBinding Background}' " +
            "BorderBrush='{TemplateBinding Property=BorderBrush}' BorderThickness='{TemplateBinding BorderThickness}' " +
            "Padding='{TemplateBinding Padding}'/>";

        [Test]
        public void InitialAndLocalValuesPropagateAndInvalidateRenderingAndMeasure()
        {
            var red = new SolidColorBrush(Color.FromRgb(255, 0, 0));
            var blue = new SolidColorBrush(Color.FromRgb(0, 0, 255));
            var control = new Control { Template = Template(BorderContent), Background = red, BorderBrush = blue,
                BorderThickness = new Thickness(2), Padding = new Thickness(3) };
            var root = new UIRoot(control);
            var drawing = new EmptyDrawingContext();
            root.UpdateLayout(new Size(200, 100));
            root.Render(drawing);
            var border = (Border)control.GetTemplateChild("part");
            Assert.That(border.Background, Is.SameAs(red));
            Assert.That(border.BorderBrush, Is.SameAs(blue));
            Assert.That(border.BorderThickness, Is.EqualTo(new Thickness(2)));
            Assert.That(border.Padding, Is.EqualTo(new Thickness(3)));
            foreach (var dp in new[] { Border.BackgroundProperty, Border.BorderBrushProperty,
                Border.BorderThicknessProperty, Border.PaddingProperty })
                Assert.That(border.ReadLocalValue(dp), Is.SameAs(DependencyProperty.UnsetValue));

            control.Background = blue;
            control.BorderBrush = red;
            Assert.That(border.Background, Is.SameAs(blue));
            Assert.That(border.BorderBrush, Is.SameAs(red));
            Assert.That(root.IsRenderDirty, Is.True);
            Assert.That(border.IsMeasureValid, Is.True);
            control.Padding = new Thickness(5);
            Assert.That(border.Padding, Is.EqualTo(new Thickness(5)));
            Assert.That(border.IsMeasureValid, Is.False);
            root.UpdateLayout(new Size(200, 100));
            control.BorderThickness = new Thickness(4);
            Assert.That(border.BorderThickness, Is.EqualTo(new Thickness(4)));
            Assert.That(border.IsMeasureValid, Is.False);
            root.UpdateLayout(new Size(200, 100));
            Assert.That(border.DesiredSize, Is.EqualTo(new Size(18, 18)));
        }

        [Test]
        public void StyleAndTriggerChangesPropagateOnlyWhenEffectiveSourceChanges()
        {
            var control = new Control { Template = Template(BorderContent) };
            var first = new Style(typeof(Control));
            first.Setters.Add(new Setter(Control.BackgroundProperty, new SolidColorBrush(Color.FromRgb(255, 0, 0))));
            first.Setters.Add(new Setter(Control.BorderBrushProperty, new SolidColorBrush(Color.FromRgb(0, 0, 255))));
            first.Setters.Add(new Setter(Control.BorderThicknessProperty, new Thickness(2)));
            first.Setters.Add(new Setter(Control.PaddingProperty, new Thickness(3)));
            control.Style = first;
            control.ApplyTemplate();
            var border = (Border)control.GetTemplateChild("part");
            Assert.That(border.Background, Is.SameAs(control.Background));
            Assert.That(border.BorderBrush, Is.SameAs(control.BorderBrush));
            Assert.That(border.BorderThickness, Is.EqualTo(new Thickness(2)));
            Assert.That(border.Padding, Is.EqualTo(new Thickness(3)));
            var second = new Style(typeof(Control));
            second.Setters.Add(new Setter(Control.PaddingProperty, new Thickness(7)));
            control.Style = second;
            Assert.That(border.Padding, Is.EqualTo(new Thickness(7)));
            Assert.That(border.Background, Is.Null);
            control.SetLayerValue(Control.PaddingProperty, ValueLayer.StyleTrigger, new Thickness(9));
            Assert.That(border.Padding, Is.EqualTo(new Thickness(9)));
            control.Padding = new Thickness(11);
            control.ClearLayerValue(Control.PaddingProperty, ValueLayer.StyleTrigger);
            Assert.That(border.Padding, Is.EqualTo(new Thickness(11)));
            control.ClearValue(Control.PaddingProperty);
            Assert.That(border.Padding, Is.EqualTo(new Thickness(7)));
        }

        [Test]
        public void InheritedSourceUpdatesASeparateTargetPropertyIncludingReparenting()
        {
            var control = new Control { Template = Template("<Border x:Name='part' Background='{TemplateBinding Foreground}'/>") };
            control.ApplyTemplate();
            var border = (Border)control.GetTemplateChild("part");
            var parent = new Border();
            var red = new SolidColorBrush(Color.FromRgb(255, 0, 0));
            var blue = new SolidColorBrush(Color.FromRgb(0, 0, 255));
            parent.SetValue(Control.ForegroundProperty, red);
            parent.Child = control;
            Assert.That(border.Background, Is.SameAs(red));
            parent.SetValue(Control.ForegroundProperty, blue);
            Assert.That(border.Background, Is.SameAs(blue));
            parent.Child = null;
            Assert.That(border.Background, Is.SameAs(control.Foreground));
            Assert.That(border.Background, Is.Not.SameAs(blue));
        }

        [Test]
        public void ParentTemplateTriggerAndLocalValuesOverrideBindingWithoutLosingUpdates()
        {
            var control = new Control { Template = Template(BorderContent), Padding = new Thickness(2) };
            control.ApplyTemplate();
            var border = (Border)control.GetTemplateChild("part");
            border.SetLayerValue(Border.PaddingProperty, ValueLayer.ParentTemplateTrigger, new Thickness(9));
            control.Padding = new Thickness(4);
            Assert.That(border.Padding, Is.EqualTo(new Thickness(9)));
            border.ClearLayerValue(Border.PaddingProperty, ValueLayer.ParentTemplateTrigger);
            Assert.That(border.Padding, Is.EqualTo(new Thickness(4)));
            border.Padding = new Thickness(12);
            control.Padding = new Thickness(6);
            Assert.That(border.Padding, Is.EqualTo(new Thickness(12)));
            border.ClearValue(Border.PaddingProperty);
            Assert.That(border.Padding, Is.EqualTo(new Thickness(6)));
        }

        [Test]
        public void ReplacementDetachesOldBindingsAndSharedTemplateInstancesStayIndependent()
        {
            var template = Template(BorderContent);
            var first = new Control { Template = template, Padding = new Thickness(2) };
            var second = new Control { Template = template, Padding = new Thickness(3) };
            first.ApplyTemplate();
            second.ApplyTemplate();
            var old = (Border)first.GetTemplateChild("part");
            first.Template = Template(BorderContent);
            first.Padding = new Thickness(4);
            first.ApplyTemplate();
            var current = (Border)first.GetTemplateChild("part");
            first.Padding = new Thickness(5);
            Assert.That(old.Padding, Is.EqualTo(new Thickness(2)));
            Assert.That(current.Padding, Is.EqualTo(new Thickness(5)));
            Assert.That(((Border)second.GetTemplateChild("part")).Padding, Is.EqualTo(new Thickness(3)));
            first.Template = null;
            first.Padding = new Thickness(6);
            Assert.That(current.Padding, Is.EqualTo(new Thickness(5)));
        }

        [Test]
        public void HorizontalContentAlignmentControlsTemplateChildCoordinates()
        {
            var control = new Control { Template = Template("<Border x:Name='part' Width='20' Height='10' " +
                "HorizontalAlignment='{TemplateBinding HorizontalContentAlignment}'/>") };
            var root = new UIRoot(control);
            root.UpdateLayout(new Size(100, 50));
            var border = (Border)control.GetTemplateChild("part");
            Assert.That(border.VisualOffset.X, Is.Zero);
            control.HorizontalContentAlignment = HorizontalAlignment.Center;
            root.UpdateLayout(new Size(100, 50));
            Assert.That(border.VisualOffset.X, Is.EqualTo(40));
            control.HorizontalContentAlignment = HorizontalAlignment.Right;
            root.UpdateLayout(new Size(100, 50));
            Assert.That(border.VisualOffset.X, Is.EqualTo(80));
        }

        [TestCase("{TemplateBinding Background}")]
        [TestCase("{TemplateBinding Property=Background}")]
        [TestCase("{TemplateBinding Control.Background}")]
        [TestCase("{ TemplateBinding Property = 'Control.Background' }")]
        public void ExtensionIrPreservesPropertyAndLocationsThroughSerialization(string extension)
        {
            var document = Parse(ControlXaml("<Border\n  Background=\"" + extension + "\"/>"));
            var property = document.Root.Properties.Single().Objects.Single().Properties.Single().Objects.Single().Properties.Single();
            var node = property.Objects.Single();
            Assert.That(property.TextValue, Is.Null);
            Assert.That(node.TypeName, Is.EqualTo("Xamlity.Markup.TemplateBindingExtension, Xamlity.Core"));
            Assert.That(node.Properties.Single().PropertyName, Is.EqualTo("Property"));
            Assert.That(node.Properties.Single().TextValue, Is.EqualTo(extension.Contains("Control.") ? "Control.Background" : "Background"));
            Assert.That((property.Line, property.Column), Is.EqualTo((2, 3)));
            Assert.That((node.Line, node.Column), Is.EqualTo((2, 3)));
            Assert.That((node.Properties[0].Line, node.Properties[0].Column), Is.EqualTo((2, 3)));
            var control = (Control)XamlBuilder.Build(document);
            control.Background = new SolidColorBrush(Color.FromRgb(255, 0, 0));
            Assert.That(control.ApplyTemplate(), Is.True);
        }

        [Test]
        public void NearestTargetTypeAndDefaultControlAreUsedInNestedTemplates()
        {
            var control = Build<TemplateBindingTestControl>("<t:TemplateBindingTestControl " + Namespaces + " Caption='outer'>" +
                "<Control.Template><ControlTemplate TargetType='{x:Type t:TemplateBindingTestControl}'>" +
                "<StackPanel><TextBlock x:Name='label' Text='{TemplateBinding Caption}'/>" +
                "<Control x:Name='inner' Background='Red'><Control.Template><ControlTemplate>" +
                "<Border x:Name='part' Background='{TemplateBinding Background}'/>" +
                "</ControlTemplate></Control.Template></Control></StackPanel>" +
                "</ControlTemplate></Control.Template></t:TemplateBindingTestControl>");
            control.ApplyTemplate();
            Assert.That(((TextBlock)control.GetTemplateChild("label")).Text, Is.EqualTo("outer"));
            control.Caption = "changed";
            Assert.That(((TextBlock)control.GetTemplateChild("label")).Text, Is.EqualTo("changed"));
            var inner = (Control)control.GetTemplateChild("inner");
            inner.ApplyTemplate();
            Assert.That(((Border)inner.GetTemplateChild("part")).Background, Is.SameAs(inner.Background));
            control.Background = new SolidColorBrush(Color.FromRgb(0, 0, 255));
            Assert.That(((Border)inner.GetTemplateChild("part")).Background, Is.SameAs(inner.Background));
        }

        [Test]
        public void DependencyObjectTargetsAndAttachedPropertiesAreSupported()
        {
            var control = new Control { Width = 12, Template = Template("<Grid><Grid.ColumnDefinitions>" +
                "<ColumnDefinition MinWidth='{TemplateBinding Width}'/></Grid.ColumnDefinitions>" +
                "<Border x:Name='part' Canvas.Left='{TemplateBinding Canvas.Left}'/></Grid>") };
            Canvas.SetLeft(control, 7);
            control.ApplyTemplate();
            var border = (Border)control.GetTemplateChild("part");
            var grid = (Grid)border.VisualParent;
            Assert.That(grid.ColumnDefinitions[0].MinWidth, Is.EqualTo(12));
            Assert.That(Canvas.GetLeft(border), Is.EqualTo(7));
            control.Width = 20;
            Canvas.SetLeft(control, 9);
            Assert.That(grid.ColumnDefinitions[0].MinWidth, Is.EqualTo(20));
            Assert.That(Canvas.GetLeft(border), Is.EqualTo(9));
        }

        [Test]
        public void SourceTypeMayBeAssignableToAnObjectTarget()
        {
            var control = new Control { Template = Template("<Border x:Name='part' DataContext='{TemplateBinding Width}'/>"),
                Width = 12 };
            control.ApplyTemplate();
            var border = (Border)control.GetTemplateChild("part");
            Assert.That(border.DataContext, Is.EqualTo(12));
            control.Width = 20;
            Assert.That(border.DataContext, Is.EqualTo(20));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void ResourceTargetTypeResolvesAtApplicationAndKeepsErrorLocation(bool invalid)
        {
            var document = Parse("<ResourceDictionary " + Namespaces + ">" +
                "<ControlTemplate x:Key='template' TargetType='{StaticResource target}'>" +
                "<TextBlock x:Name='part'\n  Text='{TemplateBinding " + (invalid ? "Missing" : "Caption") + "}'/>" +
                "</ControlTemplate></ResourceDictionary>");
            var resources = (ResourceDictionary)XamlBuilder.Build(document, null,
                new ResourceDictionary { ["target"] = typeof(TemplateBindingTestControl) }, null);
            var control = new TemplateBindingTestControl { Template = (ControlTemplate)resources["template"], Caption = "initial" };
            if (invalid)
            {
                var error = Assert.Throws<XamlBuildException>(() => control.ApplyTemplate());
                Assert.That((error.Line, error.Column), Is.EqualTo((2, 3)));
                Assert.That(error.Message, Is.EqualTo("(2,3): Cannot set property 'Text': " +
                    "Cannot resolve the TemplateBinding Property 'Missing' on type 'TemplateBindingTestControl'."));
                return;
            }
            control.ApplyTemplate();
            var text = (TextBlock)control.GetTemplateChild("part");
            Assert.That(text.Text, Is.EqualTo("initial"));
            control.Caption = "changed";
            Assert.That(text.Text, Is.EqualTo("changed"));
        }

        [TestCase("<Border\n  Background='{TemplateBinding Missing}'/>", "Cannot resolve the TemplateBinding Property 'Missing' on type 'Control'.")]
        [TestCase("<Border\n  Background='{TemplateBinding Unknown.Background}'/>", "Cannot resolve the TemplateBinding Property 'Unknown.Background' on type 'Control'.")]
        [TestCase("<Border\n  Background='{TemplateBinding t:TemplateBindingTestControl.Caption}'/>", "Cannot resolve the TemplateBinding Property 't:TemplateBindingTestControl.Caption' on type 'Control'.")]
        [TestCase("<Border\n  Background='{TemplateBinding Width}'/>", "TemplateBinding source property 'Width' of type 'Double' cannot be assigned to property 'Background' of type 'Brush'.")]
        [TestCase("<Border\n  Background='{TemplateBinding Background, Converter=convert}'/>", "Markup extension 'TemplateBinding' accepts only Property.")]
        [TestCase("<Border\n  Background='{TemplateBinding Converter=convert}'/>", "Markup extension 'TemplateBinding' accepts only Property.")]
        [TestCase("<Border\n  Background='{TemplateBinding}'/>", "Markup extension 'TemplateBinding' accepts only Property.")]
        [TestCase("<Border\n  Background='{TemplateBinding Background, Property=Background}'/>", "Markup extension 'TemplateBinding' accepts only Property.")]
        [TestCase("<Border\n  Background='{TemplateBinding Property={StaticResource prop}}'/>", "Nested markup extension 'StaticResource' is not supported here.")]
        [TestCase("<t:TemplateBindingTestControl\n  Label='{TemplateBinding Background}'/>", "TemplateBinding can only be set on a dependency property.")]
        [TestCase("<Border><Border.Background><SolidColorBrush\n  Color='{TemplateBinding Background}'/></Border.Background></Border>", "TemplateBinding can only be set on a dependency property.")]
        [TestCase("<Border><Border.Background><SolidColorBrush\n  Canvas.Left='{TemplateBinding Width}'/></Border.Background></Border>", "TemplateBinding can only be set on a dependency property.")]
        public void InvalidBindingsReportExactMessagesAndAttributeLocations(string content, string message)
        {
            AssertError(ControlXaml(content), message);
        }

        [Test]
        public void OutsideTemplateAndNestedTargetTypeErrorsUseNearestScope()
        {
            AssertError("<Control " + Namespaces + "\n  Background='{TemplateBinding Background}'/>",
                "TemplateBinding can only be used within a ControlTemplate.");
            AssertError(ControlXaml("<Control><Control.Template><ControlTemplate>" +
                "<TextBlock\n  Text='{TemplateBinding Caption}'/>" +
                "</ControlTemplate></Control.Template></Control>", "t:TemplateBindingTestControl"),
                "Cannot resolve the TemplateBinding Property 'Caption' on type 'Control'.");
            AssertError(ControlXaml("<TextBlock\n  Text='{TemplateBinding Missing}'/>", "t:TemplateBindingTestControl"),
                "Cannot resolve the TemplateBinding Property 'Missing' on type 'TemplateBindingTestControl'.");
        }

        [Test]
        public void UnchangedEffectiveValuesAndLayoutRenderAllocateNothing()
        {
            var brush = new SolidColorBrush(Color.FromRgb(255, 0, 0));
            var control = new Control { Template = Template(BorderContent), Background = brush,
                Padding = new Thickness(4) };
            var root = new UIRoot(control);
            var drawing = new EmptyDrawingContext();
            var viewport = new Size(200, 100);
            for (int i = 0; i < 100; i++) { root.UpdateLayout(viewport); root.Render(drawing); }
            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 100; i++) { root.UpdateLayout(viewport); root.Render(drawing); }
            Assert.That(GC.GetAllocatedBytesForCurrentThread() - before, Is.Zero);
            before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 100; i++) control.Background = brush;
            long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
            Assert.That(allocated, Is.Zero);
            Assert.That(root.IsRenderDirty, Is.False);
            Assert.That(((Border)control.GetTemplateChild("part")).IsMeasureValid, Is.True);
        }

        private static string ControlXaml(string content, string targetType = null) => "<Control " + Namespaces + ">" +
            "<Control.Template><ControlTemplate" + (targetType == null ? "" : " TargetType='" + targetType + "'") + ">" +
            content + "</ControlTemplate></Control.Template></Control>";

        private static ControlTemplate Template(string content) => Build<Control>(ControlXaml(content)).Template;

        private static T Build<T>(string xaml) => (T)XamlBuilder.Build(Parse(xaml));

        private static XamlDocument Parse(string xaml)
        {
            var result = XamlParser.Parse(xaml, "template-binding.xaml");
            Assert.That(result.Errors, Is.Empty, string.Join("\n", result.Errors));
            return XamlIrSerializer.Deserialize(XamlIrSerializer.Serialize(result.Document));
        }

        private static void AssertError(string xaml, string message)
        {
            var result = XamlParser.Parse(xaml, "template-binding.xaml");
            Assert.That(result.Document, Is.Null);
            Assert.That(result.Errors.Count, Is.EqualTo(1), string.Join("\n", result.Errors));
            Assert.That(result.Errors[0].Message, Is.EqualTo(message));
            Assert.That(result.Errors[0].File, Is.EqualTo("template-binding.xaml"));
            Assert.That((result.Errors[0].Line, result.Errors[0].Column), Is.EqualTo((2, 3)));
        }
    }

    public class TemplateBindingTestControl : Control
    {
        public static readonly DependencyProperty CaptionProperty = DependencyProperty.Register(
            nameof(Caption), typeof(string), typeof(TemplateBindingTestControl), new PropertyMetadata(""));
        public string Caption { get => (string)GetValue(CaptionProperty); set => SetValue(CaptionProperty, value); }
        public string Label { get; set; }
    }
}
