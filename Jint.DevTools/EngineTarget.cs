using Jint.DevTools.Domains;
using Jint.DevTools.Session;
using Jint.Native.Promise;
using Jint.Runtime;
using Jint.WebApi;

namespace Jint.DevTools;

/// <summary>
/// One engine, as a client sees it: a target it can list, attach to and evaluate in.
/// </summary>
/// <remarks>
/// <para>
/// A target owns the mailbox that brings a protocol command from a transport thread to the thread that runs
/// the engine, which is the whole of the thread rule: a <see cref="Native.JsValue"/> never leaves the engine
/// thread, and a transport thread only ever moves strings. Which thread that is comes from
/// <see cref="EngineTargetOptions.ThreadMode"/>.
/// </para>
/// <para>
/// The target reports itself to clients as Node's <c>node</c> type, so the DevTools front end opens its
/// JavaScript-only layout and never asks the target for a page. Page targets belong to the browser package.
/// </para>
/// <para>
/// <b>The target does not own the engine's lifetime.</b> Disposing it stops the thread it started, if it
/// started one, and stops answering commands; the <see cref="Engine"/> is the host's, before and after.
/// </para>
/// </remarks>
public sealed class EngineTarget : IAsyncDisposable
{
    /// <summary>
    /// How long the library-owned loop parks before looking again. It is woken by every arrival, so this
    /// bounds nothing but how quickly a cancellation with no work behind it is noticed.
    /// </summary>
    private static readonly TimeSpan IdleInterval = TimeSpan.FromMilliseconds(100);

    /// <summary>The longest a single wait may ask for, which is what the blocking primitives accept.</summary>
    private static readonly TimeSpan MaxWait = TimeSpan.FromMilliseconds(int.MaxValue);

    /// <summary>
    /// How many targets this process has made, which is what prefixes a target's remote-object identifiers
    /// so that a handle from one target is refused by another rather than resolving in the wrong engine.
    /// </summary>
    private static int _serial;

    private readonly EngineDispatcher _dispatcher;
    private readonly CancellationTokenSource? _stopping;
    private readonly Thread? _thread;
    private readonly ManualResetEventSlim? _debuggerWaitEnded;
    private readonly DevToolsConsoleSink? _console;
    private readonly object _observerLock = new();

    private ITargetObserver[] _observers = [];
    private int _disposed;

    /// <summary>Creates a target over <paramref name="engine"/>.</summary>
    /// <param name="engine">The engine a client attaches to.</param>
    /// <param name="options">How the target presents itself and which thread runs it, or the defaults.</param>
    /// <exception cref="ArgumentNullException"><paramref name="engine"/> is <see langword="null"/>.</exception>
    public EngineTarget(Engine engine, EngineTargetOptions? options = null)
    {
        if (engine is null)
        {
            Throw.ArgumentNull(nameof(engine));
        }

        options ??= new EngineTargetOptions();

        Engine = engine;
        Title = options.Title;
        Url = options.Url;
        ThreadMode = options.ThreadMode;
        TargetId = Identifiers.New();
        Describer = options.RemoteObjectDescriber;
        RemoteObjects = new RemoteObjectTable(Interlocked.Increment(ref _serial));

        _dispatcher = new EngineDispatcher(engine, DevToolsServerOptions.DefaultCommandTimeout, options.WaitForDebuggerOnStart);

        // Console records reach a client only if the engine was built with UseDevTools, which is what
        // installed the sink this binds to. An engine built without it is attachable and evaluable and says
        // nothing about what its scripts logged, which is stated rather than silently half-true.
        _console = engine.Options.WebApi.Console.Sink as DevToolsConsoleSink;
        if (_console?.TryBind(this) == false)
        {
            // A second engine built from one Options instance. The first target speaks for that sink; this
            // one keeps everything else and reports no console traffic.
            _console = null;
        }

        // An unhandled rejection is an event rather than a command, so the subscription is the target's and
        // outlives every attachment. It is taken before the loop thread starts, so nothing is missed.
        engine.Tasks.PromiseRejectionTracker += OnPromiseRejection;

        if (options.WaitForDebuggerOnStart)
        {
            var released = new ManualResetEventSlim(initialState: false);
            _debuggerWaitEnded = released;
            _dispatcher.DebuggerWaitEnded += released.Set;
        }

        if (ThreadMode != ThreadMode.LibraryOwned)
        {
            return;
        }

        _stopping = new CancellationTokenSource();
        _thread = new Thread(RunLoop)
        {
            IsBackground = true,
            Name = "Jint DevTools target " + TargetId,
        };

        _thread.Start();
    }

    /// <summary>Gets the engine this target attaches a client to.</summary>
    public Engine Engine { get; }

    /// <summary>Gets the opaque identifier a client addresses this target by.</summary>
    public string TargetId { get; }

    /// <summary>Gets the target's protocol type, which is always <c>node</c> for an engine target.</summary>
    public string Type { get; } = "node";

    /// <summary>Gets the name a client lists the target under.</summary>
    public string Title { get; }

    /// <summary>Gets the location a client shows for the target.</summary>
    public string Url { get; }

    /// <summary>Gets which thread runs the engine and answers commands addressed to this target.</summary>
    public ThreadMode ThreadMode { get; }

    /// <summary>Gets whether work posted to the target is still held for a client that has not released it.</summary>
    public bool IsWaitingForDebugger => _dispatcher.IsWaitingForDebugger;

    /// <summary>Gets the mailbox every protocol command addressed to this target crosses.</summary>
    internal EngineDispatcher Dispatcher => _dispatcher;

    /// <summary>
    /// Gets the handles this target has handed out, which live for as long as the client holding one does.
    /// </summary>
    /// <remarks>
    /// On the target rather than on a session because the values in it belong to the engine: two sessions
    /// attached to one engine address the same value by the same identifier, and each releases only what it
    /// registered.
    /// </remarks>
    internal RemoteObjectTable RemoteObjects { get; }

    /// <summary>Gets what names a value this package does not recognize, or <see langword="null"/>.</summary>
    internal RemoteObjectDescriber? Describer { get; }

    /// <summary>
    /// Gets the scripts <c>Runtime.compileScript</c> persisted, by the identifier it answered with.
    /// </summary>
    /// <remarks>
    /// On the target for the same reason the object table is: a compiled script belongs to the engine that
    /// would run it, and the identifier a client is holding has to mean the same thing on every attachment.
    /// </remarks>
    internal CompiledScriptRegistry CompiledScripts { get; } = new();

    /// <summary>Gets the global functions <c>Runtime.addBinding</c> installed, and who hears them.</summary>
    internal BindingRegistry Bindings { get; } = new();

    /// <summary>Gets the last few <c>console</c> calls, which a client enabling after the fact is replayed.</summary>
    internal ConsoleJournal Console { get; } = new();

    /// <summary>
    /// Reports one exception that escaped the engine, so that every attached client hears about it.
    /// </summary>
    /// <param name="exception">What escaped.</param>
    /// <remarks>
    /// <para>
    /// A <see cref="ThreadMode.LibraryOwned"/> target calls this itself, for anything a timer callback, a
    /// promise reaction or a host job let out of <see cref="Engine.TaskOperations.ProcessTasks"/>. A
    /// <see cref="ThreadMode.HostOwned"/> host owns its own loop, so it is the one that catches — and this
    /// is how it tells a client, in the shape a client already understands: <c>Runtime.exceptionThrown</c>
    /// and <c>Log.entryAdded</c>.
    /// </para>
    /// <para>
    /// <b>Reporting is not handling.</b> It writes to whoever is attached and returns; whether the host
    /// swallows the exception, rethrows it or ends the run is the host's decision and this changes none of
    /// it. With no client attached it does nothing at all.
    /// </para>
    /// <para>
    /// Call it on the engine's own thread, like everything else that carries a <see cref="Native.JsValue"/>
    /// — the exception's error value is one.
    /// </para>
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="exception"/> is <see langword="null"/>.</exception>
    public void ReportUncaughtException(JavaScriptException exception)
    {
        if (exception is null)
        {
            Throw.ArgumentNull(nameof(exception));
        }

        foreach (var observer in Volatile.Read(ref _observers))
        {
            observer.ExceptionThrown(exception);
        }
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

    /// <summary>Journals one <c>console</c> call and tells everyone attached, on the engine thread.</summary>
    internal void Record(in ConsoleRecord record)
    {
        var entry = Console.Add(in record, UnixMilliseconds(), RemoteObjects);

        foreach (var observer in Volatile.Read(ref _observers))
        {
            observer.ConsoleRecorded(entry);
        }
    }

    /// <summary>The protocol's timestamp: milliseconds since the Unix epoch, as a double.</summary>
    internal static double UnixMilliseconds() => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

    /// <summary>
    /// Gives the engine one turn: answers the protocol commands waiting for it, then runs the event loop.
    /// </summary>
    /// <remarks>
    /// What a <see cref="ThreadMode.HostOwned"/> host calls from its own loop.
    /// <see cref="Engine.TaskOperations.ProcessTasks"/> alone is enough — the mailbox wakes the loop through
    /// <see cref="Engine.TaskOperations.Post(System.Action)"/> and is drained as an ordinary job — so this is
    /// the convenience, not the mechanism.
    /// </remarks>
    /// <exception cref="ObjectDisposedException">The target has been disposed.</exception>
    /// <exception cref="InvalidOperationException">The target is <see cref="ThreadMode.LibraryOwned"/>.</exception>
    public void Pump()
    {
        ThrowIfDisposed();

        if (ThreadMode == ThreadMode.LibraryOwned)
        {
            Throw.InvalidOperation("A LibraryOwned target pumps itself; calling Pump on it from another thread is what the engine's single-drainer guard refuses.");
        }

        _dispatcher.Drain();
        Engine.Tasks.ProcessTasks();
    }

    /// <summary>Queues host work to run on the engine's own thread, from any thread.</summary>
    /// <param name="work">What to run, given the engine.</param>
    /// <remarks>
    /// The supported way for a <see cref="ThreadMode.LibraryOwned"/> host to reach its engine, and safe from
    /// a <see cref="ThreadMode.HostOwned"/> one too: the work runs in queue order with the protocol commands
    /// around it. While <see cref="IsWaitingForDebugger"/> holds, it is held; protocol commands are not, or
    /// the command that ends the wait could never be answered.
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="work"/> is <see langword="null"/>.</exception>
    /// <exception cref="ObjectDisposedException">The target has been disposed.</exception>
    public void Post(Action<Engine> work)
    {
        if (work is null)
        {
            Throw.ArgumentNull(nameof(work));
        }

        ThrowIfDisposed();
        _dispatcher.Post(work);
    }

    /// <summary>Queues host work and answers when it has run.</summary>
    /// <param name="work">What to run, given the engine.</param>
    /// <returns>A task that completes when <paramref name="work"/> has run, or faults with what it threw.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="work"/> is <see langword="null"/>.</exception>
    /// <exception cref="ObjectDisposedException">The target has been disposed.</exception>
    public Task PostAsync(Action<Engine> work)
    {
        if (work is null)
        {
            Throw.ArgumentNull(nameof(work));
        }

        return PostAsync<object?>(engine =>
        {
            work(engine);
            return null;
        });
    }

    /// <summary>Queues host work and answers with what it returned.</summary>
    /// <typeparam name="T">What the work answers with, which must be a CLR value rather than a <c>JsValue</c>.</typeparam>
    /// <param name="work">What to run, given the engine.</param>
    /// <returns>A task carrying the work's result, or faulted with what it threw.</returns>
    /// <remarks>
    /// <b>Never answer with a <see cref="Native.JsValue"/>.</b> It belongs to the engine's thread, and the
    /// awaiting thread is not it; convert to a CLR value inside the work.
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="work"/> is <see langword="null"/>.</exception>
    /// <exception cref="ObjectDisposedException">The target has been disposed.</exception>
    public Task<T> PostAsync<T>(Func<Engine, T> work)
    {
        if (work is null)
        {
            Throw.ArgumentNull(nameof(work));
        }

        ThrowIfDisposed();

        var completion = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
        _dispatcher.Post(engine =>
        {
            try
            {
                completion.TrySetResult(work(engine));
            }
            catch (Exception exception)
            {
                completion.TrySetException(exception);
            }
        });

        return completion.Task;
    }

    /// <summary>
    /// Pumps until a client releases a target started with
    /// <see cref="EngineTargetOptions.WaitForDebuggerOnStart"/>.
    /// </summary>
    /// <param name="timeout">How long to wait before giving up.</param>
    /// <returns><see langword="true"/> if a client released the target within <paramref name="timeout"/>.</returns>
    /// <remarks>
    /// What a <see cref="ThreadMode.HostOwned"/> host calls in place of its first
    /// <see cref="Pump"/>: the command that ends the wait — <c>Runtime.runIfWaitingForDebugger</c> — is
    /// itself answered on this thread, so a host that merely blocked would wait forever. A
    /// <see cref="ThreadMode.LibraryOwned"/> target needs none of this and answers immediately.
    /// </remarks>
    /// <exception cref="ObjectDisposedException">The target has been disposed.</exception>
    public bool WaitForDebugger(TimeSpan timeout)
    {
        ThrowIfDisposed();

        if (!IsWaitingForDebugger)
        {
            return true;
        }

        // Clamped rather than validated: a host spelling "effectively no bound" passes something enormous,
        // and both the wait below and the arithmetic after it reject anything past int.MaxValue milliseconds.
        var bound = timeout < TimeSpan.Zero || timeout > MaxWait ? MaxWait : timeout;

        if (ThreadMode == ThreadMode.LibraryOwned)
        {
            return _debuggerWaitEnded?.Wait(bound) ?? true;
        }

        var deadline = Environment.TickCount64 + (long) bound.TotalMilliseconds;
        while (IsWaitingForDebugger)
        {
            Pump();

            if (!IsWaitingForDebugger)
            {
                break;
            }

            var remaining = deadline - Environment.TickCount64;
            if (remaining <= 0)
            {
                return false;
            }

            Engine.Tasks.WaitForScheduledWork(TimeSpan.FromMilliseconds(Math.Min(remaining, IdleInterval.TotalMilliseconds)));
        }

        return true;
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        // The engine is the host's, before and after: a disposed target stops reaching into it first, so
        // that nothing arrives after the release below.
        Engine.Tasks.PromiseRejectionTracker -= OnPromiseRejection;
        _console?.Unbind(this);

        // A handle is a promise to keep a value alive until the client releases it, and there is no client
        // left to release one. Dropping the references runs no engine code, so it is safe from here, and so
        // is letting go of the journal that was holding the arguments of the last hundred console calls.
        Console.Clear(RemoteObjects);
        RemoteObjects.Clear();

        if (_stopping is not null)
        {
            await _stopping.CancelAsync().ConfigureAwait(false);
        }

        if (_thread is not null && _thread.IsAlive)
        {
            _thread.Join(TimeSpan.FromSeconds(5));
        }

        _stopping?.Dispose();
        _debuggerWaitEnded?.Dispose();
    }

    private void RunLoop()
    {
        var token = _stopping!.Token;

        while (!token.IsCancellationRequested)
        {
            try
            {
                _dispatcher.Drain();
                Engine.Tasks.ProcessTasks();
                Engine.Tasks.WaitForScheduledWork(IdleInterval, token);
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (JavaScriptException exception)
            {
                // Script that escaped the pump — a timer callback, a promise reaction, an event listener.
                // The loop swallows it either way, because the next turn is still owed to every other
                // client; what changed is that a client now hears about it instead of it vanishing.
                Report(exception);
            }
#pragma warning disable CA1031 // the loop is the last thing between one bad command and a dead target
            catch (Exception)
#pragma warning restore CA1031
            {
                // Host work that threw. There is no protocol shape for a CLR exception — Runtime.exceptionThrown
                // carries a JavaScript error — so it is the host's own business, as it was before.
            }
        }
    }

    /// <summary>Reports without letting the report itself end the loop.</summary>
    private void Report(JavaScriptException exception)
    {
        try
        {
            ReportUncaughtException(exception);
        }
#pragma warning disable CA1031 // a failure to tell a client is not a reason to stop running the engine
        catch (Exception)
#pragma warning restore CA1031
        {
        }
    }

    /// <summary>
    /// The engine's own unhandled-rejection channel, which is where <c>Runtime.exceptionThrown</c> and
    /// <c>Runtime.exceptionRevoked</c> come from.
    /// </summary>
    /// <remarks>
    /// The engine raises <c>Reject</c> the moment a promise is rejected with nothing to handle it, and
    /// <c>Handle</c> if something handles it afterwards. V8 waits for the end of the microtask checkpoint
    /// before deciding, so a rejection handled on the very next line produces a throw and a revoke here
    /// where Chrome produces neither — which is exactly the pair <c>exceptionRevoked</c> exists for, and is
    /// why the identifier is remembered rather than the event delayed.
    /// </remarks>
    private void OnPromiseRejection(object? sender, PromiseRejectionTrackerEventArgs arguments)
    {
        var observers = Volatile.Read(ref _observers);
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

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);
    }

}
