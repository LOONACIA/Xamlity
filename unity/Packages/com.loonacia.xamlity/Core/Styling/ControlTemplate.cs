using System;
using System.Collections.Generic;
using Xamlity.Markup;
using Xamlity.Properties;
using Xamlity.Tree;

namespace Xamlity.Styling
{
    public sealed class ControlTemplate
    {
        private Type targetType;
        private XamlObjectNode content;
        private object[] resourceScope;
        private IXamlAssetResolver assetResolver;

        public Type TargetType
        {
            get => targetType;
            set
            {
                if (IsSealed)
                    throw new InvalidOperationException("Cannot modify a sealed ControlTemplate.");
                targetType = value;
            }
        }

        public bool IsSealed { get; private set; }

        public void Seal() => IsSealed = true;

        internal void SetContent(XamlObjectNode node, object[] scope, IXamlAssetResolver resolver)
        {
            content = node;
            resourceScope = scope;
            assetResolver = resolver;
        }

        internal FrameworkElement BuildContent(DependencyObject parent, Dictionary<string, DependencyObject> names,
            List<TemplateBinding> bindings) =>
            content == null ? null : XamlBuilder.BuildTemplate(content, assetResolver, resourceScope, parent, names,
                TargetType ?? typeof(Controls.Control), bindings);
    }
}
