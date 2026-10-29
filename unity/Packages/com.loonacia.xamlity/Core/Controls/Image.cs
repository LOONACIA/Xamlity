using System;
using Xamlity.Layout;
using Xamlity.Properties;
using Xamlity.Rendering;
using Xamlity.Tree;

namespace Xamlity.Controls
{
    public class Image : FrameworkElement
    {
        public static readonly DependencyProperty SourceProperty = DependencyProperty.Register(
            nameof(Source), typeof(ImageSource), typeof(Image),
            new FrameworkPropertyMetadata(null,
                FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty StretchProperty = DependencyProperty.Register(
            nameof(Stretch), typeof(Stretch), typeof(Image),
            new FrameworkPropertyMetadata(Stretch.Uniform, FrameworkPropertyMetadataOptions.AffectsMeasure));

        public ImageSource Source
        {
            get => (ImageSource)GetValue(SourceProperty);
            set => SetValue(SourceProperty, value);
        }

        public Stretch Stretch
        {
            get => (Stretch)GetValue(StretchProperty);
            set => SetValue(StretchProperty, value);
        }

        protected override Size MeasureOverride(Size availableSize) => MeasureArrangeHelper(availableSize);

        protected override Size ArrangeOverride(Size finalSize) => MeasureArrangeHelper(finalSize);

        private Size MeasureArrangeHelper(Size constraint)
        {
            Stretch stretch = Stretch;
            if (stretch < Stretch.None || stretch > Stretch.UniformToFill)
                throw new ArgumentOutOfRangeException(nameof(Stretch));

            ImageSource source = Source;
            if (source == null)
                return new Size(0, 0);

            double width = source.Width;
            double height = source.Height;
            double scaleX = 1;
            double scaleY = 1;
            bool constrainedWidth = !double.IsPositiveInfinity(constraint.Width);
            bool constrainedHeight = !double.IsPositiveInfinity(constraint.Height);
            if (stretch != Stretch.None && (constrainedWidth || constrainedHeight))
            {
                scaleX = width == 0 ? 0 : constraint.Width / width;
                scaleY = height == 0 ? 0 : constraint.Height / height;
                if (!constrainedWidth)
                    scaleX = scaleY;
                else if (!constrainedHeight)
                    scaleY = scaleX;
                else if (stretch == Stretch.Uniform)
                    scaleX = scaleY = Math.Min(scaleX, scaleY);
                else if (stretch == Stretch.UniformToFill)
                    scaleX = scaleY = Math.Max(scaleX, scaleY);
            }
            return new Size(width * scaleX, height * scaleY);
        }

        protected override void OnRender(IDrawingContext drawingContext)
        {
            ImageSource source = Source;
            if (source != null)
                drawingContext.DrawImage(source, new Rect(new Point(0, 0), RenderSize));
        }
    }
}
