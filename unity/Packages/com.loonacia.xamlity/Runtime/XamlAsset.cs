using System;
using UnityEngine;
using Xamlity.Markup;
using Xamlity.Rendering;
using Xamlity.Tree;
using Object = UnityEngine.Object;

namespace Xamlity.Unity
{
    public sealed class XamlAsset : ScriptableObject
    {
        [SerializeField] private byte[] ir;
        [SerializeField] private string[] assetKeys;
        [SerializeField] private Object[] assets;

        internal void Initialize(byte[] ir, string[] assetKeys, Object[] assets)
        {
            this.ir = ir;
            this.assetKeys = assetKeys;
            this.assets = assets;
        }

        internal bool HasIr => ir != null && ir.Length != 0;

        public UIElement Build()
        {
            if (!HasIr)
                throw new InvalidOperationException($"XAML asset '{name}' has import errors.");
            return (UIElement)XamlBuilder.Build(XamlIrSerializer.Deserialize(ir), new AssetResolver(this));
        }

        private sealed class AssetResolver : IXamlAssetResolver
        {
            private readonly XamlAsset owner;

            internal AssetResolver(XamlAsset owner) => this.owner = owner;

            public ImageSource ResolveImage(string value)
            {
                int index = owner.assetKeys == null ? -1 : Array.IndexOf(owner.assetKeys, value);
                if (index >= 0 && owner.assets != null && index < owner.assets.Length)
                {
                    Object asset = owner.assets[index];
                    if (asset is Sprite sprite && sprite != null)
                        return new UnityImageSource(sprite);
                    if (asset is Texture2D texture && texture != null)
                        return new UnityImageSource(texture);
                }
                Debug.LogWarning($"Image asset '{value}' is missing from XAML asset '{owner.name}'.", owner);
                return null;
            }
        }
    }
}
