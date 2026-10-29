using System;

namespace Xamlity.Rendering
{
    public readonly struct Color : IEquatable<Color>
    {
        private Color(byte a, byte r, byte g, byte b)
        {
            A = a;
            R = r;
            G = g;
            B = b;
        }

        public byte A { get; }
        public byte R { get; }
        public byte G { get; }
        public byte B { get; }

        public static Color FromArgb(byte a, byte r, byte g, byte b) => new Color(a, r, g, b);
        public static Color FromRgb(byte r, byte g, byte b) => new Color(255, r, g, b);
        public bool Equals(Color other) => A == other.A && R == other.R && G == other.G && B == other.B;
        public override bool Equals(object obj) => obj is Color other && Equals(other);
        public override int GetHashCode() => HashCode.Combine(A, R, G, B);
        public static bool operator ==(Color left, Color right) => left.Equals(right);
        public static bool operator !=(Color left, Color right) => !left.Equals(right);
    }
}
