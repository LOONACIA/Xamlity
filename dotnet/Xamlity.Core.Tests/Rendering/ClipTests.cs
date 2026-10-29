using System;
using System.Linq;
using NUnit.Framework;
using Xamlity.Controls;
using Xamlity.Layout;
using Xamlity.Rendering;
using Xamlity.Tree;

namespace Xamlity.Core.Tests.Rendering
{
    public class ClipTests
    {
        private static readonly Brush Red = new SolidColorBrush(Color.FromRgb(255, 0, 0));

        [Test]
        public void UIElementClipsToRenderSizeAndReceivesLastArrangeSlot()
        {
            var element = new ClipElement { ClipToBounds = true };
            var root = new UIRoot(element);
            element.Arrange(new Rect(3, 5, 70, 40));
            var context = new RecordingDrawingContext();
            root.Render(context);

            Assert.That(element.LastSlot, Is.EqualTo(new Size(70, 40)));
            Assert.That(context.Calls.Select(call => call.Method), Is.EqualTo(new[] { "PushClip", "Pop" }));
            Assert.That(context.Calls[0].Rect, Is.EqualTo(new Rect(3, 5, 70, 40)));
        }

        [Test]
        public void NoWrapTextClipsToExplicitWidthWithoutReducingRenderSize()
        {
            var text = new TextBlock { Text = new string('A', 20), FontSize = 12, Width = 50,
                TextWrapping = TextWrapping.NoWrap, HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top };
            var root = new UIRoot(text, new TextBlockTests.FakeTextMeasurer(), new FontHandle(new object()));
            root.UpdateLayout(new Size(200, 100));
            var context = new RecordingDrawingContext();
            root.Render(context);

            Assert.That(text.RenderSize.Width, Is.EqualTo(120));
            Assert.That(context.Calls.Select(call => call.Method),
                Is.EqualTo(new[] { "PushClip", "DrawGlyphRun", "Pop" }));
            Assert.That(context.Calls[0].Rect, Is.EqualTo(new Rect(0, 0, 50, 12 * 1.2)));
        }

        [TestCase(HorizontalAlignment.Left, 0)]
        [TestCase(HorizontalAlignment.Center, 50)]
        [TestCase(HorizontalAlignment.Right, 100)]
        [TestCase(HorizontalAlignment.Stretch, 0)]
        public void OversizedChildClipsToAlignedParentSlot(HorizontalAlignment alignment, double x)
        {
            var child = new ClipBorder { Width = 200, Height = 20, HorizontalAlignment = alignment,
                Background = Red };
            var root = new UIRoot(new Border { Width = 100, Height = 20, Child = child });
            root.UpdateLayout(new Size(100, 20));
            var context = new RecordingDrawingContext();
            root.Render(context);

            Assert.That(child.Clip(new Size(100, 20)), Is.EqualTo(new Rect(x, 0, 100, 20)));
            Assert.That(context.Calls[0].Rect, Is.EqualTo(new Rect(0, 0, 100, 20)));
            Assert.That(context.Calls.Select(call => call.Method),
                Is.EqualTo(new[] { "PushClip", "DrawRectangle", "Pop" }));
        }

        [TestCase(VerticalAlignment.Top, 0)]
        [TestCase(VerticalAlignment.Center, 30)]
        [TestCase(VerticalAlignment.Bottom, 60)]
        [TestCase(VerticalAlignment.Stretch, 0)]
        public void VerticalSlotClipExcludesMargins(VerticalAlignment alignment, double y)
        {
            var child = new ClipBorder { Width = 40, Height = 80, Margin = new Thickness(5),
                VerticalAlignment = alignment };
            var root = new UIRoot(child);
            root.UpdateLayout(new Size(50, 30));

            Assert.That(child.Clip(new Size(50, 30)), Is.EqualTo(new Rect(0, y, 40, 20)));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void CanvasClipsOverflowOnlyWhenRequested(bool clipToBounds)
        {
            var canvas = new Canvas { Width = 100, Height = 20, ClipToBounds = clipToBounds };
            var child = new Border { Width = 200, Height = 40, Background = Red };
            canvas.Children.Add(child);
            Canvas.SetLeft(child, 10);
            var root = new UIRoot(canvas);
            root.UpdateLayout(new Size(100, 20));
            var context = new RecordingDrawingContext();
            root.Render(context);

            Assert.That(context.Calls.Select(call => call.Method), Is.EqualTo(clipToBounds
                ? new[] { "PushClip", "DrawRectangle", "Pop" } : new[] { "DrawRectangle" }));
            if (clipToBounds)
                Assert.That(context.Calls[0].Rect, Is.EqualTo(new Rect(0, 0, 100, 20)));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void CanvasIgnoresItsOwnLayoutSlotClip(bool clipToBounds)
        {
            var canvas = new Canvas { MinWidth = 200, ClipToBounds = clipToBounds };
            var root = new UIRoot(canvas);
            root.UpdateLayout(new Size(100, 20));
            var context = new RecordingDrawingContext();
            root.Render(context);

            Assert.That(context.Calls.Count, Is.EqualTo(clipToBounds ? 2 : 0));
            if (clipToBounds)
                Assert.That(context.Calls[0].Rect, Is.EqualTo(new Rect(0, 0, 200, 20)));
        }

        [Test]
        public void OrdinaryLayoutPreservesCommandsWithoutClipPushes()
        {
            var root = new UIRoot(new Border { Background = Red,
                Child = new Border { Background = Red, Width = 20, Height = 20 } });
            root.UpdateLayout(new Size(100, 100));
            var context = new RecordingDrawingContext();
            root.Render(context);

            Assert.That(context.Calls.Select(call => call.Method),
                Is.EqualTo(new[] { "DrawRectangle", "DrawRectangle" }));
        }

        [Test]
        public void LocalAndSlotClipsIntersectUsingMaximumConstrainedInk()
        {
            var element = new OversizedElement { MaxWidth = 80, MaxHeight = 30,
                HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
            var root = new UIRoot(element);
            root.UpdateLayout(new Size(40, 20));

            Assert.That(element.RenderSize, Is.EqualTo(new Size(120, 60)));
            Assert.That(element.Clip(new Size(40, 20)), Is.EqualTo(new Rect(20, 5, 40, 20)));
        }

        [Test]
        public void ClipToBoundsIntersectsSlotWithLocalBoundsOnBothAxes()
        {
            var element = new ClipBorder { Width = 40, Height = 80, ClipToBounds = true };
            var root = new UIRoot(element);
            root.UpdateLayout(new Size(100, 20));

            Assert.That(element.Clip(new Size(100, 20)), Is.EqualTo(new Rect(0, 0, 40, 20)));
        }

        [Test]
        public void FiniteMaximumRemainsTheLocalClipWhenLargerThanRenderSize()
        {
            var element = new ClipBorder { ClipToBounds = true, MaxWidth = 200, MaxHeight = 150 };
            var root = new UIRoot(element);
            root.UpdateLayout(new Size(100, 80));

            Assert.That(element.Clip(new Size(100, 80)), Is.EqualTo(new Rect(0, 0, 200, 150)));
        }

        [Test]
        public void NestedClipAndOpacitySurroundDrawingAndRestoreBeforeSibling()
        {
            var panel = new StackPanel { ClipToBounds = true, Opacity = 0.5, Margin = new Thickness(3) };
            panel.Children.Add(new Border { ClipToBounds = true, Opacity = 0.25,
                Height = 20, Margin = new Thickness(2), Background = Red });
            panel.Children.Add(new Border { Height = 20, Background = Red });
            var root = new UIRoot(panel);
            root.UpdateLayout(new Size(100, 100));
            var context = new RecordingDrawingContext();
            root.Render(context);

            Assert.That(context.Calls.Select(call => call.Method), Is.EqualTo(new[]
            {
                "PushClip", "PushOpacity", "PushClip", "PushOpacity", "DrawRectangle", "Pop", "Pop",
                "DrawRectangle", "Pop", "Pop"
            }));
            Assert.That(context.Calls[0].Rect, Is.EqualTo(new Rect(3, 3, 94, 94)));
            Assert.That(context.Calls[2].Rect, Is.EqualTo(new Rect(5, 5, 90, 20)));
            Assert.That(context.Calls[1].Opacity, Is.EqualTo(0.5));
            Assert.That(context.Calls[3].Opacity, Is.EqualTo(0.25));
        }

        [Test]
        public void ChangingAndClearingClipToBoundsInvalidatesOnlyRendering()
        {
            var child = new Border();
            var root = new UIRoot(new Border { Child = child });
            root.UpdateLayout(new Size(100, 100));
            root.Render(new EmptyDrawingContext());
            Assert.That(child.ClipToBounds, Is.False);

            child.ClipToBounds = true;
            Assert.That(root.IsRenderDirty, Is.True);
            Assert.That(child.IsMeasureValid && child.IsArrangeValid, Is.True);
            var context = new RecordingDrawingContext();
            root.Render(context);
            Assert.That(context.Calls[0].Rect, Is.EqualTo(new Rect(0, 0, 100, 100)));
            child.ClipToBounds = true;
            Assert.That(root.IsRenderDirty, Is.False);
            child.ClearValue(UIElement.ClipToBoundsProperty);
            Assert.That(child.ClipToBounds, Is.False);
            Assert.That(root.IsRenderDirty, Is.True);
            Assert.That(child.IsMeasureValid && child.IsArrangeValid, Is.True);
            context.Calls.Clear();
            root.Render(context);
            Assert.That(context.Calls, Is.Empty);
        }

        [Test]
        public void SlotOnlyChangeInvalidatesRenderAndRemovesObsoleteClip()
        {
            var child = new Border { Width = 200, Height = 20, Background = Red,
                HorizontalAlignment = HorizontalAlignment.Left };
            var root = new UIRoot(child);
            root.UpdateLayout(new Size(100, 20));
            root.Render(new EmptyDrawingContext());
            root.UpdateLayout(new Size(150, 20));
            Assert.That(child.RenderSize, Is.EqualTo(new Size(200, 20)));
            Assert.That(root.IsRenderDirty, Is.True);
            var context = new RecordingDrawingContext();
            root.Render(context);
            Assert.That(context.Calls[0].Rect, Is.EqualTo(new Rect(0, 0, 150, 20)));

            root.UpdateLayout(new Size(200, 20));
            Assert.That(root.IsRenderDirty, Is.True);
            context.Calls.Clear();
            root.Render(context);
            Assert.That(context.Calls.Select(call => call.Method), Is.EqualTo(new[] { "DrawRectangle" }));
        }

        [Test]
        public void UnchangedRenderWithNestedClipsAndOpacityAllocatesNothing()
        {
            var root = new UIRoot(new Border { ClipToBounds = true, Opacity = 0.5,
                Child = new Border { Width = 200, Background = Red, ClipToBounds = true, Opacity = 0.25 } });
            var context = new EmptyDrawingContext();
            root.UpdateLayout(new Size(100, 100));
            root.Render(context);

            long before = GC.GetAllocatedBytesForCurrentThread();
            root.Render(context);
            long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
            Assert.That(allocated, Is.Zero);
        }

        [Test]
        public void RenderFailureUnwindsBothClipsAndOpacity()
        {
            var root = new UIRoot(new Border { ClipToBounds = true, Opacity = 0.5,
                Child = new ClipElement { ClipToBounds = true, Opacity = 0.25, ThrowOnRender = true } });
            root.UpdateLayout(new Size(100, 100));
            var context = new RecordingDrawingContext();

            Assert.Throws<InvalidOperationException>(() => root.Render(context));
            Assert.That(context.Calls.Select(call => call.Method), Is.EqualTo(new[]
                { "PushClip", "PushOpacity", "PushClip", "PushOpacity", "Pop", "Pop", "Pop", "Pop" }));
            Assert.That(root.IsRenderDirty, Is.True);
        }

        private sealed class ClipElement : UIElement
        {
            public Size LastSlot { get; private set; }
            public bool ThrowOnRender { get; set; }

            protected override Rect? GetLayoutClip(Size layoutSlotSize)
            {
                LastSlot = layoutSlotSize;
                return base.GetLayoutClip(layoutSlotSize);
            }

            protected override void OnRender(IDrawingContext drawingContext)
            {
                if (ThrowOnRender)
                    throw new InvalidOperationException("Rendering failed.");
            }
        }

        private class ClipBorder : Border
        {
            public Rect? Clip(Size slot) => GetLayoutClip(slot);
        }

        private sealed class OversizedElement : ClipBorder
        {
            protected override Size ArrangeOverride(Size finalSize) => new Size(120, 60);
        }
    }
}
