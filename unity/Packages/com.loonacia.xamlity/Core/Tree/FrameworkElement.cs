using System;
using Xamlity.Layout;
using Xamlity.Properties;

namespace Xamlity.Tree
{
    public class FrameworkElement : UIElement
    {
        public static readonly DependencyProperty WidthProperty = DependencyProperty.Register(
            nameof(Width), typeof(double), typeof(FrameworkElement),
            new FrameworkPropertyMetadata(double.NaN, FrameworkPropertyMetadataOptions.AffectsMeasure));

        public static readonly DependencyProperty HeightProperty = DependencyProperty.Register(
            nameof(Height), typeof(double), typeof(FrameworkElement),
            new FrameworkPropertyMetadata(double.NaN, FrameworkPropertyMetadataOptions.AffectsMeasure));

        public static readonly DependencyProperty MinWidthProperty = DependencyProperty.Register(
            nameof(MinWidth), typeof(double), typeof(FrameworkElement),
            new FrameworkPropertyMetadata(0.0, FrameworkPropertyMetadataOptions.AffectsMeasure));

        public static readonly DependencyProperty MinHeightProperty = DependencyProperty.Register(
            nameof(MinHeight), typeof(double), typeof(FrameworkElement),
            new FrameworkPropertyMetadata(0.0, FrameworkPropertyMetadataOptions.AffectsMeasure));

        public static readonly DependencyProperty MaxWidthProperty = DependencyProperty.Register(
            nameof(MaxWidth), typeof(double), typeof(FrameworkElement),
            new FrameworkPropertyMetadata(double.PositiveInfinity, FrameworkPropertyMetadataOptions.AffectsMeasure));

        public static readonly DependencyProperty MaxHeightProperty = DependencyProperty.Register(
            nameof(MaxHeight), typeof(double), typeof(FrameworkElement),
            new FrameworkPropertyMetadata(double.PositiveInfinity, FrameworkPropertyMetadataOptions.AffectsMeasure));

        public static readonly DependencyProperty MarginProperty = DependencyProperty.Register(
            nameof(Margin), typeof(Thickness), typeof(FrameworkElement),
            new FrameworkPropertyMetadata(new Thickness(0), FrameworkPropertyMetadataOptions.AffectsMeasure));

        public static readonly DependencyProperty HorizontalAlignmentProperty = DependencyProperty.Register(
            nameof(HorizontalAlignment), typeof(HorizontalAlignment), typeof(FrameworkElement),
            new FrameworkPropertyMetadata(HorizontalAlignment.Stretch, FrameworkPropertyMetadataOptions.AffectsArrange));

        public static readonly DependencyProperty VerticalAlignmentProperty = DependencyProperty.Register(
            nameof(VerticalAlignment), typeof(VerticalAlignment), typeof(FrameworkElement),
            new FrameworkPropertyMetadata(VerticalAlignment.Stretch, FrameworkPropertyMetadataOptions.AffectsArrange));

        public static readonly DependencyProperty NameProperty = DependencyProperty.Register(
            nameof(Name), typeof(string), typeof(FrameworkElement),
            new FrameworkPropertyMetadata(string.Empty, FrameworkPropertyMetadataOptions.None));

        private Size unclippedDesiredSize;

        public double Width
        {
            get => (double)GetValue(WidthProperty);
            set => SetValue(WidthProperty, value);
        }

        public double Height
        {
            get => (double)GetValue(HeightProperty);
            set => SetValue(HeightProperty, value);
        }

        public double MinWidth
        {
            get => (double)GetValue(MinWidthProperty);
            set => SetValue(MinWidthProperty, value);
        }

        public double MinHeight
        {
            get => (double)GetValue(MinHeightProperty);
            set => SetValue(MinHeightProperty, value);
        }

        public double MaxWidth
        {
            get => (double)GetValue(MaxWidthProperty);
            set => SetValue(MaxWidthProperty, value);
        }

        public double MaxHeight
        {
            get => (double)GetValue(MaxHeightProperty);
            set => SetValue(MaxHeightProperty, value);
        }

        public Thickness Margin
        {
            get => (Thickness)GetValue(MarginProperty);
            set => SetValue(MarginProperty, value);
        }

        public HorizontalAlignment HorizontalAlignment
        {
            get => (HorizontalAlignment)GetValue(HorizontalAlignmentProperty);
            set => SetValue(HorizontalAlignmentProperty, value);
        }

        public VerticalAlignment VerticalAlignment
        {
            get => (VerticalAlignment)GetValue(VerticalAlignmentProperty);
            set => SetValue(VerticalAlignmentProperty, value);
        }

        public string Name
        {
            get => (string)GetValue(NameProperty);
            set => SetValue(NameProperty, value);
        }

        protected virtual Size MeasureOverride(Size availableSize) => new Size(0, 0);
        protected virtual Size ArrangeOverride(Size finalSize) => finalSize;

        protected sealed override Size MeasureCore(Size availableSize)
        {
            Thickness margin = Margin;
            double marginWidth = margin.Left + margin.Right;
            double marginHeight = margin.Top + margin.Bottom;
            GetSizeLimits(out Size minimum, out Size maximum);
            var constraint = new Size(
                Math.Max(minimum.Width, Math.Min(Math.Max(0, availableSize.Width - marginWidth), maximum.Width)),
                Math.Max(minimum.Height, Math.Min(Math.Max(0, availableSize.Height - marginHeight), maximum.Height)));
            Size desired = MeasureOverride(constraint);

            // Keep the content's minimum size even when the size reported to the parent is constrained.
            unclippedDesiredSize = new Size(Math.Max(minimum.Width, desired.Width),
                Math.Max(minimum.Height, desired.Height));
            return new Size(
                Math.Max(0, Math.Min(availableSize.Width, Math.Min(unclippedDesiredSize.Width, maximum.Width) + marginWidth)),
                Math.Max(0, Math.Min(availableSize.Height, Math.Min(unclippedDesiredSize.Height, maximum.Height) + marginHeight)));
        }

        protected sealed override void ArrangeCore(Rect finalRect)
        {
            Thickness margin = Margin;
            var clientSize = new Size(Math.Max(0, finalRect.Width - margin.Left - margin.Right),
                Math.Max(0, finalRect.Height - margin.Top - margin.Bottom));
            GetSizeLimits(out _, out Size maximum);
            double width = HorizontalAlignment == HorizontalAlignment.Stretch
                ? Math.Max(clientSize.Width, unclippedDesiredSize.Width) : unclippedDesiredSize.Width;
            double height = VerticalAlignment == VerticalAlignment.Stretch
                ? Math.Max(clientSize.Height, unclippedDesiredSize.Height) : unclippedDesiredSize.Height;
            var arrangeSize = new Size(
                Math.Min(width, Math.Max(unclippedDesiredSize.Width, maximum.Width)),
                Math.Min(height, Math.Max(unclippedDesiredSize.Height, maximum.Height)));
            RenderSize = ArrangeOverride(arrangeSize);

            // WPF aligns using the maximum-constrained ink size, without changing RenderSize.
            double inkWidth = Math.Min(RenderSize.Width, maximum.Width);
            double inkHeight = Math.Min(RenderSize.Height, maximum.Height);
            double x = 0;
            double y = 0;
            if (HorizontalAlignment == HorizontalAlignment.Center
                || (HorizontalAlignment == HorizontalAlignment.Stretch && inkWidth <= clientSize.Width))
                x = (clientSize.Width - inkWidth) / 2;
            else if (HorizontalAlignment == HorizontalAlignment.Right)
                x = clientSize.Width - inkWidth;
            if (VerticalAlignment == VerticalAlignment.Center
                || (VerticalAlignment == VerticalAlignment.Stretch && inkHeight <= clientSize.Height))
                y = (clientSize.Height - inkHeight) / 2;
            else if (VerticalAlignment == VerticalAlignment.Bottom)
                y = clientSize.Height - inkHeight;
            VisualOffset = new Point(finalRect.X + margin.Left + x, finalRect.Y + margin.Top + y);
        }

        private void GetSizeLimits(out Size minimum, out Size maximum)
        {
            double width = Width;
            double height = Height;
            double minWidth = MinWidth;
            double minHeight = MinHeight;
            double maxWidth = Math.Max(minWidth, Math.Min(MaxWidth, double.IsNaN(width) ? double.PositiveInfinity : width));
            double maxHeight = Math.Max(minHeight, Math.Min(MaxHeight, double.IsNaN(height) ? double.PositiveInfinity : height));
            minimum = new Size(
                Math.Max(minWidth, Math.Min(maxWidth, double.IsNaN(width) ? 0 : width)),
                Math.Max(minHeight, Math.Min(maxHeight, double.IsNaN(height) ? 0 : height)));
            maximum = new Size(maxWidth, maxHeight);
        }
    }
}
