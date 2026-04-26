using System.Collections;
using System.Collections.Immutable;

namespace Jint.SourceGenerators;

internal readonly struct EquatableArray<T> : IEquatable<EquatableArray<T>>, IReadOnlyList<T>
    where T : IEquatable<T>
{
    private readonly ImmutableArray<T> _array;

    public EquatableArray(ImmutableArray<T> array)
    {
        _array = array;
    }

    public static EquatableArray<T> Empty { get; } = new(ImmutableArray<T>.Empty);

    public int Count => _array.IsDefault ? 0 : _array.Length;

    public T this[int index] => _array[index];

    public bool Equals(EquatableArray<T> other)
    {
        if (_array.IsDefault) return other._array.IsDefault;
        if (other._array.IsDefault) return false;
        if (_array.Length != other._array.Length) return false;
        for (var i = 0; i < _array.Length; i++)
        {
            var a = _array[i];
            var b = other._array[i];
            if (a is null) { if (b is not null) return false; continue; }
            if (b is null) return false;
            if (!a.Equals(b)) return false;
        }
        return true;
    }

    public override bool Equals(object? obj) => obj is EquatableArray<T> other && Equals(other);

    public override int GetHashCode()
    {
        if (_array.IsDefault) return 0;
        unchecked
        {
            var hash = 17;
            foreach (var item in _array) hash = hash * 31 + (item is null ? 0 : item.GetHashCode());
            return hash;
        }
    }

    public ImmutableArray<T>.Enumerator GetEnumerator() => _array.GetEnumerator();
    IEnumerator<T> IEnumerable<T>.GetEnumerator() => ((IEnumerable<T>) _array).GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => ((IEnumerable<T>) _array).GetEnumerator();
}

internal static class EquatableArray
{
    public static EquatableArray<T> ToEquatableArray<T>(this IEnumerable<T> source) where T : IEquatable<T>
        => new(source.ToImmutableArray());
}
