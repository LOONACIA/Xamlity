using System;
using System.Collections.Generic;
using System.Globalization;
using NUnit.Framework;
using Xamlity.Layout;
using Xamlity.Properties;
using Xamlity.Tree;

namespace Xamlity.Core.Tests.Layout
{
    public class GridTests
    {
        private static readonly Size Viewport = new Size(300, 200);
        private static readonly GridLength Star = new GridLength(1, GridUnitType.Star);

        [Test]
        public void MixedDefinitionsProduceExpectedSizesAndOffsets()
        {
            var grid = new Grid();
            AddColumns(grid, new GridLength(100), GridLength.Auto, Star);
            AddRows(grid, GridLength.Auto, Star, new GridLength(2, GridUnitType.Star));
            TestElement a = AddChild(grid, 40, 20, 0, 1);
            TestElement b = AddChild(grid, 10, 10, 2, 2);
            TestElement c = AddChild(grid, 30, 30, 1, 0);
            c.HorizontalAlignment = HorizontalAlignment.Left;
            c.VerticalAlignment = VerticalAlignment.Top;
            new UIRoot(grid).UpdateLayout(Viewport);

            AssertColumns(grid, 100, 40, 160);
            AssertRows(grid, 20, 60, 120);
            Assert.That(grid.DesiredSize, Is.EqualTo(new Size(150, 60)));
            Assert.That(a.VisualOffset, Is.EqualTo(new Point(100, 0)));
            Assert.That(b.VisualOffset, Is.EqualTo(new Point(140, 80)));
            Assert.That(b.RenderSize, Is.EqualTo(new Size(160, 120)));
            Assert.That(c.VisualOffset, Is.EqualTo(new Point(0, 20)));
            Assert.That(c.RenderSize, Is.EqualTo(new Size(30, 30)));
        }

        [TestCase(true)]
        [TestCase(false)]
        public void InfiniteStackPanelConstraintMeasuresStarsAsAuto(bool horizontal)
        {
            var panel = new StackPanel { Orientation = horizontal ? Orientation.Horizontal : Orientation.Vertical };
            var grid = new Grid();
            if (horizontal)
                AddColumns(grid, Star, new GridLength(2, GridUnitType.Star));
            else
                AddRows(grid, Star, new GridLength(2, GridUnitType.Star));
            TestElement first = AddChild(grid, horizontal ? 50 : 10, horizontal ? 10 : 50);
            TestElement second = AddChild(grid, horizontal ? 30 : 10, horizontal ? 10 : 30,
                horizontal ? 0 : 1, horizontal ? 1 : 0);
            panel.Children.Add(grid);
            new UIRoot(panel).UpdateLayout(Viewport);
            Assert.That(grid.DesiredSize, Is.EqualTo(horizontal ? new Size(80, 10) : new Size(10, 80)));
            Assert.That(horizontal ? first.MeasureConstraint.Width : first.MeasureConstraint.Height, Is.EqualTo(double.PositiveInfinity));
            Assert.That(horizontal ? second.MeasureConstraint.Width : second.MeasureConstraint.Height, Is.EqualTo(double.PositiveInfinity));
            if (horizontal)
                AssertColumns(grid, 50, 30);
            else
                AssertRows(grid, 50, 30);
        }

        [Test]
        public void NoDefinitionsMeasuresAndArrangesAllChildrenInOneCell()
        {
            var grid = new Grid();
            TestElement first = AddChild(grid, 50, 10, int.MinValue, int.MaxValue);
            TestElement second = AddChild(grid, 30, 40, int.MaxValue, int.MinValue);
            new UIRoot(grid).UpdateLayout(Viewport);
            Assert.That(grid.DesiredSize, Is.EqualTo(new Size(50, 40)));
            foreach (TestElement child in new[] { first, second })
            {
                Assert.That(child.MeasureConstraint, Is.EqualTo(Viewport));
                Assert.That(child.RenderSize, Is.EqualTo(Viewport));
                Assert.That(child.VisualOffset, Is.EqualTo(new Point(0, 0)));
            }
        }

        [TestCase(true)]
        [TestCase(false)]
        public void MissingAxisUsesOneStarAndIndicesAreClamped(bool columns)
        {
            var grid = new Grid();
            if (columns)
                AddColumns(grid, new GridLength(40), Star);
            else
                AddRows(grid, new GridLength(40), Star);
            TestElement first = AddChild(grid, 10, 10, int.MinValue, int.MinValue);
            TestElement last = AddChild(grid, 10, 10, int.MaxValue, int.MaxValue);
            new UIRoot(grid).UpdateLayout(Viewport);
            Assert.That(first.VisualOffset, Is.EqualTo(new Point(0, 0)));
            Assert.That(last.VisualOffset, Is.EqualTo(columns ? new Point(40, 0) : new Point(0, 40)));
            Assert.That(last.RenderSize, Is.EqualTo(columns ? new Size(260, 200) : new Size(300, 160)));
            Assert.That(Grid.GetRow(last), Is.EqualTo(int.MaxValue));
            Assert.That(Grid.GetColumn(first), Is.EqualTo(int.MinValue));
        }

        [Test]
        public void ZeroWeightAndEmptyStarsContributeNoDesiredSize()
        {
            var grid = new Grid();
            AddColumns(grid, new GridLength(0, GridUnitType.Star), Star, new GridLength(2, GridUnitType.Star));
            TestElement child = AddChild(grid, 50, 10);
            new UIRoot(grid).UpdateLayout(Viewport);
            AssertColumns(grid, 0, 100, 200);
            Assert.That(child.MeasureConstraint.Width, Is.Zero);
            Assert.That(grid.DesiredSize, Is.EqualTo(new Size(0, 10)));
        }

        [Test]
        public void ArrangeRedistributesStarsAfterSuccessiveMinimumClamps()
        {
            var grid = new Grid();
            AddColumns(grid, Star, Star, new GridLength(10, GridUnitType.Star));
            AddChild(grid, 70, 10, 0, 0);
            AddChild(grid, 60, 10, 0, 1);
            AddChild(grid, 10, 10, 0, 2);
            grid.Measure(new Size(double.PositiveInfinity, 200));
            grid.Arrange(new Rect(0, 0, 150, 200));
            AssertColumns(grid, 70, 60, 20);
            grid.Arrange(new Rect(0, 0, 240, 200));
            AssertColumns(grid, 70, 60, 110);
        }

        [Test]
        public void LargeFiniteWeightsDoNotOverflowTheirSum()
        {
            var grid = new Grid();
            AddColumns(grid, new GridLength(double.MaxValue, GridUnitType.Star),
                new GridLength(double.MaxValue, GridUnitType.Star));
            TestElement child = AddChild(grid, 10, 10);
            new UIRoot(grid).UpdateLayout(Viewport);
            AssertColumns(grid, 150, 150);
            Assert.That(child.MeasureConstraint.Width, Is.EqualTo(150));
        }

        [Test]
        public void GroupsAreMeasuredInDependencyOrderWithResolvedConstraints()
        {
            var grid = new Grid();
            AddColumns(grid, GridLength.Auto, Star, new GridLength(40));
            AddRows(grid, GridLength.Auto, Star);
            var order = new List<string>();
            TestElement fourth = AddChild(grid, 10, 10, 1, 1);
            TestElement third = AddChild(grid, 10, 20, 0, 1);
            TestElement second = AddChild(grid, 50, 10, 1, 0);
            TestElement first = AddChild(grid, 30, 20, 0, 0);
            foreach (var item in new[] { (first, "1"), (second, "2"), (third, "3"), (fourth, "4") })
            {
                item.Item1.Name = item.Item2;
                item.Item1.MeasureOrder = order;
            }
            new UIRoot(grid).UpdateLayout(Viewport);
            Assert.That(order, Is.EqualTo(new[] { "1", "2", "3", "4" }));
            Assert.That(first.MeasureConstraint, Is.EqualTo(new Size(double.PositiveInfinity, double.PositiveInfinity)));
            Assert.That(second.MeasureConstraint, Is.EqualTo(new Size(double.PositiveInfinity, 180)));
            Assert.That(third.MeasureConstraint, Is.EqualTo(new Size(210, double.PositiveInfinity)));
            Assert.That(fourth.MeasureConstraint, Is.EqualTo(new Size(210, 180)));
        }

        [Test]
        public void AttachedAndDefinitionChangesInvalidateMeasureAndUpdateLayout()
        {
            var grid = new Grid();
            AddColumns(grid, new GridLength(40), Star);
            AddRows(grid, new GridLength(20), Star);
            TestElement child = AddChild(grid, 10, 10);
            var root = new UIRoot(grid);
            root.UpdateLayout(Viewport);
            Grid.SetRow(child, 1);
            Assert.That(grid.IsMeasureValid, Is.False);
            root.UpdateLayout(Viewport);
            Assert.That(child.MeasureCount, Is.EqualTo(2));
            Assert.That(child.VisualOffset.Y, Is.EqualTo(20));
            Grid.SetColumn(child, 1);
            Assert.That(grid.IsMeasureValid, Is.False);
            root.UpdateLayout(Viewport);
            Assert.That(child.VisualOffset.X, Is.EqualTo(40));
            grid.ColumnDefinitions[0].SetValue(ColumnDefinition.WidthProperty, new GridLength(100));
            Assert.That(grid.IsMeasureValid, Is.False);
            root.UpdateLayout(Viewport);
            Assert.That(child.VisualOffset.X, Is.EqualTo(100));
            grid.RowDefinitions[0].Height = new GridLength(50);
            Assert.That(grid.IsMeasureValid, Is.False);
            root.UpdateLayout(Viewport);
            Assert.That(child.VisualOffset.Y, Is.EqualTo(50));
            grid.ColumnDefinitions[0].ClearValue(ColumnDefinition.WidthProperty);
            Assert.That(grid.IsMeasureValid, Is.False);
            root.UpdateLayout(Viewport);
            AssertColumns(grid, 150, 150);
            grid.ColumnDefinitions.Add(new ColumnDefinition());
            Assert.That(grid.IsMeasureValid, Is.False);
            root.UpdateLayout(Viewport);
            AssertColumns(grid, 100, 100, 100);
            grid.ColumnDefinitions.RemoveAt(2);
            Assert.That(grid.IsMeasureValid, Is.False);
            root.UpdateLayout(Viewport);
            AssertColumns(grid, 150, 150);
        }

        [Test]
        public void UnchangedUpdateLayoutDoesNotMeasureOrAllocate()
        {
            var grid = new Grid();
            AddColumns(grid, GridLength.Auto, Star);
            TestElement child = AddChild(grid, 30, 40);
            var root = new UIRoot(grid);
            root.UpdateLayout(Viewport);
            long before = GC.GetAllocatedBytesForCurrentThread();
            root.UpdateLayout(Viewport);
            long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
            Assert.That(allocated, Is.Zero);
            Assert.That(child.MeasureCount, Is.EqualTo(1));
        }

        [Test]
        public void CollectionsValidateOwnershipAndDetachOnReplacementRemovalAndClear()
        {
            var grid = new Grid();
            var other = new Grid();
            CheckCollection(grid.RowDefinitions, other.RowDefinitions, new RowDefinition(), new RowDefinition());
            CheckCollection(grid.ColumnDefinitions, other.ColumnDefinitions, new ColumnDefinition(), new ColumnDefinition());
        }

        private static void CheckCollection<T>(IList<T> collection, IList<T> other, T first, T second)
            where T : DefinitionBase
        {
            Assert.That(collection.IsReadOnly, Is.False);
            Assert.Throws<ArgumentNullException>(() => collection.Add(null));
            collection.Add(first);
            other.Add(second);
            Assert.Throws<InvalidOperationException>(() => collection.Add(first));
            Assert.Throws<InvalidOperationException>(() => other.Add(first));
            Assert.Throws<InvalidOperationException>(() => collection[0] = second);
            Assert.Throws<ArgumentNullException>(() => collection[0] = null);
            Assert.That(collection[0], Is.SameAs(first));
            collection[0] = first;
            Assert.That(collection.Count, Is.EqualTo(1));
            other.Clear();
            Assert.Throws<ArgumentOutOfRangeException>(() => collection.Insert(2, second));
            collection[0] = second;
            other.Add(first);
            Assert.That(collection.Contains(second), Is.True);
            Assert.That(collection.IndexOf(second), Is.Zero);
            var copy = new T[1];
            collection.CopyTo(copy, 0);
            Assert.That(copy[0], Is.SameAs(second));
            Assert.That(collection.Remove(first), Is.False);
            Assert.That(collection.Remove(second), Is.True);
            other.Insert(0, second);
            other.Clear();
            collection.Add(first);
            collection.Add(second);
            Assert.That(collection, Is.EqualTo(new[] { first, second }));
        }

        [Test]
        public void CollectionMutationsInvalidateOnlyTheirCurrentOwner()
        {
            var grid = new Grid();
            var row = new RowDefinition();
            var root = new UIRoot(grid);
            root.UpdateLayout(Viewport);
            grid.RowDefinitions.Add(row);
            Assert.That(grid.IsMeasureValid, Is.False);
            root.UpdateLayout(Viewport);
            grid.RowDefinitions[0] = new RowDefinition();
            Assert.That(grid.IsMeasureValid, Is.False);
            root.UpdateLayout(Viewport);
            row.Height = new GridLength(20);
            Assert.That(grid.IsMeasureValid, Is.True);
            grid.RowDefinitions.Clear();
            Assert.That(grid.IsMeasureValid, Is.False);
            root.UpdateLayout(Viewport);
            Assert.That(grid.DesiredSize, Is.EqualTo(new Size(0, 0)));
        }

        [Test]
        public void DefaultsAndAttachedMetadataMatchLayoutContract()
        {
            Assert.That(new RowDefinition().Height, Is.EqualTo(Star));
            Assert.That(new ColumnDefinition().Width, Is.EqualTo(Star));
            Assert.That(new RowDefinition().ActualHeight, Is.Zero);
            Assert.That(new ColumnDefinition().ActualWidth, Is.Zero);
            var child = new TestElement();
            Assert.That(Grid.GetRow(child), Is.Zero);
            Assert.That(Grid.GetColumn(child), Is.Zero);
            foreach (DependencyProperty property in new[] { Grid.RowProperty, Grid.ColumnProperty })
            {
                Assert.That(property.IsAttached, Is.True);
                Assert.That(((FrameworkPropertyMetadata)property.DefaultMetadata).AffectsMeasure, Is.True);
            }
            Assert.Throws<ArgumentNullException>(() => Grid.GetRow(null));
            Assert.Throws<ArgumentNullException>(() => Grid.SetRow(null, 0));
            Assert.Throws<ArgumentNullException>(() => Grid.GetColumn(null));
            Assert.Throws<ArgumentNullException>(() => Grid.SetColumn(null, 0));
        }

        [TestCase(-1)]
        [TestCase(double.NaN)]
        [TestCase(double.PositiveInfinity)]
        [TestCase(double.NegativeInfinity)]
        public void GridLengthRejectsInvalidValuesForEveryUnit(double value)
        {
            Assert.Throws<ArgumentException>(() => new GridLength(value));
            foreach (GridUnitType unit in Enum.GetValues(typeof(GridUnitType)))
                Assert.Throws<ArgumentException>(() => new GridLength(value, unit));
        }

        [Test]
        public void GridLengthHasValueSemanticsAndInvariantFormatting()
        {
            CultureInfo previous = CultureInfo.CurrentCulture;
            try
            {
                CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("fr-FR");
                Assert.That(new GridLength(100).ToString(), Is.EqualTo("100"));
                Assert.That(new GridLength(2.5, GridUnitType.Star).ToString(), Is.EqualTo("2.5*"));
                Assert.That(Star.ToString(), Is.EqualTo("*"));
                Assert.That(GridLength.Auto.ToString(), Is.EqualTo("Auto"));
                Assert.That(GridLength.Auto.IsAuto, Is.True);
                Assert.That(Star.IsStar, Is.True);
                Assert.That(new GridLength(0).IsAbsolute, Is.True);
                Assert.That(new GridLength(0, GridUnitType.Auto).Value, Is.EqualTo(1));
                Assert.That(default(GridLength), Is.EqualTo(GridLength.Auto));
                Assert.That(GridLength.Auto == new GridLength(20, GridUnitType.Auto), Is.True);
                Assert.That(Star != new GridLength(1), Is.True);
                Assert.That(Star.Equals((object)new GridLength(1, GridUnitType.Star)), Is.True);
                Assert.That(Star.Equals(null), Is.False);
                Assert.That(GridLength.Auto.GetHashCode(), Is.EqualTo(default(GridLength).GetHashCode()));
                Assert.Throws<ArgumentException>(() => new GridLength(1, (GridUnitType)99));
            }
            finally
            {
                CultureInfo.CurrentCulture = previous;
            }
        }

        private static void AddColumns(Grid grid, params GridLength[] lengths)
        {
            foreach (GridLength length in lengths)
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = length });
        }

        private static void AddRows(Grid grid, params GridLength[] lengths)
        {
            foreach (GridLength length in lengths)
                grid.RowDefinitions.Add(new RowDefinition { Height = length });
        }

        private static TestElement AddChild(Grid grid, double width, double height, int row = 0, int column = 0)
        {
            var child = new TestElement { ContentSize = new Size(width, height) };
            Grid.SetRow(child, row);
            Grid.SetColumn(child, column);
            grid.Children.Add(child);
            return child;
        }

        private static void AssertColumns(Grid grid, params double[] widths)
        {
            Assert.That(grid.ColumnDefinitions.Count, Is.EqualTo(widths.Length));
            for (int i = 0; i < widths.Length; i++)
                Assert.That(grid.ColumnDefinitions[i].ActualWidth, Is.EqualTo(widths[i]).Within(1e-9));
        }

        private static void AssertRows(Grid grid, params double[] heights)
        {
            Assert.That(grid.RowDefinitions.Count, Is.EqualTo(heights.Length));
            for (int i = 0; i < heights.Length; i++)
                Assert.That(grid.RowDefinitions[i].ActualHeight, Is.EqualTo(heights[i]).Within(1e-9));
        }

        private sealed class TestElement : FrameworkElement
        {
            public Size ContentSize { get; set; }
            public Size MeasureConstraint { get; private set; }
            public int MeasureCount { get; private set; }
            public List<string> MeasureOrder { get; set; }

            protected override Size MeasureOverride(Size availableSize)
            {
                MeasureCount++;
                MeasureOrder?.Add(Name);
                MeasureConstraint = availableSize;
                return ContentSize;
            }
        }
    }
}
