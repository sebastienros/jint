using System.Globalization;
using System.Runtime.InteropServices;
using Jint.DevTools.Domains;
using Jint.DevTools.Protocol;
using Jint.DevTools.Protocol.Target;
using Jint.DevTools.Session;
using Jint.Runtime;
using Jint.WebApi;

namespace Jint.DevTools;

/// <summary>
/// One thing a client can list, attach to and evaluate in — and which may outlive the engine behind it.
/// </summary>
/// <remarks>
/// <para>
/// <b>A target is an identity; an engine is a document.</b> An <see cref="EngineTarget"/> has one engine for
/// its whole life, and a page target has one <i>per navigation</i>: committing a document builds a new
/// engine and hands it to <see cref="Replace"/>. So this class carries what a client keeps addressing across
/// that — the identifier, the type, the title and location, the browser context, the bindings
/// <c>Runtime.addBinding</c> installed, whether the target is still waiting for a debugger — and
/// <see cref="Runtime"/> carries everything that dies with one engine.
/// </para>
/// <para>
/// <b>A domain holds the target and reads <see cref="Runtime"/> per command.</b> A domain that cached the
/// engine, the object table or the script registry would go on answering about a document that has been
/// replaced, which is the one mistake this split exists to make impossible.
/// </para>
/// <para>
/// It is also the gateway every command addressed to the target crosses, so that the mailbox a request is
/// queued on is always the current engine's. The thread rule is unchanged: a <c>JsValue</c> never leaves the
/// engine thread and a transport thread only ever moves strings.
/// </para>
/// </remarks>
#pragma warning disable CA1001 // the runtime is released through CloseAsync, which a target has instead of Dispose
public abstract class DevToolsTarget : ICommandGateway
#pragma warning restore CA1001
{
    private readonly object _observerLock = new();
    private readonly object _worldLock = new();
    private readonly List<TargetWorld> _worlds = [];

    private ITargetObserver[] _observers = [];
    private DebuggerDomain? _debugger;
    private TargetRuntime _runtime = null!;
    private int _nextExecutionContextId;
    private int _waitingForDebugger;

    /// <summary>Creates a target of <paramref name="type"/>.</summary>
    /// <param name="type">The protocol's target type: <c>node</c> for an engine, <c>page</c> for a page.</param>
    /// <param name="title">The name a client lists the target under.</param>
    /// <param name="url">The location a client shows for the target.</param>
    /// <param name="browserContextId">Which context the target belongs to, or <see langword="null"/>.</param>
    /// <param name="openerId">Which target opened this one, or <see langword="null"/>.</param>
    /// <param name="describer">What names a value this package does not recognize, or <see langword="null"/>.</param>
    /// <param name="waitForDebuggerOnStart">Whether the target runs nothing until a client releases it.</param>
    private protected DevToolsTarget(
        string type,
        string title,
        string url,
        string? browserContextId,
        string? openerId,
        RemoteObjectDescriber? describer,
        bool waitForDebuggerOnStart)
    {
        TargetId = Identifiers.New();
        Type = type;
        Title = title;
        Url = url;
        BrowserContextId = browserContextId;
        OpenerId = openerId;
        Describer = describer;
        _waitingForDebugger = waitForDebuggerOnStart ? 1 : 0;
    }

    /// <summary>Raised on the engine thread the first time the wait for a debugger ends.</summary>
    internal event Action? DebuggerWaitEnded;

    /// <summary>Gets the opaque identifier a client addresses this target by.</summary>
    internal string TargetId { get; }

    /// <summary>Gets the target's protocol type.</summary>
    internal string Type { get; }

    /// <summary>Gets the name a client lists the target under.</summary>
    internal string Title { get; private set; }

    /// <summary>Gets the location a client shows for the target.</summary>
    internal string Url { get; private set; }

    /// <summary>Gets the browser context this target belongs to, or <see langword="null"/> for none.</summary>
    /// <remarks>
    /// An engine target has none and says so: a context partitions cookies, storage and a cache, and an
    /// engine has none of the three. A page target belongs to exactly one.
    /// </remarks>
    internal string? BrowserContextId { get; }

    /// <summary>Gets which target opened this one, or <see langword="null"/>.</summary>
    internal string? OpenerId { get; }

    /// <summary>Gets what names a value this package does not recognize, or <see langword="null"/>.</summary>
    internal RemoteObjectDescriber? Describer { get; }

    /// <summary>Gets the global functions <c>Runtime.addBinding</c> installed, and who hears them.</summary>
    /// <remarks>
    /// <b>On the target, so that a binding survives a navigation.</b> A client installs one and then expects
    /// the page it is about to load to be able to call it; re-installing into every new engine before any of
    /// its script runs is what makes that true, and is what <see cref="Replace"/> does.
    /// </remarks>
    internal BindingRegistry Bindings { get; } = new();

    /// <summary>Gets the engine a client is evaluating in, and everything that dies with it.</summary>
    internal TargetRuntime Runtime => _runtime;

    /// <summary>Gets whether work posted to the target is still held for a client that has not released it.</summary>
    internal bool IsWaitingForDebugger => Volatile.Read(ref _waitingForDebugger) != 0;

    /// <summary>Gets or sets how long a client waits for one command before it is told nothing is pumping.</summary>
    /// <remarks>
    /// The bound belongs to the server a target is published on, and a target may exist before that server
    /// does — so it is settable, and it lives here rather than on the mailbox because the mailbox is
    /// replaced with the engine and the bound is not.
    /// </remarks>
    internal TimeSpan CommandTimeout { get; set; } = DevToolsServerOptions.DefaultCommandTimeout;

    /// <summary>Gets or sets how long the engine may stay paused with no client saying what to do next.</summary>
    /// <inheritdoc cref="CommandTimeout" path="/remarks"/>
    internal TimeSpan PauseTimeout { get; set; } = DevToolsServerOptions.DefaultPauseTimeout;

    /// <summary>Gets the token that is cancelled when the target stops running, if it has one.</summary>
    internal virtual CancellationToken StoppingToken => CancellationToken.None;

    /// <summary>Gets what to tell when this target's title or location changes, set by the server.</summary>
    internal Action<DevToolsTarget>? InfoChanged { get; set; }

    /// <summary>Gets everything currently listening to what the engine says without being asked.</summary>
    internal ITargetObserver[] Observers => Volatile.Read(ref _observers);

    /// <summary>Gets the one attachment that has the <c>Debugger</c> domain enabled, if any.</summary>
    /// <remarks>
    /// <b>One at a time, which is a documented divergence from Chrome.</b> Breakpoints and the step mode live
    /// on the engine's own <c>DebugHandler</c> rather than per session, so a second client enabling the domain
    /// would silently share the first one's breakpoints and steal its pauses. It is refused instead.
    /// </remarks>
    internal DebuggerDomain? ActiveDebugger => Volatile.Read(ref _debugger);

    /// <summary>Gets whether the engine is currently stopped inside the debugger.</summary>
    internal bool IsPaused => Volatile.Read(ref _debugger)?.IsPaused == true;

    /// <inheritdoc/>
    /// <remarks>
    /// Read per command rather than captured, because the mailbox belongs to the engine and the engine is
    /// replaced under the target on every navigation.
    /// </remarks>
    ValueTask<string> ICommandGateway.DispatchAsync(DevToolsSession session, ProtocolRequest request, CommandContext context)
        => _runtime.Dispatcher.DispatchAsync(session, request, context);

    /// <summary>Registers what one attachment to this target answers, on <paramref name="session"/>.</summary>
    /// <param name="session">The session node the attachment answers on.</param>
    /// <param name="browser">The conversation it belongs to, or <see langword="null"/> for a direct connection.</param>
    /// <remarks>
    /// Virtual so that a subclass adds its own domains next to the built-in five: a page target registers
    /// <c>Page</c>, <c>Emulation</c> and their kind here, over the same session core.
    /// </remarks>
    internal virtual TargetDomains RegisterDomains(DevToolsSession session, BrowserSession? browser)
        => BuiltInDomains.RegisterTargetDomains(session, this, browser);

    /// <summary>Replaces the engine under this target, which is what committing a document means.</summary>
    /// <param name="engine">The engine the next document runs in.</param>
    /// <remarks>
    /// <para>
    /// In order, and every step of the order matters. The previous runtime is disposed first, so that a
    /// handle, a script identifier and a queued command of the document that is going all end before the one
    /// arriving is announced. The isolated worlds go with it, because a world is a name for the realm of the
    /// document that minted it. The bindings are re-installed <b>before</b> the observers hear about the
    /// swap, so that a client told about the new context can already call what it added.
    /// </para>
    /// <para>
    /// Runs on the thread that owns both engines — the page loop — like everything else that touches one.
    /// </para>
    /// </remarks>
    internal void Replace(Engine engine)
    {
        if (engine is null)
        {
            Throw.ArgumentNull(nameof(engine));
        }

        var previous = _runtime;
        var next = new TargetRuntime(this, engine, Interlocked.Increment(ref _nextExecutionContextId));

        lock (_worldLock)
        {
            _worlds.Clear();
        }

        _runtime = next;
        previous?.Dispose();

        Bindings.Reinstall(engine);

        foreach (var observer in Observers)
        {
            observer.RuntimeReplaced(next);
        }
    }

    /// <summary>Mints a context identifier that names the current document's realm under a name.</summary>
    /// <param name="worldName">The name a client asked for, which may be empty.</param>
    /// <returns>The world, whose identifier a client may then evaluate against.</returns>
    /// <remarks>
    /// <b>An isolated world here is an alias, and that is a documented divergence.</b> A browser gives a
    /// world a realm of its own, so a script running in it cannot see the page's globals; there is one realm
    /// per document here, and a world is a second name for it. It buys a client its own
    /// <c>executionContextId</c> to address — which is what Puppeteer and Playwright use it for — and it
    /// buys none of the isolation the name promises. The alias lasts until the next navigation, because the
    /// realm it names does.
    /// </remarks>
    internal TargetWorld CreateWorldContext(string? worldName)
    {
        var world = new TargetWorld(
            Interlocked.Increment(ref _nextExecutionContextId),
            worldName ?? "",
            TargetId);

        lock (_worldLock)
        {
            _worlds.Add(world);
        }

        foreach (var observer in Observers)
        {
            observer.WorldCreated(world);
        }

        return world;
    }

    /// <summary>Answers every isolated world of the current document, oldest first.</summary>
    internal TargetWorld[] Worlds
    {
        get
        {
            lock (_worldLock)
            {
                return _worlds.Count == 0 ? [] : [.. _worlds];
            }
        }
    }

    /// <summary>
    /// Refuses a context identifier that is not one of this target's, in Chrome's own wording.
    /// </summary>
    /// <param name="contextId">What the client sent, or <see langword="null"/> for "wherever you are".</param>
    /// <exception cref="ProtocolException">
    /// <c>-32000 Cannot find context with specified id</c>, which is what a client matches on to decide the
    /// context it was addressing went away rather than that it called the command wrongly. A navigation is
    /// exactly that: the identifier a client read off the last <c>executionContextCreated</c> stops meaning
    /// anything the moment the next document commits.
    /// </exception>
    internal void RequireContext(int? contextId)
    {
        if (contextId is not { } id || id == _runtime.ExecutionContextId)
        {
            return;
        }

        lock (_worldLock)
        {
            foreach (var world in _worlds)
            {
                if (world.Id == id)
                {
                    return;
                }
            }
        }

        Throw.ServerError("Cannot find context with specified id");
    }

    /// <summary>Registers what hears the engine's own events, from the attachment that owns them.</summary>
    internal void Observe(ITargetObserver observer)
    {
        lock (_observerLock)
        {
            _observers = [.. _observers, observer];
        }
    }

    /// <summary>Stops telling <paramref name="observer"/> anything, which is what detaching means.</summary>
    internal void Unobserve(ITargetObserver observer)
    {
        lock (_observerLock)
        {
            _observers = [.. _observers.Where(candidate => !ReferenceEquals(candidate, observer))];
        }
    }

    /// <summary>Claims the debugger for <paramref name="domain"/>, answering whether it now has it.</summary>
    internal bool TryClaimDebugger(DebuggerDomain domain)
    {
        var existing = Interlocked.CompareExchange(ref _debugger, domain, null);
        return existing is null || ReferenceEquals(existing, domain);
    }

    /// <summary>Gives the debugger back, if <paramref name="domain"/> is what held it.</summary>
    internal void ReleaseDebugger(DebuggerDomain domain)
    {
        Interlocked.CompareExchange(ref _debugger, null, domain);
    }

    /// <summary>Journals one <c>console</c> call and tells everyone attached, on the engine thread.</summary>
    internal void Record(in ConsoleRecord record)
    {
        var runtime = _runtime;
        var entry = runtime.Console.Add(in record, UnixMilliseconds(), runtime.RemoteObjects);

        foreach (var observer in Observers)
        {
            observer.ConsoleRecorded(entry);
        }
    }

    /// <summary>Reports one exception that escaped the engine, so that every attached client hears it.</summary>
    internal void ReportUncaughtException(JavaScriptException exception)
    {
        if (exception is null)
        {
            Throw.ArgumentNull(nameof(exception));
        }

        foreach (var observer in Observers)
        {
            observer.ExceptionThrown(exception);
        }
    }

    /// <summary>
    /// Ends the wait for a debugger, releasing whatever host work was held. Idempotent, and a no-op on a
    /// target that was never waiting.
    /// </summary>
    internal void ReleaseDebuggerWait()
    {
        if (Interlocked.Exchange(ref _waitingForDebugger, 0) == 0)
        {
            return;
        }

        DebuggerWaitEnded?.Invoke();
        _runtime.Dispatcher.ScheduleDrain();
    }

    /// <summary>Holds every posted host work until a client sends <c>Runtime.runIfWaitingForDebugger</c>.</summary>
    /// <remarks>
    /// What <c>Target.setAutoAttach(waitForDebuggerOnStart: true)</c> followed by <c>Target.createTarget</c>
    /// means: the target exists and runs nothing. Called before the target is published, so that no work has
    /// been posted to it yet.
    /// </remarks>
    internal void HoldForDebugger() => Interlocked.Exchange(ref _waitingForDebugger, 1);

    /// <summary>Changes what a client is told this target is showing, announcing it if anything moved.</summary>
    /// <param name="title">The new title, or <see langword="null"/> to leave it.</param>
    /// <param name="url">The new location, or <see langword="null"/> to leave it.</param>
    internal void UpdateInfo(string? title = null, string? url = null)
    {
        var changed = false;

        if (title is not null && !string.Equals(Title, title, StringComparison.Ordinal))
        {
            Title = title;
            changed = true;
        }

        if (url is not null && !string.Equals(Url, url, StringComparison.Ordinal))
        {
            Url = url;
            changed = true;
        }

        if (changed)
        {
            InfoChanged?.Invoke(this);
        }
    }

    /// <summary>Describes this target the way both <c>/json/list</c> and <c>Target.getTargets</c> do.</summary>
    /// <param name="attached">Whether the asking conversation is attached to it.</param>
    /// <remarks>
    /// One description rather than two, because a client that reads the discovery document and then asks the
    /// same question over the socket must not be told two different things about one target.
    /// </remarks>
    internal TargetInfo Describe(bool attached) => new()
    {
        TargetId = TargetId,
        Type = Type,
        Title = Title,
        Url = Url,
        Attached = attached,
        BrowserContextId = BrowserContextId,
        OpenerId = OpenerId,
        CanAccessOpener = false,
    };

    /// <summary>Closes the target, releasing what it holds. Idempotent.</summary>
    internal abstract ValueTask CloseAsync();

    /// <summary>The protocol's timestamp: milliseconds since the Unix epoch, as a double.</summary>
    internal static double UnixMilliseconds() => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

    /// <summary>Installs the first runtime, which a subclass does once its engine exists.</summary>
    /// <remarks>
    /// Separate from the constructor because a subclass may have to build its engine after the target it
    /// belongs to: a page target is registered, observed and only then given the engine of its first
    /// document.
    /// </remarks>
    private protected void InstallRuntime(Engine engine)
    {
        _runtime = new TargetRuntime(this, engine, Interlocked.Increment(ref _nextExecutionContextId));
    }

    /// <summary>Releases the current runtime, which is what disposing a target means.</summary>
    private protected void DisposeRuntime() => _runtime?.Dispose();
}

/// <summary>
/// One isolated world of the current document: a context identifier and the name a client asked for.
/// </summary>
/// <param name="Id">The execution-context identifier a client evaluates against.</param>
/// <param name="Name">The name the client gave the world, which may be empty.</param>
/// <param name="TargetId">The target the world belongs to, which its unique identifier is built from.</param>
[StructLayout(LayoutKind.Auto)]
internal readonly record struct TargetWorld(int Id, string Name, string TargetId)
{
    /// <summary>Gets the opaque identifier a client tells two contexts apart by.</summary>
    internal string UniqueId => TargetId + "." + Id.ToString(CultureInfo.InvariantCulture);
}
