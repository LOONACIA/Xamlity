using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Rendering;
using Xamlity.Layout;
using Xamlity.Rendering;
using Xamlity.Styling;
using Xamlity.Tree;

namespace Xamlity.Unity
{
    public sealed class XamlityRoot : MonoBehaviour
    {
        [SerializeField] private XamlAsset xaml;
        [SerializeField] private Vector2Int referenceResolution = new Vector2Int(1920, 1080);

        private static readonly List<XamlityRoot> activeRoots = new List<XamlityRoot>();
        private static bool themeWarningLogged;
        private readonly MeshDrawingContext drawingContext = new MeshDrawingContext();
        private int viewportWidth;
        private int viewportHeight;
        private object dataContext;

        public object DataContext
        {
            get => dataContext;
            set
            {
                dataContext = value;
                if (Tree?.RootElement is FrameworkElement element)
                    element.DataContext = value;
            }
        }

        internal static IReadOnlyList<XamlityRoot> ActiveRoots => activeRoots;
        internal UIRoot Tree { get; private set; }
        internal Mesh Mesh { get; private set; }
        internal IReadOnlyList<MeshDrawingContext.Batch> Batches => drawingContext.Batches;
        internal int MeshRebuildCount { get; private set; }
        internal double Scale { get; private set; } = 1;
        internal Vector2Int ReferenceResolution
        {
            get => referenceResolution;
            set => referenceResolution = value;
        }

        private void Awake()
        {
            var settings = XamlitySettings.Load();
            Build(xaml, settings != null ? settings.DefaultFont : null,
                settings != null ? settings.ApplicationResources : null);
        }
        private void OnEnable() => Register();
        private void OnDisable() => Unregister();
        private void LateUpdate() => Refresh(Screen.width, Screen.height);

        private void OnDestroy() => Release();

        internal void Release()
        {
            Unregister();
            if (Tree?.RootElement is FrameworkElement element)
                element.DataContext = null;
            CoreUtils.Destroy(Mesh);
        }

        internal void Build(XamlAsset asset, TMP_FontAsset defaultFont = null, XamlAsset applicationResources = null)
        {
            if (asset == null)
                return;
            ResourceDictionary theme = null;
            var themeAsset = Resources.Load<XamlAsset>("Xamlity/Generic");
            if (themeAsset != null && themeAsset.HasIr)
                theme = themeAsset.BuildResourceDictionary(null);
            else if (!themeWarningLogged)
            {
                Debug.LogWarning("Xamlity default theme is missing or has import errors.");
                themeWarningLogged = true;
            }
            var application = applicationResources != null ? applicationResources.BuildResourceDictionary(theme) : null;
            var tree = new UIRoot(asset.Build(application, theme), new TmpTextMeasurer(), new FontHandle(defaultFont),
                application, theme);
            if (Tree?.RootElement is FrameworkElement oldElement)
                oldElement.DataContext = null;
            Tree = tree;
            if (Tree.RootElement is FrameworkElement element)
                element.DataContext = dataContext;
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

            double scale = referenceResolution.x <= 0 || referenceResolution.y <= 0
                || pixelWidth <= 0 || pixelHeight <= 0
                ? 1
                : System.Math.Min((double)pixelWidth / referenceResolution.x,
                    (double)pixelHeight / referenceResolution.y);
            if (viewportWidth != pixelWidth || viewportHeight != pixelHeight || Scale != scale)
            {
                viewportWidth = pixelWidth;
                viewportHeight = pixelHeight;
                Scale = scale;
                // Even a fixed-size element needs new vertex coordinates on resize or scale changes.
                Tree.RootElement.InvalidateVisual();
            }
            Tree.UpdateLayout(new Size(pixelWidth / scale, pixelHeight / scale));
            if (!Tree.IsRenderDirty)
                return;

            drawingContext.Begin(pixelHeight, scale);
            Tree.Render(drawingContext);
            drawingContext.Upload(Mesh);
            MeshRebuildCount++;
        }
    }
}
