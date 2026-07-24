using Jint.Native.Object;
using Jint.Runtime;

namespace Jint.Native.Function;

/// <summary>
/// https://tc39.es/ecma262/#sec-bound-function-exotic-objects
/// </summary>
public sealed class BindFunction : ObjectInstance, IConstructor, ICallable
{
    private readonly Realm _realm;

    public BindFunction(Engine engine,
        Realm realm,
        ObjectInstance? proto,
        ObjectInstance targetFunction,
        JsValue boundThis,
        JsValue[] boundArgs)
        : base(engine, ObjectClass.Function)
    {
        _type |= InternalTypes.Callable;
        _realm = realm;
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

    JsValue ICallable.Call(JsValue thisObject, params JsCallArguments arguments)
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

    ObjectInstance IConstructor.Construct(JsCallArguments arguments, JsValue newTarget)
    {
        var target = BoundTargetFunction as IConstructor;
        if (target is null)
        {
            Throw.TypeError(_realm, $"{BoundTargetFunction} is not a constructor");
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

    public override string ToString() => "function () { [native code] }";
}
