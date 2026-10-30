using Xamlity.Properties;

namespace Xamlity.Styling
{
    internal readonly struct TemplateBinding
    {
        internal TemplateBinding(DependencyObject target, DependencyProperty targetProperty, DependencyProperty sourceProperty)
        {
            Target = target;
            TargetProperty = targetProperty;
            SourceProperty = sourceProperty;
        }

        internal DependencyObject Target { get; }
        internal DependencyProperty TargetProperty { get; }
        internal DependencyProperty SourceProperty { get; }

        internal void Update(object value) => Target.SetLayerValue(TargetProperty, ValueLayer.ParentTemplate, value);
    }
}
