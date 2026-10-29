using Xamlity.Properties;

namespace Xamlity.Layout
{
    public class RowDefinition : DefinitionBase
    {
        public static readonly DependencyProperty HeightProperty = DependencyProperty.Register(
            nameof(Height), typeof(GridLength), typeof(RowDefinition),
            new FrameworkPropertyMetadata(new GridLength(1, GridUnitType.Star), FrameworkPropertyMetadataOptions.AffectsMeasure));

        public GridLength Height
        {
            get => (GridLength)GetValue(HeightProperty);
            set => SetValue(HeightProperty, value);
        }

        public double ActualHeight { get; internal set; }
    }
}
