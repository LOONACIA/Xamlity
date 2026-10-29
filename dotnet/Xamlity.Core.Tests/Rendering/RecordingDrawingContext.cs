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
        public List<(ImageSource Source, Rect Rect)> Images { get; } = new List<(ImageSource, Rect)>();
        public List<(Brush Brush, Rect Rect, CornerRadius Radius)> RoundedRectangles { get; } =
            new List<(Brush, Rect, CornerRadius)>();
        public List<(Brush Brush, Rect OuterRect, CornerRadius OuterRadius, Rect InnerRect, CornerRadius InnerRadius)>
            RoundedBorders { get; } = new List<(Brush, Rect, CornerRadius, Rect, CornerRadius)>();
        public List<(Brush Foreground, GlyphRun Run, Point BaselineOrigin)> GlyphRuns { get; } =
            new List<(Brush, GlyphRun, Point)>();

        public void PushClip(Rect clipRect) =>
            Calls.Add((nameof(PushClip), default, null, clipRect, null, default));
        public void PushOpacity(double opacity) =>
            Calls.Add((nameof(PushOpacity), opacity, null, default, null, default));
        public void Pop() => Calls.Add((nameof(Pop), default, null, default, null, default));

        public void DrawImage(ImageSource imageSource, Rect rectangle)
        {
            Calls.Add((nameof(DrawImage), default, null, rectangle, null, default));
            Images.Add((imageSource, rectangle));
        }

        public void DrawRectangle(Brush brush, Rect rect)
        {
            Calls.Add((nameof(DrawRectangle), default, brush, rect, null, default));
            Rectangles.Add((brush, rect));
        }

        public void DrawRoundedRectangle(Brush brush, Rect rectangle, CornerRadius cornerRadius)
        {
            Calls.Add((nameof(DrawRoundedRectangle), default, brush, rectangle, null, default));
            RoundedRectangles.Add((brush, rectangle, cornerRadius));
        }

        public void DrawRoundedBorder(Brush brush, Rect outerRectangle, CornerRadius outerCornerRadius,
            Rect innerRectangle, CornerRadius innerCornerRadius)
        {
            Calls.Add((nameof(DrawRoundedBorder), default, brush, outerRectangle, null, default));
            RoundedBorders.Add((brush, outerRectangle, outerCornerRadius, innerRectangle, innerCornerRadius));
        }

        public void DrawGlyphRun(Brush foreground, GlyphRun glyphRun, Point baselineOrigin)
        {
            Calls.Add((nameof(DrawGlyphRun), default, foreground, default, glyphRun, baselineOrigin));
            GlyphRuns.Add((foreground, glyphRun, baselineOrigin));
        }
    }
}
