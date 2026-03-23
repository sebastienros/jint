using System.Runtime.CompilerServices;

namespace System.Buffers;

#if !NET8_0_OR_GREATER
internal static class SearchValues
{
    internal static SearchValues<char> Create(string input) => new CharSearchValues(input.AsSpan());
    internal static SearchValues<char> Create(ReadOnlySpan<char> input) => new CharSearchValues(input);
}
#endif

#if !NET8_0_OR_GREATER
internal abstract class SearchValues<T>
{
    public abstract bool Contains(T data);
}

file sealed class CharSearchValues : SearchValues<char>
{
    private readonly bool[] _data;
    private readonly char _min;

    internal CharSearchValues(ReadOnlySpan<char> input)
    {
        _min = char.MaxValue;
        var max = char.MinValue;
        foreach (var c in input)
        {
            _min = (char) Math.Min(_min, c);
            max = (char) Math.Max(max, c);
        }

        _data = new bool[max - _min + 1];
        foreach (var c in input)
        {
            _data[c - _min] = true;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override bool Contains(char c)
    {
        var i = (uint) (c - _min);
        var temp = _data;
        return i < temp.Length && temp[i];
    }
}
#endif

#if !NET9_0_OR_GREATER
internal readonly struct StringSearchValues
{
    private readonly HashSet<string> _data;

    internal StringSearchValues(ReadOnlySpan<string> input, StringComparison comparer)
    {
        if (comparer != StringComparison.Ordinal)
        {
            Jint.Runtime.Throw.ArgumentException("comparer", nameof(comparer));
        }

        _data = new HashSet<string>(StringComparer.Ordinal);
        foreach (var s in input)
        {
            _data.Add(s);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Contains(string s) => _data.Contains(s);
}
#else
internal readonly struct StringSearchValues
{
    private readonly SearchValues<string> _data;

    internal StringSearchValues(ReadOnlySpan<string> input, StringComparison comparer)
    {
        _data = SearchValues.Create(input, comparer);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Contains(string s) => _data.Contains(s);
}
#endif
