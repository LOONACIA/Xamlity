using System.Collections.Generic;
using Xamlity.Layout;
using Xamlity.Rendering;

namespace Xamlity.Core.Tests.Rendering
{
    internal sealed class RecordingDrawingContext : IDrawingContext
    {
        public List<(string Method, double Opacity, Brush Brush, Rect Rect, GlyphRun Run, Point BaselineOrigin)>
            Calls { get; } = new List<(string, double, Brush, Rect, GlyphRun, Point)>(16);
        public List<(Brush Brush, Rect Rect)> Rectangles { get; } = new List<(Brush, Rect)>();
        public List<(Brush Foreground, GlyphRun Run, Point BaselineOrigin)> GlyphRuns { get; } =
            new List<(Brush, GlyphRun, Point)>();

        public void PushOpacity(double opacity) =>
            Calls.Add((nameof(PushOpacity), opacity, null, default, null, default));
        public void Pop() => Calls.Add((nameof(Pop), default, null, default, null, default));

        public void DrawRectangle(Brush brush, Rect rect)
        {
            Calls.Add((nameof(DrawRectangle), default, brush, rect, null, default));
            Rectangles.Add((brush, rect));
        }

        public void DrawGlyphRun(Brush foreground, GlyphRun glyphRun, Point baselineOrigin)
        {
            Calls.Add((nameof(DrawGlyphRun), default, foreground, default, glyphRun, baselineOrigin));
            GlyphRuns.Add((foreground, glyphRun, baselineOrigin));
        }
    }
}
