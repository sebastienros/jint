#if NET8_0_OR_GREATER
namespace Jint.WebApi;

/// <summary>
/// The <see cref="StorageProvider"/> an engine gets when the host supplies none: a plain in-memory map that
/// keeps insertion order and enforces a byte budget. Requires .NET 8 or higher.
/// </summary>
/// <remarks>
/// <para>
/// <b>It stores nothing anywhere.</b> The data lives in this object and dies with it, so with the default
/// configuration <c>localStorage</c> and <c>sessionStorage</c> are two separate per-engine dictionaries that
/// do not survive the engine and are not shared with any other engine — the difference the two names carry in
/// a browser is entirely about lifetime, and a host that wants one is expected to supply a provider that has
/// one. Assign the same instance to
/// <see cref="Options.StorageOptions.LocalStorageProvider"/> on an <see cref="Options"/> object shared by
/// several engines to share a store between them.
/// </para>
/// <para>
/// <b>Not thread-safe.</b> Two engines running concurrently over one instance corrupt it. That is the same
/// obligation every provider carries (see <see cref="StorageProvider"/>) and the default arrangement never
/// meets it, because each engine builds its own.
/// </para>
/// <para>
/// <b>The quota.</b> An entry's size is counted as <c>2 × (key.Length + value.Length)</c> — the UTF-16
/// storage the pair occupies, which is the unit browsers express their own ~5&#160;MB limits in. The
/// standard requires only that a value which "cannot be stored" raises a <c>QuotaExceededError</c>; how big
/// the store may get is not specified, so this is a Jint-defined budget rather than a conformance
/// requirement. Overwriting an existing key charges only the difference, so replacing a large value with a
/// small one frees space.
/// </para>
/// </remarks>
public sealed class InMemoryStorageProvider : StorageProvider
{
    private readonly Dictionary<string, string> _entries = new(StringComparer.Ordinal);

    /// <summary>
    /// Insertion order, which is what <see cref="Keys"/> hands back. A list rather than an ordered
    /// dictionary because .NET 8 has none, and because removal — the only O(n) operation this costs — is by
    /// far the rarest thing a storage does.
    /// </summary>
    private readonly List<string> _order = [];

    private readonly long _maxTotalBytes;
    private long _totalBytes;

    /// <summary>
    /// Creates an empty store limited to five mebibytes, the quota browsers converged on.
    /// </summary>
    public InMemoryStorageProvider() : this(Options.StorageOptions.DefaultMaxTotalBytes)
    {
    }

    /// <summary>
    /// Creates an empty store limited to <paramref name="maxTotalBytes"/>.
    /// </summary>
    /// <param name="maxTotalBytes">
    /// The most the whole store may occupy, counted as described on the class. There is no "unlimited"
    /// sentinel: <see cref="long.MaxValue"/> is how that is spelled, and a value of zero or less admits
    /// nothing but empty keys and values.
    /// </param>
    public InMemoryStorageProvider(long maxTotalBytes)
    {
        _maxTotalBytes = maxTotalBytes;
    }

    /// <summary>
    /// How much of the budget the entries currently occupy, in the unit described on the class. A host that
    /// wants to report free space to its scripts can project this itself.
    /// </summary>
    public long UsedBytes => _totalBytes;

    /// <inheritdoc />
    public override IReadOnlyList<string> Keys => _order;

    /// <inheritdoc />
    public override int Count => _order.Count;

    /// <inheritdoc />
    public override string? GetItem(string key) => _entries.TryGetValue(key, out var value) ? value : null;

    /// <inheritdoc />
    public override void SetItem(string key, string value)
    {
        var existed = _entries.TryGetValue(key, out var previous);
        var delta = existed
            ? SizeOf(value) - SizeOf(previous!)
            : SizeOf(key) + SizeOf(value);

        var total = _totalBytes + delta;
        if (total > _maxTotalBytes)
        {
            // The two numbers reach the script as `error.quota` and `error.requested`
            // (https://webidl.spec.whatwg.org/#quotaexceedederror). `total` is the size the store *would*
            // have had, not the increment, which is what makes the pair comparable — and it is greater than
            // the quota by the very test above, so the interface's requested-not-less-than-quota rule holds.
            // A quota of long.MaxValue is the documented way to spell "unlimited"; the branch cannot be
            // reached with one, since nothing can exceed it.
            throw new StorageQuotaExceededException(
                $"Setting the value of '{key}' would take this storage to {total} bytes, over its {_maxTotalBytes}-byte quota.",
                quota: Math.Max(0, _maxTotalBytes),
                requested: total);
        }

        _entries[key] = value;
        if (!existed)
        {
            _order.Add(key);
        }

        _totalBytes = total;
    }

    /// <inheritdoc />
    public override void RemoveItem(string key)
    {
        if (!_entries.TryGetValue(key, out var value))
        {
            return;
        }

        _entries.Remove(key);
        _order.Remove(key);
        _totalBytes -= SizeOf(key) + SizeOf(value);
    }

    /// <inheritdoc />
    public override void Clear()
    {
        _entries.Clear();
        _order.Clear();
        _totalBytes = 0;
    }

    private static long SizeOf(string value) => 2L * value.Length;
}
#endif
