using Jint.Native;
using Jint.Runtime;
using Jint.WebApi;

namespace Jint.DevTools.Domains;

/// <summary>
/// One thing that happened in the engine which a client wants to hear about, whether or not anybody was
/// listening at the time.
/// </summary>
/// <remarks>
/// <para>
/// Three of them — a <c>console</c> call, an exception nobody caught, a promise rejected with no handler —
/// and all three arrive on the engine thread from inside the engine, where there is no command to answer and
/// nothing to <c>await</c> onto.
/// </para>
/// <para>
/// Default implementations throughout, because a domain hears about one of the three and not the others: the
/// <c>Runtime</c> domain hears all of it, <c>Console</c> hears console calls, <c>Log</c> hears failures.
/// </para>
/// </remarks>
internal interface ITargetObserver
{
    /// <summary>One <c>console</c> call, already journalled.</summary>
    void ConsoleRecorded(ConsoleEntry entry)
    {
    }

    /// <summary>One exception that escaped the engine's own pump.</summary>
    void ExceptionThrown(JavaScriptException exception)
    {
    }

    /// <summary>One promise rejected with nothing to handle it.</summary>
    void RejectionThrown(JsValue promise, JsValue reason)
    {
    }

    /// <summary>One previously unhandled rejection that has since been handled.</summary>
    void RejectionHandled(JsValue promise)
    {
    }

    /// <summary>
    /// The engine under the target has been replaced, which is what committing a document means.
    /// </summary>
    /// <param name="runtime">The engine a client is evaluating in from now on, and its fresh state.</param>
    /// <remarks>
    /// Every handle, script identifier and console entry of the document that is going has already been
    /// released by the time this runs, so a domain's job here is to tell the client what it now has — a new
    /// default execution context, a fresh set of parsed scripts — rather than to tidy up after the old one.
    /// </remarks>
    void RuntimeReplaced(TargetRuntime runtime)
    {
    }

    /// <summary>An isolated world was minted over the current document's realm.</summary>
    /// <param name="world">The alias, whose identifier a client may then evaluate against.</param>
    void WorldCreated(TargetWorld world)
    {
    }
}

/// <summary>
/// One journalled <c>console</c> call: what the engine saw, plus the handles every attachment has minted for
/// it.
/// </summary>
/// <remarks>
/// <para>
/// <b>The arguments are copied out of the record.</b> <see cref="ConsoleRecord.Arguments"/> is documented as
/// valid only for the duration of the sink's call, so what is kept here is an array of this journal's own —
/// which holds the values strongly, exactly as a remote-object handle does, and is why the journal is
/// bounded.
/// </para>
/// <para>
/// The identifiers are recorded per entry rather than per session so that evicting an entry releases every
/// handle any attachment minted for it. Without that a long-running page that logs objects would keep every
/// one of them alive for as long as a client stayed attached.
/// </para>
/// </remarks>
internal sealed class ConsoleEntry
{
    private List<string>? _handles;

    internal ConsoleEntry(in ConsoleRecord record, double timestamp)
    {
        Method = record.Method;
        Level = record.Level;
        Message = record.Message;
        GroupDepth = record.GroupDepth;
        Timestamp = timestamp;

        var arguments = record.Arguments;
        if (arguments.Count == 0)
        {
            Arguments = [];
        }
        else
        {
            var copy = new JsValue[arguments.Count];
            for (var i = 0; i < copy.Length; i++)
            {
                copy[i] = arguments[i];
            }

            Arguments = copy;
        }

        StackTrace = record.StackTrace is { Count: > 0 } frames ? [.. frames] : null;
    }

    /// <summary>Gets which console method was called.</summary>
    internal ConsoleMethod Method { get; }

    /// <summary>Gets the severity that method implies.</summary>
    internal ConsoleLogLevel Level { get; }

    /// <summary>Gets the arguments, held strongly for as long as this entry is in the journal.</summary>
    internal JsValue[] Arguments { get; }

    /// <summary>Gets the finished line, or <see langword="null"/> when the method printed nothing.</summary>
    internal string? Message { get; }

    /// <summary>Gets how many <c>console.group</c>s were open.</summary>
    internal int GroupDepth { get; }

    /// <summary>Gets when the call happened, in milliseconds since the Unix epoch.</summary>
    internal double Timestamp { get; }

    /// <summary>Gets the captured frames of a <c>console.trace</c>, or <see langword="null"/>.</summary>
    internal ConsoleStackFrame[]? StackTrace { get; }

    /// <summary>Records that an attachment minted <paramref name="objectId"/> for one of these arguments.</summary>
    /// <remarks>
    /// Minting happens on the engine thread and releasing may not — a target disposed from anywhere empties
    /// the journal — so the one list both touch is locked. It is taken once per console argument.
    /// </remarks>
    internal void Minted(string objectId)
    {
        lock (this)
        {
            (_handles ??= []).Add(objectId);
        }
    }

    /// <summary>Releases every handle minted for this entry, which is what falling out of the journal means.</summary>
    internal void Release(RemoteObjectTable table)
    {
        List<string>? handles;

        lock (this)
        {
            handles = _handles;
            _handles = null;
        }

        if (handles is null)
        {
            return;
        }

        foreach (var objectId in handles)
        {
            table.Release(objectId);
        }
    }
}

/// <summary>
/// The last few <c>console</c> calls, so that a client attaching after the fact is not told there was
/// silence.
/// </summary>
/// <remarks>
/// <para>
/// V8 keeps such a store and reports it on <c>Runtime.enable</c> and <c>Console.enable</c>, which is what
/// makes a front end opened halfway through a run useful rather than empty. This one is the same idea with
/// the bound stated: <see cref="MaxEntries"/> calls, after which the oldest is dropped and every handle
/// minted for it released.
/// </para>
/// <para>
/// It lives on the <see cref="TargetRuntime"/>, because the values in it belong to <i>that</i> engine and
/// every attachment replays the same history. A navigation therefore ends it: the next document starts with
/// a console of its own, which is what a browser shows. <c>Runtime.discardConsoleEntries</c> and
/// <c>Console.clearMessages</c> both empty it — which is what those commands mean, and why they are not the
/// no-op they were.
/// </para>
/// </remarks>
internal sealed class ConsoleJournal
{
    /// <summary>
    /// How many calls are kept. Every one of them holds its arguments alive, so this is a memory bound
    /// before it is a replay bound: a page that logs a large object every second keeps the last hundred.
    /// </summary>
    private const int MaxEntries = 100;

    private readonly object _gate = new();
    private readonly Queue<ConsoleEntry> _entries = new();

    /// <summary>Journals one call and answers the entry, for the caller to hand to the observers.</summary>
    internal ConsoleEntry Add(in ConsoleRecord record, double timestamp, RemoteObjectTable table)
    {
        var entry = new ConsoleEntry(in record, timestamp);
        ConsoleEntry? evicted = null;

        lock (_gate)
        {
            _entries.Enqueue(entry);
            if (_entries.Count > MaxEntries)
            {
                evicted = _entries.Dequeue();
            }
        }

        evicted?.Release(table);
        return entry;
    }

    /// <summary>Answers what a client enabling after the fact is owed, oldest first.</summary>
    internal ConsoleEntry[] Snapshot()
    {
        lock (_gate)
        {
            return _entries.Count == 0 ? [] : [.. _entries];
        }
    }

    /// <summary>Empties the journal, releasing every handle any attachment minted for what was in it.</summary>
    internal void Clear(RemoteObjectTable table)
    {
        ConsoleEntry[] cleared;

        lock (_gate)
        {
            if (_entries.Count == 0)
            {
                return;
            }

            cleared = [.. _entries];
            _entries.Clear();
        }

        foreach (var entry in cleared)
        {
            entry.Release(table);
        }
    }
}
