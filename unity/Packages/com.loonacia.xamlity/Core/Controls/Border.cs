using System;
using Xamlity.Layout;
using Xamlity.Markup;
using Xamlity.Properties;
using Xamlity.Rendering;
using Xamlity.Tree;

namespace Xamlity.Controls
{
    [ContentProperty("Child")]
    public class Border : FrameworkElement
    {
        public static readonly DependencyProperty BackgroundProperty = DependencyProperty.Register(
            nameof(Background), typeof(Brush), typeof(Border),
            new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));
        public static readonly DependencyProperty BorderBrushProperty = DependencyProperty.Register(
            nameof(BorderBrush), typeof(Brush), typeof(Border),
            new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));
        public static readonly DependencyProperty BorderThicknessProperty = DependencyProperty.Register(
            nameof(BorderThickness), typeof(Thickness), typeof(Border),
            new FrameworkPropertyMetadata(new Thickness(0),
                FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsRender));
        public static readonly DependencyProperty PaddingProperty = DependencyProperty.Register(
            nameof(Padding), typeof(Thickness), typeof(Border),
            new FrameworkPropertyMetadata(new Thickness(0),
                FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsRender));

        private UIElement child;

        public Brush Background
        {
            get => (Brush)GetValue(BackgroundProperty);
            set => SetValue(BackgroundProperty, value);
        }

        public Brush BorderBrush
        {
            get => (Brush)GetValue(BorderBrushProperty);
            set => SetValue(BorderBrushProperty, value);
        }

        public Thickness BorderThickness
        {
            get => (Thickness)GetValue(BorderThicknessProperty);
            set => SetValue(BorderThicknessProperty, value);
        }

        public Thickness Padding
        {
            get => (Thickness)GetValue(PaddingProperty);
            set => SetValue(PaddingProperty, value);
        }

        public UIElement Child
        {
            get => child;
            set
            {
                if (ReferenceEquals(child, value))
                    return;
                // Attach first so an invalid replacement preserves the current child.
                if (value != null)
                    AddVisualChild(value);
                if (child != null)
                    RemoveVisualChild(child);
                child = value;
                InvalidateMeasure();
            }
        }

        protected override int VisualChildrenCount => child == null ? 0 : 1;

        protected override UIElement GetVisualChild(int index)
        {
            if (index != 0 || child == null)
                throw new ArgumentOutOfRangeException(nameof(index));
            return child;
        }

        protected override Size MeasureOverride(Size availableSize)
        {
            Thickness border = BorderThickness;
            Thickness padding = Padding;
            double width = border.Left + border.Right + padding.Left + padding.Right;
            double height = border.Top + border.Bottom + padding.Top + padding.Bottom;
            child?.Measure(new Size(Math.Max(0, availableSize.Width - width),
                Math.Max(0, availableSize.Height - height)));
            Size desired = child == null ? new Size(0, 0) : child.DesiredSize;
            return new Size(desired.Width + width, desired.Height + height);
        }

        protected override Size ArrangeOverride(Size finalSize)
        {
            Thickness border = BorderThickness;
            Thickness padding = Padding;
            child?.Arrange(new Rect(border.Left + padding.Left, border.Top + padding.Top,
                Math.Max(0, finalSize.Width - border.Left - border.Right - padding.Left - padding.Right),
                Math.Max(0, finalSize.Height - border.Top - border.Bottom - padding.Top - padding.Bottom)));
            return finalSize;
        }

        protected override void OnRender(IDrawingContext drawingContext)
        {
            Thickness border = BorderThickness;
            double width = RenderSize.Width;
            double height = RenderSize.Height;
            double innerWidth = Math.Max(0, width - border.Left - border.Right);
            double innerHeight = Math.Max(0, height - border.Top - border.Bottom);
            Brush borderBrush = BorderBrush;
            if (borderBrush != null)
            {
                DrawRectangle(drawingContext, borderBrush, 0, 0, width, border.Top);
                DrawRectangle(drawingContext, borderBrush, 0, height - border.Bottom, width, border.Bottom);
                DrawRectangle(drawingContext, borderBrush, 0, border.Top, border.Left, innerHeight);
                DrawRectangle(drawingContext, borderBrush, width - border.Right, border.Top, border.Right, innerHeight);
            }
            Brush background = Background;
            if (background != null)
                DrawRectangle(drawingContext, background, border.Left, border.Top, innerWidth, innerHeight);
        }

        private static void DrawRectangle(IDrawingContext drawingContext, Brush brush,
            double x, double y, double width, double height)
        {
            if (width > 0 && height > 0)
                drawingContext.DrawRectangle(brush, new Rect(x, y, width, height));
        }
    }
}
