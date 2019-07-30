using System;
using System.Diagnostics;
using Jint.Runtime;

namespace Jint
{
    /// <summary>
    /// Represents a key that Jint uses with pre-calculated hash code
    /// as runtime does a lot of repetitive dictionary lookups.
    /// </summary>
    [DebuggerDisplay("{" + nameof(Name) + "}")]
    public readonly struct Key : IEquatable<Key>
    {
        public Key(string name)
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

        public static implicit operator Key(string name)
        {
            return new Key(name);
        }

        public static implicit operator string(Key key) => key.Name;

        public static bool operator ==(in Key a, Key b)
        {
            return a.HashCode == b.HashCode && a.Name == b.Name;
        }

        public static bool operator !=(in Key a, Key b)
        {
            return a.HashCode != b.HashCode || a.Name != b.Name;
        }

        public static bool operator ==(in Key a, string b)
        {
            return a.Name == b;
        }

        public static bool operator !=(in Key a, string b)
        {
            return a.Name != b;
        }

        public bool Equals(Key other)
        {
            return HashCode == other.HashCode && Name == other.Name;
        }

        public override bool Equals(object obj)
        {
            return obj is Key other && Equals(other);
        }

        public override int GetHashCode() => HashCode;

        public override string ToString() => Name;
    }
}