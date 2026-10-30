using System;
using System.Collections.Generic;
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

namespace Xamlity.Core.Tests.Styling
{
    public class ImplicitStyleTests
    {
        private const string Namespaces = "xmlns=\"https://github.com/loonacia/xamlity\" " +
            "xmlns:x=\"http://schemas.microsoft.com/winfx/2006/xaml\"";

        [TestCase(0)]
        [TestCase(1)]
        [TestCase(2)]
        [TestCase(3)]
        public void ImplicitStyleUsesClosestElementParentAncestorOrApplication(int nearest)
        {
            var element = new FrameworkElement();
            var parent = new Border { Child = element };
            var ancestor = new Border { Child = parent };
            var application = new ResourceDictionary();
            var dictionaries = new[] { element.Resources, parent.Resources, ancestor.Resources, application };
            for (int i = nearest; i < dictionaries.Length; i++)
                dictionaries[i].Add(typeof(FrameworkElement), WidthStyle(typeof(FrameworkElement), 10 + i));

            var root = CreateRoot(ancestor, application);

            Assert.That(element.Style, Is.SameAs(dictionaries[nearest][typeof(FrameworkElement)]));
            Assert.That(element.Width, Is.EqualTo(10 + nearest));
            Assert.That(element.Style.IsSealed, Is.True);
            Assert.That(root.ApplicationResources, Is.SameAs(application));
            Assert.That(element.ReadLocalValue(FrameworkElement.StyleProperty), Is.SameAs(DependencyProperty.UnsetValue));
        }

        [Test]
        public void AttachAppliesOwnAndAncestorResourcesWithoutUIRoot()
        {
            var own = WidthStyle(typeof(FrameworkElement), 10);
            var inherited = WidthStyle(typeof(FrameworkElement), 20);
            var element = new FrameworkElement();
            element.Resources.Add(typeof(FrameworkElement), own);
            var parent = new Border();
            parent.Resources.Add(typeof(FrameworkElement), inherited);
            parent.Child = element;
            Assert.That(element.Style, Is.SameAs(own));
            parent.Child = null;
            Assert.That(element.Style, Is.SameAs(own));

            var other = new FrameworkElement();
            parent.Child = other;
            Assert.That(other.Style, Is.SameAs(inherited));
            parent.Child = null;
            Assert.That(other.Style, Is.Null);
            Assert.That(other.Width, Is.NaN);
        }

        [Test]
        public void ImplicitLookupStopsAtApplicationAndUsesExactRuntimeType()
        {
            var theme = new ResourceDictionary();
            theme.Add(typeof(FrameworkElement), WidthStyle(typeof(FrameworkElement), 10));
            var element = new FrameworkElement();
            CreateRoot(element, null, theme);
            Assert.That(element.Style, Is.Null);
            Assert.That(element.Width, Is.NaN);

            var derived = new ThemedElement();
            var application = new ResourceDictionary();
            application.Add(typeof(FrameworkElement), WidthStyle(typeof(FrameworkElement), 20));
            CreateRoot(derived, application);
            Assert.That(derived.Style, Is.Null);
            Assert.That(derived.Width, Is.NaN);
        }

        [Test]
        public void LocalAndParentTemplateStylesOverrideImplicitAndClearingRestoresIt()
        {
            var implicitStyle = WidthStyle(typeof(FrameworkElement), 10);
            var templateStyle = WidthStyle(typeof(FrameworkElement), 20);
            var localStyle = WidthStyle(typeof(FrameworkElement), 30);
            var element = new FrameworkElement { Style = localStyle };
            element.Resources.Add(typeof(FrameworkElement), implicitStyle);
            CreateRoot(element);
            Assert.That(element.Style, Is.SameAs(localStyle));
            Assert.That(element.Width, Is.EqualTo(30));
            element.SetLayerValue(FrameworkElement.StyleProperty, ValueLayer.ParentTemplate, templateStyle);
            element.ClearValue(FrameworkElement.StyleProperty);
            Assert.That(element.Style, Is.SameAs(templateStyle));
            Assert.That(element.Width, Is.EqualTo(20));
            element.ClearLayerValue(FrameworkElement.StyleProperty, ValueLayer.ParentTemplate);
            Assert.That(element.Style, Is.SameAs(implicitStyle));
            Assert.That(element.Width, Is.EqualTo(10));
            element.Style = null;
            Assert.That(element.Style, Is.Null);
            Assert.That(element.Width, Is.NaN);
            element.ClearValue(FrameworkElement.StyleProperty);
            Assert.That(element.Style, Is.SameAs(implicitStyle));
        }

        [Test]
        public void MovingAndDetachingSubtreeRefreshesDescendantsAndApplicationStyles()
        {
            var firstStyle = WidthStyle(typeof(FrameworkElement), 10);
            var secondStyle = WidthStyle(typeof(FrameworkElement), 20);
            var firstApp = new ResourceDictionary { [typeof(FrameworkElement)] = firstStyle };
            var secondApp = new ResourceDictionary { [typeof(FrameworkElement)] = secondStyle };
            var first = new Border();
            var second = new Border();
            CreateRoot(first, firstApp);
            CreateRoot(second, secondApp);
            var element = new FrameworkElement();
            var subtree = new Border { Child = new VisualContainer(element) };
            first.Child = subtree;
            Assert.That(element.Style, Is.SameAs(firstStyle));
            first.Child = null;
            Assert.That(element.Style, Is.Null);
            Assert.That(element.Width, Is.NaN);
            second.Child = subtree;
            Assert.That(element.Style, Is.SameAs(secondStyle));
            Assert.That(element.Width, Is.EqualTo(20));

            var ancestorStyle = WidthStyle(typeof(FrameworkElement), 30);
            first.Resources.Add(typeof(FrameworkElement), ancestorStyle);
            second.Child = null;
            first.Child = subtree;
            Assert.That(element.Style, Is.SameAs(ancestorStyle));
            first.Child = null;
            Assert.That(element.Style, Is.Null);
        }

        [Test]
        public void RootCreationAppliesApplicationStyleToRootAndExistingSubtree()
        {
            var style = WidthStyle(typeof(Border), 10);
            var child = new Border();
            var parent = new Border { Child = child };
            Assert.That(child.Style, Is.Null);
            CreateRoot(parent, new ResourceDictionary { [typeof(Border)] = style });
            Assert.That(parent.Style, Is.SameAs(style));
            Assert.That(child.Style, Is.SameAs(style));
        }

        [Test]
        public void ThemeUsesDefaultStyleKeyAndBasedOnSettersAlongsideImplicitAndLocalStyle()
        {
            var basis = WidthStyle(typeof(ThemedElement), 10);
            basis.Setters.Add(new Setter(FrameworkElement.HeightProperty, 20.0));
            var themeStyle = new Style(typeof(ThemedElement), basis);
            themeStyle.Setters.Add(new Setter(FrameworkElement.HeightProperty, 30.0));
            var implicitStyle = WidthStyle(typeof(ThemedElement), 40);
            var element = new ThemedElement();
            var theme = new ResourceDictionary { [typeof(ThemedElement)] = themeStyle };
            CreateRoot(element, new ResourceDictionary { [typeof(ThemedElement)] = implicitStyle }, theme);

            Assert.That(element.Style, Is.SameAs(implicitStyle));
            Assert.That(element.Width, Is.EqualTo(40));
            Assert.That(element.Height, Is.EqualTo(30));
            Assert.That(themeStyle.IsSealed && basis.IsSealed, Is.True);
            element.Style = WidthStyle(typeof(ThemedElement), 50);
            Assert.That(element.Width, Is.EqualTo(50));
            Assert.That(element.Height, Is.EqualTo(30));
            element.Style = null;
            Assert.That(element.Width, Is.EqualTo(10));
            Assert.That(element.Height, Is.EqualTo(30));
            element.Width = 60;
            Assert.That(element.Width, Is.EqualTo(60));
            element.ClearValue(FrameworkElement.WidthProperty);
            Assert.That(element.Width, Is.EqualTo(10));
        }

        [Test]
        public void DerivedElementInheritsBaseDefaultStyleKey()
        {
            var style = WidthStyle(typeof(ThemedElement), 10);
            var element = new DerivedThemedElement();
            CreateRoot(element, null, new ResourceDictionary { [typeof(ThemedElement)] = style });
            Assert.That(element.Width, Is.EqualTo(10));
            Assert.That(element.Style, Is.Null);
        }

        [Test]
        public void ThemeLookupIgnoresTreeAndApplicationResourcesAndRequiresRootAndKey()
        {
            var style = WidthStyle(typeof(ThemedElement), 10);
            var element = new DerivedThemedElement();
            var parent = new Border { Child = element };
            element.Resources.Add(typeof(ThemedElement), style);
            parent.Resources.Add(typeof(ThemedElement), style);
            Assert.That(element.Width, Is.NaN);
            CreateRoot(parent, new ResourceDictionary { [typeof(ThemedElement)] = style });
            Assert.That(element.Width, Is.NaN);
            CreateRoot(parent, null, new ResourceDictionary { [typeof(ThemedElement)] = "not a style" });
            Assert.That(element.Width, Is.NaN);

            var noKey = new FrameworkElement();
            CreateRoot(noKey, null, new ResourceDictionary { [typeof(FrameworkElement)] = WidthStyle(typeof(FrameworkElement), 20) });
            Assert.That(noKey.Width, Is.NaN);
        }

        [Test]
        public void ThemeStylesRefreshForEntireSubtreeOnAttachDetachAndMove()
        {
            var firstStyle = WidthStyle(typeof(ThemedElement), 10);
            firstStyle.Setters.Add(new Setter(FrameworkElement.HeightProperty, 15.0));
            var secondStyle = WidthStyle(typeof(ThemedElement), 20);
            var first = new Border();
            var second = new Border();
            CreateRoot(first, null, new ResourceDictionary { [typeof(ThemedElement)] = firstStyle });
            CreateRoot(second, null, new ResourceDictionary { [typeof(ThemedElement)] = secondStyle });
            var element = new DerivedThemedElement();
            var subtree = new Border { Child = element };
            first.Child = subtree;
            Assert.That(element.Width, Is.EqualTo(10));
            Assert.That(element.Height, Is.EqualTo(15));
            first.Child = null;
            Assert.That(element.Width, Is.NaN);
            Assert.That(element.Height, Is.NaN);
            second.Child = subtree;
            Assert.That(element.Width, Is.EqualTo(20));
            Assert.That(element.Height, Is.NaN);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void ResourceStylesValidateTargetTypeAndSeal(bool theme)
        {
            var style = WidthStyle(typeof(Border), 10);
            var dictionary = new ResourceDictionary { [typeof(ThemedElement)] = style };
            var element = new ThemedElement();
            var exception = Assert.Throws<InvalidOperationException>(() =>
                CreateRoot(element, theme ? null : dictionary, theme ? dictionary : null));
            Assert.That(exception.Message, Is.EqualTo("'Border' TargetType does not match type of element 'ThemedElement'."));
            Assert.That(style.IsSealed, Is.True);
            Assert.That(element.Width, Is.NaN);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void ResourceStylesRejectInvalidSetters(bool theme)
        {
            var style = new Style(typeof(ThemedElement));
            style.Setters.Add(new Setter(FrameworkElement.WidthProperty, "invalid"));
            var dictionary = new ResourceDictionary { [typeof(ThemedElement)] = style };
            Assert.Throws<InvalidOperationException>(() =>
                CreateRoot(new ThemedElement(), theme ? null : dictionary, theme ? dictionary : null));
        }

        [Test]
        public void SameStyleReferencesDoNotNotifyAndDictionaryChangesWaitForTreeRefresh()
        {
            var element = new ThemedElement();
            var style = WidthStyle(typeof(ThemedElement), 10);
            var themeStyle = new Style(typeof(ThemedElement));
            themeStyle.Setters.Add(new Setter(FrameworkElement.HeightProperty, 20.0));
            var application = new ResourceDictionary { [typeof(ThemedElement)] = style };
            var theme = new ResourceDictionary { [typeof(ThemedElement)] = themeStyle };
            CreateRoot(element, application, theme);
            element.Changes.Clear();
            var root = CreateRoot(element, application, theme);
            Assert.That(element.Changes, Is.Empty);

            var replacement = WidthStyle(typeof(ThemedElement), 30);
            application[typeof(ThemedElement)] = replacement;
            theme.Clear();
            root.UpdateLayout(new Size(300, 200));
            root.Render(new EmptyDrawingContext());
            Assert.That(element.Style, Is.SameAs(style));
            Assert.That(element.Height, Is.EqualTo(20));
            CreateRoot(element, application, theme);
            Assert.That(element.Style, Is.SameAs(replacement));
            Assert.That(element.Height, Is.NaN);
        }

        [Test]
        public void FindResourceUsesElementVisualParentsApplicationThenTheme()
        {
            var element = new FrameworkElement();
            var parent = new Border { Child = new VisualContainer(element) };
            var ancestor = new Border { Child = parent };
            var application = new ResourceDictionary();
            var theme = new ResourceDictionary();
            CreateRoot(ancestor, application, theme);
            var dictionaries = new[] { element.Resources, parent.Resources, ancestor.Resources, application, theme };
            for (int i = 0; i < dictionaries.Length; i++)
                dictionaries[i].Add("key", new object());
            for (int i = 0; i < dictionaries.Length; i++)
            {
                Assert.That(element.FindResource("key"), Is.SameAs(dictionaries[i]["key"]));
                Assert.That(element.TryFindResource("key"), Is.SameAs(dictionaries[i]["key"]));
                dictionaries[i].Remove("key");
            }
            Assert.That(element.TryFindResource("key"), Is.Null);
            Assert.That(Assert.Throws<InvalidOperationException>(() => element.FindResource("key")).Message,
                Is.EqualTo("'key' resource not found."));
            Assert.Throws<ArgumentNullException>(() => element.FindResource(null));
            Assert.Throws<ArgumentNullException>(() => element.TryFindResource(null));
        }

        [Test]
        public void RuntimeLookupSupportsTypeKeysMergedDictionariesAndNullValues()
        {
            var element = new FrameworkElement();
            var value = new object();
            element.Resources.MergedDictionaries.Add(new ResourceDictionary { [typeof(Border)] = value });
            Assert.That(element.FindResource(typeof(Border)), Is.SameAs(value));
            element.Resources.Add("null", null);
            Assert.That(element.FindResource("null"), Is.Null);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void StaticResourceFallsBackToApplicationBeforeTheme(bool hasApplication)
        {
            var application = new ResourceDictionary { ["width"] = 10.0 };
            var theme = new ResourceDictionary { ["width"] = 20.0 };
            var element = Build("<Border " + Namespaces + " Width=\"{StaticResource width}\"/>",
                hasApplication ? application : null, theme);
            Assert.That(element.Width, Is.EqualTo(hasApplication ? 10 : 20));
        }

        [Test]
        public void StaticResourceLexicalScopeWinsOverApplicationAndTheme()
        {
            var application = new ResourceDictionary { ["brush"] = new SolidColorBrush(Color.FromRgb(255, 0, 0)) };
            var theme = new ResourceDictionary { ["brush"] = new SolidColorBrush(Color.FromRgb(0, 0, 255)) };
            var parent = Build("<Border " + Namespaces + "><Border.Resources>" +
                "<SolidColorBrush x:Key=\"brush\" Color=\"Green\"/></Border.Resources>" +
                "<Border Background=\"{StaticResource brush}\"/></Border>", application, theme);
            Assert.That(((Border)parent.Child).Background, Is.SameAs(parent.Resources["brush"]));
        }

        [Test]
        public void StaticResourceFallbackWorksInStyleSettersAndNestedBindingWithTypeKey()
        {
            var theme = new ResourceDictionary { ["width"] = 25.0 };
            var application = new ResourceDictionary { [typeof(FrameworkElement)] = 35.0 };
            var parent = Build("<Border " + Namespaces + "><Border.Resources>" +
                "<Style TargetType=\"Border\"><Setter Property=\"Height\" Value=\"{StaticResource width}\"/></Style>" +
                "</Border.Resources><Border Width=\"{Binding Width, ConverterParameter={StaticResource {x:Type FrameworkElement}}}\"/></Border>",
                application, theme);
            var child = (Border)parent.Child;
            Assert.That(child.Height, Is.EqualTo(25));
            Assert.That(BindingOperations.GetBindingExpression(child, FrameworkElement.WidthProperty)
                .ParentBinding.ConverterParameter, Is.EqualTo(35.0));
        }

        [Test]
        public void SeparateBuildsAndRootsDoNotShareResources()
        {
            var application = new ResourceDictionary { ["width"] = 10.0 };
            var xaml = "<Border " + Namespaces + " Width=\"{StaticResource width}\"/>";
            Assert.That(Build(xaml, application).Width, Is.EqualTo(10));
            Assert.Throws<XamlBuildException>(() => XamlBuilder.Build(Parse(xaml)));
            Assert.Throws<XamlBuildException>(() => XamlBuilder.Build(Parse(xaml), null));
            application.Add(typeof(Border), WidthStyle(typeof(Border), 20));
            var styled = new Border();
            CreateRoot(styled, application);
            var plain = new Border();
            var root = new UIRoot(plain);
            Assert.That(plain.Style, Is.Null);
            Assert.That(root.ApplicationResources, Is.Null);
            Assert.That(root.ThemeResources, Is.Null);
            Assert.That(styled.Width, Is.EqualTo(20));
        }

        [Test]
        public void UnchangedStyledTreeLayoutAndRenderAllocateNothing()
        {
            var implicitStyle = WidthStyle(typeof(Border), 100);
            implicitStyle.Setters.Add(new Setter(Border.BackgroundProperty, new SolidColorBrush(Color.FromRgb(10, 20, 30))));
            var themeStyle = new Style(typeof(ThemedElement));
            themeStyle.Setters.Add(new Setter(FrameworkElement.HeightProperty, 20.0));
            var parent = new Border { Child = new ThemedElement() };
            var root = CreateRoot(parent, new ResourceDictionary { [typeof(Border)] = implicitStyle },
                new ResourceDictionary { [typeof(ThemedElement)] = themeStyle });
            var viewport = new Size(300, 200);
            var drawing = new EmptyDrawingContext();
            for (int i = 0; i < 100; i++)
            {
                root.UpdateLayout(viewport);
                root.Render(drawing);
            }
            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 100; i++)
            {
                root.UpdateLayout(viewport);
                root.Render(drawing);
            }
            long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
            Assert.That(allocated, Is.Zero);
            Assert.That(parent.Style, Is.SameAs(implicitStyle));
            Assert.That(((ThemedElement)parent.Child).Height, Is.EqualTo(20));
        }

        private static Style WidthStyle(Type targetType, double width)
        {
            var style = new Style(targetType);
            style.Setters.Add(new Setter(FrameworkElement.WidthProperty, width));
            return style;
        }

        private static UIRoot CreateRoot(UIElement element, ResourceDictionary application = null,
            ResourceDictionary theme = null) => new UIRoot(element, null, default, application, theme);

        private static XamlDocument Parse(string xaml)
        {
            var result = XamlParser.Parse(xaml, "implicit.xaml");
            Assert.That(result.Errors, Is.Empty);
            return result.Document;
        }

        private static Border Build(string xaml, ResourceDictionary application = null,
            ResourceDictionary theme = null) => (Border)XamlBuilder.Build(Parse(xaml), null, application, theme);

        private class ThemedElement : FrameworkElement
        {
            public ThemedElement() => DefaultStyleKey = typeof(ThemedElement);

            public List<DependencyPropertyChangedEventArgs> Changes { get; } = new List<DependencyPropertyChangedEventArgs>();

            protected override void OnPropertyChanged(DependencyPropertyChangedEventArgs e)
            {
                base.OnPropertyChanged(e);
                Changes.Add(e);
            }
        }

        private sealed class DerivedThemedElement : ThemedElement { }

        private sealed class VisualContainer : UIElement
        {
            private readonly UIElement child;

            public VisualContainer(UIElement child)
            {
                this.child = child;
                AddVisualChild(child);
            }

            protected override int VisualChildrenCount => 1;
            protected override UIElement GetVisualChild(int index) => child;
        }
    }
}
