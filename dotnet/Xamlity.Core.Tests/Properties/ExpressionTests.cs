using System;
using System.Collections.Generic;
using NUnit.Framework;
using Xamlity.Properties;

namespace Xamlity.Core.Tests.Properties
{
    public class ExpressionTests
    {
        [Test]
        public void ExpressionsNotifyLikeLocalValuesAndDetachOnReplacementOrRemoval()
        {
            var order = new List<string>();
            var node = new Node(order);
            var property = DependencyProperty.Register(Guid.NewGuid().ToString(), typeof(int), typeof(Node),
                new PropertyMetadata(5, (d, e) =>
                {
                    Assert.That(d.GetValue(e.Property), Is.EqualTo(e.NewValue));
                    order.Add("callback");
                }));
            var first = new TestExpression(node, property, 10);
            node.SetExpression(property, first);
            Assert.That(first.Attached, Is.True);
            Assert.That(node.GetValue(property), Is.EqualTo(10));
            Assert.That(node.ReadLocalValue(property), Is.SameAs(first));
            Assert.That(order, Is.EqualTo(new[] { "callback", "override" }));
            AssertChange(node, 5, 10);
            order.Clear();
            node.Changes.Clear();
            first.Change(10);
            Assert.That(order, Is.Empty);
            var second = new TestExpression(node, property, 10);
            node.SetExpression(property, second);
            Assert.That(first.Attached, Is.False);
            Assert.That(second.Attached, Is.True);
            Assert.That(order, Is.Empty);
            first.Change(99);
            Assert.That(node.GetValue(property), Is.EqualTo(10));
            second.Change(20);
            AssertChange(node, 10, 20);
            node.Changes.Clear();
            node.SetValue(property, 30);
            Assert.That(second.Attached, Is.False);
            AssertChange(node, 20, 30);
            var third = new TestExpression(node, property, 40);
            node.SetExpression(property, third);
            node.Changes.Clear();
            node.ClearValue(property);
            Assert.That(third.Attached, Is.False);
            AssertChange(node, 40, 5);
        }

        [Test]
        public void UnsetExpressionBlocksInheritanceAndItsChangesPropagateToDescendants()
        {
            var property = DependencyProperty.Register(Guid.NewGuid().ToString(), typeof(int), typeof(Node),
                new FrameworkPropertyMetadata(5, FrameworkPropertyMetadataOptions.Inherits));
            var root = new Node(new List<string>());
            var child = new Node(new List<string>());
            var grandchild = new Node(new List<string>());
            child.SetParent(root);
            grandchild.SetParent(child);
            root.SetValue(property, 10);
            var expression = new TestExpression(child, property, DependencyProperty.UnsetValue);
            child.Changes.Clear();
            grandchild.Changes.Clear();
            child.SetExpression(property, expression);
            Assert.That(child.GetValue(property), Is.EqualTo(5));
            Assert.That(grandchild.GetValue(property), Is.EqualTo(5));
            AssertChange(child, 10, 5);
            AssertChange(grandchild, 10, 5);
            child.Changes.Clear();
            grandchild.Changes.Clear();
            root.SetValue(property, 20);
            Assert.That(child.Changes, Is.Empty);
            Assert.That(grandchild.Changes, Is.Empty);
            expression.Change(30);
            AssertChange(child, 5, 30);
            AssertChange(grandchild, 5, 30);
            object cached = child.GetValue(property);
            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 10000; i++)
            {
                child.GetValue(property);
                grandchild.GetValue(property);
            }
            long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
            Assert.That(allocated, Is.Zero);
            Assert.That(grandchild.GetValue(property), Is.SameAs(cached));
            child.Changes.Clear();
            grandchild.Changes.Clear();
            child.ClearValue(property);
            AssertChange(child, 30, 20);
            AssertChange(grandchild, 30, 20);
        }

        private static void AssertChange(Node node, object oldValue, object newValue)
        {
            Assert.That(node.Changes.Count, Is.EqualTo(1));
            Assert.That(node.Changes[0].OldValue, Is.EqualTo(oldValue));
            Assert.That(node.Changes[0].NewValue, Is.EqualTo(newValue));
        }

        private sealed class TestExpression : Expression
        {
            private readonly DependencyObject target;
            private readonly DependencyProperty property;
            private object value;
            public bool Attached { get; private set; }

            public TestExpression(DependencyObject target, DependencyProperty property, object value)
            {
                this.target = target;
                this.property = property;
                this.value = value;
            }

            internal override void Attach() => Attached = true;
            internal override void Detach() => Attached = false;
            internal override object GetValue() => value;
            internal override bool TrySetValue(object newValue) => false;
            public void Change(object newValue)
            {
                object oldValue = value;
                value = newValue;
                target.OnExpressionValueChanged(property, this, oldValue);
            }
        }

        private sealed class Node : DependencyObject
        {
            private Node parent;
            private readonly List<Node> children = new List<Node>();
            private readonly List<string> order;
            public List<DependencyPropertyChangedEventArgs> Changes { get; } = new List<DependencyPropertyChangedEventArgs>();
            public Node(List<string> order) { this.order = order; }
            protected internal override DependencyObject InheritanceParent => parent;
            protected internal override int InheritanceChildrenCount => children.Count;
            protected internal override DependencyObject GetInheritanceChild(int index) => children[index];
            public void SetParent(Node newParent)
            {
                Node oldParent = parent;
                oldParent?.children.Remove(this);
                parent = newParent;
                newParent?.children.Add(this);
                OnInheritanceParentChanged(oldParent);
            }
            protected override void OnPropertyChanged(DependencyPropertyChangedEventArgs e)
            {
                order.Add("override");
                Changes.Add(e);
            }
        }
    }
}
