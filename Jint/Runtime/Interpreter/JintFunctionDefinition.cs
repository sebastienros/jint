using System.Runtime.CompilerServices;
using Jint.Native;
using Jint.Native.Function;
using Jint.Native.AsyncFunction;
using Jint.Native.AsyncGenerator;
using Jint.Native.Disposable;
using Jint.Native.Generator;
using Jint.Native.Promise;
using Jint.Runtime.Environments;
using Jint.Runtime.Interpreter.Expressions;

namespace Jint.Runtime.Interpreter;

/// <summary>
/// Works as memento for function execution. Optimization to cache things that don't change.
/// </summary>
internal sealed class JintFunctionDefinition
{
    // Upper bound on bindings that qualify for the array-backed fixed-slot fast path. Above this a
    // function falls back to the dictionary-backed environment. Sized so the common "many locals"
    // shape (e.g. 3d-cube's DrawLine: 2 params + 17 vars) stays on the slot fast path while keeping
    // the linear SlotIndexOf scan (on cache misses) short.
    private const int MaxFixedSlots = 24;

    private JintExpression? _bodyExpression;
    private JintStatementList? _bodyStatementList;

    public readonly string? Name;
    public readonly IFunction Function;

    // The function's own name as a JsValue, cached here so that every instantiation of this
    // definition (nested function declarations re-instantiate on every call of their enclosing
    // function) shares one immutable JsString instead of allocating a fresh one per instance.
    public readonly JsString? JsName;

    // True for definitions created by the Function constructor (CreateDynamicFunction). Their
    // definition lives in the per-realm dynamic-function cache and every `new Function(...)`
    // produces a fresh ScriptFunction instance, which changes where call environments can be
    // safely and usefully cached — see State._dynamicCachedEnv.
    public bool IsDynamic;

    // Stores the AST node needed for creating the source text.
    // (This might be different from the Function node, e.g., in the case of class methods.)
    public readonly INode SourceTextNode;

    public JintFunctionDefinition(IFunction function, INode sourceTextNode)
    {
        Function = function;
        Name = !string.IsNullOrEmpty(function.Id?.Name) ? function.Id!.Name : null;
        JsName = Name is not null ? new JsString(Name) : null;
        SourceTextNode = sourceTextNode;
    }

    public JintFunctionDefinition(IFunction function)
        : this(function, function) { }

    public bool Strict => Function.IsStrict();

    public FunctionThisMode ThisMode => Function.IsStrict() ? FunctionThisMode.Strict : FunctionThisMode.Global;

    /// <summary>
    /// https://tc39.es/ecma262/#sec-ordinarycallevaluatebody
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining | (MethodImplOptions) 512)]
    internal Completion EvaluateBody(EvaluationContext context, Function functionObject, JsCallArguments argumentsList, State state)
    {
        Completion result;
        JsArguments? argumentsInstance = null;
        if (Function.Body is not FunctionBody)
        {
            // https://tc39.es/ecma262/#sec-runtime-semantics-evaluateconcisebody
            _bodyExpression ??= JintExpression.Build((Expression) Function.Body);

            // The async path captures locals into a closure; keeping it in a separate non-inlined
            // method ensures the C# compiler does not allocate that display class on the hot sync
            // call path (every function call goes through EvaluateBody).
            if (Function.Async)
            {
                return EvaluateConciseBodyAsync(context, functionObject, argumentsList);
            }

            argumentsInstance = context.Engine.FunctionDeclarationInstantiation(context, functionObject, argumentsList, state);
            context.RunBeforeExecuteStatementChecks(Function.Body);
            var jsValue = _bodyExpression.GetValue(context).Clone();
            result = new Completion(CompletionType.Return, jsValue, Function.Body);
        }
        else if (Function.Generator)
        {
            result = Function.Async
                ? EvaluateAsyncGeneratorBody(context, functionObject, argumentsList)
                : EvaluateGeneratorBody(context, functionObject, argumentsList);
        }
        else
        {
            // See note above: extracted so the closure's display class is not allocated per sync call.
            if (Function.Async)
            {
                return EvaluateFunctionBodyAsync(context, functionObject, argumentsList);
            }

            // https://tc39.es/ecma262/#sec-runtime-semantics-evaluatefunctionbody
            argumentsInstance = context.Engine.FunctionDeclarationInstantiation(context, functionObject, argumentsList, state);
            _bodyStatementList ??= new JintStatementList(Function);
            result = _bodyStatementList.Execute(context);
        }

        argumentsInstance?.FunctionWasCalled();
        return result;
    }

    /// <summary>
    /// The synchronous, fast-FDI arms of <see cref="EvaluateBody"/>, generic over where the argument
    /// values live. Callers must have established that the function is neither a generator nor async
    /// and that <see cref="State.CanUseFastFDI"/> holds, which removes both async extractions, the
    /// generator arms, and the arguments-object bookkeeping (fixed slots require
    /// <c>!ArgumentsObjectNeeded</c>, so <c>FunctionWasCalled</c> has nothing to notify).
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining | (MethodImplOptions) 512)]
    internal Completion EvaluateBodyFast<TArgs>(EvaluationContext context, in TArgs argumentsList, State state)
        where TArgs : struct, IArgumentSource
    {
        System.Diagnostics.Debug.Assert(!Function.Generator && !Function.Async);

        if (Function.Body is not FunctionBody)
        {
            // https://tc39.es/ecma262/#sec-runtime-semantics-evaluateconcisebody
            _bodyExpression ??= JintExpression.Build((Expression) Function.Body);

            context.Engine.FunctionDeclarationInstantiationFast(state, in argumentsList);
            context.RunBeforeExecuteStatementChecks(Function.Body);
            var jsValue = _bodyExpression.GetValue(context).Clone();
            return new Completion(CompletionType.Return, jsValue, Function.Body);
        }

        // https://tc39.es/ecma262/#sec-runtime-semantics-evaluatefunctionbody
        context.Engine.FunctionDeclarationInstantiationFast(state, in argumentsList);
        _bodyStatementList ??= new JintStatementList(Function);
        return _bodyStatementList.Execute(context);
    }

    /// <summary>
    /// Body evaluation for env-less leaf calls (<see cref="State.SupportsLeafCall"/>): FDI is a
    /// no-op by the flag's gate and the body is a plain synchronous statement list, so nothing
    /// remains but executing it.
    /// </summary>
    internal Completion EvaluateLeafBody(EvaluationContext context)
    {
        System.Diagnostics.Debug.Assert(Function.Body is FunctionBody && !Function.Generator && !Function.Async);
        var list = _bodyStatementList ??= new JintStatementList(Function);
        return list.Execute(context);
    }

    /// <summary>
    /// Async concise-body (arrow expression body) evaluation. Kept out of <see cref="EvaluateBody"/>
    /// so the captured-locals closure's display class is not allocated on the hot sync call path.
    /// </summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private Completion EvaluateConciseBodyAsync(EvaluationContext context, Function functionObject, JsCallArguments argumentsList)
    {
        // local copies to prevent capturing the method parameters
        var function = functionObject;
        JsCallArguments? jsValues = argumentsList;

        var promiseCapability = PromiseConstructor.NewPromiseCapability(context.Engine, context.Engine.Realm.Intrinsics.Promise);
        // Expression bodies don't have a statement list (used only for resumption)
        AsyncFunctionStart(context, promiseCapability, body: null, context =>
        {
            // Instantiate only on the first, synchronous slice. The body delegate re-runs on every
            // resume after an await, but by then the (possibly pooled) arguments array has been
            // returned to its pool and the saved execution context already carries the environments
            // and parameter bindings from this first run.
            if (jsValues is not null)
            {
                context.Engine.FunctionDeclarationInstantiation(context, function, jsValues);
                jsValues = null;
            }
            context.RunBeforeExecuteStatementChecks(Function.Body);
            var jsValue = _bodyExpression!.GetValue(context).Clone();

            // Check for async suspension - if suspended, return early to allow resumption
            if (context.IsSuspended())
            {
                return new Completion(CompletionType.Normal, jsValue, _bodyExpression._expression);
            }

            return new Completion(CompletionType.Return, jsValue, _bodyExpression._expression);
        });
        return new Completion(CompletionType.Return, promiseCapability.PromiseInstance, Function.Body);
    }

    /// <summary>
    /// Async function-body evaluation. Kept out of <see cref="EvaluateBody"/> so the captured-locals
    /// closure's display class is not allocated on the hot sync call path.
    /// </summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private Completion EvaluateFunctionBodyAsync(EvaluationContext context, Function functionObject, JsCallArguments argumentsList)
    {
        // local copies to prevent capturing the method parameters
        var function = functionObject;
        var arguments = argumentsList;

        var promiseCapability = PromiseConstructor.NewPromiseCapability(context.Engine, context.Engine.Realm.Intrinsics.Promise);
        // The statement list is immutable and shareable across invocations: each invocation's
        // resume position lives on its AsyncFunctionInstance (SuspendDataDictionary).
        var bodyStatementList = _bodyStatementList ??= new JintStatementList(Function);
        AsyncFunctionStart(context, promiseCapability, bodyStatementList, context =>
        {
            context.Engine.FunctionDeclarationInstantiation(context, function, arguments);
            return bodyStatementList.Execute(context);
        });
        return new Completion(CompletionType.Return, promiseCapability.PromiseInstance, Function.Body);
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-async-functions-abstract-operations-async-function-start
    /// </summary>
    private static void AsyncFunctionStart(
        EvaluationContext context,
        PromiseCapability promiseCapability,
        JintStatementList? body,
        Func<EvaluationContext, Completion> asyncFunctionBody)
    {
        var engine = context.Engine;
        var runningContext = engine.ExecutionContext;

        // Step 1-2: Create async function state tracking instance
        // This is an implementation detail not explicitly in spec, but needed for suspension/resumption
        var asyncInstance = new AsyncFunctionInstance
        {
            _state = AsyncFunctionState.Executing,
            _capability = promiseCapability,
            _body = body,
            _bodyFunction = asyncFunctionBody
        };

        // Step 3: "Let asyncContext be a copy of runningContext"
        // Since ExecutionContext is a readonly struct, UpdateAsyncFunction creates a new copy
        // with the AsyncFunction field set, achieving the spec's "copy" semantics.
        var asyncContext = runningContext.UpdateAsyncFunction(asyncInstance);

        // Store the context for resumption when awaited promises settle
        asyncInstance._savedContext = asyncContext;

        // Step 5: "Push asyncContext onto the execution context stack"
        // We leave the old context and push the new one (equivalent to spec's push operation)
        engine.LeaveExecutionContext();
        engine.EnterExecutionContext(in asyncContext);

        // Step 6: "Resume the suspended evaluation of asyncContext"
        // Perform AsyncBlockStart to begin executing the async function body
        AsyncBlockStart(context, asyncInstance, asyncFunctionBody);
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-asyncblockstart
    /// </summary>
    private static void AsyncBlockStart(
        EvaluationContext context,
        AsyncFunctionInstance asyncInstance,
        Func<EvaluationContext, Completion> asyncBody)
    {
        var engine = context.Engine;

        Completion result;
        try
        {
            result = asyncBody(context);
        }
        catch (JavaScriptException e)
        {
            // Per spec: DisposeResources before rejecting. Use the helper so async-dispose
            // resources are awaited via the state machine instead of sync-blocking. Skip
            // the helper entirely if the env has no disposables — common-case hot path.
            var env = engine.ExecutionContext.LexicalEnvironment;
            if (!env.HasDisposeResources)
            {
                asyncInstance._state = AsyncFunctionState.Completed;
                asyncInstance._capability.Reject(e.Error);
                return;
            }
            DisposeResourcesHelper.DisposeAndThen(
                engine,
                env,
                new Completion(CompletionType.Throw, e.Error, null!),
                final =>
                {
                    asyncInstance._state = AsyncFunctionState.Completed;
                    asyncInstance._capability.Reject(final.Value);
                });
            return;
        }

        // Check if we suspended at an await
        if (asyncInstance._state == AsyncFunctionState.SuspendedAwait)
        {
            // Suspended - promise reaction will resume execution later
            // Do NOT dispose resources yet - body hasn't completed
            return;
        }

        // Per spec AsyncBlockStart step 3.f: DisposeResources after body completes.
        // Settlement of the function's return promise is deferred until the dispose chain
        // (which may itself await) finishes. Fast-path skip when no disposables registered.
        var lexEnv = engine.ExecutionContext.LexicalEnvironment;
        if (!lexEnv.HasDisposeResources)
        {
            SettleAsyncFunctionCompletion(asyncInstance, result);
            return;
        }
        DisposeResourcesHelper.DisposeAndThen(engine, lexEnv, result, final => SettleAsyncFunctionCompletion(asyncInstance, final));
    }

    private static void SettleAsyncFunctionCompletion(AsyncFunctionInstance asyncInstance, Completion final)
    {
        asyncInstance._state = AsyncFunctionState.Completed;

        if (final.Type == CompletionType.Throw)
        {
            asyncInstance._capability.Reject(final.Value);
        }
        else if (final.Type == CompletionType.Normal)
        {
            asyncInstance._capability.Resolve(JsValue.Undefined);
        }
        else if (final.Type == CompletionType.Return)
        {
            asyncInstance._capability.Resolve(final.Value);
        }
        else
        {
            asyncInstance._capability.Reject(final.Value);
        }
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-runtime-semantics-evaluategeneratorbody
    /// </summary>
    private Completion EvaluateGeneratorBody(
        EvaluationContext context,
        Function functionObject,
        JsCallArguments argumentsList)
    {
        var engine = context.Engine;
        engine.FunctionDeclarationInstantiation(context, functionObject, argumentsList);
        var G = engine.Realm.Intrinsics.Function.OrdinaryCreateFromConstructor(
            functionObject,
            static intrinsics => intrinsics.GeneratorFunction.PrototypeObject.PrototypeObject,
            static (Engine engine, Realm _, object? _) => new GeneratorInstance(engine));

        // The statement list is immutable and shareable across generator instances:
        // each instance's resume position lives on the instance itself (SuspendDataDictionary).
        _bodyStatementList ??= new JintStatementList(Function);
        G.GeneratorStart(_bodyStatementList);

        return new Completion(CompletionType.Return, G, Function.Body);
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-runtime-semantics-evaluateasyncgeneratorbody
    /// </summary>
    private Completion EvaluateAsyncGeneratorBody(
        EvaluationContext context,
        Function functionObject,
        JsCallArguments argumentsList)
    {
        var engine = context.Engine;
        engine.FunctionDeclarationInstantiation(context, functionObject, argumentsList);
        var G = engine.Realm.Intrinsics.Function.OrdinaryCreateFromConstructor(
            functionObject,
            static intrinsics => intrinsics.AsyncGeneratorFunction.PrototypeObject.PrototypeObject,
            static (Engine engine, Realm _, object? _) => new AsyncGeneratorInstance(engine));

        // See EvaluateGeneratorBody: the shared list is safe, positions are per instance.
        _bodyStatementList ??= new JintStatementList(Function);
        G.AsyncGeneratorStart(_bodyStatementList);

        return new Completion(CompletionType.Return, G, Function.Body);
    }

    internal State Initialize()
    {
        var node = (Node) Function;
        var stateOrFullSourceText = node.UserData;
        if (stateOrFullSourceText is not State state)
        {
            node.UserData = state = BuildState(Function, stateOrFullSourceText as string);
        }
        return state;
    }

    internal sealed class State
    {
        public int Length;
        public Key[] ParameterNames = null!;
        public bool HasDuplicates;
        public bool IsSimpleParameterList;
        public bool HasParameterExpressions;
        public bool ArgumentsObjectNeeded;
        public bool RequiresInputArgumentsOwnership;
        public List<Key>? VarNames;
        public FunctionToInitialize[]? FunctionsToInitialize;
        public readonly HashSet<Key> FunctionNames = new();
        public DeclarationCache? LexicalDeclarations;
        public HashSet<Key>? ParameterBindings;
        public List<VariableValuePair>? VarsToInitialize;
        public bool NeedsEvalContext;
        /// <summary>
        /// B.3.3.1: Names of block-level function declarations that need runtime var-scope copy.
        /// </summary>
        public HashSet<Key>? AnnexBFunctionNames;

        /// <summary>
        /// B.3.3.1: The specific function declaration AST nodes that are AnnexB-eligible.
        /// Used to distinguish same-named declarations at different block levels.
        /// </summary>
        public HashSet<FunctionDeclaration>? AnnexBFunctionDeclarations;

        // Fixed-slot optimization fields
        public bool UseFixedSlots;
        public Key[]? SlotNames;
        public int ParameterSlotCount;
        public int VarSlotCount;

        /// <summary>
        /// The initial <see cref="Binding"/> for every slot the fixed-slot instantiation arm fills —
        /// the whole region from <see cref="ParameterSlotCount"/> to the end of <see cref="SlotNames"/>,
        /// so <c>Length</c> is exactly <c>SlotNames.Length - ParameterSlotCount</c>. Non-null whenever
        /// <see cref="CanUseFastFDI"/> holds (empty for a parameters-only function), which is what lets
        /// the arm be one unconditional copy with no arm selection of its own.
        /// <para>
        /// The hoisted-var region holds <c>undefined</c>, mutable, exactly as the general arm writes it.
        /// The top-level let/const region holds UNINITIALIZED entries — the temporal dead zone — each
        /// carrying its declaration kind's mutability, so a read before the declaration executes is a
        /// ReferenceError and a write to a <c>const</c> is a TypeError. Byte for byte what
        /// <see cref="Engine.FunctionDeclarationInstantiation"/>'s general arm writes into the same slots.
        /// </para>
        /// <para>
        /// Immutable once built, and the only <see cref="Jint.Native.JsValue"/> it can hold is the
        /// <see cref="Jint.Native.JsValue.Undefined"/> singleton, which is engine-independent — hence
        /// safe on this cross-engine shared State (same argument as
        /// <c>JintBlockStatement.BlockState.SlotTemplates</c>).
        /// </para>
        /// </summary>
        public Binding[]? NonParameterSlotTemplate;

        /// <summary>
        /// True when <see cref="Engine.FunctionDeclarationInstantiation"/> can be served by its
        /// fixed-slot arm. Currently coincides with <see cref="UseFixedSlots"/>: the arm can express
        /// every binding kind the slot layout admits, including the temporal dead zone (see
        /// <see cref="NonParameterSlotTemplate"/>). Kept as its own flag because it names the
        /// instantiation capability, not the storage — a future lane may gate more narrowly.
        /// </summary>
        public bool CanUseFastFDI;
        /// <summary>
        /// True when FunctionDeclarationInstantiation has nothing to do at all: no parameters,
        /// no vars, no lexical declarations, no inner function declarations, no arguments object
        /// and no eval context. Lets calls skip FDI entirely (common for tiny closure methods).
        /// </summary>
        public bool CanUseEmptyFDI;

        /// <summary>
        /// True when nothing in the function's own params/body can observe the call frame's
        /// this-binding, super base or new.target: no ThisExpression/Super/MetaProperty node
        /// anywhere in the subtree. The scan over-approximates into nested functions, which is
        /// the safe direction — and exact in practice, because the flag is only computed when
        /// <see cref="EnvironmentMayEscape"/> is false, which excludes nested functions entirely.
        /// Lets [[Call]] skip OrdinaryCallBindThis: the this-binding stays Uninitialized, and
        /// FunctionEnvironment.GetThisBinding throws loudly if a resolution route was missed,
        /// rather than silently observing a wrong value.
        /// </summary>
        public bool CanSkipThisBinding;

        /// <summary>
        /// True when a plain [[Call]] can run without a callee FunctionEnvironment at all: the
        /// ExecutionContext's lexical/variable environment is the function's captured environment
        /// directly. Requires <see cref="CanUseEmptyFDI"/> (no bindings to create),
        /// <see cref="CanSkipThisBinding"/> (no this/super/new.target route; implies
        /// !EnvironmentMayEscape — no closures/classes/with/direct-eval that could resolve
        /// through or capture the frame — and non-arrow/non-generator/non-async), and a statement
        /// body. Callers must additionally gate on !Engine._isDebugMode (the debugger walks the
        /// frame) and !_isClassConstructor at runtime.
        /// </summary>
        public bool SupportsLeafCall;

        /// <summary>
        /// The static half of the register-lane gate (<c>ScriptFunction.CallCore</c>): a plain
        /// synchronous call whose instantiation is the fixed-slot fast path. Requires
        /// <see cref="CanUseFastFDI"/> (so there is no arguments object and no parameter-default
        /// evaluation) plus non-generator and non-async (so there is no suspension machinery and
        /// no deferred DisposeResources). Precomputed rather than re-derived per call because
        /// <c>IFunction.Generator</c>/<c>.Async</c> are interface properties on a polymorphic AST
        /// node. Callers additionally gate on !Engine._isDebugMode and !_isClassConstructor at
        /// runtime, exactly as <see cref="SupportsLeafCall"/>'s callers do.
        /// </summary>
        public bool SupportsRegisterCall;

        public bool EnvironmentMayEscape;
        // True when the function body contains a direct call to itself by name. Tight recursion
        // (e.g. fib/ack/tak) keeps several frames live at once, which a single-slot per-call reuse cache
        // cannot serve — only the topmost frame would ever be reusable. Such functions use the bounded
        // RecursiveEnvPool on the function instance instead so each live frame reuses a distinct env.
        //
        // NOTE: call ENVIRONMENTS are cached on the ScriptFunction instance (_envReuse), not on this
        // State. A prepared script's State is shared across engines, and an environment roots its creating
        // engine, so pooling environments here kept the last engine that ran each function alive (issue
        // #2560). The slot array below is different: it is cleared before being cached, holds no engine
        // references, and so can safely be shared across instances and engines.
        public bool IsDirectRecursive;

        /// <summary>
        /// Static constructor-body shape eligibility (see <see cref="ComputeCtorBodyShapeEligibility"/>):
        /// 0 = not yet analyzed, 1 = eligible, 2 = ineligible. Computed lazily on first [[Construct]] —
        /// never in BuildState, so never-constructed functions pay nothing. A pure function of the
        /// immutable AST, so it is safe on this cross-engine shared State: racing recomputation is
        /// idempotent and the byte write is atomic.
        /// </summary>
        public byte CtorBodyShapeEligibility;

        // Cleared fixed-slot Binding[] reused by the next call to any function instance sharing this State
        // (also across engines — e.g. freshly created instances when a prepared script is re-evaluated).
        // Interlocked is required: parallel test fixtures share cached States (see PR #2418 fallout).
        public Binding[]? _cachedSlots;

        // Exception to the "no environments on State" rule above, for Function-constructor
        // definitions only (JintFunctionDefinition.IsDynamic): their definition lives in the
        // per-realm dynamic-function cache and is never shared across engines, while every
        // `new Function(...)` call produces a fresh ScriptFunction whose per-instance cache can
        // never warm. Parking the call environment here keeps one stable environment identity
        // across those one-shot instances, which is what the shared statement tree's per-node
        // slot caches key on. Interlocked for the same reason as _cachedSlots.
        public FunctionEnvironment? _dynamicCachedEnv;

        public SourceText SourceText;

        internal readonly record struct VariableValuePair(Key Name, JsValue? InitialValue);

        /// <summary>
        /// A function declaration hoisted by FunctionDeclarationInstantiation, with its binding
        /// name pre-converted to a <see cref="Key"/> so the per-call FDI loop neither re-hashes
        /// the name nor walks a linked list.
        /// </summary>
        [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
        internal readonly record struct FunctionToInitialize(FunctionDeclaration Declaration, Key Name);
    }

    internal static State BuildState(IFunction function, string? fullSourceText = null)
    {
        var state = new State();

        ProcessParameters(function, state, out var hasArguments);

        var strict = function.IsStrict();
        var hoistingScope = HoistingScope.GetFunctionLevelDeclarations(strict, function);
        var functionDeclarations = hoistingScope._functionDeclarations;
        var lexicalNames = hoistingScope._lexicalNames;
        state.VarNames = hoistingScope._varNames;

        State.FunctionToInitialize[]? functionsToInitialize = null;

        if (functionDeclarations != null)
        {
            // The last declaration of a name wins, others keep source order: walk backwards
            // de-duplicating, then reverse the survivors.
            var survivors = new List<State.FunctionToInitialize>(functionDeclarations.Count);
            for (var i = functionDeclarations.Count - 1; i >= 0; i--)
            {
                var d = functionDeclarations[i];
                Key fn = d.Id!.Name;
                if (state.FunctionNames.Add(fn))
                {
                    survivors.Add(new State.FunctionToInitialize(d, fn));
                }
            }

            if (survivors.Count > 0)
            {
                survivors.Reverse();
                functionsToInitialize = survivors.ToArray();
            }
        }

        state.FunctionsToInitialize = functionsToInitialize;

        state.ArgumentsObjectNeeded = true;
        var thisMode = strict ? FunctionThisMode.Strict : FunctionThisMode.Global;
        if (function.Type == NodeType.ArrowFunctionExpression)
        {
            thisMode = FunctionThisMode.Lexical;
        }

        if (thisMode == FunctionThisMode.Lexical || hasArguments)
        {
            state.ArgumentsObjectNeeded = false;
        }
        else if (!state.HasParameterExpressions)
        {
            if (state.FunctionNames.Contains(KnownKeys.Arguments) || lexicalNames?.Contains(KnownKeys.Arguments) == true)
            {
                state.ArgumentsObjectNeeded = false;
            }
        }

        // Whether this function has an arguments object of its own for anything to reach: the steps
        // above are the spec's, and everything below is Jint declining to build one nothing can name.
        var argumentsObjectReachable = state.ArgumentsObjectNeeded;

        if (state.ArgumentsObjectNeeded)
        {
            // just one extra check...
            state.ArgumentsObjectNeeded = ArgumentsUsageAstVisitor.HasArgumentsReference(function);
        }

        // One walk answers both of the remaining questions, and runs only while one of them is open.
        // A sloppy function may need the eval context; and a function whose source holds no
        // `arguments` token can still name the arguments object through a direct eval, which the
        // scan above cannot see into. `eval("arguments")` resolves lexically, so it reaches this
        // function's arguments object in strict mode as much as in sloppy mode — the strict eval
        // gets a variable environment of its own, not a fresh `arguments`.
        var evalUsage = EvalContextAstVisitor.Usage.None;
        if (!strict || (argumentsObjectReachable && !state.ArgumentsObjectNeeded))
        {
            evalUsage = EvalContextAstVisitor.Scan(function);
        }

        state.NeedsEvalContext = !strict && (evalUsage & EvalContextAstVisitor.Usage.EvalContext) != EvalContextAstVisitor.Usage.None;

        if (argumentsObjectReachable && !state.ArgumentsObjectNeeded)
        {
            state.ArgumentsObjectNeeded = (evalUsage & EvalContextAstVisitor.Usage.ArgumentsReachingEval) != EvalContextAstVisitor.Usage.None;
        }

        var parameterBindings = new HashSet<Key>(state.ParameterNames);
        if (state.ArgumentsObjectNeeded)
        {
            parameterBindings.Add(KnownKeys.Arguments);
        }

        if (function.Type == NodeType.ArrowFunctionExpression)
        {
            state.RequiresInputArgumentsOwnership = state.ArgumentsObjectNeeded ||
                (function.Async && ArgumentsUsageAstVisitor.HasArgumentsReference(function));
        }
        else
        {
            state.RequiresInputArgumentsOwnership = state.ArgumentsObjectNeeded &&
                (function.Async || function.Generator);
        }

        state.ParameterBindings = parameterBindings;

        var varsToInitialize = new List<State.VariableValuePair>();
        if (!state.HasParameterExpressions)
        {
            var instantiatedVarNames = state.VarNames != null
                ? new HashSet<Key>(state.ParameterBindings)
                : new HashSet<Key>();

            // Add function names first (they take precedence over var declarations with same name)
            foreach (var fn in state.FunctionNames)
            {
                if (instantiatedVarNames.Add(fn))
                {
                    varsToInitialize.Add(new State.VariableValuePair(Name: fn, InitialValue: null));
                }
            }

            for (var i = 0; i < state.VarNames?.Count; i++)
            {
                var n = state.VarNames[i];
                if (instantiatedVarNames.Add(n))
                {
                    varsToInitialize.Add(new State.VariableValuePair(Name: n, InitialValue: null));
                }
            }
        }
        else
        {
            var instantiatedVarNames = state.VarNames != null
                ? new HashSet<Key>(state.ParameterBindings)
                : null;

            // Add function names first (they take precedence over var declarations with same name)
            foreach (var fn in state.FunctionNames)
            {
                if (instantiatedVarNames?.Add(fn) != false)
                {
                    instantiatedVarNames ??= new HashSet<Key>();
                    instantiatedVarNames.Add(fn);
                    JsValue? initialValue = null;
                    if (!state.ParameterBindings.Contains(fn))
                    {
                        initialValue = JsValue.Undefined;
                    }
                    varsToInitialize.Add(new State.VariableValuePair(Name: fn, InitialValue: initialValue));
                }
            }

            for (var i = 0; i < state.VarNames?.Count; i++)
            {
                var n = state.VarNames[i];
                if (instantiatedVarNames!.Add(n))
                {
                    JsValue? initialValue = null;
                    if (!state.ParameterBindings.Contains(n) || state.FunctionNames.Contains(n))
                    {
                        initialValue = JsValue.Undefined;
                    }

                    varsToInitialize.Add(new State.VariableValuePair(Name: n, InitialValue: initialValue));
                }
            }
        }

        state.VarsToInitialize = varsToInitialize;

        // B.3.3.1: AnnexB block-level function declarations need var bindings
        var annexBFunctions = hoistingScope._annexBFunctionDeclarations;
        if (annexBFunctions != null)
        {
            var instantiatedVarNames = new HashSet<Key>(state.ParameterNames);
            foreach (var pair in varsToInitialize)
            {
                instantiatedVarNames.Add(pair.Name);
            }

            for (var i = 0; i < annexBFunctions.Count; i++)
            {
                var f = annexBFunctions[i];
                Key fn = f.Id!.Name;

                // Skip if name conflicts with parameter or lexical declaration
                if (state.ParameterBindings!.Contains(fn))
                {
                    continue;
                }

                if (lexicalNames?.Contains(fn) == true)
                {
                    continue;
                }

                state.AnnexBFunctionNames ??= new HashSet<Key>();
                state.AnnexBFunctionNames.Add(fn);

                state.AnnexBFunctionDeclarations ??= [];
                state.AnnexBFunctionDeclarations.Add(f);

                if (instantiatedVarNames.Add(fn))
                {
                    varsToInitialize.Add(new State.VariableValuePair(Name: fn, InitialValue: JsValue.Undefined));
                }
            }
        }

        if (hoistingScope._lexicalDeclarations != null)
        {
            state.LexicalDeclarations = DeclarationCacheBuilder.Build(hoistingScope._lexicalDeclarations);
        }

        // Fixed-slot qualification: use array-based binding storage for simple functions
        if (state.IsSimpleParameterList
            && !state.HasDuplicates
            && !state.HasParameterExpressions
            && !state.NeedsEvalContext
            && !state.ArgumentsObjectNeeded
            && state.FunctionsToInitialize is null)
        {
            // Count lexical declaration bindings (let/const only, no function/class declarations)
            var lexicalBindingCount = 0;
            var lexDecls = state.LexicalDeclarations;
            if (lexDecls is { AllLexicalScoped: true } ld)
            {
                foreach (var decl in ld.Declarations)
                {
                    lexicalBindingCount += decl.BoundNames.Length;
                }
            }
            else if (lexDecls is not null)
            {
                // Has non-lexical declarations (function/class) — can't use fixed slots
                lexicalBindingCount = -1;
            }

            var totalSlots = state.ParameterNames.Length + varsToInitialize.Count + lexicalBindingCount;
            if (lexicalBindingCount >= 0 && totalSlots > 0 && totalSlots <= MaxFixedSlots)
            {
                var slotNames = new Key[totalSlots];
                state.ParameterNames.CopyTo(slotNames, 0);
                var varOffset = state.ParameterNames.Length;

                // Every non-parameter slot's initial Binding, built here alongside the names so the
                // two orders can never drift. The fixed-slot instantiation arm then stamps this over
                // the whole non-parameter region unconditionally — one code path, whatever the
                // function declares. Empty (and allocation-free) for a parameters-only function.
                var nonParameterSlotTemplate = totalSlots > varOffset ? new Binding[totalSlots - varOffset] : [];

                for (var i = 0; i < varsToInitialize.Count; i++)
                {
                    slotNames[varOffset + i] = varsToInitialize[i].Name;

                    // A hoisted var starts initialized to undefined and mutable. JsValue.Undefined is a
                    // process-wide singleton, so this stays engine-independent.
                    nonParameterSlotTemplate[i] = new Binding(JsValue.Undefined, canBeDeleted: false, mutable: true, strict: false);
                }

                // Add lexical declaration names (let/const) and their initial Bindings.
                if (lexicalBindingCount > 0)
                {
                    var lexOffset = varOffset + varsToInitialize.Count;
                    var templateIndex = varsToInitialize.Count;
                    foreach (var decl in lexDecls!.Value.Declarations)
                    {
                        // A null value is the temporal dead zone: the binding exists but is
                        // uninitialized until its declaration executes, so a read before that
                        // point is a ReferenceError rather than `undefined`. Byte for byte what
                        // FunctionDeclarationInstantiation's general arm writes into these slots.
                        var template = decl.IsConstantDeclaration
                            ? new Binding(null!, canBeDeleted: false, mutable: false, strict: true)
                            : new Binding(null!, canBeDeleted: false, mutable: true, strict: false);

                        foreach (var bn in decl.BoundNames)
                        {
                            slotNames[lexOffset++] = bn;
                            nonParameterSlotTemplate[templateIndex++] = template;
                        }
                    }
                }

                state.SlotNames = slotNames;
                state.ParameterSlotCount = state.ParameterNames.Length;
                state.VarSlotCount = varsToInitialize.Count;
                state.NonParameterSlotTemplate = nonParameterSlotTemplate;
                state.UseFixedSlots = true;
                state.CanUseFastFDI = true;
            }
        }

        // Empty-FDI: instantiation is a complete no-op. Common for tiny closure methods like
        // `this.start = function () { ... }` that only touch captured or global state.
        // IsSimpleParameterList is required because rest/pattern parameters bind via
        // AddFunctionParameters from the AST and may not appear in ParameterNames
        // (e.g. the synthesized default derived constructor `constructor(...args)`).
        state.CanUseEmptyFDI = state.IsSimpleParameterList
            && state.ParameterNames.Length == 0
            && !state.NeedsEvalContext
            && !state.ArgumentsObjectNeeded
            && state.FunctionsToInitialize is null
            && varsToInitialize.Count == 0
            && state.LexicalDeclarations is null;

        state.SupportsRegisterCall = state.CanUseFastFDI && !function.Generator && !function.Async;

        // Compute EnvironmentMayEscape unconditionally so consumers (e.g. FunctionEnvironment pooling)
        // can rely on it without first checking UseFixedSlots. Generators / async functions / direct eval
        // always escape; otherwise inspect the body. When the function qualified for fixed slots, prefer
        // the slot-aware analysis (only escapes if a closure actually references a slot variable);
        // otherwise fall back to the conservative "any inner closure means escape" check.
        if (function.Generator || function.Async || state.NeedsEvalContext)
        {
            state.EnvironmentMayEscape = true;
        }
        else if (state.UseFixedSlots)
        {
            state.EnvironmentMayEscape = EnvironmentEscapeAstVisitor.MayEscapeWithReferences(function, state.SlotNames!);
        }
        else
        {
            state.EnvironmentMayEscape = EnvironmentEscapeAstVisitor.MayEscape(function);
        }

        // This-binding elision: a non-arrow function that never references this/super/new.target
        // and creates no closures (escape analysis; also excludes generators/async/direct eval)
        // can leave its frame's this-binding uninitialized — OrdinaryCallBindThis is dead work.
        // Arrows already skip the bind through FunctionThisMode.Lexical, so no flag is needed.
        if (!state.EnvironmentMayEscape && function.Type != NodeType.ArrowFunctionExpression)
        {
            state.CanSkipThisBinding = !ThisSuperNewTargetAstVisitor.HasReference(function);

            // ...and when instantiation is additionally a complete no-op, the callee environment
            // itself is dead: it would hold no bindings and an unread this-binding, existing only
            // as a chain pointer to the environment the function captured. Such calls push that
            // captured environment directly (identifier resolution already skips the empty env —
            // this removes its allocation/reset/write-back and the extra hop).
            state.SupportsLeafCall = state.CanSkipThisBinding
                && state.CanUseEmptyFDI
                && function.Body is FunctionBody;
        }

        // Detect direct named self-call (function fib(n) { ...fib(n-1)... }). For these, the single-env
        // reuse cache is useless — only the topmost frame would ever be reusable, every deeper frame
        // allocates anyway — so they use the bounded RecursiveEnvPool on the function instance instead
        // (tight recursion, e.g. controlflow-recursive: ~500k calls per iteration).
        var name = function.Id?.Name;
        if (name is not null && !state.EnvironmentMayEscape)
        {
            state.IsDirectRecursive = SelfCallAstVisitor.ContainsCallTo(function.Body, name);
        }

        state.SourceText = new SourceText(fullSourceText);

        return state;
    }

    /// <summary>
    /// Statically classifies a constructor body as safe to start hidden-class shape building from the
    /// third instance on (see ScriptFunction's [[Construct]]; instances #1 and #2 stay dictionary-mode
    /// so constructors of unrepeated layouts intern no shape state) instead of after the sampling
    /// threshold. Eligible bodies consist, at the top level, only of:
    /// <list type="bullet">
    /// <item><c>this.identifier = value</c> assignments (plain <c>=</c>; non-computed, so index-like keys
    /// are excluded by grammar) whose RHS contains no call that can observe <c>this</c>;</item>
    /// <item><c>var</c>/<c>let</c>/<c>const</c> declarations, under the same no-this-escaping-call scan
    /// (<c>var x = f(this)</c> is the same hazard as <c>this.x = f(this)</c>);</item>
    /// <item>directives, empty statements and argument-less <c>return</c>s (in a base constructor
    /// <c>return;</c> just yields <c>this</c>);</item>
    /// </list>
    /// and parameter defaults/patterns pass the same call scan (they evaluate during construction, after
    /// <c>this</c> is bound). An EMPTY body is eligible — this covers class default constructors, whose
    /// fields are vetted separately per function instance (the empty-constructor AST and hence this
    /// verdict are shared across classes). A this-escaping call could add data-dependent keys mid-build,
    /// polluting the shared per-prototype transition tree — the real hazard. Allowed: this-free calls
    /// (<c>Date.now()</c>), <c>this.a</c> reads, literals, and function/arrow DEFINITIONS that capture
    /// <c>this</c> (they cannot run during construction in an otherwise-eligible body). Anything else —
    /// control flow, computed or non-this member targets, compound assignments — is ineligible and keeps
    /// the sampling behavior. Purely a heuristic: TryShapeAdd's megamorphic guards and the dictionary
    /// deopt remain the correctness authority either way.
    /// </summary>
    internal static bool ComputeCtorBodyShapeEligibility(IFunction function)
    {
        if (function.Body is not FunctionBody body)
        {
            // expression-bodied arrow — not constructible anyway
            return false;
        }

        foreach (var parameter in function.Params.AsSpan())
        {
            if (!parameter.ChildNodes.IsEmpty() && ContainsThisEscapingCall(parameter))
            {
                return false;
            }
        }

        foreach (var statement in body.Body.AsSpan())
        {
            if (!IsShapeEligibleCtorStatement(statement))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// The statement forms an eligible constructor body may contain: static-key `this.x = rhs`
    /// stores, local declarations and bare returns (each without a this-escaping call), and —
    /// because eligibility only decides how early shaping starts, never correctness — if/else and
    /// blocks over those same forms. Branchy constructors like sunspider-3d-raytrace's Triangle
    /// (`if (...) this.axis = 0; else this.axis = 2;`) assign the same key set on every path in
    /// practice, and a path-divergent layout merely splits the interned transition tree, which
    /// TryShapeAdd's fanout guard already bounds.
    /// </summary>
    private static bool IsShapeEligibleCtorStatement(Statement statement)
    {
        switch (statement.Type)
        {
            case NodeType.EmptyStatement:
                return true;

            case NodeType.ExpressionStatement:
                if (statement is Directive)
                {
                    // directive prologue ("use strict") — an inert string literal
                    return true;
                }

                if (((ExpressionStatement) statement).Expression is AssignmentExpression
                    {
                        Operator: Operator.Assignment,
                        Left: MemberExpression { Object.Type: NodeType.ThisExpression, Computed: false, Property.Type: NodeType.Identifier },
                    } assignment
                    && !ContainsThisEscapingCall(assignment.Right))
                {
                    return true;
                }

                return false;

            case NodeType.VariableDeclaration:
                return !ContainsThisEscapingCall(statement);

            case NodeType.ReturnStatement:
                // `return expr` could replace the instance or evaluate arbitrarily
                return ((ReturnStatement) statement).Argument is null;

            case NodeType.IfStatement:
                var ifStatement = (IfStatement) statement;
                return !ContainsThisEscapingCall(ifStatement.Test)
                    && IsShapeEligibleCtorStatement(ifStatement.Consequent)
                    && (ifStatement.Alternate is null || IsShapeEligibleCtorStatement(ifStatement.Alternate));

            case NodeType.BlockStatement:
                foreach (var inner in ((BlockStatement) statement).Body.AsSpan())
                {
                    if (!IsShapeEligibleCtorStatement(inner))
                    {
                        return false;
                    }
                }
                return true;

            default:
                return false;
        }
    }

    /// <summary>
    /// True when the subtree contains a call-like node (call / new / tagged template) that could observe
    /// <c>this</c> — i.e. carries a ThisExpression anywhere inside its own subtree. Function and arrow
    /// definitions OUTSIDE such call subtrees are skipped: they merely capture <c>this</c> and cannot run
    /// during construction in an otherwise-eligible body (any invocation route would itself be a rejected
    /// call). Inside a call subtree nothing is skipped — <c>f(() =&gt; this)</c> hands the callee a live
    /// capture it could invoke mid-construction. Aliases (<c>var self = this</c> passed to a later call)
    /// are not tracked; a missed escape only risks bounded transition-tree churn, never correctness.
    /// </summary>
    private static bool ContainsThisEscapingCall(Node node)
    {
        var type = node.Type;
        if (type is NodeType.CallExpression or NodeType.NewExpression or NodeType.TaggedTemplateExpression)
        {
            // A this-free call cannot leak `this`, and nothing nested inside it (including further
            // calls) can contain one either — so this check is complete for the whole subtree.
            return ContainsThis(node);
        }

        if (type is NodeType.FunctionExpression or NodeType.ArrowFunctionExpression or NodeType.FunctionDeclaration)
        {
            return false;
        }

        foreach (var childNode in node.ChildNodes)
        {
            if (!childNode.ChildNodes.IsEmpty() && ContainsThisEscapingCall(childNode))
            {
                return true;
            }
        }

        return false;
    }

    private static bool ContainsThis(Node node)
    {
        foreach (var childNode in node.ChildNodes)
        {
            if (childNode.Type == NodeType.ThisExpression
                || (!childNode.ChildNodes.IsEmpty() && ContainsThis(childNode)))
            {
                return true;
            }
        }

        return false;
    }

    private static void GetBoundNames(
        Node parameter,
        List<Key> target,
        ref bool hasParameterExpressions,
        ref bool hasDuplicates,
        ref bool hasArguments)
    {
Start:
        if (parameter.Type == NodeType.Identifier)
        {
            var key = (Key) ((Identifier) parameter).Name;
            hasDuplicates |= target.Contains(key);
            target.Add(key);
            hasArguments |= key == KnownKeys.Arguments;
            return;
        }

        while (true)
        {
            if (parameter.Type == NodeType.RestElement)
            {
                parameter = ((RestElement) parameter).Argument;
                continue;
            }

            if (parameter.Type == NodeType.ArrayPattern)
            {
                foreach (var element in ((ArrayPattern) parameter).Elements.AsSpan())
                {
                    if (element is null)
                    {
                        continue;
                    }

                    if (element.Type == NodeType.RestElement)
                    {
                        parameter = ((RestElement) element).Argument;
                        goto Start;
                    }

                    GetBoundNames(
                        element,
                        target,
                        ref hasParameterExpressions,
                        ref hasDuplicates,
                        ref hasArguments);
                }
            }
            else if (parameter.Type == NodeType.ObjectPattern)
            {
                foreach (var property in ((ObjectPattern) parameter).Properties.AsSpan())
                {
                    if (property.Type == NodeType.RestElement)
                    {
                        parameter = ((RestElement) property).Argument;
                        goto Start;
                    }

                    GetBoundNames(
                        ((AssignmentProperty) property).Value,
                        target,
                        ref hasParameterExpressions,
                        ref hasDuplicates,
                        ref hasArguments);
                }
            }
            else if (parameter.Type == NodeType.AssignmentPattern)
            {
                var assignmentPattern = (AssignmentPattern) parameter;
                hasParameterExpressions |= ExpressionAstVisitor.HasExpression(assignmentPattern.ChildNodes);
                parameter = assignmentPattern.Left;

                // need to goto Start so Identifier case is handled
                goto Start;
            }

            break;
        }
    }

    private static void ProcessParameters(
        IFunction function,
        State state,
        out bool hasArguments)
    {
        hasArguments = false;
        state.IsSimpleParameterList = true;

        var countParameters = true;
        ref readonly var functionDeclarationParams = ref function.Params;
        var count = functionDeclarationParams.Count;
        var parameterNames = new List<Key>(count);
        foreach (var parameter in function.Params.AsSpan())
        {
            var type = parameter.Type;

            if (type == NodeType.Identifier)
            {
                var key = (Key) ((Identifier) parameter).Name;
                state.HasDuplicates |= parameterNames.Contains(key);
                hasArguments |= key == KnownKeys.Arguments;
                parameterNames.Add(key);
            }
            else if (type != NodeType.Literal)
            {
                countParameters &= type != NodeType.AssignmentPattern;
                state.IsSimpleParameterList = false;
                GetBoundNames(
                    parameter,
                    parameterNames,
                    ref state.HasParameterExpressions,
                    ref state.HasDuplicates,
                    ref hasArguments);
            }

            if (countParameters && type is NodeType.Identifier or NodeType.ObjectPattern or NodeType.ArrayPattern)
            {
                state.Length++;
            }
        }

        state.ParameterNames = parameterNames.ToArray();
    }

    private static class ArgumentsUsageAstVisitor
    {
        public static bool HasArgumentsReference(IFunction function)
        {
            if (HasArgumentsReference(function.Body))
            {
                return true;
            }

            foreach (var parameter in function.Params.AsSpan())
            {
                if (HasArgumentsReference(parameter))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool HasArgumentsReference(Node node)
        {
            foreach (var childNode in node.ChildNodes)
            {
                var childType = childNode.Type;
                if (childType == NodeType.Identifier)
                {
                    if (string.Equals(((Identifier) childNode).Name, "arguments", StringComparison.Ordinal))
                    {
                        return true;
                    }
                }
                else if (childType != NodeType.FunctionDeclaration && !childNode.ChildNodes.IsEmpty())
                {
                    if (HasArgumentsReference(childNode))
                    {
                        return true;
                    }
                }
            }

            return false;
        }
    }

    private static class ThisSuperNewTargetAstVisitor
    {
        public static bool HasReference(IFunction function)
        {
            foreach (var parameter in function.Params.AsSpan())
            {
                if (HasReference(parameter))
                {
                    return true;
                }
            }

            return HasReference(function.Body);
        }

        private static bool HasReference(Node node)
        {
            foreach (var childNode in node.ChildNodes)
            {
                var childType = childNode.Type;

                // MetaProperty matches both new.target and import.meta — over-approximation is
                // the safe direction. Nested functions are deliberately scanned too (an arrow's
                // `this` resolves through this frame; consumers gate on !EnvironmentMayEscape,
                // which excludes nested functions, so the over-match never costs in practice).
                if (childType is NodeType.ThisExpression or NodeType.Super or NodeType.MetaProperty)
                {
                    return true;
                }

                if (!childNode.ChildNodes.IsEmpty() && HasReference(childNode))
                {
                    return true;
                }
            }

            return false;
        }
    }

    /// <summary>
    /// Finds the direct eval call sites — and the debugger statements — in a function's parameters
    /// and body, in one walk, for the two decisions that turn on them.
    /// </summary>
    private static class EvalContextAstVisitor
    {
        [Flags]
        public enum Usage
        {
            None = 0,

            /// <summary>
            /// A direct eval or a debugger statement anywhere: the function's top-level lexical
            /// declarations want a lexical environment of their own, so that a direct eval can tell
            /// whether the var declarations it introduces conflict with them.
            /// </summary>
            EvalContext = 1,

            /// <summary>
            /// A direct eval that resolves <c>arguments</c> in <em>this</em> function's scope, so the
            /// arguments object has to exist even though the token appears nowhere in the source.
            /// </summary>
            ArgumentsReachingEval = 2,

            Both = EvalContext | ArgumentsReachingEval,
        }

        public static Usage Scan(IFunction function)
        {
            var usage = Usage.None;

            // A parameter default is evaluated after the arguments object has been created and bound
            // (https://tc39.es/ecma262/#sec-functiondeclarationinstantiation, steps 22 and 24), so a
            // direct eval there reaches it exactly as one in the body does.
            foreach (var parameter in function.Params.AsSpan())
            {
                if (!parameter.ChildNodes.IsEmpty())
                {
                    Scan(parameter, sharesArguments: true, ref usage);
                    if (usage == Usage.Both)
                    {
                        return usage;
                    }
                }
            }

            Scan(function.Body, sharesArguments: true, ref usage);
            return usage;
        }

        private static void Scan(Node node, bool sharesArguments, ref Usage usage)
        {
            foreach (var childNode in node.ChildNodes)
            {
                if (usage == Usage.Both)
                {
                    return;
                }

                var childType = childNode.Type;
                if (childType == NodeType.DebuggerStatement)
                {
                    usage |= Usage.EvalContext;
                    continue;
                }

                if (childType == NodeType.FunctionDeclaration)
                {
                    // Its own arguments object, its own eval context, its own State: nothing inside
                    // a nested declaration is this function's business.
                    continue;
                }

                if (childType == NodeType.CallExpression
                    && ((CallExpression) childNode).Callee is Identifier { Name: "eval" })
                {
                    // An over-approximation of the runtime test (JintCallExpression resolves the
                    // callee and compares it against the eval intrinsic), which is the safe
                    // direction: a call site that turns out not to be eval costs a fast lane.
                    usage |= sharesArguments ? Usage.Both : Usage.EvalContext;
                }

                if (!childNode.ChildNodes.IsEmpty())
                {
                    // A nested function has an arguments object of its own, and that is the one its
                    // direct eval names. An arrow has none, so an eval inside it still names ours.
                    Scan(childNode, sharesArguments && childType != NodeType.FunctionExpression, ref usage);
                }
            }
        }
    }

    private static class ExpressionAstVisitor
    {
        internal static bool HasExpression(ChildNodes nodes)
        {
            foreach (var childNode in nodes)
            {
                switch (childNode.Type)
                {
                    case NodeType.ArrowFunctionExpression:
                    case NodeType.FunctionExpression:
                    case NodeType.CallExpression:
                    case NodeType.AssignmentExpression:
                        return true;
                    case NodeType.Identifier:
                    case NodeType.Literal:
                        continue;
                    default:
                        if (!childNode.ChildNodes.IsEmpty())
                        {
                            if (HasExpression(childNode.ChildNodes))
                            {
                                return true;
                            }
                        }

                        break;
                }
            }

            return false;
        }
    }

    /// <summary>
    /// Looks for a direct call (`name(...)`) anywhere inside a node tree. Used to detect
    /// recursive functions so they can opt out of the FunctionEnvironment pool. Recurses into
    /// inner functions/classes since the same name in a nested closure is still a self-call
    /// (closure captures the outer binding). False positives are acceptable — the only effect
    /// is that the pool is bypassed for that function, which is the conservative direction.
    /// </summary>
    internal static class SelfCallAstVisitor
    {
        internal static bool ContainsCallTo(Node node, string name)
        {
            foreach (var childNode in node.ChildNodes)
            {
                if (childNode.Type == NodeType.CallExpression
                    && ((CallExpression) childNode).Callee is Identifier id
                    && string.Equals(id.Name, name, StringComparison.Ordinal))
                {
                    return true;
                }

                if (!childNode.ChildNodes.IsEmpty() && ContainsCallTo(childNode, name))
                {
                    return true;
                }
            }

            return false;
        }
    }

    /// <summary>
    /// Checks if a function's per-call environment may escape (be captured by closures).
    /// If true, the environment cannot be pooled/cached for reuse.
    /// </summary>
    internal static class EnvironmentEscapeAstVisitor
    {
        internal static bool MayEscape(IFunction function)
        {
            // Parameter default/pattern expressions can contain closures (and direct eval) too:
            // `function f(a, get = function () { return a; }) { return get; }` — the escaped closure
            // resolves `a` through the call's environment chain, so the environment must not be reused.
            // (MayEscapeWithReferences doesn't need this: fixed slots require !HasParameterExpressions,
            // so its parameters are always plain identifiers.)
            foreach (var parameter in function.Params)
            {
                if (!parameter.ChildNodes.IsEmpty() && MayEscape(parameter))
                {
                    return true;
                }
            }

            var body = function.Body;
            if (IsCapturing(body))
            {
                return true;
            }
            return MayEscape(body);
        }

        /// <summary>
        /// Smarter escape analysis: checks if any closures in the function body actually reference
        /// any of the specified slot variable names. If closures exist but don't reference any slot
        /// variables, the environment can still be safely cached.
        /// </summary>
        internal static bool MayEscapeWithReferences(IFunction function, Key[] slotNames)
        {
            var body = function.Body;

            // For concise arrows like x => y => x * y, the body itself is a closure
            if (IsCapturing(body))
            {
                return ClosureReferencesAny(body, slotNames);
            }

            return ScanForCapturingReferences(body, slotNames);
        }

        internal static bool IsCapturing(Node node)
        {
            if (node.Type is NodeType.FunctionDeclaration
                or NodeType.FunctionExpression
                or NodeType.ArrowFunctionExpression
                or NodeType.ClassDeclaration
                or NodeType.ClassExpression
                or NodeType.WithStatement)
            {
                return true;
            }

            // Direct eval() can dynamically create closures that capture the environment
            if (node.Type == NodeType.CallExpression
                && ((CallExpression) node).Callee is Identifier { Name: "eval" })
            {
                return true;
            }

            return false;
        }

        internal static bool MayEscape(Node node)
        {
            foreach (var childNode in node.ChildNodes)
            {
                // Captures the environment — function/class/eval/with create closures over bindings
                if (IsCapturing(childNode))
                {
                    return true;
                }

                // Safe to recurse: IsCapturing already caught function/class/eval/with nodes,
                // so we only recurse into non-capturing nodes (blocks, if/else, loops, etc.)
                if (!childNode.ChildNodes.IsEmpty() && MayEscape(childNode))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Scans the node tree for closures that reference any of the specified slot names.
        /// When a closure is found, its body is searched for identifier references matching slot names.
        /// eval() and with statements always cause escape (dynamic references can't be analyzed).
        /// </summary>
        private static bool ScanForCapturingReferences(Node node, Key[] slotNames)
        {
            foreach (var childNode in node.ChildNodes)
            {
                // eval() and with statement always capture — can't analyze dynamic references
                if (childNode.Type == NodeType.WithStatement)
                {
                    return true;
                }

                if (childNode.Type == NodeType.CallExpression
                    && ((CallExpression) childNode).Callee is Identifier { Name: "eval" })
                {
                    return true;
                }

                // Found a closure — check if it references any slot variables
                if (childNode.Type is NodeType.FunctionDeclaration
                    or NodeType.FunctionExpression
                    or NodeType.ArrowFunctionExpression
                    or NodeType.ClassDeclaration
                    or NodeType.ClassExpression)
                {
                    if (ClosureReferencesAny(childNode, slotNames))
                    {
                        return true;
                    }
                    // Closure doesn't reference any slot vars — skip it
                    continue;
                }

                // Recurse into non-capturing nodes (blocks, if/else, loops, etc.)
                if (!childNode.ChildNodes.IsEmpty() && ScanForCapturingReferences(childNode, slotNames))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Checks if a closure node (function/arrow/class) references any of the specified names.
        /// Walks the entire closure tree looking for matching identifiers, including nested functions
        /// (since they can access outer variables through the scope chain).
        /// </summary>
        private static bool ClosureReferencesAny(Node closureNode, Key[] slotNames)
        {
            foreach (var childNode in closureNode.ChildNodes)
            {
                if (childNode.Type == NodeType.Identifier)
                {
                    var name = ((Identifier) childNode).Name;
                    for (var i = 0; i < slotNames.Length; i++)
                    {
                        if (string.Equals(slotNames[i].Name, name, StringComparison.Ordinal))
                        {
                            return true;
                        }
                    }
                    continue;
                }

                // The call's FunctionEnvironment also carries the this-binding, new.target and the
                // super base — an arrow (transitively) captures those lexically, so a closure whose
                // only dependency is `this`/`new.target`/`super` still pins the environment:
                //   function f(a) { var h = () => this; return h; }
                // Reusing the env would rebind `this` under the escaped arrow. Conservative: any such
                // node in the subtree counts as a reference (a nested non-arrow function's `this` would
                // actually re-bind, but that over-approximation is cheap and always safe).
                if (childNode.Type is NodeType.ThisExpression or NodeType.MetaProperty or NodeType.Super)
                {
                    return true;
                }

                // eval() inside the closure can access any outer variable
                if (childNode.Type == NodeType.CallExpression
                    && ((CallExpression) childNode).Callee is Identifier { Name: "eval" })
                {
                    return true;
                }

                if (!childNode.ChildNodes.IsEmpty() && ClosureReferencesAny(childNode, slotNames))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
