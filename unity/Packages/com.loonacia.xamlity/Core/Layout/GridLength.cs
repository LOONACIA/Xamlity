using System;
using System.Globalization;

namespace Xamlity.Layout
{
    public enum GridUnitType
    {
        Auto,
        Pixel,
        Star
    }

    public readonly struct GridLength : IEquatable<GridLength>
    {
        private readonly double value;

        public GridLength(double pixels) : this(pixels, GridUnitType.Pixel)
        {
        }

        public GridLength(double value, GridUnitType type)
        {
            if (value < 0 || double.IsNaN(value) || double.IsInfinity(value))
                throw new ArgumentException("Grid length must be finite and nonnegative.", nameof(value));
            if (type != GridUnitType.Auto && type != GridUnitType.Pixel && type != GridUnitType.Star)
                throw new ArgumentException("Unknown grid unit type.", nameof(type));
            this.value = value;
            GridUnitType = type;
        }

        public double Value => IsAuto ? 1 : value;
        public GridUnitType GridUnitType { get; }
        public bool IsAuto => GridUnitType == GridUnitType.Auto;
        public bool IsAbsolute => GridUnitType == GridUnitType.Pixel;
        public bool IsStar => GridUnitType == GridUnitType.Star;
        public static GridLength Auto => new GridLength(1, GridUnitType.Auto);

        public bool Equals(GridLength other) => GridUnitType == other.GridUnitType && Value.Equals(other.Value);
        public override bool Equals(object obj) => obj is GridLength other && Equals(other);
        public override int GetHashCode() => HashCode.Combine(Value, GridUnitType);
        public static bool operator ==(GridLength left, GridLength right) => left.Equals(right);
        public static bool operator !=(GridLength left, GridLength right) => !left.Equals(right);
        public override string ToString() => IsAuto ? "Auto" : IsStar
            ? (Value == 1 ? "*" : Value.ToString(CultureInfo.InvariantCulture) + "*")
            : Value.ToString(CultureInfo.InvariantCulture);
    }
}
