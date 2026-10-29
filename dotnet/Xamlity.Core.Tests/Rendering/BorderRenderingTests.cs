using System;
using NUnit.Framework;
using Xamlity.Controls;
using Xamlity.Layout;
using Xamlity.Rendering;
using Xamlity.Tree;

namespace Xamlity.Core.Tests.Rendering
{
    public class BorderRenderingTests
    {
        private static readonly Brush Red = new SolidColorBrush(Color.FromRgb(255, 0, 0));
        private static readonly Brush Blue = new SolidColorBrush(Color.FromRgb(0, 0, 255));
        private static readonly Size Viewport = new Size(100, 80);

        [Test]
        public void ColorPreservesChannelsAndValueEquality()
        {
            Color color = Color.FromArgb(128, 10, 20, 30);
            Assert.That((color.A, color.R, color.G, color.B), Is.EqualTo((128, 10, 20, 30)));
            Color equal = Color.FromArgb(128, 10, 20, 30);
            Assert.That(color.Equals(equal), Is.True);
            Assert.That(color.Equals((object)equal), Is.True);
            Assert.That(color == equal, Is.True);
            Assert.That(color.GetHashCode(), Is.EqualTo(equal.GetHashCode()));
            Assert.That(color.Equals(null), Is.False);
            Assert.That(color != Color.FromArgb(127, 10, 20, 30), Is.True);
            Assert.That(color != Color.FromArgb(128, 11, 20, 30), Is.True);
            Assert.That(color != Color.FromArgb(128, 10, 21, 30), Is.True);
            Assert.That(color != Color.FromArgb(128, 10, 20, 31), Is.True);
            Assert.That(Color.FromRgb(10, 20, 30), Is.EqualTo(Color.FromArgb(255, 10, 20, 30)));
            Assert.That(new SolidColorBrush(color).Color, Is.EqualTo(color));
        }

        [Test]
        public void DrawsTopBottomLeftRightThenBackgroundInsideBorder()
        {
            var border = new Border { BorderBrush = Red, Background = Blue,
                BorderThickness = new Thickness(2, 3, 4, 5), Padding = new Thickness(7) };
            var root = new UIRoot(border);
            root.UpdateLayout(Viewport);
            var context = new RecordingDrawingContext();
            root.Render(context);

            Assert.That(context.Rectangles, Is.EqualTo(new[]
            {
                (Red, new Rect(0, 0, 100, 3)),
                (Red, new Rect(0, 75, 100, 5)),
                (Red, new Rect(0, 3, 2, 72)),
                (Red, new Rect(96, 3, 4, 72)),
                (Blue, new Rect(2, 3, 94, 72))
            }));
        }

        [Test]
        public void NestedBordersRenderInAbsoluteCoordinatesAndSiblingIndexOrder()
        {
            var first = new Border { Height = 20, Background = Blue,
                BorderThickness = new Thickness(1), Padding = new Thickness(2),
                Child = new Border { Background = Red } };
            var second = new Border { Height = 10, Background = Red };
            var panel = new StackPanel();
            panel.Children.Add(first);
            panel.Children.Add(second);
            var outer = new Border { Background = Red, Margin = new Thickness(5, 7, 0, 0),
                BorderThickness = new Thickness(2, 3, 4, 5), Padding = new Thickness(6, 8, 10, 12), Child = panel };
            var root = new UIRoot(outer);
            root.UpdateLayout(new Size(120, 100));
            var context = new RecordingDrawingContext();
            root.Render(context);

            Assert.That(context.Rectangles, Is.EqualTo(new[]
            {
                (Red, new Rect(7, 10, 109, 85)),
                (Blue, new Rect(14, 19, 91, 18)),
                (Red, new Rect(16, 21, 87, 14)),
                (Red, new Rect(13, 38, 93, 10))
            }));
        }

        [Test]
        public void NullBrushesProduceNoCommands()
        {
            var root = new UIRoot(new Border { BorderThickness = new Thickness(4) });
            root.UpdateLayout(Viewport);
            var context = new RecordingDrawingContext();
            root.Render(context);
            Assert.That(context.Rectangles, Is.Empty);
        }

        [Test]
        public void ZeroThicknessSidesAndZeroAreaBackgroundAreOmitted()
        {
            var border = new Border { Background = Blue, BorderBrush = Red,
                BorderThickness = new Thickness(0, 0, 0, 80) };
            var root = new UIRoot(border);
            root.UpdateLayout(Viewport);
            var context = new RecordingDrawingContext();
            root.Render(context);
            Assert.That(context.Rectangles, Is.EqualTo(new[] { (Red, new Rect(0, 0, 100, 80)) }));
        }

        [Test]
        public void BorderAndPaddingReduceConstraintAndOffsetChild()
        {
            var child = new MeasuringElement();
            var border = new Border { BorderThickness = new Thickness(2, 3, 4, 5),
                Padding = new Thickness(6, 7, 8, 9), Child = child };
            new UIRoot(border).UpdateLayout(Viewport);
            Assert.That(child.Constraint, Is.EqualTo(new Size(80, 56)));
            Assert.That(border.DesiredSize, Is.EqualTo(new Size(50, 44)));
            Assert.That(child.VisualOffset, Is.EqualTo(new Point(8, 10)));
            Assert.That(child.RenderSize, Is.EqualTo(new Size(80, 56)));
        }

        [Test]
        public void EmptyBorderDesiresBorderAndPaddingAndSmallConstraintClampsToZero()
        {
            var border = new Border { BorderThickness = new Thickness(2), Padding = new Thickness(3) };
            border.Measure(Viewport);
            Assert.That(border.DesiredSize, Is.EqualTo(new Size(10, 10)));
            var child = new MeasuringElement();
            border.Child = child;
            border.Measure(new Size(4, 6));
            Assert.That(child.Constraint, Is.EqualTo(new Size(0, 0)));
        }

        [Test]
        public void ChildReplacementRemovalAndInvalidReplacementPreserveTree()
        {
            var first = new Border();
            var second = new Border();
            var border = new Border { Child = first };
            var root = new UIRoot(border);
            root.UpdateLayout(Viewport);
            root.Render(new EmptyDrawingContext());
            border.Child = first;
            Assert.That(root.IsRenderDirty, Is.False);
            Assert.That(border.IsMeasureValid, Is.True);

            var other = new Border { Child = second };
            Assert.Throws<InvalidOperationException>(() => border.Child = second);
            Assert.Throws<InvalidOperationException>(() => border.Child = border);
            Assert.Throws<InvalidOperationException>(() => first.Child = border);
            Assert.That(border.Child, Is.SameAs(first));
            Assert.That(first.VisualParent, Is.SameAs(border));
            Assert.That(second.VisualParent, Is.SameAs(other));

            other.Child = null;
            border.Child = second;
            Assert.That(first.VisualParent, Is.Null);
            Assert.That(second.VisualParent, Is.SameAs(border));
            Assert.That(border.IsMeasureValid, Is.False);
            Assert.That(root.IsRenderDirty, Is.True);
            root.UpdateLayout(Viewport);
            root.Render(new EmptyDrawingContext());
            border.Child = null;
            Assert.That(second.VisualParent, Is.Null);
            Assert.That(root.IsRenderDirty, Is.True);
            Assert.That(border.IsMeasureValid, Is.False);
        }

        [Test]
        public void RenderDirtinessTracksBrushChangesAndActualLayoutChanges()
        {
            var child = new Border { Width = 20, Background = Red };
            var root = new UIRoot(new Border { Child = child });
            var context = new EmptyDrawingContext();
            Assert.That(root.IsRenderDirty, Is.True);
            root.UpdateLayout(Viewport);
            root.Render(context);
            Assert.That(root.IsRenderDirty, Is.False);
            child.Background = Blue;
            Assert.That(root.IsRenderDirty, Is.True);
            Assert.That(child.IsMeasureValid && child.IsArrangeValid, Is.True);
            root.Render(context);
            child.Background = Blue;
            Assert.That(root.IsRenderDirty, Is.False);

            Point oldOffset = child.VisualOffset;
            child.Width = 40;
            root.UpdateLayout(Viewport);
            Assert.That(child.VisualOffset, Is.Not.EqualTo(oldOffset));
            Assert.That(root.IsRenderDirty, Is.True);
            root.Render(context);
            root.UpdateLayout(Viewport);
            Assert.That(root.IsRenderDirty, Is.False);
            child.InvalidateMeasure();
            root.UpdateLayout(Viewport);
            Assert.That(root.IsRenderDirty, Is.False);

            child.HorizontalAlignment = HorizontalAlignment.Left;
            root.UpdateLayout(Viewport);
            Assert.That(child.RenderSize.Width, Is.EqualTo(40));
            Assert.That(root.IsRenderDirty, Is.True);
            root.Render(context);
            child.Width = 50;
            root.UpdateLayout(Viewport);
            Assert.That(child.VisualOffset.X, Is.Zero);
            Assert.That(root.IsRenderDirty, Is.True);
        }

        [Test]
        public void AllBorderPropertiesInvalidateAccordingToMetadata()
        {
            var border = new Border();
            var root = new UIRoot(border);
            var properties = new[] { Border.BackgroundProperty, Border.BorderBrushProperty,
                Border.BorderThicknessProperty, Border.PaddingProperty };
            object[] values = { Red, Blue, new Thickness(2), new Thickness(3) };
            for (int index = 0; index < properties.Length; index++)
            {
                root.UpdateLayout(Viewport);
                root.Render(new EmptyDrawingContext());
                border.SetValue(properties[index], values[index]);
                Assert.That(root.IsRenderDirty, Is.True);
                Assert.That(border.IsMeasureValid, Is.EqualTo(index < 2));
                root.UpdateLayout(Viewport);
                root.Render(new EmptyDrawingContext());
                border.ClearValue(properties[index]);
                Assert.That(root.IsRenderDirty, Is.True);
            }
        }

        [Test]
        public void InvalidateVisualFollowsCurrentRootAfterReparenting()
        {
            var child = new Border { Child = new Border() };
            var first = new Border { Child = child };
            var second = new Border();
            var firstRoot = new UIRoot(first);
            var secondRoot = new UIRoot(second);
            var context = new EmptyDrawingContext();
            first.Child = null;
            second.Child = child;
            firstRoot.Render(context);
            secondRoot.Render(context);
            child.Child.InvalidateVisual();
            Assert.That(firstRoot.IsRenderDirty, Is.False);
            Assert.That(secondRoot.IsRenderDirty, Is.True);
            second.Child = null;
            secondRoot.Render(context);
            child.Child.InvalidateVisual();
            Assert.That(secondRoot.IsRenderDirty, Is.False);
        }

        [Test]
        public void SecondUnchangedLayoutAndRenderAllocateNothing()
        {
            var panel = new StackPanel();
            panel.Children.Add(new Border { Background = Blue, Height = 20 });
            panel.Children.Add(new Border { Background = Red, Height = 10 });
            var root = new UIRoot(new Border { Background = Blue, BorderBrush = Red,
                BorderThickness = new Thickness(2), Padding = new Thickness(3), Child = panel });
            var context = new EmptyDrawingContext();
            root.UpdateLayout(Viewport);
            root.Render(context);

            long before = GC.GetAllocatedBytesForCurrentThread();
            root.UpdateLayout(Viewport);
            root.Render(context);
            long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
            Assert.That(allocated, Is.Zero);
            Assert.That(root.IsRenderDirty, Is.False);
        }

        private sealed class MeasuringElement : FrameworkElement
        {
            public Size Constraint { get; private set; }

            protected override Size MeasureOverride(Size availableSize)
            {
                Constraint = availableSize;
                return new Size(30, 20);
            }
        }

        private sealed class EmptyDrawingContext : IDrawingContext
        {
            public void DrawRectangle(Brush brush, Rect rect)
            {
            }
        }
    }
}
