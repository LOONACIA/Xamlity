using System;
using Xamlity.Layout;
using Xamlity.Properties;

namespace Xamlity.Tree
{
    public abstract class UIElement : DependencyObject
    {
        private Size previousAvailableSize;
        private Rect previousFinalRect;
        private bool hasMeasured;

        public Size DesiredSize { get; private set; }
        public Size RenderSize { get; protected set; }
        public Point VisualOffset { get; protected set; }
        public bool IsMeasureValid { get; private set; }
        public bool IsArrangeValid { get; private set; }
        public UIElement VisualParent { get; private set; }

        public void Measure(Size availableSize)
        {
            if (IsMeasureValid && availableSize == previousAvailableSize)
                return;

            IsMeasureValid = false;
            IsArrangeValid = false;
            DesiredSize = MeasureCore(availableSize);
            previousAvailableSize = availableSize;
            hasMeasured = true;
            IsMeasureValid = true;
        }

        public void Arrange(Rect finalRect)
        {
            if (!IsMeasureValid)
                Measure(hasMeasured ? previousAvailableSize : finalRect.Size);
            if (IsArrangeValid && finalRect == previousFinalRect)
                return;

            IsArrangeValid = false;
            ArrangeCore(finalRect);
            previousFinalRect = finalRect;
            IsArrangeValid = true;
        }

        protected virtual Size MeasureCore(Size availableSize) => new Size(0, 0);

        protected virtual void ArrangeCore(Rect finalRect)
        {
            VisualOffset = finalRect.Location;
            RenderSize = finalRect.Size;
        }

        public void InvalidateMeasure()
        {
            for (UIElement element = this; element != null; element = element.VisualParent)
            {
                element.IsMeasureValid = false;
                element.IsArrangeValid = false;
            }
        }

        public void InvalidateArrange()
        {
            for (UIElement element = this; element != null; element = element.VisualParent)
                element.IsArrangeValid = false;
        }

        protected void AddVisualChild(UIElement child)
        {
            if (child == null)
                throw new ArgumentNullException(nameof(child));
            if (child.VisualParent != null)
                throw new InvalidOperationException("The element already has a visual parent.");
            for (UIElement element = this; element != null; element = element.VisualParent)
            {
                if (element == child)
                    throw new InvalidOperationException("A visual tree cannot contain a cycle.");
            }

            child.VisualParent = this;
            child.OnInheritanceParentChanged(null);
            InvalidateMeasure();
        }

        protected void RemoveVisualChild(UIElement child)
        {
            if (child == null)
                throw new ArgumentNullException(nameof(child));
            if (child.VisualParent != this)
                throw new InvalidOperationException("The element is not a visual child of this parent.");

            child.VisualParent = null;
            child.OnInheritanceParentChanged(this);
            InvalidateMeasure();
        }

        // Derived containers own their children and expose them through these members.
        protected virtual int VisualChildrenCount => 0;

        protected virtual UIElement GetVisualChild(int index)
        {
            throw new ArgumentOutOfRangeException(nameof(index));
        }

        protected internal override DependencyObject InheritanceParent => VisualParent;
        protected internal override int InheritanceChildrenCount => VisualChildrenCount;
        protected internal override DependencyObject GetInheritanceChild(int index) => GetVisualChild(index);

        protected override void OnPropertyChanged(DependencyPropertyChangedEventArgs e)
        {
            base.OnPropertyChanged(e);
            if (e.Property.DefaultMetadata is FrameworkPropertyMetadata metadata)
            {
                if (metadata.AffectsMeasure)
                    InvalidateMeasure();
                if (metadata.AffectsArrange)
                    InvalidateArrange();
            }
        }
    }
}
