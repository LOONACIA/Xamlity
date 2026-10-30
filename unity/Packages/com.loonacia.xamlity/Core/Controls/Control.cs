using System;
using System.Collections.Generic;
using Xamlity.Layout;
using Xamlity.Properties;
using Xamlity.Rendering;
using Xamlity.Styling;
using Xamlity.Tree;

namespace Xamlity.Controls
{
    public class Control : FrameworkElement
    {
        public static readonly DependencyProperty TemplateProperty = DependencyProperty.Register(
            nameof(Template), typeof(ControlTemplate), typeof(Control),
            new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsMeasure,
                (d, e) => ((Control)d).ClearTemplate()));
        public static readonly DependencyProperty BackgroundProperty = DependencyProperty.Register(
            nameof(Background), typeof(Brush), typeof(Control),
            new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));
        public static readonly DependencyProperty BorderBrushProperty = DependencyProperty.Register(
            nameof(BorderBrush), typeof(Brush), typeof(Control),
            new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));
        public static readonly DependencyProperty BorderThicknessProperty = DependencyProperty.Register(
            nameof(BorderThickness), typeof(Thickness), typeof(Control),
            new FrameworkPropertyMetadata(new Thickness(0), FrameworkPropertyMetadataOptions.AffectsMeasure));
        public static readonly DependencyProperty PaddingProperty = DependencyProperty.Register(
            nameof(Padding), typeof(Thickness), typeof(Control),
            new FrameworkPropertyMetadata(new Thickness(0), FrameworkPropertyMetadataOptions.AffectsMeasure));
        public static readonly DependencyProperty HorizontalContentAlignmentProperty = DependencyProperty.Register(
            nameof(HorizontalContentAlignment), typeof(HorizontalAlignment), typeof(Control),
            new FrameworkPropertyMetadata(HorizontalAlignment.Left, FrameworkPropertyMetadataOptions.AffectsArrange));
        public static readonly DependencyProperty VerticalContentAlignmentProperty = DependencyProperty.Register(
            nameof(VerticalContentAlignment), typeof(VerticalAlignment), typeof(Control),
            new FrameworkPropertyMetadata(VerticalAlignment.Top, FrameworkPropertyMetadataOptions.AffectsArrange));
        public static readonly DependencyProperty ForegroundProperty = TextBlock.ForegroundProperty.AddOwner(typeof(Control));
        public static readonly DependencyProperty FontSizeProperty = TextBlock.FontSizeProperty.AddOwner(typeof(Control));

        private FrameworkElement templateRoot;
        private bool templateApplied;
        private List<TemplateBinding> templateBindings;
        private TriggerInstance templateTriggers;

        public ControlTemplate Template
        {
            get => (ControlTemplate)GetValue(TemplateProperty);
            set => SetValue(TemplateProperty, value);
        }

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

        public HorizontalAlignment HorizontalContentAlignment
        {
            get => (HorizontalAlignment)GetValue(HorizontalContentAlignmentProperty);
            set => SetValue(HorizontalContentAlignmentProperty, value);
        }

        public VerticalAlignment VerticalContentAlignment
        {
            get => (VerticalAlignment)GetValue(VerticalContentAlignmentProperty);
            set => SetValue(VerticalContentAlignmentProperty, value);
        }

        public Brush Foreground
        {
            get => (Brush)GetValue(ForegroundProperty);
            set => SetValue(ForegroundProperty, value);
        }

        public double FontSize
        {
            get => (double)GetValue(FontSizeProperty);
            set => SetValue(FontSizeProperty, value);
        }

        internal bool ApplyControlTemplate()
        {
            ControlTemplate template = Template;
            if (templateApplied || template == null)
                return false;
            if (template.TargetType != null && !template.TargetType.IsAssignableFrom(GetType()))
                throw new InvalidOperationException($"'{template.TargetType.Name}' ControlTemplate TargetType does not match templated type '{GetType().Name}'.");
            template.Seal();
            var names = new Dictionary<string, DependencyObject>(StringComparer.Ordinal);
            var bindings = new List<TemplateBinding>();
            FrameworkElement root = template.BuildContent(this, names, bindings);
            templateRoot = root;
            templateBindings = bindings;
            TemplateNames = names;
            templateApplied = true;
            if (template.Triggers.Count != 0)
            {
                templateTriggers = new TriggerInstance(this, template.Triggers, ValueLayer.TemplateTrigger, names);
                templateTriggers.Start();
            }
            if (root == null)
                return false;
            AddVisualChild(root);
            OnApplyTemplate();
            return true;
        }

        private void ClearTemplate()
        {
            TriggerInstance previousTriggers = templateTriggers;
            templateTriggers = null;
            previousTriggers?.Detach();
            FrameworkElement previous = templateRoot;
            templateRoot = null;
            templateBindings = null;
            TemplateNames = null;
            templateApplied = false;
            if (previous != null)
            {
                DetachTemplateContent(previous);
                RemoveVisualChild(previous);
            }
        }

        private void DetachTemplateContent(UIElement element)
        {
            if (element is ContentPresenter presenter && ReferenceEquals(presenter.TemplatedParent, this))
            {
                presenter.DetachContent();
                return;
            }
            for (int i = 0; i < element.InheritanceChildrenCount; i++)
                DetachTemplateContent((UIElement)element.GetInheritanceChild(i));
        }

        protected override int VisualChildrenCount => templateRoot == null ? 0 : 1;

        protected override void OnPropertyChanged(DependencyPropertyChangedEventArgs e)
        {
            base.OnPropertyChanged(e);
            templateTriggers?.OnPropertyChanged(e.Property);
            List<TemplateBinding> bindings = templateBindings;
            if (bindings == null)
                return;
            for (int i = 0; i < bindings.Count && ReferenceEquals(bindings, templateBindings); i++)
                if (bindings[i].SourceProperty == e.Property)
                    bindings[i].Update(e.NewValue);
        }

        protected override UIElement GetVisualChild(int index)
        {
            if (index != 0 || templateRoot == null)
                throw new ArgumentOutOfRangeException(nameof(index));
            return templateRoot;
        }

        protected override Size MeasureOverride(Size availableSize)
        {
            ApplyTemplate();
            templateRoot?.Measure(availableSize);
            return templateRoot?.DesiredSize ?? new Size(0, 0);
        }

        protected override Size ArrangeOverride(Size finalSize)
        {
            templateRoot?.Arrange(new Rect(0, 0, finalSize.Width, finalSize.Height));
            return finalSize;
        }
    }
}
