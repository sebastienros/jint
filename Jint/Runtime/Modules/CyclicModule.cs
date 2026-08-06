#nullable disable

using Jint.Native;
using Jint.Native.Promise;
using Jint.Runtime.Descriptors;
using Jint.Runtime.Interop;

namespace Jint.Runtime.Modules;

#pragma warning disable CS0649 // never assigned to, waiting for new functionalities in spec

internal sealed record ResolvedBinding(Module Module, string BindingName)
{
    internal static ResolvedBinding Ambiguous => new(null, "ambiguous");
}

/// <summary>
/// https://tc39.es/ecma262/#sec-cyclic-module-records
/// </summary>
public abstract class CyclicModule : Module
{
    private Completion? _evalError;
    private int _dfsAncestorIndex;
    internal HashSet<ModuleRequest> _requestedModules;
    private CyclicModule _cycleRoot;
    protected bool _hasTLA;
    private bool _asyncEvaluation;
    private PromiseCapability _topLevelCapability;
    private readonly List<CyclicModule> _asyncParentModules = [];
    private int _asyncEvalOrder;
    private int _pendingAsyncDependencies;

    internal JsValue _evalResult;
    private SourceLocation _abnormalCompletionLocation;

    internal CyclicModule(Engine engine, Realm realm, string location, bool isAsync) : base(engine, realm, location)
    {
        _hasTLA = isAsync;
    }

    internal ModuleStatus Status { get; private set; }

    internal ref readonly SourceLocation AbnormalCompletionLocation => ref _abnormalCompletionLocation;

    /// <summary>
    /// https://tc39.es/ecma262/#sec-LoadRequestedModules
    /// </summary>
    /// <remarks>
    /// The <c>hostDefined</c> parameter of the spec's signature is not modelled: Jint's
    /// <see cref="IModuleLoader"/> receives the <see cref="ResolvedSpecifier"/> and the engine, and has no
    /// second channel that would carry an opaque host value through the graph.
    /// </remarks>
    public override JsValue LoadRequestedModules()
    {
        var capability = PromiseConstructor.NewPromiseCapability(_engine, _realm.Intrinsics.Promise);

        // The load-phase promise is engine-internal plumbing: the blocking Import consumes its rejection by
        // rethrowing it without ever attaching a reaction, and every failure it carries is also delivered
        // through whichever import produced it. Left trackable, each such rejection would fire a phantom
        // unhandled-rejection event for a promise the host never created and cannot observe.
        ((JsPromise) capability.PromiseInstance).PromiseIsHandled = true;

        var state = new GraphLoadingState(capability);
        InnerModuleLoading(state, this);
        return capability.PromiseInstance;
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-InnerModuleLoading
    /// </summary>
    internal static void InnerModuleLoading(GraphLoadingState state, Module module)
    {
        if (!state.IsLoading)
        {
            Throw.InvalidOperationException("Error while loading module: the graph loading state is no longer loading");
        }

        if (module is CyclicModule cyclicModule
            && cyclicModule.Status == ModuleStatus.New
            && !state.Visited.Contains(cyclicModule))
        {
            state.Visited.Add(cyclicModule);
            state.PendingModulesCount += cyclicModule._requestedModules.Count;

            foreach (var request in cyclicModule._requestedModules)
            {
                if (cyclicModule.TryGetLoadedModule(request, out var loaded))
                {
                    InnerModuleLoading(state, loaded);
                }
                else
                {
                    // HostLoadImportedModule finishes the request - now or on a later turn - by calling
                    // FinishLoadingImportedModule, which re-enters this method through
                    // GraphLoadingState.Continue.
                    cyclicModule._engine._host.LoadImportedModule(cyclicModule, request, state);
                }

                // Step 2.d.iii: a failed sibling ends the whole load; the remaining requests must not be
                // started. Only reachable for a loader that answers synchronously - an asynchronous one has
                // not settled anything by the time control returns here.
                if (!state.IsLoading)
                {
                    return;
                }
            }
        }

        state.SettleOnePendingModule();
    }

    /// <summary>
    /// Step 5.b.i of <see href="https://tc39.es/ecma262/#sec-InnerModuleLoading">InnerModuleLoading</see>:
    /// the graph this module belongs to has finished loading, so it becomes linkable.
    /// </summary>
    internal void OnGraphLoaded()
    {
        if (Status == ModuleStatus.New)
        {
            Status = ModuleStatus.Unlinked;
        }
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-moduledeclarationlinking
    /// </summary>
    public override void Link()
    {
        if (Status is ModuleStatus.Linking or ModuleStatus.Evaluating)
        {
            Throw.InvalidOperationException("Error while linking module: Module is already either linking or evaluating");
        }

        if (Status == ModuleStatus.New)
        {
            // The spec asserts the load phase has already run. Link() is public and predates it, so a host
            // driving a module by hand - ModuleFactory.BuildSourceTextModule, then Link(), then Evaluate() -
            // legitimately arrives here without one; run it now so that keeps working. A synchronous loader
            // settles the promise before it returns, which is exactly the pre-load this method used to do
            // from inside InnerModuleLinking. An asynchronous one cannot, and has to be driven through
            // LoadRequestedModules by a caller able to wait for the promise.
            var loadResult = LoadRequestedModules();
            if (loadResult is JsPromise loadPromise)
            {
                if (loadPromise.State == PromiseState.Pending)
                {
                    Throw.InvalidOperationException(
                        $"Error while linking module '{Location ?? "(null)"}': the module graph is still loading. An asynchronous module loader requires the load phase to complete before linking - await the promise returned by LoadRequestedModules(), or import the module through Engine.Modules.ImportAsync.");
                }

                if (loadPromise.State == PromiseState.Rejected)
                {
                    _engine.Modules.RethrowLoadFailure(loadPromise.Value);
                    Throw.JavaScriptException(_engine, loadPromise.Value, in AstExtensions.DefaultLocation);
                }
            }
        }

        var stack = new Stack<CyclicModule>();

        try
        {
            InnerModuleLinking(stack, 0);
        }
        catch
        {
            foreach (var m in stack)
            {
                m._environment = null;

                if (m.Status != ModuleStatus.Linking)
                {
                    Throw.InvalidOperationException("Error while linking module: Module should be linking after abrupt completion");
                }

                m.Status = ModuleStatus.Unlinked;
                m._dfsAncestorIndex = -1;
            }

            if (Status != ModuleStatus.Unlinked)
            {
                Throw.InvalidOperationException("Error while processing abrupt completion of module link: Module should be unlinked after cleanup");
            }

            throw;
        }

        if (Status is not (ModuleStatus.Linked or ModuleStatus.EvaluatingAsync or ModuleStatus.Evaluated))
        {
            Throw.InvalidOperationException("Error while linking module: Module is neither linked, evaluating-async or evaluated");
        }

        if (stack.Count > 0)
        {
            Throw.InvalidOperationException("Error while linking module: One or more modules were not linked");
        }
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-moduleevaluation
    /// </summary>
    public override JsValue Evaluate()
    {
        var module = this;

        // https://tc39.es/ecma262/#sec-moduleevaluation
        // Step 3: If module.[[Status]] is either evaluating-async or evaluated, then
        //   a. If module.[[CycleRoot]] is not empty, then
        //      i. Set module to module.[[CycleRoot]].
        //   b. Else,
        //      i. Assert: module.[[Status]] is evaluated and module.[[EvaluationError]] is a throw completion.
        // The guard is load-bearing: only the InnerModuleEvaluation pop loop assigns [[CycleRoot]],
        // and a module marked evaluated by step 9.a below - because a dependency threw before the
        // loop could reach it - never got one. Such a module stays the subject of the evaluation and
        // replays its own recorded [[EvaluationError]] through InnerModuleEvaluation.
        if ((module.Status is ModuleStatus.EvaluatingAsync or ModuleStatus.Evaluated) && module._cycleRoot is not null)
        {
            module = module._cycleRoot;
        }

        // Step 5: If module.[[TopLevelCapability]] is not empty, return module.[[TopLevelCapability]].[[Promise]].
        // This handles re-entrant calls (e.g., a module importing itself during evaluation).
        if (module._topLevelCapability is not null)
        {
            return module._topLevelCapability.PromiseInstance;
        }

        // Step 3 (assertion): Assert: module.[[Status]] is one of linked, evaluating-async, or evaluated.
        // Note: The spec only allows these statuses for a NEW evaluation. If we reach here, the module
        // must be ready to start evaluation (Linked) or in a valid async/evaluated state.
        if (module.Status != ModuleStatus.Linked &&
            module.Status != ModuleStatus.EvaluatingAsync &&
            module.Status != ModuleStatus.Evaluated)
        {
            Throw.InvalidOperationException("Error while evaluating module: Module is in an invalid state");
        }

        var stack = new Stack<CyclicModule>();
        var capability = PromiseConstructor.NewPromiseCapability(_engine, _realm.Intrinsics.Promise);
        // Per spec, [[ModuleAsyncEvaluationCount]] is agent-level, not per-Evaluate() call.
        // This ensures correct ordering across dynamic import() calls.
        ref var asyncEvalOrder = ref _engine.ModuleAsyncEvaluationCount;
        module._topLevelCapability = capability;

        var result = module.InnerModuleEvaluation(stack, 0, ref asyncEvalOrder);

        if (result.Type != CompletionType.Normal)
        {
            foreach (var m in stack)
            {
                m.Status = ModuleStatus.Evaluated;
                m._evalError = result;
            }

            // A completion recorded by AsyncModuleExecutionRejected carries only the thrown value and has
            // no associated AST node (source is default). Re-evaluating an errored top-level-await cycle
            // root - e.g. a dynamic import of a fulfilled member of an already-errored cycle - returns that
            // recorded [[EvaluationError]] here, so only capture a location when the completion has one.
            if (result._source is not null)
            {
                _abnormalCompletionLocation = result.Location;
            }

            capability.Reject(result.Value);
        }
        else
        {
            if (module.Status != ModuleStatus.EvaluatingAsync && module.Status != ModuleStatus.Evaluated)
            {
                Throw.InvalidOperationException("Error while evaluating module: Module is in an invalid state");
            }

            if (module._evalError is not null)
            {
                Throw.InvalidOperationException("Error while evaluating module: Module is in an invalid state");
            }

            if (!module._asyncEvaluation)
            {
                if (module.Status != ModuleStatus.Evaluated)
                {
                    Throw.InvalidOperationException("Error while evaluating module: Module is in an invalid state");
                }

                capability.Resolve(JsValue.Undefined);
            }

            if (stack.Count > 0)
            {
                Throw.InvalidOperationException("Error while evaluating module: Module is in an invalid state");
            }
        }

        return capability.PromiseInstance;
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-InnerModuleLinking
    /// </summary>
    protected internal override int InnerModuleLinking(Stack<CyclicModule> stack, int index)
    {
        if (Status is
            ModuleStatus.Linking or
            ModuleStatus.Linked or
            ModuleStatus.EvaluatingAsync or
            ModuleStatus.Evaluated)
        {
            return index;
        }

        if (Status != ModuleStatus.Unlinked)
        {
            Throw.InvalidOperationException($"Error while linking module: Module in an invalid state: {Status}");
        }

        Status = ModuleStatus.Linking;
        var moduleIndex = index;
        _dfsAncestorIndex = index;
        index++;
        stack.Push(this);

        // Loading errors used to be reported before linking errors by pre-loading every requested module
        // here. The load phase (LoadRequestedModules) now guarantees the whole graph is present before
        // Link() is entered at all, which is both the spec's ordering and a stronger one.
        foreach (var request in _requestedModules)
        {
            // Source phase imports only load the module, no recursive linking needed
            if (request.Phase == ModuleImportPhase.Source)
            {
                continue;
            }

            var requiredModule = _engine._host.GetImportedModule(this, request);

            index = requiredModule.InnerModuleLinking(stack, index);

            if (requiredModule is not CyclicModule requiredCyclicModule)
            {
                continue;
            }

            if (requiredCyclicModule.Status is not (
                ModuleStatus.Linking or
                ModuleStatus.Linked or
                ModuleStatus.EvaluatingAsync or
                ModuleStatus.Evaluated))
            {
                Throw.InvalidOperationException($"Error while linking module: Required module is in an invalid state: {requiredCyclicModule.Status}");
            }

            if ((requiredCyclicModule.Status == ModuleStatus.Linking) == !stack.Contains(requiredCyclicModule))
            {
                Throw.InvalidOperationException($"Error while linking module: Required module is in an invalid state: {requiredCyclicModule.Status}");
            }

            if (requiredCyclicModule.Status == ModuleStatus.Linking)
            {
                _dfsAncestorIndex = Math.Min(_dfsAncestorIndex, requiredCyclicModule._dfsAncestorIndex);
            }
        }

        InitializeEnvironment();

        if (StackReferenceCount(stack) != 1)
        {
            Throw.InvalidOperationException("Error while linking module: Recursive dependency detected");
        }

        if (_dfsAncestorIndex > moduleIndex)
        {
            Throw.InvalidOperationException("Error while linking module: Recursive dependency detected");
        }

        if (moduleIndex == _dfsAncestorIndex)
        {
            while (true)
            {
                var requiredModule = stack.Pop();
                requiredModule.Status = ModuleStatus.Linked;
                if (requiredModule == this)
                {
                    break;
                }
            }
        }

        return index;
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-innermoduleevaluation
    /// </summary>
    protected internal override Completion InnerModuleEvaluation(Stack<CyclicModule> stack, int index, ref int asyncEvalOrder)
    {
        if (Status is ModuleStatus.EvaluatingAsync or ModuleStatus.Evaluated)
        {
            if (_evalError is null)
            {
                return new Completion(CompletionType.Normal, index, default);
            }

            return _evalError.Value;
        }

        if (Status == ModuleStatus.Evaluating)
        {
            return new Completion(CompletionType.Normal, index, default);
        }

        if (Status != ModuleStatus.Linked)
        {
            Throw.InvalidOperationException($"Error while evaluating module: Module is in an invalid state: {Status}");
        }

        Status = ModuleStatus.Evaluating;

        var moduleIndex = index;
        _dfsAncestorIndex = index;
        _pendingAsyncDependencies = 0;
        index++;
        stack.Push(this);

        // Build evaluationList per spec - deferred imports only evaluate async transitive deps
        var evaluationList = new List<Module>();
        foreach (var required in _requestedModules)
        {
            if (required.Phase == ModuleImportPhase.Source)
            {
                // Source phase imports don't trigger evaluation
                continue;
            }

            var requiredModule = _engine._host.GetImportedModule(this, required);

            if (required.Phase == ModuleImportPhase.Defer)
            {
                // For defer phase: only gather and evaluate async transitive dependencies
                GatherAsynchronousTransitiveDependencies(requiredModule, evaluationList);
            }
            else
            {
                // For evaluation phase: add the module itself
                if (!evaluationList.Contains(requiredModule))
                {
                    evaluationList.Add(requiredModule);
                }
            }
        }

        for (var ei = 0; ei < evaluationList.Count; ei++)
        {
            var requiredModule = evaluationList[ei];

            var result = requiredModule.InnerModuleEvaluation(stack, index, ref asyncEvalOrder);
            if (result.Type != CompletionType.Normal)
            {
                return result;
            }

            index = TypeConverter.ToInt32(result.Value);

            if (requiredModule is CyclicModule requiredCyclicModule)
            {
                if (requiredCyclicModule.Status != ModuleStatus.Evaluating &&
                    requiredCyclicModule.Status != ModuleStatus.EvaluatingAsync &&
                    requiredCyclicModule.Status != ModuleStatus.Evaluated)
                {
                    Throw.InvalidOperationException($"Error while evaluating module: Module is in an invalid state: {requiredCyclicModule.Status}");
                }

                if (requiredCyclicModule.Status == ModuleStatus.Evaluating && !stack.Contains(requiredCyclicModule))
                {
                    Throw.InvalidOperationException($"Error while evaluating module: Module is in an invalid state: {requiredCyclicModule.Status}");
                }

                if (requiredCyclicModule.Status == ModuleStatus.Evaluating)
                {
                    _dfsAncestorIndex = Math.Min(_dfsAncestorIndex, requiredCyclicModule._dfsAncestorIndex);
                }
                else
                {
                    requiredCyclicModule = requiredCyclicModule._cycleRoot;
                    if (requiredCyclicModule.Status is not (ModuleStatus.EvaluatingAsync or ModuleStatus.Evaluated))
                    {
                        Throw.InvalidOperationException("Error while evaluating module: Module is in an invalid state");
                    }

                    if (requiredCyclicModule._evalError != null)
                    {
                        return requiredCyclicModule._evalError.Value;
                    }
                }

                if (requiredCyclicModule._asyncEvaluation)
                {
                    _pendingAsyncDependencies++;
                    requiredCyclicModule._asyncParentModules.Add(this);
                }
            }
        }

        if (_pendingAsyncDependencies > 0 || _hasTLA)
        {
            if (_asyncEvaluation)
            {
                Throw.InvalidOperationException("Error while evaluating module: Module is in an invalid state (async evaluation is true)");
            }

            _asyncEvaluation = true;
            _asyncEvalOrder = asyncEvalOrder++;
            if (_pendingAsyncDependencies == 0)
            {
                // Step 12.c: If module.[[PendingAsyncDependencies]] = 0, perform ExecuteAsyncModule(module).
                // ExecuteAsyncModule returns unused - it routes both outcomes through the module's own
                // promise capability - so there is no completion to propagate from here.
                ExecuteAsyncModule();
            }

            // Otherwise the module has pending async dependencies and must not execute yet:
            // AsyncModuleExecutionFulfilled runs it once they all complete.
        }
        else
        {
            // Step 13.a: Perform ? module.ExecuteModule().
            // The "?" is load-bearing. On an abrupt execution InnerModuleEvaluation returns at once,
            // leaving this module and every unfinished ancestor on the stack so that Evaluate() step
            // 9.a can mark them all evaluated *with* the evaluation error. Falling through to the pop
            // loop below instead stranded them as evaluated with an empty [[EvaluationError]], and a
            // later import then resolved against bindings the failed evaluation never initialized.
            var completion = ExecuteModule();
            if (completion.Type != CompletionType.Normal)
            {
                return completion;
            }
        }

        if (StackReferenceCount(stack) != 1)
        {
            Throw.InvalidOperationException("Error while evaluating module: Module is in an invalid state (not found exactly once in stack)");
        }

        if (_dfsAncestorIndex > moduleIndex)
        {
            Throw.InvalidOperationException("Error while evaluating module: Module is in an invalid state (mismatch DFS ancestor index)");
        }

        if (moduleIndex == _dfsAncestorIndex)
        {
            var done = false;
            while (!done)
            {
                var requiredModule = stack.Pop();
                if (!requiredModule._asyncEvaluation)
                {
                    requiredModule.Status = ModuleStatus.Evaluated;
                }
                else
                {
                    requiredModule.Status = ModuleStatus.EvaluatingAsync;
                }

                done = ReferenceEquals(requiredModule, this);
                requiredModule._cycleRoot = this;
            }
        }

        // Step 17: Return index.
        // This is the DFS counter, not the module's execution completion. Returning the latter made
        // the caller's step 11.b (`index = TypeConverter.ToInt32(result.Value)`) read ToInt32 of the
        // completion value - undefined, so 0 - which collapsed the counter: every sibling visited
        // after the first synchronous dependency then started at index 0, saw
        // moduleIndex == [[DFSAncestorIndex]], and popped itself out of its strongly connected
        // component as its own [[CycleRoot]] before the real root had executed.
        return new Completion(CompletionType.Normal, index, default);
    }

    private int StackReferenceCount(Stack<CyclicModule> stack)
    {
        var count = 0;
        foreach (var item in stack)
        {
            if (ReferenceEquals(item, this))
            {
                count++;
            }
        }

        return count;
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-execute-async-module
    /// </summary>
    private Completion ExecuteAsyncModule()
    {
        if (Status != ModuleStatus.Evaluating && Status != ModuleStatus.EvaluatingAsync || !_hasTLA)
        {
            Throw.InvalidOperationException("Error while evaluating module: Module is in an invalid state");
        }

        var capability = PromiseConstructor.NewPromiseCapability(_engine, _realm.Intrinsics.Promise);

        // The handlers capture 'this' module - they don't receive it as an argument
        var module = this;
        var onFullfilled = new ClrFunction(_engine, "fulfilled", (thisObj, args) =>
        {
            AsyncModuleExecutionFulfilled(module);
            return Undefined;
        }, 0, PropertyFlag.Configurable);

        var onRejected = new ClrFunction(_engine, "rejected", (thisObj, args) =>
        {
            AsyncModuleExecutionRejected(module, args.At(0));
            return Undefined;
        }, 1, PropertyFlag.Configurable);

        PromiseOperations.PerformPromiseThen(_engine, (JsPromise) capability.PromiseInstance, onFullfilled, onRejected, null);

        return ExecuteModule(capability);
    }


    /// <summary>
    /// https://tc39.es/ecma262/#sec-async-module-execution-fulfilled
    /// </summary>
    private static void AsyncModuleExecutionFulfilled(CyclicModule module)
    {
        if (module.Status == ModuleStatus.Evaluated)
        {
            if (module._evalError is not null)
            {
                Throw.InvalidOperationException("Error while evaluating module: Module is in an invalid state");
            }

            return;
        }

        if (module.Status != ModuleStatus.EvaluatingAsync ||
            !module._asyncEvaluation ||
            module._evalError is not null)
        {
            Throw.InvalidOperationException("Error while evaluating module: Module is in an invalid state");
        }

        module._asyncEvaluation = false;
        module.Status = ModuleStatus.Evaluated;

        if (module._topLevelCapability is not null)
        {
            if (module._cycleRoot is null)
            {
                Throw.InvalidOperationException("Error while evaluating module: Module is in an invalid state");
            }

            module._topLevelCapability.Resolve(JsValue.Undefined);
        }

        var execList = new List<CyclicModule>();
        module.GatherAvailableAncestors(execList);
        execList.Sort((x, y) => x._asyncEvalOrder - y._asyncEvalOrder);

        for (var i = 0; i < execList.Count; i++)
        {
            var m = execList[i];

            // Step 12.a: If m.[[Status]] is evaluated, then i. Assert: m.[[EvaluationError]] is not empty.
            // Nothing else happens for such a module. An earlier element of sortedExecList can reject
            // and, through AsyncModuleExecutionRejected, record the error on its async parents - which
            // may sit later in this very list. Those are finished; executing them anyway would run a
            // module body the spec never runs and resolve a capability that was already rejected.
            if (m.Status == ModuleStatus.Evaluated)
            {
                if (m._evalError is null)
                {
                    Throw.InvalidOperationException("Error while evaluating module: Module is in an invalid state");
                }
            }
            else if (m._hasTLA)
            {
                m.ExecuteAsyncModule();
            }
            else
            {
                var result = m.ExecuteModule();
                if (result.Type != CompletionType.Normal)
                {
                    AsyncModuleExecutionRejected(m, result.Value);
                }
                else
                {
                    m._asyncEvaluation = false;
                    m.Status = ModuleStatus.Evaluated;
                    if (m._topLevelCapability is not null)
                    {
                        if (m._cycleRoot is null)
                        {
                            Throw.InvalidOperationException("Error while evaluating module: Module is in an invalid state");
                        }

                        m._topLevelCapability.Resolve(JsValue.Undefined);
                    }
                }
            }
        }
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-async-module-execution-rejected
    /// </summary>
    private static void AsyncModuleExecutionRejected(CyclicModule module, JsValue error)
    {
        if (module.Status == ModuleStatus.Evaluated)
        {
            if (module._evalError is null)
            {
                Throw.InvalidOperationException("Error while evaluating module: Module is in an invalid state");
            }

            return;
        }

        if (module.Status != ModuleStatus.EvaluatingAsync ||
            !module._asyncEvaluation ||
            module._evalError is not null)
        {
            Throw.InvalidOperationException("Error while evaluating module: Module is in an invalid state");
        }

        module._evalError = new Completion(CompletionType.Throw, error, default);
        module.Status = ModuleStatus.Evaluated;

        if (module._topLevelCapability is not null)
        {
            if (module._cycleRoot is null)
            {
                Throw.InvalidOperationException("Error while evaluating module: Module is in an invalid state");
            }

            module._topLevelCapability.Reject(error);
        }

        var asyncParentModules = module._asyncParentModules;
        for (var i = 0; i < asyncParentModules.Count; i++)
        {
            var m = asyncParentModules[i];
            AsyncModuleExecutionRejected(m, error);
        }
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-gather-available-ancestors
    /// </summary>
    private void GatherAvailableAncestors(List<CyclicModule> execList)
    {
        foreach (var m in _asyncParentModules)
        {
            if (!execList.Contains(m) && m._cycleRoot._evalError is null)
            {
                if (m.Status != ModuleStatus.EvaluatingAsync ||
                    m._evalError is not null ||
                    !m._asyncEvaluation ||
                    m._pendingAsyncDependencies <= 0)
                {
                    Throw.InvalidOperationException("Error while evaluating module: Module is in an invalid state");
                }

                if (--m._pendingAsyncDependencies == 0)
                {
                    execList.Add(m);
                    if (!m._hasTLA)
                    {
                        m.GatherAvailableAncestors(execList);
                    }
                }
            }
        }
    }

    /// <summary>
    /// https://tc39.es/proposal-defer-import-eval/#sec-IsModuleSCCEvaluated
    /// A module that finished its own body is only really done once the strongly connected component
    /// it belongs to is: a member of an async cycle reaches EVALUATED as soon as its body returns,
    /// while the cycle root is still EVALUATING-ASYNC awaiting a top-level await. Reading the member's
    /// own status alone would report that graph as settled and let a deferred dependency of it run —
    /// or be declared synchronously runnable — before the cycle has actually finished.
    /// </summary>
    private static bool IsModuleSCCEvaluated(CyclicModule module)
    {
        var cycleRoot = module._cycleRoot;
        if (cycleRoot is not null)
        {
            return cycleRoot.Status == ModuleStatus.Evaluated;
        }

        return module.Status == ModuleStatus.Evaluated;
    }

    /// <summary>
    /// https://tc39.es/proposal-defer-import-eval/#sec-ReadyForSyncExecution
    /// </summary>
    internal static bool ReadyForSyncExecution(Module module, HashSet<Module> seen = null)
    {
        if (module is not CyclicModule cyclicModule)
        {
            return true;
        }

        seen ??= new HashSet<Module>();
        if (!seen.Add(cyclicModule))
        {
            return true;
        }

        if (IsModuleSCCEvaluated(cyclicModule))
        {
            return true;
        }

        // The spec asserts LINKED here, having ruled out EVALUATING and EVALUATING-ASYNC. EVALUATED is
        // reachable too — a member of an async cycle whose root has not settled — and such a module
        // cannot be completed synchronously either, so it is refused rather than asserted on.
        if (cyclicModule.Status is ModuleStatus.Evaluating or ModuleStatus.EvaluatingAsync or ModuleStatus.Evaluated)
        {
            return false;
        }

        // Assert: status is Linked
        if (cyclicModule._hasTLA)
        {
            return false;
        }

        foreach (var request in cyclicModule._requestedModules)
        {
            var requiredModule = cyclicModule._engine._host.GetImportedModule(cyclicModule, request);
            if (!ReadyForSyncExecution(requiredModule, seen))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// https://tc39.es/proposal-defer-import-eval/#sec-EvaluateSync
    /// </summary>
    internal static void EvaluateSync(Module module)
    {
        if (!ReadyForSyncExecution(module))
        {
            Throw.TypeError(module._realm, "Cannot synchronously evaluate module: module has unfinished async dependencies");
        }

        var promise = module.Evaluate();

        if (promise is JsPromise jsPromise)
        {
            // Spec (step 3): assert the promise is settled. ReadyForSyncExecution guarantees no TLA
            // in the transitive graph, so Evaluate() must settle synchronously.
            System.Diagnostics.Debug.Assert(
                jsPromise.State != PromiseState.Pending,
                "EvaluateSync called on a module whose evaluation did not settle synchronously.");

            if (jsPromise.State == PromiseState.Rejected)
            {
                Throw.JavaScriptException(module._engine, jsPromise.Value, in AstExtensions.DefaultLocation);
            }
        }
    }

    /// <summary>
    /// https://tc39.es/proposal-defer-import-eval/#sec-GatherAsynchronousTransitiveDependencies
    /// </summary>
    internal static void GatherAsynchronousTransitiveDependencies(
        Module module,
        List<Module> result,
        HashSet<Module> seen = null)
    {
        if (module is not CyclicModule cyclicModule)
        {
            return;
        }

        seen ??= new HashSet<Module>();
        if (!seen.Add(cyclicModule))
        {
            return;
        }

        if (cyclicModule.Status == ModuleStatus.Evaluating || IsModuleSCCEvaluated(cyclicModule))
        {
            return;
        }

        if (cyclicModule._hasTLA)
        {
            if (!result.Contains(cyclicModule))
            {
                result.Add(cyclicModule);
            }
            return;
        }

        foreach (var request in cyclicModule._requestedModules)
        {
            var requiredModule = cyclicModule._engine._host.GetImportedModule(cyclicModule, request);
            GatherAsynchronousTransitiveDependencies(requiredModule, result, seen);
        }
    }

    /// <summary>
    /// https://tc39.es/ecma262/#table-cyclic-module-methods
    /// </summary>
    protected abstract void InitializeEnvironment();

    internal abstract Completion ExecuteModule(PromiseCapability capability = null);
}
