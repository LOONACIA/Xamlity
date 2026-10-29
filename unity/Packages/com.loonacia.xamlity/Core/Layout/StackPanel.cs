using System;
using Xamlity.Properties;
using Xamlity.Tree;

namespace Xamlity.Layout
{
    public class StackPanel : Panel
    {
        public static readonly DependencyProperty OrientationProperty = DependencyProperty.Register(
            nameof(Orientation), typeof(Orientation), typeof(StackPanel),
            new FrameworkPropertyMetadata(Orientation.Vertical, FrameworkPropertyMetadataOptions.AffectsMeasure));

        public Orientation Orientation
        {
            get => (Orientation)GetValue(OrientationProperty);
            set => SetValue(OrientationProperty, value);
        }

        protected override Size MeasureOverride(Size availableSize)
        {
            bool horizontal = Orientation == Orientation.Horizontal;
            var constraint = horizontal
                ? new Size(double.PositiveInfinity, availableSize.Height)
                : new Size(availableSize.Width, double.PositiveInfinity);
            double width = 0;
            double height = 0;
            foreach (UIElement child in Children)
            {
                child.Measure(constraint);
                width = horizontal ? width + child.DesiredSize.Width : Math.Max(width, child.DesiredSize.Width);
                height = horizontal ? Math.Max(height, child.DesiredSize.Height) : height + child.DesiredSize.Height;
            }
            return new Size(width, height);
        }

        protected override Size ArrangeOverride(Size finalSize)
        {
            bool horizontal = Orientation == Orientation.Horizontal;
            double offset = 0;
            foreach (UIElement child in Children)
            {
                if (horizontal)
                {
                    child.Arrange(new Rect(offset, 0, child.DesiredSize.Width, finalSize.Height));
                    offset += child.DesiredSize.Width;
                }
                else
                {
                    child.Arrange(new Rect(0, offset, finalSize.Width, child.DesiredSize.Height));
                    offset += child.DesiredSize.Height;
                }
            }
            return finalSize;
        }
    }
}
