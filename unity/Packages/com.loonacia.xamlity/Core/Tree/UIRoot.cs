using System;
using Xamlity.Layout;
using Xamlity.Rendering;

namespace Xamlity.Tree
{
    public sealed class UIRoot
    {
        private readonly OffsetDrawingContext offsetDrawingContext = new OffsetDrawingContext();

        public UIRoot(UIElement rootElement)
            : this(rootElement, null, default)
        {
        }

        public UIRoot(UIElement rootElement, ITextMeasurer textMeasurer, FontHandle defaultFont)
        {
            RootElement = rootElement ?? throw new ArgumentNullException(nameof(rootElement));
            TextMeasurer = textMeasurer;
            DefaultFont = defaultFont;
            RootElement.Root = this;
            RootElement.InvalidateSubtreeMeasure();
        }

        public UIElement RootElement { get; }
        public ITextMeasurer TextMeasurer { get; }
        public FontHandle DefaultFont { get; }
        public bool IsRenderDirty { get; private set; } = true;

        public void UpdateLayout(Size viewportSize)
        {
            RootElement.Measure(viewportSize);
            RootElement.Arrange(new Rect(new Point(0, 0), viewportSize));
        }

        internal void InvalidateVisual() => IsRenderDirty = true;

        public void Render(IDrawingContext drawingContext)
        {
            if (drawingContext == null)
                throw new ArgumentNullException(nameof(drawingContext));

            offsetDrawingContext.Target = drawingContext;
            try
            {
                RootElement.Render(offsetDrawingContext, new Point(0, 0));
                IsRenderDirty = false;
            }
            finally
            {
                offsetDrawingContext.Target = null;
            }
        }

        internal sealed class OffsetDrawingContext : IDrawingContext
        {
            internal IDrawingContext Target { get; set; }
            internal Point Offset { get; set; }

            public void PushOpacity(double opacity) => Target.PushOpacity(opacity);
            public void Pop() => Target.Pop();

            public void DrawRectangle(Brush brush, Rect rect)
            {
                Target.DrawRectangle(brush,
                    new Rect(rect.X + Offset.X, rect.Y + Offset.Y, rect.Width, rect.Height));
            }

            public void DrawGlyphRun(Brush foreground, GlyphRun glyphRun, Point baselineOrigin)
            {
                Target.DrawGlyphRun(foreground, glyphRun,
                    new Point(baselineOrigin.X + Offset.X, baselineOrigin.Y + Offset.Y));
            }
        }
    }
}
