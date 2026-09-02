using Jint.DevTools.Domains;
using Jint.DevTools.Session;

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

        // A handle is a promise to keep a value alive until the client releases it, and there is no client
        // left to release one. Dropping the references runs no engine code, so it is safe from here.
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
#pragma warning disable CA1031 // the loop is the last thing between one bad command and a dead target
            catch (Exception)
#pragma warning restore CA1031
            {
                // A host callback or a script that threw out of the pump must not end the thread: every
                // protocol command already answers its own failure, so what reaches here is host work, and
                // the next turn is still owed to every other client.
            }
        }
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);
    }

}
