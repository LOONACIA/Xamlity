using System;
using System.Collections.Generic;
using NUnit.Framework;
using Xamlity.Layout;
using Xamlity.Properties;
using Xamlity.Tree;

namespace Xamlity.Core.Tests.Tree
{
    public class LayoutTests
    {
        private static readonly Size Viewport = new Size(400, 300);
        private static readonly DependencyProperty InheritedSizeProperty = DependencyProperty.Register(
            "InheritedSize", typeof(double), typeof(LayoutTests), new FrameworkPropertyMetadata(0.0,
                FrameworkPropertyMetadataOptions.Inherits | FrameworkPropertyMetadataOptions.AffectsMeasure));

        [Test]
        public void DefaultsMatchFrameworkElement()
        {
            var element = new FrameworkElement();
            Assert.That(double.IsNaN(element.Width) && double.IsNaN(element.Height), Is.True);
            Assert.That(element.MinWidth, Is.Zero);
            Assert.That(element.MinHeight, Is.Zero);
            Assert.That(element.MaxWidth, Is.EqualTo(double.PositiveInfinity));
            Assert.That(element.MaxHeight, Is.EqualTo(double.PositiveInfinity));
            Assert.That(element.Margin, Is.EqualTo(new Thickness(0)));
            Assert.That(element.HorizontalAlignment, Is.EqualTo(HorizontalAlignment.Stretch));
            Assert.That(element.VerticalAlignment, Is.EqualTo(VerticalAlignment.Stretch));
            Assert.That(element.Name, Is.Empty);
            Assert.That(element.IsMeasureValid || element.IsArrangeValid, Is.False);
            new UIRoot(element).UpdateLayout(Viewport);
            Assert.That(element.DesiredSize, Is.EqualTo(new Size(0, 0)));
            Assert.That(element.RenderSize, Is.EqualTo(Viewport));
        }

        [TestCase(HorizontalAlignment.Left, VerticalAlignment.Top, 0, 0)]
        [TestCase(HorizontalAlignment.Center, VerticalAlignment.Center, 150, 125)]
        [TestCase(HorizontalAlignment.Right, VerticalAlignment.Bottom, 300, 250)]
        [TestCase(HorizontalAlignment.Stretch, VerticalAlignment.Stretch, 150, 125)]
        public void ExplicitSizeUsesAlignment(HorizontalAlignment horizontal, VerticalAlignment vertical,
            double x, double y)
        {
            var element = new TestElement { Width = 100, Height = 50,
                HorizontalAlignment = horizontal, VerticalAlignment = vertical };
            new UIRoot(element).UpdateLayout(Viewport);
            Assert.That(element.MeasureConstraint, Is.EqualTo(new Size(100, 50)));
            Assert.That(element.DesiredSize, Is.EqualTo(new Size(100, 50)));
            Assert.That(element.RenderSize, Is.EqualTo(new Size(100, 50)));
            Assert.That(element.VisualOffset, Is.EqualTo(new Point(x, y)));
        }

        [Test]
        public void StretchSubtractsMargin()
        {
            var element = new TestElement { Margin = new Thickness(10) };
            new UIRoot(element).UpdateLayout(Viewport);
            Assert.That(element.MeasureConstraint, Is.EqualTo(new Size(380, 280)));
            Assert.That(element.DesiredSize, Is.EqualTo(new Size(20, 20)));
            Assert.That(element.VisualOffset, Is.EqualTo(new Point(10, 10)));
            Assert.That(element.RenderSize, Is.EqualTo(new Size(380, 280)));
        }

        [Test]
        public void AsymmetricMarginsAndFinalOriginAreIncludedInOffset()
        {
            var element = new TestElement { Width = 100, Height = 50,
                Margin = new Thickness(10, 20, 30, 40),
                HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Bottom };
            element.Measure(Viewport);
            element.Arrange(new Rect(7, 11, 400, 300));
            Assert.That(element.DesiredSize, Is.EqualTo(new Size(140, 110)));
            Assert.That(element.VisualOffset, Is.EqualTo(new Point(277, 221)));
            Assert.That(element.RenderSize, Is.EqualTo(new Size(100, 50)));
        }

        [TestCase(double.NaN, 80, 200, 80)]
        [TestCase(20, 80, 200, 80)]
        [TestCase(300, 80, 200, 200)]
        [TestCase(100, 150, 60, 150)]
        public void MinimumWinsOverMaximumAndExplicitSize(double length, double minimum, double maximum, double expected)
        {
            var element = new TestElement { Width = length, Height = length,
                MinWidth = minimum, MinHeight = minimum, MaxWidth = maximum, MaxHeight = maximum,
                HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top };
            new UIRoot(element).UpdateLayout(Viewport);
            Assert.That(element.DesiredSize, Is.EqualTo(new Size(expected, expected)));
            Assert.That(element.RenderSize, Is.EqualTo(new Size(expected, expected)));
        }

        [Test]
        public void MaximumLimitsStretch()
        {
            var element = new TestElement { MaxWidth = 120, MaxHeight = 70 };
            new UIRoot(element).UpdateLayout(Viewport);
            Assert.That(element.MeasureConstraint, Is.EqualTo(new Size(120, 70)));
            Assert.That(element.RenderSize, Is.EqualTo(new Size(120, 70)));
            Assert.That(element.VisualOffset, Is.EqualTo(new Point(140, 115)));
        }

        [Test]
        public void AutoSizeUsesContentAndSupportsInfiniteMeasureConstraint()
        {
            var element = new TestElement { ContentSize = new Size(73, 29),
                HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
            element.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            element.Arrange(new Rect(0, 0, 400, 300));
            Assert.That(element.DesiredSize, Is.EqualTo(new Size(73, 29)));
            Assert.That(element.RenderSize, Is.EqualTo(new Size(73, 29)));
            Assert.That(element.VisualOffset, Is.EqualTo(new Point(163.5, 135.5)));
        }

        [TestCase(HorizontalAlignment.Stretch, VerticalAlignment.Stretch, 0, 0)]
        [TestCase(HorizontalAlignment.Center, VerticalAlignment.Center, -50, -25)]
        [TestCase(HorizontalAlignment.Right, VerticalAlignment.Bottom, -100, -50)]
        public void SmallerSlotPreservesUnclippedContentSize(HorizontalAlignment horizontal,
            VerticalAlignment vertical, double x, double y)
        {
            var element = new TestElement { ContentSize = new Size(500, 350),
                HorizontalAlignment = horizontal, VerticalAlignment = vertical };
            new UIRoot(element).UpdateLayout(Viewport);
            Assert.That(element.DesiredSize, Is.EqualTo(Viewport));
            Assert.That(element.RenderSize, Is.EqualTo(new Size(500, 350)));
            Assert.That(element.VisualOffset, Is.EqualTo(new Point(x, y)));
        }

        [Test]
        public void MaximumClampsDesiredSizeButPreservesOversizedOverrideResult()
        {
            var element = new TestElement { ContentSize = new Size(200, 100), MaxWidth = 120, MaxHeight = 70 };
            new UIRoot(element).UpdateLayout(Viewport);
            Assert.That(element.DesiredSize, Is.EqualTo(new Size(120, 70)));
            Assert.That(element.RenderSize, Is.EqualTo(new Size(200, 100)));
            Assert.That(element.VisualOffset, Is.EqualTo(new Point(140, 115)));
        }

        [Test]
        public void ArrangeUsesOverrideResultForRenderSizeAndAlignment()
        {
            var element = new TestElement { ArrangedSize = new Size(60, 30) };
            new UIRoot(element).UpdateLayout(Viewport);
            Assert.That(element.ArrangeConstraint, Is.EqualTo(Viewport));
            Assert.That(element.RenderSize, Is.EqualTo(new Size(60, 30)));
            Assert.That(element.VisualOffset, Is.EqualTo(new Point(170, 135)));
        }

        [TestCase(250, 400, 40, 250)]
        [TestCase(-10, 20, 420, -10)]
        public void MarginsCanExceedSlotOrBeNegative(double margin, double desired, double render, double offset)
        {
            var element = new TestElement { ContentSize = new Size(40, 40), Margin = new Thickness(margin) };
            new UIRoot(element).UpdateLayout(new Size(400, 400));
            Assert.That(element.MeasureConstraint, Is.EqualTo(new Size(Math.Max(0, 400 - 2 * margin), Math.Max(0, 400 - 2 * margin))));
            // Oversized margins constrain the reported size, but do not shrink the content.
            Assert.That(element.DesiredSize, Is.EqualTo(new Size(desired, desired)));
            Assert.That(element.RenderSize, Is.EqualTo(new Size(render, render)));
            Assert.That(element.VisualOffset, Is.EqualTo(new Point(offset, offset)));
        }

        [Test]
        public void UnchangedLayoutIsCachedAndViewportChangesRemeasure()
        {
            var child = new TestElement();
            var parent = new Overlay();
            parent.Add(child);
            var root = new UIRoot(parent);
            Assert.That(root.RootElement, Is.SameAs(parent));
            root.UpdateLayout(Viewport);
            root.UpdateLayout(Viewport);
            AssertCounts(parent, 1, 1);
            AssertCounts(child, 1, 1);
            root.UpdateLayout(new Size(500, 400));
            AssertCounts(parent, 2, 2);
            AssertCounts(child, 2, 2);
        }

        [Test]
        public void WidthChangeInvalidatesOnlyChildAndAncestors()
        {
            var child = new TestElement();
            var sibling = new TestElement();
            var parent = new Overlay();
            var ancestor = new Overlay();
            parent.Add(child);
            parent.Add(sibling);
            ancestor.Add(parent);
            var root = new UIRoot(ancestor);
            root.UpdateLayout(Viewport);
            child.Width = 100;
            foreach (var element in new TestElement[] { child, parent, ancestor })
            {
                Assert.That(element.IsMeasureValid, Is.False);
                Assert.That(element.IsArrangeValid, Is.False);
            }
            Assert.That(sibling.IsMeasureValid && sibling.IsArrangeValid, Is.True);
            root.UpdateLayout(Viewport);
            AssertCounts(child, 2, 2);
            AssertCounts(parent, 2, 2);
            AssertCounts(ancestor, 2, 2);
            AssertCounts(sibling, 1, 1);
            Assert.That(child.RenderSize.Width, Is.EqualTo(100));
        }

        [Test]
        public void AlignmentInvalidatesArrangeOnlyAndNameDoesNotInvalidate()
        {
            var child = new TestElement { Width = 100 };
            var parent = new Overlay();
            parent.Add(child);
            var root = new UIRoot(parent);
            root.UpdateLayout(Viewport);
            child.HorizontalAlignment = HorizontalAlignment.Right;
            Assert.That(child.IsMeasureValid && parent.IsMeasureValid, Is.True);
            Assert.That(child.IsArrangeValid || parent.IsArrangeValid, Is.False);
            root.UpdateLayout(Viewport);
            AssertCounts(child, 1, 2);
            AssertCounts(parent, 1, 2);
            Assert.That(child.VisualOffset.X, Is.EqualTo(300));
            child.Name = "Content";
            child.Width = 100;
            root.UpdateLayout(Viewport);
            AssertCounts(child, 1, 2);
            AssertCounts(parent, 1, 2);
        }

        [TestCase(nameof(FrameworkElement.Width))]
        [TestCase(nameof(FrameworkElement.Height))]
        [TestCase(nameof(FrameworkElement.MinWidth))]
        [TestCase(nameof(FrameworkElement.MinHeight))]
        [TestCase(nameof(FrameworkElement.MaxWidth))]
        [TestCase(nameof(FrameworkElement.MaxHeight))]
        [TestCase(nameof(FrameworkElement.Margin))]
        public void SettingAndClearingLayoutPropertiesInvalidatesMeasure(string name)
        {
            var property = (DependencyProperty)typeof(FrameworkElement).GetField(name + "Property").GetValue(null);
            var element = new TestElement();
            var root = new UIRoot(element);
            root.UpdateLayout(Viewport);
            object value = name == nameof(FrameworkElement.Margin) ? (object)new Thickness(10) : 100.0;
            element.SetValue(property, value);
            Assert.That(element.IsMeasureValid || element.IsArrangeValid, Is.False);
            root.UpdateLayout(Viewport);
            AssertCounts(element, 2, 2);
            element.ClearValue(property);
            Assert.That(element.IsMeasureValid || element.IsArrangeValid, Is.False);
            root.UpdateLayout(Viewport);
            AssertCounts(element, 3, 3);
        }

        [Test]
        public void ArrangeMeasuresWhenNeededAndChangedOriginOnlyRearranges()
        {
            var element = new TestElement();
            element.Arrange(new Rect(0, 0, 400, 300));
            AssertCounts(element, 1, 1);
            element.Arrange(new Rect(10, 20, 400, 300));
            AssertCounts(element, 1, 2);
            Assert.That(element.VisualOffset, Is.EqualTo(new Point(10, 20)));
            element.InvalidateMeasure();
            element.Arrange(new Rect(10, 20, 400, 300));
            AssertCounts(element, 2, 3);
            Assert.That(element.MeasureConstraint, Is.EqualTo(Viewport));
        }

        [Test]
        public void VisualTreeChangesInvalidateLayoutAndUpdateInheritedValues()
        {
            var first = new Overlay();
            var second = new Overlay();
            var child = new Overlay();
            var grandchild = new TestElement();
            child.Add(grandchild);
            first.SetValue(InheritedSizeProperty, 10.0);
            second.SetValue(InheritedSizeProperty, 20.0);
            var root = new UIRoot(first);
            root.UpdateLayout(Viewport);
            first.Add(child);
            Assert.That(first.IsMeasureValid, Is.False);
            Assert.That(child.VisualParent, Is.SameAs(first));
            Assert.That(grandchild.GetValue(InheritedSizeProperty), Is.EqualTo(10.0));
            root.UpdateLayout(Viewport);
            first.SetValue(InheritedSizeProperty, 15.0);
            Assert.That(grandchild.IsMeasureValid, Is.False);
            Assert.That(grandchild.GetValue(InheritedSizeProperty), Is.EqualTo(15.0));
            root.UpdateLayout(Viewport);
            first.Remove(child);
            Assert.That(first.IsMeasureValid, Is.False);
            Assert.That(child.VisualParent, Is.Null);
            Assert.That(grandchild.GetValue(InheritedSizeProperty), Is.EqualTo(0.0));
            second.Add(child);
            Assert.That(grandchild.GetValue(InheritedSizeProperty), Is.EqualTo(20.0));
            Assert.That(grandchild.InheritedChanges, Is.EqualTo(new[] { 10.0, 15.0, 0.0, 20.0 }));
            root.UpdateLayout(Viewport);
            Assert.That(first.DesiredSize, Is.EqualTo(new Size(0, 0)));
        }

        [Test]
        public void VisualTreeRejectsMultipleParentsAndCycles()
        {
            var parent = new Overlay();
            var child = new Overlay();
            var other = new Overlay();
            parent.Add(child);
            Assert.Throws<InvalidOperationException>(() => other.Add(child));
            Assert.Throws<InvalidOperationException>(() => child.Add(parent));
            Assert.Throws<InvalidOperationException>(() => parent.Add(parent));
            Assert.Throws<InvalidOperationException>(() => other.Remove(child));
            Assert.That(child.VisualParent, Is.SameAs(parent));
            Assert.That(parent.VisualParent, Is.Null);
        }

        private static void AssertCounts(TestElement element, int measure, int arrange)
        {
            Assert.That(element.MeasureCount, Is.EqualTo(measure));
            Assert.That(element.ArrangeCount, Is.EqualTo(arrange));
        }

        private class TestElement : FrameworkElement
        {
            public Size ContentSize { get; set; }
            public Size? ArrangedSize { get; set; }
            public int MeasureCount { get; private set; }
            public int ArrangeCount { get; private set; }
            public Size MeasureConstraint { get; private set; }
            public Size ArrangeConstraint { get; private set; }
            public List<double> InheritedChanges { get; } = new List<double>();

            protected override Size MeasureOverride(Size availableSize)
            {
                MeasureCount++;
                MeasureConstraint = availableSize;
                return ContentSize;
            }

            protected override Size ArrangeOverride(Size finalSize)
            {
                ArrangeCount++;
                ArrangeConstraint = finalSize;
                return ArrangedSize ?? finalSize;
            }

            protected override void OnPropertyChanged(DependencyPropertyChangedEventArgs e)
            {
                base.OnPropertyChanged(e);
                if (e.Property == InheritedSizeProperty)
                    InheritedChanges.Add((double)e.NewValue);
            }
        }

        private sealed class Overlay : TestElement
        {
            private readonly List<UIElement> children = new List<UIElement>();

            public void Add(UIElement child)
            {
                AddVisualChild(child);
                children.Add(child);
            }

            public void Remove(UIElement child)
            {
                RemoveVisualChild(child);
                children.Remove(child);
            }

            protected override int VisualChildrenCount => children.Count;
            protected override UIElement GetVisualChild(int index) => children[index];

            protected override Size MeasureOverride(Size availableSize)
            {
                base.MeasureOverride(availableSize);
                double width = 0;
                double height = 0;
                foreach (UIElement child in children)
                {
                    child.Measure(availableSize);
                    width = Math.Max(width, child.DesiredSize.Width);
                    height = Math.Max(height, child.DesiredSize.Height);
                }
                return new Size(width, height);
            }

            protected override Size ArrangeOverride(Size finalSize)
            {
                base.ArrangeOverride(finalSize);
                foreach (UIElement child in children)
                    child.Arrange(new Rect(new Point(0, 0), finalSize));
                return finalSize;
            }
        }
    }
}
