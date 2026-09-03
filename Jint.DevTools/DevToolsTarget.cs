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
    /// <remarks>
    /// Virtual because a target need not own one: a <c>tab</c> target is a handle a client attaches to on its
    /// way to the page inside it, and it answers about that page's engine rather than about one of its own.
    /// </remarks>
    internal virtual TargetRuntime Runtime => _runtime;

    /// <summary>Gets the targets this one is a parent of, which is how a client reaches them.</summary>
    /// <remarks>
    /// <b>Empty for everything but a <c>tab</c>.</b> Modern Chrome puts a tab target between the browser and
    /// each page, and Puppeteer is written against that: it excludes <c>page</c> from its browser-level
    /// <c>setAutoAttach</c> filter and reaches a page by sending <c>setAutoAttach</c> again on the tab's own
    /// session. A server with no tab targets is one Puppeteer connects to and then never finds a page in.
    /// </remarks>
    internal virtual IReadOnlyList<DevToolsTarget> Children => [];

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

    /// <summary>The size a target with nothing to say about a window reports.</summary>
    /// <remarks>The browser package's own default viewport, so the two answer alike for a page.</remarks>
    internal static (int Width, int Height) DefaultWindowSize => (1280, 720);

    /// <summary>Gets the size <c>Browser.getWindowForTarget</c> reports for this target.</summary>
    /// <remarks>
    /// There is no window. What a client is told is the size the target believes it has — a page's viewport,
    /// which <c>Emulation.setDeviceMetricsOverride</c> really does change — so that reading the window back
    /// after setting it answers what was set.
    /// </remarks>
    internal virtual (int Width, int Height) WindowSize => DefaultWindowSize;

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
    /// replaced under the target on every navigation. A method <see cref="RunsOffThread"/> names is the one
    /// exception: it is answered here, on the thread that read it, and never reaches the mailbox at all.
    /// </remarks>
    ValueTask<string> ICommandGateway.DispatchAsync(DevToolsSession session, ProtocolRequest request, CommandContext context)
        => RunsOffThread(request.Method)
            ? session.DispatchAsync(in request, context)
            : Runtime.Dispatcher.DispatchAsync(session, request, context);

    /// <summary>Whether one command is answered on the thread that read it rather than on the engine's.</summary>
    /// <param name="method">The qualified method a client sent, such as <c>Fetch.continueRequest</c>.</param>
    /// <returns>
    /// <see langword="true"/> to answer the command in place, <see langword="false"/> — the default — to
    /// queue it on the current engine's mailbox.
    /// </returns>
    /// <remarks>
    /// <para>
    /// <b>The one documented exception to "every domain method runs on the engine thread", and the bar is
    /// what keeps it one.</b> A method may be named here only if it provably touches no engine state, no
    /// <c>JsValue</c> and no AngleSharp node — nothing but its own parameters and structures that are
    /// thread-safe by construction. One that reads a <c>RemoteObjectTable</c>, a <c>ScriptRegistry</c>, a
    /// <c>DebugHandler</c>, an <c>Engine</c> or a DOM node may never be, whatever it costs on the loop.
    /// </para>
    /// <para>
    /// <b>What it buys is a command that is answerable while the loop is not.</b> A pause a client is asked
    /// to answer holds a transport thread, and the answer has to reach that thread — so a command queued
    /// behind a loop the pause itself is blocking could not be delivered until the block ended.
    /// <c>PageTarget</c> names the three <c>Fetch</c> commands that release such a pause and nothing else.
    /// </para>
    /// <para>
    /// <b>It needs no clone of its parameters, unlike a queued command.</b> It runs inside
    /// <c>DevToolsSession.HandleMessageAsync</c>'s own <see langword="try"/>, so the caller's
    /// <c>JsonDocument</c> is still open; the mailbox clones because an item it answered on a timeout is
    /// read by the engine thread after that document has gone back to the pool.
    /// </para>
    /// <para>
    /// <b>And a navigation never abandons it.</b> Abandoning is for a command addressed to a context that
    /// was replaced; a command that reaches no engine names no context, and it is answered before a swap
    /// could reach it. <see langword="internal"/> so that <c>Jint.Browser</c> overrides it and nothing
    /// outside this repository can widen the thread rule.
    /// </para>
    /// </remarks>
    internal virtual bool RunsOffThread(string method) => false;

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
    /// <b>A named world is then made again, under its own name and with a fresh identifier.</b> Chrome does
    /// that, and every client depends on it: Puppeteer and Playwright each create one utility world when
    /// they attach and then use it for the whole life of the page, so a world that ended with the first
    /// document leaves <c>$</c>, <c>$$</c> and <c>waitForSelector</c> waiting for a context that never
    /// arrives. It happens after the observers, which is the order a client reads — the default context
    /// first, then the worlds over it.
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

        string[] names;
        lock (_worldLock)
        {
            names = [.. _worlds.Select(world => world.Name).Where(name => name.Length != 0).Distinct(StringComparer.Ordinal)];
            _worlds.Clear();
        }

        _runtime = next;
        previous?.Dispose();

        Bindings.Reinstall(engine);

        foreach (var observer in Observers)
        {
            observer.RuntimeReplaced(next);
        }

        foreach (var name in names)
        {
            CreateWorldContext(name);
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
    /// realm it names does, and <see cref="Replace"/> makes a named one again over the document that follows.
    /// </remarks>
    /// <remarks>
    /// <b>A name is a world.</b> Asking twice for the same non-empty name answers the one that exists rather
    /// than minting a second identifier over the same realm, which is Chrome's behaviour and what keeps a
    /// client that re-creates its utility world per navigation from accumulating one per attempt. An
    /// unnamed world is a fresh one every time, because there is nothing to match it by.
    /// </remarks>
    internal TargetWorld CreateWorldContext(string? worldName)
    {
        var name = worldName ?? "";
        TargetWorld world;

        lock (_worldLock)
        {
            if (name.Length != 0)
            {
                foreach (var existing in _worlds)
                {
                    if (string.Equals(existing.Name, name, StringComparison.Ordinal))
                    {
                        return existing;
                    }
                }
            }

            world = new TargetWorld(Interlocked.Increment(ref _nextExecutionContextId), name, TargetId);
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
        if (contextId is not { } id || id == Runtime.ExecutionContextId)
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
        var runtime = Runtime;
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
        Runtime.Dispatcher.ScheduleDrain();
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
