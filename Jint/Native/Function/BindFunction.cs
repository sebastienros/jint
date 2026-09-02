using Jint.Native.Object;
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
        var value = f.Call(BoundThis, args);
        _engine._jsValueArrayPool.ReturnArray(args);

        return value;
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-bound-function-exotic-objects-construct-argumentslist-newtarget
    /// </summary>
    ObjectInstance IConstructor.Construct(JsCallArguments arguments, JsValue newTarget)
    {
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

        var value = target.Construct(args, newTarget);
        _engine._jsValueArrayPool.ReturnArray(args);

        return value;
    }

    internal override bool OrdinaryHasInstance(JsValue v)
    {
        // Per https://tc39.es/ecma262/#sec-instanceofoperator a bound function delegates
        // instanceof to its [[BoundTargetFunction]], which may itself be a bound function —
        // follow the chain to the underlying function.
        var target = BoundTargetFunction;
        while (target is BindFunction nested)
        {
            target = nested.BoundTargetFunction;
        }

        var f = target as Function;
        if (f is null)
        {
            Throw.TypeError(_realm, "Right-hand side of 'instanceof' is not callable");
        }

        return f.OrdinaryHasInstance(v);
    }

    private JsValue[] CreateArguments(JsCallArguments arguments)
    {
        var combined = _engine._jsValueArrayPool.RentArray(BoundArguments.Length + arguments.Length);
        System.Array.Copy(BoundArguments, combined, BoundArguments.Length);
        System.Array.Copy(arguments, 0, combined, BoundArguments.Length, arguments.Length);
        return combined;
    }

    internal override bool IsConstructor => BoundTargetFunction.IsConstructor;

    /// <summary>
    /// https://tc39.es/ecma262/#sec-function.prototype.tostring — a bound function is not the function it
    /// wraps, so the representation names nothing, which is what every engine writes for one.
    /// </summary>
    public override string ToString() => "function () { [native code] }";
}
