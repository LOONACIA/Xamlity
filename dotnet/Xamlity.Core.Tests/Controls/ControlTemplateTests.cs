using System;
using System.Linq;
using NUnit.Framework;
using Xamlity.Controls;
using Xamlity.Core.Tests.Rendering;
using Xamlity.Data;
using Xamlity.Layout;
using Xamlity.Markup;
using Xamlity.Properties;
using Xamlity.Rendering;
using Xamlity.Styling;
using Xamlity.Tree;

namespace Xamlity.Core.Tests.Controls
{
    public class ControlTemplateTests
    {
        private const string Namespaces = "xmlns='https://github.com/loonacia/xamlity' " +
            "xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml' " +
            "xmlns:t='clr-namespace:Xamlity.Core.Tests.Controls;assembly=Xamlity.Core.Tests'";

        [Test]
        public void TemplateIsDeferredAndOnApplyTemplateSeesAttachedNamedChildren()
        {
            var control = Build<TemplateTestControl>("<t:TemplateTestControl " + Namespaces + ">" +
                "<Control.Template><ControlTemplate TargetType='{x:Type Control}'>" +
                "<Border x:Name='part'><TextBlock x:Name='text'/></Border>" +
                "</ControlTemplate></Control.Template></t:TemplateTestControl>");
            Assert.That(control.GetTemplateChild("part"), Is.Null);
            Assert.That(control.ChildCount, Is.Zero);
            Assert.That(control.ApplyTemplate(), Is.True);
            Assert.That(control.ApplyTemplate(), Is.False);
            Assert.That(control.ApplyCount, Is.EqualTo(1));
            Assert.That(control.AttachedDuringCallback, Is.True);
            Assert.That(control.GetTemplateChild("part").ReadLocalValue(FrameworkElement.NameProperty),
                Is.SameAs(DependencyProperty.UnsetValue));
            Assert.That(((FrameworkElement)control.GetTemplateChild("part")).TemplatedParent, Is.SameAs(control));
            Assert.That(((FrameworkElement)control.GetTemplateChild("text")).TemplatedParent, Is.SameAs(control));
            Assert.That(control.TemplatedParent, Is.Null);
            Assert.That(control.GetTemplateChild("missing"), Is.Null);
        }

        [Test]
        public void TemplateContentsAreConstructedOnlyOncePerApplication()
        {
            TemplateCountingBorder.Instances = 0;
            ControlTemplate template = Template("<t:TemplateCountingBorder/>");
            Assert.That(TemplateCountingBorder.Instances, Is.Zero);
            var first = new Control { Template = template };
            var second = new Control { Template = template };
            first.ApplyTemplate();
            first.ApplyTemplate();
            second.ApplyTemplate();
            Assert.That(TemplateCountingBorder.Instances, Is.EqualTo(2));
        }

        [Test]
        public void MeasureAppliesTemplateAndLayoutAndRenderingRespectMarginsAndAlignment()
        {
            var control = new TemplateTestControl
            {
                Margin = new Thickness(10), HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Bottom,
                Background = new SolidColorBrush(Color.FromRgb(0, 255, 0)),
                Template = Template("<Border x:Name='part' Width='40' Height='20' Margin='2,3,4,5' " +
                    "HorizontalAlignment='Right' VerticalAlignment='Bottom' Background='Red'/>")
            };
            var root = new UIRoot(control);
            root.UpdateLayout(new Size(200, 100));
            var child = (Border)control.GetTemplateChild("part");
            Assert.That(control.ApplyCount, Is.EqualTo(1));
            Assert.That(control.DesiredSize, Is.EqualTo(new Size(66, 48)));
            Assert.That(control.RenderSize, Is.EqualTo(new Size(46, 28)));
            Assert.That(control.VisualOffset, Is.EqualTo(new Point(144, 62)));
            Assert.That(child.VisualOffset, Is.EqualTo(new Point(2, 3)));
            Assert.That(child.RenderSize, Is.EqualTo(new Size(40, 20)));
            var drawing = new RecordingDrawingContext();
            root.Render(drawing);
            Assert.That(drawing.Rectangles.Count, Is.EqualTo(1));
            Assert.That(drawing.Rectangles[0].Rect, Is.EqualTo(new Rect(146, 65, 40, 20)));
            Assert.That(drawing.Rectangles[0].Brush, Is.SameAs(child.Background));
        }

        [Test]
        public void SharedTemplateCreatesIndependentTreesAndReplacementDetachesImmediately()
        {
            ControlTemplate template = Template("<Border x:Name='part' Width='20'/>");
            var first = new TemplateTestControl { Template = template };
            var second = new TemplateTestControl { Template = template };
            first.ApplyTemplate();
            second.ApplyTemplate();
            var oldChild = (Border)first.GetTemplateChild("part");
            Assert.That(oldChild, Is.Not.SameAs(second.GetTemplateChild("part")));
            oldChild.Width = 99;
            Assert.That(((Border)second.GetTemplateChild("part")).Width, Is.EqualTo(20));
            first.Template = Template("<Border x:Name='part' Width='30'/>");
            Assert.That(oldChild.VisualParent, Is.Null);
            Assert.That(first.GetTemplateChild("part"), Is.Null);
            Assert.That(first.ChildCount, Is.Zero);
            Assert.That(first.ApplyTemplate(), Is.True);
            Assert.That(first.GetTemplateChild("part"), Is.Not.SameAs(oldChild));
            var newChild = (Border)first.GetTemplateChild("part");
            first.Template = null;
            Assert.That(newChild.VisualParent, Is.Null);
            Assert.That(first.ApplyTemplate(), Is.False);
            first.Measure(new Size(100, 100));
            Assert.That(first.DesiredSize, Is.EqualTo(new Size(0, 0)));
            Assert.That(first.ChildCount, Is.Zero);
        }

        [Test]
        public void EmptyTemplateAndPlainFrameworkElementDoNotCreateAVisualTree()
        {
            var control = new TemplateTestControl { Template = Template("") };
            Assert.That(control.ApplyTemplate(), Is.False);
            Assert.That(control.ApplyTemplate(), Is.False);
            Assert.That(control.ApplyCount, Is.Zero);
            Assert.That(new FrameworkElement().ApplyTemplate(), Is.False);
            Assert.That(control.Template.IsSealed, Is.True);
            Assert.Throws<InvalidOperationException>(() => control.Template.TargetType = typeof(Control));
            control.Template.Seal();
        }

        [Test]
        public void OnlyEffectiveTemplateChangesReplaceTheTree()
        {
            var control = new TemplateTestControl { Template = Template("<Border x:Name='part'/>") };
            control.ApplyTemplate();
            var oldChild = (Border)control.GetTemplateChild("part");
            var style = new Style(typeof(Control));
            style.Setters.Add(new Setter(Control.TemplateProperty, Template("<TextBlock x:Name='part'/>")));
            control.Style = style;
            Assert.That(control.ApplyTemplate(), Is.False);
            Assert.That(control.GetTemplateChild("part"), Is.SameAs(oldChild));
            control.ClearValue(Control.TemplateProperty);
            Assert.That(oldChild.VisualParent, Is.Null);
            control.Measure(new Size(100, 100));
            Assert.That(control.GetTemplateChild("part"), Is.TypeOf<TextBlock>());
            Assert.That(control.ApplyCount, Is.EqualTo(2));
        }

        [Test]
        public void TemplateAttributesPropertyElementsResourcesAndStyleUseParentTemplateLayer()
        {
            var control = new Control { Template = Template(
                "<Border x:Name='part' Width='20' Height='{StaticResource height}' Grid.Row='2'>" +
                "<Border.Background><SolidColorBrush Color='Red'/></Border.Background>" +
                "<Border.Style><Style TargetType='Border'><Setter Property='Padding' Value='3'/>" +
                "</Style></Border.Style></Border>", new ResourceDictionary { ["height"] = 12.0 }) };
            control.ApplyTemplate();
            var child = (Border)control.GetTemplateChild("part");
            foreach (DependencyProperty dp in new[] { FrameworkElement.WidthProperty, FrameworkElement.HeightProperty,
                Border.BackgroundProperty, FrameworkElement.StyleProperty, Grid.RowProperty })
                Assert.That(child.ReadLocalValue(dp), Is.SameAs(DependencyProperty.UnsetValue));
            Assert.That(child.Padding, Is.EqualTo(new Thickness(3)));
            Assert.That(child.Height, Is.EqualTo(12));
            Assert.That(Grid.GetRow(child), Is.EqualTo(2));
            child.SetLayerValue(FrameworkElement.WidthProperty, ValueLayer.ParentTemplateTrigger, 42.0);
            Assert.That(child.Width, Is.EqualTo(42));
            child.ClearLayerValue(FrameworkElement.WidthProperty, ValueLayer.ParentTemplateTrigger);
            Assert.That(child.Width, Is.EqualTo(20));
            child.Width = 60;
            Assert.That(child.Width, Is.EqualTo(60));
            var replacementStyle = new Style(typeof(Border));
            replacementStyle.Setters.Add(new Setter(Border.PaddingProperty, new Thickness(9)));
            child.SetLayerValue(FrameworkElement.StyleProperty, ValueLayer.ParentTemplateTrigger, replacementStyle);
            Assert.That(child.Padding, Is.EqualTo(new Thickness(9)));
        }

        [Test]
        public void BindingInsideTemplateRemainsALocalExpressionAndUsesInheritedDataContext()
        {
            var control = new Control { DataContext = new { Width = 75.0 },
                Template = Template("<Border x:Name='part' Width='{Binding Width}'/>") };
            control.ApplyTemplate();
            var child = (Border)control.GetTemplateChild("part");
            Assert.That(child.Width, Is.EqualTo(75));
            Assert.That(BindingOperations.GetBindingExpression(child, FrameworkElement.WidthProperty), Is.Not.Null);
            child.SetLayerValue(FrameworkElement.WidthProperty, ValueLayer.ParentTemplateTrigger, 42.0);
            Assert.That(child.Width, Is.EqualTo(75));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void StaticResourceUsesDefinitionScopeAndThenTemplateTree(bool innerResource)
        {
            var dictionary = Build<ResourceDictionary>("<ResourceDictionary " + Namespaces + ">" +
                "<ControlTemplate x:Key='template'><Border x:Name='part'>" +
                (innerResource ? "<Border.Resources><SolidColorBrush x:Key='brush' Color='Blue'/></Border.Resources>" : "") +
                "<Border x:Name='inner' Background='{StaticResource brush}'/>" +
                "</Border></ControlTemplate>" +
                "<SolidColorBrush x:Key='brush' Color='Red'/></ResourceDictionary>");
            var control = new Control { Template = (ControlTemplate)dictionary["template"] };
            control.Resources["brush"] = new SolidColorBrush(Color.FromRgb(0, 255, 0));
            control.ApplyTemplate();
            var part = (Border)control.GetTemplateChild("part");
            Assert.That(((Border)part.Child).Background, Is.SameAs(innerResource ? part.Resources["brush"] : dictionary["brush"]));
        }

        [Test]
        public void StaticResourceRetainsDefiningElementResourcesWhenUsedElsewhere()
        {
            var definition = Build<Border>("<Border " + Namespaces + "><Border.Resources>" +
                "<SolidColorBrush x:Key='brush' Color='Red'/>" +
                "<ControlTemplate x:Key='template'><Border x:Name='part' Background='{StaticResource brush}'/>" +
                "</ControlTemplate></Border.Resources></Border>");
            var control = new Control { Template = (ControlTemplate)definition.Resources["template"] };
            control.ApplyTemplate();
            Assert.That(((Border)control.GetTemplateChild("part")).Background, Is.SameAs(definition.Resources["brush"]));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void DeferredStaticResourceFallsBackToBuildApplicationThenTheme(bool hasApplication)
        {
            var template = Template("<Border x:Name='part' Width='{StaticResource width}'/>",
                hasApplication ? new ResourceDictionary { ["width"] = 10.0 } : null,
                new ResourceDictionary { ["width"] = 20.0 });
            var control = new Control { Template = template };
            new UIRoot(control, null, default, new ResourceDictionary { ["width"] = 99.0 }, null);
            control.ApplyTemplate();
            Assert.That(((Border)control.GetTemplateChild("part")).Width, Is.EqualTo(hasApplication ? 10 : 20));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void MissingResourceFailsAtApplyOrMeasureWithOriginalLocation(bool measure)
        {
            const string line = "    <Border Width='{StaticResource missing}'/>";
            var control = Build<Control>("<Control " + Namespaces + ">\n" +
                "  <Control.Template><ControlTemplate>\n" + line + "\n" +
                "  </ControlTemplate></Control.Template>\n</Control>");
            XamlBuildException error = Assert.Throws<XamlBuildException>(() =>
            {
                if (measure) control.Measure(new Size(100, 100));
                else control.ApplyTemplate();
            });
            Assert.That(error.Line, Is.EqualTo(3));
            Assert.That(error.Column, Is.EqualTo(line.IndexOf("Width", StringComparison.Ordinal) + 1));
            Assert.That(error.Message, Does.Contain("Resource 'missing' was not found."));
            Assert.That(control.GetTemplateChild("part"), Is.Null);
        }

        [Test]
        public void StyleTemplateSetterAndThemeTemplateBothApply()
        {
            var control = Build<Control>("<Control " + Namespaces + "><Control.Style><Style TargetType='Control'>" +
                "<Setter Property='Template'><Setter.Value><ControlTemplate><Border x:Name='part' Width='31'/>" +
                "</ControlTemplate></Setter.Value></Setter></Style></Control.Style></Control>");
            control.Measure(new Size(100, 100));
            Assert.That(control.DesiredSize.Width, Is.EqualTo(31));
            var themed = new TemplateTestControl();
            var style = new Style(typeof(TemplateTestControl));
            style.Setters.Add(new Setter(Control.TemplateProperty, control.Template));
            var root = new UIRoot(themed, null, default, null,
                new ResourceDictionary { [typeof(TemplateTestControl)] = style });
            root.UpdateLayout(new Size(100, 100));
            Assert.That(themed.Template, Is.SameAs(control.Template));
            Assert.That(themed.GetTemplateChild("part"), Is.Not.SameAs(control.GetTemplateChild("part")));
        }

        [Test]
        public void ForegroundAndFontSizeShareDependencyPropertiesAndInheritThroughTemplate()
        {
            var control = Build<Control>("<Control " + Namespaces + " Foreground='Red' FontSize='25'>" +
                "<Control.Template><ControlTemplate><Border><TextBlock x:Name='text'/></Border>" +
                "</ControlTemplate></Control.Template></Control>");
            control.ApplyTemplate();
            var text = (TextBlock)control.GetTemplateChild("text");
            Assert.That(Control.ForegroundProperty, Is.SameAs(TextBlock.ForegroundProperty));
            Assert.That(Control.FontSizeProperty, Is.SameAs(TextBlock.FontSizeProperty));
            Assert.That(text.Foreground, Is.SameAs(control.Foreground));
            Assert.That(text.FontSize, Is.EqualTo(25));
            control.FontSize = 18;
            Assert.That(text.FontSize, Is.EqualTo(18));
            Assert.That(DependencyProperty.FromName(typeof(Control), "Foreground"), Is.SameAs(TextBlock.ForegroundProperty));
            Assert.Throws<ArgumentException>(() => TextBlock.ForegroundProperty.AddOwner(typeof(Control)));
            Assert.Throws<ArgumentNullException>(() => TextBlock.ForegroundProperty.AddOwner(null));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void ImplicitStyleBoundaryIncludesTemplatedParentThenApplicationButControlsSeeOuterAncestors(bool parentStyle)
        {
            var control = new TemplateTestControl { Template = Template("<StackPanel><TextBlock x:Name='text'/>" +
                "<Control x:Name='control'/></StackPanel>") };
            var outer = new Border { Child = control };
            outer.Resources[typeof(TextBlock)] = WidthStyle(typeof(TextBlock), 90);
            outer.Resources[typeof(Control)] = WidthStyle(typeof(Control), 80);
            if (parentStyle) control.Resources[typeof(TextBlock)] = WidthStyle(typeof(TextBlock), 30);
            new UIRoot(outer, null, default,
                new ResourceDictionary { [typeof(TextBlock)] = WidthStyle(typeof(TextBlock), 20) }, null);
            control.ApplyTemplate();
            var text = (TextBlock)control.GetTemplateChild("text");
            Assert.That(text.Width, Is.EqualTo(parentStyle ? 30 : 20));
            Assert.That(((Control)control.GetTemplateChild("control")).Width, Is.EqualTo(80));
            Assert.That(text.FindResource(typeof(TextBlock)), Is.SameAs(parentStyle
                ? control.Resources[typeof(TextBlock)] : outer.Resources[typeof(TextBlock)]));
        }

        [TestCase("<Border/><Border/>", "ControlTemplate can have only one root element.")]
        [TestCase("<StackPanel><Border x:Name='same'/><TextBlock x:Name='same'/></StackPanel>", "Name 'same' is already defined in this template.")]
        [TestCase("<Border Unknown='1'/>", "Unknown or read-only property 'Unknown'")]
        [TestCase("<SolidColorBrush/>", "not assignable")]
        [TestCase("<Border Width='invalid'/>", "Width")]
        [TestCase("<Border TemplatedParent='invalid'/>", "Unknown or read-only property 'TemplatedParent'")]
        public void InvalidTemplateContentIsAParseError(string content, string message)
        {
            var result = XamlParser.Parse(ControlXaml(content), "template.xaml");
            Assert.That(result.Document, Is.Null);
            Assert.That(result.Errors.Any(e => e.Message.Contains(message)), Is.True);
            Assert.That(result.Errors.All(e => e.Line > 0 && e.Column > 0), Is.True);
        }

        [Test]
        public void UnsupportedTemplatePropertiesAreRejected()
        {
            var result = XamlParser.Parse("<Control " + Namespaces + "><Control.Template><ControlTemplate>" +
                "<ControlTemplate.Resources><SolidColorBrush x:Key='brush'/></ControlTemplate.Resources>" +
                "</ControlTemplate></Control.Template></Control>", "template.xaml");
            Assert.That(result.Document, Is.Null);
            Assert.That(result.Errors.Any(e => e.Message.Contains("Unknown or read-only property 'Resources'")), Is.True);
        }

        [Test]
        public void DuplicateDocumentNamesRemainOutsideTemplateValidation()
        {
            var result = XamlParser.Parse("<StackPanel " + Namespaces + ">" +
                "<Border x:Name='same'/><Border x:Name='same'/></StackPanel>", "template.xaml");
            Assert.That(result.Errors, Is.Empty);
        }

        [Test]
        public void SeparateAndNestedTemplatesHaveIndependentNameScopes()
        {
            var outer = Build<Border>("<Border " + Namespaces + "><Border.Resources>" +
                "<ControlTemplate x:Key='one'><Border x:Name='same'/></ControlTemplate>" +
                "<ControlTemplate x:Key='two'><Border x:Name='same'/></ControlTemplate>" +
                "</Border.Resources><Control><Control.Template><ControlTemplate>" +
                "<Control x:Name='same'><Control.Template><ControlTemplate>" +
                "<Border x:Name='same'/></ControlTemplate></Control.Template></Control>" +
                "</ControlTemplate></Control.Template></Control></Border>");
            var control = (Control)outer.Child;
            control.ApplyTemplate();
            var inner = (Control)control.GetTemplateChild("same");
            inner.ApplyTemplate();
            Assert.That(inner.GetTemplateChild("same"), Is.TypeOf<Border>());
            Assert.That(inner.TemplatedParent, Is.SameAs(control));
            Assert.That(((Border)inner.GetTemplateChild("same")).TemplatedParent, Is.SameAs(inner));
        }

        [Test]
        public void TargetTypeMismatchFailsAtApplication()
        {
            var control = Build<Control>("<Control " + Namespaces + "><Control.Template>" +
                "<ControlTemplate TargetType='t:TemplateTestControl'><Border/></ControlTemplate>" +
                "</Control.Template></Control>");
            Assert.That(Assert.Throws<InvalidOperationException>(() => control.ApplyTemplate()).Message,
                Is.EqualTo("'TemplateTestControl' ControlTemplate TargetType does not match templated type 'Control'."));
        }

        [Test]
        public void UnchangedTemplatedTreeLayoutAndRenderAllocateNothing()
        {
            var control = new Control { Template = Template("<Border Background='Red' Padding='4'>" +
                "<Border Width='30' Height='20' Background='Blue'/></Border>") };
            var root = new UIRoot(control);
            var drawing = new EmptyDrawingContext();
            var viewport = new Size(200, 100);
            for (int i = 0; i < 100; i++) { root.UpdateLayout(viewport); root.Render(drawing); }
            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 100; i++) { root.UpdateLayout(viewport); root.Render(drawing); }
            long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
            Assert.That(allocated, Is.Zero);
        }

        private static Style WidthStyle(Type type, double width)
        {
            var style = new Style(type);
            style.Setters.Add(new Setter(FrameworkElement.WidthProperty, width));
            return style;
        }

        private static string ControlXaml(string content) => "<Control " + Namespaces + ">" +
            "<Control.Template><ControlTemplate>" + content + "</ControlTemplate></Control.Template></Control>";

        private static ControlTemplate Template(string content, ResourceDictionary application = null,
            ResourceDictionary theme = null) => Build<Control>(ControlXaml(content), application, theme).Template;

        private static T Build<T>(string xaml, ResourceDictionary application = null, ResourceDictionary theme = null)
        {
            var parsed = XamlParser.Parse(xaml, "template.xaml");
            Assert.That(parsed.Errors, Is.Empty);
            var document = XamlIrSerializer.Deserialize(XamlIrSerializer.Serialize(parsed.Document));
            return (T)XamlBuilder.Build(document, null, application, theme);
        }
    }

    public class TemplateTestControl : Control
    {
        public TemplateTestControl() => DefaultStyleKey = typeof(TemplateTestControl);
        public int ApplyCount { get; private set; }
        public bool AttachedDuringCallback { get; private set; }
        public int ChildCount => VisualChildrenCount;
        public override void OnApplyTemplate()
        {
            ApplyCount++;
            AttachedDuringCallback = ((UIElement)GetTemplateChild("part"))?.VisualParent == this;
        }
    }

    public class TemplateCountingBorder : Border
    {
        public static int Instances;
        public TemplateCountingBorder() => Instances++;
    }
}
