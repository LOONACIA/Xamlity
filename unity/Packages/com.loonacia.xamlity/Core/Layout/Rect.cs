using System;
using System.Globalization;

namespace Xamlity.Layout
{
    public readonly struct Rect : IEquatable<Rect>
    {
        public Rect(double x, double y, double width, double height)
        {
            if (width < 0)
                throw new ArgumentException("Width cannot be negative.", nameof(width));
            if (height < 0)
                throw new ArgumentException("Height cannot be negative.", nameof(height));

            X = x;
            Y = y;
            Width = width;
            Height = height;
        }

        public Rect(Point location, Size size) : this(location.X, location.Y, size.Width, size.Height)
        {
        }

        public double X { get; }
        public double Y { get; }
        public double Width { get; }
        public double Height { get; }
        public double Left => X;
        public double Top => Y;
        public double Right => X + Width;
        public double Bottom => Y + Height;
        public Point Location => new Point(X, Y);
        public Size Size => new Size(Width, Height);

        public bool Equals(Rect other) => X.Equals(other.X) && Y.Equals(other.Y)
            && Width.Equals(other.Width) && Height.Equals(other.Height);
        public override bool Equals(object obj) => obj is Rect other && Equals(other);
        public override int GetHashCode() => HashCode.Combine(X, Y, Width, Height);
        public static bool operator ==(Rect left, Rect right) => left.X == right.X && left.Y == right.Y
            && left.Width == right.Width && left.Height == right.Height;
        public static bool operator !=(Rect left, Rect right) => !(left == right);
        public override string ToString() => string.Format(CultureInfo.InvariantCulture, "{0},{1},{2},{3}", X, Y, Width, Height);
    }
}
