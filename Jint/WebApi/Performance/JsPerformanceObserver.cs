#if NET8_0_OR_GREATER
using System.Runtime.InteropServices;
using Jint.Native;
using Jint.Native.Object;
using Jint.Runtime;

namespace Jint.WebApi.Performance;

/// <summary>
/// Which of the two mutually exclusive <c>observe()</c> shapes an observer has committed to.
/// <para>
/// https://w3c.github.io/performance-timeline/#dom-performanceobserver-observe
/// </para>
/// </summary>
/// <remarks>
/// "A <c>PerformanceObserver</c> object needs to always call <c>observe()</c> with <c>entryTypes</c> set OR
/// always call <c>observe()</c> with <c>type</c> set" — the first successful call decides, and the second
/// shape is refused with an <c>InvalidModificationError</c> from then on. The difference is what the
/// following calls do: an <c>entryTypes</c> call <i>replaces</i> the whole options list, a <c>type</c> call
/// <i>stacks</i> onto it (replacing only an entry of the same type).
/// </remarks>
internal enum PerformanceObserverType
{
    /// <summary>Nothing has been observed yet, so either shape is still available.</summary>
    Undefined,

    /// <summary>The observer has been used with <c>type</c>, and its options stack.</summary>
    Single,

    /// <summary>The observer has been used with <c>entryTypes</c>, and each call replaces the last.</summary>
    Multiple,
}

/// <summary>
/// One <c>PerformanceObserverInit</c> dictionary, already through the WebIDL conversion.
/// <para>
/// https://w3c.github.io/performance-timeline/#dictdef-performanceobserverinit
/// </para>
/// </summary>
/// <param name="Type">The <c>type</c> member, or <see langword="null"/> when it was not present.</param>
/// <param name="EntryTypes">
/// The <c>entryTypes</c> member with the unsupported names already removed, or <see langword="null"/> when it
/// was not present. Never empty: an <c>entryTypes</c> that filters down to nothing aborts <c>observe()</c>
/// before a registration exists.
/// </param>
/// <param name="Buffered">The <c>buffered</c> member, which defaults to <see langword="false"/>.</param>
[StructLayout(LayoutKind.Auto)]
internal readonly record struct PerformanceObserverOptions(string? Type, string[]? EntryTypes, bool Buffered)
{
    /// <summary>
    /// Whether this registration is interested in <paramref name="entryType"/> — "whose <c>entryTypes</c>
    /// member includes entryType or whose <c>type</c> member equals to entryType".
    /// </summary>
    internal bool Matches(string entryType)
    {
        if (Type is not null)
        {
            return string.Equals(Type, entryType, StringComparison.Ordinal);
        }

        var entryTypes = EntryTypes;
        if (entryTypes is null)
        {
            return false;
        }

        for (var i = 0; i < entryTypes.Length; i++)
        {
            if (string.Equals(entryTypes[i], entryType, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }
}

/// <summary>
/// A <c>PerformanceObserver</c> instance: the callback, the entries queued for it, and which entry types it
/// asked for.
/// <para>
/// https://w3c.github.io/performance-timeline/#dom-performanceobserver
/// </para>
/// </summary>
/// <remarks>
/// <para>
/// The <b>observer buffer</b> lives here, on the object, because <c>takeRecords()</c> hands it to script and
/// <c>disconnect()</c> empties it; the <b>list of registered performance observer objects</b> is the engine's
/// and lives in <see cref="PerformanceObserverRegistry"/>, because the specification hangs it off the global
/// and because a restore has to be able to end it. So an observer that has been disconnected is an ordinary
/// live object that simply nothing delivers to.
/// </para>
/// <para>
/// Every attribute of the interface is an operation, so the instance has no own property at all —
/// <c>Object.getOwnPropertyNames(new PerformanceObserver(() =&gt; {}))</c> is empty, as in a browser.
/// </para>
/// </remarks>
internal sealed class JsPerformanceObserver : ObjectInstance
{
    /// <summary>
    /// https://w3c.github.io/performance-timeline/#dfn-observer-buffer — the entries queued for the next
    /// callback. Null until the first one, so an observer that never matches anything allocates nothing.
    /// </summary>
    private List<JsPerformanceEntry>? _buffer;

    internal JsPerformanceObserver(Engine engine, Realm realm, JsValue callback, JsPerformance performance)
        : base(engine, ObjectClass.Object)
    {
        Realm = realm;
        Callback = callback;
        Performance = performance;
    }

    /// <summary>
    /// The realm the observer was constructed in, which is the one its <c>PerformanceObserverEntryList</c> and
    /// its callback options object are built against — a delivery job runs on a later event-loop turn, under
    /// whatever realm happens to be ambient then.
    /// </summary>
    internal Realm Realm { get; }

    /// <summary>
    /// https://w3c.github.io/performance-timeline/#dfn-observer-callback, set on creation and never replaced.
    /// </summary>
    internal JsValue Callback { get; }

    /// <summary>
    /// The <c>performance</c> object of the observer's relevant global — where <c>buffered: true</c> reads
    /// its replay from and where the dropped entries count comes from. Captured on creation, because that is
    /// when the specification fixes the relevant global object.
    /// </summary>
    internal JsPerformance Performance { get; }

    /// <summary>https://w3c.github.io/performance-timeline/#dfn-observer-type.</summary>
    internal PerformanceObserverType ObserverType { get; set; }

    /// <summary>
    /// https://w3c.github.io/performance-timeline/#dfn-requires-dropped-entries — set by every successful
    /// <c>observe()</c> and unset by the first callback that reports the count.
    /// </summary>
    internal bool RequiresDroppedEntries { get; set; }

    /// <summary>Whether anything is waiting to be delivered.</summary>
    internal bool HasBufferedEntries => _buffer is { Count: > 0 };

    /// <summary>Step 5 of <i>queue a PerformanceEntry</i>: "append newEntry to observer's observer buffer".</summary>
    internal void AppendToObserverBuffer(JsPerformanceEntry entry) => (_buffer ??= new List<JsPerformanceEntry>()).Add(entry);

    /// <summary>
    /// Takes the observer buffer and leaves an empty one behind, which is what both <c>takeRecords()</c> and
    /// the delivery task do with it.
    /// </summary>
    internal List<JsPerformanceEntry>? TakeObserverBuffer()
    {
        var buffer = _buffer;
        _buffer = null;
        return buffer;
    }

    /// <summary>https://w3c.github.io/performance-timeline/#dom-performanceobserver-disconnect, step 2.</summary>
    internal void EmptyObserverBuffer() => _buffer = null;
}
#endif
