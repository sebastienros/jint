using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using Jint.Native;
using Jint.Native.Function;
using Jint.Native.Object;
using Jint.Runtime.CallStack;
using Jint.Runtime.Environments;
using Environment = Jint.Runtime.Environments.Environment;

namespace Jint.Runtime.Interpreter.Expressions;

internal sealed class JintCallExpression : JintExpression
{
    private readonly ExpressionCache _arguments = new();
    private readonly JintExpression _calleeExpression;

    // Callee shape is fixed at build time. Keeping it in fields instead of re-testing
    // `_calleeExpression._expression.Type == NodeType.Super` and `_calleeExpression is
    // JintMemberExpression` on every evaluation removes two loads and a type check per call;
    // `_calleeMember` doubles as the already-narrowed receiver for the fast member-call lane.
    private readonly bool _calleeIsSuper;
    private readonly JintMemberExpression? _calleeMember;

    // Monomorphic fast-call cache: the callee seen by the last evaluation plus its verdict for this
    // site's arity. Identity is what makes the verdict safe to reuse — re-assigning the property
    // (`Math.abs = f`) swaps the value without bumping any version counter, so nothing weaker than a
    // reference compare would notice. A miss re-caches rather than declining permanently, so a site
    // that legitimately sees more than one callee over its lifetime keeps the lane.
    // NOTE: _fastCallee is a Function, i.e. engine-affine state. Handler trees are engine-owned for
    // exactly this reason (Engine._functionDefinitions / _scriptStatementLists) and must never be
    // stashed on the AST shared by a Prepared<Script> — see the INVARIANT in JintStatement.Build.
    private Function? _fastCallee;
    private FastCallShape _fastShape;

    // Argument shape is fixed at build time; only sites the fast lane could ever serve pay any probe.
    private readonly int _argCount;
    private readonly bool _fastArgsEligible;

    public JintCallExpression(CallExpression expression) : base(expression)
    {
        _arguments.Initialize(expression.Arguments.AsSpan());
        _calleeExpression = Build(expression.Callee);
        _calleeIsSuper = _calleeExpression._expression.Type == NodeType.Super;
        _calleeMember = _calleeExpression as JintMemberExpression;

        _argCount = expression.Arguments.Count;
        _fastArgsEligible = !_arguments.HasSpreads && _argCount <= 2;
    }

    protected override object EvaluateInternal(EvaluationContext context)
    {

        if (!context.Engine._stackGuard.TryEnterOnCurrentStack())
        {
            return StackGuard.RunOnEmptyStack(EvaluateInternal, context);
        }

        if (_calleeIsSuper)
        {
            return SuperCall(context);
        }

        // https://tc39.es/ecma262/#sec-function-calls

        var engine = context.Engine;

        // The frame's suspendable is fixed for the duration of this evaluation (nested calls
        // balance their context push/pop), so capture it once and probe the reference instead
        // of re-reading the execution context after every sub-expression.
        var suspendable = engine.ExecutionContext.Suspendable;

        object reference;
        Reference? referenceRecord;
        JsValue func;
        JsValue thisObject;

        // Fast path: obj.method() / this.method() where the receiver is a plain identifier or `this`
        // and the property is a literal name. Reuse the member expression's own-property inline cache
        // to resolve the callee and `this` without renting a Reference. Only callables take the fast
        // path; a non-callable result falls through to the Reference path so the exact "Property 'x'
        // of object is not a function" error and this-binding are preserved (the identifier/`this`
        // receiver is side-effect-free, so re-evaluating it on that rare path is unobservable).
        JsValue? fastFunc = null;
        var fastThis = JsValue.Undefined;
        var member = _calleeMember;
        // Only a resolver watching object/primitive property bases has to disarm this: GetCalleeForCall
        // returns Undefined for a null/undefined receiver and for a non-callable result, and both of those
        // fall through to the Reference path where CheckCoercible / TryUnresolvableReference / TryGetCallable
        // still run.
        if (member is not null
            && member.IsFastCallEligible
            && !engine._resolverWatchesValueBase)
        {
            fastFunc = member.GetCalleeForCall(context, out fastThis);
            if (suspendable is not null && suspendable.IsSuspended)
            {
                return fastFunc;
            }
            if (!IsCallableFlagged(fastFunc))
            {
                fastFunc = null;
            }
        }

        if (fastFunc is not null)
        {
            func = fastFunc;
            thisObject = fastThis;
            referenceRecord = null;
            reference = fastFunc;
        }
        else
        {
            var calleeReference = _calleeExpression.Evaluate(context);

            // Narrowed up front rather than after the exits below, so every one of them can hand the
            // rented Reference back. GetValue is called with returnReferenceToPool: false because the
            // reference is still needed for the this-binding and for the "not a function" messages, so
            // releasing it is this method's job on every path it can leave by.
            referenceRecord = calleeReference as Reference;

            // Check for generator suspension after evaluating callee
            if (suspendable is not null && suspendable.IsSuspended)
            {
                // Resume re-evaluates the callee and rents its own reference, so this one is done with.
                engine._referencePool.Return(referenceRecord);
                return calleeReference as JsValue ?? JsValue.Undefined;
            }

            if (ReferenceEquals(calleeReference, JsValue.Undefined))
            {
                return JsValue.Undefined;
            }

            func = engine.GetValue(calleeReference, false);

            if (func.IsNullOrUndefined() && _expression.IsOptional())
            {
                engine._referencePool.Return(referenceRecord);
                return JsValue.Undefined;
            }

            if (ReferenceEquals(func, engine.Realm.Intrinsics.Eval)
                && referenceRecord != null
                && !referenceRecord.IsPropertyReference
                && CommonProperties.Eval.Equals(referenceRecord.ReferencedName))
            {
                return HandleEval(context, func, engine, referenceRecord);
            }

            // https://tc39.es/ecma262/#sec-evaluatecall

            if (referenceRecord is not null)
            {
                if (referenceRecord.IsPropertyReference)
                {
                    thisObject = referenceRecord.ThisValue;
                }
                else
                {
                    var baseValue = referenceRecord.Base;

                    // deviation from the spec to support null-propagation helper;
                    // since the unresolvable reference base is a sentinel (not undefined), also
                    // consult the resolver for unresolvable references so a call to an undefined
                    // name is routed through it instead of casting the sentinel to an Environment
                    if (engine._resolverWatchesUnresolvable
                        && (baseValue.IsNullOrUndefined() || referenceRecord.IsUnresolvableReference)
                        && engine._referenceResolver.TryUnresolvableReference(engine, referenceRecord, out var value))
                    {
                        thisObject = value;
                    }
                    else
                    {
                        var refEnv = (Environment) baseValue;
                        thisObject = refEnv.WithBaseObject();
                    }
                }
            }
            else
            {
                thisObject = JsValue.Undefined;
            }

            reference = calleeReference;
        }

        // Fast-call lane. Gated on callee identity, so a re-assigned or shadowed built-in, a different
        // engine's instance, or any non-built-in callee simply misses and takes the path below.
        // Debug mode is excluded because the debugger walks the call stack, and generator/async frames
        // are excluded because argument evaluation there must go through ExpressionCache's resume
        // buffer rather than straight into locals.
        if (_fastArgsEligible
            && suspendable is null
            && ReferenceEquals(func, _fastCallee)
            && _fastShape.Supported
            && !engine._isDebugMode)
        {
            var arg0 = _argCount >= 1 ? _arguments.GetValue(context, 0) : JsValue.Undefined;
            var arg1 = _argCount >= 2 ? _arguments.GetValue(context, 1) : JsValue.Undefined;

            // Everything the reference was kept for has been read (the this-binding above; the
            // "not a function" messages below cannot be reached from here, the callee is callable).
            if (referenceRecord is not null)
            {
                engine._referencePool.Return(referenceRecord);
            }

            return FastCall(engine, Unsafe.As<Function>(func), thisObject, arg0, arg1);
        }

        var tailCall = IsInTailPosition((CallExpression) _expression);

        var arguments = this._arguments.ArgumentListEvaluation(context, this, out var rented);

        // Check for generator suspension after argument evaluation
        if (suspendable is not null && suspendable.IsSuspended)
        {
            // When suspended mid-arglist, ExpressionCache keeps the array alive
            // in suspend data and returns rented=false, so we don't release it here.
            if (rented && arguments is not null)
            {
                engine._jsValueArrayPool.ReturnArray(arguments);
            }

            // Resume re-evaluates the callee and rents its own reference, so this one is done with.
            engine._referencePool.Return(referenceRecord);
            return func; // Return any value, caller will check Suspended
        }

        if (!func.IsObject()
            && (!engine._resolverWatchesCallee || !engine._referenceResolver.TryGetCallable(engine, reference, out func)))
        {
            ThrowMemberIsNotFunction(referenceRecord, reference, engine);
        }

        if (!IsCallableFlagged(func))
        {
            ThrowReferenceNotFunction(referenceRecord, reference, engine);
        }

        var callable = Unsafe.As<ICallable>(func);

        if (tailCall)
        {
            // TODO tail call
            // PrepareForTailCall();
        }

        // ensure logic is in sync between Call, Construct and JintCallExpression!

        JsValue result;
        if (IsFunctionFlagged(func))
        {
            var functionInstance = Unsafe.As<Function>(func);
            var callStack = engine.CallStack;
            var recursionDepth = callStack.Push(functionInstance, _calleeExpression, engine.ExecutionContext);

            if (recursionDepth > engine._maxRecursionDepth)
            {
                // automatically pops the current element as it was never reached
                Throw.RecursionDepthOverflowException(callStack);
            }

            try
            {
                result = functionInstance.Call(thisObject, arguments);
            }
            finally
            {
                // if call stack was reset due to recursive call to engine or similar, we might not have it anymore
                if (callStack.Count > 0)
                {
                    callStack.Pop();
                }
            }

            // Populate the fast-call cache after a successful dispatch, so the *next* evaluation of
            // this site can take the lane above. Deliberately after the call: asking the callee for
            // its shape is only worth it once we know this site actually reaches a Function.
            if (_fastArgsEligible && !ReferenceEquals(functionInstance, _fastCallee))
            {
                _fastCallee = functionInstance;
                _fastShape = functionInstance.GetFastCallShape(_argCount);
            }
        }
        else
        {
            result = callable.Call(thisObject, arguments);
        }

        if (rented)
        {
            engine._jsValueArrayPool.ReturnArray(arguments);
        }

        // The fast member-call lane never rents a Reference, so skip the call on that path rather
        // than entering Return only to have it discover the null.
        if (referenceRecord is not null)
        {
            engine._referencePool.Return(referenceRecord);
        }

        return result;
    }

    /// <summary>
    /// Whether <paramref name="value"/> is a <see cref="Function"/>, decided by the
    /// <see cref="InternalTypes.Function"/> flag instead of a class-hierarchy walk. Only functions get a
    /// call-stack frame, so this runs on every dispatched call; <see cref="Function"/> is abstract with
    /// many subclasses, which is exactly the shape `is` resolves via <c>CastHelpers.IsInstanceOfClass</c>.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool IsFunctionFlagged(JsValue value)
    {
        var flagged = (value._type & InternalTypes.Function) != InternalTypes.Empty;
        Debug.Assert(flagged == value is Function, $"InternalTypes.Function disagrees with `is Function` for {value.GetType()}");
        return flagged;
    }

    /// <summary>
    /// Whether <paramref name="value"/> implements <see cref="ICallable"/>, decided by the
    /// <see cref="InternalTypes.Callable"/> flag instead of an `is ICallable` interface-map scan.
    /// Every <see cref="ICallable"/> root sets the flag in its constructor, so the two answers are
    /// equivalent by construction — asserted here so a future ICallable implementer that forgets
    /// the flag trips in debug builds rather than silently losing callability.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool IsCallableFlagged(JsValue value)
    {
        var flagged = (value._type & InternalTypes.Callable) != InternalTypes.Empty;
        Debug.Assert(flagged == value is ICallable, $"InternalTypes.Callable disagrees with `is ICallable` for {value.GetType()}");
        return flagged;
    }

    /// <summary>
    /// Invokes a built-in through its arity-specialized entry point, eliding the call-stack frame
    /// only when the shape's guards hold for these exact values.
    /// </summary>
    /// <remarks>
    /// The leaf test is per-call and not per-method on purpose: <c>Math.abs(x)</c> cannot reach user
    /// code when <c>x</c> is a number, but with an object it coerces through <c>valueOf</c>, and that
    /// user code can both throw and read <c>error.stack</c> — where the built-in's own frame is
    /// observable. Anything that fails a guard keeps its frame and only takes the argument-passing
    /// half of the optimization.
    /// </remarks>
    private JsValue FastCall(Engine engine, Function target, JsValue thisObject, JsValue arg0, JsValue arg1)
    {
        if (_fastShape.IsLeafFor(thisObject, arg0, arg1))
        {
#if DEBUG
            // try/finally so a throw out of a mis-annotated built-in cannot strand the debug
            // counter above zero and poison every later assertion on this thread. Compiled out
            // rather than left to [Conditional] emptying the finally, so Release codegen on the
            // engine's fastest lane carries no exception-handling region at all.
            LeafCallGuard.Enter();
            try
            {
                return target.CallFast(thisObject, arg0, arg1);
            }
            finally
            {
                LeafCallGuard.Exit();
            }
#else
            return target.CallFast(thisObject, arg0, arg1);
#endif
        }

        var callStack = engine.CallStack;
        var recursionDepth = callStack.Push(target, _calleeExpression, engine.ExecutionContext);

        if (recursionDepth > engine._maxRecursionDepth)
        {
            Throw.RecursionDepthOverflowException(callStack);
        }

        try
        {
            return target.CallFast(thisObject, arg0, arg1);
        }
        finally
        {
            if (callStack.Count > 0)
            {
                callStack.Pop();
            }
        }
    }

    [DoesNotReturn]
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void ThrowReferenceNotFunction(Reference? referenceRecord1, object reference, Engine engine)
    {
        var message = $"{referenceRecord1?.ReferencedName ?? reference} is not a function";
        Throw.TypeError(engine.Realm, message);
    }

    [DoesNotReturn]
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void ThrowMemberIsNotFunction(Reference? referenceRecord1, object reference, Engine engine)
    {
        var message = referenceRecord1 == null
            ? reference + " is not a function"
            : $"Property '{referenceRecord1.ReferencedName}' of object is not a function";
        Throw.TypeError(engine.Realm, message);
    }

    private JsValue HandleEval(EvaluationContext context, JsValue func, Engine engine, Reference referenceRecord)
    {
        var argList = _arguments.ArgumentListEvaluation(context, this, out var rented);

        if (argList.Length == 0)
        {
            engine._referencePool.Return(referenceRecord);
            return JsValue.Undefined;
        }

        var evalFunctionInstance = (EvalFunction) func;
        var evalArg = argList[0];
        var strictCaller = engine.ExecutionContext.Strict;
        var evalRealm = evalFunctionInstance._realm;
        var direct = !_expression.IsOptional();
        var value = evalFunctionInstance.PerformEval(evalArg, evalRealm, strictCaller, direct);

        if (rented)
        {
            engine._jsValueArrayPool.ReturnArray(argList);
        }
        engine._referencePool.Return(referenceRecord);

        return value;
    }

    private ObjectInstance SuperCall(EvaluationContext context)
    {
        var engine = context.Engine;
        var thisEnvironment = (FunctionEnvironment) engine.ExecutionContext.GetThisEnvironment();
        var newTarget = engine.GetNewTarget(thisEnvironment);
        var func = GetSuperConstructor(thisEnvironment);

        var rented = false;
        var defaultSuperCall = ReferenceEquals(_expression, ClassDefinition._defaultSuperCall);

        var argList = defaultSuperCall
            ? _arguments.DefaultSuperCallArgumentListEvaluation(context)
            : _arguments.ArgumentListEvaluation(context, this, out rented);

        if (func is null || !func.IsConstructor)
        {
            if (rented)
            {
                engine._jsValueArrayPool.ReturnArray(argList);
            }
            Throw.TypeError(engine.Realm, "Not a constructor");
        }

        var result = ((IConstructor) func).Construct(argList, newTarget);

        var thisER = (FunctionEnvironment) engine.ExecutionContext.GetThisEnvironment();
        thisER.BindThisValue(result);
        var F = thisER._functionObject;

        result.InitializeInstanceElements((ScriptFunction) F);

        if (rented)
        {
            engine._jsValueArrayPool.ReturnArray(argList);
        }

        return result;
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-getsuperconstructor
    /// </summary>
    private static ObjectInstance? GetSuperConstructor(FunctionEnvironment thisEnvironment)
    {
        var envRec = thisEnvironment;
        var activeFunction = envRec._functionObject;
        var superConstructor = activeFunction.GetPrototypeOf();
        return superConstructor;
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-isintailposition
    /// </summary>
    private static bool IsInTailPosition(CallExpression call)
    {
        // TODO tail calls
        return false;
    }
}
