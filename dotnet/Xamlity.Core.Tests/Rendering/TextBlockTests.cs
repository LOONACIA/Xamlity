using System;
using System.Collections.Generic;
using NUnit.Framework;
using Xamlity.Controls;
using Xamlity.Layout;
using Xamlity.Rendering;
using Xamlity.Tree;

namespace Xamlity.Core.Tests.Rendering
{
    public class TextBlockTests
    {
        private static readonly Size Viewport = new Size(300, 200);
        private static readonly FontHandle Font = new FontHandle(new object());

        [Test]
        public void FontHandleUsesSourceIdentityAndDefaultIsInvalid()
        {
            var source = new string('a', 1);
            var font = new FontHandle(source);
            var equal = new FontHandle(source);
            Assert.That(font.Source, Is.SameAs(source));
            Assert.That(font.IsValid, Is.True);
            Assert.That(font.Equals(equal), Is.True);
            Assert.That(font.Equals((object)equal), Is.True);
            Assert.That(font == equal, Is.True);
            Assert.That(font.GetHashCode(), Is.EqualTo(equal.GetHashCode()));
            Assert.That(font != new FontHandle(new string('a', 1)), Is.True);
            Assert.That(font.Equals(null), Is.False);
            Assert.That(default(FontHandle).IsValid, Is.False);
            Assert.That(new FontHandle(null), Is.EqualTo(default(FontHandle)));
            Assert.That(default(FontHandle).GetHashCode(), Is.Zero);
        }

        [Test]
        public void MeasuresSingleLineAndRendersMeasuredGlyphsAtBaseline()
        {
            var text = new TextBlock { Text = "A한글" };
            var measurer = new FakeTextMeasurer();
            var root = new UIRoot(text, measurer, Font);
            root.UpdateLayout(Viewport);
            var context = new RecordingDrawingContext();
            root.Render(context);

            Assert.That(root.TextMeasurer, Is.SameAs(measurer));
            Assert.That(root.DefaultFont, Is.EqualTo(Font));
            Assert.That(text.DesiredSize.Width, Is.EqualTo(18));
            Assert.That(text.DesiredSize.Height, Is.EqualTo(14.4).Within(0.000001));
            Assert.That(context.GlyphRuns.Count, Is.EqualTo(1));
            var command = context.GlyphRuns[0];
            Assert.That(((SolidColorBrush)command.Foreground).Color, Is.EqualTo(Color.FromRgb(0, 0, 0)));
            Assert.That(command.Run.Font, Is.EqualTo(Font));
            Assert.That(command.Run.FontSize, Is.EqualTo(12));
            Assert.That(command.Run.GlyphIndices, Is.EqualTo(new uint[] { 'A' + 1, '한' + 1, '글' + 1 }));
            Assert.That(command.Run.AdvanceWidths, Is.EqualTo(new[] { 6.0, 6.0, 6.0 }));
            Assert.That(command.BaselineOrigin.X, Is.Zero);
            Assert.That(command.BaselineOrigin.Y, Is.EqualTo(9.6).Within(0.000001));
            Assert.That(measurer.LastFont, Is.EqualTo(Font));
            Assert.That(measurer.LastFontSize, Is.EqualTo(12));
            Assert.That(new FontMetrics(8, 2, 12).Descent, Is.EqualTo(2));
        }

        [Test]
        public void BorderOffsetsBaselineToAbsoluteCoordinates()
        {
            var text = new TextBlock { Text = "한", FontSize = 20, Margin = new Thickness(1, 2, 0, 0) };
            var border = new Border { Margin = new Thickness(5, 7, 0, 0),
                BorderThickness = new Thickness(2, 3, 4, 5), Padding = new Thickness(6, 8, 10, 12), Child = text };
            var root = new UIRoot(border, new FakeTextMeasurer(), Font);
            root.UpdateLayout(Viewport);
            var context = new RecordingDrawingContext();
            root.Render(context);
            Assert.That(context.GlyphRuns[0].BaselineOrigin, Is.EqualTo(new Point(14, 36)));
        }

        [TestCase("A?B", new[] { 65, 63, 66 }, new uint[] { 66, 67 })]
        [TestCase("A\U0001F600B", new[] { 65, 0x1F600, 66 }, new uint[] { 66, 0x1F601, 67 })]
        [TestCase("??", new[] { 63, 63 }, new uint[0])]
        public void VisitsCodePointsAndSkipsOnlyMissingGlyphs(string value, int[] codePoints, uint[] glyphs)
        {
            var text = new TextBlock { Text = value };
            var measurer = new FakeTextMeasurer();
            var root = new UIRoot(text, measurer, Font);
            root.UpdateLayout(Viewport);
            var context = new RecordingDrawingContext();
            root.Render(context);
            Assert.That(measurer.CodePoints, Is.EqualTo(codePoints));
            Assert.That(context.GlyphRuns[0].Run.GlyphIndices, Is.EqualTo(glyphs));
            Assert.That(context.GlyphRuns[0].Run.AdvanceWidths.Length, Is.EqualTo(glyphs.Length));
            Assert.That(text.DesiredSize.Width, Is.EqualTo(glyphs.Length * 6));
            Assert.That(text.DesiredSize.Height, Is.EqualTo(14.4).Within(0.000001));
        }

        [Test]
        public void CarriageReturnLineFeedCreatesTwoLinesWithoutMeasuringBreakCharacters()
        {
            var text = new TextBlock { Text = "A\r\nB" };
            var measurer = new FakeTextMeasurer();
            var root = new UIRoot(text, measurer, Font);
            root.UpdateLayout(Viewport);
            var context = new RecordingDrawingContext();
            root.Render(context);
            Assert.That(measurer.CodePoints, Is.EqualTo(new[] { 65, 66 }));
            Assert.That(context.GlyphRuns.Count, Is.EqualTo(2));
            Assert.That(context.GlyphRuns[0].Run.GlyphIndices, Is.EqualTo(new uint[] { 66 }));
            Assert.That(context.GlyphRuns[1].Run.GlyphIndices, Is.EqualTo(new uint[] { 67 }));
            Assert.That(text.DesiredSize.Width, Is.EqualTo(6));
            Assert.That(text.DesiredSize.Height, Is.EqualTo(28.8).Within(0.000001));
        }

        [TestCase("")]
        [TestCase(null)]
        public void EmptyTextHasLineHeightAndNoGlyphs(string value)
        {
            var text = new TextBlock { Text = value };
            var root = new UIRoot(text, new FakeTextMeasurer(), Font);
            root.UpdateLayout(Viewport);
            var context = new RecordingDrawingContext();
            root.Render(context);
            Assert.That(text.DesiredSize.Width, Is.Zero);
            Assert.That(text.DesiredSize.Height, Is.EqualTo(14.4).Within(0.000001));
            Assert.That(context.GlyphRuns[0].Run.GlyphIndices, Is.Empty);
        }

        [TestCase(true)]
        [TestCase(false)]
        public void MissingMeasurerOrFontMeasuresZeroAndDoesNotDraw(bool missingMeasurer)
        {
            var text = new TextBlock { Text = "A" };
            var measurer = new FakeTextMeasurer();
            var root = missingMeasurer ? new UIRoot(text) : new UIRoot(text, measurer, default);
            root.UpdateLayout(Viewport);
            var context = new RecordingDrawingContext();
            root.Render(context);
            Assert.That(text.DesiredSize, Is.EqualTo(new Size(0, 0)));
            Assert.That(context.GlyphRuns, Is.Empty);
            Assert.That(measurer.MetricsCalls, Is.Zero);
        }

        [Test]
        public void FontSizeInheritanceInvalidatesMeasurementAndRebuildsRun()
        {
            var text = new TextBlock { Text = "AB" };
            var border = new Border { Child = text };
            border.SetValue(TextBlock.FontSizeProperty, 24.0);
            var measurer = new FakeTextMeasurer();
            var root = new UIRoot(border, measurer, Font);
            var context = new RecordingDrawingContext();
            root.UpdateLayout(Viewport);
            root.Render(context);
            Assert.That(text.FontSize, Is.EqualTo(24));
            Assert.That(text.DesiredSize.Width, Is.EqualTo(24));
            Assert.That(text.DesiredSize.Height, Is.EqualTo(28.8).Within(0.000001));

            border.SetValue(TextBlock.FontSizeProperty, 30.0);
            Assert.That(text.IsMeasureValid, Is.False);
            Assert.That(root.IsRenderDirty, Is.True);
            root.UpdateLayout(Viewport);
            root.Render(context);
            Assert.That(text.DesiredSize.Width, Is.EqualTo(30));
            Assert.That(measurer.MetricsCalls, Is.EqualTo(2));
            Assert.That(context.GlyphRuns[1].Run, Is.Not.SameAs(context.GlyphRuns[0].Run));
            Assert.That(context.GlyphRuns[1].Run.FontSize, Is.EqualTo(30));
        }

        [Test]
        public void EqualTextDoesNotRemeasureButChangedTextDoes()
        {
            var text = new TextBlock { Text = "AB" };
            var measurer = new FakeTextMeasurer();
            var root = new UIRoot(text, measurer, Font);
            var context = new RecordingDrawingContext();
            root.UpdateLayout(Viewport);
            root.Render(context);
            text.Text = new string(new[] { 'A', 'B' });
            Assert.That(text.IsMeasureValid, Is.True);
            Assert.That(root.IsRenderDirty, Is.False);
            root.UpdateLayout(Viewport);
            root.Render(context);
            Assert.That(measurer.MetricsCalls, Is.EqualTo(1));
            Assert.That(context.GlyphRuns[1].Run, Is.SameAs(context.GlyphRuns[0].Run));

            text.Text = "ABC";
            Assert.That(text.IsMeasureValid, Is.False);
            Assert.That(root.IsRenderDirty, Is.True);
            root.UpdateLayout(Viewport);
            root.Render(context);
            Assert.That(text.DesiredSize.Width, Is.EqualTo(18));
            Assert.That(measurer.MetricsCalls, Is.EqualTo(2));
            Assert.That(context.GlyphRuns[2].Run, Is.Not.SameAs(context.GlyphRuns[0].Run));
        }

        [Test]
        public void ForegroundInheritanceAndConstraintChangesReuseRun()
        {
            var text = new TextBlock { Text = "AB" };
            var border = new Border { Child = text };
            var measurer = new FakeTextMeasurer();
            var root = new UIRoot(border, measurer, Font);
            var context = new RecordingDrawingContext();
            root.UpdateLayout(Viewport);
            root.Render(context);
            var blue = new SolidColorBrush(Color.FromRgb(0, 0, 255));
            border.SetValue(TextBlock.ForegroundProperty, blue);
            Assert.That(root.IsRenderDirty, Is.True);
            Assert.That(text.IsMeasureValid, Is.True);
            root.UpdateLayout(new Size(250, 150));
            root.Render(context);
            Assert.That(context.GlyphRuns[1].Foreground, Is.SameAs(blue));
            Assert.That(context.GlyphRuns[1].Run, Is.SameAs(context.GlyphRuns[0].Run));
            Assert.That(measurer.MetricsCalls, Is.EqualTo(1));
            text.Foreground = null;
            Assert.That(root.IsRenderDirty, Is.True);
            root.Render(context);
            Assert.That(context.GlyphRuns.Count, Is.EqualTo(2));
        }

        [Test]
        public void AttachingAlreadyMeasuredSubtreeUsesRootTextServices()
        {
            var text = new TextBlock { Text = "AB" };
            var border = new Border { Child = text };
            border.Measure(Viewport);
            Assert.That(text.DesiredSize, Is.EqualTo(new Size(0, 0)));
            var root = new UIRoot(border, new FakeTextMeasurer(), Font);
            root.UpdateLayout(Viewport);
            Assert.That(text.DesiredSize.Width, Is.EqualTo(12));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void MovingSubtreeRebuildsRunForChangedFontOrMeasurer(bool changeMeasurer)
        {
            var text = new TextBlock { Text = "AB" };
            var subtree = new Border { Child = text };
            var first = new Border { Child = subtree };
            var second = new Border();
            var measurer = new FakeTextMeasurer();
            var nextMeasurer = changeMeasurer ? new FakeTextMeasurer() : measurer;
            var nextFont = changeMeasurer ? Font : new FontHandle(new object());
            var firstRoot = new UIRoot(first, measurer, Font);
            var secondRoot = new UIRoot(second, nextMeasurer, nextFont);
            var context = new RecordingDrawingContext();
            firstRoot.UpdateLayout(Viewport);
            firstRoot.Render(context);
            secondRoot.UpdateLayout(Viewport);
            first.Child = null;
            second.Child = subtree;
            secondRoot.UpdateLayout(Viewport);
            secondRoot.Render(context);
            Assert.That(context.GlyphRuns[1].Run, Is.Not.SameAs(context.GlyphRuns[0].Run));
            Assert.That(context.GlyphRuns[1].Run.Font, Is.EqualTo(nextFont));
            Assert.That(nextMeasurer.MetricsCalls, Is.EqualTo(changeMeasurer ? 1 : 2));

            second.Child = null;
            var noTextRoot = new UIRoot(new Border { Child = subtree });
            noTextRoot.UpdateLayout(Viewport);
            context.GlyphRuns.Clear();
            noTextRoot.Render(context);
            Assert.That(text.DesiredSize, Is.EqualTo(new Size(0, 0)));
            Assert.That(context.GlyphRuns, Is.Empty);
        }

        [Test]
        public void SecondUnchangedLayoutAndRenderAllocateNothing()
        {
            var text = new TextBlock { Text = "한글\U0001F600", FontSize = 24 };
            var root = new UIRoot(new Border { Padding = new Thickness(3), Child = text },
                new FakeTextMeasurer(), Font);
            var context = new EmptyDrawingContext();
            root.UpdateLayout(Viewport);
            root.Render(context);

            long before = GC.GetAllocatedBytesForCurrentThread();
            root.UpdateLayout(Viewport);
            root.Render(context);
            long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
            Assert.That(allocated, Is.Zero);
        }

        internal sealed class FakeTextMeasurer : ITextMeasurer
        {
            public int MetricsCalls { get; private set; }
            public FontHandle LastFont { get; private set; }
            public double LastFontSize { get; private set; }
            public List<int> CodePoints { get; } = new List<int>();

            public FontMetrics GetFontMetrics(FontHandle font, double fontSize)
            {
                MetricsCalls++;
                LastFont = font;
                LastFontSize = fontSize;
                return new FontMetrics(fontSize * 0.8, fontSize * 0.2, fontSize * 1.2);
            }

            public bool TryGetGlyph(FontHandle font, int codePoint, double fontSize, out GlyphMetrics glyph)
            {
                CodePoints.Add(codePoint);
                LastFont = font;
                LastFontSize = fontSize;
                glyph = new GlyphMetrics((uint)codePoint + 1, fontSize * 0.5);
                return codePoint != '?';
            }
        }
    }
}
