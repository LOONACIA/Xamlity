using System;
using NUnit.Framework;
using Xamlity.Layout;
using Xamlity.Properties;
using Xamlity.Tree;

namespace Xamlity.Core.Tests.Layout
{
    public class CanvasTests
    {
        private static readonly Size Viewport = new Size(200, 100);

        [TestCase(10, 20, double.NaN, double.NaN, 10, 20)]
        [TestCase(double.NaN, double.NaN, 10, 20, 160, 40)]
        [TestCase(10, 20, 50, 60, 10, 20)]
        [TestCase(double.NaN, double.NaN, double.NaN, double.NaN, 0, 0)]
        [TestCase(-10, -20, double.NaN, double.NaN, -10, -20)]
        public void PositionsChildrenUsingOffsetsAndDesiredSize(double left, double top, double right,
            double bottom, double x, double y)
        {
            var canvas = new Canvas();
            var child = new TestElement();
            canvas.Children.Add(child);
            Canvas.SetLeft(child, left);
            Canvas.SetTop(child, top);
            Canvas.SetRight(child, right);
            Canvas.SetBottom(child, bottom);
            new UIRoot(canvas).UpdateLayout(Viewport);

            Assert.That(child.VisualOffset, Is.EqualTo(new Point(x, y)));
            Assert.That(child.RenderSize, Is.EqualTo(new Size(30, 40)));
            Assert.That(child.MeasureConstraint, Is.EqualTo(new Size(double.PositiveInfinity, double.PositiveInfinity)));
            Assert.That(canvas.DesiredSize, Is.EqualTo(new Size(0, 0)));
            Assert.That(canvas.RenderSize, Is.EqualTo(Viewport));
        }

        [Test]
        public void MovingOneChildInvalidatesAncestorArrangeAndPreservesSiblingLayoutCache()
        {
            var canvas = new Canvas();
            var ancestor = new Canvas();
            ancestor.Children.Add(canvas);
            canvas.Width = 200;
            canvas.Height = 100;
            var first = new TestElement();
            var second = new TestElement();
            canvas.Children.Add(first);
            canvas.Children.Add(second);
            var root = new UIRoot(ancestor);
            root.UpdateLayout(Viewport);
            Canvas.SetLeft(first, 15);

            Assert.That(first.IsArrangeValid, Is.False);
            Assert.That(canvas.IsArrangeValid, Is.False);
            Assert.That(ancestor.IsArrangeValid, Is.False);
            Assert.That(first.IsMeasureValid && canvas.IsMeasureValid && ancestor.IsMeasureValid, Is.True);
            Assert.That(second.IsArrangeValid, Is.True);
            root.UpdateLayout(Viewport);
            Assert.That(first.VisualOffset, Is.EqualTo(new Point(15, 0)));
            Assert.That(first.ArrangeCount, Is.EqualTo(2));
            Assert.That(second.ArrangeCount, Is.EqualTo(1));
            Assert.That(first.MeasureCount, Is.EqualTo(1));
            Assert.That(second.MeasureCount, Is.EqualTo(1));
        }

        [Test]
        public void OffsetsDefaultToNaNAndOnlyAffectArrange()
        {
            var child = new TestElement();
            Assert.That(Canvas.GetLeft(child), Is.NaN);
            Assert.That(Canvas.GetTop(child), Is.NaN);
            Assert.That(Canvas.GetRight(child), Is.NaN);
            Assert.That(Canvas.GetBottom(child), Is.NaN);
            foreach (DependencyProperty property in new[] { Canvas.LeftProperty, Canvas.TopProperty,
                Canvas.RightProperty, Canvas.BottomProperty })
            {
                Assert.That(property.IsAttached, Is.True);
                var metadata = (FrameworkPropertyMetadata)property.DefaultMetadata;
                Assert.That(metadata.AffectsArrange, Is.True);
                Assert.That(metadata.AffectsMeasure || metadata.AffectsRender || metadata.Inherits, Is.False);
                Assert.That(metadata.PropertyChangedCallback, Is.Null);
            }
        }

        [Test]
        public void AccessorsRejectNullElements()
        {
            Assert.Throws<ArgumentNullException>(() => Canvas.GetLeft(null));
            Assert.Throws<ArgumentNullException>(() => Canvas.SetLeft(null, 1));
            Assert.Throws<ArgumentNullException>(() => Canvas.GetTop(null));
            Assert.Throws<ArgumentNullException>(() => Canvas.SetTop(null, 1));
            Assert.Throws<ArgumentNullException>(() => Canvas.GetRight(null));
            Assert.Throws<ArgumentNullException>(() => Canvas.SetRight(null, 1));
            Assert.Throws<ArgumentNullException>(() => Canvas.GetBottom(null));
            Assert.Throws<ArgumentNullException>(() => Canvas.SetBottom(null, 1));
        }

        private sealed class TestElement : FrameworkElement
        {
            public Size MeasureConstraint { get; private set; }
            public int MeasureCount { get; private set; }
            public int ArrangeCount { get; private set; }

            protected override Size MeasureOverride(Size availableSize)
            {
                MeasureCount++;
                MeasureConstraint = availableSize;
                return new Size(30, 40);
            }

            protected override Size ArrangeOverride(Size finalSize)
            {
                ArrangeCount++;
                return finalSize;
            }
        }
    }
}
