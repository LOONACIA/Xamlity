using Xamlity.Properties;

namespace Xamlity.Layout
{
    public class ColumnDefinition : DefinitionBase
    {
        public static readonly DependencyProperty WidthProperty = DependencyProperty.Register(
            nameof(Width), typeof(GridLength), typeof(ColumnDefinition),
            new FrameworkPropertyMetadata(new GridLength(1, GridUnitType.Star), FrameworkPropertyMetadataOptions.AffectsMeasure));

        public GridLength Width
        {
            get => (GridLength)GetValue(WidthProperty);
            set => SetValue(WidthProperty, value);
        }

        public double ActualWidth { get; internal set; }
    }
}
