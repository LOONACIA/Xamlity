using System;
using System.Collections.Generic;
using Xamlity.Layout;
using Xamlity.Properties;
using Xamlity.Rendering;
using Xamlity.Tree;

namespace Xamlity.Controls
{
    public class TextBlock : FrameworkElement
    {
        public static readonly DependencyProperty TextProperty = DependencyProperty.Register(
            nameof(Text), typeof(string), typeof(TextBlock),
            new FrameworkPropertyMetadata(string.Empty,
                FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsRender));
        public static readonly DependencyProperty FontSizeProperty = DependencyProperty.Register(
            nameof(FontSize), typeof(double), typeof(TextBlock),
            new FrameworkPropertyMetadata(12.0, FrameworkPropertyMetadataOptions.AffectsMeasure |
                FrameworkPropertyMetadataOptions.AffectsRender | FrameworkPropertyMetadataOptions.Inherits));
        public static readonly DependencyProperty ForegroundProperty = DependencyProperty.Register(
            nameof(Foreground), typeof(Brush), typeof(TextBlock),
            new FrameworkPropertyMetadata(new SolidColorBrush(Color.FromRgb(0, 0, 0)),
                FrameworkPropertyMetadataOptions.AffectsRender | FrameworkPropertyMetadataOptions.Inherits));

        public static readonly DependencyProperty TextWrappingProperty = DependencyProperty.Register(
            nameof(TextWrapping), typeof(TextWrapping), typeof(TextBlock),
            new FrameworkPropertyMetadata(TextWrapping.NoWrap,
                FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsRender));
        public static readonly DependencyProperty TextAlignmentProperty = DependencyProperty.Register(
            nameof(TextAlignment), typeof(TextAlignment), typeof(TextBlock),
            new FrameworkPropertyMetadata(TextAlignment.Left, FrameworkPropertyMetadataOptions.AffectsMeasure |
                FrameworkPropertyMetadataOptions.AffectsRender | FrameworkPropertyMetadataOptions.Inherits));

        private Character[] characters;
        private string measuredText;
        private ITextMeasurer measuredWith;
        private FontHandle measuredFont;
        private double measuredFontSize;
        private FontMetrics fontMetrics;
        private TextWrapping layoutWrapping;
        private TextLayout measuredLayout;
        private TextLayout arrangedLayout;

        public string Text
        {
            get => (string)GetValue(TextProperty);
            set => SetValue(TextProperty, value);
        }

        public double FontSize
        {
            get => (double)GetValue(FontSizeProperty);
            set => SetValue(FontSizeProperty, value);
        }

        public Brush Foreground
        {
            get => (Brush)GetValue(ForegroundProperty);
            set => SetValue(ForegroundProperty, value);
        }

        public TextWrapping TextWrapping
        {
            get => (TextWrapping)GetValue(TextWrappingProperty);
            set => SetValue(TextWrappingProperty, value);
        }

        public TextAlignment TextAlignment
        {
            get => (TextAlignment)GetValue(TextAlignmentProperty);
            set => SetValue(TextAlignmentProperty, value);
        }

        protected override Size MeasureOverride(Size availableSize)
        {
            TextWrapping wrapping = TextWrapping;
            if (wrapping < TextWrapping.WrapWithOverflow || wrapping > TextWrapping.Wrap)
                throw new ArgumentOutOfRangeException(nameof(TextWrapping));
            TextAlignment alignment = TextAlignment;
            if (alignment < TextAlignment.Left || alignment > TextAlignment.Justify)
                throw new ArgumentOutOfRangeException(nameof(TextAlignment));

            UIRoot root = GetRoot();
            ITextMeasurer measurer = root?.TextMeasurer;
            FontHandle font = root == null ? default : root.DefaultFont;
            if (measurer == null || !font.IsValid)
            {
                characters = null;
                measuredLayout = null;
                arrangedLayout = null;
                return new Size(0, 0);
            }

            string text = Text ?? string.Empty;
            double fontSize = FontSize;
            if (characters == null || measuredText != text || measuredFontSize != fontSize ||
                measuredFont != font || !ReferenceEquals(measuredWith, measurer))
            {
                MeasureCharacters(text, fontSize, font, measurer);
                measuredLayout = null;
                arrangedLayout = null;
            }
            if (layoutWrapping != wrapping)
            {
                measuredLayout = null;
                arrangedLayout = null;
                layoutWrapping = wrapping;
            }
            double width = wrapping == TextWrapping.NoWrap ? double.PositiveInfinity : availableSize.Width;
            if (measuredLayout == null || measuredLayout.Width != width)
                measuredLayout = arrangedLayout != null && arrangedLayout.Width == width
                    ? arrangedLayout : BreakLines(width);
            return measuredLayout.Size;
        }

        protected override Size ArrangeOverride(Size finalSize)
        {
            if (measuredLayout != null)
            {
                double width = layoutWrapping == TextWrapping.NoWrap ? double.PositiveInfinity : finalSize.Width;
                if (measuredLayout.Width == width)
                    arrangedLayout = measuredLayout;
                else if (arrangedLayout == null || arrangedLayout.Width != width)
                    arrangedLayout = BreakLines(width);
            }
            return finalSize;
        }

        private void MeasureCharacters(string text, double fontSize, FontHandle font, ITextMeasurer measurer)
        {
            fontMetrics = measurer.GetFontMetrics(font, fontSize);
            characters = new Character[text.Length];
            int count = 0;
            for (int index = 0; index < text.Length; index++)
            {
                int codePoint = text[index];
                if (codePoint == '\r' || codePoint == '\n')
                {
                    if (codePoint == '\r' && index + 1 < text.Length && text[index + 1] == '\n')
                        index++;
                    characters[count++] = new Character { CodePoint = '\n' };
                    continue;
                }
                if (char.IsHighSurrogate(text[index]) && index + 1 < text.Length &&
                    char.IsLowSurrogate(text[index + 1]))
                    codePoint = char.ConvertToUtf32(text[index], text[++index]);
                bool hasGlyph = measurer.TryGetGlyph(font, codePoint, fontSize, out GlyphMetrics glyph);
                characters[count++] = new Character
                {
                    CodePoint = codePoint, HasGlyph = hasGlyph,
                    GlyphIndex = glyph.GlyphIndex, Advance = hasGlyph ? glyph.AdvanceWidth : 0
                };
            }
            Array.Resize(ref characters, count);
            measuredText = text;
            measuredWith = measurer;
            measuredFont = font;
            measuredFontSize = fontSize;
        }

        private TextLayout BreakLines(double width)
        {
            var lines = new List<Line>();
            int start = 0;
            int cursor = 0;
            double lineWidth = 0;
            while (cursor < characters.Length)
            {
                Character character = characters[cursor];
                if (character.CodePoint == '\n')
                {
                    lines.Add(CreateLine(start, cursor, false));
                    start = ++cursor;
                    lineWidth = 0;
                    continue;
                }
                // Spaces hang off the line until the following word requires a break.
                if (character.CodePoint == ' ')
                {
                    lineWidth += character.Advance;
                    cursor++;
                    continue;
                }

                int wordEnd = cursor;
                double wordWidth = 0;
                while (wordEnd < characters.Length && characters[wordEnd].CodePoint != ' ' &&
                    characters[wordEnd].CodePoint != '\n')
                    wordWidth += characters[wordEnd++].Advance;
                if (cursor > start && lineWidth + wordWidth > width)
                {
                    lines.Add(CreateLine(start, cursor, true));
                    start = cursor;
                    lineWidth = 0;
                }
                while (cursor < wordEnd)
                {
                    double advance = characters[cursor].Advance;
                    if (layoutWrapping == TextWrapping.Wrap && cursor > start && lineWidth + advance > width)
                    {
                        lines.Add(CreateLine(start, cursor, true));
                        start = cursor;
                        lineWidth = 0;
                    }
                    lineWidth += advance;
                    cursor++;
                }
            }
            lines.Add(CreateLine(start, cursor, false));
            double longest = 0;
            for (int index = 0; index < lines.Count; index++)
                longest = Math.Max(longest, lines[index].Width);
            return new TextLayout { Width = width, Lines = lines,
                Size = new Size(longest, lines.Count * fontMetrics.LineHeight) };
        }

        private Line CreateLine(int start, int end, bool softBreak)
        {
            if (softBreak)
            {
                while (end > start && characters[end - 1].CodePoint == ' ')
                    end--;
            }
            int firstContent = start;
            while (firstContent < end && characters[firstContent].CodePoint == ' ')
                firstContent++;
            var indices = new uint[end - start];
            var advances = new double[end - start];
            var spaces = new List<int>();
            int count = 0;
            double width = 0;
            for (int index = start; index < end; index++)
            {
                Character character = characters[index];
                if (!character.HasGlyph)
                    continue;
                if (softBreak && index > firstContent && character.CodePoint == ' ')
                    spaces.Add(count);
                indices[count] = character.GlyphIndex;
                advances[count++] = character.Advance;
                width += character.Advance;
            }
            Array.Resize(ref indices, count);
            Array.Resize(ref advances, count);
            return new Line { Run = new GlyphRun(measuredFont, measuredFontSize, indices, advances),
                Width = width, Spaces = spaces.ToArray() };
        }

        protected override void OnRender(IDrawingContext drawingContext)
        {
            Brush foreground = Foreground;
            TextLayout layout = arrangedLayout ?? measuredLayout;
            if (foreground == null || layout == null)
                return;
            TextAlignment alignment = TextAlignment;
            double width = RenderSize.Width;
            for (int index = 0; index < layout.Lines.Count; index++)
            {
                Line line = layout.Lines[index];
                double x = 0;
                GlyphRun run = line.Run;
                switch (alignment)
                {
                    case TextAlignment.Left:
                        break;
                    case TextAlignment.Right:
                        x = width - line.Width;
                        break;
                    case TextAlignment.Center:
                        x = (width - line.Width) / 2;
                        break;
                    case TextAlignment.Justify:
                        if (line.Spaces.Length > 0 && width > line.Width)
                        {
                            if (line.JustifiedRun == null || line.JustifiedWidth != width)
                            {
                                var advances = (double[])run.AdvanceWidths.Clone();
                                double extra = (width - line.Width) / line.Spaces.Length;
                                for (int space = 0; space < line.Spaces.Length; space++)
                                    advances[line.Spaces[space]] += extra;
                                line.JustifiedRun = new GlyphRun(run.Font, run.FontSize, run.GlyphIndices, advances);
                                line.JustifiedWidth = width;
                            }
                            run = line.JustifiedRun;
                        }
                        break;
                    default:
                        throw new ArgumentOutOfRangeException(nameof(TextAlignment));
                }
                drawingContext.DrawGlyphRun(foreground, run,
                    new Point(x, fontMetrics.Ascent + index * fontMetrics.LineHeight));
            }
        }

        private struct Character
        {
            internal int CodePoint;
            internal bool HasGlyph;
            internal uint GlyphIndex;
            internal double Advance;
        }

        private sealed class Line
        {
            internal GlyphRun Run;
            internal double Width;
            internal int[] Spaces;
            internal GlyphRun JustifiedRun;
            internal double JustifiedWidth;
        }

        private sealed class TextLayout
        {
            internal double Width;
            internal Size Size;
            internal List<Line> Lines;
        }
    }
}
