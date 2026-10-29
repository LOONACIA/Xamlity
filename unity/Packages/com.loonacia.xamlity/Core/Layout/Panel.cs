using Xamlity.Tree;

namespace Xamlity.Layout
{
    public abstract class Panel : FrameworkElement
    {
        protected Panel()
        {
            Children = new UIElementCollection(this);
        }

        public UIElementCollection Children { get; }

        protected override int VisualChildrenCount => Children.Count;
        protected override UIElement GetVisualChild(int index) => Children[index];

        internal void AttachChild(UIElement child) => AddVisualChild(child);
        internal void DetachChild(UIElement child) => RemoveVisualChild(child);
    }
}
