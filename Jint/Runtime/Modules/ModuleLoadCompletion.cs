using System.Runtime.ExceptionServices;
using System.Threading;
using Jint.Constraints;
using Jint.Native;

namespace Jint.Runtime.Modules;

/// <summary>
/// The handle an <see cref="IAsyncModuleLoader"/> settles to finish a load it was asked to start. Settling it
/// is the asynchronous equivalent of returning from <see cref="IModuleLoader.LoadModule"/>, and drives the
/// specification's
/// <see href="https://tc39.es/ecma262/#sec-FinishLoadingImportedModule">FinishLoadingImportedModule</see>.
/// </summary>
/// <remarks>
/// <para>
/// Settle-once and callable from any thread. The engine is not thread-safe, so nothing a settle triggers runs
/// on the caller's thread: the outcome is recorded and a job is queued on the engine's event loop, and the
/// module is built, registered and handed to the waiting importers when the engine next gets a turn. Which is
/// also why <see cref="SetSource(string)"/> is the safer of the two ways to finish a load —
/// <see cref="SetModule"/> requires a module record the host built itself, and building one touches the
/// engine.
/// </para>
/// <para>
/// The one exception is a settle that happens before <see cref="IAsyncModuleLoader.LoadModuleAsync"/> has
/// returned: the calling stack there is the engine's own, waiting for exactly this answer, so the load is
/// finished on it directly — no queue, no turn. A loader whose answer is already at hand therefore behaves
/// exactly like a synchronous <see cref="IModuleLoader"/>, and a graph of such answers makes the blocking
/// <c>Engine.Modules.Import</c> fully synchronous.
/// </para>
/// <para>
/// A settle arriving after the engine has ended the evaluation cycle it was registered in — see
/// <c>Engine.Advanced.RestoreGlobalSnapshot</c> — is discarded rather than applied to the restored engine,
/// the same fence every other cross-thread completion in Jint sits behind. The importing promise from that
/// cycle simply never settles.
/// </para>
/// </remarks>
public sealed class ModuleLoadCompletion
{
    private readonly Engine.ModuleOperations _modules;
    private readonly Engine.ModuleCacheKey _cacheKey;

    /// <summary>
    /// The evaluation cycle this load was registered in, captured at registration rather than read at settle
    /// time: the two differ exactly when the engine's globals were restored while the load was in flight, and
    /// that is the case the fence exists for.
    /// </summary>
    private readonly int _generation;

    /// <summary>
    /// The realm the load was started in, captured at registration for the same reason as the generation. A
    /// ShadowRealm import enters the shadow realm's execution context only around the synchronous
    /// <c>HostLoadImportedModule</c> call, so a settle arriving on a later event-loop turn runs under
    /// whatever realm is ambient then — the principal realm — and would build the module record, and mint
    /// error objects, against the wrong realm's intrinsics and globals. The deferred halves below re-enter
    /// this realm before doing either, so an asynchronously loaded module lands in the same realm a
    /// synchronously loaded one always has.
    /// </summary>
    private readonly Realm _realm;
    private readonly MemoryLimitConstraint.OperationState? _memoryState;
    private readonly ParsingConstraints _parsingConstraints;

    /// <summary>
    /// The engine cancellation token that governed the load when it was registered. A deferred settle can run
    /// on a background thread while an async host operation owns the engine, so it must neither enter the
    /// engine to rediscover this state nor observe a token installed for a later operation.
    /// </summary>
    private readonly CancellationToken _cancellationToken;

    private readonly List<Waiter> _waiters = new();
    private int _settled;

    /// <summary>
    /// The id of the thread whose settle may run inline instead of being queued, or -1. Non-negative exactly
    /// while the engine is inside its own call to <see cref="IAsyncModuleLoader.LoadModuleAsync"/>: the engine
    /// is at a known-safe point then — it initiated the call and is waiting for it to return — so a loader
    /// that can answer without waiting continues the load on the caller's stack, the way a synchronous
    /// <see cref="IModuleLoader"/>'s answer always has. A settle from any other thread, or from this thread
    /// once the call has returned, is queued: "the engine thread" is not an identity the engine tracks, and a
    /// late settle can arrive while that very thread is mid-evaluation somewhere else entirely.
    /// </summary>
    private volatile int _inlineSettleThreadId = -1;

    internal ModuleLoadCompletion(Engine.ModuleOperations modules, ResolvedSpecifier resolved, Engine.ModuleCacheKey cacheKey)
    {
        _modules = modules;
        _cacheKey = cacheKey;
        Resolved = resolved;
        Engine = modules.Engine;
        _generation = Engine.EventLoopGeneration;
        _realm = Engine.Realm;
        _memoryState = Engine.CaptureMemoryLimitState();
        _parsingConstraints = Engine.GetActiveParsingConstraints();
        _cancellationToken = Engine.Constraints.Find<CancellationConstraint>()?.Token ?? CancellationToken.None;
    }

    /// <summary>The engine the module is being loaded for.</summary>
    public Engine Engine { get; }

    /// <summary>The resolved specifier the load was started for.</summary>
    public ResolvedSpecifier Resolved { get; }

    /// <summary>Whether this load has already been settled. A second settle is ignored.</summary>
    public bool IsCompleted => Volatile.Read(ref _settled) != 0;

    /// <summary>
    /// Finishes the load with module source text, which the engine turns into a module record of the kind the
    /// request's import attributes ask for — the same dispatch <see cref="IModuleLoader.LoadModule"/> performs
    /// — on the engine thread.
    /// </summary>
    public void SetSource(string code)
    {
        if (code is null)
        {
            Throw.ArgumentNullException(nameof(code));
        }

        Settle(() => Build(() =>
        {
            _modules.EnsureModuleRegistrationAllowed(
                _cacheKey,
                System.Text.Encoding.UTF8.GetByteCount(code));
            return ModuleFactory.BuildFromContents(Engine, Resolved, code, _parsingConstraints);
        }));
    }

    /// <summary>
    /// Finishes the load with raw module content. Bytes modules (<c>with { type: "bytes" }</c>) take them as
    /// they are; anything else is decoded as UTF-8.
    /// </summary>
    public void SetSource(byte[] bytes)
    {
        if (bytes is null)
        {
            Throw.ArgumentNullException(nameof(bytes));
        }

        Settle(() => Build(() =>
        {
            _modules.EnsureModuleRegistrationAllowed(_cacheKey, bytes.Length);
            return ModuleFactory.BuildFromContents(Engine, Resolved, bytes, _parsingConstraints);
        }));
    }

    /// <summary>
    /// Finishes the load with a module record the host built itself, e.g. through <see cref="ModuleFactory"/>.
    /// </summary>
    /// <remarks>
    /// The record must have been built for <see cref="Engine"/>: a <see cref="Module"/> holds its engine and
    /// realm, and sharing one across engines is unsupported. Parsing is safe to do on a worker thread —
    /// <c>Engine.PrepareModule</c> exists for that, and its result may be shared — but the
    /// <see cref="ModuleFactory"/> call that turns it into a record should happen on the engine thread, so
    /// prefer <see cref="SetSource(string)"/> and let the engine make that call at the right moment.
    /// </remarks>
    public void SetModule(Module module)
    {
        if (module is null)
        {
            Throw.ArgumentNullException(nameof(module));
        }

        Settle(() => Build(() => module));
    }

    /// <summary>
    /// Fails the load. Every importer waiting on it — the promise from a dynamic <c>import()</c>, the load
    /// phase of a static import graph — is rejected with an <c>Error</c> carrying
    /// <paramref name="exception"/>'s message. Engine resource-limit and cancellation exceptions propagate
    /// instead, because turning them into a script-catchable rejection would defeat the bound.
    /// </summary>
    public void SetError(Exception exception)
    {
        if (exception is null)
        {
            Throw.ArgumentNullException(nameof(exception));
        }

        if (MustPropagateLoaderException(exception, _cancellationToken))
        {
            Settle(() => Propagate(exception));
            return;
        }

        // A JavaScriptException already carries the error value the host wants raised — and travels along so
        // a blocking importer can rethrow it with its location and stack intact; anything else is a CLR
        // failure whose message is the only part meaningful to script.
        var javaScriptException = exception as JavaScriptException;
        Settle(() => Fail(javaScriptException, javaScriptException?.Error, exception.Message));
    }

    /// <summary>
    /// Fails the load with an <c>Error</c> carrying <paramref name="message"/>.
    /// </summary>
    public void SetError(string message)
    {
        Settle(() => Fail(exception: null, error: null, message ?? "Could not load module"));
    }

    internal void SetConstraintError(Exception exception)
    {
        Settle(() =>
        {
            _modules.RemovePendingLoad(_cacheKey);
            _waiters.Clear();
            ExceptionDispatchInfo.Capture(exception).Throw();
        });
    }

    internal void AddWaiter(IScriptOrModule? referrer, string? referrerLocation, ModuleRequest request, ModuleLoadPayload payload)
    {
        _waiters.Add(new Waiter(referrer, referrerLocation, request, payload));
    }

    internal void OpenInlineSettleWindow() => _inlineSettleThreadId = Environment.CurrentManagedThreadId;

    internal void CloseInlineSettleWindow() => _inlineSettleThreadId = -1;

    internal CancellationToken CancellationToken => _cancellationToken;

    private void Settle(Action onEngineThread)
    {
        if (Interlocked.CompareExchange(ref _settled, 1, 0) != 0)
        {
            return;
        }

        // A settle from inside the engine's own LoadModuleAsync call runs here and now, so a loader that can
        // answer without waiting — a cache in front of the network, source already in hand — finishes the
        // load on this very stack and a synchronous Import over such answers never touches the event loop.
        // The generation check keeps the restore fence airtight even for the pathological loader that
        // restores a snapshot from inside LoadModuleAsync before settling.
        if (_inlineSettleThreadId == Environment.CurrentManagedThreadId && _generation == Engine.EventLoopGeneration)
        {
            onEngineThread();
            return;
        }

        Engine.EnqueueModuleLoadCompletion(onEngineThread, _generation, _memoryState);
    }

    private void Build(Func<Module> build)
    {
        var previousParsingConstraints = Engine._parsingConstraintsOverride;
        Engine._parsingConstraintsOverride = _parsingConstraints;
        var enteredRealm = EnterLoadRealm();
        try
        {
            // A module may have been registered for this key while the fetch was in flight — Modules.Add of
            // the same name followed by a synchronous lookup that consumed the builder. The registry is never
            // evicted and every synchronous path already answers with that record, so the waiters get it too:
            // registering the fetched record over it would leave two live modules for one key, each with its
            // own top-level state.
            if (_modules.TryGetRegisteredModule(_cacheKey, out var registered))
            {
                _modules.RemovePendingLoad(_cacheKey);
                FinishWaiters(registered, error: null, exception: null);
                return;
            }

            Module module;
            try
            {
                module = build();

                // Attach the host-defined [[ModuleSource]] used by source-phase imports, so a module reached
                // asynchronously supports them exactly as far as a synchronously loaded one does.
                if (_modules.ModuleLoader is ModuleLoader moduleLoader)
                {
                    module.ModuleSource ??= moduleLoader.GetModuleSourceForAsyncLoad(Engine, Resolved);
                }

                // Registration is inside the try so a throwing debugger callback cannot leave the waiters
                // stranded below with the module half-registered above them.
                _modules.RemovePendingLoad(_cacheKey);
                _modules.RegisterModuleWithAccounting(_cacheKey, module, module.SourceByteLength);
            }
            catch (JavaScriptException ex)
            {
                // Parsing the source the host handed over is engine work, so a syntax error in it belongs to
                // the importer as a rejection rather than to whichever turn of the event loop ran the build.
                Fail(ex, ex.Error, ex.Message);
                return;
            }
            catch (Exception ex) when (Engine.EventLoop.IsRunningJob && !MustPropagate(ex))
            {
                // On a queued event-loop turn there is no caller left to throw to: escaping would erupt out
                // of ProcessTasks with every waiter permanently pending. The failure becomes the load's
                // failure instead — and carries the original exception, so a blocking importer driving the
                // loop still catches what a synchronous stack would have thrown.
                Fail(ex, error: null, ex.Message);
                return;
            }
            catch
            {
                // A constraint exception, or a failure on a synchronous stack that still has a caller: it
                // keeps propagating — but the load must not stay registered as in flight, or a later import
                // of the same specifier would attach to a completion that can never settle.
                _modules.RemovePendingLoad(_cacheKey);
                _waiters.Clear();
                throw;
            }

            FinishWaiters(module, error: null, exception: null);
        }
        finally
        {
            LeaveLoadRealm(enteredRealm);
            Engine._parsingConstraintsOverride = previousParsingConstraints;
        }
    }

    private void Fail(Exception? exception, JsValue? error, string message)
    {
        var enteredRealm = EnterLoadRealm();
        try
        {
            _modules.RemovePendingLoad(_cacheKey);
            FinishWaiters(module: null, error ?? CreateError(message), exception);
        }

        finally
        {
            LeaveLoadRealm(enteredRealm);
        }
    }

    private void Propagate(Exception exception)
    {
        _modules.RemovePendingLoad(_cacheKey);
        _waiters.Clear();
        ExceptionDispatchInfo.Capture(exception).Throw();
    }

    private void FinishWaiters(Module? module, JsValue? error, Exception? exception)
    {
        if (error is not null && exception is not null)
        {
            // Keeps the original exception reachable for the blocking import paths, which otherwise can only
            // rebuild a JavaScriptException from the error value — losing its location, stack and Data.
            _modules.RememberLoadFailure(error, exception);
        }

        // A waiter's continuation can import further modules and so append to this list; iterate by index so
        // that is not a concurrent-modification error, and so anything appended is served too. One waiter's
        // continuation throwing — a constraint firing inside Link, say — must not leave the waiters after it
        // permanently pending: the pending-load entry is already gone and the module registered, so nothing
        // would ever finish them. Serve everyone, then let the first failure propagate.
        ExceptionDispatchInfo? failure = null;
        for (var i = 0; i < _waiters.Count; i++)
        {
            var waiter = _waiters[i];
            try
            {
                Engine._host.FinishLoadingImportedModule(waiter.Referrer, waiter.ReferrerLocation, waiter.Request, waiter.Payload, module, error);
            }
            catch (Exception ex)
            {
                failure ??= ExceptionDispatchInfo.Capture(ex);
            }
        }

        // A settled completion is what a host naturally retains to correlate with its transport callbacks;
        // the served waiters must not keep their referrer modules and promise-capability graphs alive
        // through it.
        _waiters.Clear();

        failure?.Throw();
    }

    /// <summary>
    /// Enters the captured load realm when it is not already the ambient one, so everything a deferred
    /// settle does — building the module record, minting an error, running the waiters' continuations —
    /// observes the realm the import was started in, exactly as the synchronous loader path does by running
    /// inside the importer's own stack.
    /// </summary>
    private bool EnterLoadRealm()
    {
        if (ReferenceEquals(Engine.Realm, _realm))
        {
            return false;
        }

        Engine.EnterExecutionContext(_realm.GlobalEnv, _realm.GlobalEnv, _realm, privateEnvironment: null, strict: Engine.Options.Strict);
        return true;
    }

    private void LeaveLoadRealm(bool entered)
    {
        if (entered)
        {
            Engine.LeaveExecutionContext();
        }
    }

    /// <summary>
    /// The failures that must keep propagating rather than become a module-load rejection: each one exists to
    /// bound or abort execution, and a constraint that turns into a rejection no longer bounds anything —
    /// script observes it as an ordinary failed import and carries on, in a loop if it likes.
    /// </summary>
    /// <remarks>
    /// The list itself lives on <see cref="ConstraintFailure"/>, because <c>fetch</c>'s settle job needs the
    /// same one and the two must not be able to drift. <see cref="RecursionDepthOverflowException"/> is on it
    /// for the same reason as the rest, and reaches <i>these</i> paths whenever the loader itself re-enters
    /// the engine — a resolve hook or a virtual file system written in script, which is a shape hosts really
    /// do use. It is not raised by anything the load pipeline does on its own; a module <em>body</em> that
    /// recurses too deeply fails outside every one of these catches.
    /// </remarks>
    internal static bool MustPropagate(Exception exception) => ConstraintFailure.MustPropagate(exception);

    internal static bool MustPropagateLoaderException(Engine engine, Exception exception)
        => MustPropagateLoaderException(
            exception,
            engine.Constraints.Find<CancellationConstraint>()?.Token ?? CancellationToken.None);

    private static bool MustPropagateLoaderException(Exception exception, CancellationToken cancellationToken)
    {
        if (exception is ExecutionCanceledException
            or ParsingLimitException
            or MemoryLimitExceededException
            or StatementsCountOverflowException
            or RecursionDepthOverflowException
            or ModuleGraphLimitException
            or System.Text.RegularExpressions.RegexMatchTimeoutException
            or OutOfMemoryException)
        {
            return true;
        }

        if (exception is OperationCanceledException)
        {
            return Throw.IsEngineAbortException(exception)
                   || cancellationToken.IsCancellationRequested;
        }

        return exception is TimeoutException && Throw.IsEngineAbortException(exception);
    }

    private Native.Object.ObjectInstance CreateError(string message) => Engine.Realm.Intrinsics.Error.Construct(message);

    private readonly record struct Waiter(
        IScriptOrModule? Referrer,
        string? ReferrerLocation,
        ModuleRequest Request,
        ModuleLoadPayload Payload);
}
