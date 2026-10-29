using System;
using System.Linq;
using NUnit.Framework;
using Xamlity.Controls;
using Xamlity.Layout;
using Xamlity.Rendering;
using Xamlity.Tree;

namespace Xamlity.Core.Tests.Rendering
{
    public class OpacityTests
    {
        private static readonly Size Viewport = new Size(100, 100);
        private static readonly Brush Red = new SolidColorBrush(Color.FromRgb(255, 0, 0));

        [Test]
        public void OpacitySurroundsOwnDrawingAndDescendantsInOrder()
        {
            var child = new TextBlock { Text = "A" };
            var border = new Border { Background = Red, Opacity = 0.5, Child = child,
                Margin = new Thickness(3), Padding = new Thickness(2) };
            var root = new UIRoot(border, new TextBlockTests.FakeTextMeasurer(), new FontHandle(new object()));
            root.UpdateLayout(Viewport);
            var context = new RecordingDrawingContext();
            root.Render(context);

            Assert.That(child.Opacity, Is.EqualTo(1.0));
            Assert.That(context.Calls.Select(call => call.Method), Is.EqualTo(new[]
                { "PushOpacity", "DrawRectangle", "DrawGlyphRun", "Pop" }));
            Assert.That(context.Calls[0].Opacity, Is.EqualTo(0.5));
            Assert.That((context.Calls[1].Brush, context.Calls[1].Rect), Is.EqualTo(context.Rectangles[0]));
            Assert.That((context.Calls[2].Brush, context.Calls[2].Run, context.Calls[2].BaselineOrigin),
                Is.EqualTo(context.GlyphRuns[0]));
            Assert.That(context.Rectangles[0].Rect.Location, Is.EqualTo(new Point(3, 3)));
            Assert.That(context.GlyphRuns[0].BaselineOrigin.X, Is.EqualTo(5));
        }

        [Test]
        public void NestedOpacityPopsBeforeDrawingSibling()
        {
            var panel = new StackPanel { Opacity = 0.5 };
            panel.Children.Add(new Border { Background = Red, Height = 20, Opacity = 0.25 });
            panel.Children.Add(new Border { Background = Red, Height = 20 });
            var root = new UIRoot(panel);
            root.UpdateLayout(Viewport);
            var context = new RecordingDrawingContext();
            root.Render(context);

            Assert.That(context.Calls.Select(call => call.Method), Is.EqualTo(new[]
                { "PushOpacity", "PushOpacity", "DrawRectangle", "Pop", "DrawRectangle", "Pop" }));
            Assert.That(context.Calls[0].Opacity, Is.EqualTo(0.5));
            Assert.That(context.Calls[1].Opacity, Is.EqualTo(0.25));
        }

        [TestCase(0.0)]
        [TestCase(-0.5)]
        [TestCase(double.NegativeInfinity)]
        [TestCase(double.NaN)]
        public void InvisibleOpacitySkipsAllSubtreeCommands(double opacity)
        {
            var border = new Border { Background = Red, Opacity = opacity,
                Child = new Border { Background = Red, Opacity = 0.5 } };
            var root = new UIRoot(border);
            root.UpdateLayout(Viewport);
            var context = new RecordingDrawingContext();
            root.Render(context);

            Assert.That(context.Calls, Is.Empty);
            Assert.That(root.IsRenderDirty, Is.False);
        }

        [TestCase(1.0)]
        [TestCase(2.0)]
        [TestCase(double.PositiveInfinity)]
        public void FullOpacityDoesNotPush(double opacity)
        {
            Assert.That(new Border().Opacity, Is.EqualTo(1.0));
            var root = new UIRoot(new Border { Background = Red, Opacity = opacity });
            root.UpdateLayout(Viewport);
            var context = new RecordingDrawingContext();
            root.Render(context);

            Assert.That(context.Calls.Select(call => call.Method), Is.EqualTo(new[] { "DrawRectangle" }));
        }

        [Test]
        public void ChangingAndClearingOpacityInvalidatesOnlyRendering()
        {
            var child = new Border { Background = Red };
            var parent = new Border { Child = child };
            var root = new UIRoot(parent);
            var context = new EmptyDrawingContext();
            root.UpdateLayout(Viewport);
            root.Render(context);

            child.Opacity = 0.5;
            AssertRenderDirtyWithValidLayout();
            root.Render(context);
            child.Opacity = 0.5;
            Assert.That(root.IsRenderDirty, Is.False);
            child.ClearValue(UIElement.OpacityProperty);
            Assert.That(child.Opacity, Is.EqualTo(1.0));
            AssertRenderDirtyWithValidLayout();

            void AssertRenderDirtyWithValidLayout()
            {
                Assert.That(root.IsRenderDirty, Is.True);
                Assert.That(child.IsMeasureValid && parent.IsMeasureValid, Is.True);
                Assert.That(child.IsArrangeValid && parent.IsArrangeValid, Is.True);
            }
        }

        [Test]
        public void UnchangedRenderWithNestedOpacityAllocatesNothing()
        {
            var root = new UIRoot(new Border { Background = Red, Opacity = 0.5,
                Child = new Border { Background = Red, Opacity = 0.25 } });
            var context = new EmptyDrawingContext();
            root.UpdateLayout(Viewport);
            root.Render(context);

            long before = GC.GetAllocatedBytesForCurrentThread();
            root.Render(context);
            long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
            Assert.That(allocated, Is.Zero);
        }

        [Test]
        public void RenderFailureUnwindsOpacityAndKeepsRootDirty()
        {
            var root = new UIRoot(new Border { Opacity = 0.5,
                Child = new ThrowingElement { Opacity = 0.25 } });
            var context = new RecordingDrawingContext();

            Assert.Throws<InvalidOperationException>(() => root.Render(context));

            Assert.That(context.Calls.Select(call => call.Method), Is.EqualTo(new[]
                { "PushOpacity", "PushOpacity", "Pop", "Pop" }));
            Assert.That(root.IsRenderDirty, Is.True);
        }

        private sealed class ThrowingElement : UIElement
        {
            protected override void OnRender(IDrawingContext drawingContext) =>
                throw new InvalidOperationException("Rendering failed.");
        }
    }
}
