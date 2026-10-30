using System;
using System.Collections.Generic;
using NUnit.Framework;
using Xamlity.Controls;
using Xamlity.Core.Tests.Rendering;
using Xamlity.Data;
using Xamlity.Layout;
using Xamlity.Properties;
using Xamlity.Tree;

namespace Xamlity.Core.Tests.Properties
{
    public class ValuePrecedenceTests
    {
        private static readonly ValueLayer[] Layers = {
            ValueLayer.ParentTemplateTrigger, ValueLayer.ParentTemplate, ValueLayer.ImplicitStyleReference,
            ValueLayer.StyleTrigger, ValueLayer.TemplateTrigger, ValueLayer.Style,
            ValueLayer.DefaultStyleTrigger, ValueLayer.DefaultStyle
        };

        [Test]
        public void RemovingValuesRevealsEveryLayerThenInheritanceThenDefault()
        {
            var parent = new StackPanel();
            var child = new TextBlock();
            parent.Children.Add(child);
            var property = TextBlock.FontSizeProperty;
            parent.SetValue(property, 40.0);
            for (int i = Layers.Length - 1; i >= 0; i--)
                child.SetLayerValue(property, Layers[i], 20.0 + i);
            Assert.That(child.ReadLocalValue(property), Is.SameAs(DependencyProperty.UnsetValue));
            child.FontSize = 10;
            Assert.That(child.FontSize, Is.EqualTo(10));
            child.ClearValue(property);
            for (int i = 0; i < Layers.Length; i++)
            {
                Assert.That(child.FontSize, Is.EqualTo(20.0 + i), Layers[i].ToString());
                child.ClearLayerValue(property, Layers[i]);
            }
            Assert.That(child.FontSize, Is.EqualTo(40));
            parent.ClearValue(property);
            Assert.That(child.FontSize, Is.EqualTo(property.DefaultMetadata.DefaultValue));
        }

        [Test]
        public void BindingIsLocalAndOverridesAllLayersUntilCleared()
        {
            var child = new TextBlock { DataContext = new TextBlock { FontSize = 42 } };
            var expression = child.SetBinding(TextBlock.FontSizeProperty,
                new Binding(nameof(TextBlock.FontSize)));
            foreach (ValueLayer layer in Layers)
                child.SetLayerValue(TextBlock.FontSizeProperty, layer, 20.0);
            Assert.That(child.ReadLocalValue(TextBlock.FontSizeProperty), Is.SameAs(expression));
            Assert.That(child.FontSize, Is.EqualTo(42));
            child.ClearValue(TextBlock.FontSizeProperty);
            Assert.That(child.FontSize, Is.EqualTo(20));
            Assert.That(BindingOperations.GetBindingExpression(child, TextBlock.FontSizeProperty), Is.Null);
        }

        [TestCase(typeof(int), null)]
        [TestCase(typeof(int?), null)]
        [TestCase(typeof(double), 1)]
        [TestCase(typeof(string), 1)]
        public void LayerTypeValidationMatchesLocalValues(Type type, object value)
        {
            var property = DependencyProperty.Register(Guid.NewGuid().ToString(), type, typeof(ValuePrecedenceTests));
            var node = new DependencyObject();
            foreach (ValueLayer layer in Layers)
            {
                Assert.Throws<ArgumentException>(() => node.SetLayerValue(property, layer, value));
                Assert.That(node.GetValue(property), Is.EqualTo(property.DefaultMetadata.DefaultValue));
            }
        }

        [Test]
        public void LayersRejectExpressionsAndUnsetEvenForObjectPropertiesAndAllowNullReferences()
        {
            var node = new FrameworkElement();
            var expression = node.SetBinding(FrameworkElement.NameProperty, new Binding());
            foreach (ValueLayer layer in Layers)
            {
                Assert.Throws<ArgumentException>(() => node.SetLayerValue(FrameworkElement.DataContextProperty, layer, expression));
                Assert.Throws<ArgumentException>(() => node.SetLayerValue(FrameworkElement.DataContextProperty, layer, DependencyProperty.UnsetValue));
            }
            node.SetLayerValue(FrameworkElement.DataContextProperty, ValueLayer.DefaultStyle, new object());
            node.SetLayerValue(FrameworkElement.DataContextProperty, ValueLayer.Style, null);
            Assert.That(node.DataContext, Is.Null);
            Assert.That(node.ReadLocalValue(FrameworkElement.DataContextProperty), Is.SameAs(DependencyProperty.UnsetValue));
            Assert.Throws<ArgumentNullException>(() => node.SetLayerValue(null, ValueLayer.Style, null));
            Assert.Throws<ArgumentNullException>(() => node.ClearLayerValue(null, ValueLayer.Style));
        }

        [Test]
        public void InheritedLayerChangesNotifyDescendantsAndChildLayersBlockPropagation()
        {
            var parent = new StackPanel();
            var middle = new TrackingPanel();
            var child = new TrackingTextBlock();
            parent.Children.Add(middle);
            middle.Children.Add(child);
            var property = TextBlock.FontSizeProperty;
            parent.SetLayerValue(property, ValueLayer.Style, 20.0);
            AssertChange(middle.Changes, 12.0, 20.0);
            AssertChange(child.Changes, 12.0, 20.0);
            middle.SetLayerValue(property, ValueLayer.DefaultStyle, 30.0);
            middle.Changes.Clear();
            child.Changes.Clear();
            parent.SetLayerValue(property, ValueLayer.Style, 40.0);
            Assert.That(middle.Changes, Is.Empty);
            Assert.That(child.Changes, Is.Empty);
            Assert.That(child.FontSize, Is.EqualTo(30));
            middle.ClearLayerValue(property, ValueLayer.DefaultStyle);
            AssertChange(middle.Changes, 30.0, 40.0);
            AssertChange(child.Changes, 30.0, 40.0);
            child.FontSize = 50;
            child.Changes.Clear();
            parent.SetLayerValue(property, ValueLayer.Style, 60.0);
            Assert.That(child.Changes, Is.Empty);
            child.ClearValue(property);
            AssertChange(child.Changes, 50.0, 60.0);
        }

        [Test]
        public void ReparentingUsesParentLayersAndPreservesChildLayerOverrides()
        {
            var parent = new StackPanel();
            var middle = new TrackingPanel();
            var child = new TrackingTextBlock();
            middle.Children.Add(child);
            parent.SetLayerValue(TextBlock.FontSizeProperty, ValueLayer.Style, 20.0);
            parent.Children.Add(middle);
            AssertChange(child.Changes, 12.0, 20.0);
            child.Changes.Clear();
            parent.Children.Remove(middle);
            AssertChange(child.Changes, 20.0, 12.0);
            middle.SetLayerValue(TextBlock.FontSizeProperty, ValueLayer.DefaultStyle, 30.0);
            child.Changes.Clear();
            middle.Changes.Clear();
            parent.Children.Add(middle);
            parent.Children.Remove(middle);
            Assert.That(child.Changes, Is.Empty);
            Assert.That(middle.Changes, Is.Empty);
            Assert.That(child.FontSize, Is.EqualTo(30));
        }

        [Test]
        public void OnlyEffectiveChangesNotifyAndInvalidateLayoutAndRender()
        {
            var changes = new List<string>();
            var node = new TrackingTextBlock { NotificationOrder = changes };
            var property = DependencyProperty.Register(Guid.NewGuid().ToString(), typeof(double), typeof(ValuePrecedenceTests),
                new FrameworkPropertyMetadata(0.0, FrameworkPropertyMetadataOptions.AffectsMeasure |
                    FrameworkPropertyMetadataOptions.AffectsArrange | FrameworkPropertyMetadataOptions.AffectsRender,
                    (d, e) => changes.Add("callback")));
            var root = new UIRoot(node);
            var context = new EmptyDrawingContext();
            node.SetLayerValue(property, ValueLayer.Style, 10.0);
            Assert.That(changes, Is.EqualTo(new[] { "callback", "override" }));
            root.UpdateLayout(new Size(100, 100));
            root.Render(context);
            changes.Clear();
            node.Changes.Clear();
            node.SetLayerValue(property, ValueLayer.Style, 10.0);
            node.SetLayerValue(property, ValueLayer.DefaultStyle, 20.0);
            node.ClearLayerValue(property, ValueLayer.DefaultStyle);
            node.SetLayerValue(property, ValueLayer.StyleTrigger, 10.0);
            node.ClearLayerValue(property, ValueLayer.StyleTrigger);
            Assert.That(changes, Is.Empty);
            Assert.That(node.Changes, Is.Empty);
            Assert.That(node.IsMeasureValid && node.IsArrangeValid, Is.True);
            Assert.That(root.IsRenderDirty, Is.False);
            node.SetLayerValue(property, ValueLayer.Style, 30.0);
            AssertChange(node.Changes, 10.0, 30.0);
            Assert.That(changes, Is.EqualTo(new[] { "callback", "override" }));
            Assert.That(node.IsMeasureValid || node.IsArrangeValid, Is.False);
            Assert.That(root.IsRenderDirty, Is.True);
        }

        [Test]
        public void LayeredTreeReadsAndUnchangedLayoutAndRenderAllocateNothing()
        {
            var parent = new StackPanel();
            var middle = new StackPanel();
            var child = new TextBlock();
            parent.Children.Add(middle);
            middle.Children.Add(child);
            foreach (ValueLayer layer in Layers)
                parent.SetLayerValue(TextBlock.FontSizeProperty, layer, 20.0);
            child.SetLayerValue(FrameworkElement.WidthProperty, ValueLayer.Style, 40.0);
            var root = new UIRoot(parent);
            var context = new EmptyDrawingContext();
            var viewport = new Size(200, 200);
            root.UpdateLayout(viewport);
            root.Render(context);
            child.GetValue(TextBlock.FontSizeProperty);
            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 1000; i++)
            {
                parent.GetValue(TextBlock.FontSizeProperty);
                middle.GetValue(TextBlock.FontSizeProperty);
                child.GetValue(TextBlock.FontSizeProperty);
                child.GetValue(FrameworkElement.WidthProperty);
                root.UpdateLayout(viewport);
                root.Render(context);
            }
            long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
            Assert.That(allocated, Is.Zero);
        }

        private static void AssertChange(List<DependencyPropertyChangedEventArgs> changes, object oldValue, object newValue)
        {
            Assert.That(changes.Count, Is.EqualTo(1));
            Assert.That(changes[0].OldValue, Is.EqualTo(oldValue));
            Assert.That(changes[0].NewValue, Is.EqualTo(newValue));
            changes.Clear();
        }

        private sealed class TrackingPanel : StackPanel
        {
            public List<DependencyPropertyChangedEventArgs> Changes { get; } = new List<DependencyPropertyChangedEventArgs>();
            protected override void OnPropertyChanged(DependencyPropertyChangedEventArgs e)
            {
                base.OnPropertyChanged(e);
                Changes.Add(e);
            }
        }

        private sealed class TrackingTextBlock : TextBlock
        {
            public List<DependencyPropertyChangedEventArgs> Changes { get; } = new List<DependencyPropertyChangedEventArgs>();
            public List<string> NotificationOrder { get; set; }
            protected override void OnPropertyChanged(DependencyPropertyChangedEventArgs e)
            {
                base.OnPropertyChanged(e);
                Changes.Add(e);
                NotificationOrder?.Add("override");
            }
        }
    }
}
