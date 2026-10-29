using System;
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using Xamlity.Layout;
using Xamlity.Properties;
using Xamlity.Tree;

namespace Xamlity.Core.Tests.Layout
{
    public class StackPanelTests
    {
        private static readonly Size Viewport = new Size(200, 100);
        private static readonly DependencyProperty InheritedSizeProperty = DependencyProperty.Register(
            "StackInheritedSize", typeof(double), typeof(StackPanelTests), new FrameworkPropertyMetadata(0.0,
                FrameworkPropertyMetadataOptions.Inherits | FrameworkPropertyMetadataOptions.AffectsMeasure));

        [TestCase(Orientation.Vertical, 40, 45, 0, 20)]
        [TestCase(Orientation.Horizontal, 70, 25, 30, 0)]
        public void StacksChildrenAndMeasuresWithInfinityAlongStack(Orientation orientation,
            double width, double height, double secondX, double secondY)
        {
            var panel = new StackPanel { Orientation = orientation };
            var first = new TestElement { ContentSize = new Size(30, 20) };
            var second = new TestElement { ContentSize = new Size(40, 25) };
            panel.Children.Add(first);
            panel.Children.Add(second);
            new UIRoot(panel).UpdateLayout(Viewport);

            bool horizontal = orientation == Orientation.Horizontal;
            var constraint = horizontal ? new Size(double.PositiveInfinity, 100) : new Size(200, double.PositiveInfinity);
            Assert.That(first.MeasureConstraint, Is.EqualTo(constraint));
            Assert.That(second.MeasureConstraint, Is.EqualTo(constraint));
            Assert.That(panel.DesiredSize, Is.EqualTo(new Size(width, height)));
            Assert.That(first.VisualOffset, Is.EqualTo(new Point(0, 0)));
            Assert.That(second.VisualOffset, Is.EqualTo(new Point(secondX, secondY)));
            Assert.That(first.RenderSize, Is.EqualTo(horizontal ? new Size(30, 100) : new Size(200, 20)));
            Assert.That(second.RenderSize, Is.EqualTo(horizontal ? new Size(40, 100) : new Size(200, 25)));
        }

        [TestCase(Orientation.Vertical)]
        [TestCase(Orientation.Horizontal)]
        public void CrossAxisAlignmentAndMarginsApplyInsideEachSlot(Orientation orientation)
        {
            var panel = new StackPanel { Orientation = orientation };
            var first = new TestElement { ContentSize = new Size(30, 20), Margin = new Thickness(10, 5, 20, 15),
                HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
            var second = new TestElement { ContentSize = new Size(40, 25) };
            panel.Children.Add(first);
            panel.Children.Add(second);
            new UIRoot(panel).UpdateLayout(Viewport);

            bool horizontal = orientation == Orientation.Horizontal;
            Assert.That(first.DesiredSize, Is.EqualTo(new Size(60, 40)));
            Assert.That(first.RenderSize, Is.EqualTo(new Size(30, 20)));
            Assert.That(first.VisualOffset, Is.EqualTo(horizontal ? new Point(10, 35) : new Point(80, 5)));
            Assert.That(second.VisualOffset, Is.EqualTo(horizontal ? new Point(60, 0) : new Point(0, 40)));
            Assert.That(panel.DesiredSize, Is.EqualTo(horizontal ? new Size(100, 40) : new Size(60, 65)));
        }

        [Test]
        public void EmptyPanelHasZeroDesiredSizeAndDefaultsToVertical()
        {
            var panel = new StackPanel();
            new UIRoot(panel).UpdateLayout(Viewport);
            Assert.That(panel.Orientation, Is.EqualTo(Orientation.Vertical));
            Assert.That(panel.DesiredSize, Is.EqualTo(new Size(0, 0)));
            Assert.That(panel.RenderSize, Is.EqualTo(Viewport));
            Assert.That(panel.Children.Count, Is.Zero);
        }

        [Test]
        public void AddInsertAndRemoveInvalidateAncestorsAndRelayoutInOrder()
        {
            var panel = new StackPanel();
            var ancestor = new StackPanel();
            ancestor.Children.Add(panel);
            var root = new UIRoot(ancestor);
            root.UpdateLayout(Viewport);
            var first = new TestElement { ContentSize = new Size(30, 20) };
            var second = new TestElement { ContentSize = new Size(40, 25) };
            panel.Children.Add(first);
            AssertInvalid(panel);
            AssertInvalid(ancestor);
            Assert.That(first.VisualParent, Is.SameAs(panel));
            root.UpdateLayout(Viewport);
            Assert.That(panel.DesiredSize, Is.EqualTo(new Size(30, 20)));

            panel.Children.Insert(0, second);
            AssertInvalid(panel);
            AssertInvalid(ancestor);
            Assert.That(second.VisualParent, Is.SameAs(panel));
            root.UpdateLayout(Viewport);
            Assert.That(first.VisualOffset.Y, Is.EqualTo(25));
            Assert.That(second.VisualOffset.Y, Is.Zero);
            Assert.That(panel.DesiredSize, Is.EqualTo(new Size(40, 45)));

            Assert.That(panel.Children.Remove(second), Is.True);
            AssertInvalid(panel);
            AssertInvalid(ancestor);
            Assert.That(second.VisualParent, Is.Null);
            root.UpdateLayout(Viewport);
            Assert.That(first.VisualOffset.Y, Is.Zero);
            Assert.That(panel.DesiredSize, Is.EqualTo(new Size(30, 20)));
            Assert.That(panel.Children.Remove(second), Is.False);
            Assert.That(panel.IsMeasureValid && panel.IsArrangeValid, Is.True);

            panel.Children.RemoveAt(0);
            AssertInvalid(panel);
            Assert.That(first.VisualParent, Is.Null);
            root.UpdateLayout(Viewport);
            Assert.That(panel.DesiredSize, Is.EqualTo(new Size(0, 0)));
        }

        [Test]
        public void ReplacementAndClearUpdateParentsAndLayout()
        {
            var panel = new StackPanel();
            var first = new TestElement { ContentSize = new Size(30, 20) };
            var second = new TestElement { ContentSize = new Size(40, 25) };
            var replacement = new TestElement { ContentSize = new Size(50, 10) };
            panel.Children.Add(first);
            panel.Children.Add(second);
            var root = new UIRoot(panel);
            root.UpdateLayout(Viewport);

            panel.Children[0] = replacement;
            AssertInvalid(panel);
            Assert.That(first.VisualParent, Is.Null);
            Assert.That(replacement.VisualParent, Is.SameAs(panel));
            Assert.That(panel.Children[0], Is.SameAs(replacement));
            root.UpdateLayout(Viewport);
            Assert.That(second.VisualOffset.Y, Is.EqualTo(10));
            Assert.That(panel.DesiredSize, Is.EqualTo(new Size(50, 35)));
            panel.Children[0] = replacement;
            Assert.That(panel.Children.Count, Is.EqualTo(2));
            Assert.That(replacement.VisualParent, Is.SameAs(panel));

            panel.Children.Clear();
            AssertInvalid(panel);
            Assert.That(replacement.VisualParent, Is.Null);
            Assert.That(second.VisualParent, Is.Null);
            Assert.That(panel.Children.Count, Is.Zero);
            root.UpdateLayout(Viewport);
            Assert.That(panel.DesiredSize, Is.EqualTo(new Size(0, 0)));
        }

        [Test]
        public void InvalidMutationsPreserveCollectionAndParents()
        {
            var panel = new StackPanel();
            var other = new StackPanel();
            var first = new TestElement();
            var second = new TestElement();
            var unattached = new TestElement();
            panel.Children.Add(first);
            other.Children.Add(second);
            Assert.Throws<ArgumentNullException>(() => panel.Children.Add(null));
            Assert.Throws<ArgumentNullException>(() => panel.Children.Insert(0, null));
            Assert.Throws<ArgumentNullException>(() => panel.Children[0] = null);
            Assert.Throws<ArgumentOutOfRangeException>(() => panel.Children.Insert(-1, unattached));
            Assert.Throws<ArgumentOutOfRangeException>(() => panel.Children.Insert(2, unattached));
            Assert.Throws<ArgumentOutOfRangeException>(() => panel.Children[1] = unattached);
            Assert.Throws<ArgumentOutOfRangeException>(() => panel.Children.RemoveAt(1));
            Assert.Throws<InvalidOperationException>(() => panel.Children.Add(second));
            Assert.Throws<InvalidOperationException>(() => panel.Children.Insert(0, second));
            Assert.Throws<InvalidOperationException>(() => panel.Children[0] = second);
            Assert.Throws<InvalidOperationException>(() => panel.Children.Add(first));
            Assert.Throws<InvalidOperationException>(() => panel.Children.Add(panel));
            Assert.Throws<InvalidOperationException>(() => panel.Children[0] = panel);
            Assert.That(panel.Children.Count, Is.EqualTo(1));
            Assert.That(panel.Children[0], Is.SameAs(first));
            Assert.That(first.VisualParent, Is.SameAs(panel));
            Assert.That(second.VisualParent, Is.SameAs(other));
            Assert.That(unattached.VisualParent, Is.Null);
            Assert.That(panel.VisualParent, Is.Null);

            panel.Children.Add(other);
            Assert.Throws<InvalidOperationException>(() => other.Children.Add(panel));
            Assert.That(other.Children.Count, Is.EqualTo(1));
            Assert.That(panel.VisualParent, Is.Null);
        }

        [Test]
        public void OrientationChangeRemeasuresAndRearrangesChildren()
        {
            var panel = new StackPanel();
            var first = new TestElement { ContentSize = new Size(30, 20) };
            var second = new TestElement { ContentSize = new Size(40, 25) };
            panel.Children.Add(first);
            panel.Children.Add(second);
            var root = new UIRoot(panel);
            root.UpdateLayout(Viewport);
            root.UpdateLayout(Viewport);
            Assert.That(first.MeasureCount, Is.EqualTo(1));

            panel.Orientation = Orientation.Horizontal;
            AssertInvalid(panel);
            root.UpdateLayout(Viewport);
            Assert.That(first.MeasureCount, Is.EqualTo(2));
            Assert.That(second.MeasureCount, Is.EqualTo(2));
            Assert.That(first.MeasureConstraint, Is.EqualTo(new Size(double.PositiveInfinity, 100)));
            Assert.That(panel.DesiredSize, Is.EqualTo(new Size(70, 25)));
            Assert.That(second.VisualOffset, Is.EqualTo(new Point(30, 0)));

            panel.ClearValue(StackPanel.OrientationProperty);
            AssertInvalid(panel);
            root.UpdateLayout(Viewport);
            Assert.That(first.MeasureCount, Is.EqualTo(3));
            Assert.That(second.VisualOffset, Is.EqualTo(new Point(0, 20)));
        }

        [Test]
        public void VisualChildrenPropagateInheritanceThroughNestedPanelsAndReparenting()
        {
            var first = new StackPanel();
            var second = new StackPanel();
            var nested = new StackPanel();
            var child = new TestElement();
            nested.Children.Add(child);
            first.Children.Add(nested);
            var root = new UIRoot(first);
            root.UpdateLayout(Viewport);
            first.SetValue(InheritedSizeProperty, 10.0);
            Assert.That(child.GetValue(InheritedSizeProperty), Is.EqualTo(10.0));
            AssertInvalid(child);
            AssertInvalid(nested);
            root.UpdateLayout(Viewport);

            first.Children.Remove(nested);
            Assert.That(child.GetValue(InheritedSizeProperty), Is.EqualTo(0.0));
            AssertInvalid(child);
            second.SetValue(InheritedSizeProperty, 20.0);
            second.Children.Add(nested);
            Assert.That(nested.VisualParent, Is.SameAs(second));
            Assert.That(child.GetValue(InheritedSizeProperty), Is.EqualTo(20.0));
            Assert.That(child.InheritedChanges, Is.EqualTo(new[] { 10.0, 0.0, 20.0 }));
        }

        [Test]
        public void CollectionSupportsListQueriesCopyAndInterfaceEnumeration()
        {
            var panel = new StackPanel();
            IList<UIElement> children = panel.Children;
            var first = new TestElement();
            var second = new TestElement();
            children.Add(first);
            children.Insert(1, second);
            Assert.That(children.IsReadOnly, Is.False);
            Assert.That(children.Contains(first), Is.True);
            Assert.That(children.Contains(null), Is.False);
            Assert.That(children.IndexOf(second), Is.EqualTo(1));
            Assert.That(children.IndexOf(new TestElement()), Is.EqualTo(-1));
            var copy = new UIElement[3];
            children.CopyTo(copy, 1);
            Assert.That(copy, Is.EqualTo(new UIElement[] { null, first, second }));
            var generic = new List<UIElement>();
            foreach (UIElement child in children)
                generic.Add(child);
            var nonGeneric = new List<UIElement>();
            foreach (UIElement child in (IEnumerable)panel.Children)
                nonGeneric.Add(child);
            Assert.That(generic, Is.EqualTo(new UIElement[] { first, second }));
            Assert.That(nonGeneric, Is.EqualTo(generic));
        }

        [Test]
        public void ConcreteForeachUsesStructEnumeratorWithoutAllocating()
        {
            var children = new StackPanel().Children;
            children.Add(new TestElement());
            children.Add(new TestElement());
            Assert.That(children.GetEnumerator().GetType().IsValueType, Is.True);
            int visited = 0;
            for (int iteration = 0; iteration < 100; iteration++)
            {
                foreach (UIElement child in children)
                    if (child != null)
                        visited++;
            }

            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int iteration = 0; iteration < 1000; iteration++)
            {
                foreach (UIElement child in children)
                    if (child != null)
                        visited++;
            }
            long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
            Assert.That(visited, Is.EqualTo(2200));
            Assert.That(allocated, Is.Zero);
        }

        private static void AssertInvalid(UIElement element)
        {
            Assert.That(element.IsMeasureValid, Is.False);
            Assert.That(element.IsArrangeValid, Is.False);
        }

        private sealed class TestElement : FrameworkElement
        {
            public Size ContentSize { get; set; }
            public Size MeasureConstraint { get; private set; }
            public int MeasureCount { get; private set; }
            public List<double> InheritedChanges { get; } = new List<double>();

            protected override Size MeasureOverride(Size availableSize)
            {
                MeasureCount++;
                MeasureConstraint = availableSize;
                return ContentSize;
            }

            protected override void OnPropertyChanged(DependencyPropertyChangedEventArgs e)
            {
                base.OnPropertyChanged(e);
                if (e.Property == InheritedSizeProperty)
                    InheritedChanges.Add((double)e.NewValue);
            }
        }
    }
}
