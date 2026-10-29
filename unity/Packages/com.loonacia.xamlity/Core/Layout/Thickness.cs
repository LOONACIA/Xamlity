using System;
using System.Globalization;

namespace Xamlity.Layout
{
    public readonly struct Thickness : IEquatable<Thickness>
    {
        public Thickness(double uniformLength) : this(uniformLength, uniformLength, uniformLength, uniformLength)
        {
        }

        public Thickness(double left, double top, double right, double bottom)
        {
            Left = left;
            Top = top;
            Right = right;
            Bottom = bottom;
        }

        public double Left { get; }
        public double Top { get; }
        public double Right { get; }
        public double Bottom { get; }

        public bool Equals(Thickness other) => Left.Equals(other.Left) && Top.Equals(other.Top)
            && Right.Equals(other.Right) && Bottom.Equals(other.Bottom);
        public override bool Equals(object obj) => obj is Thickness other && Equals(other);
        public override int GetHashCode() => HashCode.Combine(Left, Top, Right, Bottom);
        public static bool operator ==(Thickness left, Thickness right) => left.Equals(right);
        public static bool operator !=(Thickness left, Thickness right) => !(left == right);
        public override string ToString() => string.Format(CultureInfo.InvariantCulture, "{0},{1},{2},{3}", Left, Top, Right, Bottom);
    }
}
