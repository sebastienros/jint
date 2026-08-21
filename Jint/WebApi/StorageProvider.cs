#if NET8_0_OR_GREATER
namespace Jint.WebApi;

/// <summary>
/// The store behind a <c>localStorage</c> or <c>sessionStorage</c> object: the
/// <see href="https://html.spec.whatwg.org/multipage/webstorage.html#concept-storage-map">storage map</see>
/// the Web Storage algorithms operate on. Requires .NET 8 or higher.
/// </summary>
/// <remarks>
/// <para>
/// The HTML Standard describes what a <c>Storage</c> object does to its map and says nothing about where the
/// map lives — in a browser it is a per-origin bottle backed by disk, and nothing about that is derivable
/// from script. So Jint implements the algorithms and leaves the map to the host: implement this class to put
/// storage on a file, a database, a per-tenant cache or a request-scoped dictionary, and
/// <see cref="InMemoryStorageProvider"/> is what an engine gets when the host says nothing.
/// </para>
/// <para>
/// <b>Persistence and sharing are entirely yours.</b> Nothing in Jint writes a provider to disk, and nothing
/// makes two engines share one: whether <c>localStorage</c> survives a process restart, and whether two
/// engines see the same data, is decided by which instance you hand to which
/// <see cref="Options.StorageOptions"/> — a provider assigned to an <see cref="Options"/> instance shared by
/// several engines is shared by all of them. The default gives each engine its own in-memory store, which
/// nothing outlives.
/// </para>
/// <para>
/// <b>Threading.</b> An engine calls its providers only from the thread running that engine, and only inside
/// a script evaluation. A provider handed to engines that run concurrently is called from each of their
/// threads and must then be thread-safe; <see cref="InMemoryStorageProvider"/> deliberately is not.
/// </para>
/// <para>
/// <b>Keys and values are arbitrary UTF-16 strings.</b> They are WebIDL <c>DOMString</c>s, never validated
/// and never normalized: an unpaired surrogate, an embedded NUL and the empty string are all legal, and a
/// provider that cannot store one of those must say so by throwing
/// <see cref="StorageQuotaExceededException"/> rather than by silently dropping or altering it.
/// </para>
/// <para>
/// <b>Exceptions.</b> A <see cref="StorageQuotaExceededException"/> from <see cref="SetItem"/> becomes the
/// <c>QuotaExceededError</c> (https://webidl.spec.whatwg.org/#quotaexceedederror) the script can catch, which
/// is exactly what the specification's "if value cannot be stored, then throw a QuotaExceededError" step asks
/// for — and it carries whatever <c>quota</c> and <c>requested</c> the exception named. Every other
/// exception propagates to the host as itself: an I/O failure in a database-backed provider is a host
/// problem, and turning it into a JavaScript error the script swallows would hide it.
/// </para>
/// <para>
/// An abstract class rather than an interface so that later revisions can add members — a byte-size
/// estimate, an async warm-up — without breaking the hosts that implement it today.
/// </para>
/// </remarks>
public abstract class StorageProvider
{
    /// <summary>
    /// Initializes a new provider.
    /// </summary>
    protected StorageProvider()
    {
    }

    /// <summary>
    /// The store's keys, in the order <c>Storage.key(n)</c>, <c>Object.keys</c> and <c>for..in</c> report
    /// them — the result of
    /// <see href="https://html.spec.whatwg.org/multipage/webstorage.html#dom-storage-key">"get the keys" on
    /// the map</see>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The order is implementation-defined: the standard's "reorder a Storage object" step is explicitly
    /// "in an implementation-defined manner", and notes that iteration order "is not defined and can change
    /// upon most mutations". Keeping insertion order — what <see cref="InMemoryStorageProvider"/> does — is
    /// therefore conforming, and so is any other stable order. What a script may rely on is only that the
    /// order does not change while the store does not.
    /// </para>
    /// <para>
    /// The engine reads the list immediately and never retains it, so returning a live view of the store's
    /// own key list is fine; no script can run between the call and the copy. It must contain no duplicates
    /// and no <see langword="null"/>, and it must agree with <see cref="GetItem"/>: a key it lists must
    /// resolve to a value, and a key it omits must not.
    /// </para>
    /// </remarks>
    public abstract IReadOnlyList<string> Keys { get; }

    /// <summary>
    /// The number of entries — <c>storage.length</c>, which the standard defines as "this's map's size".
    /// </summary>
    /// <remarks>
    /// Defaults to <c>Keys.Count</c>, which is always correct; override it when the store can count more
    /// cheaply than it can list. An override must agree with <see cref="Keys"/>.
    /// </remarks>
    public virtual int Count => Keys.Count;

    /// <summary>
    /// The value stored under <paramref name="key"/>, or <see langword="null"/> when the map has no such
    /// entry — <see href="https://html.spec.whatwg.org/multipage/webstorage.html#dom-storage-getitem">getItem</see>.
    /// </summary>
    /// <remarks>
    /// <see langword="null"/> means <i>absent</i> and nothing else: a stored value is always a string, so
    /// there is no ambiguity, and the distinction is what decides whether the key appears in
    /// <c>Object.keys(storage)</c> and whether <c>'key' in storage</c> is true.
    /// </remarks>
    /// <param name="key">The key to read. Never <see langword="null"/>.</param>
    public abstract string? GetItem(string key);

    /// <summary>
    /// Stores <paramref name="value"/> under <paramref name="key"/>, replacing any existing entry —
    /// <see href="https://html.spec.whatwg.org/multipage/webstorage.html#dom-storage-setitem">setItem</see>.
    /// </summary>
    /// <remarks>
    /// The engine has already applied the specification's step 3: a set whose value equals the stored one is
    /// a no-op and never reaches the provider. A new key goes to the end of <see cref="Keys"/> in the
    /// in-box provider; an existing key keeps its position, which is what "if reorder is true" leaves open
    /// to the implementation.
    /// </remarks>
    /// <param name="key">The key to write. Never <see langword="null"/>.</param>
    /// <param name="value">The value to store. Never <see langword="null"/>.</param>
    /// <exception cref="StorageQuotaExceededException">
    /// The value cannot be stored. The script sees a catchable <c>QuotaExceededError</c>, and the store must
    /// be left exactly as it was.
    /// </exception>
    public abstract void SetItem(string key, string value);

    /// <summary>
    /// Removes the entry under <paramref name="key"/>, doing nothing when there is none —
    /// <see href="https://html.spec.whatwg.org/multipage/webstorage.html#dom-storage-removeitem">removeItem</see>.
    /// </summary>
    /// <param name="key">The key to remove. Never <see langword="null"/>.</param>
    public abstract void RemoveItem(string key);

    /// <summary>
    /// Removes every entry —
    /// <see href="https://html.spec.whatwg.org/multipage/webstorage.html#dom-storage-clear">clear</see>.
    /// </summary>
    public abstract void Clear();
}

/// <summary>
/// Thrown by a <see cref="StorageProvider"/> that cannot store a value, and turned by the engine into the
/// <c>QuotaExceededError</c> the Web Storage standard's <c>setItem</c> raises. Requires .NET 8 or higher.
/// </summary>
/// <remarks>
/// <para>
/// This is the one exception a provider may use to reach the script: its message becomes the error's message,
/// and everything else a provider throws reaches the host unchanged. It is deliberately not a
/// <c>JintException</c> — a host raises it, the engine only translates it.
/// </para>
/// <para>
/// <see cref="Quota"/> and <see cref="Requested"/> are the two numbers
/// <see href="https://webidl.spec.whatwg.org/#quotaexceedederror">QuotaExceededError</see> carries, and the
/// engine passes whatever this exception holds straight through to the script. They are
/// <see langword="null"/> unless a constructor was given them, which is what the interface says an
/// unspecified quota reads as — a provider that has no meaningful figure to report leaves them alone rather
/// than inventing one.
/// </para>
/// </remarks>
public sealed class StorageQuotaExceededException : Exception
{
    /// <summary>
    /// Initializes a new instance with a default message.
    /// </summary>
    public StorageQuotaExceededException()
        : this("The value could not be stored: the storage quota has been exceeded.")
    {
    }

    /// <summary>
    /// Initializes a new instance with the message the script's error will carry.
    /// </summary>
    /// <param name="message">The message, which reaches the script.</param>
    public StorageQuotaExceededException(string message) : base(message)
    {
    }

    /// <summary>
    /// Initializes a new instance with a message and the exception that caused it.
    /// </summary>
    /// <param name="message">The message, which reaches the script.</param>
    /// <param name="innerException">The underlying failure, which does not.</param>
    public StorageQuotaExceededException(string message, Exception innerException) : base(message, innerException)
    {
    }

    /// <summary>
    /// Initializes a new instance that also reports how much room there was and how much the write wanted,
    /// which the script reads back as <c>error.quota</c> and <c>error.requested</c>.
    /// </summary>
    /// <param name="message">The message, which reaches the script.</param>
    /// <param name="quota">How much the store may hold, in whatever unit the provider counts in.</param>
    /// <param name="requested">
    /// How much the refused write would have taken the store to, in that same unit. It is the <i>total</i>
    /// rather than the increment, which is what makes it comparable with <paramref name="quota"/>.
    /// </param>
    /// <param name="innerException">The underlying failure, which the script never sees.</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// A value is negative or not finite, or <paramref name="requested"/> is less than
    /// <paramref name="quota"/> — the three things
    /// <see href="https://webidl.spec.whatwg.org/#quotaexceedederror">QuotaExceededError</see> forbids, checked
    /// here so that a mistake is reported where it was made rather than reaching the script as a nonsensical
    /// pair of numbers.
    /// </exception>
    public StorageQuotaExceededException(string message, double quota, double requested, Exception? innerException = null)
        : base(message, innerException)
    {
        QuotaExceededAmounts.Validate(quota, requested);
        Quota = quota;
        Requested = requested;
    }

    /// <summary>
    /// How much the store may hold, or <see langword="null"/> when this exception does not say.
    /// </summary>
    public double? Quota { get; }

    /// <summary>
    /// How much the refused write would have taken the store to, or <see langword="null"/> when this exception
    /// does not say.
    /// </summary>
    public double? Requested { get; }
}
#endif
