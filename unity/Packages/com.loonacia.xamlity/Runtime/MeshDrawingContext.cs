using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Rendering;
using Xamlity.Rendering;
using CornerRadius = Xamlity.Layout.CornerRadius;
using Point = Xamlity.Layout.Point;
using Rect = Xamlity.Layout.Rect;

namespace Xamlity.Unity
{
    internal sealed class MeshDrawingContext : IDrawingContext
    {
        private readonly List<Vector3> vertices = new List<Vector3>();
        private readonly List<Color32> colors = new List<Color32>();
        private readonly List<Vector2> uvs = new List<Vector2>();
        // TEXCOORD1: local position (y down), outer half size. Zero size means no shape.
        // TEXCOORD2/4: radii in top-left, top-right, bottom-right, bottom-left order.
        // TEXCOORD3: inner center offset, inner half size. Zero size means no hole.
        private readonly List<Vector4> shapeCoordinates = new List<Vector4>();
        private readonly List<Vector4> outerRadii = new List<Vector4>();
        private readonly List<Vector4> innerRectangles = new List<Vector4>();
        private readonly List<Vector4> innerRadii = new List<Vector4>();
        private readonly List<int> indices = new List<int>();
        private readonly List<Batch> batches = new List<Batch>();
        private readonly List<DrawingState> stateStack = new List<DrawingState>();
        private double currentOpacity = 1;
        private Rect? currentClip;
        private int viewportHeight;
        private double coordinateScale;

        private readonly struct DrawingState
        {
            internal DrawingState(double opacity, Rect? clip)
            {
                Opacity = opacity;
                Clip = clip;
            }

            internal double Opacity { get; }
            internal Rect? Clip { get; }
        }

        // Null texture selects the solid material; every atlas uses the shared text material.
        internal readonly struct Batch
        {
            internal Batch(Texture texture, int start, int count)
            {
                Texture = texture;
                Start = start;
                Count = count;
            }

            internal Texture Texture { get; }
            internal int Start { get; }
            internal int Count { get; }
        }

        internal IReadOnlyList<Batch> Batches => batches;

        internal void Begin(int pixelHeight, double scale)
        {
            viewportHeight = pixelHeight;
            coordinateScale = scale;
            vertices.Clear();
            colors.Clear();
            uvs.Clear();
            shapeCoordinates.Clear();
            outerRadii.Clear();
            innerRectangles.Clear();
            innerRadii.Clear();
            indices.Clear();
            batches.Clear();
            stateStack.Clear();
            currentOpacity = 1;
            currentClip = null;
        }

        public void PushClip(Rect clipRect)
        {
            stateStack.Add(new DrawingState(currentOpacity, currentClip));
            if (currentClip.HasValue)
            {
                Rect previous = currentClip.Value;
                double left = Math.Max(previous.Left, clipRect.Left);
                double top = Math.Max(previous.Top, clipRect.Top);
                clipRect = new Rect(left, top, Math.Max(0, Math.Min(previous.Right, clipRect.Right) - left),
                    Math.Max(0, Math.Min(previous.Bottom, clipRect.Bottom) - top));
            }
            currentClip = clipRect;
        }

        public void PushOpacity(double opacity)
        {
            stateStack.Add(new DrawingState(currentOpacity, currentClip));
            currentOpacity *= opacity;
        }

        public void Pop()
        {
            if (stateStack.Count == 0)
                throw new InvalidOperationException("There is no drawing state to pop.");
            DrawingState previous = stateStack[stateStack.Count - 1];
            currentOpacity = previous.Opacity;
            currentClip = previous.Clip;
            stateStack.RemoveAt(stateStack.Count - 1);
        }

        public void DrawRectangle(Brush brush, Rect rect)
        {
            if (!(brush is SolidColorBrush solid) || rect.Width <= 0 || rect.Height <= 0)
                return;

            AddQuad(solid, rect, null, Vector2.zero, Vector2.zero);
        }

        public void DrawRoundedRectangle(Brush brush, Rect rectangle, CornerRadius cornerRadius)
        {
            DrawRoundedBorder(brush, rectangle, cornerRadius, default, default);
        }

        public void DrawRoundedBorder(Brush brush, Rect outerRectangle, CornerRadius outerCornerRadius,
            Rect innerRectangle, CornerRadius innerCornerRadius)
        {
            if (!(brush is SolidColorBrush solid) || outerRectangle.Width <= 0 || outerRectangle.Height <= 0)
                return;

            var inner = Vector4.zero;
            var radii = Vector4.zero;
            if (innerRectangle.Width > 0 && innerRectangle.Height > 0)
            {
                inner = new Vector4(
                    (float)((innerRectangle.X + innerRectangle.Width / 2 - outerRectangle.X - outerRectangle.Width / 2) * coordinateScale),
                    (float)((innerRectangle.Y + innerRectangle.Height / 2 - outerRectangle.Y - outerRectangle.Height / 2) * coordinateScale),
                    (float)(innerRectangle.Width / 2 * coordinateScale),
                    (float)(innerRectangle.Height / 2 * coordinateScale));
                radii = ScaleRadii(innerCornerRadius);
            }
            AddQuad(solid, outerRectangle, null, Vector2.zero, Vector2.zero,
                true, ScaleRadii(outerCornerRadius), inner, radii);
        }

        private Vector4 ScaleRadii(CornerRadius radius) => new Vector4(
            (float)(radius.TopLeft * coordinateScale), (float)(radius.TopRight * coordinateScale),
            (float)(radius.BottomRight * coordinateScale), (float)(radius.BottomLeft * coordinateScale));

        private void AddQuad(SolidColorBrush solid, Rect rect, Texture texture, Vector2 uvMin, Vector2 uvMax,
            bool hasShape = false, Vector4 outerRadius = default, Vector4 innerRectangle = default,
            Vector4 innerRadius = default)
        {
            Rect outerRectangle = rect;
            if (currentClip.HasValue)
            {
                Rect clip = currentClip.Value;
                double leftEdge = Math.Max(rect.Left, clip.Left);
                double topEdge = Math.Max(rect.Top, clip.Top);
                double rightEdge = Math.Min(rect.Right, clip.Right);
                double bottomEdge = Math.Min(rect.Bottom, clip.Bottom);
                if (rightEdge <= leftEdge || bottomEdge <= topEdge)
                    return;

                Vector2 uvSize = uvMax - uvMin;
                var clippedUvMin = new Vector2(
                    uvMin.x + uvSize.x * (float)((leftEdge - rect.Left) / rect.Width),
                    uvMin.y + uvSize.y * (float)((rect.Bottom - bottomEdge) / rect.Height));
                uvMax = new Vector2(
                    uvMax.x - uvSize.x * (float)((rect.Right - rightEdge) / rect.Width),
                    uvMax.y - uvSize.y * (float)((topEdge - rect.Top) / rect.Height));
                uvMin = clippedUvMin;
                rect = new Rect(leftEdge, topEdge, rightEdge - leftEdge, bottomEdge - topEdge);
            }

            if (batches.Count != 0 && batches[batches.Count - 1].Texture == texture)
            {
                var previous = batches[batches.Count - 1];
                batches[batches.Count - 1] = new Batch(texture, previous.Start, previous.Count + 6);
            }
            else
                batches.Add(new Batch(texture, indices.Count, 6));

            int first = vertices.Count;
            // Scale the clipped logical edges before converting to bottom-left pixel coordinates.
            float left = (float)(rect.X * coordinateScale);
            float right = (float)((rect.X + rect.Width) * coordinateScale);
            float top = (float)(viewportHeight - rect.Y * coordinateScale);
            float bottom = (float)(viewportHeight - (rect.Y + rect.Height) * coordinateScale);
            vertices.Add(new Vector3(left, top, 0));
            vertices.Add(new Vector3(right, top, 0));
            vertices.Add(new Vector3(right, bottom, 0));
            vertices.Add(new Vector3(left, bottom, 0));
            uvs.Add(new Vector2(uvMin.x, uvMax.y));
            uvs.Add(uvMax);
            uvs.Add(new Vector2(uvMax.x, uvMin.y));
            uvs.Add(uvMin);
            if (hasShape)
            {
                double centerX = outerRectangle.X + outerRectangle.Width / 2;
                double centerY = outerRectangle.Y + outerRectangle.Height / 2;
                float localLeft = (float)((rect.Left - centerX) * coordinateScale);
                float localRight = (float)((rect.Right - centerX) * coordinateScale);
                float localTop = (float)((rect.Top - centerY) * coordinateScale);
                float localBottom = (float)((rect.Bottom - centerY) * coordinateScale);
                float halfWidth = (float)(outerRectangle.Width / 2 * coordinateScale);
                float halfHeight = (float)(outerRectangle.Height / 2 * coordinateScale);
                shapeCoordinates.Add(new Vector4(localLeft, localTop, halfWidth, halfHeight));
                shapeCoordinates.Add(new Vector4(localRight, localTop, halfWidth, halfHeight));
                shapeCoordinates.Add(new Vector4(localRight, localBottom, halfWidth, halfHeight));
                shapeCoordinates.Add(new Vector4(localLeft, localBottom, halfWidth, halfHeight));
            }
            else
            {
                for (int i = 0; i < 4; i++)
                    shapeCoordinates.Add(Vector4.zero);
            }
            double opacity = currentOpacity;
            opacity = double.IsNaN(opacity) ? 0 : Math.Max(0, Math.Min(1, opacity));
            var color = new Color32(solid.Color.R, solid.Color.G, solid.Color.B,
                (byte)Math.Round(solid.Color.A * opacity));
            for (int i = 0; i < 4; i++)
            {
                colors.Add(color);
                outerRadii.Add(outerRadius);
                innerRectangles.Add(innerRectangle);
                innerRadii.Add(innerRadius);
            }
            indices.Add(first);
            indices.Add(first + 1);
            indices.Add(first + 2);
            indices.Add(first);
            indices.Add(first + 2);
            indices.Add(first + 3);
        }

        public void DrawGlyphRun(Brush foreground, GlyphRun glyphRun, Point baselineOrigin)
        {
            if (!(foreground is SolidColorBrush solid))
                return;

            var font = (TMP_FontAsset)glyphRun.Font.Source;
            var face = font.faceInfo;
            double advance = 0;
            int padding = font.atlasPadding;
            for (int i = 0; i < glyphRun.GlyphIndices.Length; i++)
            {
                var glyph = font.glyphLookupTable[glyphRun.GlyphIndices[i]];
                var metrics = glyph.metrics;
                var atlasRect = glyph.glyphRect;
                if (atlasRect.width > 0 && atlasRect.height > 0)
                {
                    double scale = glyphRun.FontSize / face.pointSize * face.scale * glyph.scale;
                    var rect = new Rect(
                        baselineOrigin.X + advance + (metrics.horizontalBearingX - padding) * scale,
                        baselineOrigin.Y - (metrics.horizontalBearingY + padding) * scale,
                        (metrics.width + 2 * padding) * scale,
                        (metrics.height + 2 * padding) * scale);
                    // Measurement may have populated or expanded the atlas earlier in this frame.
                    var atlas = font.atlasTextures[glyph.atlasIndex];
                    var uvMin = new Vector2((atlasRect.x - padding) / (float)atlas.width,
                        (atlasRect.y - padding) / (float)atlas.height);
                    var uvMax = new Vector2((atlasRect.x + atlasRect.width + padding) / (float)atlas.width,
                        (atlasRect.y + atlasRect.height + padding) / (float)atlas.height);
                    AddQuad(solid, rect, atlas, uvMin, uvMax);
                }
                advance += glyphRun.AdvanceWidths[i];
            }
        }

        internal void Upload(Mesh mesh)
        {
            mesh.Clear();
            mesh.indexFormat = vertices.Count > ushort.MaxValue ? IndexFormat.UInt32 : IndexFormat.UInt16;
            mesh.SetVertices(vertices);
            mesh.SetColors(colors);
            mesh.SetUVs(0, uvs);
            mesh.SetUVs(1, shapeCoordinates);
            mesh.SetUVs(2, outerRadii);
            mesh.SetUVs(3, innerRectangles);
            mesh.SetUVs(4, innerRadii);
            mesh.subMeshCount = batches.Count;
            for (int i = 0; i < batches.Count; i++)
                mesh.SetTriangles(indices, batches[i].Start, batches[i].Count, i);
        }
    }
}
