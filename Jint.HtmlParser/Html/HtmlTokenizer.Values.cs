using System.IO.Hashing;
using System.Runtime.InteropServices;
using System.Text;

namespace Jint.HtmlParser.Html;

internal sealed partial class HtmlTokenizer
{
    private const int MaxCachedNameLength = 64;
    private static readonly (string Source, string?[] ByOffset, (int Start, int Length)[] Ranges) KnownNames = CreateKnownNames();

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
        Span<char> scratch = stackalloc char[MaxCachedNameLength + 2];
        var name = scratch.Slice(1, buffer.Length);
        buffer.CopyTo(0, name, name.Length);
        ChargeCopy(name.Length);
        scratch[0] = scratch[name.Length + 1] = '\0';
        var range = KnownNames.Ranges[name.Length];
        var knownOffset = KnownNames.Source.AsSpan(range.Start, range.Length).IndexOf(scratch[..(name.Length + 2)]);
        if (knownOffset >= 0) return KnownNames.ByOffset[range.Start + knownOffset + 1]!;
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

    private static (string Source, string?[] ByOffset, (int Start, int Length)[] Ranges) CreateKnownNames()
    {
        string[] values =
        [
            "a", "b", "i", "p", "s", "u",
            "br", "em", "h1", "h2", "h3", "h4", "h5", "h6", "hr", "id", "li", "ol", "td", "th", "tr", "ul",
            "alt", "div", "img", "rel", "src",
            "body", "form", "head", "href", "html", "link", "meta", "name", "span", "type",
            "class", "input", "label", "style", "table", "tbody", "tfoot", "thead", "title", "value",
            "button", "option", "script", "select", "strong",
            "content", "section", "template", "textarea"
        ];
        var source = string.Concat("\0", string.Join('\0', values), "\0");
        var names = new string?[source.Length - values[^1].Length];
        var ranges = new (int Start, int Length)[MaxCachedNameLength + 1];
        var start = 1;
        foreach (var name in values)
        {
            names[start] = name;
            ref var range = ref ranges[name.Length];
            if (range.Length == 0) range.Start = start - 1;
            range.Length = start + name.Length - range.Start + 1;
            start += name.Length + 1;
        }
        return (source, names, ranges);
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
