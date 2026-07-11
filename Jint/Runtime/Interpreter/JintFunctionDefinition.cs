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
        engine.EnterExecutionContext(asyncContext);

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
        public bool HasRestParameter;
        public int Length;
        public Key[] ParameterNames = null!;
        public bool HasDuplicates;
        public bool IsSimpleParameterList;
        public bool HasParameterExpressions;
        public bool ArgumentsObjectNeeded;
        public bool RequiresInputArgumentsOwnership;
        public List<Key>? VarNames;
        public LinkedList<FunctionDeclaration>? FunctionsToInitialize;
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

        LinkedList<FunctionDeclaration>? functionsToInitialize = null;

        if (functionDeclarations != null)
        {
            functionsToInitialize = new LinkedList<FunctionDeclaration>();
            for (var i = functionDeclarations.Count - 1; i >= 0; i--)
            {
                var d = functionDeclarations[i];
                var fn = d.Id!.Name;
                if (state.FunctionNames.Add(fn))
                {
                    functionsToInitialize.AddFirst(d);
                }
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

        if (state.ArgumentsObjectNeeded)
        {
            // just one extra check...
            state.ArgumentsObjectNeeded = ArgumentsUsageAstVisitor.HasArgumentsReference(function);
        }

        state.NeedsEvalContext = !strict;
        if (state.NeedsEvalContext)
        {
            // yet another extra check
            state.NeedsEvalContext = EvalContextAstVisitor.HasEvalOrDebugger(function);
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
                for (var i = 0; i < varsToInitialize.Count; i++)
                {
                    slotNames[varOffset + i] = varsToInitialize[i].Name;
                }

                // Add lexical declaration names (let/const)
                if (lexicalBindingCount > 0)
                {
                    var lexOffset = varOffset + varsToInitialize.Count;
                    foreach (var decl in lexDecls!.Value.Declarations)
                    {
                        foreach (var bn in decl.BoundNames)
                        {
                            slotNames[lexOffset++] = bn;
                        }
                    }
                }

                state.SlotNames = slotNames;
                state.ParameterSlotCount = state.ParameterNames.Length;
                state.VarSlotCount = varsToInitialize.Count;
                state.UseFixedSlots = true;
                state.CanUseFastFDI = lexicalBindingCount == 0;
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

    private static void GetBoundNames(
        Node parameter,
        List<Key> target,
        ref bool hasRestParameter,
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
                hasRestParameter = true;
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
                        hasRestParameter = true;
                        parameter = ((RestElement) element).Argument;
                        goto Start;
                    }

                    GetBoundNames(
                        element,
                        target,
                        ref hasRestParameter,
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
                        hasRestParameter = true;
                        parameter = ((RestElement) property).Argument;
                        goto Start;
                    }

                    GetBoundNames(
                        ((AssignmentProperty) property).Value,
                        target,
                        ref hasRestParameter,
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
                    ref state.HasRestParameter,
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

    private static class EvalContextAstVisitor
    {
        public static bool HasEvalOrDebugger(IFunction function)
        {
            if (HasEvalOrDebugger(function.Body))
            {
                return true;
            }

            return false;
        }

        private static bool HasEvalOrDebugger(Node node)
        {
            foreach (var childNode in node.ChildNodes)
            {
                var childType = childNode.Type;
                if (childType == NodeType.DebuggerStatement)
                {
                    return true;
                }

                if (childType == NodeType.CallExpression)
                {
                    if (((CallExpression) childNode).Callee is Identifier identifier && identifier.Name.Equals("eval", StringComparison.Ordinal))
                    {
                        return true;
                    }
                }
                else if (childType != NodeType.FunctionDeclaration && !childNode.ChildNodes.IsEmpty())
                {
                    if (HasEvalOrDebugger(childNode))
                    {
                        return true;
                    }
                }
            }

            return false;
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
