using System;
using Jint.Runtime;

namespace Jint
{
    /// <summary>
    /// Represents identifier that Jint uses with pre-calculated hash code
    /// as runtime is calculation-heavy.
    /// </summary>
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
        public readonly int HashCode;

        public void Deconstruct(out string name, out int hashCode)
        {
            name = Name;
            hashCode = HashCode;
        }

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
            return a.Name.Equals(b);
        }

        public static bool operator !=(in Identifier a, string b)
        {
            return !a.Name.Equals(b);
        }

        public bool Equals(Identifier other)
        {
            var (name, hashCode) = other;
            return HashCode == hashCode && Name == name;
        }

        public override bool Equals(object obj)
        {
            return obj is Identifier other && Equals(other);
        }

        public override int GetHashCode() => HashCode;

        public override string ToString() => Name;
    }
}