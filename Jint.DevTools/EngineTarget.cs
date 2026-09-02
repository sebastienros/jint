using Jint.Runtime;

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
/// <b>One engine, for the target's whole life.</b> A page replaces its engine on every navigation; an engine
/// target never does, which is why every identifier it hands out stays valid until it is disposed.
/// </para>
/// <para>
/// <b>The target does not own the engine's lifetime.</b> Disposing it stops the thread it started, if it
/// started one, and stops answering commands; the <see cref="Engine"/> is the host's, before and after.
/// </para>
/// </remarks>
public sealed class EngineTarget : DevToolsTarget, IAsyncDisposable
{
    /// <summary>
    /// How long the library-owned loop parks before looking again. It is woken by every arrival, so this
    /// bounds nothing but how quickly a cancellation with no work behind it is noticed.
    /// </summary>
    private static readonly TimeSpan IdleInterval = TimeSpan.FromMilliseconds(100);

    /// <summary>The longest a single wait may ask for, which is what the blocking primitives accept.</summary>
    private static readonly TimeSpan MaxWait = TimeSpan.FromMilliseconds(int.MaxValue);

    private readonly CancellationTokenSource? _stopping;
    private readonly Thread? _thread;
    private readonly ManualResetEventSlim? _debuggerWaitEnded;

    private int _disposed;

    /// <summary>Creates a target over <paramref name="engine"/>.</summary>
    /// <param name="engine">The engine a client attaches to.</param>
    /// <param name="options">How the target presents itself and which thread runs it, or the defaults.</param>
    /// <exception cref="ArgumentNullException"><paramref name="engine"/> is <see langword="null"/>.</exception>
    public EngineTarget(Engine engine, EngineTargetOptions? options = null)
        : this(options ?? new EngineTargetOptions(), engine)
    {
    }

    /// <summary>Builds the target once the options are known to be there, so each is read exactly once.</summary>
    /// <remarks>
    /// The URL goes through the same mapping a script's own source name does, so a host that names its
    /// target after the file it runs sees one URL in <c>/json/list</c> and in <c>Debugger.scriptParsed</c>
    /// rather than two spellings of one location.
    /// </remarks>
    private EngineTarget(EngineTargetOptions options, Engine engine)
        : base(
            type: "node",
            title: options.Title,
            url: Domains.ScriptUrl.From(options.Url),
            browserContextId: null,
            openerId: null,
            describer: options.RemoteObjectDescriber,
            waitForDebuggerOnStart: options.WaitForDebuggerOnStart)
    {
        if (engine is null)
        {
            Throw.ArgumentNull(nameof(engine));
        }

        Engine = engine;
        ThreadMode = options.ThreadMode;
        InstallRuntime(engine);

        if (options.WaitForDebuggerOnStart)
        {
            var released = new ManualResetEventSlim(initialState: false);
            _debuggerWaitEnded = released;
            DebuggerWaitEnded += released.Set;
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
    /// <remarks>
    /// Declared here as well as on the base so that the surface an embedder compiles against stays declared
    /// on the type an embedder holds; the base class is internal, and its members are the package's own.
    /// </remarks>
    public new string TargetId => base.TargetId;

    /// <summary>Gets the target's protocol type, which is always <c>node</c> for an engine target.</summary>
    /// <inheritdoc cref="TargetId" path="/remarks"/>
    public new string Type => base.Type;

    /// <summary>Gets the name a client lists the target under.</summary>
    /// <inheritdoc cref="TargetId" path="/remarks"/>
    public new string Title => base.Title;

    /// <summary>Gets the location a client shows for the target.</summary>
    /// <inheritdoc cref="TargetId" path="/remarks"/>
    public new string Url => base.Url;

    /// <summary>Gets which thread runs the engine and answers commands addressed to this target.</summary>
    public ThreadMode ThreadMode { get; }

    /// <summary>Gets whether work posted to the target is still held for a client that has not released it.</summary>
    /// <inheritdoc cref="TargetId" path="/remarks"/>
    public new bool IsWaitingForDebugger => base.IsWaitingForDebugger;

    /// <inheritdoc/>
    internal override CancellationToken StoppingToken => _stopping?.Token ?? CancellationToken.None;

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
    public new void ReportUncaughtException(JavaScriptException exception) => base.ReportUncaughtException(exception);

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

        Runtime.Dispatcher.Drain();
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
        Runtime.Dispatcher.Post(work);
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
        Runtime.Dispatcher.Post(engine =>
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
        // that nothing arrives after the release below. A target that is disposed while its engine is paused
        // has to let go of the pause too, or the thread inside it waits for a client nothing will deliver.
        ActiveDebugger?.Detach();

        if (_stopping is not null)
        {
            await _stopping.CancelAsync().ConfigureAwait(false);
        }

        if (_thread is not null && _thread.IsAlive)
        {
            _thread.Join(TimeSpan.FromSeconds(5));
        }

        // The subscriptions, the handles and the journal, all of them the runtime's.
        DisposeRuntime();

        _stopping?.Dispose();
        _debuggerWaitEnded?.Dispose();
    }

    /// <inheritdoc/>
    internal override ValueTask CloseAsync() => DisposeAsync();

    private void RunLoop()
    {
        var token = _stopping!.Token;

        while (!token.IsCancellationRequested)
        {
            try
            {
                Runtime.Dispatcher.Drain();
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

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);
    }
}
