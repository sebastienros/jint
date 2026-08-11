using System.Runtime.CompilerServices;
using System.Threading;
using Jint.Native.Object;
using Jint.Pooling;
using Jint.Runtime;
using Jint.Runtime.Descriptors;
using Jint.Runtime.Environments;
using Jint.Runtime.Interpreter;
using Jint.Runtime.Interpreter.Expressions;
using Environment = Jint.Runtime.Environments.Environment;

namespace Jint.Native.Function;

public sealed class ScriptFunction : Function, IConstructor
{
    internal bool _isClassConstructor;
    internal JsValue? _classFieldInitializerName;

    // Whether this function's code is strict, resolved once at creation. Normally that is a static
    // property of the AST: the parser marks the FunctionBody, propagating an enclosing "use strict"
    // into every nested function. An arrow function with a *concise* (expression) body has no
    // FunctionBody node to carry the mark, yet is still strict when it appears in strict code
    // (https://tc39.es/ecma262/#sec-strict-mode-code), so JintArrowFunctionExpression ORs in the
    // strictness of the running context it was created from.
    internal bool _strict;

    // Own restricted "arguments"/"caller" properties of non-strict, non-arrow, non-generator,
    // non-async functions. Dedicated fields (like Function's name/length/prototype) instead of
    // dictionary entries, so a plain sloppy function's instantiation allocates neither the
    // property dictionary nor the two descriptors: the fields start at the pending sentinel and
    // materialize on first read. null means absent (strict functions, methods after MakeMethod,
    // or deleted); a deleted-then-redefined property goes to the dictionary, which preserves the
    // previous key order after resurrection. Enumeration order (length, name, prototype,
    // arguments, caller) matches the old ctor-time dictionary inserts.
    internal PropertyDescriptor? _argumentsDescriptor;
    internal PropertyDescriptor? _callerDescriptor;

    // Reuse cache for this function's call environments, populated on a pool-eligible call's return.
    // Interpreted via State.IsDirectRecursive: a FunctionEnvironment (the next call reuses it directly)
    // for ordinary functions, or a RecursiveEnvPool for direct-recursive ones. Held per function instance
    // (thus per engine) rather than on the shared JintFunctionDefinition.State so a prepared script reused
    // across engines never pins an engine via a cached environment (issue #2560). Cleared slot arrays hold
    // no engine references and stay shared on State._cachedSlots.
    internal object? _envReuse;

    internal List<PrivateElement>? _privateMethods;
    internal List<ClassFieldDefinition>? _fields;

    // Allocation-site feedback for shaping `new T()` instances. A constructor's first
    // CtorShapePromoteThreshold instances build dictionaries (so a constructor called once or twice — the
    // overwhelming norm, e.g. across the Test262 suite — never grows the shared per-prototype transition
    // tree); once it proves "hot" it is promoted to shape mode so repeated `new T()` with a stable layout
    // reuse one interned hidden class. _ctorEmptyShape caches the prototype's empty root to avoid a
    // per-construct lookup (revalidated when .prototype is reassigned).
    private const int CtorShapePromoteThreshold = 16;
    private bool _ctorShaped;

    // Static eligibility verdict for this function instance: 0 = not yet analyzed, 1 = eligible
    // (body statically clean AND every class field name shape-compatible), 2 = ineligible. Provably
    // clean constructors skip the sampling window and shape from instance #3 — a short-lived engine
    // constructing 3-15 instances of each type otherwise never promotes — while instances #1 and #2
    // stay on the dictionary path: the shared per-prototype transition tree only pays off from about
    // three instances of a layout, so constructors of unrepeated layouts intern no shape state at
    // all. Combines the AST-pure State.CtorBodyShapeEligibility with the per-function class-fields
    // check; the combined verdict cannot live on the shared State because the shared
    // empty-constructor AST serves classes with different fields.
    private byte _ctorStaticEligibility;
    private int _ctorSampleCount;
    private Shape? _ctorEmptyShape;
    private ObjectInstance? _ctorEmptyShapeProto;

    /// <summary>
    /// http://www.ecma-international.org/ecma-262/5.1/#sec-13.2
    /// </summary>
    public ScriptFunction(
        Engine engine,
        IFunction functionDeclaration,
        bool strict,
        ObjectInstance? proto = null)
        : this(
            engine,
            new JintFunctionDefinition(functionDeclaration),
            JintEnvironment.NewDeclarativeEnvironment(engine, engine.ExecutionContext.LexicalEnvironment),
            strict ? FunctionThisMode.Strict : FunctionThisMode.Global,
            proto)
    {
    }

    internal ScriptFunction(
        Engine engine,
        JintFunctionDefinition function,
        Environment env,
        FunctionThisMode thisMode,
        ObjectInstance? proto = null)
        : base(engine, engine.Realm, function, env, thisMode)
    {
        _prototype = proto ?? _engine.Realm.Intrinsics.Function.PrototypeObject;
        // The own "length" property exists from birth; its descriptor is materialized lazily
        // from the definition on first read (see Function._pendingDescriptor).
        _length = _pendingDescriptor;
        _strict = function.Strict || thisMode == FunctionThisMode.Strict;

        if (!function.Strict
            && function.Function is not ArrowFunctionExpression
            && !function.Function.Generator
            && !function.Function.Async)
        {
            _argumentsDescriptor = _pendingDescriptor;
            _callerDescriptor = _pendingDescriptor;
        }
    }

    internal PropertyDescriptor MaterializeArgumentsDescriptor()
    {
        // Same deferred %ThrowTypeError% resolution as the old eager descriptor: the thrower is
        // looked up from the engine's active realm on the first Get/Set access either way.
        return _argumentsDescriptor = new GetSetPropertyDescriptor.ThrowerPropertyDescriptor(_engine, PropertyFlag.Configurable);
    }

    internal PropertyDescriptor MaterializeCallerDescriptor()
    {
        return _callerDescriptor = new PropertyDescriptor(Undefined, PropertyFlag.Configurable);
    }

    /// <summary>
    /// Stores a replacement descriptor for a currently field-backed restricted property. Returns
    /// false when the property is not field-backed (never was, or was deleted), in which case the
    /// caller stores it in the property dictionary — putting a resurrected property at the end of
    /// the key order exactly like the previous dictionary-backed representation did.
    /// </summary>
    internal bool TrySetRestrictedOwnProperty(JsValue property, PropertyDescriptor desc)
    {
        if (_argumentsDescriptor is not null && CommonProperties.Arguments.Equals(property))
        {
            _argumentsDescriptor = desc;
            return true;
        }

        if (_callerDescriptor is not null && CommonProperties.Caller.Equals(property))
        {
            _callerDescriptor = desc;
            return true;
        }

        return false;
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-ecmascript-function-objects-call-thisargument-argumentslist
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected internal override JsValue Call(JsValue thisObject, JsCallArguments arguments)
    {
        var result = CallOnce(thisObject, arguments);
        return result is TailCallRequest ? ContinueTailCalls(result, ownsFrame: false) : result;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal JsValue CallWithStackFrame(JsValue thisObject, JsCallArguments arguments)
    {
        var result = CallOnce(thisObject, arguments);
        return result is TailCallRequest ? ContinueTailCalls(result, ownsFrame: true) : result;
    }

    private JsValue CallOnce(JsValue thisObject, JsCallArguments arguments)
    {
        var state = _functionDefinition!.Initialize();
        var strict = _strict;
        _functionDefinition.EnsureTailCallMarkers(state, strict);

        // Env-less leaf call: no bindings to create, no this/arguments/new.target route, no
        // closures — the callee FunctionEnvironment would exist only as a chain pointer, so the
        // frame runs against the captured environment directly. `arguments` are intentionally
        // ignored (0 params, no arguments object). Strictness rides on the pushed execution
        // context (set in CallLeaf), so no separate scope is needed here.
        if (state.SupportsLeafCall && !_engine._isDebugMode && !_isClassConstructor)
        {
            return CallLeaf(strict);
        }

        // Fixed-slot synchronous call: shares its whole body with the register-argument entry point
        // (CallFromRegisters) through CallCore, which the ArrayArguments source specializes back to
        // reading this very array. Disjoint from the leaf arm above — SupportsLeafCall requires
        // CanUseEmptyFDI (no slots at all) while SupportsRegisterCall requires CanUseFastFDI (at
        // least one). Anything else falls through to the general arm below unchanged.
        if (state.SupportsRegisterCall && !_engine._isDebugMode && !_isClassConstructor)
        {
            return CallCore(thisObject, new ArrayArguments(arguments), state);
        }

        FunctionEnvironment? funcEnv = null;

        try
        {
            ref readonly var calleeContext = ref PrepareForOrdinaryCall(Undefined, state, strict);

            if (_isClassConstructor)
            {
                Throw.TypeError(calleeContext.Realm, $"Class constructor {_functionDefinition.Name} cannot be invoked without 'new'");
            }

            // Capture funcEnv for end-of-call pool return when bindings can't escape. Direct-recursive
            // functions return their env to a small bounded pool (so each live frame reuses a distinct
            // env); other non-escaping functions use the single-slot pool. Escaping envs are not pooled.
            if (!state.EnvironmentMayEscape)
            {
                funcEnv = (FunctionEnvironment) calleeContext.LexicalEnvironment;
            }

            // Bodies that provably never resolve this/super/new.target leave the this-binding
            // Uninitialized (any missed route throws via GetThisBinding rather than silently
            // observing a wrong value). The debugger reads the binding through CallFrame.This,
            // so debug mode always binds.
            if (!state.CanSkipThisBinding || _engine._isDebugMode)
            {
                OrdinaryCallBindThis(in calleeContext, thisObject);
            }

            // actual call
            var context = _engine._evaluationContext;

            var result = _functionDefinition.EvaluateBody(context, this, arguments, state);

            // For async functions/generators, DisposeResources is deferred to when
            // the body truly completes (AsyncBlockStart/AsyncFunctionResume).
            // Calling it here would dispose too early (before awaits complete).
            if (!_functionDefinition.Function.Async)
            {
                result = calleeContext.LexicalEnvironment.DisposeResources(result);
            }

            if (result.Type == CompletionType.Throw)
            {
                Throw.JavaScriptException(_engine, result.Value, in result);
            }

            // The DebugHandler needs the current execution context before the return for stepping through the return point
            if (context.DebugMode)
            {
                // We don't have a statement, but we still need a Location for debuggers. DebugHandler will infer one from
                // the function body:
                _engine.Debugger.OnReturnPoint(
                    _functionDefinition.Function.Body,
                    result.Type == CompletionType.Normal ? Undefined : result.Value
                );
            }

            if (result.Type == CompletionType.Return)
            {
                return result.Value;
            }
        }
        finally
        {
            if (funcEnv is not null)
            {
                ReturnEnvironment(funcEnv, state);
            }
            _engine.LeaveExecutionContext();
        }

        return Undefined;
    }

    /// <summary>
    /// The end-of-call environment-reuse step, shared by <see cref="Call"/>'s general arm and by
    /// <see cref="CallCore{TArgs}"/>: hands a non-escaping call environment (and its fixed-slot
    /// array) to whichever cache can serve the next call.
    /// </summary>
    private void ReturnEnvironment(FunctionEnvironment funcEnv, JintFunctionDefinition.State state)
    {
        // Cache on this function instance (per-engine by construction, so a prepared script's
        // shared State can't pin engines — see _envReuse). Single-threaded like the engine, so
        // no Interlocked is needed on the instance side.
        if (state.IsDirectRecursive)
        {
            // Return the env (with its fixed-slot array still attached) to the bounded
            // recursive pool so another simultaneously live frame can reuse env + slots.
            if (funcEnv._slots is { } recursiveSlots)
            {
                System.Array.Clear(recursiveSlots, 0, recursiveSlots.Length);
            }
            var pool = _envReuse as RecursiveEnvPool;
            if (pool is null)
            {
                _envReuse = pool = new RecursiveEnvPool();
            }
            pool.Return(funcEnv);
        }
        else
        {
            // Cache the slot array on the shared State: cleared, it holds no engine references,
            // so any instance sharing this State (also in another engine) can reuse it.
            if (funcEnv._slots is { } slots)
            {
                System.Array.Clear(slots, 0, slots.Length);
                Interlocked.Exchange(ref state._cachedSlots, slots);
                funcEnv._slots = null;
            }

            if (_functionDefinition!.IsDynamic)
            {
                // Function-constructor instances are one-shot (a fresh ScriptFunction per
                // `new Function(...)`), so an instance-level cache never warms. Park the env
                // on the per-realm definition instead — env identity then stays stable across
                // instances, keeping the shared statement tree's per-node slot caches valid.
                funcEnv._outerEnv = null;
                Interlocked.Exchange(ref state._dynamicCachedEnv, funcEnv);
            }
            else
            {
                // Cache the env itself so the next call to this function avoids the allocation.
                _envReuse = funcEnv;
            }
        }
    }

    /// <summary>
    /// The shared body behind this function's two fast synchronous [[Call]] entry points — the
    /// array-backed <see cref="Call"/> arm and the register-backed <see cref="CallFromRegisters"/> —
    /// generic over where the argument values live, so the JIT specializes it per source and the
    /// register form never materializes an argument array.
    /// </summary>
    /// <remarks>
    /// Covers only what both callers gate on: <see cref="JintFunctionDefinition.State.SupportsRegisterCall"/>
    /// (fixed-slot FDI, neither generator nor async), not in debug mode, not a class constructor.
    /// Those preconditions are exactly what removes the three things <see cref="Call"/>'s general arm
    /// still carries — the async DisposeResources deferral, the JsArguments materialization (fixed
    /// slots require !ArgumentsObjectNeeded) and the debugger's OnReturnPoint hook. Everything else
    /// is step for step the general arm.
    /// </remarks>
    private JsValue CallCore<TArgs>(JsValue thisObject, in TArgs args, JintFunctionDefinition.State state)
        where TArgs : struct, IArgumentSource
    {
        FunctionEnvironment? funcEnv = null;

        try
        {
            ref readonly var calleeContext = ref PrepareForOrdinaryCall(Undefined, state, _strict);

            // Capture funcEnv for end-of-call pool return when bindings can't escape.
            if (!state.EnvironmentMayEscape)
            {
                funcEnv = (FunctionEnvironment) calleeContext.LexicalEnvironment;
            }

            // Bodies that provably never resolve this/super/new.target leave the this-binding
            // Uninitialized. Debug mode, which always binds, is excluded by the gate.
            if (!state.CanSkipThisBinding)
            {
                OrdinaryCallBindThis(in calleeContext, thisObject);
            }

            // actual call
            var context = _engine._evaluationContext;

            var result = _functionDefinition!.EvaluateBodyFast(context, in args, state);

            // Not async by the gate, so disposal is never deferred to AsyncBlockStart.
            result = calleeContext.LexicalEnvironment.DisposeResources(result);

            if (result.Type == CompletionType.Throw)
            {
                Throw.JavaScriptException(_engine, result.Value, in result);
            }

            if (result.Type == CompletionType.Return)
            {
                return result.Value;
            }
        }
        finally
        {
            if (funcEnv is not null)
            {
                ReturnEnvironment(funcEnv, state);
            }
            _engine.LeaveExecutionContext();
        }

        return Undefined;
    }

    /// <summary>
    /// The register-argument [[Call]] entry point: same observable behaviour as <see cref="Call"/>,
    /// but the arguments arrive in locals instead of through a rented <see cref="JsCallArguments"/>.
    /// Arguments the site did not supply arrive as <see cref="JsValue.Undefined"/>, matching
    /// <c>Arguments.At</c>; <paramref name="argCount"/> is the site's real arity, so registers beyond
    /// it are never read.
    /// </summary>
    /// <remarks>
    /// Only valid when <paramref name="state"/> is this function's own state, its
    /// <see cref="JintFunctionDefinition.State.SupportsRegisterCall"/> holds, and the caller has
    /// established !Engine._isDebugMode and !_isClassConstructor — the same gate <see cref="Call"/>
    /// applies before taking the array-backed arm. Deliberately not named CallFast: that name belongs
    /// to <see cref="Function.CallFast"/>, the arity-specialized built-in lane, which this is not.
    /// </remarks>
    internal JsValue CallFromRegisters(
        JsValue thisObject,
        JsValue arg0,
        JsValue arg1,
        JsValue arg2,
        JsValue arg3,
        int argCount,
        JintFunctionDefinition.State state,
        bool ownsFrame)
    {
        _functionDefinition!.EnsureTailCallMarkers(state, _strict);
        var result = CallCore(thisObject, new RegisterArguments(arg0, arg1, arg2, arg3, argCount), state);
        return result is TailCallRequest ? ContinueTailCalls(result, ownsFrame) : result;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static bool CanPrepareTailCall(EvaluationContext context, bool isTailPosition)
    {
        var engine = context.Engine;
        return isTailPosition
               && engine.ExecutionContext.Strict
               && engine.ExecutionContext.Suspendable is null
               && !engine._isDebugMode;
    }

    /// <summary>
    /// Runs interpreted proper tail calls as a trampoline, after the caller's execution context and
    /// environment have unwound. Replacing the visible frame keeps error stacks and recursion tracking
    /// aligned with the execution-context replacement required by PrepareForTailCall.
    /// </summary>
    private JsValue ContinueTailCalls(JsValue result, bool ownsFrame)
    {
        var engine = _engine;
        var callStack = engine.CallStack;
        var pushedFrame = false;
        Function? currentFrame = ownsFrame ? this : null;
        JintFunctionDefinition? depthDefinition0 = null;
        JintFunctionDefinition? depthDefinition1 = null;
        var depth0 = 0;
        var depth1 = 0;
        Dictionary<JintFunctionDefinition, int>? tailDepths = null;
        if (engine._maxRecursionDepth >= 0)
        {
            depthDefinition0 = _functionDefinition!;
            depth0 = ownsFrame
                ? callStack.GetRecursionDepth(this)
                : callStack.GetNextRecursionDepth(this);
        }

        try
        {
            while (result is TailCallRequest request)
            {
                var target = request.Target;
                var thisObject = request.ThisObject;
                var arguments = request.Arguments;
                var argumentsRented = request.ArgumentsRented;
                var expression = request.Expression;
                var registerState = request.RegisterState;
                var arg0 = request.Arg0;
                var arg1 = request.Arg1;
                var arg2 = request.Arg2;
                var arg3 = request.Arg3;
                var argCount = request.ArgCount;
                engine.ReturnTailCallRequest(request);

                try
                {
                    if (currentFrame is not null && callStack.TopIs(currentFrame))
                    {
                        callStack.ReplaceTop(target, expression);
                    }
                    else
                    {
                        callStack.Push(target, expression, engine.ExecutionContext);
                        pushedFrame = true;
                    }

                    currentFrame = target;
                    if (depthDefinition0 is not null)
                    {
                        var definition = target._functionDefinition!;
                        int recursionDepth;
                        if (ReferenceEquals(definition, depthDefinition0))
                        {
                            recursionDepth = ++depth0;
                        }
                        else if (ReferenceEquals(definition, depthDefinition1))
                        {
                            recursionDepth = ++depth1;
                        }
                        else if (depthDefinition1 is null)
                        {
                            depthDefinition1 = definition;
                            recursionDepth = depth1 = callStack.GetRecursionDepth(target);
                        }
                        else
                        {
                            tailDepths ??= new Dictionary<JintFunctionDefinition, int>
                            {
                                [depthDefinition0] = depth0,
                                [depthDefinition1] = depth1
                            };

                            if (tailDepths.TryGetValue(definition, out var previousDepth))
                            {
                                recursionDepth = previousDepth + 1;
                            }
                            else
                            {
                                recursionDepth = callStack.GetRecursionDepth(target);
                            }
                            tailDepths[definition] = recursionDepth;
                        }

                        if (recursionDepth > engine._maxRecursionDepth)
                        {
                            Throw.RecursionDepthOverflowException(callStack, preserveTop: true);
                        }
                    }

                    result = registerState is null
                        ? target.CallOnce(thisObject, arguments)
                        : target.CallCore(
                            thisObject,
                            new RegisterArguments(arg0, arg1, arg2, arg3, argCount),
                            registerState);
                }
                finally
                {
                    if (argumentsRented)
                    {
                        engine._jsValueArrayPool.ReturnArray(arguments);
                    }
                }
            }

            return result;
        }
        finally
        {
            if (pushedFrame)
            {
                callStack.TryPop(out _);
            }
        }
    }

    /// <summary>
    /// The env-less [[Call]] arm for <see cref="JintFunctionDefinition.State.SupportsLeafCall"/>
    /// functions: pushes an execution context whose environments are the captured environment
    /// itself, runs the body statement list, and maps the completion exactly like the ordinary
    /// arm (Return → value, fall-through → undefined, Throw → JavaScriptException).
    /// Function-level DisposeResources is skipped deliberately: a leaf body cannot register
    /// function-level dispose resources (no lexical declarations), and running dispose against
    /// the CAPTURED environment would drain the enclosing function's pending `using` resources
    /// mid-lifetime. Nested blocks own their disposal end-to-end.
    /// </summary>
    private JsValue CallLeaf(bool strict)
    {
        var engine = _engine;
        engine.EnterLeafCallExecutionContext(_scriptOrModule, _environment!, _privateEnvironment, _realm, this, strict);
        try
        {
            var context = engine._evaluationContext;
            var result = _functionDefinition!.EvaluateLeafBody(context);

            if (result.Type == CompletionType.Throw)
            {
                Throw.JavaScriptException(engine, result.Value, in result);
            }

            return result.Type == CompletionType.Return ? result.Value : Undefined;
        }
        finally
        {
            engine.LeaveExecutionContext();
        }
    }

    internal override bool IsConstructor
    {
        get
        {
            if (!_homeObject.IsUndefined() && !_isClassConstructor)
            {
                return false;
            }

            var function = _functionDefinition?.Function;
            return function is not null
                   && function is not ArrowFunctionExpression
                   && !function.Generator
                   && !function.Async;
        }
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-ecmascript-function-objects-construct-argumentslist-newtarget
    /// </summary>
    ObjectInstance IConstructor.Construct(JsCallArguments arguments, JsValue newTarget)
        => Construct(arguments, newTarget, ownsFrame: false);

    internal ObjectInstance ConstructWithStackFrame(JsCallArguments arguments, JsValue newTarget)
        => Construct(arguments, newTarget, ownsFrame: true);

    private ObjectInstance Construct(JsCallArguments arguments, JsValue newTarget, bool ownsFrame)
    {
        var state = _functionDefinition!.Initialize();
        _functionDefinition.EnsureTailCallMarkers(state, _strict);
        var callerContext = _engine.ExecutionContext;
        var kind = _constructorKind;

        var thisArgument = Undefined;

        if (kind == ConstructorKind.Base)
        {
            var currentPrototypeDescriptor = _prototypeDescriptor;
            if (ReferenceEquals(newTarget, this) && ReferenceEquals(currentPrototypeDescriptor, _pendingDescriptor))
            {
                currentPrototypeDescriptor = MaterializePrototypeDescriptor();
            }

            if (ReferenceEquals(newTarget, this)
                && currentPrototypeDescriptor is { } prototypeDescriptor
                && !prototypeDescriptor.IsAccessorDescriptor())
            {
                var prototype = prototypeDescriptor.Value as ObjectInstance ?? _realm.Intrinsics.Object.PrototypeObject;
                thisArgument = new JsObject(_engine)
                {
                    _prototype = prototype
                };
            }
            else
            {
                thisArgument = OrdinaryCreateFromConstructor(
                    newTarget,
                    static intrinsics => intrinsics.Object.PrototypeObject,
                    static (Engine engine, Realm _, object? _) => new JsObject(engine));
            }

            // Once the constructor is hot, start each fresh `this` in shape-building mode so this.x= /
            // class fields transition a shared interned hidden class instead of building a dictionary.
            // Cold constructors (below the promote threshold) stay on the dictionary path — unless the
            // body is statically provably clean, in which case shaping starts already at instance #3.
            if (thisArgument is JsObject thisObject && thisObject.Prototype is { } proto)
            {
                if (_ctorShaped || CheckCtorShapeEligibility(state))
                {
                    if (!ReferenceEquals(_ctorEmptyShapeProto, proto))
                    {
                        _ctorEmptyShape = _engine.GetEmptyShape(proto);
                        _ctorEmptyShapeProto = proto;
                    }
                    thisObject.StartShapeBuilding(_ctorEmptyShape!);
                }
            }
        }

        var strict = _thisMode == FunctionThisMode.Strict;
        ref readonly var calleeContext = ref PrepareForOrdinaryCall(newTarget, state, strict);
        var constructorEnv = (FunctionEnvironment) calleeContext.LexicalEnvironment;
        TailCallRequest? tailCallRequest = null;

        try
        {
            if (kind == ConstructorKind.Base)
            {
                OrdinaryCallBindThis(in calleeContext, thisArgument);
                ((ObjectInstance) thisArgument).InitializeInstanceElements(this);
            }

            var context = _engine._evaluationContext;

            var result = _functionDefinition.EvaluateBody(context, this, arguments, state);
            result = constructorEnv.DisposeResources(result);

            // The DebugHandler needs the current execution context before the return for stepping through the return point
            // We exclude the empty constructor generated for classes without an explicit constructor.
            bool isStep = context.DebugMode &&
                          result.Type != CompletionType.Throw &&
                          _functionDefinition.Function != ClassDefinition._emptyConstructor.Value;
            if (isStep)
            {
                // We don't have a statement, but we still need a Location for debuggers. DebugHandler will infer one from
                // the function body:
                _engine.Debugger.OnReturnPoint(
                    _functionDefinition.Function.Body,
                    result.Type == CompletionType.Normal ? thisArgument : result.Value
                );
            }

            if (result is { Type: CompletionType.Return, Value: TailCallRequest request })
            {
                tailCallRequest = request;
            }
            else if (result.Type == CompletionType.Return)
            {
                if (ResolveConstructorReturn(result.Value, kind, thisArgument, callerContext.Realm) is { } returnObject)
                {
                    return returnObject;
                }
            }
            else if (result.Type == CompletionType.Throw)
            {
                Throw.JavaScriptException(_engine, result.Value, in result);
            }
        }
        finally
        {
            _engine.LeaveExecutionContext();
        }

        if (tailCallRequest is not null)
        {
            var value = ContinueTailCalls(tailCallRequest, ownsFrame);
            if (ResolveConstructorReturn(value, kind, thisArgument, callerContext.Realm) is { } returnObject)
            {
                return returnObject;
            }
        }

        return (ObjectInstance) constructorEnv.GetThisBinding();
    }

    private static ObjectInstance? ResolveConstructorReturn(
        JsValue value,
        ConstructorKind kind,
        JsValue thisArgument,
        Realm callerRealm)
    {
        if (value is ObjectInstance objectValue)
        {
            return objectValue;
        }

        if (kind == ConstructorKind.Base)
        {
            return (ObjectInstance) thisArgument;
        }

        if (!value.IsUndefined())
        {
            Throw.TypeError(callerRealm);
        }

        return null;
    }

    /// <summary>
    /// Cold-path shaping decision for a not-yet-promoted constructor: statically clean bodies (see
    /// <see cref="JintFunctionDefinition.ComputeCtorBodyShapeEligibility"/>) promote after their SECOND
    /// construction and shape from instance #3 — instances #1 and #2 stay on the dictionary path, so
    /// constructors of layouts that never recur (the overwhelming norm) intern no transition tree,
    /// empty-shape root or prototype CWT entry. Measured: shaping at instance #1 regressed a
    /// 200-distinct-one-shot-ctor guard ~20% time/alloc; shaping at instance #2 still lost ~3% time /
    /// ~440 B per eval on re-evaluated class declarations constructing exactly two instances — the
    /// interned tree pays off at ≥3 instances of a layout. (Documented alternatives: shape from #1 =
    /// `return true` from the eligible branch; from #2 = set _ctorShaped unconditionally there.)
    /// Ineligible bodies keep the sampling threshold with its pre-existing pacing (the
    /// threshold-tripping instance itself stays on the dictionary path; the next construct starts
    /// shaped). Returns whether the CURRENT instance should start in shape-building mode.
    /// </summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private bool CheckCtorShapeEligibility(JintFunctionDefinition.State state)
    {
        var eligibility = _ctorStaticEligibility;
        if (eligibility == 0)
        {
            _ctorStaticEligibility = eligibility = ComputeCtorStaticEligibility(state);
        }

        if (eligibility == 1)
        {
            // dictionaries for the first two instances; shape from the THIRD construction on
            if (++_ctorSampleCount >= 2)
            {
                _ctorShaped = true;
            }
            return false;
        }

        if (++_ctorSampleCount >= CtorShapePromoteThreshold)
        {
            _ctorShaped = true;
        }

        return false;
    }

    /// <summary>
    /// Combines the shared AST-level body verdict (cached on the cross-engine State) with this function's
    /// class-field names: every field must be a plain string key that cannot be an array index — a
    /// digit-leading key would force the ordered-enumeration deopt on first keys read. Private names are
    /// fine (PrivateFieldAdd bypasses property storage), while symbol names and decorator
    /// extra-initializer runners (Name == Undefined; they invoke arbitrary callables against `this`)
    /// reject. Field initializer bodies are deliberately not scanned: post-threshold hot constructors
    /// already run them under shape building today, and TryShapeAdd's MaxFanout bounds any dynamism.
    /// </summary>
    private byte ComputeCtorStaticEligibility(JintFunctionDefinition.State state)
    {
        var bodyEligibility = state.CtorBodyShapeEligibility;
        if (bodyEligibility == 0)
        {
            bodyEligibility = JintFunctionDefinition.ComputeCtorBodyShapeEligibility(_functionDefinition!.Function) ? (byte) 1 : (byte) 2;
            state.CtorBodyShapeEligibility = bodyEligibility;
        }

        if (bodyEligibility != 1)
        {
            return 2;
        }

        var fields = _fields;
        if (fields is not null)
        {
            for (var i = 0; i < fields.Count; i++)
            {
                var name = fields[i].Name;
                if (name is PrivateName)
                {
                    continue;
                }

                if (name is not JsString jsString)
                {
                    return 2;
                }

                var stringName = jsString.ToString();
                if (stringName.Length > 0 && char.IsDigit(stringName[0]))
                {
                    return 2;
                }
            }
        }

        return 1;
    }

    internal void MakeClassConstructor()
    {
        _isClassConstructor = true;
    }
}

/// <summary>
/// Internal completion payload used to transfer an interpreted tail call to the trampoline.
/// It is represented as a non-empty JsValue so ordinary Completion propagation carries it unchanged.
/// </summary>
internal sealed class TailCallRequest : JsValue
{
    internal TailCallRequest()
        : base(InternalTypes.TailCall)
    {
    }

    internal TailCallRequest Reassign(
        ScriptFunction target,
        JsValue thisObject,
        JsValue[] arguments,
        bool argumentsRented,
        JintExpression expression)
    {
        Target = target;
        ThisObject = thisObject;
        Arguments = arguments;
        ArgumentsRented = argumentsRented;
        RegisterState = null;
        Expression = expression;
        return this;
    }

    internal TailCallRequest Reassign(
        ScriptFunction target,
        JsValue thisObject,
        JsValue arg0,
        JsValue arg1,
        JsValue arg2,
        JsValue arg3,
        int argCount,
        JintFunctionDefinition.State state,
        JintExpression expression)
    {
        Target = target;
        ThisObject = thisObject;
        Arguments = null!;
        ArgumentsRented = false;
        Arg0 = arg0;
        Arg1 = arg1;
        Arg2 = arg2;
        Arg3 = arg3;
        ArgCount = argCount;
        RegisterState = state;
        Expression = expression;
        return this;
    }

    internal ScriptFunction Target = null!;
    internal JsValue ThisObject = null!;
    internal JsValue[] Arguments = null!;
    internal bool ArgumentsRented;
    internal JsValue Arg0 = null!;
    internal JsValue Arg1 = null!;
    internal JsValue Arg2 = null!;
    internal JsValue Arg3 = null!;
    internal int ArgCount;
    internal JintFunctionDefinition.State? RegisterState;
    internal JintExpression Expression = null!;

    public override object? ToObject() => throw new InvalidOperationException("A tail-call request escaped the interpreter trampoline.");
}

internal static class TailCallRequestReuse
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static TailCallRequest RentTailCallRequest(
        this Engine engine,
        ScriptFunction target,
        JsValue thisObject,
        JsValue[] arguments,
        bool argumentsRented,
        JintExpression expression)
    {
        var request = engine._tailCallRequest;
        engine._tailCallRequest = null;
        return (request ?? new TailCallRequest()).Reassign(target, thisObject, arguments, argumentsRented, expression);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static TailCallRequest RentTailCallRequest(
        this Engine engine,
        ScriptFunction target,
        JsValue thisObject,
        JsValue arg0,
        JsValue arg1,
        JsValue arg2,
        JsValue arg3,
        int argCount,
        JintFunctionDefinition.State state,
        JintExpression expression)
    {
        var request = engine._tailCallRequest;
        engine._tailCallRequest = null;
        return (request ?? new TailCallRequest()).Reassign(
            target,
            thisObject,
            arg0,
            arg1,
            arg2,
            arg3,
            argCount,
            state,
            expression);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void ReturnTailCallRequest(this Engine engine, TailCallRequest request)
    {
        request.Target = null!;
        request.ThisObject = null!;
        request.Arguments = null!;
        request.ArgumentsRented = false;
        request.Arg0 = null!;
        request.Arg1 = null!;
        request.Arg2 = null!;
        request.Arg3 = null!;
        request.ArgCount = 0;
        request.RegisterState = null;
        request.Expression = null!;
        engine._tailCallRequest = request;
    }
}
