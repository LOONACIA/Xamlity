using Xamlity.Layout;

namespace Xamlity.Rendering
{
    public interface IDrawingContext
    {
        void PushClip(Rect clipRect);
        void PushOpacity(double opacity);
        void Pop();
        void DrawRectangle(Brush brush, Rect rect);
        void DrawRoundedRectangle(Brush brush, Rect rectangle, CornerRadius cornerRadius);
        void DrawRoundedBorder(Brush brush, Rect outerRectangle, CornerRadius outerCornerRadius,
            Rect innerRectangle, CornerRadius innerCornerRadius);
        void DrawGlyphRun(Brush foreground, GlyphRun glyphRun, Point baselineOrigin);
    }
}
