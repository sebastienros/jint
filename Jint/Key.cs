using System.Diagnostics;
using System.Runtime.CompilerServices;
using Jint.Extensions;

namespace Jint;

/// <summary>
/// Represents a key that Jint uses with pre-calculated hash code
/// as runtime does a lot of repetitive dictionary lookups.
/// </summary>
[DebuggerDisplay("{" + nameof(Name) + "}")]
internal readonly struct Key : IEquatable<Key>
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private Key(string name)
    {
        Name = name;
        HashCode = Hash.GetFNVHashCode(name);
    }

    internal readonly string Name;
    internal readonly int HashCode;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator Key(string name) => new(name);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator string(Key key) => key.Name;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator ==(in Key a, in Key b)
    {
        return a.HashCode == b.HashCode && string.Equals(a.Name, b.Name, StringComparison.Ordinal);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator !=(in Key a, in Key b)
    {
        return a.HashCode != b.HashCode || !string.Equals(a.Name, b.Name, StringComparison.Ordinal);
    }

    public bool Equals(Key other)
    {
        return HashCode == other.HashCode && string.Equals(Name, other.Name, StringComparison.Ordinal);
    }

    public override bool Equals(object? obj)
    {
        return obj is Key other && Equals(other);
    }

    public override int GetHashCode() => HashCode;

    public override string ToString() => Name;
}