#if NET8_0_OR_GREATER
using System.Runtime.InteropServices;

namespace Jint.WebApi;

/// <summary>
/// Where the engine's <c>caches</c> object keeps what a script stored. Requires .NET 8 or higher.
/// </summary>
/// <remarks>
/// <para>
/// Jint implements the <see href="https://w3c.github.io/ServiceWorker/#cache-interface">Service Workers
/// Standard</see>'s object model — the <c>Cache</c> and <c>CacheStorage</c> classes, the request-matching
/// algorithm, the <c>Vary</c> rules, the batch semantics — and delegates the storage itself to a provider.
/// A <c>Request</c>/<c>Response</c> pair is flattened into <see cref="CacheEntry"/>, a plain CLR record with
/// no engine reference in it, so a provider may put it in a dictionary, a file, a database or a distributed
/// cache without knowing anything about JavaScript.
/// </para>
/// <para>
/// <b>Threading.</b> Every member here is called on the engine's own thread, synchronously, from inside the
/// <c>Cache</c> method the script called. Nothing is ever called from a thread pool thread, and no member is
/// called while another one is in progress on the same engine. A provider must not block — the script that
/// called it is suspended — and one that is shared by engines running concurrently must be thread-safe, the
/// same obligation <see cref="ConsoleSink"/> carries. A provider must not touch the <see cref="Engine"/>.
/// </para>
/// <para>
/// <b>Failure.</b> Any exception a provider throws becomes a rejection of the promise the script is holding:
/// <see cref="CacheQuotaExceededException"/> becomes a <c>QuotaExceededError</c>
/// (https://webidl.spec.whatwg.org/#quotaexceedederror), which is what the standard's storage steps raise —
/// carrying whatever <c>quota</c> and <c>requested</c> the exception named — and anything else becomes a
/// <c>TypeError</c> carrying the original
/// exception on the error value for the host to read with <c>JintException.TryGetClrException</c>. The engine
/// never swallows one.
/// </para>
/// <para>
/// The default is <see cref="InMemoryCacheStorageProvider"/>, one per engine, which is why the caches of two
/// engines are unrelated unless a host deliberately hands them the same provider.
/// </para>
/// </remarks>
public abstract class CacheStorageProvider
{
    /// <summary>
    /// The names of the caches that exist, in the order they were first opened.
    /// </summary>
    /// <remarks>
    /// This is what <c>caches.keys()</c> answers and the order <c>caches.match()</c> searches in, so the
    /// creation order is observable and a provider that cannot preserve it changes which response a script
    /// gets. The engine never mutates the returned list and reads it once per call.
    /// </remarks>
    public abstract IReadOnlyList<string> Names { get; }

    /// <summary>
    /// Whether a cache with this name exists — <c>caches.has(name)</c>.
    /// </summary>
    /// <param name="cacheName">The name, compared exactly as the script wrote it.</param>
    public abstract bool Contains(string cacheName);

    /// <summary>
    /// The cache with this name, <b>creating it when it does not exist</b> — <c>caches.open(name)</c>.
    /// </summary>
    /// <remarks>
    /// Never returns <see langword="null"/>. Opening the same name twice may hand back the same
    /// <see cref="CacheStore"/> or two objects over one store; the script sees a new <c>Cache</c> object
    /// either way, as the standard requires.
    /// </remarks>
    /// <param name="cacheName">The name, taken verbatim from the script.</param>
    public abstract CacheStore Open(string cacheName);

    /// <summary>
    /// Forgets the cache with this name — <c>caches.delete(name)</c>. Returns whether it existed.
    /// </summary>
    /// <remarks>
    /// A <see cref="CacheStore"/> a script is still holding a <c>Cache</c> object for goes on working after
    /// this, which is what the standard's own note asks for; it is simply no longer reachable by name.
    /// </remarks>
    /// <param name="cacheName">The name, taken verbatim from the script.</param>
    public abstract bool Delete(string cacheName);
}

/// <summary>
/// One named cache: an ordered list of request/response pairs. Requires .NET 8 or higher.
/// </summary>
/// <remarks>
/// <para>
/// The engine performs every algorithm the standard defines — matching, <c>Vary</c>, the duplicate check —
/// against the snapshot <see cref="Entries"/> returns, and expresses the whole outcome as one
/// <see cref="Write"/>. That is what makes a <c>put</c>, a <c>delete</c> or a whole <c>addAll</c> atomic:
/// there is one call, and a provider that applies it in a transaction gives the script the all-or-nothing
/// behaviour the standard's rollback step describes.
/// </para>
/// <para>
/// <b>The two calls are a pair.</b> A <see cref="Write"/> names entries by their index in the list the
/// immediately preceding <see cref="Entries"/> read returned, and no engine code — and therefore no script —
/// runs between the two, so the list a provider hands out only has to stay stable across that window.
/// </para>
/// </remarks>
public abstract class CacheStore
{
    /// <summary>
    /// The pairs this cache holds, oldest first.
    /// </summary>
    /// <remarks>
    /// The order is observable: it is the order <c>matchAll()</c> and <c>keys()</c> answer in, and therefore
    /// decides which response a bare <c>match()</c> picks. The engine never mutates the returned list.
    /// </remarks>
    public abstract IReadOnlyList<CacheEntry> Entries { get; }

    /// <summary>
    /// Applies one atomic change: removes the named entries, then appends the new ones.
    /// </summary>
    /// <remarks>
    /// Either the whole write happens or none of it does — a provider that cannot honour that turns a failed
    /// <c>addAll</c> into a half-populated cache, which is exactly what the standard's rollback step forbids.
    /// Throw to refuse the write; see <see cref="CacheStorageProvider"/> for what the script then sees.
    /// </remarks>
    /// <param name="write">The removals and additions, both possibly empty.</param>
    public abstract void Write(in CacheWrite write);
}

/// <summary>
/// One atomic change to a <see cref="CacheStore"/>. Requires .NET 8 or higher.
/// </summary>
/// <param name="RemovedIndexes">
/// Ascending indexes into the list the immediately preceding <see cref="CacheStore.Entries"/> read returned.
/// Empty for a write that only adds.
/// </param>
/// <param name="Added">
/// The entries to append, in order, <i>after</i> the removals have been applied. Empty for a write that only
/// removes.
/// </param>
[StructLayout(LayoutKind.Auto)]
public readonly record struct CacheWrite(IReadOnlyList<int> RemovedIndexes, IReadOnlyList<CacheEntry> Added);

/// <summary>
/// One cached request/response pair — https://w3c.github.io/ServiceWorker/#dfn-request-response-list.
/// Requires .NET 8 or higher.
/// </summary>
/// <param name="Request">The request the pair is keyed by.</param>
/// <param name="Response">The response that was stored for it.</param>
public sealed record CacheEntry(CachedRequest Request, CachedResponse Response);

/// <summary>
/// A cached request, flattened out of a <c>Request</c> object. Requires .NET 8 or higher.
/// </summary>
/// <param name="Url">
/// The absolute URL, serialized. Written by the engine as the WHATWG URL serialization <i>including</i> any
/// fragment, because that is what <c>request.url</c> answers; matching excludes the fragment either way. An
/// entry whose URL a provider hands back does not parse is invisible to the script — it can never match and
/// never appears in <c>keys()</c>.
/// </param>
/// <param name="Method">
/// The HTTP method. Always <c>GET</c> for anything the engine stored, because
/// <see href="https://w3c.github.io/ServiceWorker/#dom-cache-put">put</see> refuses every other method; a
/// provider may still hand back another one, and such an entry then matches only a query that passed
/// <c>ignoreMethod</c>.
/// </param>
/// <param name="Headers">
/// The request's headers, in order, with repeated names kept apart. They are what the <c>Vary</c> rules
/// compare, so dropping them changes which entry a query matches.
/// </param>
public sealed record CachedRequest(string Url, string Method, IReadOnlyList<CachedHeader> Headers);

/// <summary>
/// A cached response, flattened out of a <c>Response</c> object. Requires .NET 8 or higher.
/// </summary>
/// <param name="Status">The HTTP status code.</param>
/// <param name="StatusText">The reason phrase, possibly empty.</param>
/// <param name="Headers">
/// The response's headers, in order, with repeated names kept apart — <c>Set-Cookie</c> in particular.
/// A <c>Vary</c> header among them is honoured by the matching algorithm.
/// </param>
/// <param name="Body">
/// The body's bytes, or <see langword="null"/> for the standard's <i>null body</i>, which is not the same as
/// an empty one: a null body can be consumed any number of times and never flips <c>bodyUsed</c>.
/// </param>
/// <param name="Url">
/// What <c>response.url</c> answers — the URL the response came from, or the empty string for one a script
/// built itself.
/// </param>
/// <param name="Redirected">What <c>response.redirected</c> answers.</param>
/// <remarks>
/// The response's <i>type</i> is deliberately not part of the record. It describes how a browser filtered a
/// cross-origin response, and this engine has no origins to filter across, so a response handed back from a
/// cache is a plain one — its status, headers and body are the whole of what a script here can act on.
/// </remarks>
public sealed record CachedResponse(
    int Status,
    string StatusText,
    IReadOnlyList<CachedHeader> Headers,
    ReadOnlyMemory<byte>? Body,
    string Url,
    bool Redirected);

/// <summary>
/// One header of a cached request or response. Requires .NET 8 or higher.
/// </summary>
/// <param name="Name">
/// The header's name. The engine writes it already lowercased, and reads every name back
/// ASCII-case-insensitively, so a provider that round-trips through a store which corrects casing loses
/// nothing.
/// </param>
/// <param name="Value">The header's value, already normalized.</param>
[StructLayout(LayoutKind.Auto)]
public readonly record struct CachedHeader(string Name, string Value);

/// <summary>
/// Thrown by a <see cref="CacheStore"/> or a <see cref="CacheStorageProvider"/> to refuse a write for want of
/// room. Requires .NET 8 or higher.
/// </summary>
/// <remarks>
/// This is the one provider failure with a specified meaning: the engine turns it into the
/// <c>QuotaExceededError</c> the storage steps of
/// <see href="https://w3c.github.io/ServiceWorker/#batch-cache-operations">Batch Cache Operations</see> and
/// <see href="https://w3c.github.io/ServiceWorker/#cache-storage-open">open</see> raise, so a script sees the
/// failure it would see in a browser and can handle it by name. Every other exception becomes a
/// <c>TypeError</c>.
/// <para>
/// Nothing in Jint throws it: the built-in <see cref="InMemoryCacheStorageProvider"/> has no quota at all,
/// and imposing one is a host decision.
/// </para>
/// <para>
/// <see cref="Quota"/> and <see cref="Requested"/> are the two numbers
/// <see href="https://webidl.spec.whatwg.org/#quotaexceedederror">QuotaExceededError</see> carries, and the
/// engine passes whatever this exception holds straight through to the script. They are
/// <see langword="null"/> unless a constructor was given them.
/// </para>
/// </remarks>
public sealed class CacheQuotaExceededException : Exception
{
    /// <summary>
    /// Creates the exception with a default message.
    /// </summary>
    public CacheQuotaExceededException() : this("The cache storage quota has been exceeded.")
    {
    }

    /// <summary>
    /// Creates the exception with a message of the host's own.
    /// </summary>
    /// <param name="message">
    /// What went wrong. It reaches the script as the <c>DOMException</c>'s message, so it must not name
    /// anything the script should not learn.
    /// </param>
    public CacheQuotaExceededException(string message) : base(message)
    {
    }

    /// <summary>
    /// Creates the exception with a message and the failure underneath it.
    /// </summary>
    /// <param name="message">What went wrong; it reaches the script as the <c>DOMException</c>'s message.</param>
    /// <param name="innerException">The underlying failure, which the script never sees.</param>
    public CacheQuotaExceededException(string message, Exception innerException) : base(message, innerException)
    {
    }

    /// <summary>
    /// Creates the exception with the two numbers the script reads back as <c>error.quota</c> and
    /// <c>error.requested</c>.
    /// </summary>
    /// <param name="message">What went wrong; it reaches the script as the error's message.</param>
    /// <param name="quota">How much the cache storage may hold, in whatever unit the provider counts in.</param>
    /// <param name="requested">
    /// How much the refused write would have taken it to, in that same unit — the <i>total</i> rather than the
    /// increment, which is what makes it comparable with <paramref name="quota"/>.
    /// </param>
    /// <param name="innerException">The underlying failure, which the script never sees.</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// A value is negative or not finite, or <paramref name="requested"/> is less than
    /// <paramref name="quota"/> — the three things
    /// <see href="https://webidl.spec.whatwg.org/#quotaexceedederror">QuotaExceededError</see> forbids, checked
    /// here so that a mistake is reported where it was made.
    /// </exception>
    public CacheQuotaExceededException(string message, double quota, double requested, Exception? innerException = null)
        : base(message, innerException)
    {
        QuotaExceededAmounts.Validate(quota, requested);
        Quota = quota;
        Requested = requested;
    }

    /// <summary>
    /// How much the cache storage may hold, or <see langword="null"/> when this exception does not say.
    /// </summary>
    public double? Quota { get; }

    /// <summary>
    /// How much the refused write would have taken it to, or <see langword="null"/> when this exception does
    /// not say.
    /// </summary>
    public double? Requested { get; }
}
#endif
