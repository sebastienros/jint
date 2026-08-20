#if NET8_0_OR_GREATER
using System.Threading;

namespace Jint.WebApi;

/// <summary>
/// The <see cref="CacheStorageProvider"/> an engine uses when the host names none: the caches live in this
/// object's own memory and die with it. Requires .NET 8 or higher.
/// </summary>
/// <remarks>
/// <para>
/// <b>There is no quota.</b> A script with the Cache API can go on storing until the process runs out of
/// memory, and nothing here will stop it — the same shape of exposure a script that keeps appending to an
/// array already has, except that this store outlives the evaluation that filled it. A host running untrusted
/// script that wants a ceiling implements <see cref="CacheStorageProvider"/> itself and throws
/// <see cref="CacheQuotaExceededException"/>, which the script sees as the <c>QuotaExceededError</c> a browser
/// would raise.
/// </para>
/// <para>
/// One instance is created per engine when <c>Options.WebApi.Cache.Provider</c> is left unset, so two engines
/// never see each other's caches. Assigning one instance to a shared <see cref="Options"/> is how a host
/// deliberately shares them; this class is thread-safe for that, guarding every read and write with a lock.
/// </para>
/// <para>
/// The contents survive <c>Engine.Advanced.RestoreGlobalSnapshot</c>. A restore reverts the engine's global
/// bindings, not host storage — the same answer the module registry gets — so a pooled engine's next cycle
/// finds whatever the previous one cached. A host that wants each cycle to start empty gives the engine a
/// fresh provider, which means a fresh engine or a provider of its own.
/// </para>
/// </remarks>
public sealed class InMemoryCacheStorageProvider : CacheStorageProvider
{
    private readonly Lock _lock = new();

    /// <summary>
    /// The caches, in the order they were first opened — which is the order
    /// <see cref="CacheStorageProvider.Names"/> has to answer in.
    /// </summary>
    private readonly List<string> _names = new();

    private readonly Dictionary<string, InMemoryCacheStore> _stores = new(StringComparer.Ordinal);

    /// <inheritdoc />
    public override IReadOnlyList<string> Names
    {
        get
        {
            lock (_lock)
            {
                return _names.ToArray();
            }
        }
    }

    /// <inheritdoc />
    public override bool Contains(string cacheName)
    {
        lock (_lock)
        {
            return _stores.ContainsKey(cacheName);
        }
    }

    /// <inheritdoc />
    public override CacheStore Open(string cacheName)
    {
        lock (_lock)
        {
            if (_stores.TryGetValue(cacheName, out var existing))
            {
                return existing;
            }

            var store = new InMemoryCacheStore();
            _stores.Add(cacheName, store);
            _names.Add(cacheName);
            return store;
        }
    }

    /// <inheritdoc />
    public override bool Delete(string cacheName)
    {
        lock (_lock)
        {
            if (!_stores.Remove(cacheName))
            {
                return false;
            }

            _names.Remove(cacheName);
            return true;
        }
    }

    /// <summary>
    /// One in-memory cache: a list of entries a write replaces wholesale, which is what makes the write
    /// atomic without a transaction log.
    /// </summary>
    private sealed class InMemoryCacheStore : CacheStore
    {
        private readonly Lock _lock = new();

        /// <summary>
        /// Immutable once published, so <see cref="Entries"/> can hand it out without copying and a write in
        /// progress can never be observed half-applied.
        /// </summary>
        private CacheEntry[] _entries = [];

        public override IReadOnlyList<CacheEntry> Entries
        {
            get
            {
                lock (_lock)
                {
                    return _entries;
                }
            }
        }

        public override void Write(in CacheWrite write)
        {
            var removed = write.RemovedIndexes;
            var added = write.Added;

            lock (_lock)
            {
                var current = _entries;
                var result = new List<CacheEntry>(current.Length - removed.Count + added.Count);

                var next = 0;
                for (var i = 0; i < current.Length; i++)
                {
                    if (next < removed.Count && removed[next] == i)
                    {
                        next++;
                        continue;
                    }

                    result.Add(current[i]);
                }

                for (var i = 0; i < added.Count; i++)
                {
                    result.Add(added[i]);
                }

                _entries = result.ToArray();
            }
        }
    }
}
#endif
