using System;
using Xamlity.Layout;
using Xamlity.Properties;
using Xamlity.Rendering;

namespace Xamlity.Tree
{
    public abstract class UIElement : DependencyObject
    {
        public static readonly DependencyProperty OpacityProperty = DependencyProperty.Register(
            nameof(Opacity), typeof(double), typeof(UIElement),
            new FrameworkPropertyMetadata(1.0, FrameworkPropertyMetadataOptions.AffectsRender));

        private Size previousAvailableSize;
        private Rect previousFinalRect;
        private bool hasMeasured;

        public Size DesiredSize { get; private set; }
        public Size RenderSize { get; protected set; }
        public Point VisualOffset { get; protected set; }
        public bool IsMeasureValid { get; private set; }
        public bool IsArrangeValid { get; private set; }
        public UIElement VisualParent { get; private set; }
        internal UIRoot Root { get; set; }

        public double Opacity
        {
            get => (double)GetValue(OpacityProperty);
            set => SetValue(OpacityProperty, value);
        }

        internal UIRoot GetRoot()
        {
            UIElement element = this;
            while (element.VisualParent != null)
                element = element.VisualParent;
            return element.Root;
        }

        internal void InvalidateSubtreeMeasure()
        {
            IsMeasureValid = false;
            IsArrangeValid = false;
            for (int index = 0; index < VisualChildrenCount; index++)
                GetVisualChild(index).InvalidateSubtreeMeasure();
        }

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
            Point oldOffset = VisualOffset;
            Size oldSize = RenderSize;
            ArrangeCore(finalRect);
            previousFinalRect = finalRect;
            IsArrangeValid = true;
            if (VisualOffset != oldOffset || RenderSize != oldSize)
                InvalidateVisual();
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

        public void InvalidateVisual()
        {
            UIElement element = this;
            while (element.VisualParent != null)
                element = element.VisualParent;
            element.Root?.InvalidateVisual();
        }

        protected virtual void OnRender(IDrawingContext drawingContext)
        {
        }

        internal void Render(UIRoot.OffsetDrawingContext drawingContext, Point parentOffset)
        {
            double opacity = Opacity;
            if (opacity <= 0 || double.IsNaN(opacity))
                return;

            bool pushOpacity = opacity < 1;
            if (pushOpacity)
                drawingContext.PushOpacity(opacity);
            try
            {
                var offset = new Point(parentOffset.X + VisualOffset.X, parentOffset.Y + VisualOffset.Y);
                drawingContext.Offset = offset;
                OnRender(drawingContext);
                for (int index = 0; index < VisualChildrenCount; index++)
                    GetVisualChild(index).Render(drawingContext, offset);
            }
            finally
            {
                if (pushOpacity)
                    drawingContext.Pop();
            }
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
            child.InvalidateSubtreeMeasure();
            child.OnInheritanceParentChanged(null);
            InvalidateMeasure();
            InvalidateVisual();
        }

        protected void RemoveVisualChild(UIElement child)
        {
            if (child == null)
                throw new ArgumentNullException(nameof(child));
            if (child.VisualParent != this)
                throw new InvalidOperationException("The element is not a visual child of this parent.");

            child.VisualParent = null;
            child.InvalidateSubtreeMeasure();
            child.OnInheritanceParentChanged(this);
            InvalidateMeasure();
            InvalidateVisual();
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
                if (metadata.AffectsRender)
                    InvalidateVisual();
            }
        }
    }
}
