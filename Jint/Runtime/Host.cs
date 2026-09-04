using Jint.Native;
using Jint.Native.Global;
using Jint.Native.Object;
using Jint.Native.Promise;
using Jint.Runtime.Descriptors;
using Jint.Runtime.Environments;
using Jint.Runtime.Interop;
using Jint.Runtime.Modules;

namespace Jint.Runtime;

public class Host
{
    private Engine? _engine;
    private readonly List<string> _supportedImportAttributes = ["type"];

    protected Engine Engine
    {
        get
        {
            if (_engine is null)
            {
                Throw.InvalidOperationException("Initialize has not been called");
            }
            return _engine!;
        }
        private set => _engine = value;
    }

    /// <summary>
    /// Initializes the host.
    /// </summary>
    public void Initialize(Engine engine)
    {
        Engine = engine;
        InitializeHostDefinedRealm();
        PostInitialize();
    }

    protected virtual void PostInitialize()
    {
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-initializehostdefinedrealm
    /// </summary>
    protected virtual void InitializeHostDefinedRealm()
    {
        var realm = CreateRealm();

        var newContext = new ExecutionContext(
            scriptOrModule: null,
            lexicalEnvironment: realm.GlobalEnv,
            variableEnvironment: realm.GlobalEnv,
            privateEnvironment: null,
            realm: realm,
            function: null,
            // The base global context is pushed once at realm init and never popped; it governs
            // strictness for any code that runs before an inner frame is pushed (e.g. engine.Invoke
            // argument handling). _isStrict is assigned after this runs, so read Options directly.
            strict: Engine.Options.Strict);

        Engine.EnterExecutionContext(in newContext);
    }

    internal virtual GlobalEnvironment CreateGlobalEnvironment(ObjectInstance globalObject)
    {
        return JintEnvironment.NewGlobalEnvironment(Engine, globalObject, globalObject);
    }

    protected virtual ObjectInstance CreateGlobalObject(Realm realm)
    {
        var globalObject = new GlobalObject(Engine, realm);
        // Because the properties might need some of the built-in object
        // their configuration is delayed to a later step
        // trigger initialization
        globalObject.EnsureInitialized();
        return globalObject;
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-createrealm
    /// </summary>
    protected internal virtual Realm CreateRealm()
    {
        var realmRec = new Realm();
        Engine._realmInConstruction = realmRec;

        CreateIntrinsics(realmRec);

        var globalObject = CreateGlobalObject(realmRec);

        var globalEnv = CreateGlobalEnvironment(globalObject);
        realmRec.GlobalEnv = globalEnv;
        realmRec.GlobalObject = globalObject;

        Engine._realmInConstruction = null!;

        return realmRec;
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-createintrinsics
    /// </summary>
    protected virtual void CreateIntrinsics(Realm realmRec)
    {
        var intrinsics = new Intrinsics(Engine, realmRec);
        realmRec.Intrinsics = intrinsics;
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-hostensurecancompilestrings
    /// </summary>
    public virtual void EnsureCanCompileStrings(Realm callerRealm, Realm evalRealm)
    {
        if (!Engine.Options.Host.StringCompilationAllowed)
        {
            Throw.JavaScriptException(callerRealm.Intrinsics.TypeError, "String compilation has been disabled in engine options");
        }
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-GetImportedModule
    /// </summary>
    /// <remarks>
    /// The spec asserts the referrer's <c>[[LoadedModules]]</c> holds an entry for the request: everything
    /// that asks has already been through the load phase. The fallback covers the paths that predate it — a
    /// host calling <see cref="ModuleRecord.Link"/> or <see cref="ModuleRecord.Evaluate"/> on a module it built itself —
    /// and is a plain synchronous load, exactly what those paths used to do.
    /// </remarks>
    internal virtual ModuleRecord GetImportedModule(IScriptOrModule? referrer, ModuleRequest request)
    {
        if (Engine.Modules.TryGetLoadedModule(referrer, request, out var module))
        {
            return module;
        }

        return Engine.Modules.Load(referrer?.Location, request);
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-HostLoadImportedModule
    /// </summary>
    /// <remarks>
    /// May complete asynchronously: a host whose <see cref="IModuleLoader"/> also implements
    /// <see cref="IAsyncModuleLoader"/> starts the load and returns, and the engine calls
    /// <see cref="FinishLoadingImportedModule"/> once the host settles the
    /// <see cref="ModuleLoadCompletion"/> it was handed. A synchronous loader is finished inline, which is
    /// what keeps every existing <see cref="IModuleLoader"/> behaving exactly as before.
    /// </remarks>
    internal virtual void LoadImportedModule(IScriptOrModule? referrer, ModuleRequest moduleRequest, ModuleLoadPayload payload)
    {
        Engine.Modules.LoadImportedModule(referrer, referrer?.Location, moduleRequest, payload);
    }

    /// <summary>
    /// <see cref="LoadImportedModule(IScriptOrModule?,ModuleRequest,ModuleLoadPayload)"/> for a load the host
    /// started itself through <c>Engine.Modules.Import</c>, which resolves against a location without having a
    /// referrer record to take it from.
    /// </summary>
    internal virtual void LoadImportedModule(IScriptOrModule? referrer, string? referrerLocation, ModuleRequest moduleRequest, ModuleLoadPayload payload)
    {
        Engine.Modules.LoadImportedModule(referrer, referrerLocation, moduleRequest, payload);
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-FinishLoadingImportedModule
    /// </summary>
    /// <param name="referrer">The script or module the request came from, or null.</param>
    /// <param name="referrerLocation">
    /// The location resolution was performed against. Equal to <c>referrer.Location</c> except for a load the
    /// host started itself through <c>Engine.Modules.Import</c>, which has a location but no referrer record.
    /// </param>
    /// <param name="moduleRequest">The request that was loaded.</param>
    /// <param name="payload">The state the load was started for; consumes the completion.</param>
    /// <param name="module">The loaded module on a normal completion, otherwise null.</param>
    /// <param name="error">The error value on a throw completion, otherwise null.</param>
    internal virtual void FinishLoadingImportedModule(
        IScriptOrModule? referrer,
        string? referrerLocation,
        ModuleRequest moduleRequest,
        ModuleLoadPayload payload,
        ModuleRecord? module,
        JsValue? error)
    {
        if (error is null)
        {
            // Step 1: record the answer against the referrer, so this referrer/specifier pair is never
            // handed to the loader a second time and can never be answered differently.
            Engine.Modules.RecordLoadedModule(referrer, referrerLocation, moduleRequest, module!);
        }

        payload.Continue(module, error);
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-ContinueDynamicImport
    /// </summary>
    /// <remarks>
    /// The rest of a dynamic <c>import()</c> once its root module has loaded: load the requested modules,
    /// then link, then evaluate, then resolve with the namespace. Nothing here blocks — each stage is chained
    /// onto the previous stage's promise, so a load that is still in flight simply means the next stage runs
    /// on a later event-loop turn.
    /// </remarks>
    internal virtual void ContinueDynamicImport(
        ModuleRecord module,
        ModuleRequest moduleRequest,
        PromiseCapability payload,
        ModuleLoadBudget budget)
    {
        // Step 3 of https://tc39.es/proposal-source-phase-imports/#sec-ContinueDynamicImport: a source-phase
        // import settles here and goes no further — the module is never linked or evaluated. It resolves with
        // the module's [[ModuleSource]], and rejects with a SyntaxError only when that slot is empty, which is
        // the case for every ECMA-262 module record (a JavaScript module has no source representation).
        if (moduleRequest.Phase == ModuleImportPhase.Source)
        {
            var moduleSource = module.ModuleSource;
            if (moduleSource is null)
            {
                payload.Reject(Engine.Realm.Intrinsics.SyntaxError.Construct("Source phase import is not supported for this module"));
            }
            else
            {
                payload.Resolve(moduleSource);
            }

            return;
        }

        var onRejected = new ClrFunction(Engine, "", (thisObj, args) =>
        {
            payload.Reject(args.At(0));
            return JsValue.Undefined;
        }, 1, PropertyFlag.Configurable);

        var linkAndEvaluate = new ClrFunction(Engine, "", (thisObj, args) =>
        {
            LinkAndEvaluateForDynamicImport(module, moduleRequest, payload);
            return JsValue.Undefined;
        }, 0, PropertyFlag.Configurable);

        JsValue loadResult;
        try
        {
            loadResult = module is CyclicModuleRecord cyclicModule
                ? cyclicModule.LoadRequestedModulesWithBudget(budget)
                : module.LoadRequestedModules();
        }
        catch (JavaScriptException ex)
        {
            payload.Reject(ex.Error);
            return;
        }

        if (loadResult is not JsPromise loadPromise)
        {
            LinkAndEvaluateForDynamicImport(module, moduleRequest, payload);
            return;
        }

        PromiseOperations.PerformPromiseThen(Engine, loadPromise, linkAndEvaluate, onRejected, resultCapability: null!);
    }

    /// <summary>
    /// The <c>linkAndEvaluateClosure</c> of
    /// <see href="https://tc39.es/ecma262/#sec-ContinueDynamicImport">ContinueDynamicImport</see>.
    /// </summary>
    private void LinkAndEvaluateForDynamicImport(ModuleRecord moduleRecord, ModuleRequest moduleRequest, PromiseCapability payload)
    {
        try
        {
            // Link the module if not already linked/linking/evaluating
            if (moduleRecord is CyclicModuleRecord cyclicModule)
            {
                if (cyclicModule.Status == ModuleStatus.Unlinked)
                {
                    moduleRecord.Link();
                }
            }
            else
            {
                // Non-cyclic modules - safe to call Link
                moduleRecord.Link();
            }

            // Defer phase: link but don't fully evaluate, only evaluate async transitive deps.
            // See https://tc39.es/proposal-defer-import-eval/#sec-ContinueDynamicImport
            if (moduleRequest.Phase == ModuleImportPhase.Defer)
            {
                HandleDeferredImport(moduleRecord, payload);
                return;
            }

            // Evaluate returns a promise for async (TLA) modules
            var evaluateResult = moduleRecord.Evaluate();
            if (evaluateResult is not JsPromise evaluatePromise)
            {
                // Non-cyclic module - shouldn't happen but handle gracefully
                var ns = ModuleRecord.GetModuleNamespace(moduleRecord);
                payload.Resolve(ns);
                return;
            }

            if (evaluatePromise.State == PromiseState.Fulfilled)
            {
                // Sync completion - resolve immediately with namespace
                var ns = ModuleRecord.GetModuleNamespace(moduleRecord);
                payload.Resolve(ns);
            }
            else if (evaluatePromise.State == PromiseState.Rejected)
            {
                payload.Reject(evaluatePromise.Value);
            }
            else
            {
                // Pending - chain on the evaluation promise
                var onEvalFulfilled = new ClrFunction(Engine, "", (_, evalArgs) =>
                {
                    var ns = ModuleRecord.GetModuleNamespace(moduleRecord);
                    payload.Resolve(ns);
                    return JsValue.Undefined;
                }, 0, PropertyFlag.Configurable);

                var onEvalRejected = new ClrFunction(Engine, "", (_, evalArgs) =>
                {
                    payload.Reject(evalArgs.At(0));
                    return JsValue.Undefined;
                }, 1, PropertyFlag.Configurable);

                PromiseOperations.PerformPromiseThen(Engine, evaluatePromise,
                    onEvalFulfilled, onEvalRejected, resultCapability: null!);
            }
        }
        catch (JavaScriptException ex)
        {
            payload.Reject(ex.Error);
        }
    }

    /// <summary>
    /// Implements the defer-phase branch of
    /// <see href="https://tc39.es/proposal-defer-import-eval/#sec-ContinueDynamicImport">ContinueDynamicImport</see>:
    /// gather async transitive dependencies, await their evaluation, then resolve the payload with the deferred namespace.
    /// </summary>
    private void HandleDeferredImport(ModuleRecord moduleRecord, PromiseCapability payload)
    {
        var asyncDeps = new List<ModuleRecord>();
        if (moduleRecord is CyclicModuleRecord cm)
        {
            CyclicModuleRecord.GatherAsynchronousTransitiveDependencies(cm, asyncDeps);
        }

        if (asyncDeps.Count == 0)
        {
            // No async deps - resolve immediately with deferred namespace.
            payload.Resolve(ModuleRecord.GetModuleNamespace(moduleRecord, ModuleImportPhase.Defer));
            return;
        }

        // Evaluate all async deps, collect their promises, and classify states in a single pass.
        var evalPromises = new List<JsPromise>(asyncDeps.Count);
        var pending = 0;
        JsPromise? firstRejected = null;

        foreach (var dep in asyncDeps)
        {
            if (dep.Evaluate() is not JsPromise depPromise)
            {
                continue;
            }

            evalPromises.Add(depPromise);
            switch (depPromise.State)
            {
                case PromiseState.Rejected:
                    firstRejected ??= depPromise;
                    break;
                case PromiseState.Pending:
                    pending++;
                    break;
            }
        }

        // Promise.all semantics: reject on first rejection, otherwise wait for all to fulfill.
        if (firstRejected is not null)
        {
            payload.Reject(firstRejected.Value);
            return;
        }

        if (pending == 0)
        {
            payload.Resolve(ModuleRecord.GetModuleNamespace(moduleRecord, ModuleImportPhase.Defer));
            return;
        }

        // One-shot guard: once the payload is settled, subsequent handler firings are ignored.
        // The PromiseCapability itself enforces settle-once, but this avoids redundant work and
        // leaking onto the event loop if multiple deps settle near-simultaneously.
        var settled = false;
        var remaining = pending;
        var deferredModule = moduleRecord;

        var onDepFulfilled = new ClrFunction(Engine, "", (_, _) =>
        {
            if (settled)
            {
                return JsValue.Undefined;
            }

            if (--remaining == 0)
            {
                settled = true;
                payload.Resolve(ModuleRecord.GetModuleNamespace(deferredModule, ModuleImportPhase.Defer));
            }
            return JsValue.Undefined;
        }, 0, PropertyFlag.Configurable);

        var onDepRejected = new ClrFunction(Engine, "", (_, depArgs) =>
        {
            if (settled)
            {
                return JsValue.Undefined;
            }

            settled = true;
            payload.Reject(depArgs.At(0));
            return JsValue.Undefined;
        }, 1, PropertyFlag.Configurable);

        foreach (var evalPromise in evalPromises)
        {
            if (evalPromise.State != PromiseState.Pending)
            {
                continue;
            }
            PromiseOperations.PerformPromiseThen(Engine, evalPromise, onDepFulfilled, onDepRejected, resultCapability: null!);
        }
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-hostgetimportmetaproperties
    /// </summary>
    public virtual List<KeyValuePair<JsValue, JsValue>> GetImportMetaProperties(ModuleRecord moduleRecord)
    {
        return new List<KeyValuePair<JsValue, JsValue>>();
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-hostfinalizeimportmeta
    /// </summary>
    public virtual void FinalizeImportMeta(ObjectInstance importMeta, ModuleRecord moduleRecord)
    {
    }

    /// <summary>
    /// https://tc39.es/proposal-shadowrealm/#sec-hostinitializeshadowrealm
    /// </summary>
    public virtual void InitializeShadowRealm(Realm realm)
    {
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-hostmakejobcallback
    /// </summary>
    internal virtual JobCallback MakeJobCallBack(ICallable cleanupCallback)
    {
        return new JobCallback(cleanupCallback, null);
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-host-promise-rejection-tracker
    /// Called when a promise is rejected without a handler, or when a handler is
    /// added to a previously unhandled rejected promise.
    /// </summary>
    /// <param name="promise">The promise that was rejected.</param>
    /// <param name="operation">Whether the promise was rejected ("reject") or a handler was added ("handle").</param>
    internal virtual void HostPromiseRejectionTracker(JsPromise promise, PromiseRejectionOperation operation)
    {
        Engine.OnPromiseRejectionTracker(promise, operation);
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-hostenqueuepromisejob
    /// </summary>
    internal void HostEnqueuePromiseJob(Action job, Realm realm)
    {
        // A microtask: HTML implements this host hook by queueing one
        // (https://html.spec.whatwg.org/multipage/webappapis.html#hostenqueuepromisejob), which is what makes
        // a promise job run before the next task rather than behind it.
        Engine.AddToEventLoop(job, EventLoopJobKind.Microtask);
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-host-cleanup-finalization-registry
    /// <para>
    /// "An implementation of HostEnqueueFinalizationRegistryCleanupJob schedules cleanupJob to be performed
    /// at some future time, if possible." Jint schedules it on the event loop, so the callback runs on the
    /// engine's thread on a later turn — never on the CLR finalizer thread that discovered the collection,
    /// which is the only thread the discovery is available on and the one thread that must never enter the
    /// engine.
    /// </para>
    /// </summary>
    /// <param name="cleanupJob">The job, which runs <c>CleanupFinalizationRegistry</c> for one registry.</param>
    /// <param name="generation">
    /// The evaluation cycle the collected cell was registered in, so that a collection observed after a
    /// <c>RestoreGlobalSnapshot</c> is dropped rather than run against the restored globals. Callable from
    /// any thread for exactly that reason: the stamp is read at registration on the engine's thread and
    /// checked at dequeue on the engine's thread, and this enqueue in between is the only cross-thread step.
    /// </param>
    internal void HostEnqueueFinalizationRegistryCleanupJob(Action cleanupJob, int generation)
    {
        // A task, unlike the promise job above: HTML queues this one on a task source
        // (https://html.spec.whatwg.org/multipage/webappapis.html#hostenqueuefinalizationregistrycleanupjob),
        // so a cleanup callback never runs inside a microtask checkpoint.
        Engine.AddToEventLoop(cleanupJob, generation, EventLoopJobKind.Task);
    }

    internal virtual List<string> GetSupportedImportAttributes()
    {
        return _supportedImportAttributes;
    }
}

internal sealed record JobCallback(ICallable Callback, object? HostDefined);
