using System.Runtime.CompilerServices;
using System.Threading;
using Jint.Native.Object;
using Jint.Runtime;
using Jint.Runtime.Descriptors;
using Jint.Runtime.Environments;
using Jint.Runtime.Interpreter;
using Environment = Jint.Runtime.Environments.Environment;

namespace Jint.Native.Function;

public sealed class ScriptFunction : Function, IConstructor
{
    internal bool _isClassConstructor;
    internal JsValue? _classFieldInitializerName;

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
    protected internal override JsValue Call(JsValue thisObject, JsCallArguments arguments)
    {
        var state = _functionDefinition!.Initialize();
        var strict = _functionDefinition.Strict || _thisMode == FunctionThisMode.Strict;

        // Env-less leaf call: no bindings to create, no this/arguments/new.target route, no
        // closures — the callee FunctionEnvironment would exist only as a chain pointer, so the
        // frame runs against the captured environment directly. `arguments` are intentionally
        // ignored (0 params, no arguments object). The StrictModeScope push is itself skippable
        // when the ambient strictness already matches: every reader consumes only the boolean.
        if (state.SupportsLeafCall && !_engine._isDebugMode && !_isClassConstructor)
        {
            if (StrictModeScope.IsStrictModeCode == strict)
            {
                return CallLeaf();
            }

            using (new StrictModeScope(strict, force: true))
            {
                return CallLeaf();
            }
        }

        using (new StrictModeScope(strict, force: true))
        {
            FunctionEnvironment? funcEnv = null;

            try
            {
                ref readonly var calleeContext = ref PrepareForOrdinaryCall(Undefined, state);

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
                    OrdinaryCallBindThis(calleeContext, thisObject);
                }

                // actual call
                var context = _engine._activeEvaluationContext ?? new EvaluationContext(_engine);

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
                    Throw.JavaScriptException(_engine, result.Value, result);
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
                _engine.LeaveExecutionContext();
            }

            return Undefined;
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
    private JsValue CallLeaf()
    {
        var engine = _engine;
        engine.EnterLeafCallExecutionContext(_scriptOrModule, _environment!, _privateEnvironment, _realm, this);
        try
        {
            var context = engine._activeEvaluationContext ?? new EvaluationContext(engine);
            var result = _functionDefinition!.EvaluateLeafBody(context);

            if (result.Type == CompletionType.Throw)
            {
                Throw.JavaScriptException(engine, result.Value, result);
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
    {
        var state = _functionDefinition!.Initialize();
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

        ref readonly var calleeContext = ref PrepareForOrdinaryCall(newTarget, state);
        var constructorEnv = (FunctionEnvironment) calleeContext.LexicalEnvironment;

        var strict = _thisMode == FunctionThisMode.Strict;
        using (new StrictModeScope(strict, force: true))
        {
            try
            {
                if (kind == ConstructorKind.Base)
                {
                    OrdinaryCallBindThis(calleeContext, thisArgument);
                    ((ObjectInstance) thisArgument).InitializeInstanceElements(this);
                }

                var context = _engine._activeEvaluationContext ?? new EvaluationContext(_engine);

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

                if (result.Type == CompletionType.Return)
                {
                    if (result.Value is ObjectInstance oi)
                    {
                        return oi;
                    }

                    if (kind == ConstructorKind.Base)
                    {
                        return (ObjectInstance) thisArgument;
                    }

                    if (!result.Value.IsUndefined())
                    {
                        Throw.TypeError(callerContext.Realm);
                    }
                }
                else if (result.Type == CompletionType.Throw)
                {
                    Throw.JavaScriptException(_engine, result.Value, result);
                }
            }
            finally
            {
                _engine.LeaveExecutionContext();
            }
        }

        return (ObjectInstance) constructorEnv.GetThisBinding();
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
