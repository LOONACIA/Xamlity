using System;
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

        private GlyphRun glyphRun;
        private string measuredText;
        private ITextMeasurer measuredWith;
        private FontMetrics fontMetrics;
        private Size textSize;

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

        protected override Size MeasureOverride(Size availableSize)
        {
            UIRoot root = GetRoot();
            ITextMeasurer measurer = root?.TextMeasurer;
            FontHandle font = root == null ? default : root.DefaultFont;
            if (measurer == null || !font.IsValid)
            {
                glyphRun = null;
                return new Size(0, 0);
            }

            string text = Text ?? string.Empty;
            double fontSize = FontSize;
            if (glyphRun != null && measuredText == text && glyphRun.FontSize == fontSize &&
                glyphRun.Font == font && ReferenceEquals(measuredWith, measurer))
                return textSize;

            FontMetrics metrics = measurer.GetFontMetrics(font, fontSize);
            var indices = new uint[text.Length];
            var advances = new double[text.Length];
            int count = 0;
            double width = 0;
            for (int index = 0; index < text.Length; index++)
            {
                int codePoint = text[index];
                if (char.IsHighSurrogate(text[index]) && index + 1 < text.Length &&
                    char.IsLowSurrogate(text[index + 1]))
                    codePoint = char.ConvertToUtf32(text[index], text[++index]);
                if (!measurer.TryGetGlyph(font, codePoint, fontSize, out GlyphMetrics glyph))
                    continue;
                indices[count] = glyph.GlyphIndex;
                advances[count] = glyph.AdvanceWidth;
                width += glyph.AdvanceWidth;
                count++;
            }
            Array.Resize(ref indices, count);
            Array.Resize(ref advances, count);
            glyphRun = new GlyphRun(font, fontSize, indices, advances);
            measuredText = text;
            measuredWith = measurer;
            fontMetrics = metrics;
            textSize = new Size(width, metrics.LineHeight);
            return textSize;
        }

        protected override void OnRender(IDrawingContext drawingContext)
        {
            Brush foreground = Foreground;
            if (foreground != null && glyphRun != null)
                drawingContext.DrawGlyphRun(foreground, glyphRun, new Point(0, fontMetrics.Ascent));
        }
    }
}
