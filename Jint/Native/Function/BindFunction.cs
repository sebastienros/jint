using Jint.Native.Object;
using Jint.Native.Symbol;
using Jint.Runtime;

namespace Jint.Native.Function;

/// <summary>
/// https://tc39.es/ecma262/#sec-bound-function-exotic-objects
/// </summary>
/// <remarks>
/// A <see cref="Function"/> because that is what it is: a bound function exotic object has
/// <c>[[Call]]</c>, <c>[[Construct]]</c>, a <c>name</c> and a <c>length</c>, and every describer, console
/// and debugger protocol that asks "is this a function?" asks it by type. It carries no
/// <c>[[ECMAScriptCode]]</c>, so it has no source text, no <c>prototype</c> property and no environment;
/// everything it does it delegates to <see cref="BoundTargetFunction"/>.
/// </remarks>
public sealed class BindFunction : Function, IConstructor
{
    /// <summary>
    /// Internal because a <see cref="Realm"/> is not something a host can obtain, so nothing outside this
    /// assembly could ever have called it. The way to bind a function is <c>Function.prototype.bind</c>; the
    /// three properties below are what a host does with a bound function it is <i>handed</i>.
    /// </summary>
    internal BindFunction(Engine engine,
        Realm realm,
        ObjectInstance? proto,
        ObjectInstance targetFunction,
        JsValue boundThis,
        JsValue[] boundArgs)
        : base(engine, realm, name: null)
    {
        _prototype = proto;
        BoundTargetFunction = targetFunction;
        BoundThis = boundThis;
        BoundArguments = boundArgs;
    }

    /// <summary>
    /// The wrapped function object.
    /// </summary>
    public JsValue BoundTargetFunction { get; }

    /// <summary>
    /// The value that is always passed as the this value when calling the wrapped function.
    /// </summary>
    public JsValue BoundThis { get; }

    /// <summary>
    /// A list of values whose elements are used as the first arguments to any call to the wrapped function.
    /// </summary>
    public JsValue[] BoundArguments { get; }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-bound-function-exotic-objects-call-thisargument-argumentslist
    /// </summary>
    protected internal override JsValue Call(JsValue thisObject, JsCallArguments arguments)
    {
        _engine._stackGuard.EnsureNativeStackHeadroom();

        // Per https://tc39.es/ecma262/#sec-bound-function-exotic-objects-call-thisargument-argumentslist
        // the [[BoundTargetFunction]] only needs to be callable — it is not necessarily a
        // Function instance. Binding an already-bound function produces a BindFunction whose
        // target is another BindFunction (an ObjectInstance, not a Function), which a
        // Function-only cast rejected — making `f.bind(a).bind(b)()` throw where the spec
        // (and every other engine) calls through.
        var f = BoundTargetFunction as ICallable;
        if (f is null)
        {
            Throw.TypeError(_realm, "Bind must be called on a function");
        }

        var args = CreateArguments(arguments);
        try
        {
            return f.Call(BoundThis, args);
        }
        finally
        {
            _engine._jsValueArrayPool.ReturnArray(args);
        }
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-bound-function-exotic-objects-construct-argumentslist-newtarget
    /// </summary>
    ObjectInstance IConstructor.Construct(JsCallArguments arguments, JsValue newTarget)
    {
        _engine._stackGuard.EnsureNativeStackHeadroom();

        var target = BoundTargetFunction as IConstructor;
        if (target is null)
        {
            Throw.TypeError(_realm, "The bound target function is not a constructor");
        }

        var args = CreateArguments(arguments);

        if (ReferenceEquals(this, newTarget))
        {
            newTarget = BoundTargetFunction;
        }

        try
        {
            return target.Construct(args, newTarget);
        }
        finally
        {
            _engine._jsValueArrayPool.ReturnArray(args);
        }
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-ordinaryhasinstance step 2: a bound function answers
    /// <c>? InstanceofOperator(O, BC)</c> (https://tc39.es/ecma262/#sec-instanceofoperator) for its
    /// <c>[[BoundTargetFunction]]</c> BC — so every link of a chain of binds is asked for its own
    /// <c>@@hasInstance</c>, and a link with a method of its own answers for the whole chain below it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A loop rather than the recursion the specification writes, for the reason <see cref="IsConstructor"/> is
    /// one: a chain of binds is a linked list script can make as long as it likes, and one native frame per link
    /// ends the process on a deep enough chain. The recursion only continues through
    /// <c>%Function.prototype[@@hasInstance]%</c>, whose whole behaviour is <c>OrdinaryHasInstance(this, V)</c>
    /// (https://tc39.es/ecma262/#sec-function.prototype-@@hasinstance), so when a link's <c>GetMethod</c> finds that
    /// intrinsic — any realm's — the loop takes the next step itself instead of calling it. Everything the
    /// specification observes is still observed, in its order: the <c>GetMethod</c> of every link, through a getter
    /// or a proxy's <c>get</c> trap included.
    /// </para>
    /// <para>
    /// Any other method is called, and that call is where script can come back here with the native frames of
    /// everything below it — a method that asks <c>instanceof</c> of another bound chain is a recursion script
    /// controls. So the call is probed. The called function usually probes for itself, but on the
    /// <see cref="Options.ConstraintOptions.MaxExecutionStackCount"/> lane a script function does not, and this
    /// probe is then the only thing between such a chain and a dead process.
    /// </para>
    /// </remarks>
    internal override bool OrdinaryHasInstance(JsValue v)
    {
        // 1. IsCallable(C) holds for every bound function: Function.prototype.bind refuses a target without [[Call]].
        var c = this;
        while (true)
        {
            // 2. a. Let BC be C.[[BoundTargetFunction]].
            //    b. Return ? InstanceofOperator(O, BC).
            // InstanceofOperator 1. If target is not an Object, throw a TypeError exception.
            if (c.BoundTargetFunction is not ObjectInstance target)
            {
                Throw.TypeError(_realm, "Right-hand side of 'instanceof' is not an object");
                return false;
            }

            // InstanceofOperator 2. Let instOfHandler be ? GetMethod(target, %Symbol.hasInstance%).
            var instOfHandler = target.GetMethod(GlobalSymbolRegistry.HasInstance);
            if (instOfHandler is null)
            {
                // InstanceofOperator 4. If IsCallable(target) is false, throw a TypeError exception.
                if (!target.HasCall)
                {
                    Throw.TypeError(_realm, "Right-hand side of 'instanceof' is not callable");
                }
            }
            else if (!IsFunctionPrototypeHasInstance(instOfHandler))
            {
                // InstanceofOperator 3. If instOfHandler is not undefined, return ToBoolean(? Call(instOfHandler, target, « V »)).
                _engine._stackGuard.EnsureNativeStackHeadroom();
                return TypeConverter.ToBoolean(instOfHandler.Call(target, v));
            }

            // InstanceofOperator 5. Return ? OrdinaryHasInstance(target, V) — which is also all that calling the intrinsic
            // %Function.prototype[@@hasInstance]% with target as this would do, so both routes arrive here.
            if (target is not BindFunction bound)
            {
                return target.OrdinaryHasInstance(v);
            }

            c = bound;
        }
    }

    /// <summary>
    /// Whether <paramref name="method"/> is <c>%Function.prototype[@@hasInstance]%</c> of the realm it belongs to.
    /// Asked of the method's own realm rather than of this function's: a bound function's prototype is its target's,
    /// so a chain built by one realm's <c>bind</c> over another realm's function inherits the other realm's
    /// intrinsic, which is the same algorithm.
    /// </summary>
    private static bool IsFunctionPrototypeHasInstance(ICallable method)
    {
        return method is Function { _realm: { } realm } && realm.Intrinsics.Function.PrototypeObject.IsHasInstanceFunction(method);
    }

    private JsValue[] CreateArguments(JsCallArguments arguments)
    {
        var combined = _engine._jsValueArrayPool.RentArray(BoundArguments.Length + arguments.Length);
        System.Array.Copy(BoundArguments, combined, BoundArguments.Length);
        System.Array.Copy(arguments, 0, combined, BoundArguments.Length, arguments.Length);
        return combined;
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-boundfunctioncreate gives a bound function <c>[[Construct]]</c> exactly when
    /// its target has one, so the answer is the first non-bound link's.
    /// </summary>
    /// <remarks>
    /// A walk rather than <c>BoundTargetFunction.IsConstructor</c>: a chain of binds is a linked list script can
    /// make as long as it likes (#4130 made building one linear), and asking each link in turn cost one native
    /// frame per link with no stack probe on the way — so <c>new f()</c> on a deep enough chain ended the process
    /// before <c>[[Construct]]</c>'s own probe was reached. No link has anything to observe, so the loop is exact.
    /// </remarks>
    internal override bool IsConstructor
    {
        get
        {
            var target = BoundTargetFunction;
            while (target is BindFunction bound)
            {
                target = bound.BoundTargetFunction;
            }

            return target.IsConstructor;
        }
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-function.prototype.tostring — a bound function is not the function it
    /// wraps, so the representation names nothing, which is what every engine writes for one.
    /// </summary>
    public override string ToString() => "function () { [native code] }";
}
