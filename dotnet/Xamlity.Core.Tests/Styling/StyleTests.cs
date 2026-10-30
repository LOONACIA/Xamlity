using System;
using System.Collections.Generic;
using NUnit.Framework;
using Xamlity.Controls;
using Xamlity.Data;
using Xamlity.Layout;
using Xamlity.Properties;
using Xamlity.Styling;
using Xamlity.Tree;

namespace Xamlity.Core.Tests.Styling
{
    public class StyleTests
    {
        [Test]
        public void SettersApplyBelowLocalValuesAndReplacementAndNullRemoveOldValues()
        {
            var first = new Style(typeof(FrameworkElement));
            first.Setters.Add(new Setter(FrameworkElement.WidthProperty, 100.0));
            first.Setters.Add(new Setter(FrameworkElement.HeightProperty, 50.0));
            var second = new Style(typeof(FrameworkElement));
            second.Setters.Add(new Setter(FrameworkElement.WidthProperty, 200.0));
            var element = new FrameworkElement { Width = 25, Style = first };
            Assert.That(element.Width, Is.EqualTo(25));
            Assert.That(element.Height, Is.EqualTo(50));
            Assert.That(element.ReadLocalValue(FrameworkElement.HeightProperty), Is.SameAs(DependencyProperty.UnsetValue));
            element.ClearValue(FrameworkElement.WidthProperty);
            Assert.That(element.Width, Is.EqualTo(100));
            element.Style = second;
            Assert.That(element.Width, Is.EqualTo(200));
            Assert.That(element.Height, Is.NaN);
            element.Style = null;
            Assert.That(element.Width, Is.NaN);
            Assert.That(element.Style, Is.Null);
            Assert.That(first.IsSealed && second.IsSealed, Is.True);
        }

        [Test]
        public void BasedOnAndDuplicateSettersUseLastValueAndStylesCanBeShared()
        {
            var basis = new Style(typeof(FrameworkElement));
            basis.Setters.Add(new Setter(FrameworkElement.WidthProperty, 10.0));
            basis.Setters.Add(new Setter(FrameworkElement.HeightProperty, 40.0));
            var middle = new Style(typeof(TextBlock), basis);
            middle.Setters.Add(new Setter(FrameworkElement.WidthProperty, 20.0));
            var style = new Style(typeof(TextBlock), middle);
            style.Setters.Add(new Setter(FrameworkElement.WidthProperty, 30.0));
            style.Setters.Add(new Setter(FrameworkElement.WidthProperty, 50.0));
            var first = new TextBlock { Style = style };
            var second = new TextBlock { Style = style };
            Assert.That(first.Width, Is.EqualTo(50));
            Assert.That(second.Width, Is.EqualTo(50));
            Assert.That(first.Height, Is.EqualTo(40));
            Assert.That(basis.IsSealed && middle.IsSealed && style.IsSealed, Is.True);
            Assert.That(basis.Setters[0].IsSealed && middle.Setters[0].IsSealed && style.Setters[1].IsSealed, Is.True);
            first.Style = null;
            Assert.That(first.Width, Is.NaN);
            Assert.That(first.Height, Is.NaN);
            Assert.That(second.Width, Is.EqualTo(50));
            Assert.DoesNotThrow(style.Seal);
        }

        [Test]
        public void StyleValuesCoexistWithOtherLayersAndInheritedValues()
        {
            var parent = new StackPanel();
            var child = new TextBlock();
            parent.Children.Add(child);
            var style = new Style(typeof(StackPanel));
            style.Setters.Add(new Setter(TextBlock.FontSizeProperty, 30.0));
            parent.Style = style;
            Assert.That(child.FontSize, Is.EqualTo(30));
            child.SetLayerValue(TextBlock.FontSizeProperty, ValueLayer.DefaultStyle, 20.0);
            var childStyle = new Style(typeof(TextBlock));
            childStyle.Setters.Add(new Setter(TextBlock.FontSizeProperty, 40.0));
            child.Style = childStyle;
            Assert.That(child.FontSize, Is.EqualTo(40));
            child.Style = null;
            Assert.That(child.FontSize, Is.EqualTo(20));
            child.ClearLayerValue(TextBlock.FontSizeProperty, ValueLayer.DefaultStyle);
            Assert.That(child.FontSize, Is.EqualTo(30));
            parent.Style = null;
            Assert.That(child.FontSize, Is.EqualTo(12));
        }

        [Test]
        public void EffectiveStyleChangesApplyEvenWhenStoredInALayer()
        {
            var implicitStyle = new Style(typeof(FrameworkElement));
            implicitStyle.Setters.Add(new Setter(FrameworkElement.WidthProperty, 10.0));
            var explicitStyle = new Style(typeof(FrameworkElement));
            explicitStyle.Setters.Add(new Setter(FrameworkElement.WidthProperty, 20.0));
            var element = new FrameworkElement();
            element.SetLayerValue(FrameworkElement.StyleProperty, ValueLayer.ImplicitStyleReference, implicitStyle);
            Assert.That(element.Width, Is.EqualTo(10));
            element.Style = explicitStyle;
            Assert.That(element.Width, Is.EqualTo(20));
            element.ClearValue(FrameworkElement.StyleProperty);
            Assert.That(element.Style, Is.SameAs(implicitStyle));
            Assert.That(element.Width, Is.EqualTo(10));
            element.Style = null;
            Assert.That(element.Width, Is.NaN);
        }

        [Test]
        public void TargetTypeMismatchLeavesNoStyleValuesAndBaseTargetAcceptsDerivedElement()
        {
            var old = new Style(typeof(FrameworkElement));
            old.Setters.Add(new Setter(FrameworkElement.HeightProperty, 50.0));
            var element = new TextBlock { Style = old };
            var wrong = new Style(typeof(StackPanel));
            wrong.Setters.Add(new Setter(FrameworkElement.WidthProperty, 100.0));
            AssertError(() => element.Style = wrong,
                "'StackPanel' TargetType does not match type of element 'TextBlock'.");
            Assert.That(element.Width, Is.NaN);
            Assert.That(element.Height, Is.NaN);
            Assert.That(wrong.IsSealed, Is.True);
            element.Style = old;
            Assert.That(element.Height, Is.EqualTo(50));
        }

        [Test]
        public void StyleWidthInvalidatesMeasureAndStyleMetadataMatchesContract()
        {
            var element = new FrameworkElement();
            var root = new UIRoot(element);
            var viewport = new Size(300, 200);
            root.UpdateLayout(viewport);
            var style = new Style(typeof(FrameworkElement));
            style.Setters.Add(new Setter(FrameworkElement.WidthProperty, 100.0));
            element.Style = style;
            Assert.That(element.IsMeasureValid, Is.False);
            root.UpdateLayout(viewport);
            Assert.That(element.RenderSize.Width, Is.EqualTo(100));
            element.Style = style;
            Assert.That(element.IsMeasureValid, Is.True);
            element.Style = null;
            Assert.That(element.IsMeasureValid, Is.False);
            var metadata = (FrameworkPropertyMetadata)FrameworkElement.StyleProperty.DefaultMetadata;
            Assert.That(metadata.DefaultValue, Is.Null);
            Assert.That(metadata.AffectsMeasure, Is.True);
            Assert.That(FrameworkElement.StyleProperty.PropertyType, Is.EqualTo(typeof(Style)));
        }

        [Test]
        public void SealReportsMissingTargetInvalidBaseAndCycles()
        {
            AssertError(new Style().Seal, "Style must have a TargetType.");
            AssertError(new Style(typeof(TextBlock), new Style(typeof(StackPanel))).Seal,
                "Can only base on a Style with target type that is base type 'TextBlock'.");
            var first = new Style(typeof(FrameworkElement));
            var second = new Style(typeof(FrameworkElement), first);
            first.BasedOn = second;
            AssertError(first.Seal, "Style BasedOn chain contains a cycle.");
            Assert.That(first.IsSealed || second.IsSealed, Is.False);
            first.BasedOn = first;
            AssertError(first.Seal, "Style BasedOn chain contains a cycle.");
            first.BasedOn = null;
            Assert.DoesNotThrow(second.Seal);
        }

        [Test]
        public void SealRejectsMissingPropertyStylePropertyAndAllUnsupportedValues()
        {
            var missing = new Style(typeof(FrameworkElement));
            missing.Setters.Add(new Setter());
            AssertError(missing.Seal, "Setter.Property must not be null.");
            var recursive = new Style(typeof(FrameworkElement));
            recursive.Setters.Add(new Setter(FrameworkElement.StyleProperty, null));
            AssertError(recursive.Seal,
                "Style object is not allowed to affect the Style property of the object to which it applies.");
            AssertInvalidSetter(FrameworkElement.WidthProperty, null);
            AssertInvalidSetter(FrameworkElement.WidthProperty, "wrong");
            AssertInvalidSetter(FrameworkElement.WidthProperty, 10);
            var expression = new FrameworkElement().SetBinding(FrameworkElement.NameProperty, new Binding());
            AssertInvalidSetter(FrameworkElement.DataContextProperty, expression);
            AssertInvalidSetter(FrameworkElement.DataContextProperty, new Binding());
            AssertInvalidSetter(FrameworkElement.DataContextProperty, DependencyProperty.UnsetValue);
            var nullable = new Style { TargetType = typeof(FrameworkElement) };
            nullable.Setters.Add(new Setter { Property = FrameworkElement.DataContextProperty, Value = null });
            Assert.DoesNotThrow(nullable.Seal);
        }

        [Test]
        public void SealedStylesCollectionsAndSettersRejectEveryMutation()
        {
            var setter = new Setter(FrameworkElement.WidthProperty, 10.0);
            var style = new Style(typeof(FrameworkElement));
            style.Setters.Add(setter);
            style.Seal();
            AssertError(() => style.TargetType = typeof(TextBlock), "Cannot modify a sealed Style.");
            AssertError(() => style.BasedOn = new Style(), "Cannot modify a sealed Style.");
            AssertError(() => style.Setters.Add(new Setter()), "Cannot modify a sealed Style.");
            AssertError(() => style.Setters.Insert(0, new Setter()), "Cannot modify a sealed Style.");
            AssertError(() => style.Setters[0] = new Setter(), "Cannot modify a sealed Style.");
            AssertError(() => style.Setters.Remove(setter), "Cannot modify a sealed Style.");
            AssertError(() => style.Setters.RemoveAt(0), "Cannot modify a sealed Style.");
            AssertError(style.Setters.Clear, "Cannot modify a sealed Style.");
            AssertError(() => setter.Property = FrameworkElement.HeightProperty, "Cannot modify a sealed Setter.");
            AssertError(() => setter.Value = 20.0, "Cannot modify a sealed Setter.");
        }

        [Test]
        public void MutableCollectionsRejectNullAndSupportReplacingAndRemovingSetters()
        {
            var collection = new SetterBaseCollection();
            Assert.Throws<ArgumentNullException>(() => collection.Add(null));
            collection.Add(new Setter());
            Assert.Throws<ArgumentNullException>(() => collection[0] = null);
            var setter = new Setter { Property = FrameworkElement.WidthProperty, Value = 20.0 };
            collection[0] = setter;
            Assert.That(collection[0], Is.SameAs(setter));
            collection.RemoveAt(0);
            collection.Insert(0, setter);
            collection.Clear();
            Assert.That(collection, Is.Empty);
        }

        [Test]
        public void ReplacementPreservesSharedValuesAndNotifiesOnlyAddedAndRemovedProperties()
        {
            var first = new Style(typeof(TextBlock));
            first.Setters.Add(new Setter(FrameworkElement.WidthProperty, 100.0));
            first.Setters.Add(new Setter(FrameworkElement.HeightProperty, 50.0));
            var second = new Style(typeof(TextBlock));
            second.Setters.Add(new Setter(FrameworkElement.WidthProperty, 100.0));
            second.Setters.Add(new Setter(FrameworkElement.MinWidthProperty, 20.0));
            var element = new TrackingTextBlock { Style = first };
            new UIRoot(element).UpdateLayout(new Size(300, 200));
            Assert.That(element.IsMeasureValid, Is.True);
            element.Changes.Clear();

            element.Style = second;

            Assert.That(element.Changes.FindAll(e => e.Property == FrameworkElement.WidthProperty), Is.Empty);
            AssertSingleChange(element, FrameworkElement.HeightProperty, 50.0, double.NaN);
            AssertSingleChange(element, FrameworkElement.MinWidthProperty, 0.0, 20.0);
            Assert.That(element.Changes.Count, Is.EqualTo(3)); // Includes StyleProperty itself.
            Assert.That(element.Width, Is.EqualTo(100));
            Assert.That(element.Height, Is.NaN);
            Assert.That(element.MinWidth, Is.EqualTo(20));
        }

        [Test]
        public void EqualReplacementDoesNotInvalidateMeasureThroughSetters()
        {
            var basis = new Style(typeof(TextBlock));
            basis.Setters.Add(new Setter(FrameworkElement.WidthProperty, 100.0));
            var first = new Style(typeof(TextBlock), basis);
            var second = new Style(typeof(TextBlock), basis);
            var element = new TrackingTextBlock { Style = first };
            new UIRoot(element).UpdateLayout(new Size(300, 200));
            element.Changes.Clear();

            element.Style = second;

            Assert.That(element.Changes.Count, Is.EqualTo(1));
            Assert.That(element.Changes[0].Property, Is.SameAs(FrameworkElement.StyleProperty));
            // Setter callbacks run before StyleProperty's own AffectsMeasure invalidation.
            Assert.That(element.MeasureValidBeforeStyleChange, Is.True);
            Assert.That(element.IsMeasureValid, Is.False);
        }

        [Test]
        public void ReplacementWithDifferentValueNotifiesOnceWithFinalOldAndNewValues()
        {
            var first = new Style(typeof(TextBlock));
            first.Setters.Add(new Setter(FrameworkElement.WidthProperty, 100.0));
            var second = new Style(typeof(TextBlock));
            second.Setters.Add(new Setter(FrameworkElement.WidthProperty, 200.0));
            var element = new TrackingTextBlock { Style = first };
            new UIRoot(element).UpdateLayout(new Size(300, 200));
            element.Changes.Clear();

            element.Style = second;

            AssertSingleChange(element, FrameworkElement.WidthProperty, 100.0, 200.0);
            Assert.That(element.Changes.Count, Is.EqualTo(2));
            Assert.That(element.IsMeasureValid, Is.False);
        }

        [Test]
        public void EqualInheritedStyleReplacementDoesNotNotifyOrInvalidateChild()
        {
            var first = new Style(typeof(StackPanel));
            first.Setters.Add(new Setter(TextBlock.FontSizeProperty, 24.0));
            var second = new Style(typeof(StackPanel));
            second.Setters.Add(new Setter(TextBlock.FontSizeProperty, 24.0));
            var parent = new StackPanel { Style = first };
            var child = new TrackingTextBlock();
            parent.Children.Add(child);
            new UIRoot(parent).UpdateLayout(new Size(300, 200));
            child.Changes.Clear();

            parent.Style = second;

            Assert.That(child.Changes, Is.Empty);
            Assert.That(child.FontSize, Is.EqualTo(24));
            Assert.That(child.IsMeasureValid, Is.True);
        }

        private static void AssertSingleChange(TrackingTextBlock element, DependencyProperty property,
            object oldValue, object newValue)
        {
            var changes = element.Changes.FindAll(e => e.Property == property);
            Assert.That(changes.Count, Is.EqualTo(1));
            Assert.That(changes[0].OldValue, Is.EqualTo(oldValue));
            Assert.That(changes[0].NewValue, Is.EqualTo(newValue));
        }

        private sealed class TrackingTextBlock : TextBlock
        {
            public List<DependencyPropertyChangedEventArgs> Changes { get; } = new List<DependencyPropertyChangedEventArgs>();
            public bool MeasureValidBeforeStyleChange { get; private set; }

            protected override void OnPropertyChanged(DependencyPropertyChangedEventArgs e)
            {
                if (e.Property == StyleProperty)
                    MeasureValidBeforeStyleChange = IsMeasureValid;
                base.OnPropertyChanged(e);
                Changes.Add(e);
            }
        }

        private static void AssertInvalidSetter(DependencyProperty property, object value)
        {
            var style = new Style(typeof(FrameworkElement));
            style.Setters.Add(new Setter(property, value));
            AssertError(style.Seal, $"'{value}' is not a valid value for the '{property.Name}' property on a Setter.");
        }

        private static void AssertError(TestDelegate action, string message) =>
            Assert.That(Assert.Throws<InvalidOperationException>(action).Message, Is.EqualTo(message));
    }
}
