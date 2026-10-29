using Xamlity.Properties;

namespace Xamlity.Layout
{
    public abstract class DefinitionBase : DependencyObject
    {
        internal Grid Owner { get; set; }

        protected override void OnPropertyChanged(DependencyPropertyChangedEventArgs e)
        {
            base.OnPropertyChanged(e);
            if (e.Property.DefaultMetadata is FrameworkPropertyMetadata metadata && metadata.AffectsMeasure)
                Owner?.InvalidateMeasure();
        }
    }
}
