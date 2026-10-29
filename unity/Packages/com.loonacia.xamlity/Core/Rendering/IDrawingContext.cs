using Xamlity.Layout;

namespace Xamlity.Rendering
{
    public interface IDrawingContext
    {
        void PushOpacity(double opacity);
        void Pop();
        void DrawRectangle(Brush brush, Rect rect);
        void DrawGlyphRun(Brush foreground, GlyphRun glyphRun, Point baselineOrigin);
    }
}
