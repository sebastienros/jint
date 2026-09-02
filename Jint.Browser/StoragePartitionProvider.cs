using System.Collections.Concurrent;
using Jint.WebApi;

namespace Jint.Browser;

/// <summary>
/// Where one <see cref="BrowserContext"/>'s <c>localStorage</c> lives, partitioned the way a browser
/// partitions it: one store per origin.
/// </summary>
/// <remarks>
/// <para>
/// <b>One provider is one profile.</b> A context asks it for a store the first time a page of that origin
/// loads, and every later page of the same origin in the same context is handed the same one — which is what
/// makes two pages of a site share <c>localStorage</c>, and what makes two contexts not.
/// </para>
/// <para>
/// <b>Called on a page's own thread.</b> A context's pages each have a thread, so a provider serving a
/// context with more than one page open is reached from all of them and must be thread-safe. It is called
/// once per engine — that is, once per navigation — rather than once per <c>localStorage</c> read.
/// </para>
/// <para>
/// <b>An opaque origin never reaches it.</b> <c>about:blank</c>, a <c>data:</c> URL and a document
/// <see cref="Page.SetContentAsync"/> built have no origin to partition by, so a page showing one gets a
/// <c>localStorage</c> that throws <c>SecurityError</c> and this provider is not asked.
/// </para>
/// <para>
/// An abstract class rather than a delegate for the reason <see cref="StorageProvider"/> gives: a later
/// revision can add a member without breaking the hosts that implement it today.
/// </para>
/// </remarks>
public abstract class StoragePartitionProvider
{
    /// <summary>Initializes a new instance of the <see cref="StoragePartitionProvider"/> class.</summary>
    protected StoragePartitionProvider()
    {
    }

    /// <summary>
    /// The <c>localStorage</c> store for <paramref name="origin"/>, or <see langword="null"/> to give that
    /// origin no storage at all.
    /// </summary>
    /// <param name="origin">
    /// The document's origin, serialized — <c>https://example.org</c>, or <c>http://localhost:8080</c>. Never
    /// <c>null</c> and never the string <c>"null"</c>: an opaque origin is refused before this is called.
    /// </param>
    /// <returns>The store, which the same origin must be given again on its next navigation.</returns>
    /// <remarks>
    /// Answering <see langword="null"/> makes <c>localStorage</c> throw <c>SecurityError</c> for that origin,
    /// which is what a browser does for a site the user has denied storage to.
    /// </remarks>
    public abstract StorageProvider? GetLocalStorage(string origin);
}

/// <summary>
/// The default partition: one <see cref="InMemoryStorageProvider"/> per origin, kept for as long as the
/// context is open and never written anywhere.
/// </summary>
/// <remarks>
/// It is what a context with no <see cref="BrowserContextOptions.StoragePartition"/> gets, and it is the
/// shape a host persisting storage should copy: the partitioning is here and the persistence is in the
/// <see cref="StorageProvider"/> it hands back.
/// </remarks>
public sealed class InMemoryStoragePartitionProvider : StoragePartitionProvider
{
    private readonly ConcurrentDictionary<string, StorageProvider> _origins = new(StringComparer.Ordinal);
    private readonly long _maxTotalBytes;

    /// <summary>Creates a partition whose stores each enforce the default five-mebibyte quota.</summary>
    public InMemoryStoragePartitionProvider() : this(Options.StorageOptions.DefaultMaxTotalBytes)
    {
    }

    /// <summary>Creates a partition whose stores each enforce <paramref name="maxTotalBytes"/>.</summary>
    /// <param name="maxTotalBytes">The per-origin quota, in the UTF-16 bytes the store counts.</param>
    public InMemoryStoragePartitionProvider(long maxTotalBytes)
    {
        _maxTotalBytes = maxTotalBytes;
    }

    /// <inheritdoc />
    public override StorageProvider? GetLocalStorage(string origin)
    {
        ArgumentNullException.ThrowIfNull(origin);
        return _origins.GetOrAdd(origin, static (_, max) => new InMemoryStorageProvider(max), _maxTotalBytes);
    }
}
