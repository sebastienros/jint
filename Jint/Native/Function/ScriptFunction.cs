using Jint.Native.Object;
using Jint.Runtime;
using Jint.Runtime.Descriptors;
using Jint.Runtime.Descriptors.Specialized;
using Jint.Runtime.Environments;
using Jint.Runtime.Interpreter;
using Environment = Jint.Runtime.Environments.Environment;

namespace Jint.Native.Function;

public sealed class ScriptFunction : Function, IConstructor
{
    internal bool _isClassConstructor;
    internal JsValue? _classFieldInitializerName;

    // Reuse pool for this function's call environments and fixed-slot arrays; lazily created on the first
    // pool-eligible call's return. Held per function instance (thus per engine) rather than on the shared
    // JintFunctionDefinition.State so a prepared script reused across engines never pins an engine via a
    // cached environment (issue #2560).
    internal FunctionEnvPool? _envPool;

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
        _length = new LazyPropertyDescriptor<JintFunctionDefinition>(function, static function => JsNumber.Create(function.Initialize().Length), PropertyFlag.Configurable);

        if (!function.Strict
            && function.Function is not ArrowFunctionExpression
            && !function.Function.Generator
            && !function.Function.Async)
        {
            SetProperty(KnownKeys.Arguments, new GetSetPropertyDescriptor.ThrowerPropertyDescriptor(engine, PropertyFlag.Configurable));
            SetProperty(KnownKeys.Caller, new PropertyDescriptor(Undefined, PropertyFlag.Configurable));
        }
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-ecmascript-function-objects-call-thisargument-argumentslist
    /// </summary>
    protected internal override JsValue Call(JsValue thisObject, JsCallArguments arguments)
    {
        var strict = _functionDefinition!.Strict || _thisMode == FunctionThisMode.Strict;
        using (new StrictModeScope(strict, force: true))
        {
            FunctionEnvironment? funcEnv = null;
            JintFunctionDefinition.State? state = null;

            try
            {
                ref readonly var calleeContext = ref PrepareForOrdinaryCall(Undefined);

                if (_isClassConstructor)
                {
                    Throw.TypeError(calleeContext.Realm, $"Class constructor {_functionDefinition.Name} cannot be invoked without 'new'");
                }

                // Capture funcEnv for end-of-call pool return when bindings can't escape. Direct-recursive
                // functions return their env to a small bounded pool (so each live frame reuses a distinct
                // env); other non-escaping functions use the single-slot pool. Escaping envs are not pooled.
                state = _functionDefinition.Initialize();
                if (state is { EnvironmentMayEscape: false })
                {
                    funcEnv = (FunctionEnvironment) calleeContext.LexicalEnvironment;
                }

                OrdinaryCallBindThis(calleeContext, thisObject);

                // actual call
                var context = _engine._activeEvaluationContext ?? new EvaluationContext(_engine);

                var result = _functionDefinition.EvaluateBody(context, this, arguments);

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
                    // Return to this function instance's pool (per-engine by construction, so a prepared
                    // script's shared State can't pin engines — see FunctionEnvPool). Single-threaded like
                    // the engine, so no Interlocked is needed.
                    _envPool ??= new FunctionEnvPool();
                    if (state!.IsDirectRecursive)
                    {
                        // Return the env (with its fixed-slot array still attached) to the bounded
                        // recursive pool so another simultaneously live frame can reuse env + slots.
                        if (funcEnv._slots is { } recursiveSlots)
                        {
                            System.Array.Clear(recursiveSlots, 0, recursiveSlots.Length);
                        }
                        _envPool.ReturnRecursiveEnv(funcEnv);
                    }
                    else
                    {
                        // Cache the slot array for reuse by the next call to this function.
                        if (funcEnv._slots is { } slots)
                        {
                            System.Array.Clear(slots, 0, slots.Length);
                            _envPool.CachedSlots = slots;
                            funcEnv._slots = null;
                        }

                        // Return the env itself so the next call to this function avoids the allocation.
                        _envPool.CachedEnv = funcEnv;
                    }
                }
                _engine.LeaveExecutionContext();
            }

            return Undefined;
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
        var callerContext = _engine.ExecutionContext;
        var kind = _constructorKind;

        var thisArgument = Undefined;

        if (kind == ConstructorKind.Base)
        {
            if (ReferenceEquals(newTarget, this)
                && _prototypeDescriptor is { } prototypeDescriptor
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
            // Cold constructors (below the promote threshold) stay on the dictionary path.
            if (thisArgument is JsObject thisObject && thisObject.Prototype is { } proto)
            {
                if (_ctorShaped)
                {
                    if (!ReferenceEquals(_ctorEmptyShapeProto, proto))
                    {
                        _ctorEmptyShape = _engine.GetEmptyShape(proto);
                        _ctorEmptyShapeProto = proto;
                    }
                    thisObject.StartShapeBuilding(_ctorEmptyShape!);
                }
                else if (++_ctorSampleCount >= CtorShapePromoteThreshold)
                {
                    _ctorShaped = true;
                }
            }
        }

        ref readonly var calleeContext = ref PrepareForOrdinaryCall(newTarget);
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

                var result = _functionDefinition!.EvaluateBody(context, this, arguments);
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

    internal void MakeClassConstructor()
    {
        _isClassConstructor = true;
    }
}
