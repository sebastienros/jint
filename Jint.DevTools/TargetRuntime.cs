using System.Globalization;
using Jint.DevTools.Domains;
using Jint.DevTools.Session;
using Jint.Native.Promise;

namespace Jint.DevTools;

/// <summary>
/// One engine under a target, and everything that dies with it.
/// </summary>
/// <remarks>
/// <para>
/// A page is one target with one engine <i>per navigation</i>, so the state a client addresses splits in
/// two: what survives a document — the target's identity, its bindings, whether it is waiting for a
/// debugger — and what cannot, which is all of this. A handle names a value of <i>this</i> engine, a script
/// identifier names a program <i>this</i> engine parsed, and a console journal is the history of what
/// <i>this</i> document logged. Committing the next document replaces the lot through
/// <see cref="DevToolsTarget.Replace"/>.
/// </para>
/// <para>
/// <b>Every identifier it mints is unique for the life of the process</b>, not merely for the life of the
/// runtime. A client holding an <c>objectId</c> from the document before last must be told the handle has
/// gone rather than be answered about a value of the new engine that happens to sit at the same number, so
/// the tables are seeded from a serial that only ever increases.
/// </para>
/// <para>
/// Built and disposed on the engine's own thread, like everything else that touches an engine.
/// </para>
/// </remarks>
internal sealed class TargetRuntime : IDisposable
{
    /// <summary>How many runtimes this process has built, which is what makes every identifier distinct.</summary>
    private static int _serial;

    private readonly DevToolsTarget _target;
    private readonly DevToolsConsoleSink? _console;
    private readonly EventHandler<PromiseRejectionTrackerEventArgs> _onRejection;

    private int _disposed;

    /// <summary>Builds the runtime of <paramref name="engine"/> under <paramref name="target"/>.</summary>
    /// <param name="target">The target this engine is the current one of.</param>
    /// <param name="engine">The engine a client evaluates in.</param>
    /// <param name="executionContextId">
    /// The identifier the protocol's default execution context carries. It comes from a counter on the
    /// target, so the second document's context is <c>2</c> and never <c>1</c> again — which is what makes a
    /// client sending back a stale one distinguishable from one sending the current one.
    /// </param>
    internal TargetRuntime(DevToolsTarget target, Engine engine, int executionContextId)
    {
        var serial = Interlocked.Increment(ref _serial);

        _target = target;
        _onRejection = OnPromiseRejection;

        Engine = engine;
        ExecutionContextId = executionContextId;
        UniqueContextId = target.TargetId + "." + executionContextId.ToString(CultureInfo.InvariantCulture);
        RemoteObjects = new RemoteObjectTable(serial);
        Dispatcher = new EngineDispatcher(engine, target);

        // Scripts are registered from the moment the runtime exists rather than from the moment a client
        // asks, because Debugger.enable replays what has already been parsed and a front end's Sources panel
        // is built from that replay. An engine the host did not build with the debugger has no
        // BeforeEvaluate to subscribe to and no pause to serve, and the domain says so rather than answering
        // something untrue.
        if (engine.Options.Debugger.Enabled)
        {
            Scripts = new ScriptRegistry(engine, serial);
            Scripts.Start();
        }

        // Console records reach a client only if the engine was built with UseDevTools, which is what
        // installed the sink this binds to. An engine built without it is attachable and evaluable and says
        // nothing about what its scripts logged, which is stated rather than silently half-true.
        _console = engine.Options.WebApi.Console.Sink as DevToolsConsoleSink;
        if (_console?.TryBind(target, engine) == false)
        {
            // A second engine built from one Options instance. The first runtime speaks for that sink; this
            // one keeps everything else and reports no console traffic.
            _console = null;
        }

        // An unhandled rejection is an event rather than a command, so the subscription is taken before any
        // thread starts pumping and nothing is missed.
        engine.Tasks.PromiseRejectionTracker += _onRejection;
    }

    /// <summary>Gets the engine a client attached to the target is evaluating in right now.</summary>
    internal Engine Engine { get; }

    /// <summary>Gets the mailbox every protocol command addressed to this engine crosses.</summary>
    internal EngineDispatcher Dispatcher { get; }

    /// <summary>Gets the handles minted against this engine, which die with it.</summary>
    internal RemoteObjectTable RemoteObjects { get; }

    /// <summary>Gets the programs this engine has parsed, or <see langword="null"/> without a debugger.</summary>
    internal ScriptRegistry? Scripts { get; }

    /// <summary>Gets the scripts <c>Runtime.compileScript</c> persisted against this engine.</summary>
    internal CompiledScriptRegistry CompiledScripts { get; } = new();

    /// <summary>Gets the last few <c>console</c> calls this document made.</summary>
    internal ConsoleJournal Console { get; } = new();

    /// <summary>Gets the identifier the protocol's default execution context carries.</summary>
    internal int ExecutionContextId { get; }

    /// <summary>Gets the opaque identifier a client tells two contexts apart by across a reload.</summary>
    internal string UniqueContextId { get; }

    /// <summary>Gets whether this runtime has been replaced or its target disposed.</summary>
    internal bool IsDisposed => Volatile.Read(ref _disposed) != 0;

    /// <inheritdoc/>
    /// <remarks>
    /// Called on the engine thread, from <see cref="DevToolsTarget.Replace"/> or from the target going away.
    /// The engine this runtime was built over may already have been disposed by whoever owns it — a page
    /// disposes the outgoing engine on its loop and the target learns of the swap afterwards — so the two
    /// steps that reach into the engine are skipped when <see cref="Engine.IsDisposed"/> says so. Everything
    /// else touches no engine and runs either way.
    /// </remarks>
    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        // The only two steps that reach into the engine. A disposed engine has already dropped both
        // subscriptions along with everything else it held, so there is nothing left to unsubscribe from.
        if (!Engine.IsDisposed)
        {
            Engine.Tasks.PromiseRejectionTracker -= _onRejection;
            Scripts?.Stop();
        }

        _console?.Unbind(_target);

        // A handle is a promise to keep a value alive until the client releases it, and the engine those
        // values belong to is going. Dropping the references runs no engine code, and so does letting go of
        // the journal that was holding the arguments of this document's console calls.
        Console.Clear(RemoteObjects);
        RemoteObjects.Clear();

        // Last: whatever a client had queued for an engine that will never run it again is answered rather
        // than left to time out. Anything handed on goes to the runtime that replaced this one, whose engine
        // is live.
        Dispatcher.Abandon();
        Dispatcher.Dispose();
    }

    /// <summary>
    /// The engine's own unhandled-rejection channel, which is where <c>Runtime.exceptionThrown</c> and
    /// <c>Runtime.exceptionRevoked</c> come from.
    /// </summary>
    /// <remarks>
    /// The engine raises <c>Reject</c> for a promise that is still unhandled at the microtask checkpoint
    /// ending the job it was rejected in, and <c>Handle</c> if something handles it in a later turn — the
    /// same cadence V8 decides on, so a rejection handled on the very next line produces neither event here
    /// and neither in Chrome. <c>exceptionRevoked</c> is therefore what it is in Chrome too: the answer for
    /// a handler that arrives after the report, not a correction issued a moment after it.
    /// </remarks>
    private void OnPromiseRejection(object? sender, PromiseRejectionTrackerEventArgs arguments)
    {
        var observers = _target.Observers;
        if (observers.Length == 0)
        {
            return;
        }

        foreach (var observer in observers)
        {
            if (arguments.Operation == PromiseRejectionOperation.Reject)
            {
                observer.RejectionThrown(arguments.Promise, arguments.Value ?? Native.JsValue.Undefined);
            }
            else
            {
                observer.RejectionHandled(arguments.Promise);
            }
        }
    }
}
