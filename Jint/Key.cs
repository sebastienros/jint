using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using Jint.Runtime;

namespace Jint
{
    /// <summary>
    /// Represents a key that Jint uses with pre-calculated hash code
    /// as runtime does a lot of repetitive dictionary lookups.
    /// </summary>
    [DebuggerDisplay("{" + nameof(Name) + "}")]
    internal readonly struct Key : IEquatable<Key>
    {
        public Key(string name) : this(name, symbolIdentity: 0)
        {
        }

        internal Key(string name, int symbolIdentity)
        {
            if (name == null)
            {
                ExceptionHelper.ThrowArgumentException("name cannot be null");
            }
            Name = name;
            HashCode = name.GetHashCode() * 397 ^ symbolIdentity.GetHashCode();
            _symbolIdentity = symbolIdentity;
        }

        public readonly string Name;
        internal readonly int HashCode;
        internal readonly int _symbolIdentity;

        public static implicit operator Key(string name)
        {
            return new Key(name);
        }

        public static implicit operator string(Key key) => key.Name;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator ==(in Key a, Key b)
        {
            return a.Name == b.Name && a._symbolIdentity == b._symbolIdentity;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator !=(in Key a, Key b)
        {
            return a.Name != b.Name || a._symbolIdentity != b._symbolIdentity;
        }

        public static bool operator ==(in Key a, string b)
        {
            return a.Name == b && a._symbolIdentity == 0;
        }

        public static bool operator !=(in Key a, string b)
        {
            return a.Name != b || a._symbolIdentity > 0;
        }

        public bool Equals(Key other)
        {
            return Name == other.Name && _symbolIdentity == other._symbolIdentity;
        }

        public override bool Equals(object obj)
        {
            return obj is Key other && Equals(other);
        }

        public override int GetHashCode() => HashCode;

        public override string ToString() => Name;
    }
}