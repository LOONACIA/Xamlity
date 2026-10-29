using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using NUnit.Framework;
using Xamlity.Controls;
using Xamlity.Data;
using Xamlity.Layout;
using Xamlity.Properties;
using Xamlity.Rendering;
using Xamlity.Tree;
using Xamlity.Core.Tests.Rendering;

namespace Xamlity.Core.Tests.Data
{
    public class BindingTests
    {
        [Test]
        public void DataContextInheritsAndNotifiesEachAffectedElementWithoutInvalidatingLayoutOrRender()
        {
            var parent = new StackPanel();
            var child = new StackPanel();
            var grandchild = new TextBlock();
            child.Children.Add(grandchild);
            parent.Children.Add(child);
            var changes = new List<(object, object, object)>();
            foreach (FrameworkElement element in new FrameworkElement[] { parent, child, grandchild })
                element.DataContextChanged += (sender, e) =>
                {
                    Assert.That(e.Property, Is.SameAs(FrameworkElement.DataContextProperty));
                    changes.Add((sender, e.OldValue, e.NewValue));
                };
            var root = new UIRoot(parent);
            root.UpdateLayout(new Size(100, 100));
            root.Render(new EmptyDrawingContext());
            var first = new object();
            parent.DataContext = first;
            Assert.That(child.DataContext, Is.SameAs(first));
            Assert.That(grandchild.DataContext, Is.SameAs(first));
            Assert.That(changes, Is.EqualTo(new[] { ((object)parent, (object)null, first),
                ((object)child, (object)null, first), ((object)grandchild, (object)null, first) }));
            Assert.That(parent.IsMeasureValid && child.IsMeasureValid && grandchild.IsMeasureValid, Is.True);
            Assert.That(parent.IsArrangeValid && child.IsArrangeValid && grandchild.IsArrangeValid, Is.True);
            Assert.That(root.IsRenderDirty, Is.False);

            child.DataContext = "local";
            changes.Clear();
            parent.DataContext = "next";
            Assert.That(changes, Is.EqualTo(new[] { ((object)parent, first, (object)"next") }));
            Assert.That(grandchild.DataContext, Is.EqualTo("local"));
            changes.Clear();
            parent.DataContext = "next";
            Assert.That(changes, Is.Empty);
        }

        [Test]
        public void AddingAndRemovingChildrenNotifiesInheritedDataContext()
        {
            var parent = new StackPanel { DataContext = "parent" };
            var child = new StackPanel();
            var grandchild = new TextBlock();
            child.Children.Add(grandchild);
            var changes = new List<(object, object, object)>();
            child.DataContextChanged += (sender, e) => changes.Add((sender, e.OldValue, e.NewValue));
            grandchild.DataContextChanged += (sender, e) => changes.Add((sender, e.OldValue, e.NewValue));
            parent.Children.Add(child);
            parent.Children.Remove(child);
            Assert.That(changes, Is.EqualTo(new[] {
                ((object)child, (object)null, (object)"parent"),
                ((object)grandchild, (object)null, (object)"parent"),
                ((object)child, (object)"parent", (object)null),
                ((object)grandchild, (object)"parent", (object)null) }));
        }

        [TestCase(BindingMode.Default)]
        [TestCase(BindingMode.OneWay)]
        public void NameChangesUpdateTextAndOnlyRelevantNotificationsReadTheGetter(BindingMode mode)
        {
            var vm = new ViewModel { Name = "first" };
            var text = new TextBlock { DataContext = vm };
            var binding = new Binding("Name") { Mode = mode };
            BindingExpression expression = text.SetBinding(TextBlock.TextProperty, binding);
            Assert.That(expression.ParentBinding, Is.SameAs(binding));
            Assert.That(expression.Target, Is.SameAs(text));
            Assert.That(expression.TargetProperty, Is.SameAs(TextBlock.TextProperty));
            Assert.That(text.ReadLocalValue(TextBlock.TextProperty), Is.SameAs(expression));
            Assert.That(text.Text, Is.EqualTo("first"));
            Assert.That(vm.Reads["Name"], Is.EqualTo(1));
            var root = new UIRoot(text);
            root.UpdateLayout(new Size(100, 100));
            root.Render(new EmptyDrawingContext());
            vm.Raise("Other");
            Assert.That(vm.Reads["Name"], Is.EqualTo(1));
            vm.Raise(null);
            vm.Raise("");
            Assert.That(vm.Reads["Name"], Is.EqualTo(3));
            Assert.That(text.IsMeasureValid, Is.True);
            Assert.That(root.IsRenderDirty, Is.False);
            vm.Name = "second";
            vm.Raise("Name");
            Assert.That(text.Text, Is.EqualTo("second"));
            Assert.That(text.IsMeasureValid, Is.False);
            Assert.That(root.IsRenderDirty, Is.True);
        }

        [Test]
        public void ConversionUsesInvariantCultureAndFallsBackToMetadataDefaults()
        {
            CultureInfo previous = CultureInfo.CurrentCulture;
            try
            {
                CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("de-DE");
                var vm = new ViewModel { Value = 42 };
                var text = new TextBlock { DataContext = vm };
                text.SetBinding(TextBlock.TextProperty, "Value");
                text.SetBinding(FrameworkElement.WidthProperty, "Value");
                Assert.That(text.Text, Is.EqualTo("42"));
                Assert.That(text.Width, Is.EqualTo(42.0));
                vm.Value = 1.5;
                vm.Raise("Value");
                Assert.That(text.Text, Is.EqualTo("1.5"));
                vm.Value = "invalid";
                vm.Raise("Value");
                Assert.That(text.Width, Is.NaN);
                vm.Value = null;
                vm.Raise("Value");
                Assert.That(text.Text, Is.Null);
                Assert.That(text.Width, Is.NaN);
                vm.Value = new object();
                vm.Raise("Value");
                Assert.That(text.Text, Is.EqualTo("System.Object"));
                Assert.That(text.Width, Is.NaN);

                var nullable = DependencyProperty.Register(Guid.NewGuid().ToString(), typeof(int?), typeof(TextBlock));
                text.SetBinding(nullable, "Value");
                vm.Value = 7;
                vm.Raise("Value");
                Assert.That(text.GetValue(nullable), Is.EqualTo(7));
                vm.Value = null;
                vm.Raise("Value");
                Assert.That(text.GetValue(nullable), Is.Null);
            }
            finally { CultureInfo.CurrentCulture = previous; }
        }

        [TestCase("invalid")]
        [TestCase("999999999999999999999999999999999")]
        [TestCase(true)]
        public void FailedConvertibleConversionsUseDefault(object value)
        {
            Type type = value is bool ? typeof(DateTime) : typeof(int);
            var property = DependencyProperty.Register(Guid.NewGuid().ToString(), type, typeof(TextBlock));
            var text = new TextBlock { DataContext = new ViewModel { Value = value } };
            Assert.DoesNotThrow(() => text.SetBinding(property, "Value"));
            Assert.That(text.GetValue(property), Is.EqualTo(property.DefaultMetadata.DefaultValue));
        }

        [Test]
        public void NestedChangesOnlyReadFromTheChangedStageAndReleaseOldSubscriptions()
        {
            var stats = new ViewModel { Hp = 10 };
            var player = new ViewModel { Stats = stats };
            var vm = new ViewModel { Player = player };
            var text = new TextBlock { DataContext = vm };
            text.SetBinding(TextBlock.TextProperty, "Player.Stats.Hp");
            Assert.That(text.Text, Is.EqualTo("10"));
            Assert.That(new[] { vm.Subscribers, player.Subscribers, stats.Subscribers }, Is.EqualTo(new[] { 1, 1, 1 }));
            stats.Hp = 11;
            stats.Raise("Hp");
            Assert.That(text.Text, Is.EqualTo("11"));
            Assert.That(vm.Reads["Player"], Is.EqualTo(1));
            Assert.That(player.Reads["Stats"], Is.EqualTo(1));
            var nextStats = new ViewModel { Hp = 20 };
            player.Stats = nextStats;
            player.Raise("Stats");
            Assert.That(text.Text, Is.EqualTo("20"));
            Assert.That(stats.Subscribers, Is.Zero);
            stats.Raise("Hp");
            Assert.That(text.Text, Is.EqualTo("20"));
            player.Stats = null;
            player.Raise("Stats");
            Assert.That(text.Text, Is.Empty);
            Assert.That(nextStats.Subscribers, Is.Zero);
            player.Stats = nextStats;
            player.Raise("Stats");
            Assert.That(text.Text, Is.EqualTo("20"));
            vm.Player = new ViewModel { Stats = new ViewModel { Hp = 30 } };
            vm.Raise("Player");
            Assert.That(text.Text, Is.EqualTo("30"));
            Assert.That(player.Subscribers, Is.Zero);
            Assert.That(nextStats.Subscribers, Is.Zero);
            text.DataContext = null;
            Assert.That(vm.Subscribers, Is.Zero);
            Assert.That(vm.Player.Subscribers, Is.Zero);
            Assert.That(vm.Player.Stats.Subscribers, Is.Zero);
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase(".")]
        public void EmptyPathsBindTheSourceItself(string path)
        {
            var text = new TextBlock { DataContext = 42 };
            text.SetBinding(TextBlock.TextProperty, path);
            Assert.That(text.Text, Is.EqualTo("42"));
            text.DataContext = null;
            Assert.That(text.Text, Is.Empty);
            text.SetBinding(TextBlock.TextProperty, new Binding());
            text.DataContext = "next";
            Assert.That(text.Text, Is.EqualTo("next"));
        }

        [Test]
        public void ReflectionUsesPublicInstanceGettersAndMostDerivedHiddenProperty()
        {
            var text = new TextBlock { DataContext = new DerivedModel() };
            text.SetBinding(TextBlock.TextProperty, "Name");
            Assert.That(text.Text, Is.EqualTo("42"));
            foreach (string path in new[] { "Missing", "Static", "PrivateGetter", "Item", "Throws" })
            {
                Assert.DoesNotThrow(() => text.SetBinding(TextBlock.TextProperty, path));
                Assert.That(text.Text, Is.Empty);
            }
        }

        [Test]
        public void PlainSourcesAreReadOnlyOnAttachmentAndContextReplacement()
        {
            var first = new PlainModel { Name = "first" };
            var text = new TextBlock { DataContext = first };
            text.SetBinding(TextBlock.TextProperty, "Name");
            first.Name = "changed";
            Assert.That(text.Text, Is.EqualTo("first"));
            Assert.That(first.Reads, Is.EqualTo(1));
            var second = new PlainModel { Name = "second" };
            text.DataContext = second;
            Assert.That(text.Text, Is.EqualTo("second"));
            Assert.That(first.Reads, Is.EqualTo(1));
            Assert.That(second.Reads, Is.EqualTo(1));
        }

        [Test]
        public void ReplacingAndClearingContextReleasesPreviousViewModels()
        {
            var first = new ViewModel { Name = "first" };
            var second = new ViewModel { Name = "second" };
            var text = new TextBlock { DataContext = first };
            text.SetBinding(TextBlock.TextProperty, "Name");
            text.DataContext = second;
            Assert.That(first.Subscribers, Is.Zero);
            Assert.That(second.Subscribers, Is.EqualTo(1));
            first.Raise("Name");
            Assert.That(text.Text, Is.EqualTo("second"));
            text.DataContext = null;
            Assert.That(second.Subscribers, Is.Zero);
            Assert.That(text.Text, Is.Empty);
        }

        [Test]
        public void DataContextBindingFollowsParentContextAndReparenting()
        {
            var firstPlayer = new ViewModel { Name = "first" };
            var first = new ViewModel { Player = firstPlayer };
            var parent = new StackPanel { DataContext = first };
            var child = new StackPanel();
            var text = new TextBlock();
            child.Children.Add(text);
            child.SetBinding(FrameworkElement.DataContextProperty, "Player");
            text.SetBinding(TextBlock.TextProperty, "Name");
            Assert.That(child.DataContext, Is.Null);
            var changes = new List<(object, object)>();
            child.DataContextChanged += (sender, e) =>
            {
                Assert.That(sender, Is.SameAs(child));
                changes.Add((e.OldValue, e.NewValue));
            };
            parent.Children.Add(child);
            Assert.That(child.DataContext, Is.SameAs(firstPlayer));
            Assert.That(text.Text, Is.EqualTo("first"));
            var secondPlayer = new ViewModel { Name = "second" };
            var second = new ViewModel { Player = secondPlayer };
            parent.DataContext = second;
            Assert.That(text.Text, Is.EqualTo("second"));
            Assert.That(first.Subscribers, Is.Zero);
            Assert.That(firstPlayer.Subscribers, Is.Zero);
            second.Player = firstPlayer;
            second.Raise("Player");
            Assert.That(text.Text, Is.EqualTo("first"));
            parent.Children.Remove(child);
            Assert.That(child.DataContext, Is.Null);
            Assert.That(text.Text, Is.Empty);
            Assert.That(second.Subscribers, Is.Zero);
            Assert.That(firstPlayer.Subscribers, Is.Zero);
            var nextParent = new StackPanel { DataContext = first };
            nextParent.Children.Add(child);
            Assert.That(text.Text, Is.EqualTo("first"));
            parent.DataContext = null;
            Assert.That(text.Text, Is.EqualTo("first"));
            Assert.That(changes, Is.EqualTo(new[] { ((object)null, (object)firstPlayer),
                ((object)firstPlayer, (object)secondPlayer), ((object)secondPlayer, (object)firstPlayer),
                ((object)firstPlayer, (object)null), ((object)null, (object)firstPlayer) }));
            BindingOperations.ClearBinding(child, FrameworkElement.DataContextProperty);
            Assert.That(child.DataContext, Is.SameAs(first));
            Assert.That(first.Subscribers, Is.EqualTo(1)); // Only the descendant's Name binding remains.
            nextParent.DataContext = null;
            Assert.That(first.Subscribers, Is.Zero);
        }

        [TestCase("SetValue")]
        [TestCase("ClearValue")]
        [TestCase("ClearBinding")]
        [TestCase("SetBinding")]
        public void RemovingOrReplacingBindingsDetachesEverySubscription(string operation)
        {
            var oldChild = new ViewModel { Name = "old" };
            var vm = new ViewModel { Player = oldChild, Name = "new" };
            var text = new TextBlock { DataContext = vm };
            BindingExpression previous = text.SetBinding(TextBlock.TextProperty, "Player.Name");
            switch (operation)
            {
                case "SetValue": text.Text = "local"; break;
                case "ClearValue": text.ClearValue(TextBlock.TextProperty); break;
                case "ClearBinding": BindingOperations.ClearBinding(text, TextBlock.TextProperty); break;
                case "SetBinding": text.SetBinding(TextBlock.TextProperty, "Name"); break;
            }
            string expected = operation == "SetBinding" ? "new" : operation == "SetValue" ? "local" : "";
            Assert.That(text.Text, Is.EqualTo(expected));
            Assert.That(oldChild.Subscribers, Is.Zero);
            Assert.That(vm.Subscribers, Is.EqualTo(operation == "SetBinding" ? 1 : 0));
            BindingExpression current = BindingOperations.GetBindingExpression(text, TextBlock.TextProperty);
            if (operation == "SetBinding")
                Assert.That(current, Is.Not.SameAs(previous));
            else
                Assert.That(current, Is.Null);
            oldChild.Name = "ignored";
            oldChild.Raise("Name");
            vm.Raise("Player");
            Assert.That(text.Text, Is.EqualTo(expected));
            text.DataContext = null;
            Assert.That(vm.Subscribers, Is.Zero);
        }

        [Test]
        public void ClearBindingPreservesOrdinaryLocalValues()
        {
            var text = new TextBlock { Text = "local" };
            BindingOperations.ClearBinding(text, TextBlock.TextProperty);
            Assert.That(text.Text, Is.EqualTo("local"));
        }

        [Test]
        public void InvalidArgumentsAndPublicExpressionAssignmentsAreRejected()
        {
            var text = new TextBlock();
            var binding = new Binding("Name");
            DependencyProperty dp = TextBlock.TextProperty;
            Assert.Throws<ArgumentNullException>(() => BindingOperations.SetBinding(null, dp, binding));
            Assert.Throws<ArgumentNullException>(() => BindingOperations.SetBinding(text, null, binding));
            Assert.Throws<ArgumentNullException>(() => BindingOperations.SetBinding(text, dp, null));
            Assert.Throws<ArgumentNullException>(() => BindingOperations.GetBindingExpression(null, dp));
            Assert.Throws<ArgumentNullException>(() => BindingOperations.GetBindingExpression(text, null));
            Assert.Throws<ArgumentNullException>(() => BindingOperations.ClearBinding(null, dp));
            Assert.Throws<ArgumentNullException>(() => BindingOperations.ClearBinding(text, null));
            var target = new DependencyObject();
            Assert.That(Assert.Throws<InvalidOperationException>(() => BindingOperations.SetBinding(target, dp, binding)).Message,
                Is.EqualTo("Bindings are only supported on FrameworkElement targets."));
            Assert.Throws<InvalidOperationException>(() => BindingOperations.GetBindingExpression(target, dp));
            Assert.Throws<InvalidOperationException>(() => BindingOperations.ClearBinding(target, dp));
            Assert.Throws<ArgumentException>(() => binding.Mode = (BindingMode)999);
            BindingExpression expression = text.SetBinding(dp, binding);
            Assert.Throws<ArgumentException>(() => text.SetValue(FrameworkElement.DataContextProperty, expression));
            Assert.That(text.DataContext, Is.Null);
        }

        [TestCase(null)] [TestCase("")] [TestCase(".")]
        [TestCase("Name")] [TestCase("Player.Stats.Hp")] [TestCase("_a1.이름")]
        public void SupportedPropertyPathsRetainTheirOriginalText(string path)
        {
            Assert.That(new PropertyPath(path).Path, Is.EqualTo(path));
        }

        [TestCase("A[0]")] [TestCase("(Grid.Row)")] [TestCase("/")]
        [TestCase("A..B")] [TestCase(".A")] [TestCase("A.")]
        [TestCase(" ")] [TestCase("A B")] [TestCase("A\tB")]
        [TestCase("1A")] [TestCase("A.1B")] [TestCase("A-B")]
        public void UnsupportedPropertyPathsReportTheRejectedSyntax(string path)
        {
            Assert.That(Assert.Throws<ArgumentException>(() => new PropertyPath(path)).Message,
                Is.EqualTo($"Property path '{path}' is not supported; use dot-separated property names."));
        }

        [Test]
        public void UnchangedBoundTreeLayoutAndRenderingAllocateNothing()
        {
            var vm = new ViewModel { Name = "bound text", Value = 24 };
            var parent = new StackPanel { DataContext = vm };
            var text = new TextBlock();
            parent.Children.Add(text);
            text.SetBinding(TextBlock.TextProperty, "Name");
            parent.SetBinding(TextBlock.FontSizeProperty, "Value");
            var root = new UIRoot(parent, new TextBlockTests.FakeTextMeasurer(), new FontHandle(1));
            var drawing = new EmptyDrawingContext();
            var viewport = new Size(300, 200);
            for (int i = 0; i < 100; i++)
            {
                root.UpdateLayout(viewport);
                root.Render(drawing);
            }
            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 100; i++)
            {
                root.UpdateLayout(viewport);
                root.Render(drawing);
            }
            long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
            Assert.That(allocated, Is.Zero);
            Assert.That(vm.Reads["Name"], Is.EqualTo(1));
            vm.Name = "changed text";
            vm.Raise("Name");
            Assert.That(root.IsRenderDirty, Is.True);
        }

        private sealed class ViewModel : INotifyPropertyChanged
        {
            private PropertyChangedEventHandler changed;
            private string name;
            private object value;
            private ViewModel player;
            private ViewModel stats;
            private int hp;
            public Dictionary<string, int> Reads { get; } = new Dictionary<string, int>();
            public int Subscribers { get; private set; }
            public event PropertyChangedEventHandler PropertyChanged
            {
                add { changed += value; Subscribers++; }
                remove { changed -= value; Subscribers--; }
            }
            public string Name { get { Read("Name"); return name; } set => name = value; }
            public object Value { get { Read("Value"); return value; } set => this.value = value; }
            public ViewModel Player { get { Read("Player"); return player; } set => player = value; }
            public ViewModel Stats { get { Read("Stats"); return stats; } set => stats = value; }
            public int Hp { get { Read("Hp"); return hp; } set => hp = value; }
            public void Raise(string propertyName) => changed?.Invoke(this, new PropertyChangedEventArgs(propertyName));
            private void Read(string propertyName)
            {
                Reads.TryGetValue(propertyName, out int count);
                Reads[propertyName] = count + 1;
            }
        }

        private class PlainModel
        {
            private string name;
            public int Reads { get; private set; }
            public string Name { get { Reads++; return name; } set => name = value; }
        }

        private sealed class DerivedModel : PlainModel
        {
            public new int Name => 42;
            public static string Static => "static";
            public string PrivateGetter { private get; set; }
            public string this[int index] => "index";
            public string Throws => throw new InvalidOperationException("Getter failed.");
        }
    }
}
