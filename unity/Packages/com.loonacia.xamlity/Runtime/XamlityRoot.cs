using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using Xamlity.Layout;
using Xamlity.Tree;

namespace Xamlity.Unity
{
    public sealed class XamlityRoot : MonoBehaviour
    {
        [SerializeField] private XamlAsset xaml;

        private static readonly List<XamlityRoot> activeRoots = new List<XamlityRoot>();
        private readonly MeshDrawingContext drawingContext = new MeshDrawingContext();
        private int viewportWidth;
        private int viewportHeight;

        internal static IReadOnlyList<XamlityRoot> ActiveRoots => activeRoots;
        internal UIRoot Tree { get; private set; }
        internal Mesh Mesh { get; private set; }
        internal int MeshRebuildCount { get; private set; }

        private void Awake() => Build(xaml);
        private void OnEnable() => Register();
        private void OnDisable() => Unregister();
        private void LateUpdate() => Refresh(Screen.width, Screen.height);

        private void OnDestroy()
        {
            Unregister();
            CoreUtils.Destroy(Mesh);
        }

        internal void Build(XamlAsset asset)
        {
            if (asset == null)
                return;
            Tree = new UIRoot(asset.Build(), new TmpTextMeasurer(), default);
            if (Mesh == null)
            {
                Mesh = new Mesh { name = "Xamlity Root", hideFlags = HideFlags.HideAndDontSave };
                Mesh.MarkDynamic();
            }
        }

        internal void Register()
        {
            if (!activeRoots.Contains(this))
                activeRoots.Add(this);
        }

        internal void Unregister() => activeRoots.Remove(this);

        internal void Refresh(int pixelWidth, int pixelHeight)
        {
            if (Tree == null)
                return;

            if (viewportWidth != pixelWidth || viewportHeight != pixelHeight)
            {
                viewportWidth = pixelWidth;
                viewportHeight = pixelHeight;
                // Even a fixed-size element needs new bottom-left vertex coordinates on resize.
                Tree.RootElement.InvalidateVisual();
            }
            Tree.UpdateLayout(new Size(pixelWidth, pixelHeight));
            if (!Tree.IsRenderDirty)
                return;

            drawingContext.Begin(pixelHeight);
            Tree.Render(drawingContext);
            drawingContext.Upload(Mesh);
            MeshRebuildCount++;
        }
    }
}
