using System;
using System.Globalization;
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
    public class ContentControlTests
    {
        private const string Namespaces = "xmlns='https://github.com/loonacia/xamlity' " +
            "xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml'";
        private static readonly Size Viewport = new Size(100, 80);

        [Test]
        public void GenericThemeContainsExactlyTheTwoTargetTypeStyles()
        {
            ResourceDictionary theme = GenericTheme.Load();
            Assert.That(theme.Count, Is.EqualTo(2));
            foreach (Type type in new[] { typeof(ContentControl), typeof(UserControl) })
            {
                var style = (Style)theme[type];
                Assert.That(style.TargetType, Is.EqualTo(type));
                style.Seal();
                Assert.That(((ControlTemplate)style.SetterValues[Control.TemplateProperty]).TargetType, Is.EqualTo(type));
            }
            Assert.That(new ContentControl().DefaultStyleKey, Is.EqualTo(typeof(ContentControl)));
            Assert.That(new UserControl().DefaultStyleKey, Is.EqualTo(typeof(UserControl)));
        }

        [Test]
        public void KoreanContentCreatesTextAndDrawsGlyphs()
        {
            var control = Build<ContentControl>("<ContentControl " + Namespaces + " Content='한글'/>");
            UIRoot root = GenericTheme.CreateRoot(control);
            root.UpdateLayout(Viewport);
            var presenter = (ContentPresenter)Child(control);
            var text = (TextBlock)Child(presenter);
            Assert.That(text.Text, Is.EqualTo("한글"));
            Assert.That(text.TemplatedParent, Is.SameAs(presenter));
            Assert.That(presenter.TemplatedParent, Is.SameAs(control));
            Assert.That(control.DesiredSize.Width, Is.EqualTo(12));
            var drawing = new RecordingDrawingContext();
            root.Render(drawing);
            Assert.That(drawing.GlyphRuns.Count, Is.EqualTo(1));
            Assert.That(drawing.GlyphRuns[0].Run.GlyphIndices, Is.EqualTo(new uint[] { '한' + 1, '글' + 1 }));
        }

        [Test]
        public void ContentConversionUsesInvariantCultureAndFallsBackToToString()
        {
            CultureInfo previous = CultureInfo.CurrentCulture;
            try
            {
                CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("de-DE");
                var formattable = new FormattableContent();
                object[] values = { "한글", 123, 12.5m, formattable, new PlainContent() };
                string[] expected = { "한글", "123", "12.5", "formatted", "plain" };
                var control = new ContentControl();
                UIRoot root = GenericTheme.CreateRoot(control);
                for (int i = 0; i < values.Length; i++)
                {
                    control.Content = values[i];
                    root.UpdateLayout(Viewport);
                    Assert.That(((TextBlock)Child(Child(control))).Text, Is.EqualTo(expected[i]));
                }
                Assert.That(formattable.Format, Is.Null);
                Assert.That(formattable.Provider, Is.SameAs(CultureInfo.InvariantCulture));
            }
            finally
            {
                CultureInfo.CurrentCulture = previous;
            }
        }

        [Test]
        public void ReplacementUpdatesTextInvalidatesMeasureAndNullRemovesTheChild()
        {
            var control = new ContentControl { Content = "a" };
            UIRoot root = GenericTheme.CreateRoot(control);
            root.UpdateLayout(Viewport);
            var presenter = (ContentPresenter)Child(control);
            UIElement old = Child(presenter);
            control.Content = "longer";
            Assert.That(old.VisualParent, Is.Null);
            Assert.That(presenter.IsMeasureValid, Is.False);
            Assert.That(control.IsMeasureValid, Is.False);
            root.UpdateLayout(Viewport);
            Assert.That(control.DesiredSize.Width, Is.EqualTo(36));
            Assert.That(((TextBlock)Child(presenter)).Text, Is.EqualTo("longer"));
            old = Child(presenter);
            control.Content = null;
            Assert.That(old.VisualParent, Is.Null);
            Assert.That(presenter.InheritanceChildrenCount, Is.Zero);
            root.UpdateLayout(Viewport);
            Assert.That(control.DesiredSize, Is.EqualTo(new Size(0, 0)));
        }

        [TestCase("ContentControl")]
        [TestCase("UserControl")]
        public void ElementContentInheritsDataContextForegroundAndBinding(string type)
        {
            var control = Build<ContentControl>("<" + type + " " + Namespaces + ">" +
                "<TextBlock Text='{Binding Caption}'/></" + type + ">");
            var text = (TextBlock)control.Content;
            var red = new SolidColorBrush(Color.FromRgb(255, 0, 0));
            var blue = new SolidColorBrush(Color.FromRgb(0, 0, 255));
            control.DataContext = new ContentViewModel { Caption = "first" };
            control.Foreground = red;
            UIRoot root = GenericTheme.CreateRoot(control);
            root.UpdateLayout(Viewport);
            Assert.That(text.VisualParent, Is.TypeOf<ContentPresenter>());
            Assert.That(text.TemplatedParent, Is.Null);
            Assert.That(text.DataContext, Is.SameAs(control.DataContext));
            Assert.That(text.Text, Is.EqualTo("first"));
            Assert.That(text.Foreground, Is.SameAs(red));
            control.DataContext = new ContentViewModel { Caption = "second" };
            control.Foreground = blue;
            Assert.That(text.Text, Is.EqualTo("second"));
            Assert.That(text.Foreground, Is.SameAs(blue));
            var replacement = new Border();
            control.Content = replacement;
            Assert.That(text.VisualParent, Is.Null);
            Assert.That(text.DataContext, Is.Null);
            Assert.That(replacement.VisualParent, Is.TypeOf<ContentPresenter>());
        }

        [TestCase(false)]
        [TestCase(true)]
        public void TemplateReplacementReleasesTheSameElementForTheNewPresenter(bool userControl)
        {
            var content = new Border();
            ContentControl control = userControl ? new UserControl() : new ContentControl();
            control.Content = content;
            UIRoot root = GenericTheme.CreateRoot(control);
            root.UpdateLayout(Viewport);
            var previous = (ContentPresenter)content.VisualParent;
            control.Template = Template("<Border><ContentPresenter x:Name='presenter'/></Border>");
            Assert.That(content.VisualParent, Is.Null);
            Assert.That(previous.InheritanceChildrenCount, Is.Zero);
            root.UpdateLayout(Viewport);
            var current = (ContentPresenter)control.GetTemplateChild("presenter");
            Assert.That(content.VisualParent, Is.SameAs(current));
            Assert.That(current, Is.Not.SameAs(previous));
            var next = new Border();
            control.Content = next;
            Assert.That(previous.Content, Is.SameAs(content));
            Assert.That(previous.InheritanceChildrenCount, Is.Zero);
            Assert.That(content.VisualParent, Is.Null);
            Assert.That(next.VisualParent, Is.SameAs(current));
            control.Template = null;
            Assert.That(next.VisualParent, Is.Null);
            Assert.That(current.InheritanceChildrenCount, Is.Zero);
        }

        [Test]
        public void UserControlThemeDrawsBorderAndStretchesContentInsidePadding()
        {
            var background = new SolidColorBrush(Color.FromRgb(255, 0, 0));
            var outline = new SolidColorBrush(Color.FromRgb(0, 0, 255));
            var fill = new SolidColorBrush(Color.FromRgb(0, 255, 0));
            var content = new Border { Background = fill };
            var control = new UserControl { Content = content, Background = background, BorderBrush = outline,
                BorderThickness = new Thickness(2, 3, 4, 5), Padding = new Thickness(6, 7, 8, 9) };
            UIRoot root = GenericTheme.CreateRoot(control);
            root.UpdateLayout(Viewport);
            var border = (Border)Child(control);
            var presenter = (ContentPresenter)border.Child;
            Assert.That(border.Background, Is.SameAs(background));
            Assert.That(border.BorderBrush, Is.SameAs(outline));
            Assert.That(border.BorderThickness, Is.EqualTo(control.BorderThickness));
            Assert.That(border.Padding, Is.EqualTo(control.Padding));
            Assert.That(control.HorizontalContentAlignment, Is.EqualTo(HorizontalAlignment.Stretch));
            Assert.That(control.VerticalContentAlignment, Is.EqualTo(VerticalAlignment.Stretch));
            Assert.That(presenter.VisualOffset, Is.EqualTo(new Point(8, 10)));
            Assert.That(presenter.RenderSize, Is.EqualTo(new Size(80, 56)));
            Assert.That(content.VisualOffset, Is.EqualTo(new Point(0, 0)));
            Assert.That(content.RenderSize, Is.EqualTo(new Size(80, 56)));
            var drawing = new RecordingDrawingContext();
            root.Render(drawing);
            Assert.That(drawing.Rectangles, Is.EqualTo(new[]
            {
                ((Brush)outline, new Rect(0, 0, 100, 3)),
                ((Brush)outline, new Rect(0, 75, 100, 5)),
                ((Brush)outline, new Rect(0, 3, 2, 72)),
                ((Brush)outline, new Rect(96, 3, 4, 72)),
                ((Brush)background, new Rect(2, 3, 94, 72)),
                ((Brush)fill, new Rect(8, 10, 80, 56))
            }));
            control.Padding = new Thickness(1);
            root.UpdateLayout(Viewport);
            Assert.That(presenter.VisualOffset, Is.EqualTo(new Point(3, 4)));
            Assert.That(content.RenderSize, Is.EqualTo(new Size(92, 70)));
        }

        [Test]
        public void UserControlContentAlignmentBindingsUpdateCoordinates()
        {
            var control = new UserControl { Content = new Border { Width = 20, Height = 10 },
                Padding = new Thickness(5) };
            UIRoot root = GenericTheme.CreateRoot(control);
            root.UpdateLayout(Viewport);
            var presenter = (ContentPresenter)((Border)Child(control)).Child;
            control.HorizontalContentAlignment = HorizontalAlignment.Right;
            control.VerticalContentAlignment = VerticalAlignment.Bottom;
            root.UpdateLayout(Viewport);
            Assert.That(presenter.VisualOffset, Is.EqualTo(new Point(75, 65)));
            Assert.That(presenter.RenderSize, Is.EqualTo(new Size(20, 10)));
        }

        [Test]
        public void ImplicitApplicationStyleKeepsTheThemeTemplate()
        {
            var resources = Build<ResourceDictionary>("<ResourceDictionary " + Namespaces + ">" +
                "<Style TargetType='ContentControl'><Setter Property='Background' Value='Red'/></Style>" +
                "</ResourceDictionary>");
            var control = new ContentControl { Content = "text" };
            UIRoot root = GenericTheme.CreateRoot(control, resources);
            root.UpdateLayout(Viewport);
            Assert.That(control.Style, Is.SameAs(resources[typeof(ContentControl)]));
            Assert.That(((SolidColorBrush)control.Background).Color, Is.EqualTo(Color.FromRgb(255, 0, 0)));
            Assert.That(control.Template, Is.Not.Null);
            Assert.That(((TextBlock)Child(Child(control))).Text, Is.EqualTo("text"));
        }

        [TestCase("")]
        [TestCase(" Content='{TemplateBinding Content}'")]
        public void ImplicitAndExplicitTemplateBindingsShareUpdatesAndValuePrecedence(string attribute)
        {
            var control = new ContentControl { Content = "first", Template = Template(
                "<ContentPresenter x:Name='presenter'" + attribute + "/>") };
            control.ApplyTemplate();
            var presenter = (ContentPresenter)control.GetTemplateChild("presenter");
            Assert.That(presenter.Content, Is.EqualTo("first"));
            Assert.That(presenter.ReadLocalValue(ContentPresenter.ContentProperty), Is.SameAs(DependencyProperty.UnsetValue));
            presenter.Content = "local";
            control.Content = "second";
            Assert.That(presenter.Content, Is.EqualTo("local"));
            presenter.ClearValue(ContentPresenter.ContentProperty);
            Assert.That(presenter.Content, Is.EqualTo("second"));
            Assert.That(((TextBlock)Child(presenter)).Text, Is.EqualTo("second"));
            control.Content = null;
            Assert.That(presenter.InheritanceChildrenCount, Is.Zero);
        }

        [TestCase("Content='explicit'", "explicit")]
        [TestCase("Content='{StaticResource value}'", "resource")]
        [TestCase("Content='{Binding Missing}'", null)]
        public void ExplicitContentSuppressesImplicitConnection(string attribute, string expected)
        {
            var resources = new ResourceDictionary { ["value"] = "resource" };
            var template = Template("<ContentPresenter x:Name='presenter' " + attribute + "/>", resources);
            var control = new ContentControl { Content = "implicit", Template = template };
            control.ApplyTemplate();
            var presenter = (ContentPresenter)control.GetTemplateChild("presenter");
            Assert.That(presenter.Content, Is.EqualTo(expected));
            control.Content = "changed";
            Assert.That(presenter.Content, Is.EqualTo(expected));
        }

        [Test]
        public void StandaloneAndNonContentControlPresentersHaveNoImplicitContent()
        {
            var presenter = new ContentPresenter();
            Assert.That(presenter.Content, Is.Null);
            Assert.That(presenter.InheritanceChildrenCount, Is.Zero);
            presenter.Content = "standalone";
            Assert.That(((TextBlock)Child(presenter)).Text, Is.EqualTo("standalone"));
            var control = Build<Control>("<Control " + Namespaces + "><Control.Template><ControlTemplate>" +
                "<ContentPresenter x:Name='presenter'/></ControlTemplate></Control.Template></Control>");
            control.ApplyTemplate();
            Assert.That(((ContentPresenter)control.GetTemplateChild("presenter")).Content, Is.Null);
        }

        [TestCase("ContentTemplate")]
        [TestCase("ContentSource")]
        [TestCase("RecognizesAccessKey")]
        [TestCase("HasContent")]
        public void UnsupportedContentFeaturesRemainErrors(string property)
        {
            var result = XamlParser.Parse("<ContentPresenter " + Namespaces + " " + property + "='value'/>", "content.xaml");
            Assert.That(result.Document, Is.Null);
            Assert.That(result.Errors, Has.Count.EqualTo(1));
            Assert.That(result.Errors[0].Message, Does.Contain(property));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void UnchangedContentTreeLayoutAndRenderingAllocateNothing(bool elementContent)
        {
            var control = new UserControl { Content = elementContent ? (object)new TextBlock { Text = "한글" } : "한글" };
            UIRoot root = GenericTheme.CreateRoot(control);
            var drawing = new EmptyDrawingContext();
            for (int i = 0; i < 100; i++) { root.UpdateLayout(Viewport); root.Render(drawing); }
            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 100; i++) { root.UpdateLayout(Viewport); root.Render(drawing); }
            long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
            Assert.That(allocated, Is.Zero);
        }

        private static UIElement Child(UIElement parent)
        {
            Assert.That(parent.InheritanceChildrenCount, Is.EqualTo(1));
            return (UIElement)parent.GetInheritanceChild(0);
        }

        private static ControlTemplate Template(string content, ResourceDictionary resources = null) => Build<ContentControl>(
            "<ContentControl " + Namespaces + "><Control.Template><ControlTemplate TargetType='ContentControl'>" +
            content + "</ControlTemplate></Control.Template></ContentControl>", resources).Template;

        private static T Build<T>(string xaml, ResourceDictionary resources = null)
        {
            var result = XamlParser.Parse(xaml, "content.xaml");
            Assert.That(result.Errors, Is.Empty, string.Join("\n", result.Errors));
            return (T)XamlBuilder.Build(XamlIrSerializer.Deserialize(XamlIrSerializer.Serialize(result.Document)),
                null, resources, null);
        }

        private sealed class PlainContent
        {
            public override string ToString() => "plain";
        }

        private sealed class FormattableContent : IFormattable
        {
            internal string Format;
            internal IFormatProvider Provider;
            public string ToString(string format, IFormatProvider formatProvider)
            {
                Format = format;
                Provider = formatProvider;
                return "formatted";
            }
            public override string ToString() => throw new InvalidOperationException("Use IFormattable.");
        }

        public sealed class ContentViewModel
        {
            public string Caption { get; set; }
        }
    }
}
