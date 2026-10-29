using System;
using Xamlity.Layout;
using Xamlity.Rendering;

namespace Xamlity.Tree
{
    public sealed class UIRoot
    {
        private readonly OffsetDrawingContext offsetDrawingContext = new OffsetDrawingContext();

        public UIRoot(UIElement rootElement)
        {
            RootElement = rootElement ?? throw new ArgumentNullException(nameof(rootElement));
            RootElement.Root = this;
        }

        public UIElement RootElement { get; }
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

            public void DrawRectangle(Brush brush, Rect rect)
            {
                Target.DrawRectangle(brush,
                    new Rect(rect.X + Offset.X, rect.Y + Offset.Y, rect.Width, rect.Height));
            }
        }
    }
}
