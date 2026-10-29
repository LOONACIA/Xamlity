using System;
using System.Globalization;

namespace Xamlity.Layout
{
    public readonly struct Size : IEquatable<Size>
    {
        public Size(double width, double height)
        {
            if (width < 0)
                throw new ArgumentException("Width cannot be negative.", nameof(width));
            if (height < 0)
                throw new ArgumentException("Height cannot be negative.", nameof(height));

            Width = width;
            Height = height;
        }

        public double Width { get; }
        public double Height { get; }

        public bool Equals(Size other) => Width.Equals(other.Width) && Height.Equals(other.Height);
        public override bool Equals(object obj) => obj is Size other && Equals(other);
        public override int GetHashCode() => HashCode.Combine(Width, Height);
        public static bool operator ==(Size left, Size right) => left.Width == right.Width && left.Height == right.Height;
        public static bool operator !=(Size left, Size right) => !(left == right);
        public override string ToString() => string.Format(CultureInfo.InvariantCulture, "{0},{1}", Width, Height);
    }
}
