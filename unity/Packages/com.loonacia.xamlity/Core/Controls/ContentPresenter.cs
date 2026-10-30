using System;
using System.Globalization;
using Xamlity.Layout;
using Xamlity.Properties;
using Xamlity.Tree;

namespace Xamlity.Controls
{
    public class ContentPresenter : FrameworkElement
    {
        public static readonly DependencyProperty ContentProperty = DependencyProperty.Register(
            nameof(Content), typeof(object), typeof(ContentPresenter),
            new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsMeasure,
                (d, e) => ((ContentPresenter)d).UpdateContent(e.NewValue)));

        private UIElement child;

        public object Content
        {
            get => GetValue(ContentProperty);
            set => SetValue(ContentProperty, value);
        }

        private void UpdateContent(object content)
        {
            UIElement next = content as UIElement;
            if (next == null && content != null)
                next = new TextBlock
                {
                    Text = content is string text ? text : content is IFormattable formattable
                        ? formattable.ToString(null, CultureInfo.InvariantCulture) : content.ToString(),
                    TemplatedParent = this
                };
            if (ReferenceEquals(child, next))
                return;
            if (next != null)
                AddVisualChild(next);
            DetachContent();
            child = next;
        }

        internal void DetachContent()
        {
            if (child == null)
                return;
            UIElement previous = child;
            child = null;
            RemoveVisualChild(previous);
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
            child?.Measure(availableSize);
            return child?.DesiredSize ?? new Size(0, 0);
        }

        protected override Size ArrangeOverride(Size finalSize)
        {
            child?.Arrange(new Rect(0, 0, finalSize.Width, finalSize.Height));
            return finalSize;
        }
    }
}
