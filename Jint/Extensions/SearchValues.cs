#if !NET8_0_OR_GREATER

using System.Runtime.CompilerServices;

namespace System.Buffers;

internal static class SearchValues
{
    internal static SearchValues<char> Create(string input) => new(input.AsSpan());
    internal static SearchValues<char> Create(ReadOnlySpan<char> input) => new(input);
}

internal sealed class SearchValues<T>
{
    private readonly bool[] _data;
    private readonly char _min;
    private readonly char _max;

    internal SearchValues(ReadOnlySpan<char> input)
    {
        _min = char.MaxValue;
        _max = char.MinValue;
        foreach (var c in input)
        {
            _min = (char) Math.Min(_min, c);
            _max = (char) Math.Max(_max, c);
        }

        _data = new bool[_max - _min + 1];
        foreach (var c in input)
        {
            _data[c - _min] = true;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Contains(char c)
    {
        var i = (uint) (c - _min);
        var temp = _data;
        return i < temp.Length && temp[i];
    }
}
#endif
