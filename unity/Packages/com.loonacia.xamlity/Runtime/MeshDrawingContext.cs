using System.Collections.Generic;
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
        private readonly List<int> indices = new List<int>();
        private int viewportHeight;

        internal void Begin(int pixelHeight)
        {
            viewportHeight = pixelHeight;
            vertices.Clear();
            colors.Clear();
            indices.Clear();
        }

        public void DrawRectangle(Brush brush, Rect rect)
        {
            if (!(brush is SolidColorBrush solid) || rect.Width <= 0 || rect.Height <= 0)
                return;

            int first = vertices.Count;
            float left = (float)rect.X;
            float right = (float)(rect.X + rect.Width);
            float top = (float)(viewportHeight - rect.Y);
            float bottom = (float)(viewportHeight - rect.Y - rect.Height);
            vertices.Add(new Vector3(left, top, 0));
            vertices.Add(new Vector3(right, top, 0));
            vertices.Add(new Vector3(right, bottom, 0));
            vertices.Add(new Vector3(left, bottom, 0));
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
            // Glyph geometry and texture batches are added with the SDF renderer.
        }

        internal void Upload(Mesh mesh)
        {
            mesh.Clear();
            mesh.indexFormat = vertices.Count > ushort.MaxValue ? IndexFormat.UInt32 : IndexFormat.UInt16;
            mesh.SetVertices(vertices);
            mesh.SetColors(colors);
            // All current commands share the solid material and have no texture.
            mesh.subMeshCount = indices.Count == 0 ? 0 : 1;
            if (indices.Count != 0)
                mesh.SetTriangles(indices, 0);
        }
    }
}
