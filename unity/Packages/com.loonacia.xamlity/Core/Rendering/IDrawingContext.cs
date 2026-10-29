using Xamlity.Layout;

namespace Xamlity.Rendering
{
    public interface IDrawingContext
    {
        void DrawRectangle(Brush brush, Rect rect);
        void DrawGlyphRun(Brush foreground, GlyphRun glyphRun, Point baselineOrigin);
    }
}
