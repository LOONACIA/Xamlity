using System;
using UnityEngine;
using Xamlity.Markup;
using Xamlity.Tree;

namespace Xamlity.Unity
{
    public sealed class XamlAsset : ScriptableObject
    {
        [SerializeField] private byte[] ir;

        internal void Initialize(byte[] ir)
        {
            this.ir = ir;
        }

        internal bool HasIr => ir != null && ir.Length != 0;

        public UIElement Build()
        {
            if (!HasIr)
                throw new InvalidOperationException($"XAML asset '{name}' has import errors.");
            return (UIElement)XamlBuilder.Build(XamlIrSerializer.Deserialize(ir));
        }
    }
}
