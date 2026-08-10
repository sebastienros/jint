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
    private readonly bool _isTailPosition;

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

    /// <summary>
    /// Whether this site's argument shape is one <see cref="ScriptFunction.CallFromRegisters"/> can carry,
    /// which is the half of the register lane's gate that is decidable here rather than per callee.
    /// </summary>
    /// <remarks>
    /// A zero-argument site is deliberately excluded: it rents nothing today (<see cref="ExpressionCache"/>
    /// hands back a cached empty array without renting), so there is no allocation for the lane to avoid.
    /// Spreads make the arity a runtime quantity, which the register form cannot express, however few
    /// arguments a particular spread produces.
    /// </remarks>
    private readonly bool _regLaneEligible;

    // Monomorphic register-call cache for interpreted callees, deliberately NOT merged with
    // _fastCallee/_fastShape above. The built-in lane's fixed-arity shapes report Supported without
    // consulting the site's arity (the emitter writes ShapeExpression(only, arity: null)), which is
    // only safe because that lane is capped at two arguments by _fastArgsEligible; this lane serves
    // four, so sharing the slots would route a four-argument call into CallFast(this, arg0, arg1)
    // and silently drop the tail.
    //
    // _regCallee is non-null only when the probe *accepted* the callee, and every static half of the
    // gate — this site's arity and spread shape, the callee's SupportsRegisterCall, the engine's
    // readonly _isDebugMode — is folded into the arming. So the dispatch test below is a single
    // reference compare, and a site that never sees an eligible callee (every built-in call, every
    // zero-argument call) compares against a field that stays null and pays nothing else.
    // _regProbedCallee is whatever callee this site last examined, accepted or not, so such a site
    // remembers the rejection instead of re-probing on every dispatch. Only a site _regLaneEligible
    // admits ever examines one: the rest cannot arm the lane whatever they are handed, so they are
    // spared both the probe and the reference they would otherwise hold for the engine's lifetime.
    //
    // NOTE: these are engine-affine, like _fastCallee. Handler trees are engine-owned for exactly
    // this reason (Engine._functionDefinitions / _scriptStatementLists) and must never be stashed on
    // the AST shared by a Prepared<Script> — see the INVARIANT in JintStatement.Build. Unlike a realm
    // intrinsic, which is rooted anyway, a ScriptFunction drags its closure environment along — the
    // same retention a warmed member-read site already has (see AGENTS.md).
    private Function? _regProbedCallee;
    private ScriptFunction? _regCallee;
    private JintFunctionDefinition.State? _regState;

    /// <summary>
    /// Widest arity the register lane serves, matching <see cref="ScriptFunction.CallFromRegisters"/>'s
    /// register count.
    /// </summary>
    private const int MaxRegisterArguments = 4;

    public JintCallExpression(CallExpression expression) : base(expression)
    {
        _arguments.Initialize(expression.Arguments.AsSpan());
        _calleeExpression = Build(expression.Callee);
        _calleeIsSuper = _calleeExpression._expression.Type == NodeType.Super;
        _calleeMember = _calleeExpression as JintMemberExpression;
        _isTailPosition = ReferenceEquals(expression.UserData, TailCallMarker.Instance);

        _argCount = expression.Arguments.Count;
        _fastArgsEligible = !_arguments.HasSpreads && _argCount <= 2;
        _regLaneEligible = !_arguments.HasSpreads && _argCount is >= 1 and <= MaxRegisterArguments;
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
        var tailPosition = _isTailPosition
            && engine.ExecutionContext.Strict
            && suspendable is null
            && !engine._isDebugMode;

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

        var tailCall = tailPosition && func is ScriptFunction;

        // Fast-call lane. Gated on callee identity, so a re-assigned or shadowed built-in, a different
        // engine's instance, or any non-built-in callee simply misses and takes the path below.
        // Debug mode is excluded because the debugger walks the call stack, and generator/async frames
        // are excluded because argument evaluation there must go through ExpressionCache's resume
        // buffer rather than straight into locals.
        if (_fastArgsEligible
            && !tailCall
            && suspendable is null
            && ReferenceEquals(func, _fastCallee)
            && _fastShape.Supported
            && !engine._isDebugMode)
        {
            // Snapshot the shape beside the guard, i.e. before any argument expression can run. An
            // argument can re-enter this very node and re-cache it against a different callee, and
            // the guards this dispatch consults must be the ones belonging to the callee it is about
            // to invoke — otherwise a re-entrant re-cache could route it under another built-in's
            // Variadic verdict, or elide a frame that error.stack can observe.
            var shape = _fastShape;

            var arg0 = _argCount >= 1 ? _arguments.GetValue(context, 0) : JsValue.Undefined;
            var arg1 = _argCount >= 2 ? _arguments.GetValue(context, 1) : JsValue.Undefined;

            // Everything the reference was kept for has been read (the this-binding above; the
            // "not a function" messages below cannot be reached from here, the callee is callable).
            if (referenceRecord is not null)
            {
                engine._referencePool.Return(referenceRecord);
            }

            return FastCall(engine, Unsafe.As<Function>(func), shape, thisObject, arg0, arg1);
        }

        // Register-argument lane for interpreted callees, strictly additive to the built-in lane
        // above: a call that took that lane has already returned, and every other call pays exactly
        // one reference compare against a field that is null unless this site's last callee was an
        // eligible ScriptFunction. Everything else the lane needs — the argument evaluation, the
        // reference return, the frame and the dispatch — lives in RegisterLaneCall, so this method
        // grows by the guard alone. Generator/async frames are the one part of the gate that can
        // still differ between two evaluations of the same node (argument evaluation there must go
        // through ExpressionCache's resume buffer rather than straight into locals), so they stay a
        // runtime test — second, because it can only exclude a site the compare already accepted.
        if (!tailCall && ReferenceEquals(func, _regCallee) && suspendable is null)
        {
            return RegisterLaneCall(context, engine, Unsafe.As<ScriptFunction>(func), thisObject, referenceRecord);
        }

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

        if (tailCall)
        {
            if (referenceRecord is not null)
            {
                engine._referencePool.Return(referenceRecord);
            }

            return TailCallRequestPool.Rent(
                (ScriptFunction) func,
                thisObject,
                arguments,
                rented,
                _calleeExpression);
        }

        var callable = Unsafe.As<ICallable>(func);

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
                result = functionInstance is ScriptFunction scriptFunction
                    ? scriptFunction.CallWithStackFrame(thisObject, arguments)
                    : functionInstance.Call(thisObject, arguments);
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

            // Same deal for the register lane, in its own slots. Recorded against _regProbedCallee
            // rather than _regCallee so a callee the probe rejects is remembered as rejected —
            // otherwise a site whose callee never qualifies would re-probe on every dispatch.
            //
            // The build-time half of the gate is tested first, so a site the lane could never arm
            // neither probes nor remembers. Remembering is not free there: _regProbedCallee is a
            // strong reference, and a ScriptFunction drags its closure environment along, so a site
            // with five arguments or a spread would pin one such graph for the engine's lifetime in
            // exchange for a lane it can never take.
            if (_regLaneEligible && !ReferenceEquals(functionInstance, _regProbedCallee))
            {
                ProbeRegisterCallee(engine, functionInstance);
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
    /// Decides whether <paramref name="functionInstance"/> can be dispatched through
    /// <see cref="ScriptFunction.CallFromRegisters"/> and records the verdict for this site. Kept out
    /// of line because it runs at most once per distinct callee, on the already-slow generic path,
    /// and because everything it settles here is one reference compare at dispatch time.
    /// </summary>
    /// <remarks>
    /// Only reached for a site whose argument shape can arm the lane at all — see
    /// <see cref="_regLaneEligible"/>, which the caller tests first — so what is left to settle is
    /// the callee, plus <c>Engine._isDebugMode</c>. That one is <c>readonly</c> on an engine whose
    /// handler trees are its own, so an engine that debugs simply never arms the lane: the same
    /// answer a per-dispatch test would give, reached once per callee instead of per call.
    /// </remarks>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private void ProbeRegisterCallee(Engine engine, Function functionInstance)
    {
        _regProbedCallee = functionInstance;
        _regCallee = null;
        _regState = null;

        if (engine._isDebugMode)
        {
            return;
        }

        if (functionInstance is not ScriptFunction { _isClassConstructor: false } scriptFunction)
        {
            return;
        }

        // Initialize() is what the callee's own [[Call]] would do first anyway, and the State it
        // returns is stored on the (immutable) AST node, so the reference stays valid for as long as
        // this callee does.
        var state = scriptFunction._functionDefinition!.Initialize();
        if (!state.SupportsRegisterCall)
        {
            return;
        }

        _regState = state;
        _regCallee = scriptFunction;
    }

    /// <summary>
    /// Invokes an interpreted callee through its register-argument entry point, evaluating this
    /// site's arguments straight into locals instead of into a rented <c>JsCallArguments</c>. The
    /// call-stack frame is deliberately kept: unlike the built-in lane's leaf shapes, a script body
    /// can throw and read <c>error.stack</c>, where its own frame is observable.
    /// </summary>
    /// <remarks>
    /// <paramref name="target"/> arrives as a parameter and <see cref="_regState"/> is read as the
    /// first statement, i.e. both are snapshotted before any argument expression can run. That
    /// ordering is required, not incidental: an argument can re-enter this very node —
    /// <c>function h() { return f(h()); }</c> recurses through the same handler — and a re-entrant
    /// evaluation taking the generic path re-probes and overwrites the cache, so reading the state
    /// afterwards could hand this frame another function's slot layout. <c>_argCount</c> is readonly
    /// and safe to read at any point.
    /// </remarks>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private JsValue RegisterLaneCall(
        EvaluationContext context,
        Engine engine,
        ScriptFunction target,
        JsValue thisObject,
        Reference? referenceRecord)
    {
        var state = _regState!;

        // Arming requires at least one argument, so the first read is unconditional.
        var arg0 = _arguments.GetValue(context, 0);
        var arg1 = _argCount >= 2 ? _arguments.GetValue(context, 1) : JsValue.Undefined;
        var arg2 = _argCount >= 3 ? _arguments.GetValue(context, 2) : JsValue.Undefined;
        var arg3 = _argCount >= 4 ? _arguments.GetValue(context, 3) : JsValue.Undefined;

        // Everything the reference was kept for has been read (the this-binding at the call site; the
        // "not a function" messages cannot be reached from here, the callee is callable).
        if (referenceRecord is not null)
        {
            engine._referencePool.Return(referenceRecord);
        }

        var callStack = engine.CallStack;
        var recursionDepth = callStack.Push(target, _calleeExpression, engine.ExecutionContext);

        if (recursionDepth > engine._maxRecursionDepth)
        {
            // automatically pops the current element as it was never reached
            Throw.RecursionDepthOverflowException(callStack);
        }

        try
        {
            return target.CallFromRegisters(
                thisObject,
                arg0,
                arg1,
                arg2,
                arg3,
                _argCount,
                state,
                ownsFrame: true);
        }
        finally
        {
            // if call stack was reset due to recursive call to engine or similar, we might not have it anymore
            if (callStack.Count > 0)
            {
                callStack.Pop();
            }
        }
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
    /// <para>
    /// <paramref name="shape"/> arrives as a parameter rather than being re-read from
    /// <see cref="_fastShape"/>, because by this point the site's arguments have been evaluated and
    /// one of them may have re-entered this node and re-cached it against a different callee. The
    /// guards consulted here must belong to <paramref name="target"/>.
    /// </para>
    /// </remarks>
    private JsValue FastCall(Engine engine, Function target, FastCallShape shape, JsValue thisObject, JsValue arg0, JsValue arg1)
    {
        if (shape.Variadic)
        {
            return FastCallVariadic(engine, target, shape, thisObject, arg0, arg1);
        }

        if (shape.IsLeafFor(thisObject, arg0, arg1))
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

#if NET8_0_OR_GREATER
    /// <summary>
    /// Stack storage for the argument span a variadic built-in receives. It exists only because a
    /// <see cref="ReadOnlySpan{T}"/> needs contiguous memory and <see cref="JsValue"/> is a managed
    /// type, so <c>stackalloc</c> cannot supply it; the length matches the fast-call lane's two
    /// argument registers, which is the widest site the lane serves.
    /// </summary>
    [InlineArray(2)]
    private struct TwoArguments
    {
        private JsValue _element0;
    }
#endif

    /// <summary>
    /// The <see cref="FastCallShape.Variadic"/> lane: same dispatch as <see cref="FastCall"/>, but the
    /// built-in takes a <c>[Rest]</c> tail and so is handed a span sized to the site's real arity.
    /// The site's arity is a build-time constant and spreads are excluded, which is what makes the
    /// span's length knowable without walking an argument array.
    /// </summary>
    /// <remarks>
    /// On runtimes with inline arrays the buffer is stack memory and the lane allocates nothing;
    /// elsewhere it falls back to the same pooled array the framed path would have rented, which
    /// costs what today's path costs rather than adding to it.
    /// </remarks>
    private JsValue FastCallVariadic(Engine engine, Function target, FastCallShape shape, JsValue thisObject, JsValue arg0, JsValue arg1)
    {
        var count = _argCount;

#if NET8_0_OR_GREATER
        TwoArguments buffer = default;
        buffer[0] = arg0;
        buffer[1] = arg1;
        return InvokeVariadic(engine, target, shape, thisObject, arg0, arg1, ((ReadOnlySpan<JsValue>) buffer).Slice(0, count));
#else
        var rented = engine._jsValueArrayPool.RentArray(count);
        if (count >= 1)
        {
            rented[0] = arg0;
        }
        if (count >= 2)
        {
            rented[1] = arg1;
        }

        try
        {
            return InvokeVariadic(engine, target, shape, thisObject, arg0, arg1, new ReadOnlySpan<JsValue>(rented, 0, count));
        }
        finally
        {
            engine._jsValueArrayPool.ReturnArray(rented);
        }
#endif
    }

    /// <summary>
    /// The frame decision for the variadic lane, kept identical to <see cref="FastCall"/>'s: the
    /// guards are consulted before the frameless branch is entered, and the debug leaf audit brackets
    /// it, so a mis-annotated variadic built-in trips the same assertions as any other.
    /// </summary>
    private JsValue InvokeVariadic(
        Engine engine,
        Function target,
        FastCallShape shape,
        JsValue thisObject,
        JsValue arg0,
        JsValue arg1,
        ReadOnlySpan<JsValue> arguments)
    {
        if (shape.IsLeafFor(thisObject, arg0, arg1))
        {
#if DEBUG
            LeafCallGuard.Enter();
            try
            {
                return target.CallFastVariadic(thisObject, arguments);
            }
            finally
            {
                LeafCallGuard.Exit();
            }
#else
            return target.CallFastVariadic(thisObject, arguments);
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
            return target.CallFastVariadic(thisObject, arguments);
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

}

internal sealed class TailCallMarker
{
    internal static readonly TailCallMarker Instance = new();

    private TailCallMarker()
    {
    }
}
