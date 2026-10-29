using System;
using System.Globalization;
using NUnit.Framework;
using Xamlity.Layout;

namespace Xamlity.Core.Tests.Layout
{
    public class GeometryTests
    {
        [Test]
        public void ConstructorsPreserveComponents()
        {
            var size = new Size(12.5, 24);
            var point = new Point(-3, 4.5);
            var thickness = new Thickness(-1, 2, 3, 4);

            Assert.That(size.Width, Is.EqualTo(12.5));
            Assert.That(size.Height, Is.EqualTo(24));
            Assert.That(point.X, Is.EqualTo(-3));
            Assert.That(point.Y, Is.EqualTo(4.5));
            Assert.That(thickness.Left, Is.EqualTo(-1));
            Assert.That(thickness.Top, Is.EqualTo(2));
            Assert.That(thickness.Right, Is.EqualTo(3));
            Assert.That(thickness.Bottom, Is.EqualTo(4));
            Assert.That(new Thickness(5), Is.EqualTo(new Thickness(5, 5, 5, 5)));
        }

        [Test]
        public void RectExposesBoundsLocationAndSize()
        {
            var rect = new Rect(new Point(-2, 3), new Size(10, 20));

            Assert.That(rect, Is.EqualTo(new Rect(-2, 3, 10, 20)));
            Assert.That(rect.X, Is.EqualTo(-2));
            Assert.That(rect.Y, Is.EqualTo(3));
            Assert.That(rect.Width, Is.EqualTo(10));
            Assert.That(rect.Height, Is.EqualTo(20));
            Assert.That(rect.Left, Is.EqualTo(-2));
            Assert.That(rect.Top, Is.EqualTo(3));
            Assert.That(rect.Right, Is.EqualTo(8));
            Assert.That(rect.Bottom, Is.EqualTo(23));
            Assert.That(rect.Location, Is.EqualTo(new Point(-2, 3)));
            Assert.That(rect.Size, Is.EqualTo(new Size(10, 20)));
        }

        [TestCase(-1, 0)]
        [TestCase(0, -1)]
        [TestCase(double.NegativeInfinity, 1)]
        [TestCase(1, double.NegativeInfinity)]
        public void NegativeDimensionsAreRejected(double width, double height)
        {
            Assert.Throws<ArgumentException>(() => new Size(width, height));
            Assert.Throws<ArgumentException>(() => new Rect(0, 0, width, height));
        }

        [TestCase(0, 0)]
        [TestCase(double.PositiveInfinity, 1)]
        [TestCase(1, double.PositiveInfinity)]
        [TestCase(double.PositiveInfinity, double.PositiveInfinity)]
        public void ZeroAndInfiniteDimensionsAreAllowed(double width, double height)
        {
            var size = new Size(width, height);
            var rect = new Rect(new Point(0, 0), size);

            Assert.That(size.Width, Is.EqualTo(width));
            Assert.That(size.Height, Is.EqualTo(height));
            Assert.That(rect.Right, Is.EqualTo(width));
            Assert.That(rect.Bottom, Is.EqualTo(height));
        }

        [Test]
        public void DefaultValuesAreZero()
        {
            Assert.That(default(Size), Is.EqualTo(new Size(0, 0)));
            Assert.That(default(Point), Is.EqualTo(new Point(0, 0)));
            Assert.That(default(Rect), Is.EqualTo(new Rect(0, 0, 0, 0)));
            Assert.That(default(Thickness), Is.EqualTo(new Thickness(0)));
        }

        [Test]
        public void EqualityUsesEveryComponent()
        {
            AssertValueEquality(new Size(1, 2), new Size(1, 2), new Size(0, 2), new Size(1, 0));
            AssertValueEquality(new Point(1, 2), new Point(1, 2), new Point(0, 2), new Point(1, 0));
            AssertValueEquality(new Rect(1, 2, 3, 4), new Rect(1, 2, 3, 4),
                new Rect(0, 2, 3, 4), new Rect(1, 0, 3, 4), new Rect(1, 2, 0, 4), new Rect(1, 2, 3, 0));
            AssertValueEquality(new Thickness(1, 2, 3, 4), new Thickness(1, 2, 3, 4),
                new Thickness(0, 2, 3, 4), new Thickness(1, 0, 3, 4), new Thickness(1, 2, 0, 4), new Thickness(1, 2, 3, 0));
        }

        [Test]
        public void EqualityOperatorsCompareValues()
        {
            Assert.That(new Size(1, 2) == new Size(1, 2), Is.True);
            Assert.That(new Size(1, 2) == new Size(2, 1), Is.False);
            Assert.That(new Size(1, 2) != new Size(1, 2), Is.False);
            Assert.That(new Size(1, 2) != new Size(2, 1), Is.True);
            Assert.That(new Point(1, 2) == new Point(1, 2), Is.True);
            Assert.That(new Point(1, 2) == new Point(2, 1), Is.False);
            Assert.That(new Point(1, 2) != new Point(1, 2), Is.False);
            Assert.That(new Point(1, 2) != new Point(2, 1), Is.True);
            Assert.That(new Rect(1, 2, 3, 4) == new Rect(1, 2, 3, 4), Is.True);
            Assert.That(new Rect(1, 2, 3, 4) == new Rect(1, 2, 4, 3), Is.False);
            Assert.That(new Rect(1, 2, 3, 4) != new Rect(1, 2, 3, 4), Is.False);
            Assert.That(new Rect(1, 2, 3, 4) != new Rect(1, 2, 4, 3), Is.True);
            Assert.That(new Thickness(1) == new Thickness(1, 1, 1, 1), Is.True);
            Assert.That(new Thickness(1) == new Thickness(2), Is.False);
            Assert.That(new Thickness(1) != new Thickness(1, 1, 1, 1), Is.False);
            Assert.That(new Thickness(1) != new Thickness(2), Is.True);
        }

        [Test]
        public void ToStringUsesInvariantCulture()
        {
            var previousCulture = CultureInfo.CurrentCulture;
            try
            {
                CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("fr-FR");
                Assert.That(new Size(1.5, 2.5).ToString(), Is.EqualTo("1.5,2.5"));
                Assert.That(new Point(-1.5, 2.5).ToString(), Is.EqualTo("-1.5,2.5"));
                Assert.That(new Rect(1.5, 2.5, 3.5, 4.5).ToString(), Is.EqualTo("1.5,2.5,3.5,4.5"));
                Assert.That(new Thickness(1.5, 2.5, 3.5, 4.5).ToString(), Is.EqualTo("1.5,2.5,3.5,4.5"));
            }
            finally
            {
                CultureInfo.CurrentCulture = previousCulture;
            }
        }

        private static void AssertValueEquality<T>(T value, T equal, params T[] different) where T : struct, IEquatable<T>
        {
            Assert.That(value.Equals(equal), Is.True);
            Assert.That(value.Equals((object)equal), Is.True);
            Assert.That(value.GetHashCode(), Is.EqualTo(equal.GetHashCode()));
            Assert.That(value.Equals(null), Is.False);
            Assert.That(value.Equals("unrelated"), Is.False);
            foreach (var other in different)
            {
                Assert.That(value.Equals(other), Is.False);
                Assert.That(value.Equals((object)other), Is.False);
            }
        }
    }
}
