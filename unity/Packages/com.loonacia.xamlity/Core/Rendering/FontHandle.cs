using System;
using System.Runtime.CompilerServices;

namespace Xamlity.Rendering
{
    public readonly struct FontHandle : IEquatable<FontHandle>
    {
        public FontHandle(object source)
        {
            Source = source;
        }

        public object Source { get; }
        public bool IsValid => Source != null;

        public bool Equals(FontHandle other) => ReferenceEquals(Source, other.Source);
        public override bool Equals(object obj) => obj is FontHandle other && Equals(other);
        public override int GetHashCode() => Source == null ? 0 : RuntimeHelpers.GetHashCode(Source);
        public static bool operator ==(FontHandle left, FontHandle right) => left.Equals(right);
        public static bool operator !=(FontHandle left, FontHandle right) => !left.Equals(right);
    }
}
