using Jint.WebApi.Fetch;

namespace Jint.Browser;

/// <summary>
/// What one <see cref="BrowserContext"/> keeps to itself: its cookies and the name its storage is partitioned
/// under.
/// </summary>
/// <remarks>
/// A context is the unit of isolation a browser profile is — two contexts of the same browser share the
/// <see cref="BrowserOptions"/> and share nothing else. Neither member is consulted in this version, because
/// nothing in a page reaches the network or persists storage yet; they exist so that the seat is named and so
/// that a host writing against the API today does not have to move when they are.
/// </remarks>
public sealed class BrowserContextOptions
{
    /// <summary>Where this context's cookies live. Not yet consulted.</summary>
    /// <remarks>
    /// A page performs no document, subresource or <c>XMLHttpRequest</c> fetch in this version, so nothing
    /// reads or writes a cookie; the jar is carried so that a context created now keeps its identity when
    /// navigation arrives.
    /// </remarks>
    public CookieJar? CookieJar { get; set; }

    /// <summary>The name this context's <c>localStorage</c> and <c>sessionStorage</c> partition under.</summary>
    /// <remarks>
    /// Not yet consulted: storage is per engine in this version, so it is per page and it does not survive a
    /// navigation. Partitioning it by context and origin is the storage change.
    /// </remarks>
    public string? StoragePartition { get; set; }
}
