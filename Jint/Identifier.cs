using System;
using System.Diagnostics;
using Jint.Runtime;

namespace Jint
{
    /// <summary>
    /// Represents identifier that Jint uses with pre-calculated hash code
    /// as runtime is calculation-heavy.
    /// </summary>
    [DebuggerDisplay("{" + nameof(Name) + "}")]
    public readonly struct Identifier : IEquatable<Identifier>
    {
        public Identifier(string name)
        {
            if (name == null)
            {
                ExceptionHelper.ThrowArgumentException("name cannot be null");
            }
            Name = name;
            HashCode = name.GetHashCode();
        }

        public readonly string Name;
        internal readonly int HashCode;

        public static implicit operator Identifier(string name)
        {
            return new Identifier(name);
        }

        public static implicit operator string(Identifier identifier) => identifier.Name;

        public static bool operator ==(in Identifier a, Identifier b)
        {
            return a.HashCode == b.HashCode && a.Name == b.Name;
        }

        public static bool operator !=(in Identifier a, Identifier b)
        {
            return a.HashCode != b.HashCode || a.Name != b.Name;
        }

        public static bool operator ==(in Identifier a, string b)
        {
            return a.Name == b;
        }

        public static bool operator !=(in Identifier a, string b)
        {
            return a.Name != b;
        }

        public bool Equals(Identifier other)
        {
            return HashCode == other.HashCode && Name == other.Name;
        }

        public override bool Equals(object obj)
        {
            return obj is Identifier other && Equals(other);
        }

        public override int GetHashCode() => HashCode;

        public override string ToString() => Name;
    }
}