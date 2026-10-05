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

    internal string? Source => _source;
    internal int Start => _start;
}

// DOM-only storage: offsets never change during reads. Publish the complete
// string before releasing the input, using only atomic reference writes. Do not
// copy this storage during materialization; use Snapshot to obtain an immutable
// StringSlice. Replacing a value remains a single-writer DOM mutation.
internal struct StringSliceStorage
{
    private volatile string? _source;
    private volatile string? _materialized;
    private readonly int _start;
    internal readonly int Length;

    internal StringSliceStorage(StringSlice slice)
    {
        _source = slice.Source;
        _start = slice.Start;
        Length = slice.Length;
        _materialized = null;
    }

    internal readonly StringSlice Snapshot
    {
        get
        {
            // Read source first: a null source acquires the preceding cache
            // publication. A retained source always uses the original offsets.
            var source = _source;
            return source is not null ? new StringSlice(source, _start, Length) : new StringSlice(_materialized ?? string.Empty);
        }
    }

    internal readonly ReadOnlySpan<char> Span => Snapshot.Span;

    internal string Materialize()
    {
        var cached = _materialized;
        if (cached is not null) return cached;
        var source = _source;
        if (source is null) return _materialized ?? string.Empty;
        var value = _start == 0 && Length == source.Length ? source : source.AsSpan(_start, Length).ToString();
        // All racing readers produce the same string. Publish one winner so
        // subsequent getters preserve cached string identity as well as content.
        var winner = Interlocked.CompareExchange(ref _materialized, value, null) ?? value;
        _source = null;
        return winner;
    }
}
