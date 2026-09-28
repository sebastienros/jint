using System.IO.Hashing;
using System.Runtime.InteropServices;
using System.Text;

namespace Jint.HtmlParser.Html;

internal sealed partial class HtmlTokenizer
{
    private const int MaxCachedNameLength = 64;

    private StringSlice _textSource;
    private StringSlice _valueSource;
    private string?[]? _names;
    private int TextLength => _textSource.Length + _text.Length;

    // Bounded, parse-local names: look up before allocating, without retaining
    // arbitrary input or interning attacker-controlled names process-wide.
    private string MaterializeName(StringBuilder buffer)
    {
        const int Slots = 128;
        if (buffer.Length > MaxCachedNameLength) return Materialize(buffer);
        Poll();
        // A cleared builder keeps one chunk, so the name is normally read in place.
        scoped ReadOnlySpan<char> name = default;
        var chunks = 0;
        foreach (var chunk in buffer.GetChunks())
        {
            if (++chunks > 1) break;
            name = chunk.Span;
        }
        if (chunks != 1)
        {
            Span<char> scratch = stackalloc char[MaxCachedNameLength];
            buffer.CopyTo(0, scratch, buffer.Length);
            name = scratch[..buffer.Length];
        }
        ChargeCopy(name.Length);
        if (HtmlKnownNames.Match(name) is { } knownName) return knownName;
        var slot = (int) (XxHash3.HashToUInt64(MemoryMarshal.AsBytes(name)) & (Slots - 1));
        ChargeCopy(name.Length);
        _names ??= new string[Slots];
        for (var probe = 0; probe < 4; probe++)
        {
            var candidate = _names[slot];
            if (candidate is null) break;
            ChargeCopy(Math.Min(candidate.Length, name.Length));
            if (name.SequenceEqual(candidate))
            {
                Poll();
                return candidate;
            }
            if (probe < 3) slot = (slot + 1) & (Slots - 1);
        }
        return _names[slot] = Materialize(buffer);
    }

    private StringSlice TakeValue(StringBuilder buffer)
    {
        var source = buffer == _text ? _textSource : _valueSource;
        var result = source.IsEmpty ? new StringSlice(Materialize(buffer)) : source;
        buffer.Clear();
        if (buffer == _text) _textSource = default;
        else _valueSource = default;
        return result;
    }

    private void CopySourceToBuffer(StringBuilder buffer)
    {
        var source = buffer == _text ? _textSource : buffer == _value ? _valueSource : default;
        if (source.IsEmpty) return;
        EnsureAppendCapacity(buffer, source.Length);
        Poll();
        buffer.Append(source.Span);
        ChargeCopy(source.Length);
        if (buffer == _text) _textSource = default;
        else _valueSource = default;
        Poll();
    }

    private void AppendSource(StringBuilder buffer, StringSlice source)
    {
        if (buffer == _text || buffer == _value)
        {
            ref var current = ref (buffer == _text ? ref _textSource : ref _valueSource);
            if (buffer.Length == 0 && current.TryConcat(source, out var combined))
            {
                current = combined;
                return;
            }
            CopySourceToBuffer(buffer);
        }
        EnsureAppendCapacity(buffer, source.Length);
        buffer.Append(source.Span);
    }
}
