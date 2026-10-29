using System;
using System.Linq;
using NUnit.Framework;
using Xamlity.Controls;
using Xamlity.Layout;
using Xamlity.Markup;
using Xamlity.Properties;
using Xamlity.Rendering;
using Xamlity.Tree;
using FakeTextMeasurer = Xamlity.Core.Tests.Rendering.TextBlockTests.FakeTextMeasurer;

namespace Xamlity.Core.Tests.Rendering
{
    public class TextWrappingTests
    {
        private static readonly FontHandle Font = new FontHandle(new object());
        private static readonly Size Viewport = new Size(30, 200);

        [Test]
        public void DefaultsAndMetadataMatchWpf()
        {
            var text = new TextBlock();
            Assert.That(Enum.GetNames(typeof(TextWrapping)),
                Is.EqualTo(new[] { "WrapWithOverflow", "NoWrap", "Wrap" }));
            Assert.That(Enum.GetNames(typeof(TextAlignment)),
                Is.EqualTo(new[] { "Left", "Right", "Center", "Justify" }));
            Assert.That(text.TextWrapping, Is.EqualTo(TextWrapping.NoWrap));
            Assert.That(text.TextAlignment, Is.EqualTo(TextAlignment.Left));
            var wrapping = (FrameworkPropertyMetadata)TextBlock.TextWrappingProperty.DefaultMetadata;
            var alignment = (FrameworkPropertyMetadata)TextBlock.TextAlignmentProperty.DefaultMetadata;
            Assert.That(wrapping.AffectsMeasure && wrapping.AffectsRender && !wrapping.Inherits, Is.True);
            Assert.That(alignment.AffectsMeasure && alignment.AffectsRender && alignment.Inherits, Is.True);
        }

        [TestCase(TextAlignment.Left, 0, 0, 5)]
        [TestCase(TextAlignment.Center, 2.5, 10, 5)]
        [TestCase(TextAlignment.Right, 5, 20, 5)]
        [TestCase(TextAlignment.Justify, 0, 0, 10)]
        public void WrapsWordsAndAlignsEachLine(TextAlignment alignment, double firstX, double secondX,
            double spaceAdvance)
        {
            var text = new TextBlock { Text = "AB CD EF", FontSize = 10,
                TextWrapping = TextWrapping.Wrap, TextAlignment = alignment };
            var root = new UIRoot(text, new FakeTextMeasurer(), Font);
            root.UpdateLayout(Viewport);
            var context = new RecordingDrawingContext();
            root.Render(context);
            AssertLines(context, "AB CD", "EF");
            Assert.That(text.DesiredSize, Is.EqualTo(new Size(25, 24)));
            Assert.That(context.GlyphRuns[0].BaselineOrigin, Is.EqualTo(new Point(firstX, 8)));
            Assert.That(context.GlyphRuns[1].BaselineOrigin, Is.EqualTo(new Point(secondX, 20)));
            Assert.That(context.GlyphRuns[0].Run.AdvanceWidths, Is.EqualTo(new[] { 5, 5, spaceAdvance, 5, 5 }));
            Assert.That(context.GlyphRuns[1].Run.AdvanceWidths.Sum(), Is.EqualTo(10));
        }

        [TestCase("ABCDEFGH", TextWrapping.Wrap, 30, new[] { "ABCDEF", "GH" })]
        [TestCase("ABCDEFGH", TextWrapping.WrapWithOverflow, 30, new[] { "ABCDEFGH" })]
        [TestCase("ABCDEFGH", TextWrapping.NoWrap, 30, new[] { "ABCDEFGH" })]
        [TestCase("AB CDEFGHIJ", TextWrapping.Wrap, 30, new[] { "AB", "CDEFGH", "IJ" })]
        [TestCase("AB CDEFGHIJ", TextWrapping.WrapWithOverflow, 30, new[] { "AB", "CDEFGHIJ" })]
        [TestCase("가나 다라 마바", TextWrapping.Wrap, 25, new[] { "가나 다라", "마바" })]
        [TestCase("가나다라마바사", TextWrapping.WrapWithOverflow, 25, new[] { "가나다라마바사" })]
        [TestCase("AB-CD", TextWrapping.WrapWithOverflow, 15, new[] { "AB-CD" })]
        [TestCase("AB\tCD", TextWrapping.WrapWithOverflow, 15, new[] { "AB\tCD" })]
        [TestCase("AB\u00a0CD", TextWrapping.WrapWithOverflow, 15, new[] { "AB\u00a0CD" })]
        [TestCase("A\U0001F600BC", TextWrapping.Wrap, 10, new[] { "A\U0001F600", "BC" })]
        [TestCase("AB", TextWrapping.Wrap, 0, new[] { "A", "B" })]
        [TestCase("AB", TextWrapping.Wrap, 1, new[] { "A", "B" })]
        [TestCase("AB CD EF", TextWrapping.Wrap, double.PositiveInfinity, new[] { "AB CD EF" })]
        [TestCase("AB CD EF", TextWrapping.WrapWithOverflow, double.PositiveInfinity, new[] { "AB CD EF" })]
        public void BreaksOnlyAtSpacesOrCodePointsAsRequired(string value, TextWrapping wrapping, double width,
            string[] expected)
        {
            var text = new TextBlock { Text = value, FontSize = 10, TextWrapping = wrapping };
            var root = new UIRoot(text, new FakeTextMeasurer(), Font);
            // Measuring alone isolates the constraint from FrameworkElement's minimum arrange width.
            text.Measure(new Size(width, 200));
            var context = new RecordingDrawingContext();
            root.Render(context);
            AssertLines(context, expected);
            Assert.That(text.DesiredSize.Height, Is.EqualTo(expected.Length * 12));
            for (int index = 0; index < expected.Length; index++)
                Assert.That(context.GlyphRuns[index].Run.AdvanceWidths.Sum(),
                    Is.EqualTo(expected[index].EnumerateRunes().Count() * 5));
        }

        [TestCase(TextWrapping.NoWrap)]
        [TestCase(TextWrapping.Wrap)]
        [TestCase(TextWrapping.WrapWithOverflow)]
        public void HardBreaksPreserveEmptyLinesAndNeverReachMeasurer(TextWrapping wrapping)
        {
            foreach (string separator in new[] { "\n", "\r\n", "\r" })
            {
                foreach (bool emptyLine in new[] { false, true })
                {
                    var text = new TextBlock { Text = "AB" + separator + (emptyLine ? separator : "") + "CD",
                        FontSize = 10, TextWrapping = wrapping };
                    var measurer = new FakeTextMeasurer();
                    var root = new UIRoot(text, measurer, Font);
                    root.UpdateLayout(Viewport);
                    var context = new RecordingDrawingContext();
                    root.Render(context);
                    AssertLines(context, emptyLine ? new[] { "AB", "", "CD" } : new[] { "AB", "CD" });
                    Assert.That(measurer.CodePoints, Is.EqualTo(new[] { 65, 66, 67, 68 }));
                    Assert.That(text.DesiredSize, Is.EqualTo(new Size(10, emptyLine ? 36 : 24)));
                    for (int index = 0; index < context.GlyphRuns.Count; index++)
                        Assert.That(context.GlyphRuns[index].BaselineOrigin.Y, Is.EqualTo(8 + index * 12));
                }
            }
        }

        [TestCase("", new[] { "" })]
        [TestCase("\n", new[] { "", "" })]
        [TestCase("\r\n\r\n", new[] { "", "", "" })]
        [TestCase("AB\r", new[] { "AB", "" })]
        public void EmptyAndTrailingLinesHaveHeightAndEmptyRuns(string value, string[] expected)
        {
            var text = new TextBlock { Text = value, FontSize = 10, TextWrapping = TextWrapping.Wrap };
            var root = new UIRoot(text, new FakeTextMeasurer(), Font);
            root.UpdateLayout(Viewport);
            var context = new RecordingDrawingContext();
            root.Render(context);
            AssertLines(context, expected);
            Assert.That(text.DesiredSize.Height, Is.EqualTo(expected.Length * 12));
        }

        [TestCase("AB   CD", TextWrapping.Wrap, new[] { "AB", "CD" })]
        [TestCase("AB   CD", TextWrapping.WrapWithOverflow, new[] { "AB", "CD" })]
        [TestCase("AB   \nCD", TextWrapping.Wrap, new[] { "AB   ", "CD" })]
        [TestCase("AB   ", TextWrapping.Wrap, new[] { "AB   " })]
        [TestCase("AB   \nCD", TextWrapping.NoWrap, new[] { "AB   ", "CD" })]
        [TestCase("AB   CD", TextWrapping.NoWrap, new[] { "AB   CD" })]
        public void OnlySoftBreaksRemoveTrailingSpaces(string value, TextWrapping wrapping, string[] expected)
        {
            var text = new TextBlock { Text = value, FontSize = 10, TextWrapping = wrapping };
            var root = new UIRoot(text, new FakeTextMeasurer(), Font);
            text.Measure(new Size(15, 200));
            var context = new RecordingDrawingContext();
            root.Render(context);
            AssertLines(context, expected);
            for (int index = 0; index < expected.Length; index++)
                Assert.That(context.GlyphRuns[index].Run.AdvanceWidths.Sum(), Is.EqualTo(expected[index].Length * 5));
        }

        [Test]
        public void JustifyExpandsOnlyInternalSpacesOnSoftLines()
        {
            var text = new TextBlock { Text = " A B C D\nEF\nG H", FontSize = 10,
                TextWrapping = TextWrapping.Wrap, TextAlignment = TextAlignment.Justify };
            var root = new UIRoot(text, new FakeTextMeasurer(), Font);
            root.UpdateLayout(new Size(35, 200));
            var context = new RecordingDrawingContext();
            root.Render(context);
            AssertLines(context, " A B C", "D", "EF", "G H");
            Assert.That(context.GlyphRuns[0].Run.AdvanceWidths, Is.EqualTo(new[] { 5, 5, 7.5, 5, 7.5, 5 }));
            foreach (var command in context.GlyphRuns.Skip(1))
                Assert.That(command.Run.AdvanceWidths, Is.All.EqualTo(5));
            Assert.That(context.GlyphRuns.Select(command => command.BaselineOrigin.X), Is.All.Zero);
        }

        [Test]
        public void JustifyLeavesHardBreakAndLastLineSpacesUnchanged()
        {
            var text = new TextBlock { Text = "A B \nC D", FontSize = 10,
                TextWrapping = TextWrapping.Wrap, TextAlignment = TextAlignment.Justify };
            var root = new UIRoot(text, new FakeTextMeasurer(), Font);
            root.UpdateLayout(Viewport);
            var context = new RecordingDrawingContext();
            root.Render(context);
            AssertLines(context, "A B ", "C D");
            foreach (var command in context.GlyphRuns)
            {
                Assert.That(command.Run.AdvanceWidths, Is.All.EqualTo(5));
                Assert.That(command.BaselineOrigin.X, Is.Zero);
            }
        }

        [Test]
        public void ArrangeRewrapsAtFinalWidthAndAlignmentChangesReuseBothLayouts()
        {
            var text = new TextBlock { Text = "AB CD EF", FontSize = 10, TextWrapping = TextWrapping.Wrap };
            var parent = new DifferentWidthParent { Child = text };
            var measurer = new FakeTextMeasurer();
            var root = new UIRoot(parent, measurer, Font);
            root.UpdateLayout(new Size(60, 200));
            var context = new RecordingDrawingContext();
            root.Render(context);
            Assert.That(text.DesiredSize, Is.EqualTo(new Size(10, 36)));
            Assert.That(text.RenderSize.Width, Is.EqualTo(30));
            AssertLines(context, "AB CD", "EF");
            GlyphRun first = context.GlyphRuns[0].Run;
            GlyphRun second = context.GlyphRuns[1].Run;

            text.TextAlignment = TextAlignment.Right;
            root.UpdateLayout(new Size(60, 200));
            context.GlyphRuns.Clear();
            root.Render(context);
            Assert.That(context.GlyphRuns[0].Run, Is.SameAs(first));
            Assert.That(context.GlyphRuns[1].Run, Is.SameAs(second));
            Assert.That(context.GlyphRuns[0].BaselineOrigin.X, Is.EqualTo(5));
            Assert.That(measurer.MetricsCalls, Is.EqualTo(1));
            Assert.That(measurer.CodePoints.Count, Is.EqualTo(8));
        }

        [Test]
        public void AlignmentInheritsAndEqualValuesReuseRuns()
        {
            var text = new TextBlock { Text = "AB CD EF", FontSize = 10, TextWrapping = TextWrapping.Wrap };
            var border = new Border { Child = text };
            border.SetValue(TextBlock.TextAlignmentProperty, TextAlignment.Center);
            var measurer = new FakeTextMeasurer();
            var root = new UIRoot(border, measurer, Font);
            var context = new RecordingDrawingContext();
            root.UpdateLayout(Viewport);
            root.Render(context);
            GlyphRun first = context.GlyphRuns[0].Run;
            Assert.That(text.TextAlignment, Is.EqualTo(TextAlignment.Center));
            Assert.That(context.GlyphRuns[0].BaselineOrigin.X, Is.EqualTo(2.5));

            border.SetValue(TextBlock.TextAlignmentProperty, TextAlignment.Right);
            Assert.That(text.IsMeasureValid, Is.False);
            Assert.That(root.IsRenderDirty, Is.True);
            root.UpdateLayout(Viewport);
            context.GlyphRuns.Clear();
            root.Render(context);
            Assert.That(context.GlyphRuns[0].Run, Is.SameAs(first));
            Assert.That(context.GlyphRuns[0].BaselineOrigin.X, Is.EqualTo(5));
            border.SetValue(TextBlock.TextAlignmentProperty, TextAlignment.Right);
            text.TextWrapping = TextWrapping.Wrap;
            text.Text = new string("AB CD EF".ToCharArray());
            Assert.That(text.IsMeasureValid, Is.True);
            Assert.That(root.IsRenderDirty, Is.False);
            root.UpdateLayout(Viewport);
            context.GlyphRuns.Clear();
            root.Render(context);
            Assert.That(context.GlyphRuns[0].Run, Is.SameAs(first));
            Assert.That(measurer.MetricsCalls, Is.EqualTo(1));
        }

        [Test]
        public void WrappingModeAndWidthChangesInvalidateLinesWithoutRemeasuringGlyphs()
        {
            var text = new TextBlock { Text = "AB CD EF", FontSize = 10 };
            var measurer = new FakeTextMeasurer();
            var root = new UIRoot(text, measurer, Font);
            var context = new RecordingDrawingContext();
            root.UpdateLayout(Viewport);
            root.Render(context);
            AssertLines(context, "AB CD EF");
            text.TextWrapping = TextWrapping.Wrap;
            Assert.That(text.IsMeasureValid, Is.False);
            Assert.That(root.IsRenderDirty, Is.True);
            root.UpdateLayout(Viewport);
            context.GlyphRuns.Clear();
            root.Render(context);
            AssertLines(context, "AB CD", "EF");
            root.UpdateLayout(new Size(15, 200));
            context.GlyphRuns.Clear();
            root.Render(context);
            AssertLines(context, "AB", "CD", "EF");
            Assert.That(measurer.MetricsCalls, Is.EqualTo(1));
            Assert.That(measurer.CodePoints.Count, Is.EqualTo(8));
        }

        [TestCase(TextAlignment.Left)]
        [TestCase(TextAlignment.Right)]
        [TestCase(TextAlignment.Center)]
        [TestCase(TextAlignment.Justify)]
        public void UnchangedMultilineLayoutAndRenderAllocateNothing(TextAlignment alignment)
        {
            var text = new TextBlock { Text = "AB CD EF\n\n가나 다라 마바", FontSize = 10,
                TextWrapping = TextWrapping.Wrap, TextAlignment = alignment };
            var root = new UIRoot(text, new FakeTextMeasurer(), Font);
            var context = new RecordingDrawingContext();
            root.UpdateLayout(Viewport);
            root.Render(context);
            GlyphRun first = context.GlyphRuns[0].Run;
            context.GlyphRuns.Clear();
            long before = GC.GetAllocatedBytesForCurrentThread();
            root.UpdateLayout(Viewport);
            root.Render(context);
            long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
            Assert.That(allocated, Is.Zero);
            Assert.That(context.GlyphRuns[0].Run, Is.SameAs(first));
        }

        [Test]
        public void InvalidEnumValuesAreRejected()
        {
            var text = new TextBlock { TextWrapping = (TextWrapping)100 };
            Assert.Throws<ArgumentOutOfRangeException>(() => text.Measure(Viewport));
            text.TextWrapping = TextWrapping.NoWrap;
            text.TextAlignment = (TextAlignment)100;
            Assert.Throws<ArgumentOutOfRangeException>(() => text.Measure(Viewport));
        }

        [TestCase(TextAlignment.Right, -10)]
        [TestCase(TextAlignment.Center, -5)]
        public void OverflowAlignmentKeepsNegativeOffsets(TextAlignment alignment, double expectedX)
        {
            var text = new RenderWidthTextBlock { Text = "ABCDEFGH", FontSize = 10, TextAlignment = alignment };
            var root = new UIRoot(text, new FakeTextMeasurer(), Font);
            root.UpdateLayout(Viewport);
            text.SetRenderWidth(30);
            var context = new RecordingDrawingContext();
            root.Render(context);
            Assert.That(context.GlyphRuns[0].BaselineOrigin.X, Is.EqualTo(expectedX));
        }

        [Test]
        public void JustificationCachesAdvancesWithoutChangingOriginalRuns()
        {
            var text = new RenderWidthTextBlock { Text = "AB CD EF", FontSize = 10,
                TextWrapping = TextWrapping.Wrap };
            var measurer = new FakeTextMeasurer();
            var root = new UIRoot(text, measurer, Font);
            var context = new RecordingDrawingContext();
            root.UpdateLayout(Viewport);
            root.Render(context);
            GlyphRun original = context.GlyphRuns[0].Run;
            GlyphRun last = context.GlyphRuns[1].Run;

            text.TextAlignment = TextAlignment.Justify;
            root.UpdateLayout(Viewport);
            context.GlyphRuns.Clear();
            root.Render(context);
            GlyphRun justified = context.GlyphRuns[0].Run;
            Assert.That(justified.GlyphIndices, Is.SameAs(original.GlyphIndices));
            Assert.That(justified.AdvanceWidths[2], Is.EqualTo(10));
            Assert.That(original.AdvanceWidths[2], Is.EqualTo(5));
            Assert.That(context.GlyphRuns[1].Run, Is.SameAs(last));

            text.SetRenderWidth(35);
            context.GlyphRuns.Clear();
            root.Render(context);
            GlyphRun widened = context.GlyphRuns[0].Run;
            Assert.That(widened, Is.Not.SameAs(justified));
            Assert.That(widened.GlyphIndices, Is.SameAs(original.GlyphIndices));
            Assert.That(widened.AdvanceWidths[2], Is.EqualTo(15));
            Assert.That(context.GlyphRuns[1].Run, Is.SameAs(last));
            context.GlyphRuns.Clear();
            root.Render(context);
            Assert.That(context.GlyphRuns[0].Run, Is.SameAs(widened));
            Assert.That(measurer.MetricsCalls, Is.EqualTo(1));

            text.TextAlignment = TextAlignment.Left;
            root.UpdateLayout(Viewport);
            context.GlyphRuns.Clear();
            root.Render(context);
            Assert.That(context.GlyphRuns[0].Run, Is.SameAs(original));
        }

        [Test]
        public void XamlPropertiesRoundTripAndRenderWrappedAlignedText()
        {
            var parsed = XamlParser.Parse("<TextBlock xmlns=\"https://github.com/loonacia/xamlity\" " +
                "Text=\"AB CD EF\" FontSize=\"10\" TextWrapping=\"Wrap\" TextAlignment=\"Center\"/>", "text.xaml");
            Assert.That(parsed.Errors, Is.Empty);
            var document = XamlIrSerializer.Deserialize(XamlIrSerializer.Serialize(parsed.Document));
            var text = (TextBlock)XamlBuilder.Build(document);
            var root = new UIRoot(text, new FakeTextMeasurer(), Font);
            root.UpdateLayout(Viewport);
            var context = new RecordingDrawingContext();
            root.Render(context);
            AssertLines(context, "AB CD", "EF");
            Assert.That(context.GlyphRuns[0].BaselineOrigin, Is.EqualTo(new Point(2.5, 8)));
            Assert.That(context.GlyphRuns[1].BaselineOrigin, Is.EqualTo(new Point(10, 20)));
        }

        [TestCase("TextWrapping")]
        [TestCase("TextAlignment")]
        public void XamlRejectsUnsupportedEnumNames(string property)
        {
            var parsed = XamlParser.Parse("<TextBlock xmlns=\"https://github.com/loonacia/xamlity\"\n  " +
                property + "=\"Unsupported\"/>", "text.xaml");
            Assert.That(parsed.Errors.Count, Is.EqualTo(1));
            Assert.That(parsed.Errors[0].Line, Is.EqualTo(2));
        }

        private static void AssertLines(RecordingDrawingContext context, params string[] expected)
        {
            Assert.That(context.GlyphRuns.Count, Is.EqualTo(expected.Length));
            for (int index = 0; index < expected.Length; index++)
                Assert.That(context.GlyphRuns[index].Run.GlyphIndices,
                    Is.EqualTo(expected[index].EnumerateRunes().Select(rune => (uint)rune.Value + 1).ToArray()));
        }

        private sealed class DifferentWidthParent : Border
        {
            protected override Size MeasureOverride(Size availableSize)
            {
                Child.Measure(new Size(15, availableSize.Height));
                return Child.DesiredSize;
            }

            protected override Size ArrangeOverride(Size finalSize)
            {
                Child.Arrange(new Rect(0, 0, 30, finalSize.Height));
                return finalSize;
            }
        }

        private sealed class RenderWidthTextBlock : TextBlock
        {
            internal void SetRenderWidth(double width) => RenderSize = new Size(width, RenderSize.Height);
        }
    }
}
