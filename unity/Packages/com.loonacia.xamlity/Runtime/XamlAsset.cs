using System;
using System.Collections.Generic;
using UnityEngine;
using Xamlity.Markup;
using Xamlity.Rendering;
using Xamlity.Styling;
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
        internal bool HasResourceDictionaryRoot => HasIr && typeof(ResourceDictionary).IsAssignableFrom(
            Type.GetType(XamlIrSerializer.Deserialize(ir).Root.TypeName, throwOnError: true));

        public UIElement Build() => Build(null, null);

        internal UIElement Build(ResourceDictionary applicationResources, ResourceDictionary themeResources)
        {
            return BuildObject(applicationResources, themeResources) as UIElement
                ?? throw new InvalidOperationException($"XAML asset '{name}' must have a UIElement root.");
        }

        internal ResourceDictionary BuildResourceDictionary(ResourceDictionary themeResources)
        {
            return BuildObject(null, themeResources) as ResourceDictionary
                ?? throw new InvalidOperationException($"XAML asset '{name}' must have a ResourceDictionary root.");
        }

        private object BuildObject(ResourceDictionary applicationResources, ResourceDictionary themeResources,
            HashSet<XamlAsset> building = null)
        {
            if (!HasIr)
                throw new InvalidOperationException($"XAML asset '{name}' has import errors.");
            building = building ?? new HashSet<XamlAsset>();
            building.Add(this);
            try
            {
                return XamlBuilder.Build(XamlIrSerializer.Deserialize(ir),
                    new AssetResolver(this, applicationResources, themeResources, building),
                    applicationResources, themeResources);
            }
            finally
            {
                building.Remove(this);
            }
        }

        private sealed class AssetResolver : IXamlAssetResolver
        {
            private readonly XamlAsset owner;
            private readonly ResourceDictionary applicationResources;
            private readonly ResourceDictionary themeResources;
            private readonly HashSet<XamlAsset> building;

            internal AssetResolver(XamlAsset owner, ResourceDictionary applicationResources,
                ResourceDictionary themeResources, HashSet<XamlAsset> building)
            {
                this.owner = owner;
                this.applicationResources = applicationResources;
                this.themeResources = themeResources;
                this.building = building;
            }

            public ResourceDictionary ResolveResourceDictionary(string value)
            {
                int index = owner.assetKeys == null ? -1 : Array.IndexOf(owner.assetKeys, value);
                if (index < 0 || owner.assets == null || index >= owner.assets.Length ||
                    !(owner.assets[index] is XamlAsset reference))
                    return null;
                // Refresh can unload a ScriptableObject while its serialized reference remains valid.
                var asset = new LazyLoadReference<XamlAsset>(reference.GetEntityId()).asset;
                if (asset == null)
                    return null;
                if (building.Contains(asset))
                    throw new InvalidOperationException($"Resource dictionary '{value}' is part of a reference cycle.");
                return asset.BuildObject(applicationResources, themeResources, building) as ResourceDictionary;
            }

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
