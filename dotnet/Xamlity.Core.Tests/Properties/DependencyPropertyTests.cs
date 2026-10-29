using System;
using System.Collections.Generic;
using NUnit.Framework;
using Xamlity.Properties;

namespace Xamlity.Core.Tests.Properties
{
    public class DependencyPropertyTests
    {
        [Test]
        public void RegistrationPreservesIdentityAndMetadata()
        {
            PropertyChangedCallback callback = (d, e) => { };
            var metadata = new PropertyMetadata(12, callback);
            string name = Guid.NewGuid().ToString();
            var property = DependencyProperty.Register(name, typeof(int), typeof(Node), metadata);

            Assert.That(property.Name, Is.EqualTo(name));
            Assert.That(property.PropertyType, Is.EqualTo(typeof(int)));
            Assert.That(property.OwnerType, Is.EqualTo(typeof(Node)));
            Assert.That(property.DefaultMetadata, Is.SameAs(metadata));
            Assert.That(metadata.DefaultValue, Is.EqualTo(12));
            Assert.That(metadata.PropertyChangedCallback, Is.SameAs(callback));
        }

        [TestCase(FrameworkPropertyMetadataOptions.None, false, false, false, false)]
        [TestCase(FrameworkPropertyMetadataOptions.AffectsMeasure, true, false, false, false)]
        [TestCase(FrameworkPropertyMetadataOptions.AffectsArrange, false, true, false, false)]
        [TestCase(FrameworkPropertyMetadataOptions.AffectsRender, false, false, true, false)]
        [TestCase(FrameworkPropertyMetadataOptions.Inherits, false, false, false, true)]
        [TestCase((FrameworkPropertyMetadataOptions)15, true, true, true, true)]
        public void FrameworkMetadataExposesFlags(FrameworkPropertyMetadataOptions flags,
            bool measure, bool arrange, bool render, bool inherits)
        {
            PropertyChangedCallback callback = (d, e) => { };
            var metadata = new FrameworkPropertyMetadata(5, flags, callback);

            Assert.That(metadata.DefaultValue, Is.EqualTo(5));
            Assert.That(metadata.PropertyChangedCallback, Is.SameAs(callback));
            Assert.That(metadata.AffectsMeasure, Is.EqualTo(measure));
            Assert.That(metadata.AffectsArrange, Is.EqualTo(arrange));
            Assert.That(metadata.AffectsRender, Is.EqualTo(render));
            Assert.That(metadata.Inherits, Is.EqualTo(inherits));
        }

        [Test]
        public void MissingMetadataUsesTypeDefault()
        {
            var node = new DependencyObject();
            Assert.That(node.GetValue(Register(typeof(int))), Is.EqualTo(0));
            Assert.That(node.GetValue(Register(typeof(bool))), Is.EqualTo(false));
            Assert.That(node.GetValue(Register(typeof(string))), Is.Null);
            Assert.That(node.GetValue(Register(typeof(int?))), Is.Null);
        }

        [Test]
        public void LocalValuesOverrideDefaultsAndCanBeCleared()
        {
            var property = Register(typeof(int), new PropertyMetadata(5));
            var node = new Node();
            Assert.That(node.GetValue(property), Is.EqualTo(5));
            Assert.That(node.ReadLocalValue(property), Is.SameAs(DependencyProperty.UnsetValue));

            node.SetValue(property, 9);
            Assert.That(node.GetValue(property), Is.EqualTo(9));
            Assert.That(node.ReadLocalValue(property), Is.EqualTo(9));
            AssertChange(node, property, 5, 9);

            node.Changes.Clear();
            node.ClearValue(property);
            Assert.That(node.GetValue(property), Is.EqualTo(5));
            Assert.That(node.ReadLocalValue(property), Is.SameAs(DependencyProperty.UnsetValue));
            AssertChange(node, property, 9, 5);

            node.Changes.Clear();
            node.ClearValue(property);
            Assert.That(node.Changes, Is.Empty);
        }

        [Test]
        public void ReferencePropertiesAcceptNullAndAssignableValues()
        {
            var property = Register(typeof(DependencyObject), new PropertyMetadata(new DependencyObject()));
            var node = new Node();
            var value = new DerivedNode();
            node.SetValue(property, value);
            Assert.That(node.GetValue(property), Is.SameAs(value));

            node.SetValue(property, null);
            Assert.That(node.GetValue(property), Is.Null);
            Assert.That(node.ReadLocalValue(property), Is.Null);
            node.ClearValue(property);
            Assert.That(node.GetValue(property), Is.SameAs(property.DefaultMetadata.DefaultValue));
        }

        [TestCase(typeof(int), "wrong")]
        [TestCase(typeof(int), null)]
        [TestCase(typeof(int), 1.0)]
        [TestCase(typeof(int?), null)]
        [TestCase(typeof(string), 1)]
        public void InvalidLocalValuesAreRejectedWithoutChangingValue(Type propertyType, object value)
        {
            var property = Register(propertyType);
            var node = new Node();
            Assert.Throws<ArgumentException>(() => node.SetValue(property, value));
            Assert.That(node.ReadLocalValue(property), Is.SameAs(DependencyProperty.UnsetValue));
            Assert.That(node.GetValue(property), Is.EqualTo(property.DefaultMetadata.DefaultValue));
            Assert.That(node.Changes, Is.Empty);
        }

        [TestCase(typeof(int), "wrong")]
        [TestCase(typeof(int), null)]
        [TestCase(typeof(string), 1)]
        public void InvalidDefaultValuesAreRejected(Type propertyType, object defaultValue)
        {
            Assert.Throws<ArgumentException>(() => Register(propertyType, new PropertyMetadata(defaultValue)));
        }

        [Test]
        public void DuplicateRegistrationIsRejectedButDifferentOwnersAreAllowed()
        {
            string name = Guid.NewGuid().ToString();
            var property = DependencyProperty.Register(name, typeof(int), typeof(Node));
            Assert.Throws<ArgumentException>(() => DependencyProperty.Register(name, typeof(string), typeof(Node)));
            Assert.That(DependencyProperty.FromName(typeof(Node), name), Is.SameAs(property));
            Assert.DoesNotThrow(() => DependencyProperty.Register(name, typeof(int), typeof(DerivedNode)));
        }

        [Test]
        public void FromNameSearchesBaseTypesAndPrefersTheNearestOwner()
        {
            var property = Register(typeof(int));
            Assert.That(DependencyProperty.FromName(typeof(DerivedNode), property.Name), Is.SameAs(property));
            Assert.That(DependencyProperty.FromName(typeof(DependencyObject), property.Name), Is.Null);
            Assert.That(DependencyProperty.FromName(typeof(Node), "Missing"), Is.Null);

            var derivedProperty = DependencyProperty.Register(property.Name, typeof(string), typeof(DerivedNode));
            Assert.That(DependencyProperty.FromName(typeof(DerivedNode), property.Name), Is.SameAs(derivedProperty));
        }

        [Test]
        public void CallbackPrecedesOnPropertyChangedAndEqualValuesDoNotNotify()
        {
            var node = new Node();
            var callbackChanges = new List<DependencyPropertyChangedEventArgs>();
            var property = Register(typeof(string), new PropertyMetadata("initial", (d, e) =>
            {
                Assert.That(d, Is.SameAs(node));
                Assert.That(d.GetValue(e.Property), Is.SameAs(e.NewValue));
                node.NotificationOrder.Add("callback");
                callbackChanges.Add(e);
            }));

            node.SetValue(property, "changed");
            node.SetValue(property, new string("changed".ToCharArray()));
            AssertChange(node, property, "initial", "changed");
            Assert.That(callbackChanges.Count, Is.EqualTo(1));
            Assert.That(callbackChanges[0].Property, Is.SameAs(property));
            Assert.That(callbackChanges[0].OldValue, Is.EqualTo("initial"));
            Assert.That(callbackChanges[0].NewValue, Is.EqualTo("changed"));
            Assert.That(node.NotificationOrder, Is.EqualTo(new[] { "callback", "override" }));
        }

        [Test]
        public void InheritanceCrossesUnrelatedOwnerTypesAndNotifiesAllDescendants()
        {
            var recipients = new List<DependencyObject>();
            var property = DependencyProperty.Register(Guid.NewGuid().ToString(), typeof(int), typeof(UnrelatedOwner),
                new FrameworkPropertyMetadata(5, FrameworkPropertyMetadataOptions.Inherits, (d, e) => recipients.Add(d)));
            var root = new Node();
            var child = new Node();
            var grandchild = new Node();
            var sibling = new Node();
            child.SetParent(root);
            grandchild.SetParent(child);
            sibling.SetParent(root);

            Assert.That(grandchild.GetValue(property), Is.EqualTo(5));
            root.SetValue(property, 10);
            Assert.That(child.GetValue(property), Is.EqualTo(10));
            Assert.That(grandchild.GetValue(property), Is.EqualTo(10));
            Assert.That(grandchild.ReadLocalValue(property), Is.SameAs(DependencyProperty.UnsetValue));
            AssertChange(root, property, 5, 10);
            AssertChange(child, property, 5, 10);
            AssertChange(grandchild, property, 5, 10);
            AssertChange(sibling, property, 5, 10);
            Assert.That(recipients, Is.EqualTo(new[] { root, child, grandchild, sibling }));
        }

        [Test]
        public void LocalValuesBlockInheritanceForTheWholeSubtreeAndClearRestoresIt()
        {
            var property = Register(typeof(int), new FrameworkPropertyMetadata(5, FrameworkPropertyMetadataOptions.Inherits));
            var root = new Node();
            var child = new Node();
            var grandchild = new Node();
            child.SetParent(root);
            grandchild.SetParent(child);
            child.SetValue(property, 5);
            Assert.That(child.Changes, Is.Empty);
            root.SetValue(property, 10);
            Assert.That(grandchild.GetValue(property), Is.EqualTo(5));
            Assert.That(child.Changes, Is.Empty);
            Assert.That(grandchild.Changes, Is.Empty);

            child.ClearValue(property);
            Assert.That(grandchild.GetValue(property), Is.EqualTo(10));
            AssertChange(child, property, 5, 10);
            AssertChange(grandchild, property, 5, 10);

            child.Changes.Clear();
            grandchild.Changes.Clear();
            root.ClearValue(property);
            AssertChange(child, property, 10, 5);
            AssertChange(grandchild, property, 10, 5);
        }

        [Test]
        public void NullLocalValueBlocksReferenceInheritance()
        {
            var property = Register(typeof(string), new FrameworkPropertyMetadata("default", FrameworkPropertyMetadataOptions.Inherits));
            var root = new Node();
            var child = new Node();
            child.SetParent(root);
            child.SetValue(property, null);
            child.Changes.Clear();

            root.SetValue(property, "parent");
            Assert.That(child.GetValue(property), Is.Null);
            Assert.That(child.Changes, Is.Empty);
            child.ClearValue(property);
            AssertChange(child, property, null, "parent");
        }

        [Test]
        public void ClearingALocalValueEqualToTheInheritedValueDoesNotNotify()
        {
            var property = Register(typeof(int), new FrameworkPropertyMetadata(0, FrameworkPropertyMetadataOptions.Inherits));
            var root = new Node();
            var child = new Node();
            root.SetValue(property, 10);
            child.SetParent(root);
            child.Changes.Clear();
            child.SetValue(property, 10);
            child.ClearValue(property);
            Assert.That(child.Changes, Is.Empty);
            Assert.That(child.ReadLocalValue(property), Is.SameAs(DependencyProperty.UnsetValue));

            root.SetValue(property, 20);
            AssertChange(child, property, 10, 20);
        }

        [Test]
        public void NonInheritedPropertiesDoNotReadOrNotifyThroughParents()
        {
            var property = Register(typeof(int), new PropertyMetadata(5));
            var root = new Node();
            var child = new Node();
            root.SetValue(property, 10);
            child.SetParent(root);
            root.SetValue(property, 20);
            Assert.That(child.GetValue(property), Is.EqualTo(5));
            Assert.That(child.Changes, Is.Empty);
        }

        [Test]
        public void ReparentingComparesEveryInheritedPropertyAndPropagatesChanges()
        {
            var first = Register(typeof(int), new FrameworkPropertyMetadata(0, FrameworkPropertyMetadataOptions.Inherits));
            var second = Register(typeof(string), new FrameworkPropertyMetadata("default", FrameworkPropertyMetadataOptions.Inherits));
            var oldParent = new Node();
            var newParent = new Node();
            oldParent.SetValue(first, 10);
            oldParent.SetValue(second, "old");
            newParent.SetValue(first, 20);
            newParent.SetValue(second, "new");
            var child = new Node();
            var grandchild = new Node();
            grandchild.SetParent(child);

            child.SetParent(oldParent);
            Assert.That(child.Changes.Count, Is.EqualTo(2));
            Assert.That(grandchild.Changes.Count, Is.EqualTo(2));
            Assert.That(child.Changes[0].OldValue, Is.EqualTo(0));
            Assert.That(child.Changes[0].NewValue, Is.EqualTo(10));
            child.Changes.Clear();
            grandchild.Changes.Clear();

            child.SetParent(newParent);
            foreach (var node in new[] { child, grandchild })
            {
                Assert.That(node.GetValue(first), Is.EqualTo(20));
                Assert.That(node.GetValue(second), Is.EqualTo("new"));
                Assert.That(node.Changes.Count, Is.EqualTo(2));
                Assert.That(node.Changes[0].Property, Is.SameAs(first));
                Assert.That(node.Changes[0].OldValue, Is.EqualTo(10));
                Assert.That(node.Changes[0].NewValue, Is.EqualTo(20));
                Assert.That(node.Changes[1].Property, Is.SameAs(second));
                Assert.That(node.Changes[1].OldValue, Is.EqualTo("old"));
                Assert.That(node.Changes[1].NewValue, Is.EqualTo("new"));
                node.Changes.Clear();
            }

            child.SetParent(null);
            Assert.That(child.GetValue(first), Is.EqualTo(0));
            Assert.That(grandchild.GetValue(second), Is.EqualTo("default"));
            Assert.That(child.Changes[0].OldValue, Is.EqualTo(20));
            Assert.That(child.Changes[0].NewValue, Is.EqualTo(0));
            Assert.That(grandchild.Changes.Count, Is.EqualTo(2));
        }

        [Test]
        public void ReparentingSkipsEqualValuesAndRespectsLocalOverrides()
        {
            var property = Register(typeof(int), new FrameworkPropertyMetadata(0, FrameworkPropertyMetadataOptions.Inherits));
            var oldParent = new Node();
            var newParent = new Node();
            oldParent.SetValue(property, 10);
            newParent.SetValue(property, 10);
            var child = new Node();
            var grandchild = new Node();
            child.SetParent(oldParent);
            grandchild.SetParent(child);
            child.Changes.Clear();
            grandchild.Changes.Clear();

            child.SetParent(newParent);
            Assert.That(child.Changes, Is.Empty);
            Assert.That(grandchild.Changes, Is.Empty);

            grandchild.SetValue(property, 30);
            grandchild.Changes.Clear();
            child.SetParent(null);
            AssertChange(child, property, 10, 0);
            Assert.That(grandchild.Changes, Is.Empty);
            Assert.That(grandchild.GetValue(property), Is.EqualTo(30));

            child.SetValue(property, 40);
            child.Changes.Clear();
            child.SetParent(oldParent);
            Assert.That(child.GetValue(property), Is.EqualTo(40));
            Assert.That(child.Changes, Is.Empty);
            Assert.That(grandchild.Changes, Is.Empty);
        }

        [Test]
        public void GetValueReturnsStoredBoxesWithoutAllocating()
        {
            var property = Register(typeof(int), new FrameworkPropertyMetadata(5, FrameworkPropertyMetadataOptions.Inherits));
            var root = new Node();
            var child = new Node();
            var defaults = new DependencyObject();
            child.SetParent(root);
            object boxedValue = 10;
            root.SetValue(property, boxedValue);
            Assert.That(root.GetValue(property), Is.SameAs(boxedValue));
            Assert.That(child.GetValue(property), Is.SameAs(boxedValue));
            Assert.That(defaults.GetValue(property), Is.SameAs(property.DefaultMetadata.DefaultValue));

            for (int i = 0; i < 1000; i++)
            {
                root.GetValue(property);
                child.GetValue(property);
                defaults.GetValue(property);
            }
            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 10000; i++)
            {
                root.GetValue(property);
                child.GetValue(property);
                defaults.GetValue(property);
            }
            long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
            Assert.That(allocated, Is.Zero);
        }

        private static DependencyProperty Register(Type propertyType, PropertyMetadata metadata = null)
        {
            return DependencyProperty.Register(Guid.NewGuid().ToString(), propertyType, typeof(Node), metadata);
        }

        private static void AssertChange(Node node, DependencyProperty property, object oldValue, object newValue)
        {
            Assert.That(node.Changes.Count, Is.EqualTo(1));
            Assert.That(node.Changes[0].Property, Is.SameAs(property));
            Assert.That(node.Changes[0].OldValue, Is.EqualTo(oldValue));
            Assert.That(node.Changes[0].NewValue, Is.EqualTo(newValue));
        }

        private class Node : DependencyObject
        {
            private Node parent;
            private readonly List<Node> children = new List<Node>();

            public List<DependencyPropertyChangedEventArgs> Changes { get; } = new List<DependencyPropertyChangedEventArgs>();
            public List<string> NotificationOrder { get; } = new List<string>();
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
                NotificationOrder.Add("override");
                Changes.Add(e);
            }
        }

        private class DerivedNode : Node
        {
        }

        private class UnrelatedOwner : DependencyObject
        {
        }
    }
}
