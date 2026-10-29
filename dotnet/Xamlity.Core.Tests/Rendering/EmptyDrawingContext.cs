using Xamlity.Layout;
using Xamlity.Rendering;

namespace Xamlity.Core.Tests.Rendering
{
    internal sealed class EmptyDrawingContext : IDrawingContext
    {
        public void PushClip(Rect clipRect)
        {
        }

        public void PushOpacity(double opacity)
        {
        }

        public void Pop()
        {
        }

        public void DrawRectangle(Brush brush, Rect rect)
        {
        }

        public void DrawRoundedRectangle(Brush brush, Rect rectangle, CornerRadius cornerRadius)
        {
        }

        public void DrawRoundedBorder(Brush brush, Rect outerRectangle, CornerRadius outerCornerRadius,
            Rect innerRectangle, CornerRadius innerCornerRadius)
        {
        }

        public void DrawGlyphRun(Brush foreground, GlyphRun glyphRun, Point baselineOrigin)
        {
        }
    }
}
