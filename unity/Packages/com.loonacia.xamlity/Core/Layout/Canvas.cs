using System;
using Xamlity.Properties;
using Xamlity.Tree;

namespace Xamlity.Layout
{
    public class Canvas : Panel
    {
        public static readonly DependencyProperty LeftProperty = DependencyProperty.RegisterAttached(
            "Left", typeof(double), typeof(Canvas),
            new FrameworkPropertyMetadata(double.NaN, FrameworkPropertyMetadataOptions.AffectsArrange));
        public static readonly DependencyProperty TopProperty = DependencyProperty.RegisterAttached(
            "Top", typeof(double), typeof(Canvas),
            new FrameworkPropertyMetadata(double.NaN, FrameworkPropertyMetadataOptions.AffectsArrange));
        public static readonly DependencyProperty RightProperty = DependencyProperty.RegisterAttached(
            "Right", typeof(double), typeof(Canvas),
            new FrameworkPropertyMetadata(double.NaN, FrameworkPropertyMetadataOptions.AffectsArrange));
        public static readonly DependencyProperty BottomProperty = DependencyProperty.RegisterAttached(
            "Bottom", typeof(double), typeof(Canvas),
            new FrameworkPropertyMetadata(double.NaN, FrameworkPropertyMetadataOptions.AffectsArrange));

        public static double GetLeft(UIElement element) =>
            (double)(element ?? throw new ArgumentNullException(nameof(element))).GetValue(LeftProperty);
        public static void SetLeft(UIElement element, double length) =>
            (element ?? throw new ArgumentNullException(nameof(element))).SetValue(LeftProperty, length);
        public static double GetTop(UIElement element) =>
            (double)(element ?? throw new ArgumentNullException(nameof(element))).GetValue(TopProperty);
        public static void SetTop(UIElement element, double length) =>
            (element ?? throw new ArgumentNullException(nameof(element))).SetValue(TopProperty, length);
        public static double GetRight(UIElement element) =>
            (double)(element ?? throw new ArgumentNullException(nameof(element))).GetValue(RightProperty);
        public static void SetRight(UIElement element, double length) =>
            (element ?? throw new ArgumentNullException(nameof(element))).SetValue(RightProperty, length);
        public static double GetBottom(UIElement element) =>
            (double)(element ?? throw new ArgumentNullException(nameof(element))).GetValue(BottomProperty);
        public static void SetBottom(UIElement element, double length) =>
            (element ?? throw new ArgumentNullException(nameof(element))).SetValue(BottomProperty, length);

        protected override Rect? GetLayoutClip(Size layoutSlotSize) =>
            ClipToBounds ? new Rect(new Point(0, 0), RenderSize) : (Rect?)null;

        protected override Size MeasureOverride(Size availableSize)
        {
            var constraint = new Size(double.PositiveInfinity, double.PositiveInfinity);
            foreach (UIElement child in Children)
                child.Measure(constraint);
            return new Size(0, 0);
        }

        protected override Size ArrangeOverride(Size finalSize)
        {
            foreach (UIElement child in Children)
            {
                double left = GetLeft(child);
                double top = GetTop(child);
                double right = GetRight(child);
                double bottom = GetBottom(child);
                double x = !double.IsNaN(left) ? left : !double.IsNaN(right)
                    ? finalSize.Width - child.DesiredSize.Width - right : 0;
                double y = !double.IsNaN(top) ? top : !double.IsNaN(bottom)
                    ? finalSize.Height - child.DesiredSize.Height - bottom : 0;
                child.Arrange(new Rect(new Point(x, y), child.DesiredSize));
            }
            return finalSize;
        }
    }
}
