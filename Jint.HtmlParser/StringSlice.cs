namespace Jint.HtmlParser;

// Only immutable strings can back a slice; tokenizer buffers and caller arrays
// must be copied before crossing into a token or the mutable DOM.
internal readonly struct StringSlice
{
    private readonly string? _source;
    private readonly int _start;

    internal StringSlice(string source) : this(source, 0, source.Length) { }

    internal StringSlice(string source, int start, int length)
    {
        ArgumentNullException.ThrowIfNull(source);
        _ = source.AsSpan(start, length);
        _source = length == 0 ? null : source;
        _start = length == 0 ? 0 : start;
        Length = length;
    }

    internal int Length { get; }
    internal bool IsEmpty => Length == 0;
    internal ReadOnlySpan<char> Span => _source.AsSpan(_start, Length);

    internal StringSlice Slice(int start, int length)
    {
        _ = Span.Slice(start, length);
        return length == 0 ? default : new StringSlice(_source!, _start + start, length);
    }

    internal bool TryConcat(StringSlice next, out StringSlice result)
    {
        if (IsEmpty) { result = next; return true; }
        if (next.IsEmpty) { result = this; return true; }
        if (ReferenceEquals(_source, next._source) && _start + Length == next._start)
        {
            result = new StringSlice(_source!, _start, Length + next.Length);
            return true;
        }
        result = default;
        return false;
    }

    public override string ToString() => _source is null ? string.Empty :
        _start == 0 && Length == _source.Length ? _source : Span.ToString();

    internal static string Materialize(ref StringSlice slice)
    {
        if (slice._source is null) return string.Empty;
        if (slice._start == 0 && slice.Length == slice._source.Length) return slice._source;
        var value = slice.Span.ToString();
        slice = new StringSlice(value);
        return value;
    }
}
