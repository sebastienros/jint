using Jint.WebApi;

namespace Jint.Browser.Runtime;

/// <summary>Context-owned storage for https://w3c.github.io/ServiceWorker/#cache-storage-open and batch-cache-operations.</summary>
internal sealed class BoundedCacheStorageProvider(long maxBytes, StorageQuota? totalQuota = null) : CacheStorageProvider
{
    private readonly object _gate = new();
    private readonly Dictionary<string, Store> _named = new(StringComparer.Ordinal);
    private readonly List<string> _names = [];
    private readonly List<WeakReference<Store>> _live = [];

    public override IReadOnlyList<string> Names
    {
        get
        {
            lock (_gate)
            {
                return _names.ToArray();
            }
        }
    }

    public override bool Contains(string cacheName)
    {
        lock (_gate)
        {
            return _named.ContainsKey(cacheName);
        }
    }

    public override CacheStore Open(string cacheName)
    {
        lock (_gate)
        {
            if (_named.TryGetValue(cacheName, out var existing))
            {
                return existing;
            }

            var size = 64 + SizeOf(cacheName);
            CheckQuota(size);
            var store = new Store(this, size);
            ResizeTotal(store, size);
            _named.Add(cacheName, store);
            _names.Add(cacheName);
            _live.Add(new WeakReference<Store>(store));
            return store;
        }
    }

    public override bool Delete(string cacheName)
    {
        lock (_gate)
        {
            if (!_named.Remove(cacheName))
            {
                return false;
            }

            _names.Remove(cacheName);
            return true;
        }
    }

    private void CheckQuota(long delta)
    {
        var requested = delta;
        for (var i = _live.Count - 1; i >= 0; i--)
        {
            if (_live[i].TryGetTarget(out var store))
            {
                requested = checked(requested + store.Size);
            }
            else
            {
                _live.RemoveAt(i);
            }
        }

        if (requested > maxBytes)
        {
            throw new CacheQuotaExceededException(
                "The origin's Cache Storage quota has been exceeded.", maxBytes, requested);
        }
    }

    private StorageQuota.Account? CreateAccount() => totalQuota is null ? null : new StorageQuota.Account();

    private void ResizeTotal(Store store, long size)
    {
        if (totalQuota is not null && !totalQuota.TryResize(store.Account!, size, out var requested))
            throw new CacheQuotaExceededException("The context's storage quota has been exceeded.", totalQuota.MaxBytes, requested);
    }

    private static long SizeOf(string value) => 2L * value.Length;

    private static long SizeOf(IReadOnlyList<CachedHeader> headers)
    {
        long size = 0;
        foreach (var header in headers)
        {
            size += 32 + SizeOf(header.Name) + SizeOf(header.Value);
        }
        return size;
    }

    private static long SizeOf(CacheEntry entry)
        => 128 + SizeOf(entry.Request.Url) + SizeOf(entry.Request.Method) + SizeOf(entry.Request.Headers)
            + SizeOf(entry.Response.Url) + SizeOf(entry.Response.StatusText) + SizeOf(entry.Response.Headers)
            + (entry.Response.Body?.Length ?? 0);

    // Deleted caches remain usable through existing handles. Weak accounting keeps those bytes charged
    // until their last handle dies, without retaining every cache ever deleted by this context.
    private sealed class Store(BoundedCacheStorageProvider owner, long baseSize) : CacheStore
    {
        private CacheEntry[] _entries = [];
        internal StorageQuota.Account? Account { get; } = owner.CreateAccount();
        internal long Size { get; private set; } = baseSize;

        public override IReadOnlyList<CacheEntry> Entries
        {
            get
            {
                lock (owner._gate)
                {
                    return _entries;
                }
            }
        }

        public override void Write(in CacheWrite write)
        {
            lock (SyncRoot)
            {
                lock (owner._gate)
                {
                    var size = Size;
                    foreach (var index in write.RemovedIndexes)
                    {
                        size -= SizeOf(_entries[index]);
                    }
                    foreach (var entry in write.Added)
                    {
                        size = checked(size + SizeOf(entry));
                    }
                    owner.CheckQuota(size - Size);

                    var result = new List<CacheEntry>(_entries.Length - write.RemovedIndexes.Count + write.Added.Count);
                    var next = 0;
                    for (var i = 0; i < _entries.Length; i++)
                    {
                        if (next < write.RemovedIndexes.Count && write.RemovedIndexes[next] == i)
                        {
                            next++;
                        }
                        else
                        {
                            result.Add(_entries[i]);
                        }
                    }
                    result.AddRange(write.Added);
                    var entries = result.ToArray();
                    owner.ResizeTotal(this, size);
                    _entries = entries;
                    Size = size;
                }
            }
        }
    }
}
