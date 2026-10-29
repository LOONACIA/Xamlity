using System;
using System.Globalization;

namespace Xamlity.Layout
{
    public readonly struct CornerRadius : IEquatable<CornerRadius>
    {
        public CornerRadius(double uniformRadius) : this(uniformRadius, uniformRadius, uniformRadius, uniformRadius)
        {
        }

        public CornerRadius(double topLeft, double topRight, double bottomRight, double bottomLeft)
        {
            TopLeft = topLeft;
            TopRight = topRight;
            BottomRight = bottomRight;
            BottomLeft = bottomLeft;
        }

        public double TopLeft { get; }
        public double TopRight { get; }
        public double BottomRight { get; }
        public double BottomLeft { get; }

        public bool Equals(CornerRadius other) => TopLeft.Equals(other.TopLeft) && TopRight.Equals(other.TopRight)
            && BottomRight.Equals(other.BottomRight) && BottomLeft.Equals(other.BottomLeft);
        public override bool Equals(object obj) => obj is CornerRadius other && Equals(other);
        public override int GetHashCode() => HashCode.Combine(TopLeft, TopRight, BottomRight, BottomLeft);
        public static bool operator ==(CornerRadius left, CornerRadius right) => left.Equals(right);
        public static bool operator !=(CornerRadius left, CornerRadius right) => !(left == right);
        public override string ToString() => string.Format(CultureInfo.InvariantCulture, "{0},{1},{2},{3}",
            TopLeft, TopRight, BottomRight, BottomLeft);
    }
}
