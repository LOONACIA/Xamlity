using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Rendering;
using Xamlity.Rendering;
using Point = Xamlity.Layout.Point;
using Rect = Xamlity.Layout.Rect;

namespace Xamlity.Unity
{
    internal sealed class MeshDrawingContext : IDrawingContext
    {
        private readonly List<Vector3> vertices = new List<Vector3>();
        private readonly List<Color32> colors = new List<Color32>();
        private readonly List<Vector2> uvs = new List<Vector2>();
        private readonly List<int> indices = new List<int>();
        private readonly List<Batch> batches = new List<Batch>();
        private int viewportHeight;

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

        internal void Begin(int pixelHeight)
        {
            viewportHeight = pixelHeight;
            vertices.Clear();
            colors.Clear();
            uvs.Clear();
            indices.Clear();
            batches.Clear();
        }

        public void DrawRectangle(Brush brush, Rect rect)
        {
            if (!(brush is SolidColorBrush solid) || rect.Width <= 0 || rect.Height <= 0)
                return;

            AddQuad(solid, rect, null, Vector2.zero, Vector2.zero);
        }

        private void AddQuad(SolidColorBrush solid, Rect rect, Texture texture, Vector2 uvMin, Vector2 uvMax)
        {
            if (batches.Count != 0 && batches[batches.Count - 1].Texture == texture)
            {
                var previous = batches[batches.Count - 1];
                batches[batches.Count - 1] = new Batch(texture, previous.Start, previous.Count + 6);
            }
            else
                batches.Add(new Batch(texture, indices.Count, 6));

            int first = vertices.Count;
            float left = (float)rect.X;
            float right = (float)(rect.X + rect.Width);
            float top = (float)(viewportHeight - rect.Y);
            float bottom = (float)(viewportHeight - rect.Y - rect.Height);
            vertices.Add(new Vector3(left, top, 0));
            vertices.Add(new Vector3(right, top, 0));
            vertices.Add(new Vector3(right, bottom, 0));
            vertices.Add(new Vector3(left, bottom, 0));
            uvs.Add(new Vector2(uvMin.x, uvMax.y));
            uvs.Add(uvMax);
            uvs.Add(new Vector2(uvMax.x, uvMin.y));
            uvs.Add(uvMin);
            var color = new Color32(solid.Color.R, solid.Color.G, solid.Color.B, solid.Color.A);
            for (int i = 0; i < 4; i++)
                colors.Add(color);
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
            mesh.subMeshCount = batches.Count;
            for (int i = 0; i < batches.Count; i++)
                mesh.SetTriangles(indices, batches[i].Start, batches[i].Count, i);
        }
    }
}
