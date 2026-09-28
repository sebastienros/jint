using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace Jint.HtmlParser;

// A reusable, contiguous accumulation buffer for scanners. Unlike StringBuilder it exposes
// its content as one span, so callers can compare, hash or slice it without chunk walks.
// Capacity only grows when asked, which lets scanners charge the copy to their work quota
// before it happens; Clear keeps the capacity for the next token.
internal sealed class CharBuffer
{
    private const int DefaultCapacity = 16;

    private char[] _chars;

    internal CharBuffer(int capacity = DefaultCapacity) => _chars = new char[capacity];

    internal int Length { get; private set; }

    internal int Capacity => _chars.Length;

    internal ReadOnlySpan<char> Span => _chars.AsSpan(0, Length);

    internal char this[int index]
    {
        get
        {
            Debug.Assert((uint) index < (uint) Length);
            return _chars[index];
        }
    }

    internal void Clear() => Length = 0;

    internal void EnsureCapacity(int capacity)
    {
        if (capacity > _chars.Length) Array.Resize(ref _chars, capacity);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal void Append(char value)
    {
        var chars = _chars;
        var length = Length;
        if ((uint) length < (uint) chars.Length)
        {
            chars[length] = value;
            Length = length + 1;
            return;
        }
        GrowAndAppend(value);
    }

    internal void Append(scoped ReadOnlySpan<char> value)
    {
        EnsureCapacity(checked(Length + value.Length));
        value.CopyTo(_chars.AsSpan(Length));
        Length += value.Length;
    }

    internal void Append(string value) => Append(value.AsSpan());

    [MethodImpl(MethodImplOptions.NoInlining)]
    private void GrowAndAppend(char value)
    {
        EnsureCapacity(Math.Max(Length + 1, (int) Math.Min(Array.MaxLength, (long) _chars.Length * 2)));
        _chars[Length++] = value;
    }

    public override string ToString() => new(Span);

    internal string ToString(int start, int length) => new(Span.Slice(start, length));
}
