using Jint.WebApi;

namespace Jint.Browser.Runtime;

/// <summary>Thread-safe storage map: https://html.spec.whatwg.org/multipage/webstorage.html#concept-storage-map.</summary>
internal sealed class BoundedLocalStorageProvider(long maxBytes, StorageQuota? totalQuota) : StorageProvider
{
    private readonly object _gate = new();
    private readonly InMemoryStorageProvider _store = new(maxBytes);
    private readonly StorageQuota.Account? _account = totalQuota is null ? null : new StorageQuota.Account();

    public override IReadOnlyList<string> Keys { get { lock (_gate) return _store.Keys.ToArray(); } }
    public override int Count { get { lock (_gate) return _store.Count; } }
    public override string? GetItem(string key) { lock (_gate) return _store.GetItem(key); }

    public override void SetItem(string key, string value)
    {
        lock (_gate)
        {
            var previous = _store.GetItem(key);
            var bytes = _store.UsedBytes + 2L * (value.Length - (previous?.Length ?? 0))
                + (previous is null ? 2L * key.Length : 0);
            if (bytes > maxBytes)
                throw new StorageQuotaExceededException("The origin's local storage quota has been exceeded.", maxBytes, bytes);
            if (totalQuota is not null && !totalQuota.TryResize(_account!, bytes, out var requested))
                throw new StorageQuotaExceededException("The context's storage quota has been exceeded.", totalQuota.MaxBytes, requested);
            _store.SetItem(key, value);
        }
    }

    public override void RemoveItem(string key)
    {
        lock (_gate)
        {
            _store.RemoveItem(key);
            totalQuota?.TryResize(_account!, _store.UsedBytes, out _);
        }
    }

    public override void Clear()
    {
        lock (_gate)
        {
            _store.Clear();
            totalQuota?.TryResize(_account!, 0, out _);
        }
    }
}
