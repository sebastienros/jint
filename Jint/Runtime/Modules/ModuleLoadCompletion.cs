using System.Threading;
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

        Settle(() => Build(() => ModuleFactory.BuildFromContents(Engine, Resolved, code)));
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

        Settle(() => Build(() => ModuleFactory.BuildFromContents(Engine, Resolved, bytes)));
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
    /// <paramref name="exception"/>'s message.
    /// </summary>
    public void SetError(Exception exception)
    {
        if (exception is null)
        {
            Throw.ArgumentNullException(nameof(exception));
        }

        // A JavaScriptException already carries the error value the host wants raised; anything else is a CLR
        // failure whose message is the only part meaningful to script.
        Settle(() => Fail(exception is JavaScriptException javaScriptException
            ? javaScriptException.Error
            : CreateError(exception.Message)));
    }

    /// <summary>
    /// Fails the load with an <c>Error</c> carrying <paramref name="message"/>.
    /// </summary>
    public void SetError(string message)
    {
        Settle(() => Fail(CreateError(message ?? "Could not load module")));
    }

    internal void AddWaiter(IScriptOrModule? referrer, string? referrerLocation, ModuleRequest request, ModuleLoadPayload payload)
    {
        _waiters.Add(new Waiter(referrer, referrerLocation, request, payload));
    }

    internal void OpenInlineSettleWindow() => _inlineSettleThreadId = Environment.CurrentManagedThreadId;

    internal void CloseInlineSettleWindow() => _inlineSettleThreadId = -1;

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

        Engine.EnqueueModuleLoadCompletion(onEngineThread, _generation);
    }

    private void Build(Func<Module> build)
    {
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
        }
        catch (JavaScriptException ex)
        {
            // Parsing the source the host handed over is engine work, so a syntax error in it belongs to the
            // importer as a rejection rather than to whichever turn of the event loop ran the build.
            Fail(ex.Error);
            return;
        }
        catch
        {
            // Not a failure to interpret as a module-loading error, so it keeps propagating — but the load must
            // not stay registered as in flight, or a later import of the same specifier would attach to a
            // completion that can never settle.
            _modules.RemovePendingLoad(_cacheKey);
            throw;
        }

        _modules.RemovePendingLoad(_cacheKey);
        _modules.RegisterModule(_cacheKey, module);
        FinishWaiters(module, error: null);
    }

    private void Fail(JsValue error)
    {
        _modules.RemovePendingLoad(_cacheKey);
        FinishWaiters(module: null, error);
    }

    private void FinishWaiters(Module? module, JsValue? error)
    {
        // A waiter's continuation can import further modules and so append to this list; iterate by index so
        // that is not a concurrent-modification error, and so anything appended is served too.
        for (var i = 0; i < _waiters.Count; i++)
        {
            var waiter = _waiters[i];
            Engine._host.FinishLoadingImportedModule(waiter.Referrer, waiter.ReferrerLocation, waiter.Request, waiter.Payload, module, error);
        }
    }

    private Native.Object.ObjectInstance CreateError(string message) => Engine.Realm.Intrinsics.Error.Construct(message);

    private readonly record struct Waiter(
        IScriptOrModule? Referrer,
        string? ReferrerLocation,
        ModuleRequest Request,
        ModuleLoadPayload Payload);
}
