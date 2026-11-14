#nullable enable
// Multi-string search sets. SearchValues<string> is .NET 9+: net10.0 binds it directly; net8.0 and netstandard2.0 (whose
// Meziantou.Polyfill copy stalls on long inputs) get the prefiltered StringSearchValuesFallback below. Only Create and
// IndexOfAny are used.
// Jint: file-local, not global; a global alias would shadow Jint's own StringSearchValues (Jint/Native/Intl).
#if NET9_0_OR_GREATER
using StringSearchValues = System.Buffers.SearchValues<string>;
#else
using StringSearchValues = Jint.Runtime.RegExp.GregsEx.StringSearchValuesFallback;
#endif

using System.Buffers;

namespace Jint.Runtime.RegExp.GregsEx;

internal static class StringSearch
{
    internal static StringSearchValues Create(ReadOnlySpan<string> values, StringComparison comparison = StringComparison.Ordinal)
#if NET9_0_OR_GREATER
        => SearchValues.Create(values, comparison);
#else
        => new(values.ToArray(), comparison);
#endif
}

#if !NET9_0_OR_GREATER
internal sealed class StringSearchValuesFallback
{
    private readonly string[] _values;
    private readonly StringComparison _comparison;
    private readonly SearchValues<char> _firstChars;
    private readonly bool _hasEmpty;

    internal StringSearchValuesFallback(string[] values, StringComparison comparison)
    {
        _values = values;
        _comparison = comparison;
        var first = new HashSet<char>();
        foreach (var value in values)
        {
            if (value.Length == 0)
            {
                _hasEmpty = true;
            }
            else
            {
                first.Add(value[0]);
            }
        }

        var chars = new char[first.Count];
        first.CopyTo(chars);
        _firstChars = SearchValues.Create(chars);
    }

    internal int IndexOfAny(ReadOnlySpan<char> span)
    {
        if (_hasEmpty)
        {
            return 0;
        }

        // OrdinalIgnoreCase can match through case folding outside the first-character set; it scans every position.
        var prefilter = _comparison == StringComparison.Ordinal;
        for (var offset = 0; offset < span.Length; offset++)
        {
            if (prefilter)
            {
                var rel = span.Slice(offset).IndexOfAny(_firstChars);
                if (rel < 0)
                {
                    return -1;
                }

                offset += rel;
            }

            var rest = span.Slice(offset);
            foreach (var value in _values)
            {
                if (rest.StartsWith(value.AsSpan(), _comparison))
                {
                    return offset;
                }
            }
        }

        return -1;
    }
}

// In the GregsEx namespace so it binds ahead of MemoryExtensions.IndexOfAny<T>(SearchValues<T>).
internal static class StringSearchValuesFallbackExtensions
{
    internal static int IndexOfAny(this ReadOnlySpan<char> span, StringSearchValuesFallback values) => values.IndexOfAny(span);

    internal static int IndexOfAny(this Span<char> span, StringSearchValuesFallback values) => values.IndexOfAny(span);
}
#endif
