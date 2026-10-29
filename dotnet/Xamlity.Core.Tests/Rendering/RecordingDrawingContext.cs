using System.Collections.Generic;
using Xamlity.Layout;
using Xamlity.Rendering;

namespace Xamlity.Core.Tests.Rendering
{
    internal sealed class RecordingDrawingContext : IDrawingContext
    {
        public List<(Brush Brush, Rect Rect)> Rectangles { get; } = new List<(Brush, Rect)>();
        public List<(Brush Foreground, GlyphRun Run, Point BaselineOrigin)> GlyphRuns { get; } =
            new List<(Brush, GlyphRun, Point)>();

        public void DrawRectangle(Brush brush, Rect rect) => Rectangles.Add((brush, rect));
        public void DrawGlyphRun(Brush foreground, GlyphRun glyphRun, Point baselineOrigin) =>
            GlyphRuns.Add((foreground, glyphRun, baselineOrigin));
    }
}
