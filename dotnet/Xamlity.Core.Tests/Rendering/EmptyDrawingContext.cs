using Xamlity.Layout;
using Xamlity.Rendering;

namespace Xamlity.Core.Tests.Rendering
{
    internal sealed class EmptyDrawingContext : IDrawingContext
    {
        public void PushOpacity(double opacity)
        {
        }

        public void Pop()
        {
        }

        public void DrawRectangle(Brush brush, Rect rect)
        {
        }

        public void DrawGlyphRun(Brush foreground, GlyphRun glyphRun, Point baselineOrigin)
        {
        }
    }
}
