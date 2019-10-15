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
    public readonly struct Key : IEquatable<Key>
    {
        // lookup for indexer keys
        internal static readonly Key[] indexKeys = new Key[TypeConverter.intToString.Length];

        static Key()
        {
            for (uint i = 0; i < indexKeys.Length; ++i)
            {
                indexKeys[i] = new Key(TypeConverter.ToString(i));
            }
        }

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

        public static implicit operator Key(int value)
        {
            var keys = indexKeys;
            return (uint) value < keys.Length ? keys[value] : BuildKey(value);
        }

        public static implicit operator Key(uint value)
        {
            var keys = indexKeys;
            return value < keys.Length ? keys[value] : BuildKey(value);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static Key BuildKey(long value)
        {
            return new Key(value.ToString());
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