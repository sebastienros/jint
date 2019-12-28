using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using Jint.Native;
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

        public Types Type => _symbolIdentity == 0 ? Types.String : Types.Symbol;

        public bool IsSymbol => _symbolIdentity != 0;

        public static implicit operator Key(string name)
        {
            return new Key(name);
        }

        public static implicit operator Key(JsSymbol name)
        {
            return name.ToPropertyKey();
        }

        public static implicit operator JsValue(in Key key)
        {
            return !key.IsSymbol ? (JsValue) JsString.Create(key.Name) : new JsSymbol(key.Name, key._symbolIdentity);
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

        public static implicit operator Key(ulong value)
        {
            var keys = indexKeys;
            return value < (ulong) keys.Length ? keys[value] : new Key(value.ToString());
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static Key BuildKey(long value)
        {
            return new Key(value.ToString());
        }

        public static implicit operator string(Key key) => key.Name;

        public static bool operator ==(in Key a, Key b)
        {
            return a.HashCode == b.HashCode && a.Name == b.Name && a._symbolIdentity == b._symbolIdentity;
        }

        public static bool operator !=(in Key a, Key b)
        {
            return a.HashCode != b.HashCode || a.Name != b.Name || a._symbolIdentity != b._symbolIdentity;
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