using System;
using System.Globalization;
using System.Linq;
using NUnit.Framework;
using Xamlity.Controls;
using Xamlity.Layout;
using Xamlity.Rendering;
using Xamlity.Tree;

namespace Xamlity.Core.Tests.Rendering
{
    public class CornerRadiusTests
    {
        private static readonly Brush Red = new SolidColorBrush(Color.FromRgb(255, 0, 0));
        private static readonly Brush Blue = new SolidColorBrush(Color.FromRgb(0, 0, 255));

        [Test]
        public void ValueEqualityAndInvariantStringPreserveCornerOrder()
        {
            var radius = new CornerRadius(1.5, 2.5, 3.5, 4.5);
            var equal = new CornerRadius(1.5, 2.5, 3.5, 4.5);
            Assert.That((radius.TopLeft, radius.TopRight, radius.BottomRight, radius.BottomLeft),
                Is.EqualTo((1.5, 2.5, 3.5, 4.5)));
            Assert.That(radius.Equals(equal), Is.True);
            Assert.That(radius.Equals((object)equal), Is.True);
            Assert.That(radius == equal, Is.True);
            Assert.That(radius.GetHashCode(), Is.EqualTo(equal.GetHashCode()));
            Assert.That(radius.Equals(null), Is.False);
            Assert.That(radius.Equals(new Thickness(1)), Is.False);
            Assert.That(radius != new CornerRadius(0, 2.5, 3.5, 4.5), Is.True);
            Assert.That(radius != new CornerRadius(1.5, 0, 3.5, 4.5), Is.True);
            Assert.That(radius != new CornerRadius(1.5, 2.5, 0, 4.5), Is.True);
            Assert.That(radius != new CornerRadius(1.5, 2.5, 3.5, 0), Is.True);
            Assert.That(new CornerRadius(8), Is.EqualTo(new CornerRadius(8, 8, 8, 8)));
            Assert.That(default(CornerRadius), Is.EqualTo(new CornerRadius(0)));
            CultureInfo previous = CultureInfo.CurrentCulture;
            try
            {
                CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("fr-FR");
                Assert.That(radius.ToString(), Is.EqualTo("1.5,2.5,3.5,4.5"));
            }
            finally
            {
                CultureInfo.CurrentCulture = previous;
            }
        }

        [Test]
        public void ConstructorsDoNotValidateValues()
        {
            var radius = new CornerRadius(-1, double.NaN, double.PositiveInfinity, double.NegativeInfinity);
            Assert.That(radius.TopLeft, Is.EqualTo(-1));
            Assert.That(radius.TopRight, Is.NaN);
            Assert.That(radius.BottomRight, Is.EqualTo(double.PositiveInfinity));
            Assert.That(radius.BottomLeft, Is.EqualTo(double.NegativeInfinity));
            Assert.That(new CornerRadius(double.NaN).TopLeft, Is.NaN);
        }

        [Test]
        public void UniformThicknessDrawsRoundedBorderThenInsetBackground()
        {
            var context = Render(new Border { CornerRadius = new CornerRadius(8),
                BorderThickness = new Thickness(2), BorderBrush = Red, Background = Blue, Padding = new Thickness(5) });
            Assert.That(context.Calls.Select(c => c.Method),
                Is.EqualTo(new[] { "DrawRoundedBorder", "DrawRoundedRectangle" }));
            Assert.That(context.RoundedBorders, Is.EqualTo(new[] {
                (Red, new Rect(0, 0, 100, 80), new CornerRadius(9), new Rect(2, 2, 96, 76), new CornerRadius(7)) }));
            Assert.That(context.RoundedRectangles, Is.EqualTo(new[] {
                (Blue, new Rect(2, 2, 96, 76), new CornerRadius(7)) }));
            Assert.That(context.Rectangles, Is.Empty);
        }

        [Test]
        public void ZeroThicknessDrawsOnlyBackground()
        {
            var context = Render(new Border { CornerRadius = new CornerRadius(8), BorderBrush = Red, Background = Blue });
            Assert.That(context.Calls.Select(c => c.Method), Is.EqualTo(new[] { "DrawRoundedRectangle" }));
            Assert.That(context.RoundedRectangles, Is.EqualTo(new[] {
                (Blue, new Rect(0, 0, 100, 80), new CornerRadius(8)) }));
        }

        [Test]
        public void BorderBrushWithoutBackgroundDrawsOnlyBorder()
        {
            var context = Render(new Border { CornerRadius = new CornerRadius(8),
                BorderThickness = new Thickness(2), BorderBrush = Red });
            Assert.That(context.Calls.Select(c => c.Method), Is.EqualTo(new[] { "DrawRoundedBorder" }));
            Assert.That(context.RoundedBorders[0].InnerRadius, Is.EqualTo(new CornerRadius(7)));
        }

        [Test]
        public void NullBrushesDrawNothing()
        {
            Assert.That(Render(new Border { CornerRadius = new CornerRadius(8), BorderThickness = new Thickness(2) })
                .Calls, Is.Empty);
        }

        [Test]
        public void OversizedRadiusScalesToShorterSide()
        {
            var context = Render(new Border { CornerRadius = new CornerRadius(20), Background = Blue }, 40, 20);
            Assert.That(context.RoundedRectangles, Is.EqualTo(new[] {
                (Blue, new Rect(0, 0, 40, 20), new CornerRadius(10)) }));
        }

        [TestCase(30, 10, 2, 4, 20, 100)]
        [TestCase(2, 4, 30, 10, 20, 100)]
        [TestCase(30, 2, 4, 10, 100, 20)]
        [TestCase(2, 30, 10, 4, 100, 20)]
        public void EverySideCanLimitAllFourRadii(double tl, double tr, double br, double bl, double width, double height)
        {
            var context = Render(new Border { CornerRadius = new CornerRadius(tl, tr, br, bl), Background = Blue },
                width, height);
            Assert.That(context.RoundedRectangles[0].Radius, Is.EqualTo(new CornerRadius(tl / 2, tr / 2, br / 2, bl / 2)));
        }

        [Test]
        public void OuterAndInnerRadiiScaleIndependently()
        {
            var context = Render(new Border { CornerRadius = new CornerRadius(20), BorderThickness = new Thickness(2),
                BorderBrush = Red, Background = Blue }, 40, 20);
            Assert.That(context.RoundedBorders, Is.EqualTo(new[] {
                (Red, new Rect(0, 0, 40, 20), new CornerRadius(10), new Rect(2, 2, 36, 16), new CornerRadius(8)) }));
            Assert.That(context.RoundedRectangles[0].Radius, Is.EqualTo(new CornerRadius(8)));
        }

        [Test]
        public void UnequalThicknessUsesLargerAdjacentSideAndKeepsZeroCornersSquare()
        {
            var context = Render(new Border { CornerRadius = new CornerRadius(0, 8, 2, 10),
                BorderThickness = new Thickness(2, 4, 6, 8), BorderBrush = Red, Background = Blue });
            Assert.That(context.RoundedBorders, Is.EqualTo(new[] {
                (Red, new Rect(0, 0, 100, 80), new CornerRadius(0, 11, 6, 14),
                    new Rect(2, 4, 92, 68), new CornerRadius(0, 5, 0, 6)) }));
            Assert.That(context.RoundedRectangles[0].Radius, Is.EqualTo(new CornerRadius(0, 5, 0, 6)));
        }

        [TestCase(40, 0)]
        [TestCase(0, 20)]
        public void EmptyInnerRectangleKeepsBorderCommandAndOmitsBackground(double right, double bottom)
        {
            var context = Render(new Border { CornerRadius = new CornerRadius(8),
                BorderThickness = new Thickness(0, 0, right, bottom), BorderBrush = Red, Background = Blue }, 40, 20);
            Assert.That(context.Calls.Select(c => c.Method), Is.EqualTo(new[] { "DrawRoundedBorder" }));
            Assert.That(context.RoundedBorders[0].InnerRect, Is.EqualTo(new Rect(0, 0, 40 - right, 20 - bottom)));
            Assert.That(context.RoundedBorders[0].InnerRadius, Is.EqualTo(new CornerRadius(0)));
        }

        [Test]
        public void ZeroRadiusPreservesOriginalCommandsAndOrder()
        {
            var border = new Border { BorderBrush = Red, Background = Blue, BorderThickness = new Thickness(2, 3, 4, 5) };
            Assert.That(border.CornerRadius, Is.EqualTo(new CornerRadius(0)));
            border.CornerRadius = new CornerRadius(0);
            var context = Render(border);
            Assert.That(context.Calls.Select(c => c.Method), Is.EqualTo(Enumerable.Repeat("DrawRectangle", 5)));
            Assert.That(context.Rectangles, Is.EqualTo(new[] {
                (Red, new Rect(0, 0, 100, 3)), (Red, new Rect(0, 75, 100, 5)),
                (Red, new Rect(0, 3, 2, 72)), (Red, new Rect(96, 3, 4, 72)), (Blue, new Rect(2, 3, 94, 72)) }));
        }

        [Test]
        public void NestedBordersOffsetBothRoundedRectanglesWithoutChangingRadii()
        {
            var child = new Border { CornerRadius = new CornerRadius(8), BorderThickness = new Thickness(2),
                Background = Blue, BorderBrush = Red };
            var outer = new Border { Margin = new Thickness(5, 7, 0, 0), BorderThickness = new Thickness(2, 3, 4, 5),
                Padding = new Thickness(6, 8, 10, 12), Child = child };
            var context = Render(outer, 120, 100);
            Assert.That(context.RoundedBorders, Is.EqualTo(new[] {
                (Red, new Rect(13, 18, 93, 65), new CornerRadius(9), new Rect(15, 20, 89, 61), new CornerRadius(7)) }));
            Assert.That(context.RoundedRectangles, Is.EqualTo(new[] {
                (Blue, new Rect(15, 20, 89, 61), new CornerRadius(7)) }));
        }

        [Test]
        public void CornerRadiusInvalidatesMeasureAndRenderAndClearRestoresDefault()
        {
            var border = new Border { Background = Blue };
            var root = new UIRoot(border);
            var context = new EmptyDrawingContext();
            root.UpdateLayout(new Size(100, 80));
            root.Render(context);
            border.SetValue(Border.CornerRadiusProperty, new CornerRadius(8));
            Assert.That(border.IsMeasureValid, Is.False);
            Assert.That(root.IsRenderDirty, Is.True);
            root.UpdateLayout(new Size(100, 80));
            root.Render(context);
            border.CornerRadius = new CornerRadius(8);
            Assert.That(border.IsMeasureValid, Is.True);
            Assert.That(root.IsRenderDirty, Is.False);
            border.ClearValue(Border.CornerRadiusProperty);
            Assert.That(border.CornerRadius, Is.EqualTo(new CornerRadius(0)));
            Assert.That(border.IsMeasureValid, Is.False);
            Assert.That(root.IsRenderDirty, Is.True);
        }

        [Test]
        public void UnchangedRoundedLayoutAndRenderAllocateNothing()
        {
            var root = new UIRoot(new Border { CornerRadius = new CornerRadius(8), BorderThickness = new Thickness(2),
                BorderBrush = Red, Background = Blue,
                Child = new Border { CornerRadius = new CornerRadius(40), Background = Red } });
            var context = new EmptyDrawingContext();
            var viewport = new Size(100, 80);
            root.UpdateLayout(viewport);
            root.Render(context);
            long before = GC.GetAllocatedBytesForCurrentThread();
            root.UpdateLayout(viewport);
            root.Render(context);
            long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
            Assert.That(allocated, Is.Zero);
        }

        private static RecordingDrawingContext Render(Border border, double width = 100, double height = 80)
        {
            var root = new UIRoot(border);
            root.UpdateLayout(new Size(width, height));
            var context = new RecordingDrawingContext();
            root.Render(context);
            return context;
        }
    }
}
